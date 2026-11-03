using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using FindTheNeedleEditor.Core;

namespace FindTheNeedleEditor;

// Code-built pages: Money, Gear, Buildings, Missions (Tech lives in TechPage.cs).
public partial class MainWindow
{
    /// <summary>Value card for dict[key]; null when the save has no such number.</summary>
    ValueTile? Tile(GDict? dict, string key, string icon, ValueKind kind = ValueKind.Number, double min = 0,
                    Brush? brush = null, Action? after = null, string? labelKey = null)
    {
        if (dict is null || dict[key] is not (long or double)) return null;
        if (kind == ValueKind.Number && dict[key] is long) kind = ValueKind.Integer;
        var lk = labelKey ?? "F_" + key;
        return new ValueTile(icon, lk, () => dict.Num(key), v =>
        {
            if (v < min || v > 1e15) return false;
            dict.SetNum(key, v);
            MarkDirty(Loc.T(lk));
            after?.Invoke();
            return true;
        }, kind, brush);
    }

    static void AddTo(Panel p, params UIElement?[] items)
    {
        foreach (var i in items) if (i is not null) p.Children.Add(i);
    }

    // ================================================================ money

    void BuildMoneyTab()
    {
        var s = doc!.State;
        MoneyPage.Children.Clear();

        // hero: balance
        var hero = new Grid();
        hero.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        hero.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var balance = new ValueTile("💰", "F_money", () => s.Num("money"), v =>
        {
            if (v < 0 || v > 1e15) return false;
            SetMoney(v);
            return true;
        }, ValueKind.Money, UiKit.Brush("Gold"), 40) { Margin = new Thickness(0, 0, 16, 0), Padding = new Thickness(20, 14, 20, 14) };
        hero.Children.Add(balance);

        var right = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var quick = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
        foreach (var (key, tag) in new[] { ("Add1k", "1000"), ("Add10k", "10000"), ("Add100k", "100000"), ("Add1m", "1000000"), ("x2", "x2") })
        {
            var b = UiKit.SmallButton(key == "x2" ? "× 2" : key, (_, _) => AddMoney(tag), literal: key == "x2");
            b.Padding = new Thickness(12, 7, 12, 7);
            b.Margin = new Thickness(0, 0, 6, 6);
            quick.Children.Add(b);
        }
        right.Children.Add(quick);
        right.Children.Add(UiKit.Hint("MoneyHint"));
        Grid.SetColumn(right, 1);
        right.MaxWidth = 470;
        hero.Children.Add(right);

        var heroStack = new StackPanel();
        heroStack.Children.Add(hero);
        if (s.Has("debt"))
        {
            var debtRow = new DockPanel { Margin = new Thickness(0, 14, 0, 0) };
            var pay = UiKit.SmallButton("PayDebt", (_, _) => { s.SetNum("debt", 0); MarkDirty(Loc.T("F_debt")); BuildMoneyTab(); });
            pay.VerticalAlignment = VerticalAlignment.Center;
            var debt = Tile(s, "debt", "🧾", brush: s.Num("debt") > 0 ? UiKit.Brush("Bad") : null)!;
            debt.Margin = new Thickness(0, 0, 12, 0);
            debt.Width = 300;
            debt.HorizontalAlignment = HorizontalAlignment.Left;
            DockPanel.SetDock(pay, Dock.Left);
            debtRow.LastChildFill = false;
            debtRow.Children.Add(debt);
            debtRow.Children.Add(pay);
            heroStack.Children.Add(debtRow);
        }
        MoneyPage.Children.Add(UiKit.Card(heroStack));

        // records
        var rec = new StackPanel();
        rec.Children.Add(UiKit.Heading("CardStats", icon: "🏆"));
        rec.Children.Add(UiKit.Hint("StatsHint"));
        var tiles = UiKit.Tiles(4);
        AddTo(tiles,
            Tile(s, "money_earned", "💵", ValueKind.Money),
            Tile(s, "money_peak", "📈", ValueKind.Money),
            Tile(s, "best_sale", "🏷", ValueKind.Money),
            Tile(s, "best_income", "⚡", ValueKind.Money),
            Tile(s, "hay_sold", "🌾"),
            Tile(s, "best_sale_strands", "🌾"),
            Tile(s, "best_combo", "🔥"),
            Tile(s, "stacks_ordered", "📦"),
            Tile(s, "lot_tier", "🏠"),
            Tile(s, "run_secs", "⏱", ValueKind.Seconds, after: UpdateHeader, labelKey: "F_run_time"),
            Tile(s, "first_needle_secs", "📍", ValueKind.Seconds, min: -1, labelKey: "F_first_needle_time"));
        rec.Children.Add(tiles);
        MoneyPage.Children.Add(UiKit.Card(rec));

        // extras + hay side by side
        var two = new Grid();
        two.ColumnDefinitions.Add(new ColumnDefinition());
        two.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        two.ColumnDefinitions.Add(new ColumnDefinition());

        var ex = new StackPanel();
        ex.Children.Add(UiKit.Heading("CardExtras", icon: "🎒"));
        var exTiles = UiKit.Tiles(2);
        AddTo(exTiles,
            Tile(s, "jetpack_fuel", "🚀", brush: UiKit.Brush("Warn"))?.WithBar(100),
            Tile(s, "radar_cool", "📡", ValueKind.Number));
        ex.Children.Add(exTiles);
        var exButtons = new WrapPanel { Margin = new Thickness(0, 4, 0, 10) };
        exButtons.Children.Add(UiKit.SmallButton("RadarReady", (_, _) => { if (s.Has("radar_cool")) { s.SetNum("radar_cool", 0); MarkDirty(Loc.T("F_radar_cool")); BuildMoneyTab(); } }));
        exButtons.Children.Add(UiKit.SmallButton("FuelFull", (_, _) => { if (s.Has("jetpack_fuel")) { s.SetNum("jetpack_fuel", 100); MarkDirty(Loc.T("F_jetpack_fuel")); BuildMoneyTab(); } }));
        ex.Children.Add(exButtons);
        var sw = new WrapPanel();
        foreach (var k in new[] { "floor_hatch", "run_timed", "contract_pinned" })
            if (s[k] is bool b)
            {
                string key = k;
                sw.Children.Add(UiKit.Switch("C_" + key, b, v => { s[key] = v; MarkDirty(Loc.T("C_" + key)); }));
            }
        ex.Children.Add(sw);
        two.Children.Add(UiKit.Card(ex, new Thickness(0)));

        var hay = new StackPanel();
        hay.Children.Add(UiKit.Heading("CardHay", icon: "🌾"));
        hay.Children.Add(UiKit.Hint("HayHint"));
        double init = Math.Max(1, s.Num("hay_initial"));
        var hayTiles = UiKit.Tiles(2);
        AddTo(hayTiles,
            Tile(s, "hay_total", "🌾", brush: UiKit.Brush("Gold"), after: BuildMapSide)?.WithBar(init),
            Tile(s, "hay_initial", "⛰", after: BuildMapSide),
            Tile(s, "hay_dug", "⛏", after: BuildMapSide)?.WithBar(init),
            Tile(s, "hay_returned", "↩", after: BuildMapSide));
        hay.Children.Add(hayTiles);
        var hayCard = UiKit.Card(hay, new Thickness(0));
        Grid.SetColumn(hayCard, 2);
        two.Children.Add(hayCard);
        MoneyPage.Children.Add(two);
    }

