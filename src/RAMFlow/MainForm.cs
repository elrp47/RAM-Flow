namespace RAMFlow;

public sealed class MainForm : Form
{
    private readonly TrayApp _app;
    private bool _syncing;

    // Обзор
    private readonly Panel _overview = new() { Dock = DockStyle.Fill, BackColor = Theme.Bg };
    private readonly Card _gaugeCard = new() { Title = "Загрузка ОЗУ" };
    private readonly Card _statsCard = new() { Title = "Память" };
    private readonly Card _graphCard = new() { Title = "Последние 3 минуты", Note = "пунктир — порог автоочистки" };
    private readonly Card _actionCard = new();
    private readonly RingGauge _gauge = new() { Dock = DockStyle.Fill };
    private readonly StatTile _used = new() { Caption = "Занято" };
    private readonly StatTile _free = new() { Caption = "Свободно" };
    private readonly StatTile _commit = new() { Caption = "Выделено" };
    private readonly StatTile _cache = new() { Caption = "Системный кэш" };
    private readonly HistoryGraph _graph = new() { Dock = DockStyle.Fill };
    private readonly FlatButton _cleanBtn = new() { Text = "Очистить память", Size = new Size(200, 48) };
    private readonly Label _statusMain = MakeLabel(Theme.RowTitle, Theme.Text);
    private readonly Label _statusSub = MakeLabel(Theme.Small, Theme.Dim);
    private readonly Label _autoInfo = MakeLabel(Theme.Small, Theme.Dim, ContentAlignment.MiddleRight);

    // Настройки
    private readonly ScrollStack _settings = new() { Dock = DockStyle.Fill, Visible = false };
    private readonly ToggleSwitch[] _regionToggles = new ToggleSwitch[Cleaner.All.Length];
    private readonly ToggleSwitch _autoThreshold = new();
    private readonly Stepper _threshold = new() { Minimum = 30, Maximum = 99, Suffix = " %" };
    private readonly ToggleSwitch _autoInterval = new();
    private readonly Stepper _interval = new() { Minimum = 1, Maximum = 1440, Suffix = " мин" };
    private readonly ToggleSwitch _skipFullscreen = new();
    private readonly Stepper _cooldown = new() { Minimum = 30, Maximum = 3600, Step = 30, Suffix = " сек" };
    private readonly ToggleSwitch _autostart = new();
    private readonly ToggleSwitch _notify = new();
    private readonly ToggleSwitch _hotkeyOn = new();
    private readonly HotkeyBox _hotkeyBox = new();
    private readonly InputBox _exclusions = new();

    private readonly Tabs _tabs = new("Обзор", "Настройки");

    public MainForm(TrayApp app)
    {
        _app = app;
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "RAM Flow";
        BackColor = Theme.Bg;
        ForeColor = Theme.Text;
        Font = Theme.Body;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(860, 620);
        Size = new Size(980, 700);
        Padding = new Padding(24, 16, 24, 24);
        Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? Application.ExecutablePath);

        BuildHeader();
        BuildOverview();
        BuildSettings();

