using PhigrosArchive;
using PhigrosArchive.Save;

namespace PhigrosShell.Mapping;

/// <summary>
/// VDirectory 根目录映射对象。
/// 字段 -> VDirectory 不遍历；属性 -> VDirectory 可访问。
/// /SaveFiles/0/ → ShellSlotRoot 映射
/// </summary>
internal class ShellPlayerRoot
{
    /// <summary>字段，隐藏于 VDirectory</summary>
    internal PhigrosPlayerInfo? PlayerInfo;

    /// <summary>属性，VDirectory 可遍历</summary>
    public string Nickname => PlayerInfo?.Nickname ?? "(null)";
    public string ShortID => PlayerInfo?.ShortID ?? "(null)";
    public string ObjectID => PlayerInfo?.UserObjectID ?? "(null)";
    public string CreateTime => PlayerInfo?.CreateTime ?? "(null)";
    public string SessionToken => PlayerInfo?.SessionToken ?? "(null)";

    /// <summary>属性，VDirectory 通过 /SaveFiles/ 遍历到 ShellSlotRoot</summary>
    public List<ShellSlotRoot> SaveFiles { get; } = new();
}
