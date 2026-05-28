namespace PhigrosShell.VFS;

internal class VEntry
{
    public string Name { get; set; }
    public VEntryType Type { get; set; }
    public bool IsReadOnly { get; set; }

    public VEntry(string name, VEntryType type, bool isReadOnly = false)
    {
        Name = name;
        Type = type;
        IsReadOnly = isReadOnly;
    }
}
