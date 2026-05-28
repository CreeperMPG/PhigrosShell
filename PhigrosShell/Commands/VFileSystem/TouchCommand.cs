using PhigrosArchive.Utils;
using PhigrosShell.Utils;
using PhigrosShell.VFS;

namespace PhigrosShell.Commands.VFileSystem;

internal class TouchCommand : CommandBase
{
    public override string Name => "Touch";
    public override string Description => "Create or update an entry at a path. Usage: touch <path> [value]";

    public override bool Execute(string command, List<ShellArgument> args)
    {
        if (!command.Equals("touch", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!Shell.LoggedIn())
        {
            ConsoleUtils.WriteWarning("Required login.");
            return true;
        }

        if (args.Count < 1)
        {
            ConsoleUtils.WriteWarning("Usage: touch <path> [value]");
            return true;
        }

        string inputPath = args[0].Value;
        string resolvedPath = PathUtils.ResolvePath(Shell.Path, inputPath);
        string newValue = args.Count > 1
            ? string.Join(" ", args.Skip(1).Select(a => a.Value))
            : "";

        var directory = new VDirectory(Shell.CurrentPlayerRoot!);

        if (directory.Set(resolvedPath, newValue))
            ConsoleUtils.WriteSuccess("Updated successfully.");
        else
            ConsoleUtils.WriteWarning("Cannot modify this entry.");

        return true;
    }
}
