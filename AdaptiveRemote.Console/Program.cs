namespace AdaptiveRemote;

internal class Program : App
{
    public Program(string[] args)
        : base(args)
    {
    }

    [STAThread]
    private static void Main(string[] args)
    {
        new Program(args).Run();
    }
}
