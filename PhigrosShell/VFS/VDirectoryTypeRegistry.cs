namespace PhigrosShell.VFS;

/// <summary>
/// 路径类型信息，注册后 VDirectory 在遍历路径时会查询此注册表
/// 替代旧版的 VDirectoryOptionAttribute + VSpecialDirectoryTypeBase
/// </summary>
public class VDirectoryTypeInfo
{
    /// <summary>是否禁止修改此类型节点下的内容</summary>
    public bool DisallowModify { get; set; }

    /// <summary>生成目录预览文本（用于 ls 命令的预览列）</summary>
    public Func<object, string?>? PreviewGenerator { get; set; }

    /// <summary>当子项被修改时触发（参数：targetPath, filename）</summary>
    public Action<string, string>? OnModifyHandler { get; set; }

    /// <summary>当子项被创建 (touch) 时触发</summary>
    public Action<string, string>? OnTouchHandler { get; set; }
}

/// <summary>
/// 类型注册中心，VDirectory 通过 GetInfo(type) 查询行为信息
/// </summary>
public static class VDirectoryTypeRegistry
{
    private static readonly Dictionary<Type, VDirectoryTypeInfo> _registry = new();

    public static void Register<T>(Action<VDirectoryTypeInfo> configure)
    {
        var info = new VDirectoryTypeInfo();
        configure(info);
        _registry[typeof(T)] = info;
    }

    public static VDirectoryTypeInfo? GetInfo(Type type)
    {
        if (_registry.TryGetValue(type, out var info))
            return info;

        // Check base types
        var baseType = type.BaseType;
        while (baseType != null)
        {
            if (_registry.TryGetValue(baseType, out info))
                return info;
            baseType = baseType.BaseType;
        }

        return null;
    }
}
