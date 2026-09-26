using CreeperMPG.PhiKits.Save.Abstractions;
using CreeperMPG.PhiKits.Save.Data;
using CreeperMPG.PhiKits.Save.Data.SaveEntries;
using PhigrosShell.Check;
using PhigrosShell.Mapping;
using PhigrosShell.Services;
using PhigrosShell.Utils;

namespace PhigrosShell.Commands.Phigros;

internal class SaveCommand : CommandBase
{
    public override string Name => "Save";
    public override string Description => "Manage multiple save slots. Usage: save <action> <slot> [args]";

    private static LocalizationService L => Program.Localization;

    /// <summary>难度序号 → 显示名，下标与 <c>SongDifficultySet</c> 一致</summary>
    private static readonly string[] DifficultyNames = { "EZ", "HD", "IN", "AT", "Legacy" };

    /// <summary>曲名列的宽度</summary>
    private const int SongNameWidth = 50;

    /// <summary>
    /// P3 凑不满 3 首满分成绩时的哨兵定数：没有"定数最低的那个 Phi"可以被顶掉，
    /// 所以任何推分都不可能靠挤进 P3 来实现。
    /// </summary>
    private const double NoThirdPhi = 114514.0;

    // ── 入口 ──

    public override bool Execute(string command, List<ShellArgument> args)
    {
        if (!command.Equals("Save", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!Shell.LoggedIn())
        {
            ConsoleUtils.WriteWarning(L["SaveRequiredLogin"]);
            return true;
        }

        if (args.Count < 2)
        {
            ConsoleUtils.WriteWarning(L["SaveUsage"]);
            return true;
        }

        var session = Shell.CurrentSession!;
        string action = args[0].Value.ToLowerInvariant();

        if (!int.TryParse(args[1].Value, out int slotIndex) ||
            slotIndex < 0 || slotIndex >= session.SaveFiles.Count)
        {
            ConsoleUtils.WriteWarning(L["SaveNoSlot", new object[] { args[1].Value }]);
            return true;
        }

        var slot = session.SaveFiles[slotIndex];

        switch (action)
        {
            case "fetch":
                return FetchSlot(slot, slotIndex);
            case "export":
                if (args.Count < 3)
                {
                    ConsoleUtils.WriteWarning(L["SaveUsageExport"]);
                    return true;
                }
                return ExportSlot(slot, args[2].Value);
            case "check":
                return CheckSlot(slot);
            case "p3b27":
            case "phibest":
                return ShowP3B27(slot, args);
            case "upload":
                return UploadSlot(session, slot);
            case "syncsummary":
                return SyncSummary(slot);
            case "delete":
                return DeleteSlot(session, slot, slotIndex);
            case "re9":
                return Re9(slot, slotIndex);
            default:
                ConsoleUtils.WriteWarning(L["SaveUnknownAction", new object[] { action }]);
                return true;
        }
    }
    private static bool Re9(ShellSaveSlot slot, int slotIndex)
    {
        if (slot.GameProgress != null && slot.GameKey != null)
        {
            slot.GameProgress.Chapter9UnlockBegin = slot.GameProgress.Chapter9SecretChallengePendingLifeUnlock = false;
            slot.GameProgress.Chapter9SecretChallengeLifeTier = slot.GameProgress.Chapter9SecretChallengeSelectedLifeTier = 0;
            Array.Clear(slot.GameProgress.Chapter9SongUnlocked);
            slot.GameProgress.Chapter9SecretPassword = "0";
            // 清除第九章所有影响解锁进度的收集品
            foreach (var key in new List<string> { "liangrenhuihe", "m9beginning", "qiongdingguzhou", "themirage", "sundemimi", "nizhidaoma", "poyidomejiu", "poyidomegino" })
            {
                slot.GameKey.KeyMap.Remove(key);
            }
            ConsoleUtils.WriteSuccess(L["SaveRe9Done", new object[] { slotIndex }]);
        }
        return true;
    }

    // ── fetch / export ──

    private static bool FetchSlot(ShellSaveSlot slot, int slotIndex)
    {
        if (slot.Info == null)
        {
            ConsoleUtils.WriteWarning(L["SaveSlotInfoNull"]);
            return true;
        }

        try
        {
            slot.FetchAsync().GetAwaiter().GetResult();
            ConsoleUtils.WriteSuccess(L["SaveFetched", new object[] { slotIndex }]);
        }
        catch (Exception ex)
        {
            ConsoleUtils.WriteError(L["SaveFetchFailed", new object[] { ex.Message }]);
        }

        return true;
    }

    private static bool ExportSlot(ShellSaveSlot slot, string exportPath)
    {
        if (!RequiresFetched(slot)) return true;

        try
        {
            File.WriteAllBytes(exportPath, slot.File!.ToZipBytes());
            ConsoleUtils.WriteSuccess(L["SaveExported", new object[] { exportPath }]);
        }
        catch (Exception ex)
        {
            ConsoleUtils.WriteError(L["SaveExportFailed", new object[] { ex.Message }]);
        }

        return true;
    }

    // ── check ──

    private static bool CheckSlot(ShellSaveSlot slot)
    {
        if (!RequiresFetched(slot)) return true;

        if (Program.InfoTSV == null)
            FluentConsole.DarkYellow.Line(L["WarnInfoTSVNotLoaded"]);

        var issues = SaveChecker.Check(slot.File!, slot.Info?.CloudSummary, Program.DifficultyProvider);

        if (issues.Count == 0)
        {
            ConsoleUtils.WriteSuccess(L["SaveCheckPassed"]);
            return true;
        }

        ConsoleUtils.WriteWarning(L["SaveCheckIssueCount", new object[] { issues.Count }]);
        foreach (var issue in issues)
        {
            FluentConsole.Color(SeverityColor(issue.Severity))
                .Line("  " + L[issue.LocalizationKey, issue.Arguments]);
        }

        return true;
    }

    // ── p3b27 ──

    private static bool ShowP3B27(ShellSaveSlot slot, List<ShellArgument> args)
    {
        if (!RequiresFetched(slot)) return true;

        var record = slot.File!.GameRecord;
        var provider = Program.DifficultyProvider;

        if (record.Records.Count == 0)
        {
            ConsoleUtils.WriteWarning(L["SaveP3B27NoRecord"]);
            return true;
        }

        int count = 27;
        if (args.Count > 2 && int.TryParse(args[2].Value, out int customCount) && customCount > 0)
            count = customCount;

        bool hasInfoTSV = Program.InfoTSV != null;
        if (!hasInfoTSV)
            ConsoleUtils.WriteWarning(L["SaveP3B27NoInfoTSV"]);

        var views = ProjectRecords(record, provider);

        float currentRks = provider != null ? record.CalculateRankingScore(provider) : 0f;
        FluentConsole.Cyan.Text(L["SaveRankingScore"]).White.Line(currentRks.ToString("F6"));

        // ── P3：定数最高的 3 个满分成绩 ──
        var phiRecords = views
            .Where(v => v.Record.Score == 1000000 && v.Record.Acc == 100f)
            .OrderByDescending(v => v.Difficulty)
            .Take(3)
            .ToList();

        double p3Difficulty = phiRecords.Count >= 3 ? phiRecords[2].Difficulty : NoThirdPhi;

        for (int i = 0; i < phiRecords.Count; i++)
        {
            var view = phiRecords[i];
            WriteRecordLine($"P{i + 1,-4}", view, view.RankingScore,
                Describe(view, hasInfoTSV), suffix: null, suffixColor: default);
        }

        // ── B27：单曲 RKS 最高的 27 首 ──
        var b27Entries = views
            .Where(v => v.Difficulty > 0f)
            .OrderByDescending(v => v.RankingScore)
            .Take(count)
            .ToList();

        // 一条都没查到达定数时 b27Entries 是空的，[^1] 会越界
        if (b27Entries.Count == 0)
        {
            ConsoleUtils.WriteWarning(L["SaveP3B27Insufficient", new object[] { 0, count }]);
            return true;
        }

        double b27Rks = b27Entries[^1].RankingScore;

        for (int i = 0; i < b27Entries.Count; i++)
        {
            var view = b27Entries[i];
            var suggestion = RankingScoreAdvisor.AccuracyForNextRankingScore(
                view.Record.Acc, view.Difficulty, currentRks, p3Difficulty, b27Rks);

            WriteRecordLine($"#{i + 1,-4}", view, view.RankingScore, Describe(view, hasInfoTSV),
                suffix: suggestion.HasValue ? $"{suggestion:F3}%" : L["P3B27NoSuggestion"],
                suffixColor: suggestion.HasValue ? ConsoleColor.Green : ConsoleColor.DarkGray);
        }

        return true;
    }

    /// <summary>把成绩表摊平成"每条成绩一行"的视图，P3 与 B27 共用</summary>
    private static List<RecordView> ProjectRecords(PhigrosRecord record, IDifficultyProvider? provider)
    {
        var views = new List<RecordView>();

        foreach (var (songId, levels) in record.Records)
        {
            for (int index = 0; index < DifficultyNames.Length; index++)
            {
                var level = levels[index];
                if (level == null) continue;

                views.Add(new RecordView(songId, DifficultyNames[index], level,
                    provider?.GetDifficulty(songId, index) ?? 0f));
            }
        }

        return views;
    }

    private static void WriteRecordLine(string prefix, RecordView view, double rankingScore,
                                        string displayName, string? suffix, ConsoleColor suffixColor)
    {
        FluentConsole.Gray.Text(prefix)
            .Cyan.Text(rankingScore.ToString("F3").PadLeft(6))
            .Gray.Text(" | ")
            .Yellow.Text(displayName.PadRightEx(SongNameWidth))
            .Gray.Text($" Lv.{view.Difficulty,4:F1} | ")
            .Color(RankColor(view.Record.Rank))
            .Text($"{view.Record.Score:D7} {view.Record.Rank.ToString().PadRightEx(3)} {view.Record.Acc,6:F2}%")
            .Color(suffixColor)
            .Line(suffix == null ? "" : "  => " + suffix);
    }

    /// <summary>
    /// 曲目显示名：优先 info.tsv 里的曲名，查不到则回退到曲目 ID。
    /// <para>
    /// 回退时**保留原始 ID**（含 <c>.0</c> 后缀）——它与存档目录里的键一致，
    /// 方便用户直接拿它去 <c>cd</c>。
    /// </para>
    /// </summary>
    private static string Describe(RecordView view, bool hasInfoTSV)
    {
        string? songName = hasInfoTSV ? Program.InfoTSV?.GetSongName(view.SongId) : null;
        return $"[{view.DifficultyName}] {songName ?? view.SongId}";
    }

    // ── upload / syncsummary / delete ──

    /// <summary>
    /// 上传 = **替换**：先新建一个云槽位，成功后再删掉原来的那一个。
    /// </summary>
    private static bool UploadSlot(ShellSession session, ShellSaveSlot slot)
    {
        if (!RequiresFetched(slot)) return true;
        if (!RequireDifficultyProvider()) return true;

        var old = slot.Info;
        if (old == null)
        {
            ConsoleUtils.WriteWarning(L["SaveSlotInfoNull"]);
            return true;
        }

        var player = session.PlayerInfo!;
        var save = slot.File!;

        try
        {
            var created = player.UploadSave(save, save.GenerateSummary(Program.DifficultyProvider!))
                .GetAwaiter().GetResult();

            // 先新建、后删旧——反过来一旦新建失败就把云端的存档白删了。
            // 旧槽位删不掉只提示、不中断：新存档已经好好地在云端了，最坏是多留一个旧槽位。
            string? cleanupWarning = null;
            try
            {
                player.DeleteSave(old).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                cleanupWarning = L["WarnUploadOldSlotNotDeleted", new object[] { ex.Message }];
            }

            ConsoleUtils.WriteSuccess(L["SaveUploaded"]);
            if (cleanupWarning != null)
                ConsoleUtils.WriteWarning(cleanupWarning);

            // 刚下载的数据跟着新槽位走，省得用户再 fetch 一次
            ReloadSlots(session, (created.SaveInfoObjectID, save));
        }
        catch (Exception ex)
        {
            ConsoleUtils.WriteError(L["SaveUploadFailed", new object[] { ex.Message }]);
        }

        return true;
    }

    private static bool SyncSummary(ShellSaveSlot slot)
    {
        if (!RequiresFetched(slot)) return true;
        if (!RequireDifficultyProvider()) return true;

        try
        {
            var summary = slot.RebuildSummary(Program.DifficultyProvider!);
            var save = slot.File!;

            ConsoleUtils.WriteSuccess(L["SaveSynced"]);
            FluentConsole.DarkCyan.Text(L["SaveRankingScore"]).White.Line(summary.RankingScore.ToString("F6"))
                .DarkCyan.Text(L["SaveChallenge"]).White.Line(save.GameProgress.ChallengeModeRank.ToString())
                .DarkCyan.Text(L["SaveAvatar"]).White.Line(save.User.Avatar);
        }
        catch (Exception ex)
        {
            ConsoleUtils.WriteWarning(L["SaveSyncFailed", new object[] { ex.Message }]);
        }

        return true;
    }

    private static bool DeleteSlot(ShellSession session, ShellSaveSlot slot, int slotIndex)
    {
        if (slot.Info == null)
        {
            ConsoleUtils.WriteWarning(L["SaveSlotInfoNull"]);
            return true;
        }

        FluentConsole.Yellow.Line(L["SaveDeleteConfirm"]);
        var key = Console.ReadKey(intercept: true);
        Console.WriteLine();
        if (key.Key != ConsoleKey.Y)
        {
            ConsoleUtils.WriteWarning(L["OperationCancelledByUser"]);
            return true;
        }

        try
        {
            session.PlayerInfo!.DeleteSave(slot.Info).GetAwaiter().GetResult();
            ConsoleUtils.WriteSuccess(L["SaveDeleted"]);

            // 人还站在被删掉的槽位里的话，先退回根目录
            ResetPathIfInsideSlot(slotIndex);
            ReloadSlots(session);
        }
        catch (Exception ex)
        {
            ConsoleUtils.WriteError(L["SaveDeleteFailed", new object[] { ex.Message }]);
        }

        return true;
    }

    // ── 共用 ──

    /// <summary>
    /// 重新拉取槽位列表并重建 VFS 根（槽位增删后必须做，否则 VFS 里挂的还是旧的 ShellSaveSlot）。
    /// <paramref name="keep"/> 用于把刚下载好的存档数据带到新列表里的同一个槽位上。
    /// </summary>
    private static void ReloadSlots(ShellSession session, (string ObjectId, SavePackage File)? keep = null)
    {
        session.ReloadSaveFilesAsync().GetAwaiter().GetResult();
        Shell.RebuildPlayerRoot();

        if (!keep.HasValue) return;

        var (objectId, file) = keep.Value;
        var slot = session.SaveFiles.FirstOrDefault(s => s.Info?.SaveInfoObjectID == objectId);
        if (slot != null) slot.File = file;
    }

    private static void ResetPathIfInsideSlot(int slotIndex)
    {
        string prefix = $"/SaveFiles/{slotIndex}";
        if (Shell.Path == prefix || Shell.Path.StartsWith(prefix + "/", StringComparison.Ordinal))
            Shell.Path = "/";
    }

    private static bool RequiresFetched(ShellSaveSlot slot)
    {
        if (slot.File != null) return true;

        ConsoleUtils.WriteWarning(L["SaveNotFetched"]);
        return false;
    }

    /// <summary>
    /// 摘要里的 RKS 只有配上定数才算得准；没有定数时无参重载会写入占位分数，
    /// 一旦上传就是脏数据，所以宁可不做。
    /// </summary>
    private static bool RequireDifficultyProvider()
    {
        var provider = Program.DifficultyProvider;
        if (provider != null && provider.IsLoaded) return true;

        ConsoleUtils.WriteWarning(L["SaveDiffTSVRequired"]);
        return false;
    }

    private static ConsoleColor RankColor(RankType rank) => rank switch
    {
        RankType.F => ConsoleColor.Gray,
        RankType.FC => ConsoleColor.Cyan,
        RankType.Phi => ConsoleColor.Yellow,
        _ => ConsoleColor.White,
    };

    private static ConsoleColor SeverityColor(IssueSeverity severity) => severity switch
    {
        IssueSeverity.Warning => ConsoleColor.Yellow,
        IssueSeverity.Error => ConsoleColor.Red,
        _ => ConsoleColor.Gray,
    };

    /// <summary>一条成绩的展示视图：曲目、难度、成绩本身、以及查表得到的定数（查不到为 0）</summary>
    private readonly record struct RecordView(string SongId, string DifficultyName,
                                              LevelRecord Record, float Difficulty)
    {
        /// <summary>单曲 RKS = 成绩系数 × 定数</summary>
        public double RankingScore => Record.GetRankingScore(Difficulty);
    }
}
