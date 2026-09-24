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

        switch (directory.Set(resolvedPath, newValue))
        {
            case WriteResult.Success:
                ConsoleUtils.WriteSuccess(Program.Localization["VfsModified"]);
                break;

            case WriteResult.NotFound:
                ConsoleUtils.WriteError(Program.Localization["VfsPathNotFound", new object[] { resolvedPath }]);
                break;

            case WriteResult.Protected:
                ConsoleUtils.WriteWarning(Program.Localization["VfsProtected"]);
                break;

            case WriteResult.ReadOnlyProperty:
                ConsoleUtils.WriteWarning(Program.Localization["VfsReadOnly"]);
                break;

            case WriteResult.BadValue:
                ConsoleUtils.WriteError(Program.Localization["VfsBadValue"]);
                break;

            default:
                ConsoleUtils.WriteError(Program.Localization["VfsModifyFailed"]);
                break;
        }

        return true;
    }
}
