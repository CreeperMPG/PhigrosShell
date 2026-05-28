using PhigrosArchive.Utils;
using PhigrosShell.Utils;
using PhigrosShell.VFS;

namespace PhigrosShell.Commands.VFileSystem;

internal class RemoveCommand : CommandBase
{
    public override string Name => "Remove";
    public override string Description => "Remove a directory or reset a value. Usage: remove <path>";

    public override bool Execute(string command, List<ShellArgument> args)
    {
        if (!command.Equals("remove", StringComparison.OrdinalIgnoreCase) &&
            !command.Equals("rm", StringComparison.OrdinalIgnoreCase) &&
            !command.Equals("del", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!Shell.LoggedIn())
        {
            ConsoleUtils.WriteWarning("Required login.");
            return true;
        }

        if (args.Count < 1)
        {
            ConsoleUtils.WriteWarning("Usage: remove <path>");
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

        if (directory.IsDisallowToModify(resolvedPath))
        {
            ConsoleUtils.WriteWarning("This entry is protected and cannot be removed.");
            return true;
        }

        // Set to default value (0 / empty)
        if (directory.Set(resolvedPath, "0"))
            ConsoleUtils.WriteSuccess("Removed / reset successfully.");
        else
            ConsoleUtils.WriteError("Failed to remove.");

        return true;
    }
}
