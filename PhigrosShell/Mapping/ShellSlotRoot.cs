using CreeperMPG.PhiKits.Save.CloudStorage;
using CreeperMPG.PhiKits.Save.Data;
using CreeperMPG.PhiKits.Save.Data.SaveEntries;

namespace PhigrosShell.Mapping;

/// <summary>
/// 每个槽位的 VDirectory 映射对象。
/// 引用 ShellSaveSlot，所有属性实时委托到 Slot 的当前状态。
/// 字段 → VDirectory 不遍历；属性 → VDirectory 可访问。
/// </summary>
internal class ShellSlotRoot
{
    /// <summary>字段，隐藏于 VDirectory——持有对 ShellSaveSlot 的引用</summary>
    internal ShellSaveSlot? Slot;

    // ── 属性（VDirectory 可遍历，实时委托到 Slot） ──

    /// <summary>云端元信息（文件名、URL、更新时间、摘要）</summary>
    public SaveInfoObject? CloudInfo => Slot?.Info;

    /// <summary>云端摘要；摘要解析失败时为 null</summary>
    public SaveSummary? Summary => Slot?.Info?.CloudSummary;

    /// <summary>游戏进度（需要先 Fetch）</summary>
    public PhigrosProgress? GameProgress => Slot?.File?.GameProgress;

    /// <summary>用户信息</summary>
    public PhigrosUser? User => Slot?.File?.User;

    /// <summary>设置</summary>
    public PhigrosSettings? Settings => Slot?.File?.Settings;

    /// <summary>
    /// 成绩表。
    /// <para>
    /// 直接把 <c>GameRecord.Records</c> 提上来当目录用——<c>Records</c> 那一层只是
    /// <c>.save</c> 里 <c>gameRecord</c> 条目的内部形状，用户没必要看见。
    /// </para>
    /// <para>
    /// 没 <c>save fetch</c> 时返回 null（VFS 显示 <c>(null)</c>），与
    /// <see cref="GameProgress"/> 等条目保持一致——**不要**改成返回空字典，
    /// 那看起来像"存档里真的没有成绩"，会误导人。
    /// </para>
    /// </summary>
    public Dictionary<string, SongDifficultySet<LevelRecord?>>? GameRecord
        => Slot?.File?.GameRecord.Records;

    /// <summary>游戏密钥</summary>
    public PhigrosKey? GameKey => Slot?.File?.GameKey;
}
