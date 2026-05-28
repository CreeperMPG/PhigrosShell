using System.Globalization;
using PhigrosShell.Utils;
using System.Text;
using PhigrosShell.Commands;
using PhigrosShell.Commands.Phigros;
using PhigrosShell.Commands.VFileSystem;
using PhigrosShell.PhiInfo;
using PhigrosShell.Services;
using PhigrosShell.VFS;

namespace PhigrosShell;

internal class Program
{
    public static readonly Dictionary<string, List<string>> UpdateLog = new()
    {
        ["1.0.0"] = new List<string>
        {
            "2025-08-03 20:34",
            "Initial release.",
            "Basic console operations and command parser.",
            "Basic commands: about, clear, config, exit, help, pause.",
            "Virtual file system (available when logged in). Commands: cd, clear-all, ls, modify, print, remove, touch",
            "Able to login Phigros account, operate save file and summary, save, and upload.",
            "Support operating save file and summary, including checking save file, syncing summary, and getting phi best.",
            "Config file support, including setting difficulty.tsv and info.tsv paths."
        },
        ["1.1.0"] = new List<string>
        {
            "2025-09-18 00:09",
            "Added QRCode Login: login -qr/-qrcode",
            "Added localization.",
            "Improved error handling and reporting.",
            "Rebuilt the structure of filesystem, added multi-slot support",
            "Merged all save operating command to 'save' command (save fetch, save phibest, save export, save upload...).",
            "Added DownloadPhiInfo command, which allows you to download TSV files from the Internet.",
            "Minor bug fixes and performance improvements."
        },
        ["1.1.1"] = new List<string>
        {
            "2026-05-11 00:28",
            "Added Alias Command, allowing users to create custom command shortcuts. alias <name> <command>; unalias <name>",
            "Display the minimum accuracy for increasing RKS when showing P3B27."
        },
        ["1.2.0"] = new List<string>
        {
            "2026-05-29 00:02",
            "Optimized code structure",
            "Added Tree command: visual directory tree with hierarchical expansion.",
            "Fixed upload bug of removing cloud save accidentally.",
            "Disallow repeated login.",
            "Minor bug fixes and improvements."
        }
    };

    public static readonly AppConfig Config = new();
    public static DifficultyTSV? DifficultyTSV;
    public static InfoTSV? InfoTSV;
    public static PhigrosArchive.Abstractions.IDifficultyProvider? DifficultyProvider => DifficultyTSV;
    public static LocalizationService Localization = new();

    public const string AppName = "PhiShell";
    public const bool IsBeta = false;
    public const string Version = "1.2.0";
    public const bool IsDebug = false;

    public static void InitTSVFiles()
    {
        Console.BackgroundColor = ConsoleColor.DarkGray;

        // Load difficulty TSV
        string? difficultyPath = Config["info.difficulty.tsv"];
        if (difficultyPath != null && File.Exists(difficultyPath))
        {
            DifficultyTSV = new DifficultyTSV(difficultyPath);
            // 刷新已加载存档中的定数
            RefreshLoadedSaveDifficulties();
        }
        else
        {
            FluentConsole.Yellow.Line(Localization["WarnDifficultyTSVNotInitialized"]);
        }

        // Load info TSV
        string? infoPath = Config["info.info.tsv"];
        if (infoPath != null && File.Exists(infoPath))
        {
            InfoTSV = new InfoTSV(infoPath);
        }
        else
        {
            FluentConsole.Yellow.Line(Localization["WarnInfoTSVNotInitialized"]);
        }

        Console.ResetColor();
    }

    /// <summary>刷新所有已加载存档的定数（TSV 下载后调用）</summary>
    public static void RefreshLoadedSaveDifficulties()
    {
        if (DifficultyTSV == null || Shell.CurrentSession == null) return;
        foreach (var slot in Shell.CurrentSession.SaveFiles)
        {
            if (slot.File?.GameRecord != null)
            {
                slot.File.GameRecord.RefreshDifficulties(DifficultyTSV);
            }
        }
    }

