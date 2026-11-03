using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using FindTheNeedleEditor.Core;

namespace FindTheNeedleEditor;

/// <summary>3D view of the yard: the hay pile as a height-field mesh, needles as red rods, buildings as blocks,
/// the player as a white pillar. Left drag orbits, wheel zooms.</summary>
public sealed class Pile3D : Border
{
    readonly Viewport3D view = new() { ClipToBounds = true };
    readonly PerspectiveCamera camera = new() { FieldOfView = 45, NearPlaneDistance = 0.1, FarPlaneDistance = 500 };
    readonly ModelVisual3D scene = new();
    SaveDoc? doc;
    double yaw = -35, pitch = 32, dist = 26;
    Point? drag;

    public bool XRay { get; set; }
    public bool ShowBuildings { get; set; } = true;
    public bool ShowTaken { get; set; } = true;

    public Pile3D()
    {
        Background = new LinearGradientBrush(Color.FromRgb(0x16, 0x22, 0x27), Color.FromRgb(0x0A, 0x0F, 0x12), 90);
        CornerRadius = new CornerRadius(10);
        ClipToBounds = true;
        view.Camera = camera;
        view.Children.Add(scene);
        Child = view;
        Cursor = Cursors.SizeAll;
        UpdateCamera();
    }

    public void SetDoc(SaveDoc? d)
    {
        doc = d;
        Rebuild();
    }

    public void SetCamera(double yawDeg, double pitchDeg, double distance) { yaw = yawDeg; pitch = pitchDeg; dist = distance; UpdateCamera(); }

