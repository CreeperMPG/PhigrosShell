using System.Globalization;
using PhigrosShell.Utils;
using System.Text;
using CreeperMPG.PhiKits.Save.Additions;
using CreeperMPG.PhiKits.Save.CloudStorage;
using CreeperMPG.PhiKits.Save.Data;
using CreeperMPG.PhiKits.Save.Data.SaveEntries;
using PhigrosShell.Commands;
using PhigrosShell.Commands.Phigros;
using PhigrosShell.Commands.VFileSystem;
using PhigrosShell.PhiInfo;
using PhigrosShell.Services;
using PhigrosShell.VFS;

namespace PhigrosShell;

internal class Program
{
    public static readonly AppConfig Config = new();

    /// <summary>定数提供者（从 difficulty.tsv 载入）；未配置时为 null</summary>
    public static TsvDifficultyProvider? DifficultyProvider;

    public static InfoTSV? InfoTSV;
    public static LocalizationService Localization = new();

    /// <summary>
    /// 更新日志。数据在 <c>Resources/changelog/&lt;语言&gt;.json</c>，
    /// **不放在 lang 文件里**——那里是扁平的键值对，表达不了"版本 → 条目"的结构。
    /// </summary>
    public static ChangelogService Changelog = new();

    public const string AppName = "PhiShell";
    public const bool IsBeta = false;
    public const string Version = "1.3.2";
    public const bool IsDebug = false;

    public static void InitTSVFiles()
    {
        Console.BackgroundColor = ConsoleColor.DarkGray;

        string? difficultyPath = Config["info.difficulty.tsv"];
        if (difficultyPath != null && File.Exists(difficultyPath))
            DifficultyProvider = TsvDifficultyProvider.FromFile(difficultyPath);
        else
            FluentConsole.Yellow.Line(Localization["WarnDifficultyTSVNotInitialized"]);

        string? infoPath = Config["info.info.tsv"];
        if (infoPath != null && File.Exists(infoPath))
            InfoTSV = new InfoTSV(infoPath);
        else
            FluentConsole.Yellow.Line(Localization["WarnInfoTSVNotInitialized"]);

        Console.ResetColor();
    }

