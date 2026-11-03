using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using FindTheNeedleEditor.Core;
using Microsoft.Win32;

namespace FindTheNeedleEditor;

public partial class MainWindow : Window
{
    readonly AppSettings settings = AppSettings.Load();
    readonly DispatcherTimer gameTimer = new() { Interval = TimeSpan.FromSeconds(2) };

    SaveDoc? doc;
    ProfileCfg? profile;
    bool dirty, profileDirty, gameRunning, suppressSlotChange;

    string Root => App.SaveRoot;

    public MainWindow()
    {
        InitializeComponent();
        DarkTitleBar.Apply(this);

        var lang = App.LanguageOverride switch
        {
            "de" => UiLanguage.German,
            "en" => UiLanguage.English,
            _ => settings.ResolveLanguage(),
        };
        Loc.Instance.SetLanguage(lang);
        UpdateFlags();
        SaveCrypto.ExtraPasswords.AddRange(settings.ExtraPasswords);

        Map.MapClicked += Map_Clicked;
        Map.HoverText += t => HoverInfo.Text = t;

        gameTimer.Tick += (_, _) => UpdateGameState();
        gameTimer.Start();

        Loaded += async (_, _) =>
        {
            UpdateGameState();
            LoadSlots(null);
            if (App.SnapshotDir is not null) await SnapshotAsync(App.SnapshotDir);
        };
        Closing += (_, e) =>
        {
            if ((dirty || profileDirty) && App.SnapshotDir is null &&
                MessageBox.Show(this, Loc.T("DiscardQuestion"), Loc.T("DiscardTitle"), MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                e.Cancel = true;
        };
    }

    // ================================================================ language

    void UpdateFlags()
    {
        FlagDe.Tag = Loc.Instance.Language == UiLanguage.German ? "active" : null;
        FlagEn.Tag = Loc.Instance.Language == UiLanguage.English ? "active" : null;
    }

    void SwitchLanguage(UiLanguage lang)
    {
        Loc.Instance.SetLanguage(lang);
        settings.Language = AppSettings.Code(lang);
        settings.Save();
        UpdateFlags();
        var keepDirty = dirty;
        LoadSlots(doc?.Path, reloadDoc: false);
        RebuildAll();
        dirty = keepDirty;
        UpdateDirtyUi();
    }

    void LanguageGerman_Click(object sender, RoutedEventArgs e) => SwitchLanguage(UiLanguage.German);
    void LanguageEnglish_Click(object sender, RoutedEventArgs e) => SwitchLanguage(UiLanguage.English);

    // ================================================================ status

    void SetStatus(string text, Brush? dot = null)
    {
        StatusText.Text = text;
        StatusDot.Fill = dot ?? (Brush)FindResource("Muted");
    }

    void UpdateGameState()
    {
        gameRunning = !App.IgnoreGame && SaveStore.IsGameRunning();
        GameRunningBanner.Visibility = gameRunning ? Visibility.Visible : Visibility.Collapsed;
        UpdateDirtyUi();
    }

    void UpdateDirtyUi()
    {
        bool any = dirty || profileDirty;
        SaveButton.IsEnabled = any && !gameRunning;
        DiscardButton.IsEnabled = any;
    }

    void MarkDirty(string? what = null)
    {
        dirty = true;
        UpdateDirtyUi();
        UpdateHeader();
        SetStatus(what is null ? Loc.T("Unsaved") : Loc.T("Changed", what), (Brush)FindResource("Warn"));
    }

    void MarkProfileDirty()
    {
        profileDirty = true;
        UpdateDirtyUi();
        SetStatus(Loc.T("Unsaved"), (Brush)FindResource("Warn"));
    }

    bool ConfirmDiscard()
    {
        if (!dirty && !profileDirty) return true;
        return MessageBox.Show(this, Loc.T("DiscardQuestion"), Loc.T("DiscardTitle"), MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    }

    // ================================================================ slots

    void LoadSlots(string? select, bool reloadDoc = true)
    {
        suppressSlotChange = true;
        var slots = SaveStore.ListSlots(Root);
        SlotList.ItemsSource = slots;
        suppressSlotChange = false;
        FindKeyButton.Visibility = slots.Any(s => s.Error is not null && s.Error.Contains("Checksum")) ? Visibility.Visible : Visibility.Collapsed;

        if (slots.Count == 0 && doc is null)
        {
            SetStatus(Loc.T("NoSavesFound", SaveStore.SavesDir(Root)), (Brush)FindResource("Bad"));
            return;
        }
        var pick = slots.FirstOrDefault(s => string.Equals(s.Path, select, StringComparison.OrdinalIgnoreCase))
                   ?? (select is null ? slots.FirstOrDefault(s => s.Error is null) : null);
        suppressSlotChange = true;
        SlotList.SelectedItem = pick;
        suppressSlotChange = false;
        if (reloadDoc && pick is not null) LoadDoc(pick.Path);
        else if (reloadDoc && select is not null && File.Exists(select)) LoadDoc(select);
    }

    void SlotList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suppressSlotChange || SlotList.SelectedItem is not SlotInfo s) return;
        if (doc is not null && string.Equals(doc.Path, s.Path, StringComparison.OrdinalIgnoreCase)) return;
        if (!ConfirmDiscard())
        {
            suppressSlotChange = true;
            SlotList.SelectedItem = e.RemovedItems.Count > 0 ? e.RemovedItems[0] : null;
            suppressSlotChange = false;
            return;
        }
        LoadDoc(s.Path);
    }

    void LoadDoc(string path)
    {
        try
        {
            doc = SaveDoc.Load(path);
        }
        catch (Exception ex)
        {
            doc = null;
            Tabs.IsEnabled = false;
            Map.SetDoc(null);
            SetStatus(Loc.T("CannotEdit", ex.Message), (Brush)FindResource("Bad"));
            return;
        }
        profile = ProfileCfg.TryLoad(SaveStore.ProfilePath(Root));
        dirty = profileDirty = false;
        Tabs.IsEnabled = true;
        Map.SetDoc(doc);
        RebuildAll();
        UpdateDirtyUi();
        SetStatus(Loc.T("SlotLoaded", Path.GetFileName(path)), (Brush)FindResource("Good"));
    }

    async void FindKey_Click(object sender, RoutedEventArgs e)
    {
        var slot = (SlotList.ItemsSource as IEnumerable<SlotInfo>)?.FirstOrDefault(s => s.Error is not null)?.Path ?? doc?.Path;
        if (slot is null) return;
        if (!SaveStore.IsGameRunning())
        {
            MessageBox.Show(this, Loc.T("FindKeyNeedGame"), Loc.T("FindKey"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        FindKeyButton.IsEnabled = false;
        try
        {
            var file = File.ReadAllBytes(slot);
            var progress = new Progress<double>(p => SetStatus(Loc.T("FindKeyProgress", p * 100), UiKit.Brush("Warn")));
            var pass = await Task.Run(() => KeyFinder.Find(file, progress));
            if (pass is null)
            {
                SetStatus(Loc.T("FindKeyNone"), UiKit.Brush("Bad"));
                return;
            }
            if (!settings.ExtraPasswords.Contains(pass)) settings.ExtraPasswords.Add(pass);
            settings.Save();
            if (!SaveCrypto.ExtraPasswords.Contains(pass)) SaveCrypto.ExtraPasswords.Add(pass);
            LoadSlots(slot);
            SetStatus(Loc.T("FindKeyOk"), UiKit.Brush("Good"));
        }
        catch (Exception ex) { SetStatus(ex.Message, UiKit.Brush("Bad")); }
        finally { FindKeyButton.IsEnabled = true; }
    }

    void Reload_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmDiscard()) return;
        dirty = profileDirty = false;
        var keep = doc?.Path;
        doc = null;
        LoadSlots(keep);
    }

    void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmDiscard()) return;
        var dlg = new OpenFileDialog
        {
            Filter = "Find The Needle save (*.dat;*.bak)|*.dat;*.bak|*.*|*.*",
            InitialDirectory = Directory.Exists(SaveStore.SavesDir(Root)) ? SaveStore.SavesDir(Root) : null,
        };
        if (dlg.ShowDialog(this) != true) return;
        dirty = profileDirty = false;
        suppressSlotChange = true;
        SlotList.SelectedItem = null;
        suppressSlotChange = false;
        LoadDoc(dlg.FileName);
    }

