using System.Globalization;
using System.Text.Json;

namespace PhigrosShell.Services;

/// <summary>更新日志里的一条版本记录</summary>
internal sealed class ChangelogEntry
{
    /// <summary>版本号，例如 <c>1.2.0</c></summary>
    public string Version { get; set; } = "";

    /// <summary>发布日期时间，例如 <c>2026-05-29 00:02</c>（**不随语言变化**）</summary>
    public string Date { get; set; } = "";

    /// <summary>该版本的变更条目</summary>
    public List<string> Changes { get; set; } = new();
}

/// <summary>
/// 更新日志。<para>
/// 与 <see cref="LocalizationService"/> **分开存放**：
/// 界面文案（`lang/`）是短、扁平、常改的键值对；
/// 更新日志（`changelog/`）是长期累积的**结构化内容**——
/// 用数组保住顺序，用 <c>date</c> 字段代替"第一行是日期"这种隐式约定。
/// </para>
/// <para>
/// 加载策略与 <see cref="LocalizationService"/> 同构：先试当前语言，再兜底中文。
/// </para>
/// </summary>
internal sealed class ChangelogService
{
    private static readonly JsonSerializerOptions s_options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>按文件顺序（**约定为从新到旧**）排列的版本记录</summary>
    public IReadOnlyList<ChangelogEntry> Entries => _entries;

    private List<ChangelogEntry> _entries = new();

    public void Load(CultureInfo culture)
    {
        string name = culture.Name.ToLowerInvariant();

        if (!TryLoadEmbedded(ResourceName(name)))
            TryLoadEmbedded(ResourceName("zh-cn"));
    }

    private static string ResourceName(string language)
        => "PhigrosShell.Resources.changelog." + language + ".json";

    private bool TryLoadEmbedded(string resourceName)
    {
        try
        {
            using var stream = typeof(ChangelogService).Assembly.GetManifestResourceStream(resourceName);
            if (stream == null) return false;

            using var reader = new StreamReader(stream);
            var file = JsonSerializer.Deserialize<ChangelogFile>(reader.ReadToEnd(), s_options);
            if (file?.Entries == null) return false;

            _entries = file.Entries;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>磁盘上的文件结构：一个 <c>entries</c> 数组</summary>
    private sealed class ChangelogFile
    {
        public List<ChangelogEntry>? Entries { get; set; }
    }
}
