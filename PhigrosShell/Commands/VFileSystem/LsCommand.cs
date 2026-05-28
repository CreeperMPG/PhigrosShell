using PhigrosArchive.Utils;
using PhigrosShell.Utils;
using PhigrosShell.VFS;

namespace PhigrosShell.Commands.VFileSystem;

internal class LsCommand : CommandBase
{
    public override string Name => "Ls";
    public override string Description => "(a.k.a. dir) List directory contents. Usage: ls [path] [--keyword=xxx]";

    public override bool Execute(string command, List<ShellArgument> args)
    {
        if (!command.Equals("ls", StringComparison.OrdinalIgnoreCase) &&
            !command.Equals("dir", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!Shell.LoggedIn())
        {
            ConsoleUtils.WriteWarning("Required login.");
            return true;
        }

        string input = args.Where(a => a.Type != ShellArgumentType.Option)
            .FirstOrDefault()?.Value ?? Shell.Path;
        string resolved = PathUtils.ResolvePath(Shell.Path, input);

        var directory = new VDirectory(Shell.CurrentPlayerRoot!);
        if (!directory.Exists(resolved))
        {
            ConsoleUtils.WriteError("Directory not found: " + resolved);
            return true;
        }

        var entries = directory.ListEntries(resolved);
        string? keyword = ConsoleUtils.GetArgumentValue(args, "keyword")?.Trim();

        int maxNameLength = Math.Max(entries.Max(e => e.Name.Length), 8);

        foreach (var entry in entries)
        {
            string? preview = directory.GetPreview(resolved, entry);

            if (!string.IsNullOrEmpty(keyword))
            {
                bool nameMatch = entry.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase);
                bool previewMatch = !string.IsNullOrEmpty(preview) &&
                    preview.Contains(keyword, StringComparison.OrdinalIgnoreCase);
                if (!nameMatch && !previewMatch)
                    continue;
            }

            switch (entry.Type)
            {
                case VEntryType.Directory: Console.ForegroundColor = ConsoleColor.Cyan; break;
                case VEntryType.Dictionary: Console.ForegroundColor = ConsoleColor.DarkYellow; break;
                case VEntryType.Enum: Console.ForegroundColor = ConsoleColor.Yellow; break;
                case VEntryType.File: Console.ForegroundColor = ConsoleColor.White; break;
                case VEntryType.Enumerable: Console.ForegroundColor = ConsoleColor.DarkYellow; break;
                case VEntryType.Null: Console.ForegroundColor = ConsoleColor.DarkGray; break;
            }

            Console.Write(entry.Name.PadRightEx(maxNameLength + 2));

            if (entry.IsReadOnly)
                FluentConsole.DarkBlue.Text("|rX|  ");
            else
                FluentConsole.Blue.Text("|rw|  ");

            Console.ForegroundColor = ConsoleColor.Gray;
            if (!string.IsNullOrEmpty(preview))
                Console.Write(preview);

            Console.ResetColor();
            Console.WriteLine();
        }

        return true;
    }
}
