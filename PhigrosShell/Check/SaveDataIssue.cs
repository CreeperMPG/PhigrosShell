namespace PhigrosShell.Check;

/// <summary>问题的严重程度，决定在控制台用什么颜色显示。</summary>
internal enum IssueSeverity
{
    /// <summary>仅供参考的差异，通常不是错误</summary>
    Info,

    /// <summary>可疑但未必有害的数据</summary>
    Warning,

    /// <summary>确定有问题的数据</summary>
    Error
}

/// <summary>
/// 存档校验能发现的问题种类。
/// <para>
/// 每一项都对应 lang 文件里的一个 <c>Warn*</c> / <c>Info*</c> 文案键
/// （见 <see cref="SaveDataIssue.LocalizationKey"/>），所以新增种类时两份语言文件都要补。
/// </para>
/// </summary>
internal enum IssueType
{
    DifficultyTSVNotLoaded,
    InfoTSVNotLoaded,
    DifficultyTSVNoErrorDetect,
    RecordNotInTSV,
    RecordHasATButNoAT,
    InvalidScore,
    InvalidAccuracy,
    ChallengeTypeTooHigh,
    ChallengeRankTooHigh,
    ChallengeRankDifferent,
    AvatarDifferent,
    SummaryRankingScoreInvalid
}

/// <summary>
/// 单条校验结果。
/// <para>
/// 只带"类型 + 严重程度 + 本地化参数"，不带拼好的文案——
/// 文案由显示层按当前语言查表得到，这样中英文档不必在两处维护。
/// </para>
/// </summary>
internal sealed class SaveDataIssue
{
    public IssueSeverity Severity { get; }

    public IssueType Type { get; }

    /// <summary>本地化文案的格式参数，顺序与 lang 文件里的 <c>{0}</c> / <c>{1}</c> 对应</summary>
    public object[] Arguments { get; }

    public SaveDataIssue(IssueSeverity severity, IssueType type, params object[] arguments)
    {
        Severity = severity;
        Type = type;
        Arguments = arguments ?? Array.Empty<object>();
    }

    /// <summary>
    /// 对应的本地化键。
    /// 枚举名与键名只差前缀——除两条 <c>Info</c> 级提示外，其余都是 <c>Warn</c>。
    /// </summary>
    public string LocalizationKey => Type is IssueType.ChallengeRankDifferent or IssueType.AvatarDifferent
        ? "Info" + Type
        : "Warn" + Type;
}
