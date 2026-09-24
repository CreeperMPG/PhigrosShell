using PhigrosShell.Utils;
using PhigrosShell.VFS;

namespace PhigrosShell.Commands.VFileSystem;

internal class TouchCommand : CommandBase
{
    public override string Name => "Touch";
    public override string Description => "Create an entry at a path. Usage: touch <path> [value]";

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
        string? newValue = args.Count > 1
            ? string.Join(" ", args.Skip(1).Select(a => a.Value))
            : null;

        var directory = new VDirectory(Shell.CurrentPlayerRoot!);

        // touch 只负责"让它存在"：
        //   字典/列表语境 → 新建一个值
        //   普通属性     → 把**值为 null** 的属性实例化
        //   已有值时     → 报"已有值"，改用 modify（判的是值，不是"属性存在与否"）
        switch (directory.Create(resolvedPath, newValue))
        {
            case WriteResult.Success:
                ConsoleUtils.WriteSuccess(Program.Localization["VfsCreated"]);
                break;

            case WriteResult.AlreadyExists:
                ConsoleUtils.WriteWarning(Program.Localization["VfsAlreadyHasValue"]);
                break;

            case WriteResult.NotAllowed:
                ConsoleUtils.WriteWarning(Program.Localization["VfsCreateNotAllowed"]);
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
                ConsoleUtils.WriteError(Program.Localization["VfsCreateFailed"]);
                break;
        }

        return true;
    }
}
