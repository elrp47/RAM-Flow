namespace RAMFlow;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        bool trayOnly = args.Contains("--tray", StringComparer.OrdinalIgnoreCase);

        // «--clean» — очистить и выйти, удобно для ярлыка или своего планировщика.
        if (args.Contains("--clean", StringComparer.OrdinalIgnoreCase))
        {
            var s = Settings.Load();
            var r = Cleaner.Clean(s.Regions, s.Exclusions);
            if (r.FreedBytes > 0) s.TotalFreedBytes += (ulong)r.FreedBytes;
            s.TotalCleanups++;
            s.Save();
            return;
        }

        using var mutex = new Mutex(true, @"Local\RAMFlow.SingleInstance", out bool first);
        if (!first) return;

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApp(trayOnly));
    }
}
