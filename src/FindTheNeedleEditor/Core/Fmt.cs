using System.Globalization;

namespace FindTheNeedleEditor.Core;

public static class Fmt
{
    static CultureInfo C => Loc.Instance.Culture;

    public static string Money(double v) => v.ToString("#,0.00", C);
    public static string Int(double v) => v.ToString("#,0", C);
    public static string Num(double v) => v.ToString("#,0.##", C);

    public static string Duration(double seconds)
    {
        if (seconds <= 0 || double.IsNaN(seconds)) return "–";
        var t = TimeSpan.FromSeconds(seconds);
        return t.TotalHours >= 1 ? $"{(int)t.TotalHours}h {t.Minutes:00}m" : $"{t.Minutes}m {t.Seconds:00}s";
    }

    /// <summary>Text shown in edit boxes: plain, no grouping, invariant decimal point.</summary>
    public static string Edit(double v, bool isInt) =>
        isInt ? ((long)Math.Round(v)).ToString(CultureInfo.InvariantCulture) : v.ToString("0.######", CultureInfo.InvariantCulture);

    /// <summary>Accepts "1234.5", "1234,5", "1 234,5", "1_000". Rejects ambiguous input.</summary>
    public static bool TryParse(string text, out double value)
    {
        value = 0;
        var s = text.Trim().Replace(" ", "").Replace("_", "").Replace(" ", "").Replace("'", "");
        if (s.Length == 0) return false;
        int dots = s.Count(c => c == '.'), commas = s.Count(c => c == ',');
        if (dots > 0 && commas > 0)
        {
            // the later one is the decimal separator
            char dec = s.LastIndexOf('.') > s.LastIndexOf(',') ? '.' : ',';
            char grp = dec == '.' ? ',' : '.';
            s = s.Replace(grp.ToString(), "");
            if (dec == ',') s = s.Replace(',', '.');
        }
        else if (commas == 1) s = s.Replace(',', '.');
        else if (commas > 1) s = s.Replace(",", "");
        else if (dots > 1) s = s.Replace(".", "");
        if (s.EndsWith("k", StringComparison.OrdinalIgnoreCase) && double.TryParse(s[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var k)) { value = k * 1e3; return true; }
        if (s.EndsWith("m", StringComparison.OrdinalIgnoreCase) && double.TryParse(s[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var m)) { value = m * 1e6; return true; }
        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && !double.IsNaN(value) && !double.IsInfinity(value);
    }

    /// <summary>"belt_speed" -> "Belt speed".</summary>
    public static string Pretty(string id)
    {
        if (string.IsNullOrEmpty(id)) return id;
        var s = id.Replace('_', ' ');
        return char.ToUpperInvariant(s[0]) + s[1..];
    }
}