    void UpdateCamera()
    {
        double y = yaw * Math.PI / 180, p = pitch * Math.PI / 180;
        var target = new Point3D(0, 1.2, 0);
        var pos = new Point3D(target.X + dist * Math.Cos(p) * Math.Sin(y), target.Y + dist * Math.Sin(p), target.Z + dist * Math.Cos(p) * Math.Cos(y));
        camera.Position = pos;
        camera.LookDirection = target - pos;
        camera.UpDirection = new Vector3D(0, 1, 0);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e) { drag = e.GetPosition(this); CaptureMouse(); e.Handled = true; }
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) { drag = null; ReleaseMouseCapture(); }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (drag is not { } from || e.LeftButton != MouseButtonState.Pressed) return;
        var p = e.GetPosition(this);
        yaw -= (p.X - from.X) * 0.4;
        pitch = Math.Clamp(pitch + (p.Y - from.Y) * 0.3, 5, 88);
        drag = p;
        UpdateCamera();
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        dist = Math.Clamp(dist * (e.Delta > 0 ? 0.88 : 1.14), 4, 120);
        UpdateCamera();
        e.Handled = true;
    }

    static Material Mat(Color c, double emissive = 0)
    {
        var g = new MaterialGroup();
        g.Children.Add(new DiffuseMaterial(new SolidColorBrush(c)));
        if (emissive > 0) g.Children.Add(new EmissiveMaterial(new SolidColorBrush(Color.FromArgb((byte)(255 * emissive), c.R, c.G, c.B))));
        g.Freeze();
        return g;
    }

    static GeometryModel3D Box(Point3D c, double sx, double sy, double sz, Material m, double yawRad = 0)
    {
        var mesh = new MeshGeometry3D();
        double hx = sx / 2, hy = sy / 2, hz = sz / 2;
        Point3D[] v =
        [
            new(-hx, -hy, -hz), new(hx, -hy, -hz), new(hx, hy, -hz), new(-hx, hy, -hz),
            new(-hx, -hy, hz), new(hx, -hy, hz), new(hx, hy, hz), new(-hx, hy, hz),
        ];
        int[][] faces = [[0, 3, 2, 1], [4, 5, 6, 7], [0, 1, 5, 4], [3, 7, 6, 2], [0, 4, 7, 3], [1, 2, 6, 5]];
        foreach (var f in faces)
        {
            int b = mesh.Positions.Count;
            foreach (var i in f) mesh.Positions.Add(v[i]);
            mesh.TriangleIndices.Add(b); mesh.TriangleIndices.Add(b + 1); mesh.TriangleIndices.Add(b + 2);
            mesh.TriangleIndices.Add(b); mesh.TriangleIndices.Add(b + 2); mesh.TriangleIndices.Add(b + 3);
        }
        var model = new GeometryModel3D(mesh, m) { BackMaterial = m };
        var t = new Transform3DGroup();
        if (yawRad != 0) t.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 1, 0), yawRad * 180 / Math.PI)));
        t.Children.Add(new TranslateTransform3D(c.X, c.Y, c.Z));
        model.Transform = t;
        return model;
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

    public void Rebuild()
    {
        var root = new Model3DGroup();
        root.Children.Add(new AmbientLight(Color.FromRgb(0x55, 0x5A, 0x60)));
        root.Children.Add(new DirectionalLight(Color.FromRgb(0xE8, 0xE2, 0xD0), new Vector3D(-0.5, -1, -0.35)));
        root.Children.Add(new DirectionalLight(Color.FromRgb(0x40, 0x50, 0x60), new Vector3D(0.6, -0.4, 0.8)));
        scene.Content = root;
        if (doc is null) return;

        double e = doc.Extent;
        double floor = Math.Max(e + 5, 20);
        // floor
        root.Children.Add(Box(new Point3D(0, -0.03, 0), floor * 2, 0.05, floor * 2, Mat(Color.FromRgb(0x1C, 0x26, 0x2A))));
        // grid lines every 5 m
        var gridMat = Mat(Color.FromRgb(0x2E, 0x3D, 0x43));
        for (double g = -floor; g <= floor + 0.01; g += 5)
        {
            root.Children.Add(Box(new Point3D(g, 0.0, 0), 0.03, 0.01, floor * 2, gridMat));
            root.Children.Add(Box(new Point3D(0, 0.0, g), floor * 2, 0.01, 0.03, gridMat));
        }

        var transparent = new List<Model3D>();

        // hay height field
        if (doc.Heights is { } hf && doc.GridSize > 1)
        {
            int n = doc.GridSize;
            double cell = doc.Cell, max = Math.Max(0.01, hf.Reals.Max());
            var mesh = new MeshGeometry3D();
            var index = new int[n * n];
            Array.Fill(index, -1);
            int Vert(int ix, int iz)
            {
                int k = iz * n + ix;
                if (index[k] >= 0) return index[k];
                double h = hf.Reals[k];
                index[k] = mesh.Positions.Count;
                mesh.Positions.Add(new Point3D(-e + ix * cell, h, -e + iz * cell));
                mesh.TextureCoordinates.Add(new Point(Math.Clamp(h / max, 0, 1), 0.5));
                return index[k];
            }
            for (int iz = 0; iz < n - 1; iz++)
                for (int ix = 0; ix < n - 1; ix++)
                {
                    int k = iz * n + ix;
                    if (hf.Reals[k] <= 0.001 && hf.Reals[k + 1] <= 0.001 && hf.Reals[k + n] <= 0.001 && hf.Reals[k + n + 1] <= 0.001) continue;
                    int a = Vert(ix, iz), b = Vert(ix + 1, iz), c = Vert(ix + 1, iz + 1), d = Vert(ix, iz + 1);
                    mesh.TriangleIndices.Add(a); mesh.TriangleIndices.Add(d); mesh.TriangleIndices.Add(c);
                    mesh.TriangleIndices.Add(a); mesh.TriangleIndices.Add(c); mesh.TriangleIndices.Add(b);
                }
            var grad = new LinearGradientBrush { StartPoint = new Point(0, 0.5), EndPoint = new Point(1, 0.5), Opacity = XRay ? 0.28 : 1 };
            grad.GradientStops.Add(new GradientStop(Color.FromRgb(0x6B, 0x5A, 0x22), 0));
            grad.GradientStops.Add(new GradientStop(Color.FromRgb(0xC8, 0x95, 0x2E), 0.5));
            grad.GradientStops.Add(new GradientStop(Color.FromRgb(0xFF, 0xE7, 0x9A), 1));
            var hayMat = new DiffuseMaterial(grad);
            var hay = new GeometryModel3D(mesh, hayMat) { BackMaterial = hayMat };
            if (XRay) transparent.Add(hay); else root.Children.Add(hay);
        }

        // needles
        var hidden = Mat(Color.FromRgb(0xF8, 0x44, 0x44), 0.85);
        var found = Mat(Color.FromRgb(0x9C, 0xA8, 0xAD), 0.2);
        var beacon = Mat(Color.FromRgb(0xFF, 0x70, 0x70), 1);
        for (int i = 0; i < doc.PileNeedleCount; i++)
        {
            bool taken = doc.IsNeedleTaken(i);
            if (taken && !ShowTaken) continue;
            var (x, y, z) = doc.NeedlePos(i);
            root.Children.Add(Box(new Point3D(x, y, z), 0.09, 0.09, 1.0, taken ? found : hidden, i * 0.9));
            if (taken) continue;
            root.Children.Add(Box(new Point3D(x, y, z), 0.3, 0.3, 0.3, hidden)); // glowing knot
            // beacon: thin red pole from the needle up through the hay, so buried needles are easy to spot
            double top = Math.Max(y, doc.HeightAt(x, z)) + 1.5;
            root.Children.Add(Box(new Point3D(x, (y + top) / 2, z), 0.035, top - y, 0.035, beacon));
            root.Children.Add(Box(new Point3D(x, top, z), 0.18, 0.18, 0.18, hidden));
        }

        // buildings
        if (ShowBuildings && doc.Buildings is { } list)
        {
            var beltMat = Mat(Color.FromRgb(0x6B, 0x78, 0x7E));
            foreach (var b in list.Items.OfType<GDict>())
            {
                if (b["a"] is GMath a && b["b"] is GMath bb)
                {
                    double dx = bb.V[0] - a.V[0], dz = bb.V[2] - a.V[2], len = Math.Sqrt(dx * dx + dz * dz);
                    if (len < 0.01) continue;
                    double yy = (a.V[1] + bb.V[1]) / 2;
                    root.Children.Add(Box(new Point3D((a.V[0] + bb.V[0]) / 2, Math.Max(0.1, yy - 0.1), (a.V[2] + bb.V[2]) / 2), 0.35, 0.12, len, beltMat, Math.Atan2(dx, dz)));
                    continue;
                }
                if (b["position"] is not GMath p) continue;
                var type = b.Str("type") ?? "";
                double h = type switch { "power_pole" => 2.4, "hay_silo" => 2.6, "hay_drone" => 0.3, "robotic_arm" => 1.2, _ => 0.8 };
                double w = type switch { "power_pole" => 0.15, "hay_drone" => 0.6, _ => 0.75 };
                double baseY = type == "hay_drone" ? 2.5 : 0;
                double yawR = b.Num("yaw");
                root.Children.Add(Box(new Point3D(p.V[0], baseY + h / 2, p.V[2]), w, h, w, Mat(BuildingColor(type), 0.15), yawR));
            }
        }

        // player
        var (px, py, pz) = doc.PlayerPos;
        root.Children.Add(Box(new Point3D(px, py + 0.9, pz), 0.4, 1.8, 0.4, Mat(Colors.White, 0.4)));
        var (fx, fz) = doc.PlayerForward;
        root.Children.Add(Box(new Point3D(px + fx * 0.35, py + 1.5, pz + fz * 0.35), 0.2, 0.2, 0.2, Mat(Color.FromRgb(0x2D, 0xD4, 0xBF), 0.8)));

        foreach (var t in transparent) root.Children.Add(t); // transparent last
    }
}
