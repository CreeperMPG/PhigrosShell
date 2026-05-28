using PhigrosArchive.Utils;
using PhigrosShell.Utils;
using PhigrosShell.VFS;

namespace PhigrosShell.Commands.VFileSystem;

internal class TreeCommand : CommandBase
{
    public override string Name => "Tree";
    public override string Description => "Display directory tree structure. Usage: tree [path]";

    public override bool Execute(string command, List<ShellArgument> args)
    {
        if (!command.Equals("tree", StringComparison.OrdinalIgnoreCase))
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

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(resolved);
        Console.ResetColor();

        PrintTree(directory, resolved, "");

        return true;
    }

    private static void PrintTree(VDirectory directory, string path, string indent)
    {
        var entries = directory.ListEntries(path);
        for (int i = 0; i < entries.Count; i++)
        {
            bool isLast = i == entries.Count - 1;
            var entry = entries[i];
            string connector = isLast ? "└── " : "├── ";
            string childIndent = isLast ? "    " : "│   ";

            Console.Write(indent + connector);

            string entryPath = path.TrimEnd('/') + "/" + entry.Name;

            switch (entry.Type)
            {
                case VEntryType.Directory:
                case VEntryType.Dictionary:
                case VEntryType.Enumerable:
                    Console.ForegroundColor = entry.Type == VEntryType.Directory
                        ? ConsoleColor.Cyan : ConsoleColor.DarkYellow;
                    Console.WriteLine(entry.Name + (entry.Type == VEntryType.Directory ? "/" : ""));
                    Console.ResetColor();
                    PrintTree(directory, entryPath, indent + childIndent);
                    break;
                case VEntryType.Enum:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write(entry.Name);
                    Console.ResetColor();
                    Console.Write(" = ");
                    ShowPreview(directory, entryPath, entry.Type);
                    break;
                case VEntryType.File:
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.Write(entry.Name);
                    Console.ResetColor();
                    ShowPreview(directory, entryPath, entry.Type);
                    break;
                case VEntryType.Null:
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine(entry.Name + " = (null)");
                    Console.ResetColor();
                    break;
            }
        }
    }

    private static void ShowPreview(VDirectory directory, string path, VEntryType type)
    {
        string? preview = directory.GetPreview(path, type);
        if (!string.IsNullOrEmpty(preview))
        {
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write("  # " + preview);
            Console.ResetColor();
        }
        Console.WriteLine();
    }
}
