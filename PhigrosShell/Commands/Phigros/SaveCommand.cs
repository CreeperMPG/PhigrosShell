using PhigrosArchive.Abstractions;
using PhigrosArchive.Save;
using PhigrosArchive.Save.Data;
using PhigrosArchive.Utils;
using PhigrosArchive;
using PhigrosShell.Utils;

namespace PhigrosShell.Commands.Phigros;

internal class SaveCommand : CommandBase
{
    public override string Name => "Save";
    public override string Description => "Manage multiple save slots. Usage: save <action> <slot> [args]";

    private static double CalculateSingleRankingScore(double accuracy, double difficulty)
    {
        if (accuracy < 70.0) return 0.0;
        return Math.Pow((accuracy - 55.0) / 45.0, 2.0) * difficulty;
    }

    public static double CalculateNextRKS(double currentRks)
    {
        if (currentRks < 0.0)
            throw new ArgumentException("Only non-negative values supported.", nameof(currentRks));

        long rounded = (long)Math.Round(Math.Ceiling(currentRks * 200.0), 0);
        if (rounded % 2 == 0)
            rounded++;
        else if ((double)rounded / 200.0 <= currentRks + 1E-12)
            rounded += 2;

        return (double)rounded / 200.0;
    }

    public static double InverseAccuracy(double singleRks, float difficulty)
    {
        return 55.0 + 45.0 * Math.Sqrt(singleRks / (double)difficulty);
    }

    public static double? CalculateRKS(double accuracy, float difficulty, float currentRks,
        double p3Difficulty, double b27Rks)
    {
        double nextRks = CalculateNextRKS(currentRks) - (double)currentRks;
        double targetAcc = InverseAccuracy(
            Math.Max(CalculateSingleRankingScore(accuracy, difficulty), b27Rks) + 30.0 * nextRks,
            difficulty);

        if (targetAcc > 100.0)
        {
            double singleRksIncrease = CalculateSingleRankingScore(targetAcc, difficulty) -
                                       CalculateSingleRankingScore(accuracy, difficulty);
            double difficultyGap = (double)difficulty - p3Difficulty;

            if (difficultyGap <= 0.0) return null;
            if ((singleRksIncrease + difficultyGap) / 30.0 >= nextRks) return 100.0;
            return null;
        }

        return targetAcc;
    }

