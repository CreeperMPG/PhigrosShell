namespace PhigrosShell.VFS;

/// <summary>
/// 写操作（<c>modify</c> / <c>touch</c> / <c>rm</c>）的结果。
/// <para>
/// 用枚举而不是 <c>bool</c>，是为了让调用方能给出**准确**的提示。
/// 只返回 bool 时命令层只能拿"路径存不存在"去猜失败原因——
/// 曾把"类型没开白名单"报成"已存在，请用 modify"，自相矛盾。
/// </para>
/// </summary>
internal enum WriteResult
{
    Success,

    /// <summary>路径解析不到（或目标不存在）</summary>
    NotFound,

    /// <summary>路径上某一环被 <see cref="VDirectoryTypeRegistry"/> 注册为不可修改</summary>
    Protected,

    /// <summary>属性没有公开 setter</summary>
    ReadOnlyProperty,

    /// <summary>
    /// <c>touch</c> 专用的"已经有值了"：目标**存在且非 null**，该改用 <c>modify</c>。
    /// <para>
    /// ⚠️ 值为 <c>null</c> 的属性**不算**已存在——那正是 <c>touch</c> 要实例化的对象。
    /// </para>
    /// </summary>
    AlreadyExists,

    /// <summary>目标类型没开写操作白名单（不能实例化 / 不能置 null）</summary>
    NotAllowed,

    /// <summary>提供的值转不成目标类型</summary>
    BadValue,

    /// <summary>该语境不支持这个操作（例如往列表中间插入）</summary>
    Unsupported
}
