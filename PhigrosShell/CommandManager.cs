namespace PhigrosShell;

internal class CommandManager
{
    private readonly List<CommandBase> _commands = new();

    public void Register(CommandBase command) => _commands.Add(command);

    public List<CommandBase> GetCommands() => _commands;

    public bool Execute(string commandName, List<ShellArgument> args)
    {
        foreach (var command in _commands)
        {
            if (command.Execute(commandName, args))
                return true;
        }

        // Check aliases
        try
        {
            string aliasKey = "alias." + commandName;
            string? aliasValue = Program.Config[aliasKey];
            if (!string.IsNullOrEmpty(aliasValue))
            {
                var aliasArgs = Shell.ParseArguments(aliasValue);
                string aliasedCommand = aliasArgs.FirstOrDefault()?.Value ?? "";
                if (aliasArgs.Count > 0) aliasArgs.RemoveAt(0);
                return Execute(aliasedCommand, aliasArgs);
            }
        }
        catch { }

        return false;
    }
}
