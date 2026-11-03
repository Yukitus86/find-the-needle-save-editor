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
    int careerHeader = -1;
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
            if (l.StartsWith('[') && l.EndsWith(']')) { section = l[1..^1]; if (section == "career") careerHeader = i; continue; }
            if (section != "career") continue;
            int eq = l.IndexOf('=');
            if (eq <= 0) continue;
            var key = l[..eq];
            var val = l[(eq + 1)..];
            careerLine[key] = i;
            if (double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                Career.Add((key, d, !val.Contains('.') && !val.Contains('e')));
        }
    }

    public static ProfileCfg? TryLoad(string path)
    {
        try { return File.Exists(path) ? new ProfileCfg(path) : null; }
        catch (Exception) { return null; }
    }

    /// <summary>[career] debug_tainted: the game's own "mods or debug menu were used on this PC" mark,
    /// which keeps the player off the leaderboards.</summary>
    public bool IsTainted => careerLine.TryGetValue("debug_tainted", out var i) && lines[i].EndsWith("=true");

    /// <summary>Sets debug_tainted=true (adds the key if missing). Returns true when the file changed.</summary>
    public bool MarkTainted()
    {
        if (IsTainted) return false;
        if (careerLine.TryGetValue("debug_tainted", out var i)) lines[i] = "debug_tainted=true";
        else if (careerHeader >= 0)
        {
            lines.Insert(careerHeader + 1, "debug_tainted=true");
            foreach (var k in careerLine.Keys.ToList()) if (careerLine[k] > careerHeader) careerLine[k]++;
            careerLine["debug_tainted"] = careerHeader + 1;
        }
        else
        {
            lines.Insert(0, "");
            lines.Insert(0, "debug_tainted=true");
            lines.Insert(0, "[career]");
            careerHeader = 0;
            foreach (var k in careerLine.Keys.ToList()) careerLine[k] += 3;
            careerLine["debug_tainted"] = 1;
        }
        return true;
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
