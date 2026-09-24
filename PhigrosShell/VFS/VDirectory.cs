using System.Collections;
using System.Globalization;
using System.Reflection;
using PhigrosShell.Utils;

namespace PhigrosShell.VFS;

/// <summary>
/// 虚拟文件系统（VFS）：把对象的**属性**、<see cref="IDictionary"/> 的键、
/// <see cref="IList"/> 的下标统一映射成目录树。
/// <para>
/// 设计要点：**路径遍历只有一份实现**（<see cref="Resolve"/>），
/// 读（<see cref="Get"/> / <see cref="Exists"/> / <see cref="ListEntries"/>）与
/// 写（<see cref="Set"/> / <see cref="Create"/> / <see cref="Remove"/>）都建立在它之上。
/// 加新的容器类型时只需要改 <see cref="Resolve"/> 的分支和写操作的分派。
/// </para>
/// </summary>
internal class VDirectory
{
    private readonly object _root;

    public VDirectory(object root) => _root = root;

    // ────────────────────────────── 反射助手 ──────────────────────────────

    /// <summary>
    /// 可作为目录条目的属性：公开、可读、且**不是索引器**。
    /// <para>
    /// 索引器（<c>this[int]</c>）在反射里也是一个叫 <c>Item</c> 的属性，但读它需要下标参数，
    /// 直接 <c>GetValue(obj)</c> 会抛 <c>TargetParameterCountException</c>。
    /// 真正的容器类型会走 <see cref="IList"/> / <see cref="IDictionary"/> 分支，
    /// 所以索引器一律跳过——注意 <c>SongDifficultySet&lt;T&gt;</c> 就带索引器。
    /// </para>
    /// </summary>
    private static IEnumerable<PropertyInfo> EnumerableProperties(Type type)
        => type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
               .Where(p => p.CanRead && p.GetIndexParameters().Length == 0);

    /// <summary>按名字找可遍历属性（忽略大小写）；索引器不算</summary>
    private static PropertyInfo? FindProperty(Type type, string name)
        => EnumerableProperties(type)
            .FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>属性能否被外部写入</summary>
    private static bool IsWritable(PropertyInfo property)
        => property.CanWrite && property.SetMethod?.IsPublic == true;

    /// <summary>字典里按名字找键（忽略大小写）；找不到返回 null</summary>
    private static object? MatchKey(IDictionary dictionary, string name)
        => dictionary.Keys.Cast<object?>()
            .FirstOrDefault(k => string.Equals(k?.ToString(), name, StringComparison.OrdinalIgnoreCase));

    /// <summary>字典的值类型（非泛型字典退化为 <see cref="object"/>）</summary>
    private static Type DictionaryValueType(IDictionary dictionary)
    {
        var type = dictionary.GetType();
        var args = type.IsGenericType ? type.GetGenericArguments() : Type.EmptyTypes;
        return args.Length == 2 ? args[1] : typeof(object);
    }

    /// <summary>列表的元素类型（数组与非泛型列表都能应付）</summary>
    private static Type ListElementType(IList list)
    {
        var type = list.GetType();
        if (type.IsArray) return type.GetElementType() ?? typeof(object);

        var args = type.IsGenericType ? type.GetGenericArguments() : Type.EmptyTypes;
        return args.Length > 0 ? args[0] : typeof(object);
    }

    // ────────────────────────────── 类型判定 ──────────────────────────────