    public override bool Execute(string command, List<ShellArgument> args)
    {
        if (!command.Equals("Save", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!Shell.LoggedIn())
        {
            ConsoleUtils.WriteWarning("Required login.");
            return true;
        }

        if (args.Count < 2)
        {
            ConsoleUtils.WriteWarning("Usage: save <action> <slot> [args]");
            return true;
        }

        string action = args[0].Value.ToLower();
        if (!int.TryParse(args[1].Value, out int slotIndex) || slotIndex < 0)
        {
            ConsoleUtils.WriteWarning("Invalid slot index.");
            return true;
        }

        var session = Shell.CurrentSession;
        if (session == null || slotIndex >= session.SaveFiles.Count)
        {
            ConsoleUtils.WriteWarning($"No save file found in slot {slotIndex}.");
            return true;
        }

        var slot = session.SaveFiles[slotIndex];
        var playerInfo = session.PlayerInfo!;

        switch (action)
        {
            case "fetch":
                return HandleFetch(slot, slotIndex);

            case "export":
                if (args.Count < 3)
                {
                    ConsoleUtils.WriteWarning("Usage: save export <slot> <path>");
                    return true;
                }
                return HandleExport(slot, args[2].Value);

            case "check":
                return HandleCheck(slot);

            case "p3b27":
            case "phibest":
                return HandleP3B27(slot, args);

            case "upload":
                return HandleUpload(slot, playerInfo);

            case "syncsummary":
                return HandleSyncSummary(slot);

            case "delete":
                return HandleDelete(slot, playerInfo);

            default:
                ConsoleUtils.WriteWarning("Unknown action: " + action);
                return true;
        }
    }

    private bool HandleFetch(Mapping.ShellSaveSlot slot, int slotIndex)
    {
        if (slot.Info == null)
        {
            ConsoleUtils.WriteWarning("Slot info is null.");
            return true;
        }

        try
        {
            slot.FetchAsync(Program.DifficultyProvider).GetAwaiter().GetResult();
            ConsoleUtils.WriteSuccess($"Save file fetched for slot {slotIndex}.");
        }
        catch (Exception ex)
        {
            ConsoleUtils.WriteError("Failed to fetch save file: " + ex.Message);
        }

        return true;
    }

    private bool HandleExport(Mapping.ShellSaveSlot slot, string exportPath)
    {
        if (slot.File == null)
        {
            ConsoleUtils.WriteWarning("Save file not fetched. Use 'save fetch' first.");
            return true;
        }

        try
        {
            slot.File.Save(exportPath);
            ConsoleUtils.WriteSuccess("Save file exported to: " + exportPath);
        }
        catch (Exception ex)
        {
            ConsoleUtils.WriteError("Export failed: " + ex.Message);
        }

        return true;
    }

    private bool HandleCheck(Mapping.ShellSaveSlot slot)
    {
        if (slot.File == null)
        {
            ConsoleUtils.WriteWarning("Save file not fetched. Use 'save fetch' first.");
            return true;
        }

        var provider = Program.DifficultyProvider;
        if (provider == null)
        {
            FluentConsole.DarkYellow.Line(Program.Localization["WarnDifficultyTSVNotLoaded"]);
            FluentConsole.Gray.Line(Program.Localization["WarnDifficultyTSVNoErrorDetect"]);
        }
        if (Program.InfoTSV == null)
            FluentConsole.DarkYellow.Line(Program.Localization["WarnInfoTSVNotLoaded"]);

        var issues = slot.File.CheckSaveData(slot.Info?.Summary);
        if (issues.Count == 0)
        {
            ConsoleUtils.WriteSuccess("No problems detected in save file.");
        }
        else
        {
            ConsoleUtils.WriteWarning($"{issues.Count} issue(s) detected:");
            foreach (var issue in issues)
            {
                var color = issue.Severity == IssueSeverity.Warning
                    ? ConsoleColor.Yellow : ConsoleColor.Gray;
                FluentConsole.Color(color).Line($"  [{issue.Type}] {issue.Message}");
            }
        }

        return true;
    }

    private bool HandleP3B27(Mapping.ShellSaveSlot slot, List<ShellArgument> args)
    {
        if (slot.File == null)
        {
            ConsoleUtils.WriteWarning("Save file not fetched. Use 'save fetch' first.");
            return true;
        }

        // 如果 TSV 已加载但记录中还没有定数，刷新一下
        if (Program.DifficultyTSV != null && slot.File?.GameRecord != null)
        {
            slot.File.GameRecord.RefreshDifficulties(Program.DifficultyTSV);
        }

        int count = 27;
        if (args.Count > 2 && int.TryParse(args[2].Value, out int customCount) && customCount > 0)
            count = customCount;

        var records = slot.File.GameRecord.Records;
        bool hasInfoTSV = Program.InfoTSV != null;

        if (!hasInfoTSV)
            ConsoleUtils.WriteWarning("Couldn't find info.tsv. Use 'config info.info.tsv <path>' to specify.");

        if (records == null || records.Count == 0)
        {
            ConsoleUtils.WriteWarning("No game record found. Check if your save file is valid.");
            return true;
        }

        float currentRks = slot.File.GameRecord.RankingScore ?? 0f;
        FluentConsole.Cyan.Text("Ranking Score : ").White.Line(currentRks.ToString("F6"));

        // P3 analysis (top 3 phi scores by difficulty)
        var phiRecords = records.SelectMany(kvp =>
            kvp.Value.GetDictionary().Where(skvp => skvp.Value != null)
                .Select(skvp => new
                {
                    DisplayName = "[" + skvp.Key + "] " +
                        (hasInfoTSV ? (Program.InfoTSV?.GetSongName(kvp.Key) ?? kvp.Key) : kvp.Key),
                    Record = skvp.Value,
                    SongId = kvp.Key
                }))
            .Where(x => x.Record?.Score == 1000000 && x.Record?.Acc == 100f)
            .OrderByDescending(x => x.Record?.Difficulty ?? 0f)
            .Take(3)
            .ToList();

        double p3Difficulty = phiRecords.Count >= 3
            ? (phiRecords[2].Record?.Difficulty ?? 114514f)
            : 114514f;

        for (int i = 0; i < phiRecords.Count; i++)
        {
            var entry = phiRecords[i];
            var record = entry.Record!;
            FluentConsole.Gray.Text($"P{i + 1,-4}")
                .Cyan.Text((record.RankingScore?.ToString("F3") ?? "?").PadLeft(6))
                .Gray.Text(" | ")
                .Yellow.Text(entry.DisplayName.PadRightEx(50))
                .Gray.Text(" Lv." + (record.Difficulty?.ToString("F1") ?? "?").PadLeft(4) + " | ")
                .Yellow.Line($"{record.Score.ToString().PadLeft(7, '0')} {record.Rank.ToString().PadRightEx(3)} {record.Acc,6:F2}%");
        }

        // B27 analysis — 按 RankingScore 排序
        var b27Entries = records.SelectMany(kvp =>
            kvp.Value.GetDictionary().Where(skvp => skvp.Value != null && skvp.Value.RankingScore.HasValue)
                .Select(skvp => new
                {
                    DisplayName = "[" + skvp.Key + "] " +
                        (hasInfoTSV ? (Program.InfoTSV?.GetSongName(kvp.Key) ?? kvp.Key) : kvp.Key),
                    Record = skvp.Value!,
                    SongId = kvp.Key
                }))
            .OrderByDescending(x => x.Record!.RankingScore)
            .Take(count)
            .ToList();

        if (b27Entries.Count < 27)
        {
            ConsoleUtils.WriteWarning($"Only {b27Entries.Count} records found (need {count}). Please load difficulty.tsv first.");
            return true;
        }

        double b27Rks = b27Entries[26].Record!.RankingScore ?? 114514f;

        for (int i = 0; i < b27Entries.Count; i++)
        {
            var entry = b27Entries[i];
            var record = entry.Record!;
            var rank = record.Rank;

            var suggestion = CalculateRKS(record.Acc, record.Difficulty ?? 0.1f,
                currentRks, p3Difficulty, b27Rks);

            string suggestionText = suggestion.HasValue
                ? $"{suggestion:F3}%"
                : Program.Localization["P3B27NoSuggestion"];

            FluentConsole.Gray.Text($"#{i + 1,-4}")
                .Cyan.Text((record.RankingScore?.ToString("F3") ?? "?").PadLeft(6))
                .Gray.Text(" | ")
                .Yellow.Text(entry.DisplayName.PadRightEx(50))
                .Gray.Text(" Lv." + (record.Difficulty?.ToString("F1") ?? "?").PadLeft(4) + " | ")
                .Color(rank switch
                {
                    PhiLevelType.F => ConsoleColor.Gray,
                    PhiLevelType.FC => ConsoleColor.Cyan,
                    PhiLevelType.Phi => ConsoleColor.Yellow,
                    _ => ConsoleColor.White
                })
                .Text($"{record.Score.ToString().PadLeft(7, '0')} {rank.ToString().PadRightEx(3)} {record.Acc,6:F2}%")
                .Color(suggestion.HasValue ? ConsoleColor.Green : ConsoleColor.DarkGray)
                .Line("  => " + suggestionText);
        }

        return true;
    }

    private bool HandleUpload(Mapping.ShellSaveSlot slot, PhigrosPlayerInfo playerInfo)
    {
        if (slot.File == null)
        {
            ConsoleUtils.WriteWarning("Save file not fetched. Use 'save fetch' first.");
            return true;
        }

        try
        {
            byte[] zipData = slot.File.PackToZip();
            playerInfo.UploadSaveAsync(zipData, slot.Info, output: true)
                .GetAwaiter().GetResult();
            ConsoleUtils.WriteSuccess("Save file uploaded successfully!");
        }
        catch (Exception ex)
        {
            ConsoleUtils.WriteError("Failed to upload save file: " + ex.Message);
        }

        return true;
    }

    private bool HandleSyncSummary(Mapping.ShellSaveSlot slot)
    {
        try
        {
            slot.SyncSaveToSummary();
            ConsoleUtils.WriteSuccess("Save file synced to summary.");
            if (slot.File != null)
            {
                FluentConsole.DarkCyan.Text("Ranking Score : ").White.Line(
                    slot.File.GameRecord.RankingScore?.ToString("F6") ?? "Unknown")
                    .DarkCyan.Text("Challenge     : ").White.Line(
                        slot.File.GameProgress?.ChallengeModeRank.ToString() ?? "Unknown")
                    .DarkCyan.Text("Avatar        : ").White.Line(
                        slot.File.User?.Avatar ?? "Unknown");
            }
        }
        catch (Exception ex)
        {
            ConsoleUtils.WriteWarning("Failed to sync: " + ex.Message);
        }

        return true;
    }

    private bool HandleDelete(Mapping.ShellSaveSlot slot, PhigrosPlayerInfo playerInfo)
    {
        if (slot.Info?.CloudInfo == null)
        {
            ConsoleUtils.WriteWarning("No cloud info available for this slot.");
            return true;
        }

        FluentConsole.Yellow.Line("Are you sure you want to delete this save? (y/N)");
        var key = Console.ReadKey(intercept: true);
        Console.WriteLine();
        if (key.Key != ConsoleKey.Y)
        {
            ConsoleUtils.WriteWarning("Cancelled.");
            return true;
        }

        try
        {
            playerInfo.DeleteSaveAsync(slot.Info.CloudInfo.FileObjectID)
                .GetAwaiter().GetResult();
            ConsoleUtils.WriteSuccess("Save file deleted from cloud.");
        }
        catch (Exception ex)
        {
            ConsoleUtils.WriteError("Failed to delete: " + ex.Message);
        }

        return true;
    }


}