    private static void Main(string[] args)
    {
        Console.InputEncoding = Encoding.UTF8;
        Console.OutputEncoding = Encoding.UTF8;

        // Load localization
        Localization.Load(CultureInfo.CurrentCulture);

        // 更新日志跟着同一套语言设置走
        Changelog.Load(CultureInfo.CurrentCulture);

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

    /// <summary>
    /// 注册 VDirectory 的类型行为：**只读保护**、**写操作白名单**、**预览生成器**。
    /// <para>
    /// 预览的原则：**这一层光看目录名看不出所以然时才加**。所以给"自带好 <c>ToString</c>"
    /// 或"有几个关键数字"的类型加，而不给 <c>PhigrosSettings</c> 那种一屏音量的类型加。
    /// </para>
    /// <para>
    /// 白名单的原则：**默认全禁，按需显式开**——写操作的"忘了配"不该变成"能乱来"。
    /// </para>
    /// </summary>
    private static void RegisterVDirectoryTypes()
    {
        // ⚠️ 每个类型**只注册一次**，把它的所有行为写在一起。
        // （注册表的 Register 现在会累加而不是覆盖，但还是别给自己留看漏的机会。）

        // 云端元信息：只读保护 + 预览（列表里最想知道"哪条是新的"）
        VDirectoryTypeRegistry.Register<SaveInfoObject>(info =>
        {
            info.DisallowModify = true;
            info.PreviewGenerator = obj => obj is SaveInfoObject cloud
                ? $"Updated: {cloud.SaveUpdateTime}"
                : null;
        });

        // 单条成绩：
        //   CanInstantiate —— touch 时能 new 一个空的（全 0），用户再用 modify 填分
        //   CanResetToNull —— rm 时能把这一条成绩清空（置 null）
        //   PreviewGenerator —— 比裸的 ToString（完整类型名）有用得多
        // ⚠️ CanResetToNull 检查的是**被置 null 的那个值的类型**：
        //    SongDifficultySet<T>.EZ 的类型是 T（这里是 LevelRecord），不是容器本身，
        //    所以要挂在 LevelRecord 上。
        VDirectoryTypeRegistry.Register<LevelRecord>(info =>
        {
            info.CanInstantiate = true;
            info.CanResetToNull = true;
            info.PreviewGenerator = obj => obj is LevelRecord record
                ? $"{record.Score:D7} {record.Rank} {record.Acc:F2}%"
                : null;
        });

        // 难度容器：
        //   CanInstantiate —— 往 GameRecord 里 touch 一首新歌（new 一个 5 难度全 null 的容器）
        //   CanResetToNull —— 万一有 SongDifficultySet 类型的属性需要清空
        VDirectoryTypeRegistry.Register(typeof(SongLevelSet<>), info =>
        {
            info.CanInstantiate = true;
            info.CanResetToNull = true;
            info.PreviewGenerator = BuildDifficultySetPreview();
        });

        VDirectoryTypeRegistry.Register<SaveSummary>(info =>
        {
            info.PreviewGenerator = obj => obj is SaveSummary summary
                ? $"RKS: {summary.RankingScore:F4}"
                : null;
        });

        // 通关统计：ToString 已经是 "Cleared/FullCombo/Phi"
        VDirectoryTypeRegistry.Register<Achievement>(info =>
        {
            info.PreviewGenerator = obj => obj?.ToString();
        });

        // 货币：ToString 是 "xPiB xTiB ... xKiB"
        VDirectoryTypeRegistry.Register<PhiData>(info =>
        {
            info.PreviewGenerator = obj => obj?.ToString();
        });
    }

    /// <summary>
    /// 难度容器的预览：展开成 <c>EZ:... HD:... IN:...</c>，null 的难度跳过。
    /// <para>
    /// 元素用**它自己注册的**预览生成器渲染，而不是 <c>ToString</c>——
    /// 否则 <see cref="LevelRecord"/> 这种没重载 <c>ToString</c> 的类型会显示出完整类型名。
    /// 元素类型没注册预览时返回 null（不显示预览），避免糊出一长串没意义的东西。
    /// </para>
    /// </summary>
    private static Func<object, string?> BuildDifficultySetPreview() => obj =>
    {
        var elementType = obj.GetType().GetGenericArguments().FirstOrDefault();
        if (elementType == null) return null;

        var elementPreview = VDirectoryTypeRegistry.GetInfo(elementType)?.PreviewGenerator;
        if (elementPreview == null) return null;

        var parts = new List<string>();
        foreach (var (name, value) in EnumerateDifficulties(obj))
        {
            string? rendered;
            try { rendered = elementPreview(value); }
            catch { continue; }

            // 生成器可能返回 null（表示"这个值没什么好显示的"），那就跳过
            if (string.IsNullOrEmpty(rendered)) continue;

            parts.Add($"{name}:{rendered}");
        }

        return parts.Count > 0 ? string.Join(" | ", parts) : null;
    };

    /// <summary>
    /// 按 EZ/HD/IN/AT/Legacy 的顺序取出难度容器里的非 null 值。
    /// <para>
    /// 容器是泛型（<c>SongDifficultySet&lt;T&gt;</c>），调用点拿不到 <c>T</c>，只能反射取属性。
    /// </para>
    /// </summary>
    private static IEnumerable<(string Name, object Value)> EnumerateDifficulties(object set)
    {
        var setType = set.GetType();

        foreach (var name in DifficultySlotNames)
        {
            var property = setType.GetProperty(name);
            if (property == null) continue;

            var value = property.GetValue(set);
            if (value != null) yield return (name, value);
        }
    }

    /// <summary>难度容器的槽位名，顺序即显示顺序</summary>
    private static readonly string[] DifficultySlotNames = { "EZ", "HD", "IN", "AT", "Legacy" };
}
