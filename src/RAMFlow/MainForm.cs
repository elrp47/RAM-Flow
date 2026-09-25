namespace RAMFlow;

public sealed class MainForm : Form
{
    private readonly TrayApp _app;
    private bool _syncing;

    private readonly Label _physText = new() { AutoSize = true };
    private readonly Label _commitText = new() { AutoSize = true };
    private readonly Label _cacheText = new() { AutoSize = true };
    private readonly ProgressBar _physBar = new() { Dock = DockStyle.Fill, Height = 14 };
    private readonly ProgressBar _commitBar = new() { Dock = DockStyle.Fill, Height = 14 };
    private readonly HistoryGraph _graph = new() { Dock = DockStyle.Fill, MinimumSize = new Size(0, 110) };

    private readonly CheckedListBox _regions = new() { Dock = DockStyle.Fill, CheckOnClick = true, IntegralHeight = false };
    private readonly Label _regionHint = new() { Dock = DockStyle.Fill, ForeColor = SystemColors.GrayText };

    private readonly CheckBox _autoThreshold = new() { Text = "Чистить, когда загрузка ≥", AutoSize = true };
    private readonly NumericUpDown _threshold = new() { Minimum = 30, Maximum = 99, Width = 60 };
    private readonly CheckBox _autoInterval = new() { Text = "Чистить каждые (мин)", AutoSize = true };
    private readonly NumericUpDown _interval = new() { Minimum = 1, Maximum = 1440, Width = 60 };
    private readonly CheckBox _skipFullscreen = new() { Text = "Не чистить автоматически во время полноэкранных игр", AutoSize = true };

    private readonly CheckBox _autostart = new() { Text = "Запускать вместе с Windows", AutoSize = true };
    private readonly CheckBox _notify = new() { Text = "Уведомления об автоочистке", AutoSize = true };
    private readonly CheckBox _hotkeyOn = new() { Text = "Горячая клавиша:", AutoSize = true };
    private readonly TextBox _hotkeyBox = new() { ReadOnly = true, Width = 160 };
    private readonly TextBox _exclusions = new() { Dock = DockStyle.Fill, PlaceholderText = "например: gmod, hl2, chrome" };

    private readonly Button _cleanBtn = new() { Text = "Очистить сейчас", Height = 40, Dock = DockStyle.Fill };
    private readonly Label _status = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
    private readonly ToolTip _tip = new();

    public MainForm(TrayApp app)
    {
        _app = app;
        Text = "RAM Flow";
        Font = new Font("Segoe UI", 9f);
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(620, 640);
        Size = new Size(680, 720);
        Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? Application.ExecutablePath);

        BuildLayout();
        foreach (var r in Cleaner.All) _regions.Items.Add(r.Name);
        SyncFromSettings();
        WireEvents();