    void SetMoney(double v)
    {
        var s = doc!.State;
        v = Math.Round(Math.Clamp(v, 0, 1e15), 2);
        s.SetNum("money", v);
        if (s.Num("money_peak") < v) s.SetNum("money_peak", v);
        MarkDirty(Loc.T("F_money"));
        Dispatcher.BeginInvoke(BuildMoneyTab, System.Windows.Threading.DispatcherPriority.Background);
    }

    void AddMoney(string tag)
    {
        if (doc is null) return;
        double cur = doc.State.Num("money");
        SetMoney(tag == "x2" ? Math.Max(cur * 2, 1) : cur + double.Parse(tag, CultureInfo.InvariantCulture));
    }

    // ================================================================ gear

    static string ToolIcon(string id) => id switch
    {
        "sand_shovel" => "🥄", "broom" => "🧹", "spade" => "⛏", "pitchfork" => "🔱", "bucket" => "🧺", "wheelbarrow" => "🛒", _ => "🔧",
    };

    static string BuildIcon(string id) => id switch
    {
        "" => "·",
        "belt" or "enclosed_belt" or "conveyor" => "🛤",
        "splitter" or "t_splitter" or "u_splitter" or "compact_splitter" or "conveyor_splitter" or "conveyor_u_splitter" => "🔀",
        "joiner" or "u_joiner" or "conveyor_u_joiner" or "conveyor_joiner" => "🔁",
        "pole" or "power_pole" => "⚡",
        "generator" or "hay_generator" => "🔥",
        "arm" or "arm_standard" or "arm_long" or "robotic_arm" => "🦾",
        "rake" or "piston_rake" => "🧹",
        "cabinet" or "needle_cabinet" => "🗄",
        "scanner" or "haystack_scanner" => "📡",
        "compressor" or "hay_compressor" => "🧱",
        "wrapper" or "hay_wrapper" => "🎁",
        "silo" or "hay_silo" => "🛢",
        "drone" or "hay_drone" => "🛸",
        "pelletizer" or "hay_pelletizer" => "⚙",
        "launcher" or "hay_launcher" => "🎯",
        "deck" => "▦", "stair" or "haystairs" => "🪜", "haylift" => "⏫", "wall" => "🧱",
        _ => "🏗",
    };

