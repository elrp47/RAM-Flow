using System.Diagnostics;

namespace RAMFlow;

public sealed record CleanResult(long FreedBytes, MemorySnapshot After, TimeSpan Duration, List<string> Errors);

public static class Cleaner
{
    public static readonly (Regions Region, string Name, string Hint)[] All =
    {
        (Regions.WorkingSets, "Рабочие наборы процессов",
            "Выгружает неиспользуемые страницы программ. Безопасно, основной источник освобождённой памяти."),
        (Regions.SystemFileCache, "Системный файловый кэш",
            "Сбрасывает рабочий набор файлового кэша."),
        (Regions.ModifiedList, "Изменённые страницы",
            "Записывает изменённые страницы на диск и переводит их в резерв."),
        (Regions.StandbyLowPriority, "Резервный список (низкий приоритет)",
            "Удаляет из кэша давно не используемые данные. Почти не влияет на скорость."),
        (Regions.StandbyList, "Резервный список (полностью)",
            "Освобождает больше всего, но программы и игры будут заново читать данные с диска. Может вызвать подлагивания."),
        (Regions.RegistryCache, "Кэш реестра",
            "Сбрасывает накопленные изменения реестра на диск (Windows 8.1+)."),
        (Regions.CombineMemory, "Объединение одинаковых страниц",
            "Склеивает одинаковые страницы памяти в одну (Windows 10+)."),
        (Regions.VolumeBuffers, "Буферы дисков",
            "Принудительно дописывает кэш записи на все локальные диски."),
    };

    private static bool _privilegesReady;

    private static void EnsurePrivileges()
    {
        if (_privilegesReady) return;
        Native.EnablePrivilege("SeProfileSingleProcessPrivilege");
        Native.EnablePrivilege("SeIncreaseQuotaPrivilege");
        _privilegesReady = true;
    }

    public static CleanResult Clean(Regions regions, IReadOnlyCollection<string> exclusions)
    {
        EnsurePrivileges();
        var sw = Stopwatch.StartNew();
        var before = Memory.Read();
        var errors = new List<string>();

        void Check(int status, string what)
        {
            if (status != 0) errors.Add($"{what}: 0x{status:X8}");
        }

        // Порядок как в Mem Reduct: сначала выталкиваем страницы из процессов
        // и кэша, потом пишем изменённые на диск, и лишь затем чистим резерв,
        // куда они все и попали.
        if (regions.HasFlag(Regions.WorkingSets))
        {
            if (exclusions.Count == 0)
                Check(Native.SetSystemInfo(Native.SystemMemoryListInformation, Native.MemoryEmptyWorkingSets), "Рабочие наборы");
            else
                TrimProcesses(exclusions);
        }

        if (regions.HasFlag(Regions.SystemFileCache) &&
            !Native.SetSystemFileCacheSize(new IntPtr(-1), new IntPtr(-1), 0))
            errors.Add($"Файловый кэш: {new System.ComponentModel.Win32Exception().Message}");

        if (regions.HasFlag(Regions.VolumeBuffers))
            FlushVolumes();

        if (regions.HasFlag(Regions.ModifiedList))
            Check(Native.SetSystemInfo(Native.SystemMemoryListInformation, Native.MemoryFlushModifiedList), "Изменённые страницы");

        if (regions.HasFlag(Regions.StandbyList))
            Check(Native.SetSystemInfo(Native.SystemMemoryListInformation, Native.MemoryPurgeStandbyList), "Резервный список");
        else if (regions.HasFlag(Regions.StandbyLowPriority))
            Check(Native.SetSystemInfo(Native.SystemMemoryListInformation, Native.MemoryPurgeLowPriorityStandbyList), "Резерв (низкий приоритет)");

        if (regions.HasFlag(Regions.RegistryCache) && OperatingSystem.IsWindowsVersionAtLeast(6, 3))
            Check(Native.NtSetSystemInformation(Native.SystemRegistryReconciliationInformation, IntPtr.Zero, 0), "Кэш реестра");

        if (regions.HasFlag(Regions.CombineMemory) && OperatingSystem.IsWindowsVersionAtLeast(10))
            Check(Native.CombineMemory(out _), "Объединение страниц");

        var after = Memory.Read();
        return new CleanResult((long)after.AvailPhys - (long)before.AvailPhys, after, sw.Elapsed, errors);
    }

    private static void TrimProcesses(IReadOnlyCollection<string> exclusions)
    {
        var skip = new HashSet<string>(exclusions, StringComparer.OrdinalIgnoreCase);
        foreach (var p in Process.GetProcesses())
        {
            using (p)
            {
                if (p.Id <= 4 || skip.Contains(p.ProcessName)) continue;
                IntPtr h = Native.OpenProcess(Native.PROCESS_QUERY_LIMITED_INFORMATION | Native.PROCESS_SET_QUOTA, false, p.Id);
                if (h == IntPtr.Zero) continue;
                Native.EmptyWorkingSet(h);
                Native.CloseHandle(h);
            }
        }
    }

    private static void FlushVolumes()
    {
        foreach (var d in DriveInfo.GetDrives())
        {
            if (d.DriveType != DriveType.Fixed && d.DriveType != DriveType.Removable) continue;
            IntPtr h = Native.CreateFile(@"\\.\" + d.Name.TrimEnd('\\'), Native.GENERIC_READ | Native.GENERIC_WRITE,
                Native.FILE_SHARE_READ | Native.FILE_SHARE_WRITE, IntPtr.Zero, Native.OPEN_EXISTING, 0, IntPtr.Zero);
            if (h == new IntPtr(-1)) continue;
            Native.FlushFileBuffers(h);
            Native.CloseHandle(h);
        }
    }
}
