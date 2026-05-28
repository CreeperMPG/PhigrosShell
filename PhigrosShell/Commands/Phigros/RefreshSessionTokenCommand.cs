using PhigrosShell.Utils;

namespace PhigrosShell.Commands.Phigros;

internal class RefreshSessionTokenCommand : CommandBase
{
    public override string Name => "RefreshSessionToken";
    public override string Description => "Refresh the current session token.";

    public override bool Execute(string command, List<ShellArgument> args)
    {
        if (!command.Equals("refresh", StringComparison.OrdinalIgnoreCase) &&
            !command.Equals("refreshtoken", StringComparison.OrdinalIgnoreCase) &&
            !command.Equals("refresh-session-token", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!Shell.LoggedIn())
        {
            ConsoleUtils.WriteWarning("Required login.");
            return true;
        }

        FluentConsole.Cyan.Line("Refreshing session token...");
        var success = Shell.CurrentSession?.RefreshTokenAsync().GetAwaiter().GetResult();

        if (success == true)
        {
            ConsoleUtils.WriteSuccess("Session token refreshed.");
            FluentConsole.DarkCyan.Text("New Token: ").White.Line(Shell.CurrentSession?.PlayerInfo?.SessionToken ?? "?");
        }
        else
        {
            ConsoleUtils.WriteError("Failed to refresh session token.");
        }

        return true;
    }
}
