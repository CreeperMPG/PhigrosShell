namespace PhigrosShell.Commands;
using PhigrosShell.Utils;

internal class HelpCommand : CommandBase
{
    public override string Name => "Help";
    public override string Description => "Show help for all commands.";

    public override bool Execute(string command, List<ShellArgument> args)
    {
        if (!command.Equals("help", StringComparison.OrdinalIgnoreCase))
            return false;

        if (args.Count > 0)
        {
            string topic = args[0].Value;
            foreach (var cmd in Shell.Commands)
            {
                if (cmd.Name.Equals(topic, StringComparison.OrdinalIgnoreCase))
                {
                    FluentConsole.Cyan.Text(cmd.Name.PadRight(15))
                        .White.Line(cmd.Description);
                    return true;
                }
            }
            ConsoleUtils.WriteWarning("No help for: " + topic);
            return true;
        }

        FluentConsole.Cyan.Line(Program.Localization["HelpAvailableCommands"]);
        FluentConsole.Gray.Line(new string('─', 60));

        foreach (var cmd in Shell.Commands)
        {
            FluentConsole.Cyan.Text(cmd.Name.PadRight(15))
                .White.Line(cmd.Description);
        }

        return true;
    }
}
