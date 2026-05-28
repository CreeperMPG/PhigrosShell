using PhigrosArchive.Utils;
using PhigrosShell.Utils;
using PhigrosShell.VFS;

namespace PhigrosShell.Commands.VFileSystem;

internal class PrintCommand : CommandBase
{
    public override string Name => "Print";
    public override string Description => "Print the value of a path. Usage: print <path>";

    public override bool Execute(string command, List<ShellArgument> args)
    {
        if (!command.Equals("print", StringComparison.OrdinalIgnoreCase) &&
            !command.Equals("cat", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!Shell.LoggedIn())
        {
            ConsoleUtils.WriteWarning("Required login.");
            return true;
        }

        if (args.Count < 1)
        {
            ConsoleUtils.WriteWarning("Usage: print <path>");
            return true;
        }

        string inputPath = args[0].Value;
        string resolvedPath = PathUtils.ResolvePath(Shell.Path, inputPath);

        var directory = new VDirectory(Shell.CurrentPlayerRoot!);
        if (!directory.Exists(resolvedPath))
        {
            ConsoleUtils.WriteError("Path not found: " + resolvedPath);
            return true;
        }

        object? value = directory.Get(resolvedPath);
        if (value == null)
        {
            FluentConsole.DarkGray.Line("(null)");
        }
        else
        {
            string? preview = directory.GetPreview(resolvedPath,
                directory.ListEntries(System.IO.Path.GetDirectoryName(resolvedPath)?.Replace("\\", "/") ?? "/")
                    .FirstOrDefault(e => resolvedPath.EndsWith(e.Name, StringComparison.OrdinalIgnoreCase))?.Type ?? VEntryType.Directory);

            if (!string.IsNullOrEmpty(preview))
                Console.WriteLine(preview);
            else
                Console.WriteLine(value.ToString());
        }

        return true;
    }
}
