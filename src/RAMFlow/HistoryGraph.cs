using System.Drawing.Drawing2D;

namespace RAMFlow;

/// <summary>График загрузки памяти за последние N замеров с отметками очисток.</summary>
public sealed class HistoryGraph : Control
{
    private const int Capacity = 180;
    private readonly Queue<(int Load, bool Cleaned)> _points = new();
    private bool _cleanPending;

    public int Threshold { get; set; }

    public HistoryGraph()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Card;
    }

    public void Add(int load)
    {
        _points.Enqueue((load, _cleanPending));
        _cleanPending = false;
        while (_points.Count > Capacity) _points.Dequeue();
        Invalidate();
    }

    public void MarkCleanup() => _cleanPending = true;

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics.Smooth();
        g.Clear(BackColor);

        // Слева подписи шкалы, справа — сам график.
        int labelW = this.S(48);
        var area = new RectangleF(labelW, this.S(6), Width - labelW - 1, Height - this.S(12));
        if (area.Width < 10 || area.Height < 10) return;

        float Y(float load) => area.Bottom - area.Height * load / 100f;

        using (var grid = new Pen(Theme.Line))
        {
            foreach (int v in new[] { 0, 25, 50, 75, 100 })
            {
                float y = Y(v);
                g.DrawLine(grid, area.Left, y, area.Right, y);
                if (v % 50 == 0)
                    TextRenderer.DrawText(g, v + "%", Theme.Small, new Rectangle(0, (int)y - this.S(8), labelW - this.S(8), this.S(16)),
                        Theme.Faint, TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
            }
        }

        if (Threshold > 0)
        {
            using var th = new Pen(Color.FromArgb(170, Theme.Amber), 1.2f) { DashStyle = DashStyle.Dash };
            g.DrawLine(th, area.Left, Y(Threshold), area.Right, Y(Threshold));
        }

        var pts = _points.ToArray();
        if (pts.Length < 2) return;

        float step = area.Width / (Capacity - 1);
        float x0 = area.Right - step * (pts.Length - 1);
        var line = new PointF[pts.Length];
        for (int i = 0; i < pts.Length; i++)
            line[i] = new PointF(x0 + i * step, Y(pts[i].Load));

        using (var mark = new Pen(Color.FromArgb(150, Theme.Green), 1.2f) { DashStyle = DashStyle.Dot })
            for (int i = 0; i < pts.Length; i++)
                if (pts[i].Cleaned)
                    g.DrawLine(mark, line[i].X, area.Top, line[i].X, area.Bottom);

        using (var path = new GraphicsPath())
        {
            path.AddLines(line);
            path.AddLine(line[^1], new PointF(line[^1].X, area.Bottom));
            path.AddLine(new PointF(line[^1].X, area.Bottom), new PointF(line[0].X, area.Bottom));
            using var fill = new LinearGradientBrush(area,
                Color.FromArgb(90, Theme.Accent), Color.FromArgb(0, Theme.Accent), 90f);
            g.FillPath(fill, path);
        }
        using (var pen = new Pen(Theme.Accent, this.S(2f)) { LineJoin = LineJoin.Round })
            g.DrawLines(pen, line);

        // Точка текущего значения.
        var last = line[^1];
        float d = this.S(8f);
        using (var halo = new SolidBrush(Color.FromArgb(60, Theme.Accent)))
            g.FillEllipse(halo, last.X - d, last.Y - d, d * 2, d * 2);
        using (var dot = new SolidBrush(Theme.Accent))
            g.FillEllipse(dot, last.X - d / 2, last.Y - d / 2, d, d);
    }
}
