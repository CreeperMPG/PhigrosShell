using CreeperMPG.PhiKits.Save.CloudStorage;
using CreeperMPG.PhiKits.Save.Taptap;
using PhigrosShell.Mapping;
using PhigrosShell.Utils;

namespace PhigrosShell.Commands.Phigros;

internal class LoginCommand : CommandBase
{
    public override string Name => "Login";
    public override string Description => "(a.k.a. lg) Login a Phigros account with SessionToken or QRCode";

    public override bool Execute(string command, List<ShellArgument> args)
    {
        if (!command.Equals("login", StringComparison.OrdinalIgnoreCase) &&
            !command.Equals("lg", StringComparison.OrdinalIgnoreCase))
            return false;

        if (Shell.LoggedIn())
        {
            ConsoleUtils.WriteWarning("Already logged in. Use 'logout' first.");
            return true;
        }

        string? token = ConsoleUtils.GetArgumentValue(args, "token");
        bool wantsQrCode = ConsoleUtils.GetArgumentValue(args, "qrcode") != null ||
                           ConsoleUtils.GetArgumentValue(args, "qr") != null;

        if (args.Count > 0 && token != null)
            return LoginWithToken(token);

        if (wantsQrCode)
            return LoginWithQrCode();

        FluentConsole.Yellow.Line("Usage: " + command + " -token=<token>|-qr/-qrcode");
        return true;
    }

    // ── Token 登录 ──

    private static bool LoginWithToken(string token)
    {
        if (token.Length != 25)
        {
            FluentConsole.Yellow.Line(
                $"The token length is invalid (expect 25, actual {token.Length}). Check the token and try again.");
            return true;
        }

        Console.Write(Program.Localization["LoggingIn"]);
        LoadUtils.StartLoading();

        PlayerObject? player = null;
        try
        {
            player = PlayerObject.FetchAsync(token).GetAwaiter().GetResult();
        }
        catch { }

        return FinishLogin(player);
    }

    // ── 二维码登录 ──

    private static bool LoginWithQrCode()
    {
        var cancellation = new CancellationTokenSource();
        StartCancelKeyListener(cancellation);

        PlayerObject? player = null;
        try
        {
            player = new TaptapClient().LoginAsync(
                onQrCodeReady: qr =>
                {
                    ShowQrCode(qr);
                    return Task.CompletedTask;
                },
                progress: new QrStatusReporter(),
                cancellationToken: cancellation.Token).GetAwaiter().GetResult();

            Console.Write(Program.Localization["LoggingIn"]);
            LoadUtils.StartLoading();
        }
        catch (OperationCanceledException)
        {
            FluentConsole.Yellow.Line(Program.Localization["OperationCancelledByUser"]);
            return true;
        }
        catch (Exception ex)
        {
            LoadUtils.LoadingError();
            FluentConsole.Yellow.Line("Error: " + ex.Message);
            return true;
        }
        finally
        {
            // 让监听线程退出。这里刻意不 Dispose：线程可能还在读它的状态，
            // Dispose 会让它撞上 ObjectDisposedException。
            cancellation.Cancel();
        }

        return FinishLogin(player);
    }

    /// <summary>
    /// 起一个后台线程监听按键，按 Q 就取消登录。
    /// <para>
    /// <c>LoginAsync</c> 把控制台主线程一路 await 到底，主线程腾不出手抓键，只能另开线程。
    /// 取消会让 <c>LoginAsync</c> 抛 <c>OperationCanceledException</c>。
    /// </para>
    /// </summary>
    private static void StartCancelKeyListener(CancellationTokenSource cancellation)
    {
        var listener = new Thread(() =>
        {
            try
            {
                while (!cancellation.IsCancellationRequested)
                {
                    if (!Console.KeyAvailable)
                    {
                        Thread.Sleep(50);
                        continue;
                    }

                    if (Console.ReadKey(intercept: true).KeyChar is 'q' or 'Q')
                    {
                        cancellation.Cancel();
                        return;
                    }
                }
            }
            catch (InvalidOperationException)
            {
                // 输入被重定向（没有真正的控制台）时抓不到键，放弃监听即可
            }
        })
        {
            IsBackground = true,
            Name = "PhiShell.QrLoginCancel"
        };

        listener.Start();
    }