    void BuildGearTab()
    {
        var s = doc!.State;
        GearPage.Children.Clear();

        // tools as big toggle tiles
        if (s.Arr("owned_tools") is { } owned)
        {
            var have = owned.Items.OfType<string>().ToList();
            var panel = new StackPanel();
            panel.Children.Add(UiKit.Heading("CardTools", icon: "🧰"));
            panel.Children.Add(UiKit.Hint("ToolsHint"));
            var grid = UiKit.Tiles(6);
            foreach (var id in GameData.ToolIds.Union(have))
            {
                bool isOwned = have.Contains(id);
                var tile = new Border
                {
                    CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(1.5), Padding = new Thickness(10, 14, 10, 12),
                    Margin = new Thickness(0, 0, 10, 10), Cursor = System.Windows.Input.Cursors.Hand,
                    Background = UiKit.Brush(isOwned ? "AccentSoft" : "Panel2"), BorderBrush = UiKit.Brush(isOwned ? "Accent" : "Line"),
                    ToolTip = id,
                };
                var st = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                st.Children.Add(new TextBlock { Text = ToolIcon(id), FontSize = 34, HorizontalAlignment = HorizontalAlignment.Center, Opacity = isOwned ? 1 : 0.45 });
                st.Children.Add(new TextBlock { Text = GameData.TechName(id), FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 2) });
                st.Children.Add(new TextBlock { Text = Loc.T(isOwned ? "Owned" : "NotOwned"), FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center,
                                                Foreground = UiKit.Brush(isOwned ? "Good" : "Muted") });
                tile.Child = st;
                string tid = id;
                tile.MouseLeftButtonUp += (_, _) =>
                {
                    owned.Items.RemoveAll(x => x is string t && t == tid);
                    if (!isOwned) owned.Items.Add(tid);
                    MarkDirty(Loc.T("CardTools"));
                    BuildGearTab();
                };
                grid.Children.Add(tile);
            }
            panel.Children.Add(grid);
            GearPage.Children.Add(UiKit.Card(panel));
        }

