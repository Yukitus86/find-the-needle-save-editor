using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FindTheNeedleEditor.Core;

namespace FindTheNeedleEditor;

/// <summary>Top-down view of the yard: hay height map, needles, buildings, player and teleport target.
/// World x runs right, world z runs down; the map covers [-extent, extent] on both axes.</summary>
public sealed class PileMap : FrameworkElement
{
    SaveDoc? doc;
    WriteableBitmap? bitmap;
    bool bitmapIsDome;
    Point? hover;

    public bool ShowDome { get; set; }
    public bool ShowBuildings { get; set; } = true;
    public bool ShowTaken { get; set; } = true;
    public (double X, double Z)? Target { get; set; }

    /// <summary>Raised with world x/z on click; the bool is true for a double-click.</summary>
    public event Action<double, double, bool>? MapClicked;
    public event Action<string>? HoverText;

    static readonly Brush FloorBrush = Frozen(new SolidColorBrush(Color.FromRgb(0x10, 0x18, 0x1B)));
    static readonly Pen GridPen = FrozenPen(Color.FromArgb(0x30, 0x8F, 0xA3, 0xA8), 1);
    static readonly Pen AxisPen = FrozenPen(Color.FromArgb(0x55, 0x8F, 0xA3, 0xA8), 1);
    static readonly Pen BeltPen = FrozenPen(Color.FromArgb(0xB0, 0x7D, 0x8B, 0x91), 2.2);
    static readonly Brush NeedleBrush = Frozen(new SolidColorBrush(Color.FromRgb(0xF8, 0x55, 0x55)));
    static readonly Pen NeedleRing = FrozenPen(Color.FromArgb(0x90, 0xF8, 0x71, 0x71), 1.5);
    static readonly Pen TakenPen = FrozenPen(Color.FromRgb(0x9C, 0xA8, 0xAD), 1.8);
    static readonly Pen WhitePen = FrozenPen(Colors.White, 1.5);
    static readonly Brush PlayerBrush = Frozen(new SolidColorBrush(Colors.White));
    static readonly Pen TargetPen = FrozenPen(Color.FromRgb(0x2D, 0xD4, 0xBF), 2);
    static readonly Typeface LabelFace = new("Segoe UI Semibold");

    static Brush Frozen(Brush b) { b.Freeze(); return b; }
    static Pen FrozenPen(Color c, double w) { var p = new Pen(new SolidColorBrush(c), w); p.Freeze(); return p; }

    public PileMap()
    {
        ClipToBounds = true;
        Cursor = Cursors.Cross;
        SnapsToDevicePixels = true;
    }

    double viewExt = 15;
    double zoom = 1, cx, cz;      // zoom factor and view centre (world)
    Point? panFrom;

    public void SetDoc(SaveDoc? d)
    {
        doc = d;
        viewExt = ComputeViewExtent(d);
        zoom = 1; cx = cz = 0;
        bitmap = null;
        Target = null;
        InvalidateVisual();
    }

    /// <summary>Call after heights/flags changed.</summary>
    public void Refresh(bool rebuildBitmap = false)
    {
        if (rebuildBitmap) bitmap = null;
        InvalidateVisual();
    }

    double MapSize => Math.Max(10, Math.Min(ActualWidth, ActualHeight));
    double Ext => viewExt;

    /// <summary>Pile extent, widened so that buildings, props and the player outside the pile stay visible.</summary>
    static double ComputeViewExtent(SaveDoc? d)
    {
        if (d is null) return 15;
        double e = d.Extent;
        void Take(double x, double z) { if (double.IsFinite(x) && double.IsFinite(z)) e = Math.Max(e, Math.Max(Math.Abs(x), Math.Abs(z))); }
        foreach (var o in d.Buildings?.Items ?? [])
            if (o is GDict b)
                foreach (var k in new[] { "position", "a", "b" })
                    if (b[k] is GMath m && m.V.Length >= 3) Take(m.V[0], m.V[2]);
        foreach (var o in d.Props?.Items ?? [])
            if (o is GDict p && p["xform"] is GMath t && t.V.Length == 12) Take(t.V[9], t.V[11]);
        var (px, _, pz) = d.PlayerPos;
        Take(px, pz);
        return Math.Min(200, Math.Ceiling((e + 1.5) / 5) * 5);
    }

    double Half => Ext / zoom;
    Point ToScreen(double x, double z) => new((x - cx + Half) / (2 * Half) * MapSize, (z - cz + Half) / (2 * Half) * MapSize);
    (double X, double Z) ToWorld(Point p) => (p.X / MapSize * 2 * Half - Half + cx, p.Y / MapSize * 2 * Half - Half + cz);

    public void ResetZoom() { zoom = 1; cx = cz = 0; InvalidateVisual(); }

    public void SetZoom(double z, double centreX, double centreZ) { zoom = Math.Clamp(z, 1, 10); cx = centreX; cz = centreZ; ClampCentre(); InvalidateVisual(); }

