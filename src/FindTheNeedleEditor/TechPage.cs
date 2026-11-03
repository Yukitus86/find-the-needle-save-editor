using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using FindTheNeedleEditor.Core;

namespace FindTheNeedleEditor;

// Tech page built from the game's own card table (TechDb): one card per branch, cards drawn as a
// tree along their real prerequisites, ranks limited to the game's maximum.
public partial class MainWindow
{
    string techFilter = "";
    bool techShowLocked = true;

    static int MaxOf(string id) => TechDb.Get(id) is { } c ? c.Max : 999;

    void BuildTechTab()
    {
        TechPage.Children.Clear();
        var tech = doc!.Root.Dict("tech");
        var ranks = tech?.Dict("ranks");
        if (ranks is null) return;

        var owned = ranks.Items.Where(kv => kv.Key is string).Select(kv => (string)kv.Key!).ToList();
        var all = owned.Union(TechDb.Cards.Keys).ToList();

        // ---- header: stats + tools
        var head = new StackPanel();
        head.Children.Add(UiKit.Heading("TechTitle", icon: "🔧"));
        head.Children.Add(UiKit.Hint("TechHint3"));
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
        var available = all.Where(id => TechDb.Get(id) is not { IsDemoLocked: true }).ToList();
        Stat("✅", "StatTechOwned", $"{owned.Count(id => ranks.Int(id) > 0)} / {available.Count}", UiKit.Brush("Good"));
        Stat("⭐", "StatTechLevels", $"{owned.Sum(id => ranks.Int(id))} / {available.Sum(MaxOf)}", UiKit.Brush("Gold"));
        Stat("🏆", "StatTechMaxed", Fmt.Int(owned.Count(id => TechDb.Get(id) is { IsRanked: true } c && ranks.Int(id) >= c.Max)), UiKit.Brush("Warn"));
        Stat("🔒", "StatTechFullGame", Fmt.Int(TechDb.Cards.Values.Count(c => c.IsDemoLocked)));
        head.Children.Add(stats);

        var tools = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
        tools.Children.Add(new TextBlock { Text = "🔍", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 6), Foreground = UiKit.Brush("Muted") });
        var search = new TextBox { Width = 200, Text = techFilter, Margin = new Thickness(0, 0, 14, 6), Padding = new Thickness(8, 4, 8, 4), ToolTip = Loc.T("Filter") };
        search.TextChanged += (_, _) => { techFilter = search.Text; BuildTechTree(ranks, all); };
        tools.Children.Add(search);
        tools.Children.Add(UiKit.SmallButton("AllMinus", (_, _) => BulkRank(ranks, (id, r) => Math.Max(1, r - 1))));
        tools.Children.Add(UiKit.SmallButton("AllPlus", (_, _) => BulkRank(ranks, (id, r) => Math.Min(MaxOf(id), r + 1))));
        tools.Children.Add(UiKit.SmallButton("AllMax", (_, _) => BulkRank(ranks, (id, _) => MaxOf(id)), accent: true));
        tools.Children.Add(UiKit.SmallButton("UnlockAll", (_, _) =>
        {
            int n = 0;
            foreach (var c in TechDb.Cards.Values.Where(c => !c.IsDemoLocked)) n += Unlock(ranks, c.Id);
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

    /// <summary>Unlocks a card and, recursively, everything it requires. Returns the number of cards added.</summary>
    static int Unlock(GDict ranks, string id, int depth = 0)
    {
        if (depth > 30 || TechDb.Get(id) is { IsDemoLocked: true }) return 0;
        int n = 0;
        foreach (var r in TechDb.Get(id)?.Requires ?? []) n += Unlock(ranks, r, depth + 1);
        if (!ranks.Has(id) || ranks.Int(id) <= 0) { ranks[id] = 1L; n++; }
        return n;
    }

    void BuildTechTree(GDict ranks, List<string> all)
    {
        while (TechPage.Children.Count > 1) TechPage.Children.RemoveAt(1);

        var cols = new Grid();
        cols.ColumnDefinitions.Add(new ColumnDefinition());
        cols.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        cols.ColumnDefinitions.Add(new ColumnDefinition());
        var left = new StackPanel(); var right = new StackPanel();
        Grid.SetColumn(right, 2);
        cols.Children.Add(left); cols.Children.Add(right);
        int lh = 0, rh = 0;

        string NameOf(string id) => TechDb.Get(id)?.DisplayName ?? GameData.TechName(id);
        bool Matches(string id) => techFilter.Length == 0 || id.Contains(techFilter, StringComparison.OrdinalIgnoreCase)
                                   || NameOf(id).Contains(techFilter, StringComparison.CurrentCultureIgnoreCase);
        bool Visible(string id) => (techShowLocked || ranks.Has(id)) && Matches(id);

        foreach (var g in all.GroupBy(id => TechDb.Get(id)?.Branch ?? "").OrderBy(g => TechDb.BranchOrderKey(g.Key)))
        {
            var ids = g.ToList();
            var inBranch = new HashSet<string>(ids);
            var children = ids.ToDictionary(id => id, _ => new List<string>());
            var roots = new List<string>();
            foreach (var id in ids)
            {
                var parent = TechDb.Get(id)?.Requires.FirstOrDefault(inBranch.Contains);
                if (parent is not null && parent != id) children[parent].Add(id);
                else roots.Add(id);
            }
            bool SubVisible(string id, int d = 0) => d < 40 && (Visible(id) || children[id].Any(k => SubVisible(k, d + 1)));
            var shownRoots = roots.Where(r => SubVisible(r)).ToList();
            if (shownRoots.Count == 0) continue;

            var panel = new StackPanel();
            var avail = ids.Where(id => TechDb.Get(id) is not { IsDemoLocked: true }).ToList();
            var title = new TextBlock { FontSize = 15, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 10) };
            title.Inlines.Add(new System.Windows.Documents.Run(TechDb.BranchTitle(g.Key)));
            title.Inlines.Add(new System.Windows.Documents.Run($"   {avail.Count(ranks.Has)}/{avail.Count} · ⭐ {avail.Sum(id => ranks.Int(id))}/{avail.Sum(MaxOf)}")
                              { Foreground = UiKit.Brush("Muted"), FontSize = 12, FontWeight = FontWeights.Normal });
            panel.Children.Add(title);

            int rows = 0;
            var drawn = new HashSet<string>();
            void Add(string id, int depth, bool last)
            {
                if (!drawn.Add(id)) return;
                var kids = children[id].Where(k => SubVisible(k)).ToList();
                if (Visible(id) || kids.Count > 0) { panel.Children.Add(TechNode(ranks, id, depth, last, kids.Count > 0, inBranch)); rows++; }
                for (int i = 0; i < kids.Count; i++) Add(kids[i], depth + 1, i == kids.Count - 1);
            }
            for (int i = 0; i < shownRoots.Count; i++) Add(shownRoots[i], 0, i == shownRoots.Count - 1);

            var card = UiKit.Card(panel);
            if (lh <= rh) { left.Children.Add(card); lh += rows + 2; }
            else { right.Children.Add(card); rh += rows + 2; }
        }
        TechPage.Children.Add(cols);
    }

    /// <summary>One tree row: connector, status dot, name, effect, level pips and stepper (or switch / badge).</summary>
    FrameworkElement TechNode(GDict ranks, string id, int depth, bool last, bool hasKids, HashSet<string> inBranch)
    {
        var card = TechDb.Get(id);
        bool has = ranks.Has(id);
        long rank = ranks.Int(id);
        int max = card?.Max ?? 999;
        bool ranked = card is null || card.IsRanked;
        bool demo = card?.IsDemoLocked == true;

        var row = new Grid { MinHeight = 40 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(depth * 22) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        if (depth > 0)
        {
            var c = new Canvas { Width = depth * 22, HorizontalAlignment = HorizontalAlignment.Left };
            double x = depth * 22 - 12;
            var line = UiKit.Brush("Line");
            c.Children.Add(new Line { X1 = x, Y1 = 0, X2 = x, Y2 = last ? 20 : 40, Stroke = line, StrokeThickness = 1.5 });
            c.Children.Add(new Line { X1 = x, Y1 = 20, X2 = x + 10, Y2 = 20, Stroke = line, StrokeThickness = 1.5 });
            row.Children.Add(c);
        }

        // name + effect line
        var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 3, 8, 3) };
        var nameRow = new StackPanel { Orientation = Orientation.Horizontal };
        bool maxed = ranked && has && rank >= max && card is not null;
        var dotColor = demo ? "Line" : !has ? "Muted" : maxed ? "Gold" : "Accent";
        nameRow.Children.Add(new Ellipse { Width = hasKids ? 11 : 8, Height = hasKids ? 11 : 8, Fill = has && rank > 0 ? UiKit.Brush(dotColor) : Brushes.Transparent,
                                           Stroke = UiKit.Brush(dotColor), StrokeThickness = 1.6, Margin = new Thickness(0, 0, 9, 0), VerticalAlignment = VerticalAlignment.Center });
        nameRow.Children.Add(new TextBlock { Text = card?.DisplayName ?? GameData.TechName(id), VerticalAlignment = VerticalAlignment.Center,
                                             FontWeight = hasKids ? FontWeights.SemiBold : FontWeights.Normal, Foreground = UiKit.Brush(has && !demo ? "Text" : "Muted") });
        // prerequisites from other branches
        foreach (var r in card?.Requires.Where(r => !inBranch.Contains(r)) ?? [])
            nameRow.Children.Add(new Border { Background = UiKit.Brush("Bg"), CornerRadius = new CornerRadius(6), Padding = new Thickness(6, 0, 6, 1), Margin = new Thickness(8, 0, 0, 0),
                                              VerticalAlignment = VerticalAlignment.Center,
                                              Child = new TextBlock { Text = "← " + (TechDb.Get(r)?.DisplayName ?? r), FontSize = 10.5, Foreground = UiKit.Brush("Muted") } });
        info.Children.Add(nameRow);
        string? effect = ranked && card is not null ? card.ValueAt(has ? rank : 0) : null;
        if (effect is not null || card?.DisplayReward is not null)
            info.Children.Add(new TextBlock { Text = effect ?? card!.DisplayReward, FontSize = 11, Foreground = UiKit.Brush("Muted"), Margin = new Thickness(19, 1, 0, 0),
                                              TextTrimming = TextTrimming.CharacterEllipsis });
        Grid.SetColumn(info, 1);
        row.Children.Add(info);

        // tooltip: description + next level
        var tip = new List<string>();
        if (card?.DisplayBlurb is { } blurb) tip.Add(blurb);
        if (ranked && card is not null && card.Costs.Length > 0)
            tip.Add(string.Join("\n", card.Costs.Select((cost, i) => Loc.T("LevelLine", i + 1, Fmt.Money(cost), card.ValueAt(i + 1) ?? ""))));
        tip.Add(id);
        row.ToolTip = string.Join("\n\n", tip);

        FrameworkElement ctrl;
        if (demo)
        {
            ctrl = new Border { Background = UiKit.Brush("Bg"), CornerRadius = new CornerRadius(8), Padding = new Thickness(9, 3, 9, 4),
                                Child = new TextBlock { Text = Loc.T("FullGameOnly"), FontSize = 11, Foreground = UiKit.Brush("Muted") } };
        }
        else if (!has)
        {
            var b = UiKit.SmallButton("Unlock", (_, _) => { Unlock(ranks, id); MarkDirty(card?.DisplayName ?? id); BuildTechTab(); });
            b.Margin = new Thickness(0);
            ctrl = b;
        }
        else if (!ranked)
        {
            ctrl = new Border { Background = UiKit.Brush("AccentSoft"), CornerRadius = new CornerRadius(8), Padding = new Thickness(9, 3, 9, 4),
                                Child = new TextBlock { Text = "✓ " + Loc.T("Owned"), FontSize = 11.5, Foreground = UiKit.Brush("Good") } };
        }
        else
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            // level pips
            var pips = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
            if (card is not null && max <= 10)
                for (int i = 1; i <= max; i++)
                    pips.Children.Add(new Border { Width = 7, Height = 12, CornerRadius = new CornerRadius(2), Margin = new Thickness(1.5, 0, 1.5, 0),
                                                   Background = UiKit.Brush(i <= rank ? (rank >= max ? "Gold" : "Accent") : "Bg") });
            sp.Children.Add(pips);
            var minus = new Button { Content = "−", Style = (Style)FindResource("StepButton"), Width = 26, Height = 26 };
            var plus = new Button { Content = "+", Style = (Style)FindResource("StepButton"), Width = 26, Height = 26 };
            var label = new TextBlock { Width = 58, TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.SemiBold };
            void Show(long r)
            {
                bool isMax = card is not null && r >= max;
                label.Text = isMax ? "MAX" : card is null ? r.ToString(Loc.Instance.Culture) : $"{r} / {max}";
                label.Foreground = UiKit.Brush(isMax ? "Gold" : r > 0 ? "Text" : "Muted");
                plus.IsEnabled = !isMax;
                minus.IsEnabled = r > 0;
                for (int i = 0; i < pips.Children.Count; i++)
                    ((Border)pips.Children[i]).Background = UiKit.Brush(i < r ? (isMax ? "Gold" : "Accent") : "Bg");
            }
            void Set(long v)
            {
                v = Math.Clamp(v, 0, max);
                if (ranks.Int(id) != v) { ranks[id] = v; MarkDirty(card?.DisplayName ?? id); }
                Show(v);
            }
            minus.Click += (_, _) => Set(ranks.Int(id) - 1);
            plus.Click += (_, _) => Set(ranks.Int(id) + 1);
            label.MouseLeftButtonUp += (_, _) => Set(ranks.Int(id) >= max ? 1 : max); // click the label: jump to MAX (or back to 1)
            label.Cursor = System.Windows.Input.Cursors.Hand;
            label.ToolTip = Loc.T("ClickMax");
            sp.Children.Add(minus); sp.Children.Add(label); sp.Children.Add(plus);
            Show(rank);
            ctrl = sp;
        }
        ctrl.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(ctrl, 2);
        row.Children.Add(ctrl);

        return new Border { Child = row, Padding = new Thickness(6, 0, 4, 0), CornerRadius = new CornerRadius(6), Opacity = demo ? 0.55 : 1,
                            Background = depth == 0 ? UiKit.Brush("Panel2") : Brushes.Transparent, Margin = new Thickness(0, depth == 0 ? 4 : 0, 0, 0) };
    }

    void BulkRank(GDict ranks, Func<string, long, long> f)
    {
        bool Match(string id) => techFilter.Length == 0 || id.Contains(techFilter, StringComparison.OrdinalIgnoreCase)
                                 || (TechDb.Get(id)?.DisplayName ?? GameData.TechName(id)).Contains(techFilter, StringComparison.CurrentCultureIgnoreCase);
        int n = 0;
        foreach (var kv in ranks.Items.ToList())
        {
            if (kv.Key is not string id || !Match(id)) continue;
            if (TechDb.Get(id) is { } c && !c.IsRanked) continue; // unlocks stay at 1
            long v = Math.Clamp(f(id, ranks.Int(id)), 0, MaxOf(id));
            if (v != ranks.Int(id)) { ranks[id] = v; n++; }
        }
        if (n > 0) MarkDirty(Loc.T("TabTech").Trim());
        BuildTechTab();
    }
}
