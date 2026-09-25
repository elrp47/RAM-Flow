using System.ComponentModel;

namespace RAMFlow;

/// <summary>Основа своих контролов: двойная буферизация и своя отрисовка.</summary>
internal abstract class PaintedControl : Control
{
    protected PaintedControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Theme.Card;
        ForeColor = Theme.Text;
        Font = Theme.Body;
    }

    protected bool Hover { get; private set; }
    protected bool Down { get; private set; }

    protected override void OnMouseEnter(EventArgs e) { Hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { Hover = false; Down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { Down = true; Invalidate(); } base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { Down = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
}

/// <summary>Карточка со скруглёнными углами и необязательным заголовком.</summary>
internal class Card : Panel
{
    private string _title = "";

    public Card()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Card;
        ForeColor = Theme.Text;
        Padding = new Padding(18);
    }

    [DefaultValue("")]
    public string Title
    {
        get => _title;
        set { _title = value; Padding = new Padding(18, value.Length > 0 ? 46 : 18, 18, 18); Invalidate(); }
    }

    /// <summary>Мелкий текст справа от заголовка (например, пояснение к графику).</summary>
    public string Note { get; set; } = "";

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var g = e.Graphics.Smooth();
        g.Clear(Parent?.BackColor ?? Theme.Bg);
        Theme.FillRound(g, BackColor, new RectangleF(0, 0, Width - 1, Height - 1), this.S(14f));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (_title.Length == 0) return;
        var g = e.Graphics.Smooth();
        int x = this.S(18), y = this.S(16);
        TextRenderer.DrawText(g, _title.ToUpperInvariant(), Theme.Label, new Point(x, y), Theme.Dim);
        if (Note.Length > 0)
        {
            var size = TextRenderer.MeasureText(Note, Theme.Small);
            TextRenderer.DrawText(g, Note, Theme.Small, new Point(Width - x - size.Width, y - 1), Theme.Faint);
        }
    }
}

/// <summary>Карточка, которая сама складывает дочерние элементы в столбик и подгоняет свою высоту.</summary>
internal sealed class StackCard : Card
{
    protected override void OnLayout(LayoutEventArgs e)
    {
        int y = Padding.Top, w = ClientSize.Width - Padding.Horizontal;
        foreach (Control c in Controls)
        {
            if (!c.Visible) continue;
            c.SetBounds(Padding.Left, y, w, c.Height);
            y += c.Height;
        }
        int h = y + Padding.Bottom;
        if (Height != h) Height = h;
    }
}

/// <summary>Страница с вертикальной прокруткой: элементы идут столбиком на всю ширину.</summary>
internal sealed class ScrollStack : Panel
{
    public int Gap { get; set; } = 16;

    public ScrollStack()
    {
        AutoScroll = true;
        BackColor = Theme.Bg;
        DoubleBuffered = true;
        SetStyle(ControlStyles.Selectable, true);
        Theme.DarkScrollbars(this);
    }

    // Колесо мыши приходит в элемент с фокусом — забираем фокус, когда курсор над страницей.
    protected override void OnMouseEnter(EventArgs e)
    {
        if (!ContainsFocus) Focus();
        base.OnMouseEnter(e);
    }

    protected override void OnControlAdded(ControlEventArgs e)
    {
        base.OnControlAdded(e);
        HookWheel(e.Control!);
    }

    private void HookWheel(Control c)
    {
        c.MouseEnter += (_, _) => { if (!ContainsFocus) Focus(); };
        c.ControlAdded += (_, a) => HookWheel(a.Control!);
        foreach (Control child in c.Controls) HookWheel(child);
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        int gap = this.S(Gap);
        int w = ClientSize.Width;
        int y = AutoScrollPosition.Y;
        foreach (Control c in Controls)
        {
            if (!c.Visible) continue;
            c.SetBounds(0, y, w, c.Height);
            y += c.Height + gap;
        }
        var min = new Size(0, y - AutoScrollPosition.Y - gap);
        if (AutoScrollMinSize != min) AutoScrollMinSize = min;
        base.OnLayout(e);
    }
}

/// <summary>Строка настройки: заголовок и подсказка слева, элементы управления справа.</summary>
internal sealed class SettingRow : Panel
{
    public string Title { get; }
    public string Hint { get; }
    public bool Separator { get; set; } = true;
    private readonly List<Control> _accessories = new();

