using System.Text.Json;
using PhigrosShell.Utils;

namespace PhigrosShell.Commands.Phigros;

internal class DownloadPhiInfoCommand : CommandBase
{
    public override string Name => "DownloadPhiInfo";
    public override string Description => "Download difficulty.tsv and info.tsv from the internet.";

    private static readonly string DifficultyUrl =
        "https://raw.githubusercontent.com/7Cb67/Phigros/main/Assets/Resources/b19/difficulty.tsv";
    private static readonly string InfoUrl =
        "https://raw.githubusercontent.com/7Cb67/Phigros/main/Assets/Resources/b19/info.tsv";

    public override bool Execute(string command, List<ShellArgument> args)
    {
        if (!command.Equals("downloadphiinfo", StringComparison.OrdinalIgnoreCase) &&
            !command.Equals("dltsv", StringComparison.OrdinalIgnoreCase))
            return false;

        var type = ConsoleUtils.GetArgumentValue(args, "type") ?? "all";

        try
        {
            if (type == "difficulty" || type == "all")
                DownloadDifficultyTSV();

            if (type == "info" || type == "all")
                DownloadInfoTSV();

            ConsoleUtils.WriteSuccess("Download complete.");
        }
        catch (Exception ex)
        {
            ConsoleUtils.WriteError("Download failed: " + ex.Message);
        }

        return true;
    }

    private static void DownloadDifficultyTSV()
    {
        FluentConsole.Cyan.Line("Downloading difficulty.tsv...");
        using var client = new HttpClient();
        var response = client.GetStringAsync(DifficultyUrl).GetAwaiter().GetResult();

        string savePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PhiShell", "difficulty.tsv");

        var dir = Path.GetDirectoryName(savePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllText(savePath, response);
        Program.Config["info.difficulty.tsv"] = savePath;
        Program.Config.Save();

        // Reload
        Program.InitTSVFiles();
        FluentConsole.Green.Line("Saved to: " + savePath);
    }

    private static void DownloadInfoTSV()
    {
        FluentConsole.Cyan.Line("Downloading info.tsv...");
        using var client = new HttpClient();
        var response = client.GetStringAsync(InfoUrl).GetAwaiter().GetResult();

        string savePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PhiShell", "info.tsv");

        var dir = Path.GetDirectoryName(savePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllText(savePath, response);
        Program.Config["info.info.tsv"] = savePath;
        Program.Config.Save();

        // Reload
        Program.InitTSVFiles();
        FluentConsole.Green.Line("Saved to: " + savePath);
    }
}
