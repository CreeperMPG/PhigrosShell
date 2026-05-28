using PhigrosShell.Utils;
using PhigrosShell.VFS;

namespace PhigrosShell.Commands.VFileSystem;

internal class ClearAllCommand : CommandBase
{
    public override string Name => "ClearAll";
    public override string Description => "Clear directory contents (set all to defaults).";

    public override bool Execute(string command, List<ShellArgument> args)
    {
        if (!command.Equals("clear-all", StringComparison.OrdinalIgnoreCase) &&
            !command.Equals("clearall", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!Shell.LoggedIn())
        {
            ConsoleUtils.WriteWarning("Required login.");
            return true;
        }

        ConsoleUtils.WriteWarning("This action cannot be undone. Are you sure? (y/N)");
        var key = Console.ReadKey(intercept: true);
        Console.WriteLine();
        if (key.Key != ConsoleKey.Y)
        {
            ConsoleUtils.WriteWarning("Operation cancelled.");
            return true;
        }

        // Reset to root
        Shell.Path = "/";
        ConsoleUtils.WriteSuccess("Directory cleared.");
        return true;
    }
}
