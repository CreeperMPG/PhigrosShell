using CreeperMPG.PhiKits.Save.Abstractions;
using CreeperMPG.PhiKits.Save.CloudStorage;
using CreeperMPG.PhiKits.Save.Data;
using CreeperMPG.PhiKits.Save.Data.SaveEntries;

namespace PhigrosShell.Mapping;

/// <summary>
/// 槽位适配：绑定云端元信息 <see cref="SaveInfoObject"/> + 游戏数据
/// <see cref="SavePackage"/>（仅 <see cref="FetchAsync"/> 后非 null）。
/// </summary>
internal class ShellSaveSlot
{
    /// <summary>槽位号：按云端更新时间降序排出来的序号，<c>save</c> 命令用它定位</summary>
    public int SlotIndex { get; set; }

    /// <summary>字段（隐藏于 VDirectory）—— 云端元信息与摘要</summary>
    internal SaveInfoObject? Info;

    /// <summary>字段（隐藏于 VDirectory）—— 实际存档数据，仅 Fetch 后非 null</summary>
    internal SavePackage? File;

    // ── 便捷访问 ──

    public PhigrosProgress? GameProgress => File?.GameProgress;
    public PhigrosUser? User => File?.User;
    public PhigrosSettings? Settings => File?.Settings;
    public PhigrosRecord? GameRecord => File?.GameRecord;
    public PhigrosKey? GameKey => File?.GameKey;

    // ── 操作 ──

    /// <summary>从云端下载并解包存档数据</summary>
    public async Task FetchAsync()
    {
        if (Info == null) throw new InvalidOperationException("Slot info is null.");
        File = await Info.DownloadSave().ConfigureAwait(false);
    }

    /// <summary>
    /// 用当前存档数据重算摘要，写回本地的云端摘要对象。
    /// </summary>
    /// <param name="provider">
    /// 定数提供者——**必须提供**：无参重载算的是占位 RKS，
    /// 同步/上传时会把假数据写进云端。
    /// </param>
    public SaveSummary RebuildSummary(IDifficultyProvider provider)
    {
        if (Info == null) throw new InvalidOperationException("Slot info is null.");
        if (File == null) throw new InvalidOperationException("Save file is null. Use 'save fetch' first.");

        Info.CloudSummary = File.GenerateSummary(provider);
        return Info.CloudSummary;
    }
}
