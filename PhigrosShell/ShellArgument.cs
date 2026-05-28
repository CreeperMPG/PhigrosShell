namespace PhigrosShell;

internal class ShellArgument
{
    public string Value;
    public ShellArgumentType Type;

    public ShellArgument(string value, ShellArgumentType type)
    {
        Value = value;
        Type = type;
    }

    public override string ToString() => Value;

    public ConsoleColor GetColor() => GetColor(Type);

    public KeyValuePair<string, string> GetOption()
    {
        if (Type != ShellArgumentType.Option)
            return new KeyValuePair<string, string>("", "");

        var parts = Value[1..].Split('=', 2);
        return parts.Length == 1
            ? new KeyValuePair<string, string>(parts[0], "")
            : new KeyValuePair<string, string>(parts[0], parts[1]);
    }

    public static ConsoleColor GetColor(ShellArgumentType type) => type switch
    {
        ShellArgumentType.Command => ConsoleColor.Yellow,
        ShellArgumentType.String => ConsoleColor.DarkCyan,
        ShellArgumentType.Comment => ConsoleColor.DarkGreen,
        ShellArgumentType.Option => ConsoleColor.Gray,
        _ => ConsoleColor.White,
    };
}