    void OpenSaveFolder_Click(object sender, RoutedEventArgs e) => OpenFolder(Directory.Exists(SaveStore.SavesDir(Root)) ? SaveStore.SavesDir(Root) : Root);

    static void OpenFolder(string dir)
    {
        try
        {
            Directory.CreateDirectory(dir);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
        }
        catch (Exception) { /* nothing to do */ }
    }

    // ================================================================ save / discard

    void Save_Click(object sender, RoutedEventArgs e)
    {
        if (doc is null) return;
        try
        {
            if (!App.IgnoreGame && SaveStore.IsGameRunning())
            {
                MessageBox.Show(this, Loc.T("GameRunningSave"), Loc.T("SaveFailed"), MessageBoxButton.OK, MessageBoxImage.Warning);
                UpdateGameState();
                return;
            }
            FocusManager.SetFocusedElement(this, this); // commit pending text box edits
            Keyboard.ClearFocus();

            // stale checks first, so nothing is half written
            if ((dirty && File.Exists(doc.Path) && File.GetLastWriteTimeUtc(doc.Path) != doc.LoadedWriteTime)
                || (profile is not null && File.Exists(profile.Path) && File.GetLastWriteTimeUtc(profile.Path) != profile.LoadedWriteTime))
            {
                MessageBox.Show(this, Loc.T("ChangedOnDisk"), Loc.T("SaveFailed"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string backupName = "–";
            if (dirty)
            {
                doc.MarkEdited(); // fair play: an edited run never counts for the leaderboards
                var bytes = doc.Build();
                if (File.Exists(doc.Path)) backupName = Path.GetFileName(SaveStore.Backup(Root, doc.Path));
                SaveStore.WriteAtomic(doc.Path, bytes);
            }
            // fair play: mark this PC like the game does when mods or the debug menu were used
            bool tainted = profile?.MarkTainted() == true;
            if ((profileDirty || tainted) && profile is not null) profile.Save();
            var path = doc.Path;
            dirty = profileDirty = false;
            LoadSlots(path);
            SetStatus(Loc.T("Saved", backupName), (Brush)FindResource("Good"));
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, Loc.T("SaveFailed"), MessageBoxButton.OK, MessageBoxImage.Error);
            SetStatus(Loc.T("SaveFailedStatus", ex.Message), (Brush)FindResource("Bad"));
        }
    }

    void Discard_Click(object sender, RoutedEventArgs e)
    {
        if (doc is null || !ConfirmDiscard()) return;
        dirty = profileDirty = false;
        LoadDoc(doc.Path);
    }

    // ================================================================ build all

    void RebuildAll()
    {
        if (doc is null) return;
        UpdateHeader();
        BuildMoneyTab();
        BuildNeedlesTab();
        BuildMapTab();
        BuildTechTab();
        BuildGearTab();
        BuildBuildingsTab();
        BuildMissionsTab();
        BuildCareerTab();
        BuildBackupsTab();
        BuildRawTab();
    }

    /// <summary>Everything except the raw tree (keeps its expansion state).</summary>
    void RebuildEditors()
    {
        if (doc is null) return;
        UpdateHeader();
        BuildMoneyTab();
        BuildNeedlesTab();
        BuildMapTab();
        BuildTechTab();
        BuildGearTab();
        BuildBuildingsTab();
        BuildMissionsTab();
    }

    void UpdateHeader()
    {
        if (doc is null) return;
        var name = Path.GetFileNameWithoutExtension(doc.Path);
        HeaderTitle.Text = name.StartsWith("slot_") && int.TryParse(name.AsSpan(5), out int idx) ? Loc.T("SlotTitle", idx + 1) : name;
        var meta = doc.Meta;
        HeaderPath.Text = App.ShowPath(Path.GetFullPath(doc.Path)) + (meta is null ? "" : "   ·   " + Loc.T("PileDesc", meta.Str("map") ?? "?", meta.Str("pile_size") ?? "?"));
        HeaderMoney.Text = "$ " + Fmt.Money(doc.State.Num("money"));
        int taken = Enumerable.Range(0, doc.PileNeedleCount).Count(doc.IsNeedleTaken);
        HeaderNeedles.Text = $"{taken} / {doc.PileNeedleCount}";
        HeaderPlayTime.Text = Fmt.Duration(doc.State.Num("run_secs"));
        long savedAt = meta?.Int("saved_at") ?? 0;
        HeaderSavedAt.Text = savedAt > 0 ? DateTimeOffset.FromUnixTimeSeconds(savedAt).LocalDateTime.ToString("g", Loc.Instance.Culture) : "–";
    }

    // ================================================================ field helpers

    static Grid FieldRow(string labelKey, UIElement editor)
    {
        var g = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
        var label = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0), TextWrapping = TextWrapping.Wrap };
        label.SetBinding(TextBlock.TextProperty, TExtension.Bind(labelKey));
        g.Children.Add(label);
        Grid.SetColumn(editor, 1);
        g.Children.Add(editor);
        return g;
    }

