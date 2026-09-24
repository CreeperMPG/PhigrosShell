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

        // 版本顺序由 changelog 文件里的数组顺序决定（约定从新到旧）
        foreach (var entry in Program.Changelog.Entries)
        {
            FluentConsole.Cyan.Line($"\n{entry.Version}  ")
                .DarkGray.Text(entry.Date).NewLine();

            foreach (var line in entry.Changes)
            {
                FluentConsole.Gray.Text("  • ").White.Line(line);
            }
        }

        return true;
    }
}
