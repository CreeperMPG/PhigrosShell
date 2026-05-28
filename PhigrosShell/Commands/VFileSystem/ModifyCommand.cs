using PhigrosArchive.Utils;
using PhigrosShell.Utils;
using PhigrosShell.VFS;

namespace PhigrosShell.Commands.VFileSystem;

internal class ModifyCommand : CommandBase
{
    public override string Name => "Modify";
    public override string Description => "Modify a value at a path. Usage: modify <path> <value>";

    public override bool Execute(string command, List<ShellArgument> args)
    {
        if (!command.Equals("modify", StringComparison.OrdinalIgnoreCase) &&
            !command.Equals("mod", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!Shell.LoggedIn())
        {
            ConsoleUtils.WriteWarning("Required login.");
            return true;
        }

        if (args.Count < 2)
        {
            ConsoleUtils.WriteWarning("Usage: modify <path> <value>");
            return true;
        }

        string inputPath = args[0].Value;
        string resolvedPath = PathUtils.ResolvePath(Shell.Path, inputPath);
        string newValue = string.Join(" ", args.Skip(1).Select(a => a.Value));

        var directory = new VDirectory(Shell.CurrentPlayerRoot!);
        if (!directory.Exists(resolvedPath))
        {
            ConsoleUtils.WriteError("Path not found: " + resolvedPath);
            return true;
        }

        if (directory.IsReadOnly(resolvedPath))
        {
            ConsoleUtils.WriteWarning("This entry is read-only.");
            return true;
        }

        if (directory.Set(resolvedPath, newValue))
            ConsoleUtils.WriteSuccess("Modified successfully.");
        else
            ConsoleUtils.WriteError("Failed to modify.");

        return true;
    }
}
