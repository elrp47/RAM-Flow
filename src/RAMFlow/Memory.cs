namespace RAMFlow;

public readonly record struct MemorySnapshot(
    ulong TotalPhys, ulong AvailPhys, ulong CommitUsed, ulong CommitLimit, ulong SystemCache)
{
    public ulong UsedPhys => TotalPhys - AvailPhys;
    public int LoadPercent => TotalPhys == 0 ? 0 : (int)Math.Round(UsedPhys * 100.0 / TotalPhys);
    public int CommitPercent => CommitLimit == 0 ? 0 : (int)Math.Round(CommitUsed * 100.0 / CommitLimit);
}

public static class Memory
{
    public static MemorySnapshot Read()
    {
        var ms = new Native.MEMORYSTATUSEX { dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Native.MEMORYSTATUSEX>() };
        Native.GlobalMemoryStatusEx(ref ms);

        ulong commit = ms.ullTotalPageFile - ms.ullAvailPageFile, limit = ms.ullTotalPageFile, cache = 0;
        if (Native.GetPerformanceInfo(out var pi, (uint)System.Runtime.InteropServices.Marshal.SizeOf<Native.PERFORMANCE_INFORMATION>()))
        {
            ulong page = pi.PageSize.ToUInt64();
            commit = pi.CommitTotal.ToUInt64() * page;
            limit = pi.CommitLimit.ToUInt64() * page;
            cache = pi.SystemCache.ToUInt64() * page;
        }
        return new MemorySnapshot(ms.ullTotalPhys, ms.ullAvailPhys, commit, limit, cache);
    }

    public static string Format(ulong bytes)
    {
        double v = bytes;
        string[] units = { "Б", "КБ", "МБ", "ГБ", "ТБ" };
        int i = 0;
        while (v >= 1024 && i < units.Length - 1) { v /= 1024; i++; }
        return i < 2 ? $"{v:0} {units[i]}" : $"{v:0.0} {units[i]}";
    }

    public static string Format(long bytes) =>
        bytes < 0 ? "-" + Format((ulong)(-bytes)) : Format((ulong)bytes);
}
