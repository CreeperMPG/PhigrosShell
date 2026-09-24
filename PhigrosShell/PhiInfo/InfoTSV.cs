using PhigrosShell.Utils;

namespace PhigrosShell.PhiInfo;

/// <summary>
/// 曲目信息表（info.tsv）：曲目 ID → 曲名。
/// </summary>
internal class InfoTSV
{
    /// <summary>精确匹配用：TSV 里的原始 ID → 曲名</summary>
    private readonly Dictionary<string, string> _byId = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>去掉版本后缀后用：裸 ID → 曲名，处理存档里的 "xxx.0" 形式</summary>
    private readonly Dictionary<string, string> _byStrippedId = new(StringComparer.OrdinalIgnoreCase);

    public InfoTSV(string filePath)
    {
        var rows = TsvUtils.ReadTsvWithoutHeader(filePath);
        foreach (var row in rows)
        {
            if (row.Length < 2) continue;

            string id = row[0];
            string name = row[1];

            _byId[id] = name;

            // 存档里的曲目 ID 带版本后缀（"Archidoxen.Se_IRA.0"），TSV 里是裸 ID。
            // 两份都存一份，查的时候先用原样、再用剥掉后缀的。
            _byStrippedId.TryAdd(StripVersionSuffix(id), name);
        }
    }

    /// <summary>查曲名；查不到返回 null</summary>
    public string? GetSongName(string id)
    {
        if (_byId.TryGetValue(id, out string? name)) return name;

        // 存档 ID 带 ".N" 后缀时，剥掉再查一次
        return _byStrippedId.TryGetValue(StripVersionSuffix(id), out name) ? name : null;
    }

    /// <summary>
    /// 剥掉末尾的 <c>.数字</c> 版本后缀。
    /// <para>
    /// 与 <c>CreeperMPG.PhiKits.Save.Additions.TsvDifficultyProvider.StripVersionSuffix</c>
    /// 保持一致的规则——曲目 ID 本身可能含点（<c>Glaciaxion.SunsetRay</c>），
    /// 所以只有**末尾**的点加纯数字才算后缀。
    /// </para>
    /// </summary>
    private static string StripVersionSuffix(string id)
    {
        int dot = id.LastIndexOf('.');
        if (dot <= 0 || dot == id.Length - 1) return id;

        for (int i = dot + 1; i < id.Length; i++)
            if (!char.IsDigit(id[i])) return id;   // 末尾不是纯数字，原样返回

        return id[..dot];
    }
}
