using CreeperMPG.PhiKits.Save.CloudStorage;

namespace PhigrosShell.Mapping;

/// <summary>
/// Shell 适配层：持有一个已登录的 <see cref="PlayerObject"/> 和它名下的槽位列表。
/// 登录、重载槽位、刷新 Token 都从这里进。
/// </summary>
internal class ShellSession
{
    /// <summary>字段（隐藏于 VDirectory）</summary>
    internal PlayerObject? PlayerInfo;

    /// <summary>字段（隐藏于 VDirectory）—— 槽位列表，按云端更新时间从晚到早</summary>
    internal List<ShellSaveSlot> SaveFiles = new();

    // ── 建立会话 ──

    /// <summary>Token 登录：拉取玩家信息，再拉取槽位列表</summary>
    public static async Task<ShellSession> LoginAsync(string token)
        => await CreateAsync(await PlayerObject.FetchAsync(token).ConfigureAwait(false)).ConfigureAwait(false);

    /// <summary>用已登录的玩家对象（例如 QR 登录的产物）建立会话</summary>
    public static async Task<ShellSession> CreateAsync(PlayerObject player)
    {
        var session = new ShellSession { PlayerInfo = player };
        await session.ReloadSaveFilesAsync().ConfigureAwait(false);
        return session;
    }

    // ── 槽位列表 ──

    /// <summary>
    /// 重新拉取云端槽位列表，按 <c>SaveUpdateTime</c> 从晚到早排列并重排索引。
    /// <para>
    /// 时间戳是 ISO 8601 字符串，字典序即时间序；没有时间戳的槽位（空串）
    /// 在降序里自然落到最后。<c>save</c> 命令里的槽位号就是这里排出来的序号。
    /// </para>
    /// </summary>
    public async Task ReloadSaveFilesAsync()
    {
        if (PlayerInfo == null)
        {
            SaveFiles = new List<ShellSaveSlot>();
            return;
        }

        var infos = await PlayerInfo.GetSaveInfo().ConfigureAwait(false);

        SaveFiles = infos
            .OrderByDescending(info => info.SaveUpdateTime, StringComparer.Ordinal)
            .Select((info, index) => new ShellSaveSlot { Info = info, SlotIndex = index })
            .ToList();
    }

    // ── Token ──

    /// <summary>刷新 Token；失败返回 false</summary>
    public async Task<bool> RefreshTokenAsync()
        => PlayerInfo != null && await PlayerInfo.RefreshToken().ConfigureAwait(false);
}
