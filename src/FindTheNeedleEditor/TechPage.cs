using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using FindTheNeedleEditor.Core;

namespace FindTheNeedleEditor;

// Tech page: one card per area, upgrades drawn as a tree (base tech -> upgrades that build on it).
public partial class MainWindow
{
    string techFilter = "";
    bool techShowLocked = true;

    void BuildTechTab()
    {
        TechPage.Children.Clear();
        var tech = doc!.Root.Dict("tech");
        var ranks = tech?.Dict("ranks");
        if (ranks is null) return;

        var owned = ranks.Items.Where(kv => kv.Key is string).Select(kv => (string)kv.Key!).ToList();
        var all = owned.Union(GameData.KnownTechIds).ToList();

        // ---- header card: stats + tools
        var head = new StackPanel();
        head.Children.Add(UiKit.Heading("TechTitle", icon: "🔧"));
        head.Children.Add(UiKit.Hint("TechHint2"));
        var stats = UiKit.Tiles(4);
        void Stat(string icon, string key, string value, Brush? b = null)
        {
            var t = new Border { Background = UiKit.Brush("Panel2"), BorderBrush = UiKit.Brush("Line"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10),
                                 Padding = new Thickness(14, 11, 14, 11), Margin = new Thickness(0, 0, 10, 10) };
            var sp = new StackPanel();
            sp.Children.Add(new TextBlock { Text = icon + "  " + Loc.T(key), Foreground = UiKit.Brush("Muted"), FontSize = 11.5 });
            sp.Children.Add(new TextBlock { Text = value, FontSize = 21, FontWeight = FontWeights.SemiBold, Foreground = b ?? UiKit.Brush("Text"), Margin = new Thickness(0, 4, 0, 0) });
            t.Child = sp; stats.Children.Add(t);
        }
        Stat("✅", "StatTechOwned", Fmt.Int(owned.Count(id => ranks.Int(id) > 0)), UiKit.Brush("Good"));
        Stat("⭐", "StatTechLevels", Fmt.Int(owned.Sum(id => ranks.Int(id))), UiKit.Brush("Gold"));
        Stat("🔒", "StatTechLocked", Fmt.Int(all.Count(id => !ranks.Has(id))));
        Stat("🧭", "StatTechAreas", Fmt.Int(all.Select(GameData.TechGroupOf).Distinct().Count()));
        head.Children.Add(stats);

        var tools = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
        tools.Children.Add(new TextBlock { Text = "🔍", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 6), Foreground = UiKit.Brush("Muted") });
        var search = new TextBox { Width = 200, Text = techFilter, Margin = new Thickness(0, 0, 14, 6), Padding = new Thickness(8, 4, 8, 4), ToolTip = Loc.T("Filter") };
        search.TextChanged += (_, _) => { techFilter = search.Text; BuildTechTree(ranks, all); };
        tools.Children.Add(search);
        tools.Children.Add(UiKit.SmallButton("AllMinus", (_, _) => BulkRank(ranks, r => Math.Max(1, r - 1))));
        tools.Children.Add(UiKit.SmallButton("AllPlus", (_, _) => BulkRank(ranks, r => r + 1)));
        var lvl = new TextBox { Width = 46, Text = "5", Margin = new Thickness(8, 0, 6, 6), Padding = new Thickness(6, 4, 6, 4), TextAlignment = TextAlignment.Center };
        tools.Children.Add(lvl);
        tools.Children.Add(UiKit.SmallButton("AllToLevel", (_, _) =>
        {
            if (Fmt.TryParse(lvl.Text, out var v) && v >= 0 && v <= 999) BulkRank(ranks, _ => (long)v);
            else SetStatus(Loc.T("InvalidInput", lvl.Text), UiKit.Brush("Bad"));
        }));
        tools.Children.Add(UiKit.SmallButton("AddAllKnown", (_, _) =>
        {
            int n = 0;
            foreach (var id in GameData.KnownTechIds) if (!ranks.Has(id)) { ranks[id] = 1L; n++; }
            if (n > 0) MarkDirty(Loc.T("TabTech").Trim());
            BuildTechTab();
        }));
        tools.Children.Add(UiKit.Switch("ShowLocked", techShowLocked, v => { techShowLocked = v; BuildTechTree(ranks, all); }));
        if (tech!["shop_tools"] is bool shop)
            tools.Children.Add(UiKit.Switch("C_shop_tools", shop, v => { tech["shop_tools"] = v; MarkDirty(Loc.T("C_shop_tools")); }));
        head.Children.Add(tools);
        TechPage.Children.Add(UiKit.Card(head));
        BuildTechTree(ranks, all);
    }

    void BuildTechTree(GDict ranks, List<string> all)
    {
        while (TechPage.Children.Count > 1) TechPage.Children.RemoveAt(1);
        // ---- area cards in two balanced columns
        var cols = new Grid();
        cols.ColumnDefinitions.Add(new ColumnDefinition());
        cols.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        cols.ColumnDefinitions.Add(new ColumnDefinition());
        var left = new StackPanel(); var right = new StackPanel();
        Grid.SetColumn(right, 2);
        cols.Children.Add(left); cols.Children.Add(right);
        int lh = 0, rh = 0;

        bool Matches(string id) => techFilter.Length == 0 || id.Contains(techFilter, StringComparison.OrdinalIgnoreCase)
                                   || GameData.TechName(id).Contains(techFilter, StringComparison.CurrentCultureIgnoreCase);
        bool Visible(string id) => (techShowLocked || ranks.Has(id)) && Matches(id);

        var groups = all.GroupBy(GameData.TechGroupOf).OrderBy(g => g.Key is null ? 99 : (int)g.Key);
        foreach (var g in groups)
        {
            var ids = g.ToList();
            // children map inside the area
            var children = ids.ToDictionary(id => id, _ => new List<string>());
            var roots = new List<string>();
            foreach (var id in ids)
            {
                var p = GameData.TechParent(id);
                if (p is not null && children.ContainsKey(p)) children[p].Add(id);
                else roots.Add(id);
            }
            // keep a subtree when any node in it is visible
            bool SubVisible(string id) => Visible(id) || children[id].Any(SubVisible);
            var shownRoots = roots.Where(SubVisible).ToList();
            if (shownRoots.Count == 0) continue;

            var panel = new StackPanel();
            int lv = ids.Where(ranks.Has).Sum(id => (int)ranks.Int(id));
            var title = new TextBlock { FontSize = 15, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 10) };
            title.Inlines.Add(new System.Windows.Documents.Run($"{GameData.GroupIcon(g.Key)}  {GameData.GroupName(g.Key)}"));
            title.Inlines.Add(new System.Windows.Documents.Run($"   {ids.Count(ranks.Has)}/{ids.Count} · ⭐ {lv}")
                              { Foreground = UiKit.Brush("Muted"), FontSize = 12, FontWeight = FontWeights.Normal });
            panel.Children.Add(title);

            int rows = 0;
            void Add(string id, int depth, bool last)
            {
                panel.Children.Add(TechNode(ranks, id, depth, last, children[id].Any(SubVisible)));
                rows++;
                var kids = children[id].Where(SubVisible).ToList();
                for (int i = 0; i < kids.Count; i++) Add(kids[i], depth + 1, i == kids.Count - 1);
            }
            for (int i = 0; i < shownRoots.Count; i++) Add(shownRoots[i], 0, i == shownRoots.Count - 1);

            var card = UiKit.Card(panel);
            if (lh <= rh) { left.Children.Add(card); lh += rows + 2; }
            else { right.Children.Add(card); rh += rows + 2; }
        }
        TechPage.Children.Add(cols);
    }

    /// <summary>One tree row: connector, status dot, name, id, rank stepper (or unlock button).</summary>
    FrameworkElement TechNode(GDict ranks, string id, int depth, bool last, bool hasKids)
    {
        bool has = ranks.Has(id);
        long rank = ranks.Int(id);
        var row = new Grid { Height = 38 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(depth * 22) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // connector lines
        if (depth > 0)
        {
            var c = new Canvas { Width = depth * 22, Height = 38, HorizontalAlignment = HorizontalAlignment.Left };
            double x = depth * 22 - 12;
            var line = UiKit.Brush("Line");
            c.Children.Add(new Line { X1 = x, Y1 = 0, X2 = x, Y2 = last ? 19 : 38, Stroke = line, StrokeThickness = 1.5 });
            c.Children.Add(new Line { X1 = x, Y1 = 19, X2 = x + 10, Y2 = 19, Stroke = line, StrokeThickness = 1.5 });
            row.Children.Add(c);
        }

        var nameRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var dotColor = !has ? "Muted" : rank > 0 ? "Accent" : "Warn";
        nameRow.Children.Add(new Ellipse { Width = hasKids ? 11 : 8, Height = hasKids ? 11 : 8, Fill = has && rank > 0 ? UiKit.Brush(dotColor) : Brushes.Transparent,
                                           Stroke = UiKit.Brush(dotColor), StrokeThickness = 1.6, Margin = new Thickness(0, 0, 9, 0), VerticalAlignment = VerticalAlignment.Center });
        var name = new TextBlock { Text = GameData.TechName(id), VerticalAlignment = VerticalAlignment.Center,
                                   FontWeight = hasKids ? FontWeights.SemiBold : FontWeights.Normal, Foreground = UiKit.Brush(has ? "Text" : "Muted") };
        nameRow.Children.Add(name);
        nameRow.Children.Add(new TextBlock { Text = "  " + id, FontSize = 10.5, Foreground = UiKit.Brush("Muted"), Opacity = 0.7, VerticalAlignment = VerticalAlignment.Center });
        Grid.SetColumn(nameRow, 1);
        row.Children.Add(nameRow);

        FrameworkElement ctrl;
        if (!has)
        {
            var b = UiKit.SmallButton("Unlock", (_, _) => { ranks[id] = 1L; MarkDirty(GameData.TechName(id)); BuildTechTab(); });
            b.Margin = new Thickness(0);
            ctrl = b;
        }
        else
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var val = new TextBox { Width = 44, Text = rank.ToString(Loc.Instance.Culture), TextAlignment = TextAlignment.Center, Padding = new Thickness(2, 3, 2, 3),
                                    Margin = new Thickness(4, 0, 4, 0), FontWeight = FontWeights.SemiBold, Foreground = UiKit.Brush(rank > 0 ? "Gold" : "Muted") };
            void Set(long v)
            {
                v = Math.Clamp(v, 0, 999);
                val.Text = v.ToString(Loc.Instance.Culture);
                if (ranks.Int(id) == v) return;
                ranks[id] = v;
                val.Foreground = UiKit.Brush(v > 0 ? "Gold" : "Muted");
                MarkDirty(GameData.TechName(id));
            }
            void CommitBox() { if (Fmt.TryParse(val.Text, out var d)) Set((long)Math.Round(d)); else val.Text = ranks.Int(id).ToString(Loc.Instance.Culture); }
            val.LostFocus += (_, _) => CommitBox();
            val.KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) CommitBox(); };
            var minus = new Button { Content = "−", Style = (Style)FindResource("StepButton"), Width = 26, Height = 26 };
            minus.Click += (_, _) => Set(ranks.Int(id) - 1);
            var plus = new Button { Content = "+", Style = (Style)FindResource("StepButton"), Width = 26, Height = 26 };
            plus.Click += (_, _) => Set(ranks.Int(id) + 1);
            sp.Children.Add(minus); sp.Children.Add(val); sp.Children.Add(plus);
            ctrl = sp;
        }
        ctrl.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(ctrl, 2);
        row.Children.Add(ctrl);

        return new Border { Child = row, Padding = new Thickness(6, 0, 4, 0), CornerRadius = new CornerRadius(6),
                            Background = depth == 0 ? UiKit.Brush("Panel2") : Brushes.Transparent, Margin = new Thickness(0, depth == 0 ? 4 : 0, 0, 0) };
    }

    void BulkRank(GDict ranks, Func<long, long> f)
    {
        bool Match(string id) => techFilter.Length == 0 || id.Contains(techFilter, StringComparison.OrdinalIgnoreCase)
                                 || GameData.TechName(id).Contains(techFilter, StringComparison.CurrentCultureIgnoreCase);
        int n = 0;
        foreach (var kv in ranks.Items.ToList())
        {
            if (kv.Key is not string id || !Match(id)) continue;
            long v = Math.Clamp(f(ranks.Int(id)), 0, 999);
            if (v != ranks.Int(id)) { ranks[id] = v; n++; }
        }
        if (n > 0) MarkDirty(Loc.T("TabTech").Trim());
        BuildTechTab();
    }
}
