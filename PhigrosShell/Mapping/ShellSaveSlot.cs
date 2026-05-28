using PhigrosArchive.Abstractions;
using PhigrosArchive.Save;
using PhigrosArchive.Save.Data;

namespace PhigrosShell.Mapping;

/// <summary>
/// 槽位适配，绑定 SaveFileInfo（元数据）+ SaveFile（游戏数据，可为 null）。
/// </summary>
internal class ShellSaveSlot
{
    /// <summary>槽位索引</summary>
    public int SlotIndex { get; set; }

    /// <summary>字段（隐藏于 VDirectory）</summary>
    internal SaveFileInfo? Info;

    /// <summary>字段（隐藏于 VDirectory）—— 实际存档数据，仅 Fetch 后非 null</summary>
    internal SaveFile? File;

    // ── 便捷访问 ──

    public PhigrosProgress? GameProgress => File?.GameProgress;
    public PhigrosUser? User => File?.User;
    public PhigrosSettings? Settings => File?.Settings;
    public PhigrosRecord GameRecord => File?.GameRecord;
    public PhigrosKey GameKey => File?.GameKey;

    // ── 操作 ──

    /// <summary>从云端下载并解析存档数据</summary>
    public async Task FetchAsync(IDifficultyProvider? provider = null)
    {
        if (Info == null) throw new InvalidOperationException("Slot info is null.");
        File = await Info.FetchSaveAsync(provider).ConfigureAwait(false);
    }

    /// <summary>将存档数据同步回摘要</summary>
    public void SyncSaveToSummary()
    {
        if (Info == null || File == null)
            throw new InvalidOperationException("Info or File is null.");
        Info.SyncSaveToSummary(File);
    }
}
