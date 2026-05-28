using PhigrosShell.Utils;

namespace PhigrosShell.PhiInfo;

internal class InfoTSV
{
    public Dictionary<string, string> InfoData { get; } = new();

    public InfoTSV(string filePath)
    {
        var rows = TsvUtils.ReadTsvWithoutHeader(filePath);
        foreach (var row in rows)
        {
            if (row.Length >= 2)
            {
                InfoData[row[0]] = row[1];
            }
        }
    }

    public string? GetSongName(string id) =>
        InfoData.GetValueOrDefault(id);
}
