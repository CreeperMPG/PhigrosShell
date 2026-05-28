using PhigrosArchive.Utils;
using PhigrosShell.Utils;
using PhigrosShell.VFS;

namespace PhigrosShell.Commands.VFileSystem;

internal class CdCommand : CommandBase
{
    public override string Name => "Cd";
    public override string Description => "Change current directory.";

    public override bool Execute(string command, List<ShellArgument> args)
    {
        if (!command.Equals("cd", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!Shell.LoggedIn())
        {
            ConsoleUtils.WriteWarning("Required login.");
            return true;
        }

        string target = args.FirstOrDefault()?.Value ?? "/";
        string resolved = PathUtils.ResolvePath(Shell.Path, target);

        var directory = new VDirectory(Shell.CurrentPlayerRoot!);
        if (!directory.Exists(resolved))
        {
            ConsoleUtils.WriteError("Directory not found: " + resolved);
            return true;
        }

        Shell.Path = resolved;
        return true;
    }
}