    private static void ShowQrCode(QrCodeResponse qr)
    {
        FluentConsole.Cyan.Line(Program.Localization["LoginQRCodeScanPrompt"]);
        QRUtils.OutputToConsole(qr.QrCodeUrl);
        FluentConsole.Green.Line(Program.Localization["LoginLinkPrompt", new object[] { qr.QrCodeUrl }]);
        FluentConsole.Yellow.Line(Program.Localization["LoginQRExpirePrompt", new object[] { qr.ExpiresIn }]);
        FluentConsole.NewLine();
        FluentConsole.Magenta.Line(Program.Localization["CancelWithPressing", new object[] { "Q" }]);
        FluentConsole.DarkYellow.Line(Program.Localization["LoginQRWaiting"]);
    }

    // ── 收尾 ──

    private static bool FinishLogin(PlayerObject? player)
    {
        if (player == null)
        {
            LoadUtils.LoadingError();
            FluentConsole.Yellow.Line(Program.Localization["LoginPlayerGetFailed"]);
            return true;
        }

        try
        {
            var session = ShellSession.CreateAsync(player).GetAwaiter().GetResult();
            Shell.LoginSession(session);
            LoadUtils.LoadingDone();
            ShowLoginSummary(session);
        }
        catch (Exception ex)
        {
            LoadUtils.LoadingError();
            FluentConsole.Yellow.Line(
                "Error: " + ex.Message + ". Check the token and the Internet, then try again.");
        }

        return true;
    }

    private static void ShowLoginSummary(ShellSession session)
    {
        var player = session.PlayerInfo!;
        FluentConsole.Cyan.Line(Program.Localization["LoginSuccessfully"])
            .DarkCyan.Text(Program.Localization["UserInfoNameTag"].PadRightEx(17))
            .White.Line(player.Nickname)
            .DarkCyan.Text(Program.Localization["UserInfoIDTag"].PadRightEx(17))
            .White.Line(player.ShortID)
            .DarkCyan.Text(Program.Localization["UserInfoObjectIDTag"].PadRightEx(17))
            .White.Line(player.UserObjectID)
            .DarkCyan.Text(Program.Localization["UserInfoSessionTokenTag"].PadRightEx(17))
            .White.Line(player.SessionToken)
            .DarkCyan.Text(Program.Localization["UserInfoCreateTimeTag"].PadRightEx(17))
            .White.Line(player.CreateTime);

        // 摘要解析失败的槽位 CloudSummary 为 null，那就没什么可显示的
        var firstSlot = session.SaveFiles.FirstOrDefault(s => s.Info != null);
        if (firstSlot?.Info?.CloudSummary is not { } summary) return;

        FluentConsole.DarkCyan.Text(Program.Localization["UserInfoUpdateTimeTag"].PadRightEx(17))
            .White.Line(firstSlot.Info.SaveUpdateTime)
            .Cyan.Line(Program.Localization["LoginSummaryTitle"])
            .DarkCyan.Text(Program.Localization["UserSummaryRankingScoreTag"].PadRightEx(17))
            .White.Line(summary.RankingScore)
            .DarkCyan.Text(Program.Localization["UserSummaryAvatarTag"].PadRightEx(17))
            .White.Line(summary.Avatar)
            .DarkCyan.Text(Program.Localization["UserSummaryChallengeRankTag"].PadRightEx(17))
            .White.Line(summary.Challenge);
    }

    /// <summary>
    /// 轮询状态每秒来一次，只在**变化时**打印——否则会把屏幕刷满。
    /// </summary>
    private sealed class QrStatusReporter : IProgress<QrCodeStatus>
    {
        private QrCodeStatus? _lastReported;

        public void Report(QrCodeStatus status)
        {
            if (_lastReported == status) return;
            _lastReported = status;

            switch (status)
            {
                case QrCodeStatus.AuthorizationWaiting:
                    FluentConsole.Green.Line(Program.Localization["LoginQRAuthorizationWaiting"]);
                    break;
                case QrCodeStatus.InvalidGrantCode:
                    FluentConsole.Green.Line(Program.Localization["LoginQRInvalidGrantCode"]);
                    break;
            }
        }
    }
}
