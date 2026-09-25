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
        BackColor = Color.FromArgb(24, 26, 30);
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
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        int w = ClientSize.Width, h = ClientSize.Height;
        g.Clear(BackColor);

        using (var grid = new Pen(Color.FromArgb(45, 48, 55)))
            for (int i = 1; i < 4; i++)
                g.DrawLine(grid, 0, h * i / 4, w, h * i / 4);

        if (Threshold > 0)
        {
            using var th = new Pen(Color.FromArgb(160, 230, 160, 60)) { DashStyle = DashStyle.Dash };
            float y = h - h * Threshold / 100f;
            g.DrawLine(th, 0, y, w, y);
        }

        var pts = _points.ToArray();
        if (pts.Length < 2) return;

        float step = (float)w / (Capacity - 1);
        float x0 = w - step * (pts.Length - 1);
        var line = new PointF[pts.Length];
        for (int i = 0; i < pts.Length; i++)
            line[i] = new PointF(x0 + i * step, h - 1 - (h - 2) * pts[i].Load / 100f);

        using (var path = new GraphicsPath())
        {
            path.AddLines(line);
            path.AddLine(line[^1], new PointF(line[^1].X, h));
            path.AddLine(new PointF(line[^1].X, h), new PointF(line[0].X, h));
            using var fill = new LinearGradientBrush(new Rectangle(0, 0, w, h),
                Color.FromArgb(110, 70, 160, 255), Color.FromArgb(10, 70, 160, 255), 90f);
            g.FillPath(fill, path);
        }
        using (var pen = new Pen(Color.FromArgb(90, 175, 255), 1.6f))
            g.DrawLines(pen, line);

        using var mark = new Pen(Color.FromArgb(120, 220, 120), 1f) { DashStyle = DashStyle.Dot };
        for (int i = 0; i < pts.Length; i++)
            if (pts[i].Cleaned)
                g.DrawLine(mark, line[i].X, 0, line[i].X, h);
    }
}
