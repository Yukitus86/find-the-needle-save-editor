using System.IO;

namespace FindTheNeedleEditor.Core;

/// <summary>A loaded slot: the decoded Variant tree plus typed shortcuts into it.
/// Edits go straight into the tree; Build() encodes it again.</summary>
public sealed class SaveDoc
{
    public string Path { get; }
    public GDict Root { get; private set; }
    public DateTime LoadedWriteTime { get; }
    public string Password { get; private set; } = SaveCrypto.GamePassword;

    SaveDoc(string path, GDict root, DateTime writeTime)
    {
        Path = path; Root = root; LoadedWriteTime = writeTime;
    }

    public static SaveDoc Load(string path)
    {
        var file = File.ReadAllBytes(path);
        var plain = SaveCrypto.DecryptAny(file, out var pass);
        var d = FromPlain(path, plain, File.GetLastWriteTimeUtc(path));
        d.Password = pass;
        return d;
    }

    public static SaveDoc FromPlain(string path, byte[] plain, DateTime writeTime)
    {
        if (SaveCrypto.ParseStoreVar(plain) is not GDict root || root.Dict("state") is null)
            throw new InvalidDataException("Unknown save layout (no \"state\" dictionary).");
        return new SaveDoc(path, root, writeTime);
    }

    public void ReplaceRoot(GDict root) => Root = root;

    // ---------------------------------------------------------------- sections

    public GDict State => Root.Dict("state")!;
    public GDict? Meta => Root.Dict("meta");
    public GDict? TechRanks => Root.Dict("tech")?.Dict("ranks");
    public GArray? Buildings => Root.Arr("buildings");
    public GArray? Props => Root.Arr("props");
    public GPacked? Heights => Root["heights"] as GPacked;
    public GPacked? Dome => Root["dome"] as GPacked;
    public GMath? Player => Root["player"] as GMath;
    public double Cell => Root.Num("cell", 0.25);
    public double Extent => Root.Num("extent", 15);
    public int Version => (int)Root.Int("version");

    public int GridSize => Heights is { } h ? (int)Math.Round(Math.Sqrt(h.Count)) : 0;

    // ---------------------------------------------------------------- needles

    public GPacked? NeedlePositions => State["needle_positions"] as GPacked;
    public GPacked? NeedleTaken => State["needle_taken"] as GPacked;
    public GPacked? NeedleTypes => State["needle_type"] as GPacked;
    public GPacked? Discovered => State["discovered"] as GPacked;
    public GPacked? NeedleStock => State["needle_stock"] as GPacked;
    public GPacked? NeedlesByType => State["needles_by_type"] as GPacked;

    public int PileNeedleCount => NeedlePositions?.Count ?? 0;

    public int NeedleTypeCount => Math.Max(Discovered?.Count ?? 0, Math.Max(NeedleStock?.Count ?? 0, NeedlesByType?.Count ?? 0));

    public (double X, double Y, double Z) NeedlePos(int i)
    {
        var p = NeedlePositions!;
        return (p.Reals[i * 3], p.Reals[i * 3 + 1], p.Reals[i * 3 + 2]);
    }

    public bool IsNeedleTaken(int i) => NeedleTaken is { } t && i < t.Bytes.Length && t.Bytes[i] != 0;
    public int NeedleType(int i) => NeedleTypes is { } t && i < t.Bytes.Length ? t.Bytes[i] : i;

    /// <summary>Marks a pile needle as found and books it like the game does: counters, collection, discovery.</summary>
    public void TakeNeedle(int i)
    {
        if (NeedleTaken is not { } taken || i >= taken.Bytes.Length || taken.Bytes[i] != 0) return;
        taken.Bytes[i] = 1;
        int type = NeedleType(i);
        State.SetNum("needles_found", State.Num("needles_found") + 1);
        State.SetNum("pile_needles_found", State.Num("pile_needles_found") + 1);
        if (NeedleStock is { } st && type < st.Ints.Count) st.Ints[type]++;
        if (NeedlesByType is { } bt && type < bt.Ints.Count) bt.Ints[type]++;
        if (Discovered is { } d && type < d.Bytes.Length) d.Bytes[type] = 1;
        if (State.Num("first_needle_secs") <= 0) State.SetNum("first_needle_secs", State.Num("run_secs"));
    }

    /// <summary>Puts a found needle back into the pile and undoes the bookings of TakeNeedle.</summary>
    public void UntakeNeedle(int i)
    {
        if (NeedleTaken is not { } taken || i >= taken.Bytes.Length || taken.Bytes[i] == 0) return;
        taken.Bytes[i] = 0;
        int type = NeedleType(i);
        State.SetNum("needles_found", Math.Max(0, State.Num("needles_found") - 1));
        State.SetNum("pile_needles_found", Math.Max(0, State.Num("pile_needles_found") - 1));
        if (NeedleStock is { } st && type < st.Ints.Count && st.Ints[type] > 0) st.Ints[type]--;
        if (NeedlesByType is { } bt && type < bt.Ints.Count && bt.Ints[type] > 0) bt.Ints[type]--;
    }

    // ---------------------------------------------------------------- map

    /// <summary>Hay height at world x/z (rows = z, columns = x).</summary>
    public double HeightAt(double x, double z, GPacked? field = null)
    {
        field ??= Heights;
        int n = GridSize;
        if (field is null || n == 0) return 0;
        int ix = (int)Math.Round((x + Extent) / Cell), iz = (int)Math.Round((z + Extent) / Cell);
        if (ix < 0 || iz < 0 || ix >= n || iz >= n) return 0;
        return field.Reals[iz * n + ix];
    }

    public (double X, double Y, double Z) PlayerPos => Player is { } p ? (p.V[9], p.V[10], p.V[11]) : (0, 0, 0);

    /// <summary>Player facing direction on the ground plane (x, z): the basis' -Z axis.</summary>
    public (double X, double Z) PlayerForward => Player is { } p ? (-p.V[2], -p.V[8]) : (0, 1);

    public void SetPlayerPos(double x, double y, double z)
    {
        if (Player is not { } p) return;
        p.V[9] = x; p.V[10] = y; p.V[11] = z;
    }

    // ---------------------------------------------------------------- save

    /// <summary>Keeps the main-menu preview ("meta") in line with the edited state.</summary>
    public void SyncMeta()
    {
        if (Meta is not { } m) return;
        foreach (var k in new[] { "hay_dug", "hay_initial", "hay_total", "needles_found", "money", "money_earned" })
            if (m.Has(k) && State.Has(k)) m[k] = GVariant.Clone(State[k]);
    }

    /// <summary>Encodes and encrypts; throws if the result does not parse back to the same bytes.</summary>
    public byte[] Build()
    {
        SyncMeta();
        var plain = SaveCrypto.BuildStoreVar(Root);
        var check = SaveCrypto.ParseStoreVar(plain);
        if (!SaveCrypto.BuildStoreVar(check).AsSpan().SequenceEqual(plain))
            throw new InvalidDataException("Self-check failed: the encoded save does not read back identically.");
        var enc = SaveCrypto.Encrypt(plain, Password);
        if (!SaveCrypto.Decrypt(enc, Password).AsSpan().SequenceEqual(plain))
            throw new InvalidDataException("Self-check failed: encryption round trip.");
        return enc;
    }

    public byte[] BuildPlain()
    {
        SyncMeta();
        return SaveCrypto.BuildStoreVar(Root);
    }
}