        _app.Sampled += OnSampled;
        _app.Cleaned += OnCleaned;
        OnSampled();
        UpdateStatus();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(10) };
        Controls.Add(root);

        // Память
        var mem = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true };
        mem.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
        mem.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        mem.Controls.Add(_physText, 0, 0); mem.Controls.Add(_physBar, 1, 0);
        mem.Controls.Add(_commitText, 0, 1); mem.Controls.Add(_commitBar, 1, 1);
        mem.Controls.Add(_cacheText, 0, 2);
        root.Controls.Add(Group("Память", mem, autoSize: true));

        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(_graph);
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 40));

        // Области и автоочистка рядом
        var mid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        mid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        mid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        var reg = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        reg.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        reg.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        reg.Controls.Add(_regions, 0, 0);
        reg.Controls.Add(_regionHint, 0, 1);
        mid.Controls.Add(Group("Что чистить", reg), 0, 0);

        var auto = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        auto.Controls.Add(Row(_autoThreshold, _threshold, new Label { Text = "%", AutoSize = true, Margin = new Padding(0, 6, 0, 0) }));
        auto.Controls.Add(Row(_autoInterval, _interval));
        _skipFullscreen.MaximumSize = new Size(290, 0);
        auto.Controls.Add(_skipFullscreen);
        auto.Controls.Add(_autostart);
        auto.Controls.Add(_notify);
        auto.Controls.Add(Row(_hotkeyOn, _hotkeyBox));
        mid.Controls.Add(Group("Автоматика", auto), 1, 0);

        root.Controls.Add(mid);
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 60));

        root.Controls.Add(Group("Не трогать рабочий набор этих процессов", _exclusions, autoSize: true));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var bottom = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Height = 46 };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottom.Controls.Add(_cleanBtn, 0, 0);
        bottom.Controls.Add(_status, 1, 0);
        root.Controls.Add(bottom);
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
    }

    private static GroupBox Group(string title, Control content, bool autoSize = false)
    {
        var g = new GroupBox { Text = title, Dock = DockStyle.Fill, Padding = new Padding(8) };
        if (autoSize) { g.AutoSize = true; g.AutoSizeMode = AutoSizeMode.GrowAndShrink; }
        content.Dock = DockStyle.Fill;
        g.Controls.Add(content);
        return g;
    }

    private static FlowLayoutPanel Row(params Control[] items)
    {
        var p = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
        foreach (var c in items)
        {
            if (c is CheckBox) c.Margin = new Padding(3, 5, 3, 0);
            p.Controls.Add(c);
        }
        return p;
    }

    private void WireEvents()
    {
        _cleanBtn.Click += async (_, _) =>
        {
            _cleanBtn.Enabled = false;
            _status.Text = "Очистка…";
            await _app.CleanAsync(manual: true);
            _cleanBtn.Enabled = true;
        };

        _regions.SelectedIndexChanged += (_, _) =>
        {
            int i = _regions.SelectedIndex;
            _regionHint.Text = i >= 0 ? Cleaner.All[i].Hint : "";
        };
        _regions.ItemCheck += (_, e) =>
        {
            if (_syncing) return;
            var flag = Cleaner.All[e.Index].Region;
            if (e.NewValue == CheckState.Checked) _app.Settings.Regions |= flag;
            else _app.Settings.Regions &= ~flag;
            _app.Settings.Save();
        };

        void Bind(CheckBox cb, Action<bool> set) =>
            cb.CheckedChanged += (_, _) => { if (!_syncing) { set(cb.Checked); _app.SettingsChanged(); } };

        var s = _app.Settings;
        Bind(_autoThreshold, v => s.AutoByThreshold = v);
        Bind(_autoInterval, v => { s.AutoByInterval = v; _app.ResetInterval(); });
        Bind(_skipFullscreen, v => s.SkipWhenFullscreen = v);
        Bind(_notify, v => s.Notifications = v);
        Bind(_hotkeyOn, v => s.HotkeyEnabled = v);

        _threshold.ValueChanged += (_, _) =>
        {
            if (_syncing) return;
            s.ThresholdPercent = (int)_threshold.Value;
            _app.SettingsChanged();
        };
        _interval.ValueChanged += (_, _) =>
        {
            if (_syncing) return;
            s.IntervalMinutes = (int)_interval.Value;
            _app.ResetInterval();
            _app.SettingsChanged();
        };

        _autostart.CheckedChanged += (_, _) =>
        {
            if (_syncing) return;
            if (!Autostart.Set(_autostart.Checked))
            {
                MessageBox.Show(this, "Не удалось изменить задачу в Планировщике заданий.", Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _syncing = true;
                _autostart.Checked = Autostart.IsEnabled();
                _syncing = false;
            }
        };

        // Нажатие сочетания прямо в поле задаёт горячую клавишу.
        _hotkeyBox.KeyDown += (_, e) =>
        {
            e.SuppressKeyPress = true;
            Keys key = e.KeyCode;
            if (key is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin) return;
            if (key is Keys.Back or Keys.Delete or Keys.Escape) { s.HotkeyEnabled = false; }
            else
            {
                s.Hotkey = e.Modifiers | key;
                s.HotkeyEnabled = true;
            }
            _app.SettingsChanged();
            if (s.HotkeyEnabled && !_app.ApplyHotkey())
                MessageBox.Show(this, "Это сочетание уже занято другой программой.", Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
        };

        _exclusions.Leave += (_, _) =>
        {
            s.Exclusions = _exclusions.Text
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
        for (int i = 0; i < Cleaner.All.Length; i++)
            _regions.SetItemChecked(i, s.Regions.HasFlag(Cleaner.All[i].Region));
        _autoThreshold.Checked = s.AutoByThreshold;
        _threshold.Value = Math.Clamp(s.ThresholdPercent, 30, 99);
        _autoInterval.Checked = s.AutoByInterval;
        _interval.Value = Math.Clamp(s.IntervalMinutes, 1, 1440);
        _skipFullscreen.Checked = s.SkipWhenFullscreen;
        _notify.Checked = s.Notifications;
        _hotkeyOn.Checked = s.HotkeyEnabled;
        _hotkeyBox.Text = s.HotkeyEnabled ? KeysText(s.Hotkey) : "нет";
        if (!_exclusions.Focused) _exclusions.Text = string.Join(", ", s.Exclusions);
        _autostart.Checked = Autostart.IsEnabled();
        _graph.Threshold = s.AutoByThreshold ? s.ThresholdPercent : 0;
        _syncing = false;
    }

    private static string KeysText(Keys k)
    {
        var parts = new List<string>();
        if (k.HasFlag(Keys.Control)) parts.Add("Ctrl");
        if (k.HasFlag(Keys.Shift)) parts.Add("Shift");
        if (k.HasFlag(Keys.Alt)) parts.Add("Alt");
        parts.Add((k & Keys.KeyCode).ToString());
        return string.Join(" + ", parts);
    }

    private void OnSampled()
    {
        var m = _app.Current;
        _physText.Text = $"ОЗУ: {Memory.Format(m.UsedPhys)} из {Memory.Format(m.TotalPhys)} ({m.LoadPercent}%)";
        _commitText.Text = $"Выделено: {Memory.Format(m.CommitUsed)} из {Memory.Format(m.CommitLimit)}";
        _cacheText.Text = $"Системный кэш: {Memory.Format(m.SystemCache)}";
        _physBar.Value = Math.Clamp(m.LoadPercent, 0, 100);
        _commitBar.Value = Math.Clamp(m.CommitPercent, 0, 100);
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
        string total = $"Всего очисток: {s.TotalCleanups}, освобождено {Memory.Format(s.TotalFreedBytes)}";
        var r = _app.LastResult;
        if (r == null) { _status.Text = total; return; }

        _status.Text = $"{_app.LastCleanAt:HH:mm:ss} — освобождено {Memory.Format(Math.Max(0, r.FreedBytes))} " +
                       $"за {r.Duration.TotalMilliseconds:0} мс" +
                       (r.Errors.Count > 0 ? $" (ошибок: {r.Errors.Count})" : "") + "\n" + total;
        _tip.SetToolTip(_status, r.Errors.Count > 0 ? string.Join("\n", r.Errors) : "");
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
