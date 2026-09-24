namespace PhigrosShell.Utils;

/// <summary>
/// 虚拟文件系统的路径解析工具：把用户敲的一串路径结合当前目录，规范成绝对路径。
/// 纯字符串处理，不接触磁盘、不接触 VDirectory。
/// </summary>
internal static class PathUtils
{
    /// <summary>
    /// 把 <paramref name="input"/> 相对 <paramref name="current"/> 解析为绝对路径。
    /// <list type="bullet">
    ///   <item>空输入 → 原样返回 <paramref name="current"/></item>
    ///   <item>以 <c>/</c> 开头 → 视为绝对路径，忽略 <paramref name="current"/></item>
    ///   <item>其余 → 拼到当前目录后面，再消解 <c>.</c> 与 <c>..</c></item>
    /// </list>
    /// </summary>
    public static string ResolvePath(string current, string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return current;

        string combined = input.StartsWith('/')
            ? input
            : current.TrimEnd('/') + "/" + input;

        return Normalize(combined);
    }

    /// <summary>消解路径里的 <c>.</c> / <c>..</c> / 重复分隔符，返回以 <c>/</c> 开头的规范形式</summary>
    private static string Normalize(string path)
    {
        var parts = new List<string>();
        foreach (var part in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == ".") continue;

            if (part == "..")
            {
                if (parts.Count > 0) parts.RemoveAt(parts.Count - 1);
                continue;
            }

            parts.Add(part);
        }

        return "/" + string.Join('/', parts);
    }
}