    public static VEntryType GetEntryType(object? obj)
    {
        if (obj == null) return VEntryType.Null;

        var type = obj.GetType();

        if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal))
            return VEntryType.File;

        // 字典要排在列表前面：Dictionary 同时实现 ICollection，将来若出现两者都实现的类型，
        // 先判 IList 会把它错认成 Enumerable
        if (typeof(IDictionary).IsAssignableFrom(type))
            return VEntryType.Dictionary;

        if (typeof(IList).IsAssignableFrom(type))
            return VEntryType.Enumerable;

        if (type.IsEnum)
            return VEntryType.Enum;

        return VEntryType.Directory;
    }

    // ────────────────────────────── 路径解析 ──────────────────────────────

    /// <summary>
    /// 一次路径解析的完整结果。
    /// <para>
    /// <see cref="Owner"/> 是**装着目标的容器**，目标本身怎么被定位由
    /// <see cref="Dictionary"/> / <see cref="List"/> / <see cref="Property"/> 三者之一描述——
    /// 写操作靠这个区分"该往哪里写"。
    /// </para>
    /// </summary>
    private sealed class Resolved
    {
        /// <summary>途经的对象链（含根与最终值），供只读保护判定</summary>
        public List<object?> Chain { get; } = new();

        /// <summary>装着目标的容器；根路径时即根对象</summary>
        public object? Owner { get; set; }

        /// <summary>目标的当前值；不存在时为 null</summary>
        public object? Value { get; set; }

        /// <summary>目标由属性定位时非 null</summary>
        public PropertyInfo? Property { get; set; }

        /// <summary>容器是字典时非 null</summary>
        public IDictionary? Dictionary { get; set; }

        /// <summary>容器是列表时非 null</summary>
        public IList? List { get; set; }

        /// <summary>字典中命中的真实键；未命中为 null</summary>
        public object? ExistingKey { get; set; }

        /// <summary>用户输入的最后一段（新建字典项时用作键名）</summary>
        public string? RequestedName { get; set; }

        /// <summary>列表下标；非列表语境为 -1</summary>
        public int Index { get; set; } = -1;

        /// <summary>路径能否读到值</summary>
        public bool Found { get; set; }
    }

    /// <summary>
    /// 解析路径。所有读写操作都从这里拿到「容器 + 定位方式」。
    /// </summary>
    private Resolved Resolve(string path)
    {
        var result = new Resolved { Owner = _root, Value = _root, Found = true };
        result.Chain.Add(_root);

        if (string.IsNullOrWhiteSpace(path) || path.Trim('/').Length == 0)
            return result;

        var parts = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        object? current = _root;

        for (int i = 0; i < parts.Length; i++)
        {
            if (current == null) return Missing(result);

            string part = parts[i];
            bool isLast = i == parts.Length - 1;

            if (current is IDictionary dictionary)
            {
                object? key = MatchKey(dictionary, part);

                if (isLast)
                {
                    result.Owner = current;
                    result.Dictionary = dictionary;
                    result.ExistingKey = key;
                    result.RequestedName = part;
                    result.Value = key != null ? dictionary[key] : null;
                    result.Found = key != null;
                    result.Chain.Add(result.Value);
                    return result;
                }

                if (key == null) return Missing(result);
                current = dictionary[key];
            }
            else if (current is IList list)
            {
                if (!int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index))
                    return Missing(result);

                if (isLast)
                {
                    result.Owner = current;
                    result.List = list;
                    result.Index = index;
                    result.RequestedName = part;
                    result.Found = index >= 0 && index < list.Count;
                    result.Value = result.Found ? list[index] : null;
                    result.Chain.Add(result.Value);
                    return result;
                }

                if (index < 0 || index >= list.Count) return Missing(result);
                current = list[index];
            }
            else
            {
                var property = FindProperty(current.GetType(), part);
                if (property == null) return Missing(result);

                if (isLast)
                {
                    result.Owner = current;
                    result.Property = property;
                    result.RequestedName = part;
                    result.Value = property.GetValue(current);
                    result.Found = true;
                    result.Chain.Add(result.Value);
                    return result;
                }

                current = property.GetValue(current);
            }

            result.Chain.Add(current);
        }

        return Missing(result);

        // 走到死路：把结果标记为"读不到"，但保留已经走过的链（只读判定要用）
        static Resolved Missing(Resolved r)
        {
            r.Found = false;
            r.Owner = null;
            r.Value = null;
            r.Property = null;
            r.Dictionary = null;
            r.List = null;
            r.ExistingKey = null;
            r.Index = -1;
            return r;
        }
    }

    // ────────────────────────────── 读 ──────────────────────────────

    public object? Get(string path) => Resolve(path).Value;

    public bool Exists(string path) => Resolve(path).Found;

    /// <summary>路径上是否有任一环节被 <see cref="VDirectoryTypeRegistry"/> 标记为不可修改</summary>
    private static bool IsProtected(Resolved resolved)
        => resolved.Chain.Any(obj => obj != null
            && VDirectoryTypeRegistry.GetInfo(obj.GetType())?.DisallowModify == true);

    public List<VEntry> ListEntries(string path)
    {
        var entries = new List<VEntry>();
        var resolved = Resolve(path);
        object? target = resolved.Value;

        if (target == null)
        {
            entries.Add(new VEntry("(null)", VEntryType.Null));
            return entries;
        }

        // 父节点受保护时，下面所有条目都应显示为只读，否则 ls 会骗人
        bool inheritedReadOnly = IsProtected(resolved);

        if (target is IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
                entries.Add(new VEntry(entry.Key.ToString() ?? "(null)",
                    GetEntryType(entry.Value), inheritedReadOnly));
        }
        else if (target is IList list)
        {
            for (int i = 0; i < list.Count; i++)
                entries.Add(new VEntry(i.ToString(), GetEntryType(list[i]), inheritedReadOnly));
        }
        else
        {
            foreach (var property in EnumerableProperties(target.GetType()))
                entries.Add(new VEntry(property.Name, GetEntryType(property.GetValue(target)),
                    isReadOnly: inheritedReadOnly || !IsWritable(property)));
        }

        return entries;
    }

    // ────────────────────────────── 预览 ──────────────────────────────

    public string? GetPreview(string entryPath, VEntryType entryType = VEntryType.Directory)
    {
        object? obj = Get(entryPath);

        switch (entryType)
        {
            case VEntryType.File:
                return obj == null ? "(null)" : TruncatePreview(Sanitize(obj.ToString() ?? ""));

            case VEntryType.Enumerable when obj is IEnumerable enumerable:
            {
                var items = enumerable.Cast<object?>().Take(10).ToList();
                string body = string.Join(", ", items.Select(RenderValue));
                return TruncatePreview($"List => [{body}{(enumerable.Cast<object?>().Count() > 10 ? "..." : "]")}");
            }
            case VEntryType.Enumerable:
                return "List => (Not enumerable)";

            case VEntryType.Dictionary when obj is IDictionary dictionary:
            {
                string body = string.Join(", ", dictionary.Keys.Cast<object?>()
                    .Select(k => $"{k}: {RenderValue(dictionary[k!])}"));
                return TruncatePreview($"Dictionary => {{{body}}}");
            }
            case VEntryType.Dictionary:
                return "Dictionary => (Not a dictionary)";

            case VEntryType.Enum:
                return obj?.ToString() ?? "(null)";

            case VEntryType.Directory when obj != null:
            {
                var generator = VDirectoryTypeRegistry.GetInfo(obj.GetType())?.PreviewGenerator;
                if (generator == null) return null;

                try
                {
                    string? generated = generator(obj);
                    return generated == null ? null : TruncatePreview(Sanitize(generated));
                }
                catch
                {
                    return null;
                }
            }
        }

        return null;
    }

    public string? GetPreview(string parentPath, VEntry entry)
        => GetPreview(parentPath.TrimEnd('/') + "/" + entry.Name, entry.Type);

    /// <summary>
    /// 把一个值渲染成预览文本：优先用它自己注册的 <c>PreviewGenerator</c>，
    /// 没有才退回 <c>ToString()</c>。
    /// <para>
    /// 这样字典/列表里的元素也能享受类型注册（否则
    /// <c>SongDifficultySet</c> 这种没重载 <c>ToString</c> 的会显示出完整类型名）。
    /// </para>
    /// </summary>
    private static string RenderValue(object? value)
    {
        if (value == null) return "(null)";

        var generator = VDirectoryTypeRegistry.GetInfo(value.GetType())?.PreviewGenerator;
        if (generator != null)
        {
            try
            {
                string? rendered = generator(value);
                if (!string.IsNullOrEmpty(rendered)) return rendered;
            }
            catch { }
        }

        return value.ToString() ?? "(null)";
    }

    // ────────────────────────────── 写 ──────────────────────────────

    /// <summary>
    /// 修改一个**已存在**的目标（`modify`）：给属性赋值、覆写字典项、替换列表元素。
    /// 目标不存在时失败——新建请用 <see cref="Create"/>。
    /// </summary>
    public WriteResult Set(string path, object? value) => Assign(Resolve(path), path, value);

    private WriteResult Assign(Resolved resolved, string path, object? value)
    {
        if (!resolved.Found) return WriteResult.NotFound;
        if (IsProtected(resolved)) return WriteResult.Protected;

        Type targetType;
        Action<object?> commit;

        if (resolved.Dictionary != null && resolved.ExistingKey != null)
        {
            var dictionary = resolved.Dictionary;
            object key = resolved.ExistingKey;
            targetType = DictionaryValueType(dictionary);
            commit = v => dictionary[key] = v;
        }
        else if (resolved.List != null && resolved.Index >= 0 && resolved.Index < resolved.List.Count)
        {
            var list = resolved.List;
            int index = resolved.Index;
            targetType = ListElementType(list);
            commit = v => list[index] = v;
        }
        else if (resolved.Property != null)
        {
            if (!IsWritable(resolved.Property)) return WriteResult.ReadOnlyProperty;
            var property = resolved.Property;
            object owner = resolved.Owner!;
            targetType = property.PropertyType;
            commit = v => property.SetValue(owner, v);
        }
        else
        {
            return WriteResult.Unsupported;
        }

        if (!TryConvert(value, targetType, out object? converted)) return WriteResult.BadValue;

        commit(converted);
        NotifyModified(path, resolved.RequestedName ?? path);
        return WriteResult.Success;
    }

    /// <summary>
    /// 让一个目标**开始存在**（`touch`）。三种语境三种含义：
    /// <list type="bullet">
    ///   <item>容器是字典 → 新建/覆盖该键</item>
    ///   <item>容器是列表 → 追加到末尾（下标必须正好等于当前长度）</item>
    ///   <item>容器是普通对象 → 把**值为 null** 的属性实例化</item>
    /// </list>
    /// ⚠️ **值为 null 不算"已存在"**——那正是本方法要实例化的对象。
    /// 只有目标**存在且非 null** 时才返回 <see cref="WriteResult.AlreadyExists"/>。
    /// </summary>
    public WriteResult Create(string path, object? value)
    {
        var resolved = Resolve(path);
        if (IsProtected(resolved)) return WriteResult.Protected;

        if (resolved.Dictionary != null)
        {
            if (resolved.RequestedName == null) return WriteResult.Unsupported;

            var produced = Produce(value, DictionaryValueType(resolved.Dictionary), out WriteResult failure);
            if (produced == null && failure != WriteResult.Success) return failure;

            resolved.Dictionary[resolved.RequestedName] = produced;
            NotifyModified(path, resolved.RequestedName);
            return WriteResult.Success;
        }

        if (resolved.List != null)
        {
            // 只能往末尾追加：中间插一个会打乱既有下标
            if (resolved.Index != resolved.List.Count) return WriteResult.Unsupported;

            var produced = Produce(value, ListElementType(resolved.List), out WriteResult failure);
            if (produced == null && failure != WriteResult.Success) return failure;

            resolved.List.Add(produced);
            NotifyModified(path, resolved.Index.ToString());
            return WriteResult.Success;
        }

        if (resolved.Property != null)
        {
            if (!IsWritable(resolved.Property)) return WriteResult.ReadOnlyProperty;

            // 有值 → 让用户改用 modify，免得"touch 顺手覆盖"变成隐形陷阱。
            // 注意判的是**值**不是"属性存在与否"。
            if (resolved.Value != null) return WriteResult.AlreadyExists;

            var produced = Produce(value, resolved.Property.PropertyType, out WriteResult failure);
            if (produced == null && failure != WriteResult.Success) return failure;

            resolved.Property.SetValue(resolved.Owner, produced);
            NotifyModified(path, resolved.Property.Name);
            return WriteResult.Success;
        }

        return WriteResult.Unsupported;
    }

    /// <summary>
    /// 删除 / 清空一个目标（`rm`）：
    /// 字典与列表**真删除**，普通属性**置 null**（需要类型白名单，见
    /// <see cref="VDirectoryTypeRegistry.CanResetToNull"/>）。
    /// </summary>
    public WriteResult Remove(string path)
    {
        var resolved = Resolve(path);
        if (!resolved.Found) return WriteResult.NotFound;
        if (IsProtected(resolved)) return WriteResult.Protected;

        if (resolved.Dictionary != null && resolved.ExistingKey != null)
        {
            resolved.Dictionary.Remove(resolved.ExistingKey);
            NotifyModified(path, resolved.ExistingKey.ToString() ?? path);
            return WriteResult.Success;
        }

        if (resolved.List != null && resolved.Index >= 0 && resolved.Index < resolved.List.Count)
        {
            resolved.List.RemoveAt(resolved.Index);
            NotifyModified(path, resolved.Index.ToString());
            return WriteResult.Success;
        }

        if (resolved.Property != null)
        {
            if (!IsWritable(resolved.Property)) return WriteResult.ReadOnlyProperty;

            // 值类型没法置 null；引用类型也要类型自己开白名单
            Type type = Nullable.GetUnderlyingType(resolved.Property.PropertyType)
                        ?? resolved.Property.PropertyType;
            if (type.IsValueType || !VDirectoryTypeRegistry.CanResetToNull(type))
                return WriteResult.NotAllowed;

            resolved.Property.SetValue(resolved.Owner, null);
            NotifyModified(path, resolved.Property.Name);
            return WriteResult.Success;
        }

        return WriteResult.Unsupported;
    }

    // ────────────────────────────── 值与实例化 ──────────────────────────────

    /// <summary>
    /// 把用户输入的字符串转成目标类型。失败返回 false（不抛异常）。
    /// </summary>
    private static bool TryConvert(object? value, Type declaredType, out object? converted)
    {
        converted = null;

        Type type = Nullable.GetUnderlyingType(declaredType) ?? declaredType;

        if (value == null)
        {
            if (type.IsValueType) return false;
            return true;
        }

        if (type.IsInstanceOfType(value))
        {
            converted = value;
            return true;
        }

        string text = value.ToString() ?? "";

        if (type == typeof(string))
        {
            converted = text;
            return true;
        }

        if (type.IsEnum)
        {
            if (!Enum.TryParse(type, text, ignoreCase: true, out object? parsed)) return false;
            converted = parsed;
            return true;
        }

        if (type == typeof(bool))
        {
            if (bool.TryParse(text, out bool flag) || text == "1" || text == "0")
            {
                converted = flag || text == "1";
                return true;
            }
            return false;
        }

        try
        {
            // 用不变文化：中文区域下 "3.14" 否则会解析失败
            converted = Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 产出要写进去的值：调用方给了就用（转换后），没给就**按类型白名单实例化**一个默认值。
    /// <para>
    /// 成功时 <paramref name="failure"/> 为 <see cref="WriteResult.Success"/>；
    /// 失败时给出**具体原因**（值转不过去 vs 类型没开白名单），
    /// 好让上层提示准确而不是猜。
    /// </para>
    /// </summary>
    private static object? Produce(object? value, Type declaredType, out WriteResult failure)
    {
        if (value != null)
        {
            if (!TryConvert(value, declaredType, out object? converted))
            {
                failure = WriteResult.BadValue;
                return null;
            }

            failure = WriteResult.Success;
            return converted;
        }

        object? created = TryInstantiate(declaredType);
        failure = created == null ? WriteResult.NotAllowed : WriteResult.Success;
        return created;
    }

    /// <summary>
    /// 按声明类型 new 一个默认实例。
    /// 值类型默认放行；**引用类型必须在注册表里显式开
    /// <see cref="VDirectoryTypeInfo.CanInstantiate"/>**——任意构造函数可能有副作用。
    /// </summary>
    private static object? TryInstantiate(Type declaredType)
    {
        Type type = Nullable.GetUnderlyingType(declaredType) ?? declaredType;

        if (type.IsAbstract || type.IsInterface) return null;
        if (!VDirectoryTypeRegistry.CanInstantiate(type)) return null;

        try
        {
            return Activator.CreateInstance(type);
        }
        catch
        {
            return null;
        }
    }

    // ────────────────────────────── 变更通知 ──────────────────────────────

    private void NotifyModified(string path, string changedName)
    {
        string targetPath = (System.IO.Path.GetDirectoryName(path) ?? "/").Replace("\\", "/");
        var parts = targetPath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);

        for (int depth = parts.Length; depth >= 0; depth--)
        {
            string ancestor = depth == 0 ? "/" : "/" + string.Join('/', parts.Take(depth));
            object? obj = Get(ancestor);
            var handler = obj == null ? null : VDirectoryTypeRegistry.GetInfo(obj.GetType())?.OnModifyHandler;
            if (handler == null) continue;

            try { handler(targetPath, changedName); }
            catch { }
        }
    }

    // ────────────────────────────── 预览文本处理 ──────────────────────────────

    /// <summary>预览列的目标显示宽度（全角字符按 2 计）</summary>
    private const int PreviewWidth = 60;

    /// <summary>
    /// 按**显示宽度**截断：用 <see cref="ConsoleUtils.GetDisplayWidth"/> 而不是
    /// <c>string.Length</c>——后者在中文/日文曲名上会算少一半，把后面的列挤歪。
    /// </summary>
    private static string TruncatePreview(string text)
    {
        if (ConsoleUtils.GetDisplayWidth(text) <= PreviewWidth) return text;

        var builder = new System.Text.StringBuilder(PreviewWidth);
        int width = 0;

        foreach (char c in text)
        {
            int charWidth = ConsoleUtils.IsFullWidthChar(c) ? 2 : 1;

            // 放不下整个字符（例如只剩 1 格却遇到全角）就到此为止，免得截出半个字
            if (width + charWidth > PreviewWidth - 3) break;

            builder.Append(c);
            width += charWidth;
        }

        return builder.Append("...").ToString();
    }

    /// <summary>把会破坏排版的字符换成可见/中性的形式</summary>
    private static string Sanitize(string text)
    {
        var builder = new System.Text.StringBuilder(text.Length);

        foreach (char c in text)
        {
            // 换行、制表符折成空格；其余控制字符（含 \b 这种能覆盖已输出内容的）直接丢掉
            if (c is '\r' or '\n' or '\t')
                builder.Append(' ');
            else if (!char.IsControl(c))
                builder.Append(c);
        }

        return builder.ToString().Trim();
    }
}
