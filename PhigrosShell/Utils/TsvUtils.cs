using System.Text;

namespace PhigrosShell.Utils;

internal static class TsvUtils
{
    public static List<string[]> ReadTsvWithoutHeader(string filePath)
    {
        var lines = File.ReadAllLines(filePath, Encoding.UTF8);
        var result = new List<string[]>();

        foreach (var line in lines)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                result.Add(line.Split('\t').Select(v => v.Trim()).ToArray());
            }
        }

        return result;
    }
}
