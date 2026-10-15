namespace Duskbar;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(true, "Duskbar_SingleInstance", out bool created);
        if (!created)
            return;

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApp());
    }
}
