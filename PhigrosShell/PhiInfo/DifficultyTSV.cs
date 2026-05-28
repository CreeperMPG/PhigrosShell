using PhigrosArchive.Abstractions;
using PhigrosArchive.Save.Data;
using PhigrosShell.Utils;

namespace PhigrosShell.PhiInfo;

internal class DifficultyTSV : IDifficultyProvider
{
    public List<SongDifficulty> Difficulties { get; } = new();

    public bool IsLoaded => Difficulties.Count > 0;

    public DifficultyTSV(string filePath)
    {
        var rows = TsvUtils.ReadTsvWithoutHeader(filePath);
        foreach (var row in rows)
        {
            // 最少需要 songId + EZ + HD + IN = 4 列
            if (row.Length < 4) continue;

            var song = new SongDifficulty { Name = row[0] };
            float.TryParse(row[1], out float ez);
            float.TryParse(row[2], out float hd);
            float.TryParse(row[3], out float iN);

            // AT 和 Legacy 是可选的
            float at = 0f;
            if (row.Length >= 5)
                float.TryParse(row[4], out at);

            // PhiDifficultyInfo<float>(ez, hd, in_, at, legacy)
            song.Info = new PhiDifficultyInfo<float>(ez, hd, iN, at, 0f);
            Difficulties.Add(song);
        }
    }

    public DifficultyTSV() { }

    public float? GetDifficulty(string songId, int difficultyIndex)
    {
        // 存档中 songId 可能带 .0 后缀（如 Glaciaxion.SunsetRay.0）
        // 但 TSV 中只有基础名（Glaciaxion.SunsetRay），去掉后缀再匹配
        var strippedId = StripVersionSuffix(songId);

        var song = Difficulties.FirstOrDefault(s =>
            s.Name.Equals(strippedId, StringComparison.OrdinalIgnoreCase) ||
            s.Name.Equals(songId, StringComparison.OrdinalIgnoreCase));
        return song?.GetDifficulty(difficultyIndex);
    }

    /// <summary>去掉 songId 末尾的 .数字 版本后缀</summary>
    private static string StripVersionSuffix(string songId)
    {
        int dotPos = songId.LastIndexOf('.');
        if (dotPos > 0 && dotPos < songId.Length - 1)
        {
            // 检查 . 后面是否全是数字
            string suffix = songId[(dotPos + 1)..];
            if (suffix.All(char.IsDigit))
                return songId[..dotPos];
        }
        return songId;
    }
}