    public SettingRow(string title, string hint = "", params Control[] accessories)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        Title = title;
        Hint = hint;
        BackColor = Theme.Card;
        Height = hint.Length > 0 ? 64 : 50;
        foreach (var a in accessories) { _accessories.Add(a); Controls.Add(a); }
    }

    private int TextRight
    {
        get
        {
            int x = ClientSize.Width;
            foreach (var a in _accessories) if (a.Visible) x = Math.Min(x, a.Left);
            return x - this.S(16);
        }
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        int x = ClientSize.Width, gap = this.S(10);
        for (int i = _accessories.Count - 1; i >= 0; i--)
        {
            var a = _accessories[i];
            if (!a.Visible) continue;
            x -= a.Width;
            a.Location = new Point(x, (ClientSize.Height - a.Height) / 2);
            x -= gap;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics.Smooth();
        if (Separator)
            using (var pen = new Pen(Theme.Line))
                g.DrawLine(pen, 0, 0, Width, 0);

        int w = Math.Max(10, TextRight);
        var flags = TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding;
        if (Hint.Length == 0)
        {
            TextRenderer.DrawText(g, Title, Theme.RowTitle, new Rectangle(0, 0, w, Height), Theme.Text,
                flags | TextFormatFlags.VerticalCenter);
            return;
        }
        int titleH = TextRenderer.MeasureText(Title, Theme.RowTitle).Height;
        int hintH = TextRenderer.MeasureText(g, Hint, Theme.Small, new Size(w, 1000), TextFormatFlags.WordBreak).Height;
        hintH = Math.Min(hintH, Height - titleH - this.S(12));
        int top = (Height - titleH - hintH - this.S(3)) / 2;
        TextRenderer.DrawText(g, Title, Theme.RowTitle, new Rectangle(0, top, w, titleH), Theme.Text, flags);
        TextRenderer.DrawText(g, Hint, Theme.Small, new Rectangle(0, top + titleH + this.S(3), w, hintH), Theme.Dim,
            flags | TextFormatFlags.WordBreak);
    }
}

/// <summary>Переключатель «вкл/выкл» в стиле Windows 11.</summary>
internal sealed class ToggleSwitch : PaintedControl
{
    private bool _checked;
    public event EventHandler? CheckedChanged;

    public ToggleSwitch()
    {
        Size = new Size(44, 24);
        Cursor = Cursors.Hand;
        TabStop = true;
    }

    public bool Checked
    {
        get => _checked;
        set { if (_checked == value) return; _checked = value; Invalidate(); CheckedChanged?.Invoke(this, EventArgs.Empty); }
    }

    /// <summary>Выставить значение без события — для синхронизации с настройками.</summary>
    public void SetSilently(bool value) { _checked = value; Invalidate(); }

    protected override void OnClick(EventArgs e) { Focus(); Checked = !Checked; base.OnClick(e); }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Space or Keys.Enter) { Checked = !Checked; e.Handled = true; }
        base.OnKeyDown(e);
    }

    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics.Smooth();
        g.Clear(BackColor);
        var r = new RectangleF(1, 1, Width - 3, Height - 3);
        Color track = !Enabled ? Theme.Line
                    : _checked ? (Hover ? Theme.AccentHover : Theme.Accent)
                    : (Hover ? Color.FromArgb(72, 79, 95) : Theme.Off);
        Theme.FillRound(g, track, r, r.Height / 2);
        if (Focused && ShowFocusCues)
            using (var pen = new Pen(Color.FromArgb(120, Theme.Accent), 1.5f))
            using (var path = Theme.Round(r, r.Height / 2))
                g.DrawPath(pen, path);

        float d = r.Height - this.S(6f);
        float x = _checked ? r.Right - d - this.S(3f) : r.Left + this.S(3f);
        using var knob = new SolidBrush(Enabled ? Color.White : Theme.Dim);
        g.FillEllipse(knob, x, r.Top + (r.Height - d) / 2, d, d);
    }
}

/// <summary>Числовое поле «− 85 % +» с прокруткой колесом.</summary>
internal sealed class Stepper : PaintedControl
{
    private int _value;
    public int Minimum { get; set; }
    public int Maximum { get; set; } = 100;
    public int Step { get; set; } = 1;
    public string Suffix { get; set; } = "";
    public event EventHandler? ValueChanged;

    public Stepper()
    {
        Size = new Size(124, 32);
        TabStop = true;
    }

