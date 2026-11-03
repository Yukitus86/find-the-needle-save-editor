using System.Globalization;
using System.IO;
using System.Text;

namespace FindTheNeedleEditor.Core;

/// <summary>
/// profile.cfg (Godot ConfigFile text). Only simple numeric keys of [career] are edited;
/// every other line – including [identity] with the online tokens – is written back untouched.
/// </summary>
public sealed class ProfileCfg
{
    public string Path { get; }
    readonly List<string> lines;
    readonly Dictionary<string, int> careerLine = [];
    public DateTime LoadedWriteTime { get; }

    /// <summary>Numeric [career] values in file order.</summary>
    public List<(string Key, double Value, bool IsInt)> Career { get; } = [];

    ProfileCfg(string path)
    {
        Path = path;
        lines = File.ReadAllLines(path, Encoding.UTF8).ToList();
        LoadedWriteTime = File.GetLastWriteTimeUtc(path);
        string section = "";
        for (int i = 0; i < lines.Count; i++)
        {
            var l = lines[i];
            if (l.StartsWith('[') && l.EndsWith(']')) { section = l[1..^1]; continue; }
            if (section != "career") continue;
            int eq = l.IndexOf('=');
            if (eq <= 0) continue;
            var key = l[..eq];
            var val = l[(eq + 1)..];
            if (double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            {
                careerLine[key] = i;
                Career.Add((key, d, !val.Contains('.') && !val.Contains('e')));
            }
        }
    }

    public static ProfileCfg? TryLoad(string path)
    {
        try { return File.Exists(path) ? new ProfileCfg(path) : null; }
        catch (Exception) { return null; }
    }

    public void Set(string key, double value)
    {
        int idx = Career.FindIndex(c => c.Key == key);
        if (idx < 0) return;
        var c = Career[idx];
        Career[idx] = (key, c.IsInt ? Math.Round(value) : value, c.IsInt);
        var text = c.IsInt ? ((long)Math.Round(value)).ToString(CultureInfo.InvariantCulture)
                           : FormatFloat(value);
        lines[careerLine[key]] = key + "=" + text;
    }

    static string FormatFloat(double v)
    {
        var s = v.ToString("R", CultureInfo.InvariantCulture);
        return s.Contains('.') || s.Contains('E') ? s : s + ".0";
    }

    public void Save()
    {
        File.Copy(Path, Path + ".editor.bak", overwrite: true);
        SaveStore.WriteAtomic(Path, Encoding.UTF8.GetBytes(string.Join("\n", lines) + "\n"));
    }
}
