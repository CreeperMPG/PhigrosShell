using System.Collections;
using System.Reflection;

namespace PhigrosShell.VFS;

/// <summary>
/// 虚拟文件系统（VFS），通过反射遍历对象属性来模拟目录结构。
/// 不再使用 VDirectoryOptionAttribute，改为查询 VDirectoryTypeRegistry。
/// </summary>
internal class VDirectory
{
    private readonly object _root;

    public VDirectory(object root)
    {
        _root = root;
    }

    public static VEntryType GetEntryType(object? obj)
    {
        if (obj == null) return VEntryType.Null;

        var type = obj.GetType();
        if (type.IsPrimitive || type == typeof(string))
            return VEntryType.File;
        if (typeof(IList).IsAssignableFrom(type))
            return VEntryType.Enumerable;
        if (typeof(IDictionary).IsAssignableFrom(type))
            return VEntryType.Dictionary;
        if (type.IsEnum)
            return VEntryType.Enum;

        return VEntryType.Directory;
    }

    private (object? obj, string? member, int? index) ResolvePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path == "/")
            return (_root, null, null);

        var parts = path.Trim('/').Split('/');
        object? obj = _root;

        for (int i = 0; i < parts.Length; i++)
        {
            if (obj == null) return (null, null, null);

            string part = parts[i];

            if (obj is IDictionary dict)
            {
                var key = dict.Keys.Cast<object>()
                    .FirstOrDefault(k => string.Equals(k.ToString(), part, StringComparison.OrdinalIgnoreCase));
                if (key == null) return (null, null, null);

                if (i == parts.Length - 1)
                    return (dict, key.ToString(), null);
                obj = dict[key];
            }
            else if (int.TryParse(part, out int idx) && obj is IList list)
            {
                if (i == parts.Length - 1)
                    return (list, null, idx);
                if (idx >= 0 && idx < list.Count)
                    obj = list[idx];
                else
                    return (null, null, null);
            }
            else
            {
                var prop = obj.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .FirstOrDefault(p => string.Equals(p.Name, part, StringComparison.OrdinalIgnoreCase));
                if (prop == null) return (null, null, null);

                if (i == parts.Length - 1)
                    return (obj, prop.Name, null);
                obj = prop.GetValue(obj);
            }
        }

        return (obj, null, null);
    }

    public List<VEntry> ListEntries(string path)
    {
        var (resolvedObj, member, index) = ResolvePath(path);
        var entries = new List<VEntry>();

        if (resolvedObj == null)
        {
            entries.Add(new VEntry("(null)", VEntryType.Null));
            return entries;
        }

        // Resolve to final value
        object? target = resolvedObj;
        if (member != null && target is IDictionary dict)
        {
            var key = dict.Keys.Cast<object>()
                .FirstOrDefault(k => string.Equals(k.ToString(), member, StringComparison.OrdinalIgnoreCase));
            target = key != null ? dict[key] : null;
        }
        else if (member != null)
        {
            target = resolvedObj.GetType().GetProperty(member)?.GetValue(resolvedObj);
        }

        if (index.HasValue && target is IList list)
            target = list[index.Value];

        if (target == null)
        {
            entries.Add(new VEntry("(null)", VEntryType.Null));
            return entries;
        }

        if (target is IList listEntries)
        {
            for (int i = 0; i < listEntries.Count; i++)
                entries.Add(new VEntry(i.ToString(), GetEntryType(listEntries[i])));
        }
        else if (target is IDictionary dictEntries)
        {
            foreach (DictionaryEntry entry in dictEntries)
                entries.Add(new VEntry(entry.Key.ToString() ?? "(null)", GetEntryType(entry.Value)));
        }
        else
        {
            foreach (var prop in target.GetType()
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p => p.CanRead))
            {
                bool isReadOnly = !prop.CanWrite ||
                    (prop.SetMethod != null && prop.SetMethod.IsPrivate);
                entries.Add(new VEntry(prop.Name, GetEntryType(prop.GetValue(target)), isReadOnly));
            }
        }

        return entries;
    }

    public string? GetPreview(string entryPath, VEntryType entryType = VEntryType.Directory)
    {
        object? obj = Get(entryPath);

        switch (entryType)
        {
            case VEntryType.File:
            {
                string? text = obj?.ToString();
                if (text == null) return "(null)";
                text = text.Trim().Replace("\r", "").Replace("\n", " ");
                return text.Length <= 60 ? text : text[..60] + "...";
            }
            case VEntryType.Enumerable:
                if (obj is IEnumerable enumerable)
                {
                    var items = enumerable.Cast<object>().Take(10).ToList();
                    return "List => [" + string.Join(", ", items.Select(i => i?.ToString() ?? "(null)"))
                        + (enumerable.Cast<object>().Count() > 10 ? "..." : "]");
                }
                return "List => (Not enumerable)";
            case VEntryType.Dictionary:
                if (obj is IDictionary dictionary)
                {
                    var preview = string.Join(", ", dictionary.Keys.Cast<object>()
                        .Select(k => $"{k}: {dictionary[k]?.ToString() ?? "(null)"}"));
                    preview = (preview.Length <= 60) ? (preview + "}") : preview[..60] + "...";
                    return "Dictionary => {" + preview;
                }
                return "Dictionary => (Not a dictionary)";
            case VEntryType.Enum:
                return obj?.ToString() ?? "(null)";
            case VEntryType.Directory:
            {
                if (obj == null) return "(null)";
                var info = VDirectoryTypeRegistry.GetInfo(obj.GetType());
                if (info?.PreviewGenerator != null)
                {
                    try { return info.PreviewGenerator(obj); }
                    catch { }
                }
                break;
            }
        }

        return null;
    }

    public string? GetPreview(string parentPath, VEntry entry)
    {
        string entryPath = parentPath.TrimEnd('/') + "/" + entry.Name;
        return GetPreview(entryPath, entry.Type);
    }

    public bool IsReadOnly(string path)
    {
        var (resolvedObj, member, index) = ResolvePath(path);
        if (resolvedObj == null) return true;

        if (member != null)
        {
            var prop = resolvedObj.GetType().GetProperty(member);
            if (prop == null || !prop.CanWrite ||
                (prop.SetMethod != null && prop.SetMethod.IsPrivate))
                return true;
            return false;
        }

        if (index.HasValue && resolvedObj is IList)
            return false;
        return false;
    }

    public object? Get(string path)
    {
        var (resolvedObj, member, index) = ResolvePath(path);
        if (resolvedObj == null) return null;

        if (member != null && resolvedObj is IDictionary dict)
        {
            var key = dict.Keys.Cast<object>()
                .FirstOrDefault(k => string.Equals(k.ToString(), member, StringComparison.OrdinalIgnoreCase));
            if (key != null) return dict[key];
        }
        if (member != null)
            return resolvedObj.GetType().GetProperty(member)?.GetValue(resolvedObj);
        if (index.HasValue && resolvedObj is IList list)
            return list[index.Value];

        return resolvedObj;
    }

    public bool IsDisallowToModify(string path)
    {
        var parts = path.Trim('/').Split('/');
        object? obj = _root;

        for (int i = 0; i < parts.Length; i++)
        {
            if (obj == null) return false;
            string part = parts[i];

            // Check type registration for current object
            var info = VDirectoryTypeRegistry.GetInfo(obj.GetType());
            if (info?.DisallowModify == true) return true;

            if (obj is IDictionary dict)
            {
                var key = dict.Keys.Cast<object>()
                    .FirstOrDefault(k => string.Equals(k.ToString(), part, StringComparison.OrdinalIgnoreCase));
                if (key != null) obj = dict[key];
                else return false;
            }
            else if (int.TryParse(part, out int idx) && obj is IList list)
            {
                if (idx < 0 || idx >= list.Count) return false;
                obj = list[idx];
            }
            else
            {
                var prop = obj.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .FirstOrDefault(p => string.Equals(p.Name, part, StringComparison.OrdinalIgnoreCase));
                if (prop != null) obj = prop.GetValue(obj);
                else return false;
            }
        }

        if (obj != null)
        {
            var finalInfo = VDirectoryTypeRegistry.GetInfo(obj.GetType());
            if (finalInfo?.DisallowModify == true) return true;
        }

        return false;
    }

    public bool Set(string path, object value)
    {
        if (IsDisallowToModify(path)) return false;

        string targetPath = (System.IO.Path.GetDirectoryName(path) ?? "/").Replace("\\", "/");
        var (resolvedObj, member, index) = ResolvePath(path);
        if (resolvedObj == null) return false;

        bool modified = false;
        string? changedName = null;

        if (member != null)
        {
            var prop = resolvedObj.GetType().GetProperty(member);
            if (prop == null || !prop.CanWrite ||
                (prop.SetMethod != null && prop.SetMethod.IsPrivate))
                return false;

            prop.SetValue(resolvedObj, Convert.ChangeType(value, prop.PropertyType));
            changedName = member;
            modified = true;
        }
        else if (index.HasValue && resolvedObj is IList list)
        {
            var elementType = list.GetType().GetGenericArguments().FirstOrDefault()
                ?? list.GetType().GetElementType() ?? typeof(object);
            try { value = Convert.ChangeType(value, elementType); }
            catch { return false; }

            if (index.Value < 0 || index.Value >= list.Count) return false;
            list[index.Value] = value;
            changedName = index.Value.ToString();
            modified = true;
        }

        if (modified && changedName != null)
            NotifyTypeRegistrationsOnModify(targetPath, changedName);

        return modified;
    }

    private void NotifyTypeRegistrationsOnModify(string targetPath, string filename)
    {
        var parts = targetPath.Trim('/').Split('/');

        for (int depth = parts.Length; depth >= 0; depth--)
        {
            string path = depth == 0 ? "/" : string.Join('/', parts.Take(depth));
            object? obj = Get(path);
            if (obj == null) continue;

            var info = VDirectoryTypeRegistry.GetInfo(obj.GetType());
            if (info?.OnModifyHandler != null)
            {
                try { info.OnModifyHandler(targetPath, filename); }
                catch { }
            }
        }
    }

    public bool Exists(string path)
    {
        var (resolvedObj, member, index) = ResolvePath(path);
        if (resolvedObj == null) return false;

        if (member != null && resolvedObj is IDictionary dict)
            return dict.Keys.Cast<object>()
                .Any(k => string.Equals(k.ToString(), member, StringComparison.OrdinalIgnoreCase));
        if (member != null)
            return resolvedObj.GetType().GetProperty(member) != null;
        if (index.HasValue && resolvedObj is IList list)
            return index.Value >= 0 && index.Value < list.Count;

        return true;
    }
}
