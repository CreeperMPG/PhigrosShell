using PhigrosArchive;
using PhigrosArchive.Save;

namespace PhigrosShell.Mapping;

/// <summary>
/// Shell 适配层，持有 PhigrosPlayerInfo 和槽位列表。
/// 提供登录、获取存档等操作入口。
/// </summary>
internal class ShellSession
{
    /// <summary>字段（隐藏于 VDirectory）</summary>
    internal PhigrosPlayerInfo? PlayerInfo;

    /// <summary>字段（隐藏于 VDirectory）—— 槽位列表</summary>
    internal List<ShellSaveSlot> SaveFiles = new();

    // ── 登录 ──

    /// <summary>通过 Token 登录并获取所有槽位信息</summary>
    public static async Task<ShellSession> LoginAsync(string token)
    {
        var playerInfo = await PhigrosPlayerInfo.FetchAsync(token).ConfigureAwait(false);
        var session = new ShellSession { PlayerInfo = playerInfo };

        var saveInfos = await playerInfo.FetchSaveInfoAsync().ConfigureAwait(false);
        session.SaveFiles = saveInfos.Select((info, i) => new ShellSaveSlot
        {
            Info = info,
            SlotIndex = i
        }).ToList();

        return session;
    }

    /// <summary>通过 QR 登录结果创建 ShellSession</summary>
    public static async Task<ShellSession> CreateFromQRAsync(PhigrosPlayerInfo playerInfo, string token)
    {
        var session = new ShellSession { PlayerInfo = playerInfo };
        var saveInfos = await playerInfo.FetchSaveInfoAsync().ConfigureAwait(false);
        session.SaveFiles = saveInfos.Select((info, i) => new ShellSaveSlot
        {
            Info = info,
            SlotIndex = i
        }).ToList();
        return session;
    }

    /// <summary>刷新 Token</summary>
    public async Task<bool> RefreshTokenAsync()
    {
        if (PlayerInfo == null) return false;
        return await PlayerInfo.RefreshTokenAsync().ConfigureAwait(false);
    }
}
