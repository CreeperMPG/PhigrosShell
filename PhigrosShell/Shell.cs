using System.Text.RegularExpressions;
using System.Text;
using PhigrosArchive.Utils;
using PhigrosShell.Mapping;
using PhigrosShell.Utils;

namespace PhigrosShell;

internal static class Shell
{
    /// <summary>当前登录用户的标识（ShortID）</summary>
    public static string User = "";

    /// <summary>当前 VDirectory 路径</summary>
    public static string Path = "/";

    /// <summary>当前 Shell 会话（登录后非 null）</summary>
    public static ShellSession? CurrentSession;

    /// <summary>VDirectory 根映射对象（登录后非 null）</summary>
    public static ShellPlayerRoot? CurrentPlayerRoot;

    private static readonly List<string> _inputHistory = new();
    private static readonly CommandManager _commandManager = new();

    public static List<CommandBase> Commands => _commandManager.GetCommands();

    public static bool LoggedIn() => CurrentSession?.PlayerInfo != null;

    public static void LoginSession(ShellSession session)
    {
        CurrentSession = session;
        User = session.PlayerInfo?.ShortID ?? "";

        // Build VDirectory root mappings
        var root = new ShellPlayerRoot { PlayerInfo = session.PlayerInfo };
        foreach (var slot in session.SaveFiles)
        {
            root.SaveFiles.Add(new ShellSlotRoot
            {
                Slot = slot  // 引用 ShellSaveSlot，属性实时委托
            });
        }
        CurrentPlayerRoot = root;
        Path = "/";
    }

    public static void Logout()
    {
        CurrentSession = null;
        CurrentPlayerRoot = null;
        User = "";
        Path = "/";
    }



    public static void RegisterCommand(CommandBase command)
    {
        _commandManager.Register(command);
    }

    public static void InitShell(List<string>? presetCommands = null)
    {
        ConsoleUtils.ClearLine();
        FluentConsole.NewLine();

        if (presetCommands == null)
        {
            while (true)
                ShellLoop();
        }
        else
        {
            foreach (var presetCommand in presetCommands)
                ShellLoop(presetCommand);
        }
    }

    private static void ShowPrompt()
    {
        FluentConsole.Green.Text(User + "@PhiShell")
            .White.Text(": ")
            .Cyan.Text(Path)
            .White.Text(" $ ");
    }

