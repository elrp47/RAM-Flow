using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace RAMFlow;

/// <summary>Палитра, шрифты и общие хелперы отрисовки.</summary>
internal static class Theme
{
    public static readonly Color Bg = Color.FromArgb(13, 15, 20);
    public static readonly Color Card = Color.FromArgb(22, 25, 32);
    public static readonly Color CardHover = Color.FromArgb(28, 32, 41);
    public static readonly Color Input = Color.FromArgb(31, 35, 45);
    public static readonly Color Line = Color.FromArgb(36, 41, 52);
    public static readonly Color Text = Color.FromArgb(236, 239, 244);
    public static readonly Color Dim = Color.FromArgb(146, 153, 168);
    public static readonly Color Faint = Color.FromArgb(96, 103, 118);
    public static readonly Color Accent = Color.FromArgb(76, 141, 255);
    public static readonly Color AccentHover = Color.FromArgb(102, 160, 255);
    public static readonly Color AccentDown = Color.FromArgb(58, 118, 225);
    public static readonly Color Green = Color.FromArgb(52, 199, 122);
    public static readonly Color Amber = Color.FromArgb(245, 166, 35);
    public static readonly Color Red = Color.FromArgb(240, 82, 82);
    public static readonly Color Off = Color.FromArgb(58, 64, 78);

    public const string FontName = "Segoe UI";
    public static readonly Font Body = new(FontName, 9.75f);
    public static readonly Font Small = new(FontName, 8.5f);
    public static readonly Font Label = new(FontName, 8f, FontStyle.Bold);
    public static readonly Font RowTitle = new(FontName, 10f, FontStyle.Bold);
    public static readonly Font Value = new("Segoe UI Semibold", 15f);
    public static readonly Font Title = new("Segoe UI Semibold", 16f);
    public static readonly Font Button = new("Segoe UI Semibold", 10.5f);

    /// <summary>Цвет загрузки: синий → жёлтый → красный относительно порога.</summary>
    public static Color LoadColor(int percent, int threshold) =>
        percent >= threshold ? Red : percent >= threshold - 15 ? Amber : Accent;

    public static GraphicsPath Round(RectangleF r, float radius)
    {
        var p = new GraphicsPath();
        float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        if (d <= 0) { p.AddRectangle(r); return p; }
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public static void FillRound(Graphics g, Color c, RectangleF r, float radius)
    {
        using var path = Round(r, radius);
        using var b = new SolidBrush(c);
        g.FillPath(b, path);
    }

    public static Graphics Smooth(this Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        return g;
    }

    /// <summary>Пиксели макета (96 DPI) → пиксели текущего монитора.</summary>
    public static int S(this Control c, int px) => (int)Math.Round(px * c.DeviceDpi / 96f);
    public static float S(this Control c, float px) => px * c.DeviceDpi / 96f;

    // ── тёмный заголовок окна и системные скроллбары ─────────────────────
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hwnd, string? app, string? idList);

    public static void DarkTitleBar(IntPtr hwnd)
    {
        int on = 1;
        if (DwmSetWindowAttribute(hwnd, 20, ref on, 4) != 0)   // Windows 10 20H1+
            DwmSetWindowAttribute(hwnd, 19, ref on, 4);        // Windows 10 1809–1909
        int caption = ColorTranslator.ToWin32(Bg);
        DwmSetWindowAttribute(hwnd, 35, ref caption, 4);       // Windows 11: цвет заголовка
    }

    public static void DarkScrollbars(Control c)
    {
        if (c.IsHandleCreated) SetWindowTheme(c.Handle, "DarkMode_Explorer", null);
        else c.HandleCreated += (_, _) => SetWindowTheme(c.Handle, "DarkMode_Explorer", null);
    }
}

/// <summary>Тёмное контекстное меню трея.</summary>
internal sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
{
    public DarkMenuRenderer() : base(new Colors()) { RoundedEdges = false; }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? Theme.Text : Theme.Faint;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        var g = e.Graphics.Smooth();
        var r = e.ImageRectangle;
        r.Inflate(-1, -1);
        Theme.FillRound(g, Theme.Accent, r, 4);
        using var pen = new Pen(Color.White, 2f);
        g.DrawLines(pen, new[]
        {
            new PointF(r.Left + r.Width * 0.25f, r.Top + r.Height * 0.52f),
            new PointF(r.Left + r.Width * 0.43f, r.Top + r.Height * 0.70f),
            new PointF(r.Left + r.Width * 0.76f, r.Top + r.Height * 0.32f),
        });
    }

    private sealed class Colors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Theme.Card;
        public override Color ImageMarginGradientBegin => Theme.Card;
        public override Color ImageMarginGradientMiddle => Theme.Card;
        public override Color ImageMarginGradientEnd => Theme.Card;
        public override Color MenuBorder => Theme.Line;
        public override Color MenuItemBorder => Theme.CardHover;
        public override Color MenuItemSelected => Theme.CardHover;
        public override Color MenuItemSelectedGradientBegin => Theme.CardHover;
        public override Color MenuItemSelectedGradientEnd => Theme.CardHover;
        public override Color SeparatorDark => Theme.Line;
        public override Color SeparatorLight => Theme.Card;
        public override Color CheckBackground => Theme.Card;
        public override Color CheckSelectedBackground => Theme.CardHover;
        public override Color CheckPressedBackground => Theme.CardHover;
    }
}
