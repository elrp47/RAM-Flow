using System.Drawing.Text;

namespace RAMFlow;

/// <summary>Живёт в трее: замеры, автоочистка, горячая клавиша, значок с процентом.</summary>
public sealed class TrayApp : ApplicationContext
{
    public Settings Settings { get; }
    public MemorySnapshot Current { get; private set; }
    public CleanResult? LastResult { get; private set; }
    public DateTime LastCleanAt { get; private set; } = DateTime.MinValue;

    public event Action? Sampled;
    public event Action? Cleaned;

    private readonly NotifyIcon _tray;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly HotkeyWindow _hotkey;
    private MainForm? _form;
    private IntPtr _iconHandle;
    private int _iconPercent = -1;
    private bool _busy;
    private DateTime _nextIntervalClean;

    public TrayApp(bool startInTray)
    {
        Settings = Settings.Load();
        Current = Memory.Read();

        _tray = new NotifyIcon { Visible = true, Text = "RAM Flow" };
        _tray.ContextMenuStrip = BuildMenu();
        _tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) ShowWindow(); };
        UpdateIcon();

        _hotkey = new HotkeyWindow(() => _ = CleanAsync(manual: true));
        ApplyHotkey();

        ResetInterval();
        _timer = new System.Windows.Forms.Timer { Interval = 1000 };
        _timer.Tick += (_, _) => Tick();
        _timer.Start();

        if (!startInTray) ShowWindow();
    }

    // ── цикл замеров ──────────────────────────────────────────────────────
    private void Tick()
    {
        Current = Memory.Read();
        UpdateIcon();
        Sampled?.Invoke();

        if (_busy) return;
        bool cooldownOver = (DateTime.Now - LastCleanAt).TotalSeconds >= Settings.CooldownSeconds;

        string? reason = null;
        if (Settings.AutoByThreshold && cooldownOver && Current.LoadPercent >= Settings.ThresholdPercent)
            reason = $"загрузка {Current.LoadPercent}%";
        else if (Settings.AutoByInterval && DateTime.Now >= _nextIntervalClean)
            reason = "по таймеру";

        if (reason == null) return;
        ResetInterval();
        if (Settings.SkipWhenFullscreen && IsForegroundFullscreen()) return;
        _ = CleanAsync(manual: false);
    }

    public void ResetInterval() =>
        _nextIntervalClean = DateTime.Now.AddMinutes(Math.Max(1, Settings.IntervalMinutes));

    public async Task CleanAsync(bool manual)
    {
        if (_busy) return;
        _busy = true;
        try
        {
            var regions = Settings.Regions;
            var excl = Settings.Exclusions.ToArray();
            var result = await Task.Run(() => Cleaner.Clean(regions, excl));

            LastResult = result;
            LastCleanAt = DateTime.Now;
            if (result.FreedBytes > 0) Settings.TotalFreedBytes += (ulong)result.FreedBytes;
            Settings.TotalCleanups++;
            Settings.Save();
            Current = result.After;
            UpdateIcon();
            Cleaned?.Invoke();

            if (Settings.Notifications || manual)
            {
                string text = result.FreedBytes > 0
                    ? $"Освобождено {Memory.Format(result.FreedBytes)} · загрузка {result.After.LoadPercent}%"
                    : $"Свободной памяти не прибавилось · загрузка {result.After.LoadPercent}%";
                if (result.Errors.Count > 0) text += $"\nОшибок: {result.Errors.Count}";
                _tray.ShowBalloonTip(2500, manual ? "Память очищена" : "Автоочистка", text, ToolTipIcon.Info);
            }
        }
        finally { _busy = false; }
    }

    private static bool IsForegroundFullscreen()
    {
        IntPtr fg = Native.GetForegroundWindow();
        if (fg == IntPtr.Zero || fg == Native.GetShellWindow() || fg == Native.GetDesktopWindow()) return false;
        if (!Native.GetWindowRect(fg, out var r)) return false;
        var bounds = Screen.FromHandle(fg).Bounds;
        return r.Left <= bounds.Left && r.Top <= bounds.Top && r.Right >= bounds.Right && r.Bottom >= bounds.Bottom;
    }

    // ── значок в трее ─────────────────────────────────────────────────────
    private void UpdateIcon()
    {
        int pct = Current.LoadPercent;
        _tray.Text = $"RAM Flow — {pct}% · свободно {Memory.Format(Current.AvailPhys)}";
        if (pct == _iconPercent) return;
        _iconPercent = pct;

        int size = SystemInformation.SmallIconSize.Width;
        using var bmp = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            Theme.FillRound(g, Theme.LoadColor(pct, Settings.ThresholdPercent), new RectangleF(0, 0, size, size), size * 0.22f);

            string s = pct >= 100 ? "99" : pct.ToString();
            using var font = new Font("Segoe UI", size * 0.55f, FontStyle.Bold, GraphicsUnit.Pixel);
            using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(s, font, Brushes.White, new RectangleF(0, 0, size, size + 1), sf);
        }

        IntPtr h = bmp.GetHicon();
        var old = _tray.Icon;
        _tray.Icon = Icon.FromHandle(h);
        old?.Dispose();
        if (_iconHandle != IntPtr.Zero) Native.DestroyIcon(_iconHandle);
        _iconHandle = h;
    }

    private ContextMenuStrip BuildMenu()
    {
        var m = new ContextMenuStrip { Renderer = new DarkMenuRenderer(), ShowImageMargin = true, Font = Theme.Body };
        m.Items.Add("Очистить сейчас", null, async (_, _) => await CleanAsync(manual: true)).Font =
            new Font(m.Font, FontStyle.Bold);
        m.Items.Add("Открыть окно", null, (_, _) => ShowWindow());
        m.Items.Add(new ToolStripSeparator());

        var auto = new ToolStripMenuItem("Автоочистка при загрузке ≥ порога") { CheckOnClick = true };
        var timer = new ToolStripMenuItem("Автоочистка по таймеру") { CheckOnClick = true };
        auto.CheckedChanged += (_, _) => { Settings.AutoByThreshold = auto.Checked; SettingsChanged(); };
        timer.CheckedChanged += (_, _) => { Settings.AutoByInterval = timer.Checked; SettingsChanged(); };
        m.Items.Add(auto);
        m.Items.Add(timer);
        m.Items.Add(new ToolStripSeparator());
        m.Items.Add("Выход", null, (_, _) => ExitThread());

        m.Opening += (_, _) => { auto.Checked = Settings.AutoByThreshold; timer.Checked = Settings.AutoByInterval; };
        return m;
    }

    // ── окно и настройки ──────────────────────────────────────────────────
    public void ShowWindow()
    {
        if (_form == null || _form.IsDisposed)
            _form = new MainForm(this);
        _form.Show();
        if (_form.WindowState == FormWindowState.Minimized) _form.WindowState = FormWindowState.Normal;
        _form.Activate();
    }

    public void SettingsChanged()
    {
        Settings.Save();
        _iconPercent = -1;
        UpdateIcon();
        ApplyHotkey();
        _form?.SyncFromSettings();
    }

    public bool ApplyHotkey() =>
        _hotkey.Register(Settings.HotkeyEnabled ? Settings.Hotkey : Keys.None);

    protected override void ExitThreadCore()
    {
        _timer.Stop();
        _hotkey.Register(Keys.None);
        _hotkey.DestroyHandle();
        _tray.Visible = false;
        _tray.Dispose();
        if (_iconHandle != IntPtr.Zero) Native.DestroyIcon(_iconHandle);
        Settings.Save();
        _form?.Dispose();
        base.ExitThreadCore();
    }
}

/// <summary>Скрытое окно, принимающее WM_HOTKEY.</summary>
internal sealed class HotkeyWindow : NativeWindow
{
    private const int Id = 0x4D50;
    private readonly Action _onHotkey;
    private bool _registered;

    public HotkeyWindow(Action onHotkey)
    {
        _onHotkey = onHotkey;
        CreateHandle(new CreateParams());
    }

    public bool Register(Keys keys)
    {
        if (_registered) { Native.UnregisterHotKey(Handle, Id); _registered = false; }
        Keys key = keys & Keys.KeyCode;
        if (key == Keys.None || Handle == IntPtr.Zero) return true;

        uint mod = Native.MOD_NOREPEAT;
        if (keys.HasFlag(Keys.Control)) mod |= Native.MOD_CONTROL;
        if (keys.HasFlag(Keys.Shift)) mod |= Native.MOD_SHIFT;
        if (keys.HasFlag(Keys.Alt)) mod |= Native.MOD_ALT;
        _registered = Native.RegisterHotKey(Handle, Id, mod, (uint)key);
        return _registered;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == Native.WM_HOTKEY && m.WParam.ToInt32() == Id) _onHotkey();
        base.WndProc(ref m);
    }
}