    private static void ShellLoop(string? preset = null)
    {
        ShowPrompt();
        var inputBuilder = new StringBuilder();
        int historyIndex = _inputHistory.Count;
        string inputText = "";
        int cursorPos = 0;
        int startCursorLeft = Console.CursorLeft;
        int startCursorTop = Console.CursorTop;
        int promptTopLine = startCursorTop;

        while (true)
        {
            if (preset != null)
            {
                inputText = preset.Trim();
                Console.WriteLine(preset);
                break;
            }

            var keyInfo = Console.ReadKey(intercept: true);

            if (keyInfo.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                inputText = inputBuilder.ToString().Trim();
                break;
            }

            if (keyInfo.Key == ConsoleKey.Backspace)
            {
                if (cursorPos > 0)
                {
                    inputBuilder.Remove(cursorPos - 1, 1);
                    cursorPos--;
                }
            }
            else if (keyInfo.Key == ConsoleKey.Delete)
            {
                if (cursorPos < inputBuilder.Length)
                    inputBuilder.Remove(cursorPos, 1);
            }
            else if (keyInfo.Key == ConsoleKey.LeftArrow)
            {
                if (cursorPos > 0) cursorPos--;
            }
            else if (keyInfo.Key == ConsoleKey.RightArrow)
            {
                if (cursorPos < inputBuilder.Length) cursorPos++;
            }
            else if (keyInfo.Key == ConsoleKey.UpArrow)
            {
                if (_inputHistory.Count > 0 && historyIndex > 0)
                {
                    historyIndex--;
                    inputBuilder.Clear();
                    inputBuilder.Append(_inputHistory[historyIndex]);
                    cursorPos = inputBuilder.Length;
                }
            }
            else if (keyInfo.Key == ConsoleKey.DownArrow)
            {
                if (_inputHistory.Count > 0 && historyIndex < _inputHistory.Count - 1)
                {
                    historyIndex++;
                    inputBuilder.Clear();
                    inputBuilder.Append(_inputHistory[historyIndex]);
                    cursorPos = inputBuilder.Length;
                }
                else if (historyIndex == _inputHistory.Count - 1)
                {
                    historyIndex++;
                    inputBuilder.Clear();
                    cursorPos = 0;
                }
            }
            else if (!char.IsControl(keyInfo.KeyChar))
            {
                inputBuilder.Insert(cursorPos, keyInfo.KeyChar);
                cursorPos++;
            }

            // Redraw
            var widths = new int[inputBuilder.Length];
            for (int i = 0; i < inputBuilder.Length; i++)
                widths[i] = ConsoleUtils.IsFullWidthChar(inputBuilder[i]) ? 2 : 1;

            int windowWidth = Console.WindowWidth;
            int totalWidth = startCursorLeft;
            foreach (var w in widths) totalWidth += w;

            int newTopLine = totalWidth / windowWidth + 1;
            if (newTopLine + startCursorTop > Console.BufferHeight)
            {
                int diff = promptTopLine;
                promptTopLine = Math.Max(0, Console.BufferHeight - newTopLine);
                diff -= promptTopLine;
                for (int k = 0; k < diff; k++)
                    FluentConsole.NewLine();
                Console.SetCursorPosition(0, promptTopLine);
                ShowPrompt();
            }

            // Clear line
            for (int line = 0; line < newTopLine; line++)
            {
                int lineY = promptTopLine + line;
                if (lineY >= Console.BufferHeight) break;
                Console.SetCursorPosition(line == 0 ? startCursorLeft : 0, lineY);
                Console.Write(new string(' ', windowWidth - (line == 0 ? startCursorLeft : 0)));
            }

            Console.SetCursorPosition(startCursorLeft, promptTopLine);
            Console.Write(inputBuilder.ToString());
            if (totalWidth % windowWidth == 0)
                FluentConsole.NewLine();

            // Position cursor
            int cursorLine = promptTopLine;
            int cursorCol = startCursorLeft;
            for (int m = 0; m < cursorPos; m++)
            {
                if (cursorCol >= windowWidth)
                {
                    cursorCol = 0;
                    cursorLine++;
                    if (cursorLine >= Console.BufferHeight)
                        cursorLine = Console.BufferHeight - 1;
                }
                cursorCol += widths[m];
            }

            int finalCol = cursorCol % windowWidth;
            int finalLine = cursorLine;
            if (finalCol == 0) finalLine++;
            finalLine = Math.Min(finalLine, Console.BufferHeight - 1);
            finalCol = Math.Min(finalCol, windowWidth - 1);
            Console.SetCursorPosition(finalCol, finalLine);
        }

        if (string.IsNullOrEmpty(inputText)) return;

        _inputHistory.Add(inputText);
        var parsedArgs = ParseArguments(inputText);

        // Debug argument parser
        if (parsedArgs.Any(a => a.Value == "--activate-builtin-argument-parser"))
        {
            FluentConsole.Cyan.Line("\n┌─ Command Argument Parser");
            foreach (var arg in parsedArgs)
                FluentConsole.Cyan.Text("│ ").Text($"[Name] {arg}     [Type] {arg.Type}\n");
            FluentConsole.Cyan.Line("└──────────────────────────\n");
        }

        string cmdName = parsedArgs.FirstOrDefault()?.Value ?? "";
        if (parsedArgs.Count > 0)
            parsedArgs.RemoveAt(0);

        if (!string.IsNullOrEmpty(cmdName) && !_commandManager.Execute(cmdName, parsedArgs))
            ConsoleUtils.WriteError("Command not found: " + cmdName);
    }

    internal static List<ShellArgument> ParseArguments(string input)
    {
        var result = new List<ShellArgument>();
        var pattern = "'([^']*)'|\"([^\"]*)\"|(\\S+)";

        foreach (Match match in Regex.Matches(input, pattern))
        {
            if (!match.Success) continue;

            string value = match.Groups[1].Success
                ? match.Groups[1].Value
                : match.Groups[2].Success
                    ? match.Groups[2].Value
                    : match.Groups[3].Value;

            var type = ShellArgumentType.Argument;

            if (match.Groups[1].Success || match.Groups[2].Success)
                type = ShellArgumentType.String;
            else if (match.Groups[3].Success && match.Groups[3].Value.StartsWith("-"))
                type = ShellArgumentType.Option;
            else if (match.Groups[3].Success && match.Groups[3].Value.StartsWith("#"))
                break;

            if (result.Count == 0)
                type = ShellArgumentType.Command;

            result.Add(new ShellArgument(value, type));
        }

        return result;
    }
}
