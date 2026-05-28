using PhigrosShell.Utils;

namespace PhigrosShell.Commands.Phigros;

internal class LogoutCommand : CommandBase
{
    public override string Name => "Logout";
    public override string Description => "Logout current Phigros account.";

    public override bool Execute(string command, List<ShellArgument> args)
    {
        if (!command.Equals("logout", StringComparison.OrdinalIgnoreCase))
            return false;

        Shell.Logout();
        ConsoleUtils.WriteSuccess("Logged out.");
        return true;
    }
}
