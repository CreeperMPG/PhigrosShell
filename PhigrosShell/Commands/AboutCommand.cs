namespace PhigrosShell.Commands;

internal class AboutCommand : CommandBase
{
    public override string Name => "About";
    public override string Description => "Display application info.";

    public override bool Execute(string command, List<ShellArgument> args)
    {
        if (!command.Equals("about", StringComparison.OrdinalIgnoreCase))
            return false;

        FluentConsole.Blue.Line(Program.Localization["AppTitle", new object[] { Program.AppName, Program.Version }]);
        if (Program.IsBeta)
            FluentConsole.Yellow.Line(Program.Localization["BetaVersionPrompt"]);

        FluentConsole.White.Line($"\n{Program.AppName} {Program.Version}");
        FluentConsole.DarkCyan.Line($"\n{Program.Localization["UpdateLogHeader"]}");

        foreach (var version in Program.UpdateLog)
        {
            FluentConsole.Cyan.Line($"\n{version.Key}");
            foreach (var line in version.Value)
            {
                FluentConsole.Gray.Text("  • ").White.Line(line);
            }
        }

        return true;
    }
}
