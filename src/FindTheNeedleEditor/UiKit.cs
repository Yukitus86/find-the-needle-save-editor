using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using FindTheNeedleEditor.Core;

namespace FindTheNeedleEditor;

public enum ValueKind { Number, Integer, Money, Seconds }

/// <summary>
/// Read-friendly value card: icon, label, big formatted number. Click (or Enter/F2 when focused) to edit inline;
/// Enter/focus loss commits, Esc cancels.
/// </summary>
public sealed class ValueTile : Border
{
    readonly Func<double> get;
    readonly Func<double, bool> set;
    readonly ValueKind kind;
    readonly TextBlock valueText = new();
    readonly TextBlock subText = new();
    readonly TextBox editor = new();
    readonly Border bar = new();
    readonly Grid barHost = new() { Height = 5, Margin = new Thickness(0, 8, 0, 0), Visibility = Visibility.Collapsed };
    double barMax;

    public ValueTile(string icon, string labelKey, Func<double> get, Func<double, bool> set, ValueKind kind = ValueKind.Number,
                     Brush? valueBrush = null, double fontSize = 21)
    {
        this.get = get; this.set = set; this.kind = kind;
        Background = UiKit.Brush("Panel2");
        BorderBrush = UiKit.Brush("Line");
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(10);
        Padding = new Thickness(14, 11, 14, 11);
        Margin = new Thickness(0, 0, 10, 10);
        Cursor = Cursors.IBeam;
        Focusable = true;
        FocusVisualStyle = null;

        var head = new DockPanel();
        var pencil = new TextBlock { Text = "✎", Foreground = UiKit.Brush("Muted"), FontSize = 12, Opacity = 0 };
        DockPanel.SetDock(pencil, Dock.Right);
        head.Children.Add(pencil);
        var label = new TextBlock { Foreground = UiKit.Brush("Muted"), FontSize = 11.5, TextTrimming = TextTrimming.CharacterEllipsis };
        label.Inlines.Add(new System.Windows.Documents.Run(icon + "  "));
        var run = new System.Windows.Documents.Run();
        run.SetBinding(System.Windows.Documents.Run.TextProperty, TExtension.Bind(labelKey));
        label.Inlines.Add(run);
        head.Children.Add(label);

        valueText.FontSize = fontSize;
        valueText.FontWeight = FontWeights.SemiBold;
        valueText.Foreground = valueBrush ?? UiKit.Brush("Text");
        valueText.Margin = new Thickness(0, 4, 0, 0);
        valueText.TextTrimming = TextTrimming.CharacterEllipsis;
        subText.Foreground = UiKit.Brush("Muted");
        subText.FontSize = 11;
        subText.Visibility = Visibility.Collapsed;

        editor.FontSize = fontSize - 3;
        editor.Margin = new Thickness(0, 4, 0, 0);
        editor.Visibility = Visibility.Collapsed;
        editor.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { Commit(); e.Handled = true; }
            else if (e.Key == Key.Escape) { EndEdit(); e.Handled = true; }
        };
        editor.LostKeyboardFocus += (_, _) => { if (editor.Visibility == Visibility.Visible) Commit(); };

        barHost.Children.Add(new Border { Background = UiKit.Brush("Bg"), CornerRadius = new CornerRadius(3) });
        bar.CornerRadius = new CornerRadius(3);
        bar.HorizontalAlignment = HorizontalAlignment.Left;
        bar.Background = valueBrush ?? UiKit.Brush("Accent");
        barHost.Children.Add(bar);
        barHost.SizeChanged += (_, _) => UpdateBar();

        var stack = new StackPanel();
        stack.Children.Add(head);
        stack.Children.Add(valueText);
        stack.Children.Add(editor);
        stack.Children.Add(subText);
        stack.Children.Add(barHost);
        Child = stack;

        MouseEnter += (_, _) => { BorderBrush = UiKit.Brush("Accent"); pencil.Opacity = 1; };
        MouseLeave += (_, _) => { BorderBrush = UiKit.Brush("Line"); pencil.Opacity = 0; };
        MouseLeftButtonUp += (_, _) => BeginEdit();
        KeyDown += (_, e) => { if (e.Key is Key.Enter or Key.F2 && editor.Visibility != Visibility.Visible) { BeginEdit(); e.Handled = true; } };
        Refresh();
    }

    /// <summary>Shows a thin fill bar (value / max).</summary>
    public ValueTile WithBar(double max) { barMax = max; barHost.Visibility = Visibility.Visible; UpdateBar(); return this; }

    void UpdateBar()
    {
        if (barMax <= 0) return;
        bar.Width = Math.Clamp(get() / barMax, 0, 1) * Math.Max(0, barHost.ActualWidth);
    }

    public void Refresh()
    {
        double v = get();
        valueText.Text = UiKit.Format(v, kind);
        if (kind == ValueKind.Seconds)
        {
            subText.Text = v > 0 ? UiKit.Format(v, ValueKind.Number) + " s" : "";
            subText.Visibility = v > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        UpdateBar();
    }

    void BeginEdit()
    {
        if (editor.Visibility == Visibility.Visible) return;
        double v = get();
        editor.Text = kind is ValueKind.Integer ? ((long)Math.Round(v)).ToString(Loc.Instance.Culture)
                    : kind is ValueKind.Money ? v.ToString("0.##", Loc.Instance.Culture)
                    : v.ToString("0.######", Loc.Instance.Culture);
        valueText.Visibility = Visibility.Collapsed;
        editor.Visibility = Visibility.Visible;
        editor.ClearValue(Control.BorderBrushProperty);
        Dispatcher.BeginInvoke(() => { editor.Focus(); editor.SelectAll(); }, System.Windows.Threading.DispatcherPriority.Input);
    }

    void Commit()
    {
        var before = UiKit.Format(get(), kind);
        if (!Fmt.TryParse(editor.Text, out var v) || !set(v))
        {
            editor.BorderBrush = UiKit.Brush("Bad");
            return;
        }
        EndEdit();
        _ = before;
    }

    void EndEdit()
    {
        editor.Visibility = Visibility.Collapsed;
        valueText.Visibility = Visibility.Visible;
        Refresh();
    }
}

