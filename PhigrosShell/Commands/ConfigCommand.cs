using PhigrosShell.Utils;

namespace PhigrosShell.Commands;

internal class ConfigCommand : CommandBase
{
    public override string Name => "Config";
    public override string Description => "Manage configuration. Usage: config <key> [value]; config list";

    public override bool Execute(string command, List<ShellArgument> args)
    {
        if (!command.Equals("config", StringComparison.OrdinalIgnoreCase))
            return false;

        if (args.Count == 0)
        {
            ConsoleUtils.WriteWarning("Usage: config <key> [value]; config list");
            return true;
        }

        if (args[0].Value.Equals("list", StringComparison.OrdinalIgnoreCase))
        {
            FluentConsole.Cyan.Line("Current Configuration:");
            foreach (var pair in Program.Config)
            {
                FluentConsole.Gray.Text("  ")
                    .Cyan.Text(pair.Key.PadRight(30))
                    .White.Line(pair.Value);
            }
            return true;
        }

        string key = args[0].Value;
        if (args.Count == 1)
        {
            string? value = Program.Config[key];
            if (value != null)
                FluentConsole.Cyan.Text(key).Gray.Text(" = ").White.Line(value);
            else
                ConsoleUtils.WriteWarning($"Key '{key}' not found.");
            return true;
        }

        string newValue = string.Join(" ", args.Skip(1).Select(a => a.Value));
        Program.Config[key] = newValue;
        Program.Config.Save();
        ConsoleUtils.WriteSuccess($"Set {key} = {newValue}");
        return true;
    }
}
