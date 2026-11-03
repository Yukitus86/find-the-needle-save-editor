using System.IO;
using System.Reflection;
using System.Text.Json;

namespace FindTheNeedleEditor.Core;

/// <summary>
/// The game's tech cards (read from the running game's card table, English and German):
/// kind, branch, maximum rank (= number of level costs), costs, prerequisites, texts and per-level values.
/// </summary>
public sealed class TechCard
{
    public required string Id { get; init; }
    /// <summary>root, ranked, unlock, shop, demo (demo = only in the full game).</summary>
    public required string Kind { get; init; }
    public required string Branch { get; init; }
    public int Max { get; init; }
    public double[] Costs { get; init; } = [];
    public string[] Requires { get; init; } = [];
    public (string En, string De) Name { get; init; }
    public (string En, string De)? Blurb { get; init; }
    public (string En, string De)? Reward { get; init; }
    public (string En, string De)? Unit { get; init; }
    public (string En, string De)? Suffix { get; init; }
    public (string En, string De)? Base { get; init; }
    public (string[] En, string[] De)? Values { get; init; }

    public bool IsRanked => Kind == "ranked";
    public bool IsDemoLocked => Kind == "demo";

    static string Pick((string En, string De) t) => Loc.Instance.Language == UiLanguage.German ? t.De : t.En;
    public string DisplayName => Pick(Name);
    public string? DisplayBlurb => Blurb is { } b ? Pick(b) : null;
    public string? DisplayReward => Reward is { } r ? Pick(r) : null;

    /// <summary>Effect at a rank, e.g. "2.27 m/s" (rank 0 = base value).</summary>
    public string? ValueAt(long rank)
    {
        string? unit = Unit is { } u ? Pick(u) : null, suffix = Suffix is { } s ? Pick(s) : null;
        string? v;
        if (rank <= 0) v = Base is { } b ? Pick(b) : null;
        else if (Values is { } vals)
        {
            var arr = Loc.Instance.Language == UiLanguage.German ? vals.De : vals.En;
            v = arr.Length == 0 ? null : arr[Math.Min(arr.Length, (int)rank) - 1];
        }
        else v = null;
        return v is null ? null : v + (unit ?? "") + (suffix ?? "");
    }
}

public static class TechDb
{
    static Dictionary<string, TechCard>? cards;

    public static IReadOnlyDictionary<string, TechCard> Cards => cards ??= Load();

    public static TechCard? Get(string id) => Cards.TryGetValue(id, out var c) ? c : null;

    static Dictionary<string, TechCard> Load()
    {
        var dict = new Dictionary<string, TechCard>();
        try
        {
            using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("FindTheNeedleEditor.Assets.techtree.json");
            if (s is null) return dict;
            using var doc = JsonDocument.Parse(s);
            foreach (var e in doc.RootElement.EnumerateArray())
            {
                (string, string)? Tx(string f) => e.TryGetProperty(f, out var p) ? (p.GetProperty("en").GetString() ?? "", p.GetProperty("de").GetString() ?? "") : null;
                (string[], string[])? Vals() => e.TryGetProperty("values", out var p)
                    ? (p.GetProperty("en").EnumerateArray().Select(x => x.GetString() ?? "").ToArray(), p.GetProperty("de").EnumerateArray().Select(x => x.GetString() ?? "").ToArray())
                    : null;
                var id = e.GetProperty("id").GetString()!;
                dict[id] = new TechCard
                {
                    Id = id,
                    Kind = e.GetProperty("kind").GetString() ?? "",
                    Branch = e.GetProperty("branch").GetString() ?? "",
                    Max = e.GetProperty("max").GetInt32(),
                    Costs = e.GetProperty("costs").EnumerateArray().Select(x => x.GetDouble()).ToArray(),
                    Requires = e.GetProperty("requires").EnumerateArray().Select(x => x.GetString() ?? "").ToArray(),
                    Name = Tx("name") ?? (id, id),
                    Blurb = Tx("blurb"), Reward = Tx("reward"), Unit = Tx("unit"), Suffix = Tx("suffix"), Base = Tx("base"),
                    Values = Vals(),
                };
            }
        }
        catch (Exception) { /* without the table the editor falls back to generated names and no limits */ }
        return dict;
    }

    static readonly Dictionary<string, (string Icon, string De, string En)> Branches = new()
    {
        ["handling"] = ("✋", "Handarbeit", "Handling"),
        ["fitness"] = ("💪", "Fitness", "Fitness"),
        ["lines"] = ("🛤", "Fließbänder", "Lines"),
        ["automation"] = ("🦾", "Automatisierung", "Automation"),
        ["power"] = ("⚡", "Strom", "Power"),
        ["water"] = ("💧", "Wasser", "Water"),
        ["plant"] = ("🏭", "Anlagen", "Plant"),
        ["selling"] = ("💰", "Verkauf", "Selling"),
        ["prospecting"] = ("📡", "Suche", "Prospecting"),
        ["structures"] = ("🏗", "Bauten", "Structures"),
    };

    public static string BranchOrderKey(string b) => b switch
    {
        "handling" => "0", "fitness" => "1", "lines" => "2", "automation" => "3", "power" => "4", "water" => "5",
        "plant" => "6", "selling" => "7", "prospecting" => "8", "structures" => "9", _ => "z" + b,
    };

    public static string BranchTitle(string b) => Branches.TryGetValue(b, out var x)
        ? $"{x.Icon}  {(Loc.Instance.Language == UiLanguage.German ? x.De : x.En)}"
        : "❔  " + (string.IsNullOrEmpty(b) ? Loc.T("GroupOther") : Fmt.Pretty(b));
}