public static class UiKit
{
    public static Brush Brush(string key) => (Brush)Application.Current.FindResource(key);

    public static string Format(double v, ValueKind kind) => kind switch
    {
        ValueKind.Money => "$ " + Fmt.Money(v),
        ValueKind.Integer => Fmt.Int(v),
        ValueKind.Seconds => v < 0 ? "–" : Fmt.Duration(v),
        _ => Math.Abs(v) >= 1000 ? Fmt.Int(v) : Fmt.Num(v),
    };

    /// <summary>A rounded pill. With onRemove a × button is shown.</summary>
    public static Border Chip(string text, Action? onRemove = null, Brush? background = null, string? tooltip = null)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal };
        sp.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });
        if (onRemove is not null)
        {
            var x = new Button { Content = "×", Padding = new Thickness(6, 0, 6, 1), Margin = new Thickness(8, 0, -6, 0), FontSize = 13,
                                 BorderThickness = new Thickness(0), Background = Brushes.Transparent };
            x.Click += (_, _) => onRemove();
            sp.Children.Add(x);
        }
        return new Border
        {
            Child = sp, Background = background ?? Brush("AccentSoft"), CornerRadius = new CornerRadius(14),
            Padding = new Thickness(11, 4, 11, 4), Margin = new Thickness(0, 0, 8, 8), ToolTip = tooltip,
        };
    }

    /// <summary>Card section heading with an optional right-hand element.</summary>
    public static DockPanel Heading(string textKey, UIElement? right = null, string icon = "")
    {
        var dp = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
        if (right is not null) { DockPanel.SetDock(right, Dock.Right); dp.Children.Add(right); }
        var t = new TextBlock { FontSize = 15, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
        if (icon.Length > 0) t.Inlines.Add(new System.Windows.Documents.Run(icon + "  "));
        var r = new System.Windows.Documents.Run();
        r.SetBinding(System.Windows.Documents.Run.TextProperty, TExtension.Bind(textKey));
        t.Inlines.Add(r);
        dp.Children.Add(t);
        return dp;
    }

    public static TextBlock Hint(string key)
    {
        var t = new TextBlock { Foreground = Brush("Muted"), FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) };
        t.SetBinding(TextBlock.TextProperty, TExtension.Bind(key));
        return t;
    }

    public static Border Card(UIElement child, Thickness? margin = null) => new()
    {
        Style = (Style)Application.Current.FindResource("Card"),
        Child = child,
        Margin = margin ?? new Thickness(0, 0, 0, 12),
    };

    /// <summary>Switch-style toggle bound to a text key.</summary>
    public static CheckBox Switch(string labelKey, bool value, Action<bool> changed)
    {
        var cb = new CheckBox { IsChecked = value, Style = (Style)Application.Current.FindResource("Switch"), Margin = new Thickness(0, 0, 18, 10) };
        cb.SetBinding(ContentControl.ContentProperty, TExtension.Bind(labelKey));
        cb.Click += (_, _) => changed(cb.IsChecked == true);
        return cb;
    }

    public static Button SmallButton(string textKeyOrText, RoutedEventHandler click, bool literal = false, bool accent = false)
    {
        var b = new Button { Style = (Style)Application.Current.FindResource(accent ? "AccentButton" : "SmallButton"), Margin = new Thickness(0, 0, 6, 0) };
        if (accent) b.Padding = new Thickness(10, 4, 10, 4);
        if (literal) b.Content = textKeyOrText;
        else b.SetBinding(ContentControl.ContentProperty, TExtension.Bind(textKeyOrText));
        b.Click += click;
        return b;
    }

    /// <summary>Thin horizontal bar.</summary>
    public static Grid Bar(double fraction, Brush fill, double width = 120, double height = 6)
    {
        var g = new Grid { Width = width, Height = height, VerticalAlignment = VerticalAlignment.Center };
        g.Children.Add(new Border { Background = Brush("Bg"), CornerRadius = new CornerRadius(height / 2) });
        g.Children.Add(new Border { Background = fill, CornerRadius = new CornerRadius(height / 2), HorizontalAlignment = HorizontalAlignment.Left,
                                    Width = Math.Clamp(fraction, 0, 1) * width });
        return g;
    }

    public static UniformGrid Tiles(int columns) => new() { Columns = columns, Margin = new Thickness(0, 0, -10, -10) };
}