    public int Value
    {
        get => _value;
        set
        {
            value = Math.Clamp(value, Minimum, Maximum);
            if (_value == value) return;
            _value = value;
            Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void SetSilently(int value) { _value = Math.Clamp(value, Minimum, Maximum); Invalidate(); }

    private int ButtonWidth => Height;
    private int _hoverPart; // -1 минус, 1 плюс, 0 середина

    protected override void OnMouseMove(MouseEventArgs e)
    {
        int part = e.X < ButtonWidth ? -1 : e.X > Width - ButtonWidth ? 1 : 0;
        if (part != _hoverPart) { _hoverPart = part; Cursor = part == 0 ? Cursors.Default : Cursors.Hand; Invalidate(); }
        base.OnMouseMove(e);
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        Focus();
        if (e.X < ButtonWidth) Value -= Step;
        else if (e.X > Width - ButtonWidth) Value += Step;
        base.OnMouseClick(e);
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e) => OnMouseClick(e);

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        // Колесо меняет значение только под курсором; иначе прокручивается страница.
        if (Hover)
        {
            Value += Math.Sign(e.Delta) * Step;
            if (e is HandledMouseEventArgs h) h.Handled = true;
        }
        base.OnMouseWheel(e);
    }

    protected override void OnPreviewKeyDown(PreviewKeyDownEventArgs e)
    {
        if (e.KeyCode is Keys.Up or Keys.Down or Keys.Left or Keys.Right) e.IsInputKey = true;
        base.OnPreviewKeyDown(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Up or Keys.Right) Value += Step;
        if (e.KeyCode is Keys.Down or Keys.Left) Value -= Step;
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics.Smooth();
        g.Clear(BackColor);
        var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        Theme.FillRound(g, Theme.Input, r, this.S(8f));
        if (Hover)
        {
            var btn = _hoverPart < 0 ? new RectangleF(r.X, r.Y, ButtonWidth, r.Height)
                    : _hoverPart > 0 ? new RectangleF(r.Right - ButtonWidth, r.Y, ButtonWidth, r.Height)
                    : RectangleF.Empty;
            if (!btn.IsEmpty) Theme.FillRound(g, Theme.Off, btn, this.S(8f));
        }

        Color sign = Enabled ? Theme.Text : Theme.Faint;
        var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter;
        TextRenderer.DrawText(g, "−", Theme.Button, new Rectangle(0, 0, ButtonWidth, Height), _value > Minimum ? sign : Theme.Faint, flags);
        TextRenderer.DrawText(g, "+", Theme.Button, new Rectangle(Width - ButtonWidth, 0, ButtonWidth, Height), _value < Maximum ? sign : Theme.Faint, flags);
        TextRenderer.DrawText(g, _value + Suffix, Theme.Body, new Rectangle(ButtonWidth, 0, Width - 2 * ButtonWidth, Height),
            Focused ? Theme.Accent : sign, flags);
    }
}

/// <summary>Кнопка: основная (залитая акцентом) или второстепенная.</summary>
internal sealed class FlatButton : PaintedControl
{
    public bool Primary { get; set; } = true;

    public FlatButton()
    {
        Size = new Size(180, 44);
        Cursor = Cursors.Hand;
        TabStop = true;
        Font = Theme.Button;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Space or Keys.Enter) OnClick(EventArgs.Empty);
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics.Smooth();
        g.Clear(BackColor);
        var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        Color fill = !Enabled ? Theme.Off
                   : Primary ? (Down ? Theme.AccentDown : Hover ? Theme.AccentHover : Theme.Accent)
                   : (Down ? Theme.Line : Hover ? Theme.Off : Theme.Input);
        Theme.FillRound(g, fill, r, this.S(10f));
        TextRenderer.DrawText(g, Text, Font, ClientRectangle, Enabled ? Color.White : Theme.Dim,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}

/// <summary>Сегментированный переключатель вкладок.</summary>
internal sealed class Tabs : PaintedControl
{
    private readonly string[] _items;
    private int _selected, _hover = -1;
    public event EventHandler? SelectedChanged;

    public Tabs(params string[] items)
    {
        _items = items;
        BackColor = Theme.Bg;
        Size = new Size(240, 36);
        Cursor = Cursors.Hand;
    }

    public int Selected
    {
        get => _selected;
        set { if (_selected == value) return; _selected = value; Invalidate(); SelectedChanged?.Invoke(this, EventArgs.Empty); }
    }

    private int IndexAt(int x) => Math.Clamp(x * _items.Length / Math.Max(1, Width), 0, _items.Length - 1);