    private static void Main(string[] args)
    {
        Console.InputEncoding = Encoding.UTF8;
        Console.OutputEncoding = Encoding.UTF8;

        // Load localization
        Localization.Load(CultureInfo.CurrentCulture);

        if (IsBeta)
        {
            FluentConsole.Yellow.Line(Localization["BetaVersionPrompt"])
                .DarkYellow.Text("BETA - ");
        }

        FluentConsole.Blue.Line(Localization["AppTitle", new object[] { AppName, Version }]);
        Console.Title = Localization["AppTitle", new object[] { AppName, Version }];

        // Load TSV
        InitTSVFiles();

        // Register VDirectory type behaviors
        RegisterVDirectoryTypes();

        // Unhandled exception handler
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            if (e.IsTerminating)
            {
                try { Config.Save(); } catch { }
            }

            if (e.ExceptionObject is Exception ex)
                ConsoleUtils.WriteError(
                    $"{Localization["UnhandledException", new object[] { ex.Message, ex.StackTrace ?? "!!NO STACKTRACE" }]}");
            else
                ConsoleUtils.WriteError(
                    Localization["UnhandledExceptionNoObject"]);
        };

        // Register commands
        Shell.RegisterCommand(new ExitCommand());
        Shell.RegisterCommand(new ClearCommand());
        Shell.RegisterCommand(new HelpCommand());
        Shell.RegisterCommand(new AboutCommand());
        Shell.RegisterCommand(new ConfigCommand());
        Shell.RegisterCommand(new AliasCommand());
        Shell.RegisterCommand(new PauseCommand());
        Shell.RegisterCommand(new LogoutCommand());
        Shell.RegisterCommand(new LoginCommand());
        Shell.RegisterCommand(new RefreshSessionTokenCommand());
        Shell.RegisterCommand(new WhoAmICommand());
        Shell.RegisterCommand(new SaveCommand());
        Shell.RegisterCommand(new DownloadPhiInfoCommand());
        Shell.RegisterCommand(new CdCommand());
        Shell.RegisterCommand(new LsCommand());
        Shell.RegisterCommand(new ClearAllCommand());
        Shell.RegisterCommand(new PrintCommand());
        Shell.RegisterCommand(new ModifyCommand());
        Shell.RegisterCommand(new TouchCommand());
        Shell.RegisterCommand(new RemoveCommand());
        Shell.RegisterCommand(new TreeCommand());

        // Handle command-line args
        if (args.Length > 0 && args[0].Equals("-command", StringComparison.OrdinalIgnoreCase))
        {
            var values = args[1..].Select(cmd =>
                cmd.Contains(' ') ? "\"" + cmd + "\"" : cmd).ToList();
            Shell.InitShell(new List<string> { string.Join(" ", values) });
        }
        else if (args.Length > 0)
        {
            string scriptPath = args[0];
            if (File.Exists(scriptPath))
            {
                try
                {
                    var scriptLines = File.ReadAllLines(scriptPath)
                        .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("#"))
                        .Select(line => line.Trim())
                        .ToList();
                    Shell.InitShell(scriptLines);
                    return;
                }
                catch (Exception ex)
                {
                    ConsoleUtils.WriteError(
                        Localization["FailedToReadScriptFile", new object[] { ex.Message }]);
                    return;
                }
            }
            Shell.InitShell();
        }
        else
        {
            Shell.InitShell();
        }
    }

    private static void RegisterVDirectoryTypes()
    {
        // ShellSlotRoot: show RKS preview, auto-sync on modify
        VDirectoryTypeRegistry.Register<PhigrosArchive.Save.SaveSummary>(info =>
        {
            info.PreviewGenerator = obj =>
            {
                if (obj is PhigrosArchive.Save.SaveSummary summary)
                    return $"RKS: {summary.RankingScore:F4}";
                return null;
            };
        });

        VDirectoryTypeRegistry.Register<PhigrosArchive.Save.SaveCloudInfo>(info =>
        {
            info.DisallowModify = true;
        });

        // Mark read-only fields
        VDirectoryTypeRegistry.Register<PhigrosArchive.Save.Data.PhigrosRecord>(info =>
        {
            info.DisallowModify = true;
        });

        VDirectoryTypeRegistry.Register<PhigrosArchive.Save.Data.PhigrosKey>(info =>
        {
            info.DisallowModify = true;
        });
    }
}
