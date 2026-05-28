namespace PhigrosShell.Utils;

internal static class ConsoleUtils
{
    public static void ClearLine()
    {
        Console.Write("\r" + new string(' ', Console.WindowWidth - 1) + "\r");
    }

    public static void Debug(string message)
    {
        if (Program.IsDebug)
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("[DEBUG] " + message);
            Console.ResetColor();
        }
    }

    public static string? GetArgumentValue(List<ShellArgument> args, string key)
    {
        foreach (var arg in args)
        {
            var option = arg.GetOption();
            if (option.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                return option.Value;
        }
        return null;
    }

    public static void WriteError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    public static void WriteSuccess(string message, bool writeLine = true)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        if (writeLine)
            Console.WriteLine("✓ " + message);
        else
            Console.Write("✓ " + message);
        Console.ResetColor();
    }

    public static void WriteWarning(string message, bool writeLine = true)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        if (writeLine)
            Console.WriteLine("[!] " + message);
        else
            Console.Write("[!] " + message);
        Console.ResetColor();
    }

    public static string PadRightEx(this string str, int totalWidth, char paddingChar = ' ')
    {
        int displayWidth = GetDisplayWidth(str);
        if (displayWidth >= totalWidth) return str;
        return str + new string(paddingChar, totalWidth - displayWidth);
    }

    public static int GetDisplayWidth(string str)
    {
        if (string.IsNullOrEmpty(str)) return 0;
        int width = 0;
        foreach (char c in str)
            width += IsFullWidthChar(c) ? 2 : 1;
        return width;
    }

    public static bool IsFullWidthChar(char c)
    {
        return c is >= '\u4E00' and <= '\u9FFF' or // CJK Unified Ideographs
               >= '\uFF01' and <= '\uFF60' or // Fullwidth Forms
               >= '\uFFE0' and <= '\uFFE6' or // Fullwidth Signs
               >= '\u3000' and <= '\u303F' or // CJK Symbols
               >= '\uAC00' and <= '\uD7AF' or // Hangul Syllables
               >= '\u3040' and <= '\u30FF';   // Hiragana + Katakana
    }
}
