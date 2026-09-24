using CreeperMPG.PhiKits.Save.Abstractions;
using CreeperMPG.PhiKits.Save.Data;
using CreeperMPG.PhiKits.Save.Data.SaveEntries;

namespace PhigrosShell.Check;

/// <summary>
/// 存档数据校验：把 <see cref="SavePackage"/> 里的值与游戏规则、定数表、
/// 云端摘要交叉比对，产出一串 <see cref="SaveDataIssue"/>。
/// <para>
/// 只做"读"和判断，不修改存档，也不负责显示——文案与颜色由调用方决定。
/// </para>
/// </summary>
internal static class SaveChecker
{
    /// <summary>成绩满值，超过即视为改档痕迹</summary>
    private const uint MaxScore = 1_000_000;

    /// <summary>准确率上界</summary>
    private const float MaxAccuracy = 100f;

    /// <summary>课题模式最右一档（彩）的序号</summary>
    private const int MaxChallengeType = 5;

    /// <summary>课题模式等级上界</summary>
    private const int MaxChallengeRank = 51;

    /// <summary>总 RKS 的合理上界</summary>
    private const float MaxRankingScore = 17f;

    /// <summary>
    /// 校验一份已解包的存档。
    /// </summary>
    /// <param name="save">已解包的存档</param>
    /// <param name="summary">对应的云端摘要；为 null 时跳过"摘要 vs 文件"那几项</param>
    /// <param name="provider">定数提供者；缺失时相关的两项检查会被跳过</param>
    public static List<SaveDataIssue> Check(SavePackage save, SaveSummary? summary,
                                            IDifficultyProvider? provider)
    {
        var issues = new List<SaveDataIssue>();

        bool hasDifficulties = provider is { IsLoaded: true };
        if (!hasDifficulties)
            issues.Add(new SaveDataIssue(IssueSeverity.Warning, IssueType.DifficultyTSVNotLoaded));

        CheckRecords(save.GameRecord, provider, hasDifficulties, issues);
        CheckChallengeMode(save.GameProgress, issues);
        CheckSummaryAgainstSave(save, summary, issues);

        return issues;
    }

    /// <summary>逐曲逐难度检查成绩是否越界，以及定数表里有没有这首歌</summary>
    private static void CheckRecords(PhigrosRecord record, IDifficultyProvider? provider,
                                     bool hasDifficulties, List<SaveDataIssue> issues)
    {
        foreach (var (songId, levels) in record.Records)
        {
            if (hasDifficulties)
            {
                if (provider!.GetDifficulty(songId, 0) == null)
                {
                    issues.Add(new SaveDataIssue(IssueSeverity.Warning,
                        IssueType.RecordNotInTSV, songId));
                }
                else
                {
                    // 有 AT 成绩，却在定数表里查不到 AT 定数（AT 列为空）
                    float? atDifficulty = provider.GetDifficulty(songId, 3);
                    if ((atDifficulty == null || atDifficulty == 0) && levels.AT != null)
                    {
                        issues.Add(new SaveDataIssue(IssueSeverity.Warning,
                            IssueType.RecordHasATButNoAT, songId));
                    }
                }
            }

            foreach (var (difficultyName, level) in levels.ToDictionaryWithLegacy())
            {
                if (level == null) continue;

                // Score 是无符号数，"小于 0" 不可能出现，只查上界
                if (level.Score > MaxScore)
                {
                    issues.Add(new SaveDataIssue(IssueSeverity.Warning,
                        IssueType.InvalidScore, songId, difficultyName, level.Score));
                }

                if (level.Acc < 0f || level.Acc > MaxAccuracy)
                {
                    issues.Add(new SaveDataIssue(IssueSeverity.Warning,
                        IssueType.InvalidAccuracy, songId, difficultyName, level.Acc));
                }
            }
        }
    }

    /// <summary>课题模式等级由"类型 × 100 + 等级"编码，两段各有上界</summary>
    private static void CheckChallengeMode(PhigrosProgress progress, List<SaveDataIssue> issues)
    {
        short challenge = progress.ChallengeModeRank;
        int type = challenge / 100;
        int rank = challenge % 100;

        if (type > MaxChallengeType)
            issues.Add(new SaveDataIssue(IssueSeverity.Warning, IssueType.ChallengeTypeTooHigh, type));

        if (rank > MaxChallengeRank)
            issues.Add(new SaveDataIssue(IssueSeverity.Warning, IssueType.ChallengeRankTooHigh, rank));
    }

    /// <summary>云端摘要记录了上传那一刻的存档状态，与当前文件不一致说明摘要没跟着更新</summary>
    private static void CheckSummaryAgainstSave(SavePackage save, SaveSummary? summary,
                                                List<SaveDataIssue> issues)
    {
        if (summary == null) return;

        if (save.GameProgress.ChallengeModeRank != summary.Challenge)
            issues.Add(new SaveDataIssue(IssueSeverity.Info, IssueType.ChallengeRankDifferent));

        if (save.User.Avatar != summary.Avatar)
            issues.Add(new SaveDataIssue(IssueSeverity.Info, IssueType.AvatarDifferent));

        if (summary.RankingScore < 0f || summary.RankingScore > MaxRankingScore)
            issues.Add(new SaveDataIssue(IssueSeverity.Warning,
                IssueType.SummaryRankingScoreInvalid, summary.RankingScore));
    }
}