        // hotbar as key caps
        if (s["hotbar"] is GPacked { Type: VType.PackedStringArray } hb)
        {
            var panel = new StackPanel();
            panel.Children.Add(UiKit.Heading("CardHotbar", icon: "⌨"));
            panel.Children.Add(UiKit.Hint("HotbarHint"));
            var ids = new List<string> { "" };
            ids.AddRange(GameData.BuildIds);
            foreach (var x in hb.Strings) if (!ids.Contains(x)) ids.Add(x);
            var choices = ids.Select(id => new KeyValuePair<string, string>(id, id.Length == 0 ? Loc.T("EmptySlot") : GameData.ItemName(id))).ToList();
            var row = new UniformGrid { Columns = 5, Margin = new Thickness(0, 0, -8, -8) };
            for (int i = 0; i < hb.Strings.Count; i++)
            {
                int slot = i;
                var cap = new Border
                {
                    Background = UiKit.Brush("Panel2"), BorderBrush = UiKit.Brush("Line"), BorderThickness = new Thickness(1, 1, 1, 4),
                    CornerRadius = new CornerRadius(10), Padding = new Thickness(8), Margin = new Thickness(0, 0, 8, 8),
                };
                var st = new StackPanel();
                var top = new DockPanel();
                top.Children.Add(new Border
                {
                    Background = UiKit.Brush("Bg"), CornerRadius = new CornerRadius(5), Padding = new Thickness(6, 1, 6, 1), HorizontalAlignment = HorizontalAlignment.Left,
                    Child = new TextBlock { Text = ((slot + 1) % 10).ToString(CultureInfo.InvariantCulture), FontWeight = FontWeights.Bold, FontSize = 11, Foreground = UiKit.Brush("Muted") },
                });
                st.Children.Add(top);
                var icon = new TextBlock { Text = BuildIcon(hb.Strings[slot]), FontSize = 26, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 6) };
                st.Children.Add(icon);
                var combo = new ComboBox { ItemsSource = choices, DisplayMemberPath = "Value", SelectedValuePath = "Key", SelectedValue = hb.Strings[slot], FontSize = 11.5 };
                combo.SelectionChanged += (_, _) =>
                {
                    if (combo.SelectedValue is not string t || t == hb.Strings[slot]) return;
                    hb.Strings[slot] = t;
                    icon.Text = BuildIcon(t);
                    MarkDirty(Loc.T("CardHotbar"));
                };
                st.Children.Add(combo);
                cap.Child = st;
                row.Children.Add(cap);
            }
            panel.Children.Add(row);
            GearPage.Children.Add(UiKit.Card(panel));
        }

        // gifts + new badges
        var two = new Grid();
        two.ColumnDefinitions.Add(new ColumnDefinition());
        two.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        two.ColumnDefinitions.Add(new ColumnDefinition());

        if (s.Dict("gifts") is { } gifts)
        {
            var panel = new StackPanel();
            panel.Children.Add(UiKit.Heading("CardGifts", icon: "🎁"));
            panel.Children.Add(UiKit.Hint("GiftsHint"));
            var wrap = new WrapPanel();
            foreach (var kv in gifts.Items.Where(kv => kv.Key is string).ToList())
            {
                string id = (string)kv.Key!;
                var chip = new StackPanel { Orientation = Orientation.Horizontal };
                chip.Children.Add(new TextBlock { Text = $"{BuildIcon(id)}  {GameData.ItemName(id)}", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
                var minus = UiKit.SmallButton("−", (_, _) =>
                {
                    double n = gifts.Num(id) - 1;
                    if (n <= 0) gifts.Remove(id); else gifts.SetNum(id, n);
                    MarkDirty(Loc.T("CardGifts")); BuildGearTab();
                }, literal: true);
                chip.Children.Add(minus);
                chip.Children.Add(new TextBlock { Text = "× " + Fmt.Int(gifts.Num(id)), FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 10, 0) });
                chip.Children.Add(UiKit.SmallButton("+", (_, _) => { gifts.SetNum(id, gifts.Num(id) + 1); MarkDirty(Loc.T("CardGifts")); BuildGearTab(); }, literal: true));
                wrap.Children.Add(new Border { Child = chip, Background = UiKit.Brush("Panel2"), BorderBrush = UiKit.Brush("Line"), BorderThickness = new Thickness(1),
                                               CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 6, 8, 6), Margin = new Thickness(0, 0, 8, 8) });
            }
            panel.Children.Add(wrap);
            var add = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
            var combo = new ComboBox { Width = 240, Margin = new Thickness(0, 0, 8, 0), DisplayMemberPath = "Value", SelectedValuePath = "Key",
                                       ItemsSource = GameData.BuildIds.Select(id => new KeyValuePair<string, string>(id, $"{BuildIcon(id)}  {GameData.ItemName(id)}")).ToList(), SelectedIndex = 0 };
            add.Children.Add(combo);
            add.Children.Add(UiKit.SmallButton("AddGift", (_, _) =>
            {
                if (combo.SelectedValue is not string id) return;
                gifts[id] = gifts.Has(id) ? gifts[id] : 0L;
                gifts.SetNum(id, gifts.Num(id) + 1);
                MarkDirty(Loc.T("CardGifts")); BuildGearTab();
            }));
            panel.Children.Add(add);
            two.Children.Add(UiKit.Card(panel, new Thickness(0)));
        }

        {
            var panel = new StackPanel();
            var fresh = s["fresh_builds"] as GPacked;
            panel.Children.Add(UiKit.Heading("CardNew", icon: "✨"));
            panel.Children.Add(new TextBlock { Text = Loc.T("NewCount", fresh?.Count ?? 0), Foreground = UiKit.Brush("Muted"), Margin = new Thickness(0, 0, 0, 10) });
            var wrap = new WrapPanel();
            foreach (var id in fresh?.Strings ?? []) wrap.Children.Add(UiKit.Chip($"{BuildIcon(id)}  {GameData.ItemName(id)}"));
            panel.Children.Add(wrap);
            var clear = UiKit.SmallButton("ClearNew", (_, _) =>
            {
                fresh?.Strings.Clear();
                s.Dict("fresh_since")?.Items.Clear();
                MarkDirty(Loc.T("CardNew")); BuildGearTab();
            });
            clear.HorizontalAlignment = HorizontalAlignment.Left;
            clear.Margin = new Thickness(0, 4, 0, 0);
            panel.Children.Add(clear);
            var card = UiKit.Card(panel, new Thickness(0));
            Grid.SetColumn(card, 2);
            two.Children.Add(card);
        }
        GearPage.Children.Add(two);
    }

    // ================================================================ buildings

    readonly HashSet<string> expandedGroups = [];

    void BuildBuildingsTab()
    {
        BuildingsPage.Children.Clear();
        if (doc!.Buildings is not { } list) return;
        var all = list.Items.OfType<GDict>().ToList();

        // summary tiles
        var sumCard = new StackPanel();
        sumCard.Children.Add(UiKit.Heading("BuildingsTitle", icon: "🏗"));
        sumCard.Children.Add(UiKit.Hint("BuildingsHint"));
        var stats = UiKit.Tiles(4);
        void Stat(string icon, string key, string value, Brush? b = null)
        {
            var t = new Border { Background = UiKit.Brush("Panel2"), BorderBrush = UiKit.Brush("Line"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10),
                                 Padding = new Thickness(14, 11, 14, 11), Margin = new Thickness(0, 0, 10, 10) };
            var sp = new StackPanel();
            sp.Children.Add(new TextBlock { Text = icon + "  " + Loc.T(key), Foreground = UiKit.Brush("Muted"), FontSize = 11.5 });
            sp.Children.Add(new TextBlock { Text = value, FontSize = 21, FontWeight = FontWeights.SemiBold, Foreground = b ?? UiKit.Brush("Text"), Margin = new Thickness(0, 4, 0, 0) });
            t.Child = sp;
            stats.Children.Add(t);
        }
        Stat("🏗", "StatBuildings", Fmt.Int(all.Count));
        Stat("🟢", "StatRunning", $"{all.Count(b => b.Has("off") && !b.Bool("off"))} / {all.Count(b => b.Has("off"))}", UiKit.Brush("Good"));
        Stat("💲", "StatInvested", "$ " + Fmt.Money(all.Sum(b => b.Num("paid"))), UiKit.Brush("Gold"));
        Stat("🔥", "StatFuel", Fmt.Int(all.Where(b => b.Has("fuel")).Sum(b => b.Num("fuel"))), UiKit.Brush("Warn"));
        sumCard.Children.Add(stats);
        var actions = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
        actions.Children.Add(UiKit.SmallButton("AllOn", (_, _) => { SetAllOff(all, false); }));
        var fuelBox = new TextBox { Width = 80, Text = "3000", Margin = new Thickness(0, 0, 6, 0), Padding = new Thickness(6, 3, 6, 3) };
        actions.Children.Add(UiKit.SmallButton("Refuel", (_, _) => Refuel(all, fuelBox.Text)));
        actions.Children.Add(fuelBox);
        sumCard.Children.Add(actions);
        BuildingsPage.Children.Add(UiKit.Card(sumCard));

        // one card per building type, machines first
        var groups = all.Select((b, i) => (b, i)).GroupBy(t => t.b.Str("type") ?? "?")
            .OrderBy(g => g.First().b.Has("a") ? 2 : g.Key == "power_pole" ? 1 : 0).ThenByDescending(g => g.Count());
        foreach (var g in groups)
        {
            var items = g.ToList();
            bool editable = items.Any(t => t.b.Has("fuel") || t.b.Has("stored") || t.b.Has("tier"));
            bool expanded = expandedGroups.Contains(g.Key) || (editable && items.Count <= 12);
            var panel = new StackPanel();

            var right = new StackPanel { Orientation = Orientation.Horizontal };
            if (items.Any(t => t.b.Has("off")))
            {
                right.Children.Add(UiKit.SmallButton("AllOn", (_, _) => SetAllOff(items.Select(t => t.b), false)));
                right.Children.Add(UiKit.SmallButton("AllOff", (_, _) => SetAllOff(items.Select(t => t.b), true)));
            }
            string key = g.Key;
            right.Children.Add(UiKit.SmallButton(expanded ? "HideDetails" : "ShowDetails", (_, _) =>
            {
                if (!expandedGroups.Remove(key)) expandedGroups.Add(key);
                if (expanded) expandedGroups.Remove(key);
                if (!expanded) expandedGroups.Add(key);
                BuildBuildingsTab();
            }));

            var head = new DockPanel { Margin = new Thickness(0, 0, 0, expanded ? 12 : 0) };
            DockPanel.SetDock(right, Dock.Right);
            head.Children.Add(right);
            var title = new TextBlock { VerticalAlignment = VerticalAlignment.Center, FontSize = 15, FontWeight = FontWeights.SemiBold };
            title.Inlines.Add(new System.Windows.Documents.Run($"{BuildIcon(g.Key)}  {GameData.BuildingName(g.Key)}"));
            title.Inlines.Add(new System.Windows.Documents.Run($"   ×{items.Count}") { Foreground = UiKit.Brush("Accent") });
            int on = items.Count(t => t.b.Has("off") && !t.b.Bool("off")), switchable = items.Count(t => t.b.Has("off"));
            if (switchable > 0) title.Inlines.Add(new System.Windows.Documents.Run($"    {on}/{switchable} {Loc.T("On")}") { Foreground = UiKit.Brush("Muted"), FontSize = 12, FontWeight = FontWeights.Normal });
            head.Children.Add(title);
            panel.Children.Add(head);

            if (expanded)
            {
                double maxFuel = Math.Max(1, items.Max(t => t.b.Num("fuel")));
                var wrap = UiKit.Tiles(4);
                int n = 1;
                foreach (var (b, _) in items) wrap.Children.Add(BuildingCard(b, n++, maxFuel));
                panel.Children.Add(wrap);
            }
            BuildingsPage.Children.Add(UiKit.Card(panel));
        }
    }

    Border BuildingCard(GDict b, int no, double maxFuel)
    {
        var sp = new StackPanel();
        var head = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        if (b.Has("off"))
        {
            var sw = new CheckBox { Style = (Style)FindResource("Switch"), IsChecked = !b.Bool("off"), VerticalAlignment = VerticalAlignment.Center };
            sw.Click += (_, _) => { b["off"] = sw.IsChecked != true; MarkDirty(Loc.T("TabBuildings").Trim()); };
            DockPanel.SetDock(sw, Dock.Right);
            head.Children.Add(sw);
        }
        var where = b["position"] as GMath ?? b["a"] as GMath;
        var t = new TextBlock { FontWeight = FontWeights.SemiBold };
        t.Inlines.Add(new System.Windows.Documents.Run($"#{no}"));
        if (where is not null)
            t.Inlines.Add(new System.Windows.Documents.Run(string.Format(Loc.Instance.Culture, "   {0:0.0} / {1:0.0}", where.V[0], where.V[2]))
                          { Foreground = UiKit.Brush("Muted"), FontWeight = FontWeights.Normal, FontSize = 11 });
        head.Children.Add(t);
        sp.Children.Add(head);

        void Num(string key, string labelKey, string icon, Brush? brush = null, double barMax = 0)
        {
            if (b[key] is not (long or double)) return;
            var tile = new ValueTile(icon, labelKey, () => b.Num(key), v =>
            {
                if (v < 0 || v > 1e12) return false;
                b.SetNum(key, v);
                MarkDirty(Loc.T(labelKey));
                return true;
            }, b[key] is long ? ValueKind.Integer : ValueKind.Number, brush, 15)
            { Margin = new Thickness(0, 0, 0, 6), Padding = new Thickness(10, 6, 10, 6), Background = UiKit.Brush("Bg") };
            if (barMax > 0) tile.WithBar(barMax);
            sp.Children.Add(tile);
        }
        Num("fuel", "L_fuel", "🔥", UiKit.Brush("Warn"), maxFuel);
        Num("stored", "L_stored", "📦");
        Num("tier", "L_tier", "⭐");
        if (b.Has("trips")) sp.Children.Add(new TextBlock { Text = Loc.T("Trips", Fmt.Int(b.Num("trips"))), Foreground = UiKit.Brush("Muted"), FontSize = 11 });
        if (b.Has("paid")) sp.Children.Add(new TextBlock { Text = "$ " + Fmt.Money(b.Num("paid")), Foreground = UiKit.Brush("Gold"), FontSize = 11, Margin = new Thickness(0, 2, 0, 0) });

        return new Border
        {
            Child = sp, Background = UiKit.Brush("Panel2"), BorderBrush = UiKit.Brush(b.Bool("off") ? "Line" : "AccentSoft"), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 10, 12, 10), Margin = new Thickness(0, 0, 10, 10),
            Opacity = b.Bool("off") ? 0.6 : 1,
        };
    }

    void SetAllOff(IEnumerable<GDict> items, bool off)
    {
        int n = 0;
        foreach (var b in items) if (b.Has("off") && b.Bool("off") != off) { b["off"] = off; n++; }
        if (n > 0) MarkDirty(Loc.T("TabBuildings").Trim());
        BuildBuildingsTab();
    }

    void Refuel(IEnumerable<GDict> items, string text)
    {
        if (!Fmt.TryParse(text, out var v) || v < 0 || v > 1e9)
        {
            SetStatus(Loc.T("InvalidInput", text), UiKit.Brush("Bad"));
            return;
        }
        int n = 0;
        foreach (var b in items.Where(b => b.Has("fuel"))) { b.SetNum("fuel", v); n++; }
        if (n > 0) MarkDirty(Loc.T("L_fuel"));
        BuildBuildingsTab();
    }

    // ================================================================ missions

    void BuildMissionsTab()
    {
        var s = doc!.State;
        MissionsPage.Children.Clear();

        // hero: current mission
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
            var left = new StackPanel();
            left.Children.Add(new TextBlock { Text = "🎯  " + Loc.T("CardMission"), Foreground = UiKit.Brush("Muted"), FontSize = 12 });
            var id = s.Str("mission_id") ?? "";
            left.Children.Add(new TextBlock { Text = id.Length > 0 ? Fmt.Pretty(id) : "–", FontSize = 30, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 4, 0, 4) });
            left.Children.Add(UiKit.Hint("MissionHint"));
            var idRow = new StackPanel { Orientation = Orientation.Horizontal };
            idRow.Children.Add(new TextBlock { Text = Loc.T("T_mission_id"), Foreground = UiKit.Brush("Muted"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
            var idBox = new TextBox { Text = id, Width = 220, FontFamily = new FontFamily("Consolas") };
            void CommitId() { if (idBox.Text.Trim() != (s.Str("mission_id") ?? "") && s.Has("mission_id")) { s["mission_id"] = idBox.Text.Trim(); MarkDirty(Loc.T("T_mission_id")); BuildMissionsTab(); } }
            idBox.LostFocus += (_, _) => CommitId();
            idBox.KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) CommitId(); };
            idRow.Children.Add(idBox);
            left.Children.Add(idRow);
            g.Children.Add(left);
            var rt = new StackPanel();
            AddTo(rt, Tile(s, "mission_index", "#"), Tile(s, "mission_earned_from", "📍"));
            Grid.SetColumn(rt, 1);
            g.Children.Add(rt);
            MissionsPage.Children.Add(UiKit.Card(g));
        }

        // contracts + lists
        var two = new Grid();
        two.ColumnDefinitions.Add(new ColumnDefinition());
        two.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        two.ColumnDefinitions.Add(new ColumnDefinition());

        var con = new StackPanel();
        con.Children.Add(UiKit.Heading("CardContracts", icon: "📜"));
        var ct = UiKit.Tiles(2);
        AddTo(ct, Tile(s, "contract_index", "#"), Tile(s, "contract_delivered", "📦"));
        con.Children.Add(ct);
        ChipList(con, s, "contracts_done", "✅");
        ChipList(con, s, "missions_paid", "💵");
        ChipList(con, s, "gifts_given", "🎁");
        two.Children.Add(UiKit.Card(con, new Thickness(0)));

        // spending analysis from the purchase log
        var sp = new StackPanel();
        sp.Children.Add(UiKit.Heading("CardLog", icon: "🧾"));
        if (s.Arr("purchase_log") is { } log)
        {
            var entries = log.Items.OfType<GArray>().Where(e => e.Items.Count > 4)
                .Select(e => (Kind: e.Items[1] as string ?? "?", Id: e.Items[2] as string ?? "?", Cost: e.Items[4] switch { double d => d, long l => l, _ => 0.0 })).ToList();
            double total = entries.Sum(e => e.Cost);
            var tt = UiKit.Tiles(2);
            void Info(string icon, string key, string value, Brush? b = null)
            {
                var bd = new Border { Background = UiKit.Brush("Panel2"), BorderBrush = UiKit.Brush("Line"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10),
                                      Padding = new Thickness(14, 11, 14, 11), Margin = new Thickness(0, 0, 10, 10) };
                var st = new StackPanel();
                st.Children.Add(new TextBlock { Text = icon + "  " + Loc.T(key), Foreground = UiKit.Brush("Muted"), FontSize = 11.5 });
                st.Children.Add(new TextBlock { Text = value, FontSize = 21, FontWeight = FontWeights.SemiBold, Foreground = b ?? UiKit.Brush("Text"), Margin = new Thickness(0, 4, 0, 0) });
                bd.Child = st; tt.Children.Add(bd);
            }
            Info("💸", "SpentTotal", "$ " + Fmt.Money(total), UiKit.Brush("Gold"));
            Info("🛒", "Purchases", Fmt.Int(entries.Count));
            sp.Children.Add(tt);

            // by kind
            sp.Children.Add(new TextBlock { Text = Loc.T("ByKind"), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 6, 0, 8) });
            foreach (var k in entries.GroupBy(e => e.Kind).OrderByDescending(g => g.Sum(e => e.Cost)))
                sp.Children.Add(SpendRow(Loc.Has("Kind_" + k.Key) ? Loc.T("Kind_" + k.Key) : Fmt.Pretty(k.Key), k.Count(), k.Sum(e => e.Cost), total, UiKit.Brush("Accent")));

            sp.Children.Add(new TextBlock { Text = Loc.T("TopSpend"), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 12, 0, 8) });
            var top = entries.GroupBy(e => e.Id).Select(g => (Id: g.Key, N: g.Count(), Sum: g.Sum(e => e.Cost))).OrderByDescending(x => x.Sum).Take(8).ToList();
            double maxTop = top.Count > 0 ? top[0].Sum : 1;
            foreach (var x in top)
                sp.Children.Add(SpendRow($"{BuildIcon(x.Id)}  {GameData.ItemName(x.Id)}", x.N, x.Sum, maxTop, UiKit.Brush("Gold")));
        }
        var lc = UiKit.Card(sp, new Thickness(0));
        Grid.SetColumn(lc, 2);
        two.Children.Add(lc);
        MissionsPage.Children.Add(two);
    }

    static Grid SpendRow(string name, int count, double sum, double max, Brush fill)
    {
        var g = new Grid { Margin = new Thickness(0, 0, 0, 7) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        g.Children.Add(new TextBlock { Text = $"{name}  ", TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center });
        var bar = UiKit.Bar(max > 0 ? sum / max : 0, fill, 120);
        Grid.SetColumn(bar, 1);
        g.Children.Add(bar);
        var val = new TextBlock { Text = $"$ {Fmt.Money(sum)}  ·  {count}×", Foreground = UiKit.Brush("Muted"), FontSize = 11.5, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(val, 2);
        g.Children.Add(val);
        return g;
    }

    /// <summary>Editable chip list for an Array of strings / PackedStringArray.</summary>
    void ChipList(Panel host, GDict dict, string key, string icon)
    {
        List<string>? items = dict[key] switch
        {
            GArray a when a.Items.All(i => i is string) => a.Items.Cast<string>().ToList(),
            GPacked { Type: VType.PackedStringArray } p => p.Strings.ToList(),
            _ => null,
        };
        if (items is null) return;
        void Store(List<string> list)
        {
            switch (dict[key])
            {
                case GArray a: a.Items.Clear(); a.Items.AddRange(list); break;
                case GPacked p: p.Strings.Clear(); p.Strings.AddRange(list); break;
            }
            MarkDirty(Loc.T("L_" + key));
            BuildMissionsTab();
        }
        var label = new TextBlock { Margin = new Thickness(0, 8, 0, 6), FontWeight = FontWeights.SemiBold };
        label.Inlines.Add(new System.Windows.Documents.Run(icon + "  " + Loc.T("L_" + key)));
        label.Inlines.Add(new System.Windows.Documents.Run($"  {items.Count}") { Foreground = UiKit.Brush("Accent") });
        host.Children.Add(label);
        var wrap = new WrapPanel();
        foreach (var it in items)
        {
            string v = it;
            wrap.Children.Add(UiKit.Chip(Fmt.Pretty(v), () => { var l = items.ToList(); l.Remove(v); Store(l); }, tooltip: v));
        }
        var box = new TextBox { Width = 170, Padding = new Thickness(8, 3, 8, 3), ToolTip = Loc.T("AddEntryHint") };
        var ph = new TextBlock { Text = "＋ " + Loc.T("AddEntry"), Foreground = UiKit.Brush("Muted"), IsHitTestVisible = false, Margin = new Thickness(11, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, FontSize = 12 };
        box.TextChanged += (_, _) => ph.Visibility = box.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        var boxHost = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        boxHost.Children.Add(box); boxHost.Children.Add(ph);
        box.KeyDown += (_, e) =>
        {
            if (e.Key != System.Windows.Input.Key.Enter) return;
            var t = box.Text.Trim();
            if (t.Length == 0 || items.Contains(t)) return;
            var l = items.ToList(); l.Add(t); Store(l);
        };
        wrap.Children.Add(boxHost);
        host.Children.Add(wrap);
    }
}
