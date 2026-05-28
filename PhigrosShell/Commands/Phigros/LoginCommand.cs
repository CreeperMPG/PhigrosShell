using PhigrosArchive;
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

        Console.Write(Program.Localization["LoggingIn"]);
        LoadUtils.StartLoading();

        ShellSession? session = null;
        string? token = ConsoleUtils.GetArgumentValue(args, "token");

        if (args.Count > 0 && token != null)
        {
            // Token login
            if (token.Length != 25)
            {
                FluentConsole.Yellow.Line($"The token length is invalid (expect 25, actual {token.Length}). Check the token and try again.");
                return true;
            }

            try
            {
                session = ShellSession.LoginAsync(token).GetAwaiter().GetResult();
            }
            catch { }
        }
        else if (ConsoleUtils.GetArgumentValue(args, "qrcode") != null ||
                 ConsoleUtils.GetArgumentValue(args, "qr") != null)
        {
            // QR Code login
            LoadUtils.StopLoading(loadStopWriteLine: false);

            var qrResponse = Taptap.GetLoginQRCode().GetAwaiter().GetResult();
            if (qrResponse == null)
            {
                FluentConsole.Yellow.Line("Couldn't get the QR code. Check the Internet, then try again.");
                return true;
            }

            FluentConsole.Cyan.Line(Program.Localization["LoginQRCodeScanPrompt"]);
            QRUtils.OutputToConsole(qrResponse.Value.qrcode_url);
            FluentConsole.Green.Line(Program.Localization["LoginLinkPrompt", new object[] { qrResponse.Value.qrcode_url }]);
            FluentConsole.Yellow.Line(Program.Localization["LoginQRExpirePrompt", new object[] { qrResponse.Value.expires_in }]);
            FluentConsole.NewLine();
            FluentConsole.Magenta.Line(Program.Localization["CancelWithPressing", new object[] { "Q" }]);
            FluentConsole.DarkYellow.Line(Program.Localization["LoginQRWaiting"]);

            var previousStatus = QRCodeStatus.AuthorizationPending;

            while (true)
            {
                if (Console.KeyAvailable)
                {
                    char keyChar = Console.ReadKey(intercept: true).KeyChar;
                    if (keyChar == 'q' || keyChar == 'Q')
                    {
                        FluentConsole.Yellow.Line(Program.Localization["OperationCancelledByUser"]);
                        return true;
                    }
                }

                Thread.Sleep(qrResponse.Value.interval * 1000);
                var pollResult = Taptap.PollQRCode(qrResponse.Value.device_code).GetAwaiter().GetResult();
                var status = pollResult.Key;
                var qrResult = pollResult.Value;

                if (status == QRCodeStatus.Success && qrResult.HasValue)
                {
                    var userProfile = Taptap.FetchUserProfile(qrResult.Value);
                    if (userProfile == null)
                    {
                        FluentConsole.Yellow.Line(Program.Localization["LoginQRUserProfileGetFailed"]);
                        return true;
                    }

                    var phiPlayerDoc = Taptap.GetPhiPlayerInfoByTaptap(qrResult.Value, userProfile.Value)
                        .GetAwaiter().GetResult();
                    var playerInfo = PhigrosPlayerInfo.FromJson(phiPlayerDoc.RootElement, phiPlayerDoc.RootElement
                        .GetProperty("sessionToken").GetString() ?? "");
                    token = playerInfo.SessionToken;

                    Console.Write(Program.Localization["LoggingIn"]);
                    LoadUtils.StartLoading();

                    session = ShellSession.CreateFromQRAsync(playerInfo, token).GetAwaiter().GetResult();
                    break;
                }

                if (status != previousStatus)
                {
                    previousStatus = status;
                    switch (status)
                    {
                        case QRCodeStatus.Error:
                            FluentConsole.Yellow.Line(Program.Localization["LoginQRErrorWhenPolling"]);
                            return true;
                        case QRCodeStatus.AuthorizationWaiting:
                            FluentConsole.Green.Line(Program.Localization["LoginQRAuthorizationWaiting"]);
                            break;
                        case QRCodeStatus.InvalidGrantCode:
                            FluentConsole.Green.Line(Program.Localization["LoginQRInvalidGrantCode"]);
                            break;
                    }
                }
            }
        }
        else
        {
            FluentConsole.Yellow.Line("Usage: " + command + " -token=<token>|-qr/-qrcode");
        }

        if (session == null)
        {
            LoadUtils.LoadingError();
            FluentConsole.Yellow.Line(Program.Localization["LoginPlayerGetFailed"]);
            return true;
        }

        try
        {
            Shell.LoginSession(session);
            LoadUtils.LoadingDone();

            var playerInfo = session.PlayerInfo!;
            FluentConsole.Cyan.Line(Program.Localization["LoginSuccessfully"])
                .DarkCyan.Text(Program.Localization["UserInfoNameTag"].PadRightEx(17))
                .White.Line(playerInfo.Nickname)
                .DarkCyan.Text(Program.Localization["UserInfoIDTag"].PadRightEx(17))
                .White.Line(playerInfo.ShortID)
                .DarkCyan.Text(Program.Localization["UserInfoObjectIDTag"].PadRightEx(17))
                .White.Line(playerInfo.UserObjectID)
                .DarkCyan.Text(Program.Localization["UserInfoSessionTokenTag"].PadRightEx(17))
                .White.Line(playerInfo.SessionToken)
                .DarkCyan.Text(Program.Localization["UserInfoCreateTimeTag"].PadRightEx(17))
                .White.Line(playerInfo.CreateTime);

            if (session.SaveFiles.Count > 0 && session.SaveFiles[0].Info != null)
            {
                var summary = session.SaveFiles[0].Info.Summary;
                FluentConsole.DarkCyan.Text(Program.Localization["UserInfoUpdateTimeTag"].PadRightEx(17))
                    .White.Line(session.SaveFiles[0].Info.CloudInfo?.SaveUpdateTime ?? "[?]")
                    .Cyan.Line(Program.Localization["LoginSummaryTitle"])
                    .DarkCyan.Text(Program.Localization["UserSummaryRankingScoreTag"].PadRightEx(17))
                    .White.Line(summary.RankingScore)
                    .DarkCyan.Text(Program.Localization["UserSummaryAvatarTag"].PadRightEx(17))
                    .White.Line(summary.Avatar ?? "[?]")
                    .DarkCyan.Text(Program.Localization["UserSummaryChallengeRankTag"].PadRightEx(17))
                    .White.Line(summary.Challenge);
            }
        }
        catch (Exception ex)
        {
            LoadUtils.LoadingError();
            FluentConsole.Yellow.Line("Error: " + ex.Message + ". Check the token and the Internet, then try again.");
        }

        return true;
    }
}
