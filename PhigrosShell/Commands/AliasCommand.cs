using PhigrosShell.Utils;

namespace PhigrosShell.Commands;

internal class AliasCommand : CommandBase
{
    public override string Name => "Alias";
    public override string Description => "Manage command aliases. Usage: alias <name> <command>; unalias <name>";

    public override bool Execute(string command, List<ShellArgument> args)
    {
        if (!command.Equals("alias", StringComparison.OrdinalIgnoreCase) &&
            !command.Equals("unalias", StringComparison.OrdinalIgnoreCase))
            return false;

        if (command.Equals("unalias", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Count < 1)
            {
                ConsoleUtils.WriteWarning("Usage: unalias <name>");
                return true;
            }
            string aliasKey = "alias." + args[0].Value;
            if (Program.Config[aliasKey] != null)
            {
                Program.Config[aliasKey] = null;
                Program.Config.Save();
                ConsoleUtils.WriteSuccess("Alias '" + args[0].Value + "' removed.");
            }
            else
            {
                ConsoleUtils.WriteWarning("Alias not found: " + args[0].Value);
            }
            return true;
        }

        // alias
        if (args.Count < 2)
        {
            bool hasAny = false;
            foreach (var pair in Program.Config)
            {
                if (pair.Key.StartsWith("alias."))
                {
                    string aliasName = pair.Key[6..];
                    FluentConsole.Cyan.Text(aliasName.PadRight(20))
                        .Gray.Text("=> ")
                        .White.Line(pair.Value);
                    hasAny = true;
                }
            }
            if (!hasAny)
                ConsoleUtils.WriteWarning("No aliases defined.");
            return true;
        }

        string name = args[0].Value;
        string aliasCommand = string.Join(" ", args.Skip(1).Select(a => a.Value));
        Program.Config["alias." + name] = aliasCommand;
        Program.Config.Save();
        ConsoleUtils.WriteSuccess($"Alias '{name}' => {aliasCommand}");
        return true;
    }
}
