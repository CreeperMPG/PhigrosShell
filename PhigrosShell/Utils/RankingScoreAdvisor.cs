namespace PhigrosShell.Utils;

/// <summary>
/// P3B27 推分建议的纯数学部分：给定单曲成绩与账号当前 RKS，
/// 反推"刚好把总 RKS 推上下一档"所需的准确率。
/// <para>
/// 这一层刻意不依赖存档结构，只吃数字，方便单独验证。
/// </para>
/// </summary>
internal static class RankingScoreAdvisor
{
    /// <summary>低于这个准确率时单曲 RKS 记 0</summary>
    private const double MinAccuracy = 70.0;

    /// <summary>单曲 RKS 系数的基准：((Acc - 55) / 45)^2</summary>
    private const double BaseAccuracy = 55.0;
    private const double AccuracySpan = 45.0;

    /// <summary>总 RKS 由 30 首成绩平均而来（定数最高的 3 个 Phi + 单曲 RKS 最高的 27 首）</summary>
    private const double RankingScoreWeight = 30.0;

    /// <summary>准确率上界，超过即视为"理论满分"</summary>
    private const double MaxAccuracy = 100.0;

    /// <summary>RKS 的步进：1 / 200 = 0.005</summary>
    private const double RankingScoreStepDenominator = 200.0;

    /// <summary>单曲 RKS = ((Acc - 55) / 45)^2 × 定数；Acc 低于 70 记 0</summary>
    public static double SingleRankingScore(double accuracy, double difficulty)
        => accuracy < MinAccuracy
            ? 0.0
            : Math.Pow((accuracy - BaseAccuracy) / AccuracySpan, 2.0) * difficulty;

    /// <summary>
    /// RKS 以 0.005 为步进，返回严格大于 <paramref name="currentRks"/> 的下一个档位。
    /// </summary>
    /// <exception cref="ArgumentException">传入负数</exception>
    public static double NextRankingScore(double currentRks)
    {
        if (currentRks < 0.0)
            throw new ArgumentException("Only non-negative values supported.", nameof(currentRks));

        long rounded = (long)Math.Round(Math.Ceiling(currentRks * RankingScoreStepDenominator), 0);
        if (rounded % 2 == 0)
            rounded++;
        else if ((double)rounded / RankingScoreStepDenominator <= currentRks + 1E-12)
            rounded += 2;

        return rounded / RankingScoreStepDenominator;
    }

    /// <summary>反解：单曲 RKS 达到 <paramref name="singleRks"/> 所需的准确率</summary>
    public static double InverseAccuracy(double singleRks, float difficulty)
        => BaseAccuracy + AccuracySpan * Math.Sqrt(singleRks / difficulty);

    /// <summary>
    /// 计算把账号 RKS 推上下一档所需的准确率。
    /// </summary>
    /// <param name="accuracy">该成绩当前的准确率</param>
    /// <param name="difficulty">该曲目该难度的定数</param>
    /// <param name="currentRks">账号当前 RKS</param>
    /// <param name="p3Difficulty">P3 里定数最低的那首的定数（推分要越过它）</param>
    /// <param name="b27Rks">B27 里单曲 RKS 最低的那首的 RKS</param>
    /// <returns>所需准确率；<c>100</c> 表示刚好够；<c>null</c> 表示无法推分</returns>
    public static double? AccuracyForNextRankingScore(double accuracy, float difficulty, float currentRks,
                                                      double p3Difficulty, double b27Rks)
    {
        double step = NextRankingScore(currentRks) - currentRks;
        double targetAccuracy = InverseAccuracy(
            Math.Max(SingleRankingScore(accuracy, difficulty), b27Rks) + RankingScoreWeight * step,
            difficulty);

        if (targetAccuracy <= MaxAccuracy)
            return targetAccuracy;

        // 超过 100% 才够——此时只有"挤进 P3"这条路：靠定数优势补足差额
        double singleRksGain = SingleRankingScore(targetAccuracy, difficulty)
                             - SingleRankingScore(accuracy, difficulty);
        double difficultyGap = difficulty - p3Difficulty;

        if (difficultyGap <= 0.0) return null;
        return (singleRksGain + difficultyGap) / RankingScoreWeight >= step ? MaxAccuracy : null;
    }
}
