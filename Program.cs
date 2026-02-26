namespace ClaudeUsageMonitor;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        using var mutex = new Mutex(true, "ClaudeUsageMonitor_v3", out bool isNew);
        if (!isNew) { MessageBox.Show("Already running.", "Claude Usage Monitor"); return; }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

        Application.ThreadException += (_, e) => System.Diagnostics.Debug.WriteLine($"UI: {e.Exception}");
        AppDomain.CurrentDomain.UnhandledException += (_, e) => System.Diagnostics.Debug.WriteLine($"Fatal: {e.ExceptionObject}");

        Application.Run(new MainForm());
    }
}