    protected override void OnMouseMove(MouseEventArgs e)
    {
        int i = IndexAt(e.X);
        if (i != _hover) { _hover = i; Invalidate(); }
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e) { _hover = -1; base.OnMouseLeave(e); }
    protected override void OnMouseClick(MouseEventArgs e) { Selected = IndexAt(e.X); base.OnMouseClick(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics.Smooth();
        g.Clear(BackColor);
        Theme.FillRound(g, Theme.Card, new RectangleF(0, 0, Width - 1, Height - 1), this.S(10f));
        float w = (Width - this.S(8f)) / _items.Length;
        for (int i = 0; i < _items.Length; i++)
        {
            var r = new RectangleF(this.S(4f) + i * w, this.S(4f), w, Height - this.S(8f) - 1);
            if (i == _selected) Theme.FillRound(g, Theme.Input, r, this.S(7f));
            Color c = i == _selected ? Theme.Text : i == _hover ? Theme.Text : Theme.Dim;
            TextRenderer.DrawText(g, _items[i], i == _selected ? Theme.RowTitle : Theme.Body, Rectangle.Round(r), c,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }
}

/// <summary>Кольцевой индикатор загрузки памяти.</summary>
internal sealed class RingGauge : PaintedControl
{
    private float _shown = -1;
    private int _target;
    private readonly System.Windows.Forms.Timer _anim = new() { Interval = 15 };

    public int Threshold { get; set; } = 85;
    public string Caption { get; set; } = "";

    public RingGauge()
    {
        _anim.Tick += (_, _) =>
        {
            float diff = _target - _shown;
            if (Math.Abs(diff) < 0.2f) { _shown = _target; _anim.Stop(); }
            else _shown += diff * 0.18f;
            Invalidate();
        };
    }

    public int Percent
    {
        get => _target;
        set
        {
            _target = Math.Clamp(value, 0, 100);
            if (_shown < 0) { _shown = _target; Invalidate(); }
            else if (!_anim.Enabled) _anim.Start();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _anim.Dispose();
        base.Dispose(disposing);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics.Smooth();
        g.Clear(BackColor);
        float size = Math.Min(Width, Height) - this.S(4f);
        if (size <= 20) return;
        float thick = Math.Max(this.S(10f), size * 0.075f);
        var r = new RectangleF((Width - size) / 2 + thick / 2, (Height - size) / 2 + thick / 2, size - thick, size - thick);

        using (var track = new Pen(Theme.Input, thick))
            g.DrawArc(track, r, 135, 270);

        float shown = Math.Max(0, _shown);
        if (shown > 0.5f)
        {
            using var arc = new Pen(Theme.LoadColor((int)Math.Round(shown), Threshold), thick)
            { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
            g.DrawArc(arc, r, 135, 270 * shown / 100f);
        }

        // Отметка порога автоочистки на кольце.
        if (Threshold is > 0 and < 100)
        {
            double a = (135 + 270 * Threshold / 100.0) * Math.PI / 180;
            float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2, rad = r.Width / 2;
            float inner = rad - thick / 2, outer = rad + thick / 2;
            using var tick = new Pen(BackColor, this.S(3f));
            g.DrawLine(tick, cx + inner * (float)Math.Cos(a), cy + inner * (float)Math.Sin(a),
                             cx + outer * (float)Math.Cos(a), cy + outer * (float)Math.Sin(a));
        }

        using var big = new Font(Theme.FontName, Math.Max(12f, size * 0.2f / (DeviceDpi / 72f)), FontStyle.Bold);
        string pct = $"{(int)Math.Round(shown)}%";
        var bigSize = TextRenderer.MeasureText(pct, big);
        var center = new Point(Width / 2, Height / 2);
        TextRenderer.DrawText(g, pct, big, new Point(center.X - bigSize.Width / 2, center.Y - bigSize.Height / 2 - this.S(8)), Theme.Text);
        if (Caption.Length > 0)
        {
            var capSize = TextRenderer.MeasureText(Caption, Theme.Small);
            TextRenderer.DrawText(g, Caption, Theme.Small,
                new Point(center.X - capSize.Width / 2, center.Y + bigSize.Height / 2 - this.S(8)), Theme.Dim);
        }
    }
}

/// <summary>Плитка показателя: подпись, крупное значение, мини-полоска.</summary>
internal sealed class StatTile : PaintedControl
{
    public string Caption { get; set; } = "";
    public string ValueText { get; set; } = "—";
    public string Sub { get; set; } = "";
    public float Fraction { get; set; } = -1;   // < 0 — полоски нет
    public Color BarColor { get; set; } = Theme.Accent;

    public StatTile() { BackColor = Theme.Input; }

    public void Set(string value, string sub, float fraction = -1, Color? bar = null)
    {
        ValueText = value; Sub = sub; Fraction = fraction;
        if (bar.HasValue) BarColor = bar.Value;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics.Smooth();
        g.Clear(Parent?.BackColor ?? Theme.Card);
        Theme.FillRound(g, BackColor, new RectangleF(0, 0, Width - 1, Height - 1), this.S(10f));

        int pad = this.S(14);
        int y = pad;
        TextRenderer.DrawText(g, Caption.ToUpperInvariant(), Theme.Label, new Point(pad, y), Theme.Dim);
        y += TextRenderer.MeasureText(Caption, Theme.Label).Height + this.S(4);
        TextRenderer.DrawText(g, ValueText, Theme.Value, new Point(pad - this.S(2), y), Theme.Text);
        y += TextRenderer.MeasureText(ValueText, Theme.Value).Height + this.S(2);
        TextRenderer.DrawText(g, Sub, Theme.Small, new Rectangle(pad, y, Width - 2 * pad, this.S(18)), Theme.Faint,
            TextFormatFlags.EndEllipsis);

        if (Fraction >= 0)
        {
            float bh = this.S(4f);
            var track = new RectangleF(pad, Height - pad - bh, Width - 2 * pad, bh);
            Theme.FillRound(g, Theme.Off, track, bh / 2);
            if (Fraction > 0)
                Theme.FillRound(g, BarColor, new RectangleF(track.X, track.Y, Math.Max(bh, track.Width * Math.Min(1, Fraction)), bh), bh / 2);
        }
    }
}

/// <summary>Поле, в котором нажатием задаётся сочетание клавиш.</summary>
internal sealed class HotkeyBox : PaintedControl
{
    public event Action<Keys>? HotkeyPicked;
    public Keys Keys { get; set; }

    public HotkeyBox()
    {
        Size = new Size(170, 32);
        TabStop = true;
        Cursor = Cursors.Hand;
    }

    public static string Describe(Keys k)
    {
        if ((k & Keys.KeyCode) == Keys.None) return "не задана";
        var parts = new List<string>();
        if (k.HasFlag(Keys.Control)) parts.Add("Ctrl");
        if (k.HasFlag(Keys.Shift)) parts.Add("Shift");
        if (k.HasFlag(Keys.Alt)) parts.Add("Alt");
        parts.Add((k & Keys.KeyCode).ToString());
        return string.Join(" + ", parts);
    }

    protected override void OnClick(EventArgs e) { Focus(); base.OnClick(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    protected override void OnPreviewKeyDown(PreviewKeyDownEventArgs e) { e.IsInputKey = true; base.OnPreviewKeyDown(e); }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        // Перехватываем всё, включая Tab и F10, пока поле в фокусе.
        OnKeyDown(new KeyEventArgs(keyData));
        return true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        e.SuppressKeyPress = true;
        Keys key = e.KeyCode;
        if (key is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin) return;
        Keys result = key is Keys.Back or Keys.Delete or Keys.Escape ? Keys.None : e.Modifiers | key;
        Keys = result;
        Invalidate();
        Parent?.SelectNextControl(this, true, true, true, true);
        HotkeyPicked?.Invoke(result);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics.Smooth();
        g.Clear(BackColor);
        var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        Theme.FillRound(g, Theme.Input, r, this.S(8f));
        if (Focused)
            using (var pen = new Pen(Theme.Accent, 1.5f))
            using (var path = Theme.Round(r, this.S(8f)))
                g.DrawPath(pen, path);
        string text = Focused ? "Нажмите сочетание…" : Describe(Keys);
        TextRenderer.DrawText(g, text, Theme.Body, ClientRectangle, Focused ? Theme.Dim : Theme.Text,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
}

/// <summary>Однострочное текстовое поле в тёмной скруглённой рамке.</summary>
internal sealed class InputBox : Panel
{
    public TextBox Box { get; } = new()
    {
        BorderStyle = BorderStyle.None,
        BackColor = Theme.Input,
        ForeColor = Theme.Text,
        Font = Theme.Body,
    };

    public InputBox()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Card;
        Height = 38;
        Controls.Add(Box);
        Box.GotFocus += (_, _) => Invalidate();
        Box.LostFocus += (_, _) => Invalidate();
        Cursor = Cursors.IBeam;
        Click += (_, _) => Box.Focus();
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        int pad = this.S(12);
        Box.SetBounds(pad, (Height - Box.PreferredHeight) / 2, Width - 2 * pad, Box.PreferredHeight);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics.Smooth();
        g.Clear(Parent?.BackColor ?? Theme.Card);
        var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        Theme.FillRound(g, Theme.Input, r, this.S(8f));
        if (Box.Focused)
            using (var pen = new Pen(Theme.Accent, 1.5f))
            using (var path = Theme.Round(r, this.S(8f)))
                g.DrawPath(pen, path);
    }
}