        var content = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg };
        content.Controls.Add(_overview);
        content.Controls.Add(_settings);
        Controls.Add(content);
        content.BringToFront();

        SyncFromSettings();
        WireEvents();
        ResumeLayout(true);

        _app.Sampled += OnSampled;
        _app.Cleaned += OnCleaned;
        OnSampled();
        UpdateStatus();
    }

    private static Label MakeLabel(Font font, Color color, ContentAlignment align = ContentAlignment.MiddleLeft) => new()
    {
        Font = font, ForeColor = color, BackColor = Theme.Card, AutoSize = false,
        TextAlign = align, AutoEllipsis = true, UseMnemonic = false,
    };

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.DarkTitleBar(Handle);
    }

    // ── шапка ────────────────────────────────────────────────────────────
    private void BuildHeader()
    {
        var header = new Panel { Dock = DockStyle.Top, Height = 72, BackColor = Theme.Bg };
        var logo = new LogoMark { Size = new Size(40, 40) };
        var title = new Label { Text = "RAM Flow", Font = Theme.Title, ForeColor = Theme.Text, AutoSize = true, BackColor = Theme.Bg };
        var sub = new Label { Text = "Очистка оперативной памяти", Font = Theme.Small, ForeColor = Theme.Dim, AutoSize = true, BackColor = Theme.Bg };
        _tabs.BackColor = Theme.Bg;
        header.Controls.AddRange(new Control[] { logo, title, sub, _tabs });

        header.Layout += (_, _) =>
        {
            int cy = (header.Height - header.S(16)) / 2;
            logo.Location = new Point(0, cy - logo.Height / 2);
            int x = logo.Right + header.S(12);
            title.Location = new Point(x, cy - title.Height + header.S(4));
            sub.Location = new Point(x + header.S(2), title.Bottom - header.S(2));
            _tabs.Location = new Point(header.Width - _tabs.Width, cy - _tabs.Height / 2);
        };
        Controls.Add(header);
    }

    // ── обзор ────────────────────────────────────────────────────────────
    private void BuildOverview()
    {
        _gaugeCard.Controls.Add(_gauge);
        _statsCard.Controls.AddRange(new Control[] { _used, _free, _commit, _cache });
        _graphCard.Controls.Add(_graph);
        _actionCard.Controls.AddRange(new Control[] { _cleanBtn, _statusMain, _statusSub, _autoInfo });
        _overview.Controls.AddRange(new Control[] { _gaugeCard, _statsCard, _graphCard, _actionCard });

        _overview.Layout += (_, _) =>
        {
            int gap = _overview.S(16), w = _overview.Width, h = _overview.Height;
            int topH = _overview.S(250), gaugeW = _overview.S(270), actionH = _overview.S(88);
            _gaugeCard.SetBounds(0, 0, gaugeW, topH);
            _statsCard.SetBounds(gaugeW + gap, 0, w - gaugeW - gap, topH);
            _actionCard.SetBounds(0, h - actionH, w, actionH);
            _graphCard.SetBounds(0, topH + gap, w, Math.Max(_overview.S(120), h - topH - actionH - 2 * gap));
        };

        _statsCard.Layout += (_, _) =>
        {
            var r = _statsCard.DisplayRectangle;
            int gap = _statsCard.S(12);
            int tw = (r.Width - gap) / 2, th = (r.Height - gap) / 2;
            _used.SetBounds(r.X, r.Y, tw, th);
            _free.SetBounds(r.X + tw + gap, r.Y, r.Width - tw - gap, th);
            _commit.SetBounds(r.X, r.Y + th + gap, tw, r.Height - th - gap);
            _cache.SetBounds(r.X + tw + gap, r.Y + th + gap, r.Width - tw - gap, r.Height - th - gap);
        };

        _actionCard.Layout += (_, _) =>
        {
            var r = _actionCard.DisplayRectangle;
            _cleanBtn.Location = new Point(r.X, r.Y + (r.Height - _cleanBtn.Height) / 2);
            int x = _cleanBtn.Right + _actionCard.S(20);
            int autoW = _actionCard.S(230);
            int textW = Math.Max(50, r.Right - x - autoW - _actionCard.S(12));
            int lineH = _actionCard.S(22);
            _statusMain.SetBounds(x, r.Y + r.Height / 2 - lineH, textW, lineH);
            _statusSub.SetBounds(x, r.Y + r.Height / 2, textW, lineH);
            _autoInfo.SetBounds(r.Right - autoW, r.Y, autoW, r.Height);
        };
    }

    // ── настройки ────────────────────────────────────────────────────────
    private void BuildSettings()
    {
        var regions = new StackCard { Title = "Что чистить" };
        for (int i = 0; i < Cleaner.All.Length; i++)
        {
            _regionToggles[i] = new ToggleSwitch();
            var (_, name, hint) = Cleaner.All[i];
            regions.Controls.Add(new SettingRow(name, hint, _regionToggles[i]) { Separator = i > 0 });
        }

        var auto = new StackCard { Title = "Автоочистка" };
        auto.Controls.Add(new SettingRow("При высокой загрузке", "Чистить, когда занято больше порога.", _threshold, _autoThreshold) { Separator = false });
        auto.Controls.Add(new SettingRow("По таймеру", "Чистить через равные промежутки времени.", _interval, _autoInterval));
        auto.Controls.Add(new SettingRow("Игровой режим", "Не чистить автоматически, пока открыта полноэкранная игра, — никаких подлагиваний.", _skipFullscreen));
        auto.Controls.Add(new SettingRow("Пауза между автоочистками", "Защита от очистки по кругу, если память сразу снова занимается.", _cooldown));

        var system = new StackCard { Title = "Система" };
        system.Controls.Add(new SettingRow("Запуск вместе с Windows", "Через Планировщик заданий — без окна UAC при входе.", _autostart) { Separator = false });
        system.Controls.Add(new SettingRow("Уведомления", "Показывать, сколько памяти освободила автоочистка.", _notify));
        system.Controls.Add(new SettingRow("Горячая клавиша", "Нажмите на поле, затем сочетание. Backspace — убрать.", _hotkeyBox, _hotkeyOn));

        var excl = new StackCard { Title = "Исключения" };
        excl.Controls.Add(new SettingRow("Не выгружать процессы", "Имена через запятую. Их память при очистке не трогается.") { Separator = false });
        excl.Controls.Add(_exclusions);
        _exclusions.Box.PlaceholderText = "например: gmod, hl2, chrome";

        _settings.Controls.AddRange(new Control[] { regions, auto, system, excl });
    }

    private void WireEvents()
    {
        _tabs.SelectedChanged += (_, _) =>
        {
            _overview.Visible = _tabs.Selected == 0;
            _settings.Visible = _tabs.Selected == 1;
        };

        _cleanBtn.Click += async (_, _) =>
        {
            if (!_cleanBtn.Enabled) return;
            _cleanBtn.Enabled = false;
            _cleanBtn.Text = "Очистка…";
            await _app.CleanAsync(manual: true);
            _cleanBtn.Text = "Очистить память";
            _cleanBtn.Enabled = true;
        };

        var s = _app.Settings;
        for (int i = 0; i < _regionToggles.Length; i++)
        {
            var flag = Cleaner.All[i].Region;
            var t = _regionToggles[i];
            t.CheckedChanged += (_, _) =>
            {
                if (_syncing) return;
                s.Regions = t.Checked ? s.Regions | flag : s.Regions & ~flag;
                s.Save();
            };
        }

        void Bind(ToggleSwitch t, Action<bool> set) =>
            t.CheckedChanged += (_, _) => { if (!_syncing) { set(t.Checked); _app.SettingsChanged(); } };
        void BindNum(Stepper st, Action<int> set) =>
            st.ValueChanged += (_, _) => { if (!_syncing) { set(st.Value); _app.SettingsChanged(); } };

        Bind(_autoThreshold, v => s.AutoByThreshold = v);
        Bind(_autoInterval, v => { s.AutoByInterval = v; _app.ResetInterval(); });
        Bind(_skipFullscreen, v => s.SkipWhenFullscreen = v);
        Bind(_notify, v => s.Notifications = v);
        Bind(_hotkeyOn, v => s.HotkeyEnabled = v);
        BindNum(_threshold, v => s.ThresholdPercent = v);
        BindNum(_interval, v => { s.IntervalMinutes = v; _app.ResetInterval(); });
        BindNum(_cooldown, v => s.CooldownSeconds = v);

        _autostart.CheckedChanged += (_, _) =>
        {
            if (_syncing) return;
            if (!Autostart.Set(_autostart.Checked))
            {
                MessageBox.Show(this, "Не удалось изменить задачу в Планировщике заданий.", Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _autostart.SetSilently(Autostart.IsEnabled());
            }
        };

        _hotkeyBox.HotkeyPicked += keys =>
        {
            if (keys == Keys.None) s.HotkeyEnabled = false;
            else { s.Hotkey = keys; s.HotkeyEnabled = true; }
            _app.SettingsChanged();
            if (s.HotkeyEnabled && !_app.ApplyHotkey())
                MessageBox.Show(this, "Это сочетание уже занято другой программой.", Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
        };

        _exclusions.Box.Leave += (_, _) =>
        {
            s.Exclusions = _exclusions.Box.Text
                .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? x[..^4] : x)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            s.Save();
        };
    }

    public void SyncFromSettings()
    {
        _syncing = true;
        var s = _app.Settings;
        for (int i = 0; i < _regionToggles.Length; i++)
            _regionToggles[i].SetSilently(s.Regions.HasFlag(Cleaner.All[i].Region));
        _autoThreshold.SetSilently(s.AutoByThreshold);
        _threshold.SetSilently(s.ThresholdPercent);
        _autoInterval.SetSilently(s.AutoByInterval);
        _interval.SetSilently(s.IntervalMinutes);
        _skipFullscreen.SetSilently(s.SkipWhenFullscreen);
        _cooldown.SetSilently(s.CooldownSeconds);
        _notify.SetSilently(s.Notifications);
        _hotkeyOn.SetSilently(s.HotkeyEnabled);
        _hotkeyBox.Keys = s.HotkeyEnabled ? s.Hotkey : Keys.None;
        _hotkeyBox.Invalidate();
        if (!_exclusions.Box.Focused) _exclusions.Box.Text = string.Join(", ", s.Exclusions);
        _autostart.SetSilently(Autostart.IsEnabled());

        int threshold = s.AutoByThreshold ? s.ThresholdPercent : 0;
        _graph.Threshold = threshold;
        _gauge.Threshold = threshold;
        _gauge.Invalidate();
        _graph.Invalidate();
        UpdateAutoInfo();
        _syncing = false;
    }

    private void UpdateAutoInfo()
    {
        var s = _app.Settings;
        var parts = new List<string>();
        if (s.AutoByThreshold) parts.Add($"при ≥ {s.ThresholdPercent}%");
        if (s.AutoByInterval) parts.Add($"каждые {s.IntervalMinutes} мин");
        string auto = parts.Count > 0 ? "Автоочистка: " + string.Join(", ", parts) : "Автоочистка выключена";
        string hk = s.HotkeyEnabled ? "\nГорячая клавиша: " + HotkeyBox.Describe(s.Hotkey) : "";
        _autoInfo.Text = auto + hk;
    }

    private void OnSampled()
    {
        var m = _app.Current;
        int th = _app.Settings.ThresholdPercent;
        _gauge.Caption = $"{Memory.Format(m.UsedPhys)} из {Memory.Format(m.TotalPhys)}";
        _gauge.Percent = m.LoadPercent;

        _used.Set(Memory.Format(m.UsedPhys), $"{m.LoadPercent}% от {Memory.Format(m.TotalPhys)}",
            m.LoadPercent / 100f, Theme.LoadColor(m.LoadPercent, th));
        _free.Set(Memory.Format(m.AvailPhys), "доступно программам",
            m.TotalPhys == 0 ? 0 : (float)m.AvailPhys / m.TotalPhys, Theme.Green);
        _commit.Set(Memory.Format(m.CommitUsed), $"лимит {Memory.Format(m.CommitLimit)}",
            m.CommitPercent / 100f, Theme.Accent);
        _cache.Set(Memory.Format(m.SystemCache), "файлы в памяти",
            m.TotalPhys == 0 ? 0 : (float)m.SystemCache / m.TotalPhys, Color.FromArgb(160, 120, 255));
        _graph.Add(m.LoadPercent);
    }

    private void OnCleaned()
    {
        _graph.MarkCleanup();
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        var s = _app.Settings;
        string total = $"Всего очисток: {s.TotalCleanups} · освобождено {Memory.Format(s.TotalFreedBytes)}";
        var r = _app.LastResult;
        if (r == null)
        {
            _statusMain.Text = "Готово к очистке";
            _statusSub.Text = total;
            return;
        }

        _statusMain.Text = r.FreedBytes > 0
            ? $"Освобождено {Memory.Format(r.FreedBytes)} в {_app.LastCleanAt:HH:mm}"
            : $"Память уже чистая · {_app.LastCleanAt:HH:mm}";
        _statusMain.ForeColor = r.Errors.Count > 0 ? Theme.Amber : Theme.Text;
        _statusSub.Text = (r.Errors.Count > 0 ? $"Ошибок: {r.Errors.Count} · " : "") + total;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // Крестик прячет окно в трей; выход — через меню значка.
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _app.Sampled -= OnSampled;
            _app.Cleaned -= OnCleaned;
        }
        base.Dispose(disposing);
    }
}

/// <summary>Логотип: скруглённый квадрат с тремя «волнами» потока.</summary>
internal sealed class LogoMark : PaintedControl
{
    public LogoMark() { BackColor = Theme.Bg; }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics.Smooth();
        g.Clear(BackColor);
        var r = new RectangleF(0, 0, Width - 1, Height - 1);
        using (var path = Theme.Round(r, Width * 0.28f))
        using (var brush = new System.Drawing.Drawing2D.LinearGradientBrush(r, Theme.Accent, Color.FromArgb(120, 90, 255), 45f))
            g.FillPath(brush, path);

        using var pen = new Pen(Color.White, Width * 0.075f)
        { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
        for (int i = 0; i < 3; i++)
        {
            float y = Height * (0.34f + i * 0.16f);
            float x0 = Width * (0.24f + (i == 1 ? 0.08f : 0)), x1 = Width * (0.76f - (i == 2 ? 0.14f : 0));
            g.DrawBezier(pen, x0, y, (x0 + x1) / 2 - Width * 0.08f, y - Height * 0.08f,
                (x0 + x1) / 2 + Width * 0.08f, y + Height * 0.08f, x1, y);
        }
    }
}
