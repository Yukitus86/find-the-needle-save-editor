using System.Globalization;
using System.IO;
using System.Text.Json;
using FindTheNeedleEditor.Core;

namespace FindTheNeedleEditor;

/// <summary>Small per-user settings file in %APPDATA%\FindTheNeedleEditor\settings.json.</summary>
public sealed class AppSettings
{
    static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FindTheNeedleEditor", "settings.json");

    public string? Language { get; set; }

    /// <summary>Save passwords found by the key finder (only needed if a game update changes the built-in one).</summary>
    public List<string> ExtraPasswords { get; set; } = [];

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new();
        }
        catch (Exception) { /* broken settings fall back to defaults */ }
        return new();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this));
        }
        catch (Exception) { /* losing the language choice is not worth an error dialog */ }
    }

    /// <summary>English unless German was picked before (flag button).</summary>
    public UiLanguage ResolveLanguage() => Language == "de" ? UiLanguage.German : UiLanguage.English;

    public static string Code(UiLanguage language) => language == UiLanguage.German ? "de" : "en";
}
