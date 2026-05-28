namespace PhigrosShell.Commands;

internal class PauseCommand : CommandBase
{
    public override string Name => "Pause";
    public override string Description => "Pause execution until a key is pressed.";

    public override bool Execute(string command, List<ShellArgument> args)
    {
        if (!command.Equals("pause", StringComparison.OrdinalIgnoreCase))
            return false;

        FluentConsole.Yellow.Line(Program.Localization["PressAnyKeyToContinue"]);
        Console.ReadKey(intercept: true);
        Console.WriteLine();
        return true;
    }
}
