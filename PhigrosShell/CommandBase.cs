namespace PhigrosShell;

internal abstract class CommandBase
{
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract bool Execute(string command, List<ShellArgument> args);
}
