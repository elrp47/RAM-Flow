using System.Text.Json;

namespace RAMFlow;

[Flags]
public enum Regions
{
    None = 0,
    WorkingSets = 1,
    SystemFileCache = 2,
    ModifiedList = 4,
    StandbyList = 8,
    StandbyLowPriority = 16,
    RegistryCache = 32,
    CombineMemory = 64,
    VolumeBuffers = 128,
}

public sealed class Settings
{
    // По умолчанию — безопасный набор: полный сброс standby-списка оставлен
    // выключенным, потому что после него игры и браузер заново читают с диска.
    public Regions Regions { get; set; } =
        Regions.WorkingSets | Regions.SystemFileCache | Regions.ModifiedList |
        Regions.StandbyLowPriority | Regions.RegistryCache | Regions.CombineMemory;

    public bool AutoByThreshold { get; set; } = true;
    public int ThresholdPercent { get; set; } = 85;

    public bool AutoByInterval { get; set; }
    public int IntervalMinutes { get; set; } = 30;

    /// <summary>Не делать автоочистку, пока на переднем плане полноэкранное приложение (игра).</summary>
    public bool SkipWhenFullscreen { get; set; } = true;

    /// <summary>Минимальная пауза между двумя автоочистками, чтобы не чистить по кругу.</summary>
    public int CooldownSeconds { get; set; } = 120;

    public bool Notifications { get; set; } = true;

    public bool HotkeyEnabled { get; set; } = true;
    public Keys Hotkey { get; set; } = Keys.Control | Keys.Shift | Keys.F1;

    /// <summary>Процессы, у которых не трогаем рабочий набор (без .exe, через запятую в UI).</summary>
    public List<string> Exclusions { get; set; } = new();

    public ulong TotalFreedBytes { get; set; }
    public int TotalCleanups { get; set; }

    // ── хранение ──────────────────────────────────────────────────────────
    private static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RAMFlow");
    private static readonly string FilePath = Path.Combine(Dir, "settings.json");
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath), Json) ?? new Settings();
        }
        catch { /* битый файл — начинаем с настроек по умолчанию */ }
        return new Settings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Json));
        }
        catch { /* нет доступа к профилю — просто не сохраняем */ }
    }
}
