using System.ComponentModel;
using System.Runtime.CompilerServices;
using FindTheNeedleEditor.Core;

namespace FindTheNeedleEditor;

public abstract class RowBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected readonly Action changed;
    protected RowBase(Action onChanged) { changed = onChanged; }
    protected void Notify([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    public void NotifyAll() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));

    protected static string NumText(object? v) => v switch
    {
        long l => l.ToString(Loc.Instance.Culture),
        double d => d.ToString("0.###", Loc.Instance.Culture),
        _ => "",
    };

    /// <summary>Parses into the dictionary, keeping int/float kind. Returns false on bad input.</summary>
    protected bool SetNumber(GDict d, string key, string text, double min = double.MinValue)
    {
        if (!Fmt.TryParse(text, out var v) || v < min) return false;
        d.SetNum(key, v);
        changed();
        return true;
    }
}

public sealed class NeedleRow(SaveDoc doc, int index, Action onChanged) : RowBase(onChanged)
{
    public int Index { get; } = index;
    public int No => Index + 1;
    public string TypeText => Loc.T("NeedleTypeName", doc.NeedleType(Index) + 1);

    public string PosText
    {
        get { var (x, y, z) = doc.NeedlePos(Index); return string.Format(Loc.Instance.Culture, "{0:0.00}  /  {1:0.00}  /  {2:0.00}", x, y, z); }
    }

    public double Depth { get { var (x, y, z) = doc.NeedlePos(Index); return Math.Max(0, doc.HeightAt(x, z) - y); } }
    public string DepthText => Found ? "–" : $"{Depth.ToString("0.00", Loc.Instance.Culture)} m";
    public string StateText => Found ? Loc.T("Taken") : Loc.T("InPile");

    public bool Found
    {
        get => doc.IsNeedleTaken(Index);
        set
        {
            if (value == Found) return;
            if (value) doc.TakeNeedle(Index); else doc.UntakeNeedle(Index);
            changed();
            NotifyAll();
        }
    }
}

public sealed class NeedleTypeRow(SaveDoc doc, int type, Action onChanged) : RowBase(onChanged)
{
    public int Type { get; } = type;
    public string Name => Loc.T("NeedleTypeName", Type + 1);

    public bool Discovered
    {
        get => doc.Discovered is { } d && Type < d.Bytes.Length && d.Bytes[Type] != 0;
        set
        {
            if (doc.Discovered is not { } d || Type >= d.Bytes.Length) return;
            d.Bytes[Type] = (byte)(value ? 1 : 0);
            changed(); Notify();
        }
    }

    public string Stock
    {
        get => doc.NeedleStock is { } s && Type < s.Ints.Count ? s.Ints[Type].ToString(Loc.Instance.Culture) : "";
        set
        {
            if (doc.NeedleStock is not { } s || Type >= s.Ints.Count || !Fmt.TryParse(value, out var v) || v < 0 || v > int.MaxValue) return;
            s.Ints[Type] = (long)v; changed(); Notify();
        }
    }

    public string Total
    {
        get => doc.NeedlesByType is { } s && Type < s.Ints.Count ? s.Ints[Type].ToString(Loc.Instance.Culture) : "";
        set
        {
            if (doc.NeedlesByType is not { } s || Type >= s.Ints.Count || !Fmt.TryParse(value, out var v) || v < 0 || v > int.MaxValue) return;
            s.Ints[Type] = (long)v; changed(); Notify();
        }
    }
}

public sealed class CareerRow(ProfileCfg cfg, string key, Action onChanged) : RowBase(onChanged)
{
    public string Key { get; } = key;
    public string Name => Loc.Has("K_" + Key) ? Loc.T("K_" + Key) : Fmt.Pretty(Key);
    public string Value
    {
        get
        {
            var c = cfg.Career.First(c => c.Key == Key);
            return c.IsInt ? ((long)c.Value).ToString(Loc.Instance.Culture) : c.Value.ToString("0.###", Loc.Instance.Culture);
        }
        set
        {
            if (Fmt.TryParse(value, out var v)) { cfg.Set(Key, v); changed(); }
            Notify();
        }
    }
}