    void ClampCentre()
    {
        double lim = Ext - Half;
        cx = Math.Clamp(cx, -lim, lim);
        cz = Math.Clamp(cz, -lim, lim);
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        if (doc is null) return;
        var p = e.GetPosition(this);
        var (wx, wz) = ToWorld(p);
        zoom = Math.Clamp(zoom * (e.Delta > 0 ? 1.25 : 0.8), 1, 10);
        cx = wx - (p.X / MapSize * 2 * Half - Half);
        cz = wz - (p.Y / MapSize * 2 * Half - Half);
        ClampCentre();
        e.Handled = true;
        InvalidateVisual();
    }

    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonDown(e);
        panFrom = e.GetPosition(this);
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonUp(e);
        panFrom = null;
        ReleaseMouseCapture();
        e.Handled = true;
    }

    static Color HayColor(double t)
    {
        // dark olive -> straw -> pale gold
        t = Math.Clamp(t, 0, 1);
        (double r, double g, double b) a = (0x3B, 0x3A, 0x1E), m = (0xC8, 0x95, 0x2E), e = (0xFF, 0xE7, 0x9A);
        var (p, q, u) = t < 0.55 ? (a, m, t / 0.55) : (m, e, (t - 0.55) / 0.45);
        return Color.FromRgb((byte)(p.r + (q.r - p.r) * u), (byte)(p.g + (q.g - p.g) * u), (byte)(p.b + (q.b - p.b) * u));
    }

    void BuildBitmap()
    {
        bitmap = null;
        if (doc is null) return;
        var field = ShowDome ? doc.Dome ?? doc.Heights : doc.Heights;
        int n = doc.GridSize;
        if (field is null || n == 0 || field.Reals.Count < n * n) return;
        double max = 0.01;
        foreach (var h in (doc.Dome ?? field).Reals) max = Math.Max(max, h);
        var px = new byte[n * n * 4];
        for (int i = 0; i < n * n; i++)
        {
            double h = field.Reals[i];
            if (h <= 0.001) continue; // transparent: floor shows through
            var c = HayColor(h / max);
            px[i * 4] = c.B; px[i * 4 + 1] = c.G; px[i * 4 + 2] = c.R;
            px[i * 4 + 3] = (byte)Math.Clamp(150 + h / max * 105, 0, 255);
        }
        var bmp = new WriteableBitmap(n, n, 96, 96, PixelFormats.Bgra32, null);
        bmp.WritePixels(new Int32Rect(0, 0, n, n), px, n * 4, 0);
        bmp.Freeze();
        bitmap = bmp;
        bitmapIsDome = ShowDome;
    }

    static Color BuildingColor(string type) => type switch
    {
        "hay_generator" => Color.FromRgb(0xFB, 0x92, 0x3C),
        "power_pole" => Color.FromRgb(0xFB, 0xBF, 0x24),
        "robotic_arm" => Color.FromRgb(0x60, 0xA5, 0xFA),
        "haystack_scanner" => Color.FromRgb(0x2D, 0xD4, 0xBF),
        "hay_drone" => Color.FromRgb(0xC0, 0x84, 0xFC),
        "hay_compressor" or "hay_wrapper" => Color.FromRgb(0x34, 0xD3, 0x99),
        "hay_silo" => Color.FromRgb(0xA3, 0xE6, 0x35),
        "needle_cabinet" => Color.FromRgb(0xF4, 0x72, 0xB6),
        _ => Color.FromRgb(0xCB, 0xD5, 0xE1),
    };

    protected override void OnRender(DrawingContext dc)
    {
        double s = MapSize;
        dc.DrawRoundedRectangle(FloorBrush, null, new Rect(0, 0, s, s), 10, 10);
        if (doc is null) return;
        if (bitmap is null || bitmapIsDome != ShowDome) BuildBitmap();

        // 5 m grid, axes stronger
        for (int m = -(int)Ext; m <= (int)Ext; m++)
        {
            if (m % 5 != 0) continue;
            var pen = m == 0 ? AxisPen : GridPen;
            var a = ToScreen(m, -Ext); var b = ToScreen(m, Ext);
            dc.DrawLine(pen, a, b);
            a = ToScreen(-Ext, m); b = ToScreen(Ext, m);
            dc.DrawLine(pen, a, b);
        }

        if (bitmap is not null)
        {
            // cell centres sit on the grid points: widen by half a cell on each side
            double half = doc.Cell / 2, pe = doc.Extent;
            var tl = ToScreen(-pe - half, -pe - half); var br = ToScreen(pe + half, pe + half);
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.Linear);
            dc.DrawImage(bitmap, new Rect(tl, br));
        }

        if (ShowBuildings && doc.Buildings is { } list)
        {
            double r = Math.Max(2.5, s / 160);
            foreach (var o in list.Items)
            {
                if (o is not GDict b) continue;
                var type = b.Str("type") ?? "";
                if (b["a"] is GMath a && b["b"] is GMath bb && a.V.Length >= 3)
                {
                    dc.DrawLine(BeltPen, ToScreen(a.V[0], a.V[2]), ToScreen(bb.V[0], bb.V[2]));
                    continue;
                }
                if (b["position"] is GMath p && p.V.Length >= 3)
                {
                    var c = ToScreen(p.V[0], p.V[2]);
                    var brush = new SolidColorBrush(BuildingColor(type)); brush.Freeze();
                    dc.DrawRoundedRectangle(brush, null, new Rect(c.X - r, c.Y - r, 2 * r, 2 * r), 2, 2);
                }
            }
        }

        // needles
        double nr = Math.Max(5, s / 75);
        for (int i = 0; i < doc.PileNeedleCount; i++)
        {
            var (x, _, z) = doc.NeedlePos(i);
            var c = ToScreen(x, z);
            bool taken = doc.IsNeedleTaken(i);
            if (taken)
            {
                if (!ShowTaken) continue;
                dc.DrawEllipse(null, TakenPen, c, nr * 0.75, nr * 0.75);
                dc.DrawLine(TakenPen, new Point(c.X - nr * 0.45, c.Y), new Point(c.X - nr * 0.1, c.Y + nr * 0.4));
                dc.DrawLine(TakenPen, new Point(c.X - nr * 0.1, c.Y + nr * 0.4), new Point(c.X + nr * 0.5, c.Y - nr * 0.4));
            }
            else
            {
                dc.DrawEllipse(null, NeedleRing, c, nr * 1.8, nr * 1.8);
                dc.DrawEllipse(NeedleBrush, WhitePen, c, nr * 0.75, nr * 0.75);
            }
            var ft = new FormattedText((i + 1).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, LabelFace, Math.Max(10, nr * 1.3), taken ? TakenPen.Brush : Brushes.White, 1.0);
            dc.DrawText(ft, new Point(c.X + nr * 1.1, c.Y - nr * 2.2));
        }

        // player
        {
            var (px, _, pz) = doc.PlayerPos;
            var (fx, fz) = doc.PlayerForward;
            double len = Math.Sqrt(fx * fx + fz * fz);
            if (len < 1e-6) { fx = 0; fz = 1; len = 1; }
            fx /= len; fz /= len;
            var c = ToScreen(px, pz);
            double size = Math.Max(8, s / 45);
            var tip = new Point(c.X + fx * size, c.Y + fz * size);
            var l = new Point(c.X - fx * size * 0.6 + fz * size * 0.55, c.Y - fz * size * 0.6 - fx * size * 0.55);
            var rr = new Point(c.X - fx * size * 0.6 - fz * size * 0.55, c.Y - fz * size * 0.6 + fx * size * 0.55);
            var g = new StreamGeometry();
            using (var ctx = g.Open()) { ctx.BeginFigure(tip, true, true); ctx.LineTo(l, true, false); ctx.LineTo(rr, true, false); }
            g.Freeze();
            dc.DrawGeometry(PlayerBrush, new Pen(Brushes.Black, 1), g);
        }

        if (Target is { } t)
        {
            var c = ToScreen(t.X, t.Z);
            double tr = Math.Max(7, s / 60);
            dc.DrawEllipse(null, TargetPen, c, tr, tr);
            dc.DrawLine(TargetPen, new Point(c.X - tr * 1.6, c.Y), new Point(c.X - tr * 0.5, c.Y));
            dc.DrawLine(TargetPen, new Point(c.X + tr * 0.5, c.Y), new Point(c.X + tr * 1.6, c.Y));
            dc.DrawLine(TargetPen, new Point(c.X, c.Y - tr * 1.6), new Point(c.X, c.Y - tr * 0.5));
            dc.DrawLine(TargetPen, new Point(c.X, c.Y + tr * 0.5), new Point(c.X, c.Y + tr * 1.6));
        }

        if (hover is { } h && h.X <= s && h.Y <= s)
        {
            var pen = FrozenPen(Color.FromArgb(0x60, 0xFF, 0xFF, 0xFF), 1);
            dc.DrawLine(pen, new Point(h.X, 0), new Point(h.X, s));
            dc.DrawLine(pen, new Point(0, h.Y), new Point(s, h.Y));
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (doc is null) return;
        var p = e.GetPosition(this);
        if (panFrom is { } from && e.RightButton == MouseButtonState.Pressed)
        {
            cx -= (p.X - from.X) / MapSize * 2 * Half;
            cz -= (p.Y - from.Y) / MapSize * 2 * Half;
            ClampCentre();
            panFrom = p;
        }
        if (p.X > MapSize || p.Y > MapSize) { hover = null; HoverText?.Invoke(""); InvalidateVisual(); return; }
        hover = p;
        var (x, z) = ToWorld(p);
        HoverText?.Invoke(Loc.T("Hover", x, z, doc.HeightAt(x, z, ShowDome ? doc.Dome : null)));
        InvalidateVisual();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        hover = null;
        HoverText?.Invoke("");
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (doc is null) return;
        var p = e.GetPosition(this);
        if (p.X > MapSize || p.Y > MapSize) return;
        var (x, z) = ToWorld(p);
        Target = (x, z);
        InvalidateVisual();
        MapClicked?.Invoke(x, z, e.ClickCount >= 2);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double s = Math.Min(double.IsInfinity(availableSize.Width) ? 520 : availableSize.Width,
                            double.IsInfinity(availableSize.Height) ? 520 : availableSize.Height);
        return new Size(s, s);
    }
}