    /// <summary>Number field bound to dict[key]; skipped when the save has no such key.</summary>
    void NumField(Panel host, GDict? dict, string key, string? labelKey = null, double min = 0, Action? after = null)
    {
        if (dict is null || !dict.Has(key) || dict[key] is not (long or double)) return;
        bool isInt = dict[key] is long;
        var box = new TextBox { Text = EditText(dict[key]) };
        string original = box.Text;
        void Commit()
        {
            if (box.Text == original) return;
            if (!Fmt.TryParse(box.Text, out var v) || v < min || (isInt && Math.Abs(v) > 9e18))
            {
                box.BorderBrush = (Brush)FindResource("Bad");
                SetStatus(Loc.T("InvalidInput", box.Text), (Brush)FindResource("Bad"));
                return;
            }
            box.ClearValue(BorderBrushProperty);
            dict.SetNum(key, v);
            box.Text = original = EditText(dict[key]);
            MarkDirty(Loc.T(labelKey ?? "F_" + key));
            after?.Invoke();
        }
        box.LostFocus += (_, _) => Commit();
        box.KeyDown += (_, e) => { if (e.Key == Key.Enter) Commit(); };
        host.Children.Add(FieldRow(labelKey ?? "F_" + key, box));
    }

    static string MoneyText(object? v) => v switch
    {
        long l => l.ToString(Loc.Instance.Culture),
        double d => d.ToString("0.##", Loc.Instance.Culture),
        _ => "",
    };

    static string EditText(object? v) => v switch
    {
        long l => l.ToString(Loc.Instance.Culture),
        double d => d.ToString("0.######", Loc.Instance.Culture),
        _ => "",
    };

    void TextField(Panel host, GDict? dict, string key, string? labelKey = null)
    {
        if (dict is null || dict[key] is not string s) return;
        var box = new TextBox { Text = s };
        void Commit()
        {
            if (box.Text == dict.Str(key)) return;
            dict[key] = box.Text.Trim();
            MarkDirty(Loc.T(labelKey ?? "T_" + key));
        }
        box.LostFocus += (_, _) => Commit();
        box.KeyDown += (_, e) => { if (e.Key == Key.Enter) Commit(); };
        host.Children.Add(FieldRow(labelKey ?? "T_" + key, box));
    }

    void CheckField(Panel host, GDict? dict, string key, string? labelKey = null)
    {
        if (dict is null || dict[key] is not bool b) return;
        var cb = new CheckBox { IsChecked = b, Margin = new Thickness(0, 0, 0, 8) };
        cb.SetBinding(ContentControl.ContentProperty, TExtension.Bind(labelKey ?? "C_" + key));
        cb.Click += (_, _) => { dict[key] = cb.IsChecked == true; MarkDirty(Loc.T(labelKey ?? "C_" + key)); };
        host.Children.Add(cb);
    }

