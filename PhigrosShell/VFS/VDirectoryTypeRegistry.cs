namespace PhigrosShell.VFS;

/// <summary>
/// 路径类型信息，注册后 VDirectory 在遍历路径时会查询此注册表
/// 替代旧版的 VDirectoryOptionAttribute + VSpecialDirectoryTypeBase
/// </summary>
public class VDirectoryTypeInfo
{
    /// <summary>是否禁止修改此类型节点下的内容</summary>
    public bool DisallowModify { get; set; }

    /// <summary>
    /// 允许 <c>touch</c> 时**按此类型实例化**一个默认值。
    /// <para>
    /// 例如往 <c>GameRecord</c> 字典里新建一首歌，需要 new 一个
    /// <c>SongDifficultySet&lt;LevelRecord&gt;</c>。
    /// 默认 false：<c>new</c> 任意类型的构造函数可能有副作用，必须显式开。
    /// （值类型不受此限制，见 <see cref="VDirectoryTypeRegistry.CanInstantiate"/>。）
    /// </para>
    /// </summary>
    public bool CanInstantiate { get; set; }

    /// <summary>
    /// 允许 <c>rm</c> 时把此类型的属性**置为 null**。
    /// <para>
    /// 例如删掉某个难度的成绩（<c>SongDifficultySet&lt;LevelRecord&gt;.IN = null</c>）。
    /// 默认 false：置 null 可能让下游代码吃 NRE，必须显式开。
    /// </para>
    /// </summary>
    public bool CanResetToNull { get; set; }

    /// <summary>生成目录预览文本（用于 ls 命令的预览列）</summary>
    public Func<object, string?>? PreviewGenerator { get; set; }

    /// <summary>当子项被修改时触发（参数：targetPath, filename）</summary>
    public Action<string, string>? OnModifyHandler { get; set; }

    /// <summary>当子项被创建 (touch) 时触发</summary>
    public Action<string, string>? OnTouchHandler { get; set; }
}

/// <summary>
/// 类型注册中心，VDirectory 通过 GetInfo(type) 查询行为信息。
/// <para>
/// 支持**开放泛型**注册（<c>Register(typeof(List&lt;&gt;), ...)</c>），
/// 这样一条规则就能覆盖 <c>SongDifficultySet&lt;Achievement&gt;</c>、
/// <c>SongDifficultySet&lt;LevelRecord?&gt;</c> 之类的全部封闭形式。
/// </para>
/// </summary>
public static class VDirectoryTypeRegistry
{
    private static readonly Dictionary<Type, VDirectoryTypeInfo> _registry = new();

    /// <summary>关闭的注册项：类型 + 是不是开放泛型定义</summary>
    private static VDirectoryTypeInfo? Lookup(Type type)
    {
        if (_registry.TryGetValue(type, out var info))
            return info;

        // 开放泛型：拿掉泛型参数再查一次（SongDifficultySet<Achievement> → SongDifficultySet<>）
        if (type.IsGenericType && _registry.TryGetValue(type.GetGenericTypeDefinition(), out info))
            return info;

        return null;
    }

    public static void Register<T>(Action<VDirectoryTypeInfo> configure)
        => Register(typeof(T), configure);

    /// <summary>
    /// 注册类型行为；注册开放泛型定义时传 <c>typeof(SongDifficultySet&lt;&gt;)</c>。
    /// <para>
    /// ⚠️ **同一个类型可以注册多次，各次配置会叠加**（不是覆盖）。
    /// 否则"先注册只读保护、再注册预览"会把保护冲掉——
    /// 这种 bug 很隐蔽：注册顺序换了就变行为，而且编译和单次注册都测不出来。
    /// </para>
    /// </summary>
    public static void Register(Type type, Action<VDirectoryTypeInfo> configure)
    {
        if (!_registry.TryGetValue(type, out var info))
        {
            info = new VDirectoryTypeInfo();
            _registry[type] = info;
        }

        configure(info);
    }

    public static VDirectoryTypeInfo? GetInfo(Type type)
    {
        var info = Lookup(type);
        if (info != null) return info;

        // Check base types
        var baseType = type.BaseType;
        while (baseType != null)
        {
            info = Lookup(baseType);
            if (info != null) return info;
            baseType = baseType.BaseType;
        }

        return null;
    }

    /// <summary>
    /// 该类型是否有预览生成器（沿基类链查）。
    /// <para>
    /// 供泛型容器类型判断"元素类型值不值得展开"——例如
    /// <c>SongDifficultySet&lt;T&gt;</c> 只在 <c>T</c> 自己有预览时才拼接各难度。
    /// </para>
    /// </summary>
    public static bool HasPreviewGenerator(Type type)
        => GetInfo(type)?.PreviewGenerator != null;

    /// <summary>
    /// 该类型能否被 <c>touch</c> 实例化。
    /// <para>
    /// **值类型默认放行**（<c>new int()</c> 就是 0，没有副作用）；
    /// **引用类型必须显式注册 <see cref="VDirectoryTypeInfo.CanInstantiate"/>**。
    /// </para>
    /// </summary>
    public static bool CanInstantiate(Type type)
        => type.IsValueType || GetInfo(type)?.CanInstantiate == true;

    /// <summary>该类型能否被 <c>rm</c> 置为 null（必须显式注册，默认禁止）</summary>
    public static bool CanResetToNull(Type type)
        => GetInfo(type)?.CanResetToNull == true;
}
