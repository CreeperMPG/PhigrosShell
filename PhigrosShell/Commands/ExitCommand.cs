namespace PhigrosShell.Commands;

internal class ExitCommand : CommandBase
{
    public override string Name => "Exit";
    public override string Description => "Exit the shell.";

    public override bool Execute(string command, List<ShellArgument> args)
    {
        if (!command.Equals("exit", StringComparison.OrdinalIgnoreCase) &&
            !command.Equals("quit", StringComparison.OrdinalIgnoreCase))
            return false;

        Environment.Exit(0);
        return true;
    }
}
