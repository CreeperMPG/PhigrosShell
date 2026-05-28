namespace PhigrosShell.Commands;

internal class ClearCommand : CommandBase
{
    public override string Name => "Clear";
    public override string Description => "Clear the console screen.";

    public override bool Execute(string command, List<ShellArgument> args)
    {
        if (!command.Equals("clear", StringComparison.OrdinalIgnoreCase) &&
            !command.Equals("cls", StringComparison.OrdinalIgnoreCase))
            return false;

        Console.Clear();
        return true;
    }
}