    /// <summary>Comma separated list editor for an Array of strings or a PackedStringArray.</summary>
    void ListField(Panel host, GDict? dict, string key, string? labelKey = null)
    {
        if (dict is null) return;
        List<string>? items = dict[key] switch
        {
            GArray a when a.Items.All(i => i is string) => a.Items.Cast<string>().ToList(),
            GPacked { Type: VType.PackedStringArray } p => p.Strings,
            _ => null,
        };
        if (items is null) return;
        var label = new TextBlock { Margin = new Thickness(0, 4, 0, 4), FontWeight = FontWeights.SemiBold };
        label.SetBinding(TextBlock.TextProperty, TExtension.Bind(labelKey ?? "L_" + key));
        var box = new TextBox { Text = string.Join(", ", items), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10), MinHeight = 34 };
        string original = box.Text;
        void Commit()
        {
            if (box.Text == original) return;
            var list = box.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            switch (dict[key])
            {
                case GArray a: a.Items.Clear(); a.Items.AddRange(list); break;
                case GPacked p: p.Strings.Clear(); p.Strings.AddRange(list); break;
            }
            original = box.Text = string.Join(", ", list);
            MarkDirty(Loc.T(labelKey ?? "L_" + key));
        }
        box.LostFocus += (_, _) => Commit();
        box.KeyDown += (_, e) => { if (e.Key == Key.Enter) Commit(); };
        host.Children.Add(label);
        host.Children.Add(box);
    }

    // ================================================================ needles tab

    void BuildNeedlesTab()
    {
        var d = doc!;
        NeedleGrid.ItemsSource = Enumerable.Range(0, d.PileNeedleCount).Select(i => new NeedleRow(d, i, NeedleChanged)).ToList();
        TypeGrid.ItemsSource = Enumerable.Range(0, d.NeedleTypeCount).Select(i => new NeedleTypeRow(d, i, () => MarkDirty(Loc.T("CardCollection")))).ToList();
        NeedleCountersHost.Children.Clear();
        NumField(NeedleCountersHost, d.State, "needles_found");
        NumField(NeedleCountersHost, d.State, "pile_needles_found");
    }

    void NeedleChanged()
    {
        MarkDirty(Loc.T("CardPile"));
        // counters and collection changed too
        Dispatcher.BeginInvoke(() =>
        {
            if (doc is null) return;
            TypeGrid.ItemsSource = Enumerable.Range(0, doc.NeedleTypeCount).Select(i => new NeedleTypeRow(doc, i, () => MarkDirty(Loc.T("CardCollection")))).ToList();
            NeedleCountersHost.Children.Clear();
            NumField(NeedleCountersHost, doc.State, "needles_found");
            NumField(NeedleCountersHost, doc.State, "pile_needles_found");
            BuildMapSide();
            Map.Refresh(); Refresh3D();
        }, DispatcherPriority.Background);
    }

    void FindAll_Click(object sender, RoutedEventArgs e)
    {
        if (doc is null) return;
        int n = 0;
        for (int i = 0; i < doc.PileNeedleCount; i++)
            if (!doc.IsNeedleTaken(i)) { doc.TakeNeedle(i); n++; }
        if (n == 0) { SetStatus(Loc.T("NoHiddenNeedle")); return; }
        MarkDirty();
        RebuildEditors();
        SetStatus(Loc.T("NeedlesFoundAll", n), (Brush)FindResource("Warn"));
    }

    void DiscoverAll_Click(object sender, RoutedEventArgs e)
    {
        if (doc?.Discovered is not { } d) return;
        Array.Fill(d.Bytes, (byte)1);
        MarkDirty(Loc.T("CardCollection"));
        BuildNeedlesTab();
    }

    void StockPlus_Click(object sender, RoutedEventArgs e)
    {
        if (doc?.NeedleStock is not { } st) return;
        for (int i = 0; i < st.Ints.Count; i++) st.Ints[i]++;
        MarkDirty(Loc.T("CardCollection"));
        BuildNeedlesTab();
    }

    // ================================================================ map tab

    void BuildMapTab()
    {
        Map.ShowBuildings = ShowBuildingsBox.IsChecked == true;
        Map.ShowTaken = ShowTakenBox.IsChecked == true;
        Map.ShowDome = ShowDomeBox.IsChecked == true;
        Map.Refresh(rebuildBitmap: true);
        SetView3D(view3D);
        BuildMapSide();
    }

    void BuildMapSide()
    {
        if (doc is null) return;
        var s = doc.State;
        double init = s.Num("hay_initial"), total = s.Num("hay_total");
        double ratio = init > 0 ? Math.Clamp(total / init, 0, 1) : 0;
        double maxH = doc.Heights?.Reals.DefaultIfEmpty(0).Max() ?? 0;
        FillBar.Width = 290 * ratio;
        FillText.Text = Loc.T("FillText", ratio * 100, maxH);
        var (px, py, pz) = doc.PlayerPos;
        PlayerInfo.Text = Loc.T("PlayerAt", px, py, pz);
        UpdateTargetInfo();
        MapNeedleList.ItemsSource = Enumerable.Range(0, doc.PileNeedleCount).Select(i =>
        {
            var (x, y, z) = doc.NeedlePos(i);
            bool taken = doc.IsNeedleTaken(i);
            return new
            {
                Index = i,
                Text = Loc.T("NeedleLine", i + 1, taken ? Loc.T("Taken") : Loc.T("InPile"), Math.Max(0, doc.HeightAt(x, z) - y)),
                Opacity = taken ? 0.55 : 1.0,
            };
        }).ToList();
    }

    void UpdateTargetInfo()
    {
        if (doc is null) return;
        if (Map.Target is { } t)
        {
            TargetInfo.Text = Loc.T("TargetInfo", t.X, t.Z, doc.HeightAt(t.X, t.Z));
            TeleportTargetButton.IsEnabled = true;
        }
        else
        {
            TargetInfo.Text = Loc.T("TargetNone");
            TeleportTargetButton.IsEnabled = false;
        }
    }

    void MapToggle_Click(object sender, RoutedEventArgs e)
    {
        Map.ShowBuildings = ShowBuildingsBox.IsChecked == true;
        Map.ShowTaken = ShowTakenBox.IsChecked == true;
        Map.ShowDome = ShowDomeBox.IsChecked == true;
        Map.Refresh(); Refresh3D();
        Refresh3D();
    }

    void Map_Clicked(double x, double z, bool doubleClick)
    {
        UpdateTargetInfo();
        if (doubleClick) TeleportTo(x, z);
    }

    void TeleportTo(double x, double z)
    {
        if (doc?.Player is null) return;
        double h = doc.HeightAt(x, z);
        double y = h > 0.02 ? h + 0.6 : 0.05;
        doc.SetPlayerPos(x, y, z);
        MarkDirty();
        SetStatus(Loc.T("Teleported", x, z), (Brush)FindResource("Warn"));
        BuildMapSide();
        Map.Refresh(); Refresh3D();
    }

    void ResetZoom_Click(object sender, RoutedEventArgs e) => Map.ResetZoom();

    bool view3D;

    void SetView3D(bool on)
    {
        view3D = on;
        Map.Visibility = on ? Visibility.Collapsed : Visibility.Visible;
        Map3D.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        MapHintText.Visibility = on ? Visibility.Collapsed : Visibility.Visible;
        Hint3DText.Visibility = XRayBox.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        ResetZoomButton.Visibility = HoverInfo.Visibility = on ? Visibility.Collapsed : Visibility.Visible;
        View2DButton.Style = (Style)FindResource(on ? "SmallButton" : "AccentButton");
        View3DButton.Style = (Style)FindResource(on ? "AccentButton" : "SmallButton");
        View2DButton.Padding = View3DButton.Padding = new Thickness(10, 4, 10, 4);
        if (on) Refresh3D();
    }

    void Refresh3D()
    {
        if (!view3D) return;
        Map3D.XRay = XRayBox.IsChecked == true;
        Map3D.ShowBuildings = ShowBuildingsBox.IsChecked == true;
        Map3D.ShowTaken = ShowTakenBox.IsChecked == true;
        Map3D.SetDoc(doc);
    }

    void View2D_Click(object sender, RoutedEventArgs e) => SetView3D(false);
    void View3D_Click(object sender, RoutedEventArgs e) => SetView3D(true);
    void XRay_Click(object sender, RoutedEventArgs e) => Refresh3D();

    void TeleportTarget_Click(object sender, RoutedEventArgs e)
    {
        if (Map.Target is { } t) TeleportTo(t.X, t.Z);
    }

    void TeleportNeedle_Click(object sender, RoutedEventArgs e)
    {
        if (doc is null) return;
        var (px, _, pz) = doc.PlayerPos;
        int best = -1; double bestD = double.MaxValue;
        for (int i = 0; i < doc.PileNeedleCount; i++)
        {
            if (doc.IsNeedleTaken(i)) continue;
            var (x, _, z) = doc.NeedlePos(i);
            double d = (x - px) * (x - px) + (z - pz) * (z - pz);
            if (d < bestD) { bestD = d; best = i; }
        }
        if (best < 0) { SetStatus(Loc.T("NoHiddenNeedle")); return; }
        var (nx, _, nz) = doc.NeedlePos(best);
        Map.Target = (nx, nz);
        TeleportTo(nx, nz);
        UpdateTargetInfo();
    }

    void MapNeedle_Click(object sender, RoutedEventArgs e)
    {
        if (doc is null || ((Button)sender).Tag is not int i) return;
        var (x, _, z) = doc.NeedlePos(i);
        Map.Target = (x, z);
        Map.Refresh(); Refresh3D();
        UpdateTargetInfo();
    }

    // ================================================================ career tab

    void BuildCareerTab()
    {
        if (profile is null)
        {
            CareerMissing.Visibility = Visibility.Visible;
            CareerGrid.ItemsSource = null;
            return;
        }
        CareerMissing.Visibility = Visibility.Collapsed;
        CareerGrid.ItemsSource = profile.Career.Select(c => new CareerRow(profile, c.Key, MarkProfileDirty)).ToList();
    }

    // ================================================================ backups tab

    void BuildBackupsTab()
    {
        var list = SaveStore.ListBackups(Root);
        BackupList.ItemsSource = list;
        NoBackupsText.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        RestoreButton.IsEnabled = false;
    }

    void BackupList_SelectionChanged(object sender, SelectionChangedEventArgs e) => RestoreButton.IsEnabled = BackupList.SelectedItem is BackupInfo;

    void BackupNow_Click(object sender, RoutedEventArgs e)
    {
        if (doc is null || !File.Exists(doc.Path)) return;
        try
        {
            var b = SaveStore.Backup(Root, doc.Path, "_manual");
            SetStatus(Loc.T("BackupMade", Path.GetFileName(b)), (Brush)FindResource("Good"));
            BuildBackupsTab();
        }
        catch (Exception ex) { SetStatus(ex.Message, (Brush)FindResource("Bad")); }
    }

    void Restore_Click(object sender, RoutedEventArgs e)
    {
        if (BackupList.SelectedItem is not BackupInfo b) return;
        if (!App.IgnoreGame && SaveStore.IsGameRunning())
        {
            MessageBox.Show(this, Loc.T("GameRunningSave"), Loc.T("Error"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var target = SaveStore.SlotForBackup(Root, b.Path);
        if (MessageBox.Show(this, Loc.T("RestoreQuestion", b.Name, Path.GetFileName(target)), Loc.T("Restore"),
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        try
        {
            SaveDoc.Load(b.Path); // must be readable
            if (File.Exists(target)) SaveStore.Backup(Root, target, "_before-restore");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(b.Path, target, overwrite: true);
            dirty = profileDirty = false;
            doc = null;
            LoadSlots(target);
            SetStatus(Loc.T("Restored"), (Brush)FindResource("Good"));
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, Loc.T("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    void OpenBackupFolder_Click(object sender, RoutedEventArgs e) => OpenFolder(SaveStore.BackupDir(Root));

    // ================================================================ raw tab

    sealed record RawRef(object Container, object? Key, string Path);

    void BuildRawTab()
    {
        RawTree.Items.Clear();
        foreach (var kv in doc!.Root.Items)
            RawTree.Items.Add(MakeRawItem(new RawRef(doc.Root, kv.Key, KeyText(kv.Key)), kv.Value));
        ClearRawEditor();
    }

    static string KeyText(object? k) => k switch { string s => s, null => "null", _ => k.ToString() ?? "?" };

    static object? RawValue(RawRef r) => r.Container switch
    {
        GDict d => d.Items.FirstOrDefault(kv => ReferenceEquals(kv.Key, r.Key) || Equals(kv.Key, r.Key)).Value,
        GArray a when r.Key is int i && i < a.Items.Count => a.Items[i],
        _ => null,
    };

    static string Preview(object? v) => v switch
    {
        null => "null",
        bool b => b ? "true" : "false",
        long l => l.ToString(CultureInfo.InvariantCulture),
        double d => d.ToString("R", CultureInfo.InvariantCulture),
        string s => "\"" + (s.Length > 60 ? s[..60] + "…" : s) + "\"",
        GDict d => "{ " + d.Count + " }",
        GArray a => "[ " + a.Count + " ]",
        GPacked p when p.Type == VType.PackedStringArray && p.Count <= 6 => p.Type + " [" + string.Join(", ", p.Strings) + "]",
        _ => v.ToString() ?? "",
    };

    TreeViewItem MakeRawItem(RawRef r, object? value)
    {
        var header = new TextBlock();
        header.Inlines.Add(new System.Windows.Documents.Run(KeyText(r.Key)) { Foreground = (Brush)FindResource("AccentHover") });
        header.Inlines.Add(new System.Windows.Documents.Run("  " + Preview(value)) { Foreground = value is GDict or GArray ? (Brush)FindResource("Muted") : (Brush)FindResource("Text") });
        var item = new TreeViewItem { Header = header, Tag = r, Foreground = (Brush)FindResource("Text") };
        if (value is GDict { Count: > 0 } or GArray { Count: > 0 })
        {
            item.Items.Add("…");
            item.Expanded += RawItem_Expanded;
        }
        return item;
    }

    void RawItem_Expanded(object sender, RoutedEventArgs e)
    {
        if (sender is not TreeViewItem item || e.OriginalSource != item || item.Items.Count != 1 || item.Items[0] is not string) return;
        item.Items.Clear();
        var r = (RawRef)item.Tag;
        const int limit = 500;
        switch (RawValue(r))
        {
            case GDict d:
                foreach (var kv in d.Items.Take(limit))
                    item.Items.Add(MakeRawItem(new RawRef(d, kv.Key, r.Path + "." + KeyText(kv.Key)), kv.Value));
                if (d.Count > limit) item.Items.Add(new TreeViewItem { Header = Loc.T("MoreItems", d.Count - limit), Foreground = (Brush)FindResource("Muted") });
                break;
            case GArray a:
                for (int i = 0; i < Math.Min(a.Count, limit); i++)
                    item.Items.Add(MakeRawItem(new RawRef(a, i, $"{r.Path}[{i}]"), a.Items[i]));
                if (a.Count > limit) item.Items.Add(new TreeViewItem { Header = Loc.T("MoreItems", a.Count - limit), Foreground = (Brush)FindResource("Muted") });
                break;
        }
    }

    void ClearRawEditor()
    {
        RawPathText.Text = RawTypeText.Text = RawNote.Text = "";
        RawValueBox.Text = "";
        RawValueBox.IsEnabled = RawApplyButton.IsEnabled = false;
    }

    void RawTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (RawTree.SelectedItem is not TreeViewItem { Tag: RawRef r }) { ClearRawEditor(); return; }
        var v = RawValue(r);
        RawPathText.Text = r.Path;
        RawTypeText.Text = GVariant.TypeName(v);
        RawNote.Text = "";
        string? text = v switch
        {
            bool b => b ? "true" : "false",
            long l => l.ToString(CultureInfo.InvariantCulture),
            double d => d.ToString("R", CultureInfo.InvariantCulture),
            string s => s,
            GStringName sn => sn.Value,
            GMath m => string.Join(", ", m.V.Select(x => x.ToString("R", CultureInfo.InvariantCulture))),
            GPacked p when p.Count <= 512 => p.Type switch
            {
                VType.PackedByteArray => string.Join(", ", p.Bytes),
                VType.PackedInt32Array or VType.PackedInt64Array => string.Join(", ", p.Ints),
                VType.PackedStringArray => string.Join(", ", p.Strings),
                _ => string.Join(", ", p.Reals.Select(x => x.ToString("R", CultureInfo.InvariantCulture))),
            },
            _ => null,
        };
        if (text is null)
        {
            RawNote.Text = v is GPacked p ? Loc.T("RawTooBig", p.Count) : Loc.T("RawNotEditable");
            RawValueBox.Text = v is null ? "null" : "";
            RawValueBox.IsEnabled = RawApplyButton.IsEnabled = false;
            return;
        }
        RawValueBox.Text = text;
        RawValueBox.IsEnabled = RawApplyButton.IsEnabled = true;
    }

    void RawValueBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) RawApply_Click(sender, e);
    }

    void RawApply_Click(object sender, RoutedEventArgs e)
    {
        if (doc is null || RawTree.SelectedItem is not TreeViewItem { Tag: RawRef r } item) return;
        var old = RawValue(r);
        var text = RawValueBox.Text;
        object? nv;
        try
        {
            nv = ParseRaw(old, text);
        }
        catch (Exception ex)
        {
            SetStatus(Loc.T("InvalidInput", ex.Message), (Brush)FindResource("Bad"));
            return;
        }
        switch (r.Container)
        {
            case GDict d:
                int idx = d.Items.FindIndex(kv => ReferenceEquals(kv.Key, r.Key) || Equals(kv.Key, r.Key));
                if (idx >= 0) d.Items[idx] = new(d.Items[idx].Key, nv);
                break;
            case GArray a when r.Key is int i:
                a.Items[i] = nv;
                break;
        }
        var parent = item.Parent as ItemsControl;
        int pos = parent?.Items.IndexOf(item) ?? -1;
        var fresh = MakeRawItem(r, nv);
        if (parent is not null && pos >= 0)
        {
            parent.Items[pos] = fresh;
            fresh.IsSelected = true;
        }
        MarkDirty(r.Path);
        RebuildEditors();
    }

    static double ParseD(string s)
    {
        if (!double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) throw new FormatException(s);
        return d;
    }

    static string[] SplitList(string text) => text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    static object? ParseRaw(object? old, string text) => old switch
    {
        bool => text.Trim().ToLowerInvariant() switch
        {
            "true" or "1" or "ja" or "yes" => true,
            "false" or "0" or "nein" or "no" => false,
            _ => throw new FormatException(text),
        },
        long => (long)Math.Round(ParseD(text)),
        double => ParseD(text),
        string => text,
        GStringName => new GStringName(text),
        GMath m => SplitList(text) is { } parts && parts.Length == m.V.Length
            ? new GMath(m.Type, parts.Select(ParseD).ToArray(), m.Is64)
            : throw new FormatException($"{m.V.Length} values expected"),
        GPacked p => ParsePacked(p, text),
        _ => throw new FormatException("not editable"),
    };

    static GPacked ParsePacked(GPacked p, string text)
    {
        var parts = SplitList(text);
        var n = new GPacked(p.Type) { Is64 = p.Is64 };
        switch (p.Type)
        {
            case VType.PackedByteArray: n.Bytes = parts.Select(x => byte.Parse(x, CultureInfo.InvariantCulture)).ToArray(); break;
            case VType.PackedInt32Array: n.Ints.AddRange(parts.Select(x => (long)int.Parse(x, CultureInfo.InvariantCulture))); break;
            case VType.PackedInt64Array: n.Ints.AddRange(parts.Select(x => long.Parse(x, CultureInfo.InvariantCulture))); break;
            case VType.PackedStringArray: n.Strings.AddRange(parts); break;
            default:
                n.Reals.AddRange(parts.Select(ParseD));
                if (n.Reals.Count % n.Components != 0) throw new FormatException($"multiple of {n.Components} values expected");
                break;
        }
        return n;
    }

    // ---- export / import

    void ExportJson_Click(object sender, RoutedEventArgs e)
    {
        if (doc is null) return;
        var dlg = new SaveFileDialog { Filter = "JSON|*.json", FileName = Path.GetFileNameWithoutExtension(doc.Path) + ".json" };
        if (dlg.ShowDialog(this) != true) return;
        using (var fs = File.Create(dlg.FileName))
        using (var w = new Utf8JsonWriter(fs, new JsonWriterOptions { Indented = true }))
            WriteJson(w, doc.Root);
        SetStatus(Loc.T("Exported", dlg.FileName), (Brush)FindResource("Good"));
    }

    static void WriteJson(Utf8JsonWriter w, object? v)
    {
        switch (v)
        {
            case null: w.WriteNullValue(); break;
            case bool b: w.WriteBooleanValue(b); break;
            case long l: w.WriteNumberValue(l); break;
            case double d: if (double.IsFinite(d)) w.WriteNumberValue(d); else w.WriteStringValue(d.ToString(CultureInfo.InvariantCulture)); break;
            case string s: w.WriteStringValue(s); break;
            case GStringName sn: w.WriteStringValue(sn.Value); break;
            case GDict dict:
                w.WriteStartObject();
                foreach (var kv in dict.Items) { w.WritePropertyName(KeyText(kv.Key)); WriteJson(w, kv.Value); }
                w.WriteEndObject();
                break;
            case GArray a:
                w.WriteStartArray();
                foreach (var it in a.Items) WriteJson(w, it);
                w.WriteEndArray();
                break;
            case GPacked p:
                w.WriteStartArray();
                switch (p.Type)
                {
                    case VType.PackedByteArray: foreach (var b in p.Bytes) w.WriteNumberValue(b); break;
                    case VType.PackedInt32Array or VType.PackedInt64Array: foreach (var x in p.Ints) w.WriteNumberValue(x); break;
                    case VType.PackedStringArray: foreach (var x in p.Strings) w.WriteStringValue(x); break;
                    default: foreach (var x in p.Reals) w.WriteNumberValue(x); break;
                }
                w.WriteEndArray();
                break;
            case GMath m:
                w.WriteStartObject();
                w.WriteString("type", m.Type.ToString());
                w.WritePropertyName("v");
                w.WriteStartArray();
                foreach (var x in m.V) w.WriteNumberValue(x);
                w.WriteEndArray();
                w.WriteEndObject();
                break;
            default: w.WriteStringValue(v.ToString()); break;
        }
    }

    void ExportBin_Click(object sender, RoutedEventArgs e)
    {
        if (doc is null) return;
        var dlg = new SaveFileDialog { Filter = "Decrypted save|*.bin", FileName = Path.GetFileNameWithoutExtension(doc.Path) + ".bin" };
        if (dlg.ShowDialog(this) != true) return;
        File.WriteAllBytes(dlg.FileName, doc.BuildPlain());
        SetStatus(Loc.T("Exported", dlg.FileName), (Brush)FindResource("Good"));
    }

    void ImportBin_Click(object sender, RoutedEventArgs e)
    {
        if (doc is null) return;
        var dlg = new OpenFileDialog { Filter = "Decrypted save|*.bin|*.*|*.*" };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            var imported = SaveDoc.FromPlain(doc.Path, File.ReadAllBytes(dlg.FileName), doc.LoadedWriteTime);
            doc.ReplaceRoot(imported.Root);
            Map.SetDoc(doc);
            RebuildAll();
            MarkDirty();
            SetStatus(Loc.T("Imported"), (Brush)FindResource("Warn"));
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, Loc.T("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ================================================================ snapshot (UI check)

    async Task SnapshotAsync(string dir)
    {
        Directory.CreateDirectory(dir);
        for (int i = 0; i < Tabs.Items.Count; i++)
        {
            Tabs.SelectedIndex = i;
            if (i == 9 && RawTree.Items.Count > 1 && RawTree.Items[1] is TreeViewItem ti) { ti.IsExpanded = true; ti.IsSelected = true; }
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            await Task.Delay(250);
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            var content = (FrameworkElement)Content;
            var dpi = VisualTreeHelper.GetDpi(this);
            var rtb = new RenderTargetBitmap((int)(content.ActualWidth * dpi.DpiScaleX), (int)(content.ActualHeight * dpi.DpiScaleY),
                96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY, PixelFormats.Pbgra32);
            rtb.Render(content);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            using var fs = File.Create(Path.Combine(dir, $"tab{i}.png"));
            enc.Save(fs);
        }
        // radar showcase: zoomed top view with target, 3D, 3D x-ray, rotating x-ray frames for a GIF
        async Task Idle(int ms = 300)
        {
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            await Task.Delay(ms);
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        }
        Tabs.SelectedIndex = 2;
        SetView3D(false);
        if (doc is not null)
        {
            int hiddenNeedle = Enumerable.Range(0, doc.PileNeedleCount).FirstOrDefault(i => !doc.IsNeedleTaken(i), -1);
            if (hiddenNeedle >= 0) { var (nx, _, nz) = doc.NeedlePos(hiddenNeedle); Map.Target = (nx, nz); UpdateTargetInfo(); }
        }
        Map.SetZoom(1.8, 0, 0);
        await Idle();
        SnapTo(Path.Combine(dir, "radar-2d.png"));
        Map.ResetZoom();

        XRayBox.IsChecked = false;
        SetView3D(true);
        Map3D.SetCamera(-35, 32, 26);
        await Idle(600);
        SnapTo(Path.Combine(dir, "radar-3d.png"));

        XRayBox.IsChecked = true;
        Refresh3D();
        Map3D.SetCamera(-35, 30, 19);
        await Idle(600);
        SnapTo(Path.Combine(dir, "radar-xray.png"));

        Directory.CreateDirectory(Path.Combine(dir, "frames"));
        for (int f = 0; f < 48; f++)
        {
            Map3D.SetCamera(-35 + f * 7.5, 30, 19);
            await Idle(40);
            SnapTo(Path.Combine(dir, "frames", $"f{f:00}.png"), Map3D);
        }
        Close();
    }

    void SnapTo(string file, FrameworkElement? element = null)
    {
        var content = element ?? (FrameworkElement)Content;
        var dpi = VisualTreeHelper.GetDpi(this);
        var rtb = new RenderTargetBitmap((int)(content.ActualWidth * dpi.DpiScaleX), (int)(content.ActualHeight * dpi.DpiScaleY),
            96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY, PixelFormats.Pbgra32);
        rtb.Render(content);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(rtb));
        using var fs = File.Create(file);
        enc.Save(fs);
    }
}
