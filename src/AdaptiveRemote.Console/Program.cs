using AdaptiveRemote.Services.Lifecycle;

namespace AdaptiveRemote;

internal class Program : App
{
    private readonly string[] _args;

    public Program(string[] args)
    {
        _args = args;
    }

    [STAThread]
    private static void Main(string[] args)
    {
        new Program(args).Run();
    }

    protected override WpfAppHostRunner CreateAppHostRunner(string[] args) 
        => base.CreateAppHostRunner(_args);
}
