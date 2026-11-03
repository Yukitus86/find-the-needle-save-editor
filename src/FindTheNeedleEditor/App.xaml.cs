using System.Windows;
using FindTheNeedleEditor.Core;

namespace FindTheNeedleEditor;

public partial class App : Application
{
    /// <summary>Game data folder; "--root &lt;dir&gt;" points the editor at a copy (testing).</summary>
    public static string SaveRoot { get; private set; } = SaveStore.DefaultRoot;

    /// <summary>"--snapshot &lt;dir&gt;" renders every tab to PNG files and exits (UI check without clicking).</summary>
    public static string? SnapshotDir { get; private set; }

    /// <summary>"--lang de|en" overrides the saved language for this run.</summary>
    public static string? LanguageOverride { get; private set; }

    /// <summary>"--mask-user" hides the Windows user name in displayed paths (screenshots).</summary>
    public static bool MaskUser { get; private set; }

    /// <summary>Path for display; with --mask-user the user name is replaced.</summary>
    public static string ShowPath(string path) => !MaskUser ? path
        : path.Replace(@"\Users\" + Environment.UserName + @"\", @"\Users\<user>\", StringComparison.OrdinalIgnoreCase);

    /// <summary>"--ignore-game" skips the running-game check (tests on a copy while the game runs).</summary>
    public static bool IgnoreGame { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        for (int i = 0; i < e.Args.Length; i++)
        {
            string? next = i + 1 < e.Args.Length ? e.Args[i + 1] : null;
            switch (e.Args[i])
            {
                case "--root" when next is not null: SaveRoot = next; i++; break;
                case "--snapshot" when next is not null: SnapshotDir = next; i++; break;
                case "--lang" when next is not null: LanguageOverride = next; i++; break;
                case "--ignore-game": IgnoreGame = true; break;
                case "--mask-user": MaskUser = true; break;
            }
        }

        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(args.Exception.Message, Loc.T("UnexpectedError"), MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
        base.OnStartup(e);
    }
}
