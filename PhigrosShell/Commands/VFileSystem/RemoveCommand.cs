using PhigrosShell.Utils;
using PhigrosShell.VFS;

namespace PhigrosShell.Commands.VFileSystem;

internal class RemoveCommand : CommandBase
{
    public override string Name => "Remove";
    public override string Description => "Remove an entry (dictionary key / list item) or clear a value. Usage: remove <path>";

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

        // 字典/列表 → 真删除；普通属性 → 置 null（受类型白名单限制）
        switch (directory.Remove(resolvedPath))
        {
            case WriteResult.Success:
                ConsoleUtils.WriteSuccess(Program.Localization["VfsRemoved"]);
                break;

            case WriteResult.NotFound:
                ConsoleUtils.WriteError(Program.Localization["VfsPathNotFound", new object[] { resolvedPath }]);
                break;

            case WriteResult.NotAllowed:
                ConsoleUtils.WriteWarning(Program.Localization["VfsRemoveNotAllowed"]);
                break;

            case WriteResult.Protected:
                ConsoleUtils.WriteWarning(Program.Localization["VfsProtected"]);
                break;

            case WriteResult.ReadOnlyProperty:
                ConsoleUtils.WriteWarning(Program.Localization["VfsReadOnly"]);
                break;

            default:
                ConsoleUtils.WriteError(Program.Localization["VfsRemoveFailed"]);
                break;
        }

        return true;
    }
}
