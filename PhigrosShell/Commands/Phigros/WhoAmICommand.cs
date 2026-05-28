using PhigrosShell.Utils;

namespace PhigrosShell.Commands.Phigros;

internal class WhoAmICommand : CommandBase
{
    public override string Name => "WhoAmI";
    public override string Description => "Show current user info.";

    public override bool Execute(string command, List<ShellArgument> args)
    {
        if (!command.Equals("whoami", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!Shell.LoggedIn())
        {
            ConsoleUtils.WriteWarning("Not logged in.");
            return true;
        }

        var playerInfo = Shell.CurrentSession?.PlayerInfo;
        if (playerInfo == null)
        {
            ConsoleUtils.WriteWarning("Player info not available.");
            return true;
        }

        FluentConsole.DarkCyan.Text("Nickname     : ").White.Line(playerInfo.Nickname)
            .DarkCyan.Text("Short ID     : ").White.Line(playerInfo.ShortID)
            .DarkCyan.Text("Object ID    : ").White.Line(playerInfo.UserObjectID)
            .DarkCyan.Text("Create Time  : ").White.Line(playerInfo.CreateTime)
            .DarkCyan.Text("Session Token: ").White.Line(playerInfo.SessionToken)
            .DarkCyan.Text("Save Files   : ").White.Line(Shell.CurrentSession?.SaveFiles.Count.ToString() ?? "0");

        return true;
    }
}
