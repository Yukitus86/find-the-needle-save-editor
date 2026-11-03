using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace FindTheNeedleEditor.Core;

// Godot 4 binary Variant serialization (FileAccess.store_var / var_to_bytes), as used by the save file.
// Values map to .NET types:
//   NIL -> null, BOOL -> bool, INT -> long, FLOAT -> double, STRING -> string,
//   containers and everything else -> the classes below.
// INT and FLOAT pick 32 or 64 bit on write exactly like Godot does, so an untouched tree encodes byte for byte.

public enum VType
{
    Nil = 0, Bool, Int, Float, String, Vector2, Vector2i, Rect2, Rect2i, Vector3, Vector3i, Transform2D, Vector4, Vector4i,
    Plane, Quaternion, AABB, Basis, Transform3D, Projection, Color, StringName, NodePath, RID, Object, Callable, Signal,
    Dictionary, Array, PackedByteArray, PackedInt32Array, PackedInt64Array, PackedFloat32Array, PackedFloat64Array,
    PackedStringArray, PackedVector2Array, PackedVector3Array, PackedColorArray, PackedVector4Array,
}

public sealed class GStringName(string value)
{
    public string Value { get; set; } = value;
    public override string ToString() => "&\"" + Value + "\"";
}

/// <summary>Fixed-size math types (vectors, transforms, colors). Int types keep their numbers in doubles too.</summary>
public sealed class GMath(VType type, double[] values, bool is64)
{
    public VType Type { get; } = type;
    public double[] V { get; } = values;
    public bool Is64 { get; } = is64;
    public override string ToString() => $"{Type}({string.Join(", ", V.Select(x => x.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)))})";
}

public sealed class GRid(ulong id) { public ulong Id { get; set; } = id; public override string ToString() => $"RID({Id})"; }

public sealed class GDict
{
    public uint Header { get; set; } = (uint)VType.Dictionary;
    public bool SharedFlag { get; set; }
    public byte[] TypeInfo { get; set; } = []; // raw typed-dictionary info, kept as is
    public List<KeyValuePair<object?, object?>> Items { get; } = [];

    public int Count => Items.Count;

    int IndexOf(string key)
    {
        for (int i = 0; i < Items.Count; i++)
            if (Items[i].Key is string s && s == key) return i;
        return -1;
    }

    public bool Has(string key) => IndexOf(key) >= 0;

    public object? this[string key]
    {
        get { int i = IndexOf(key); return i >= 0 ? Items[i].Value : null; }
        set
        {
            int i = IndexOf(key);
            if (i >= 0) Items[i] = new(Items[i].Key, value);
            else Items.Add(new(key, value));
        }
    }

    public bool Remove(string key)
    {
        int i = IndexOf(key);
        if (i < 0) return false;
        Items.RemoveAt(i);
        return true;
    }

    public GDict? Dict(string key) => this[key] as GDict;
    public GArray? Arr(string key) => this[key] as GArray;
    public string? Str(string key) => this[key] as string;

    public double Num(string key, double fallback = 0) => this[key] switch
    {
        long l => l,
        double d => d,
        bool b => b ? 1 : 0,
        _ => fallback,
    };

    public long Int(string key, long fallback = 0) => this[key] switch
    {
        long l => l,
        double d => (long)d,
        bool b => b ? 1 : 0,
        _ => fallback,
    };

    public bool Bool(string key) => this[key] switch { bool b => b, long l => l != 0, _ => false };

    /// <summary>Sets a number and keeps the stored kind (int stays int, float stays float).</summary>
    public void SetNum(string key, double value)
    {
        if (this[key] is long) this[key] = (long)Math.Round(value);
        else this[key] = value;
    }
}

public sealed class GArray
{
    public uint Header { get; set; } = (uint)VType.Array;
    public bool SharedFlag { get; set; }
    public byte[] TypeInfo { get; set; } = []; // raw typed-array info, kept as is
    public List<object?> Items { get; } = [];
    public int Count => Items.Count;
}

/// <summary>All Packed*Array types. Exactly one of the lists is used, depending on Type.</summary>
public sealed class GPacked
{
    public VType Type { get; }
    public bool Is64 { get; set; }
    public byte[] Bytes { get; set; } = [];
    public List<long> Ints { get; } = [];      // Int32 / Int64
    public List<double> Reals { get; } = [];   // Float32 / Float64 and the vector/color arrays (flattened)
    public List<string> Strings { get; } = [];

    public GPacked(VType type) { Type = type; }

    public int Components => Type switch
    {
        VType.PackedVector2Array => 2,
        VType.PackedVector3Array => 3,
        VType.PackedColorArray or VType.PackedVector4Array => 4,
        _ => 1,
    };

    public int Count => Type switch
    {
        VType.PackedByteArray => Bytes.Length,
        VType.PackedInt32Array or VType.PackedInt64Array => Ints.Count,
        VType.PackedStringArray => Strings.Count,
        _ => Reals.Count / Components,
    };

    public override string ToString() => $"{Type}[{Count}]";
}

public static class GVariant
{
    const uint Flag64 = 1u << 16;

    static int MathCount(VType t) => t switch
    {
        VType.Vector2 or VType.Vector2i => 2,
        VType.Vector3 or VType.Vector3i => 3,
        VType.Rect2 or VType.Rect2i or VType.Vector4 or VType.Vector4i or VType.Plane or VType.Quaternion or VType.Color => 4,
        VType.Transform2D or VType.AABB => 6,
        VType.Basis => 9,
        VType.Transform3D => 12,
        VType.Projection => 16,
        _ => 0,
    };

    static bool IsIntMath(VType t) => t is VType.Vector2i or VType.Vector3i or VType.Rect2i or VType.Vector4i;

    // ---------------------------------------------------------------- decode

    public static object? Decode(byte[] data, ref int pos)
    {
        var r = new Reader(data, pos);
        var v = r.Var();
        pos = r.Pos;
        return v;
    }

    sealed class Reader(byte[] d, int pos)
    {
        public int Pos = pos;

        uint U32() { Need(4); var v = BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(Pos)); Pos += 4; return v; }
        int I32() => (int)U32();
        long I64() { Need(8); var v = BinaryPrimitives.ReadInt64LittleEndian(d.AsSpan(Pos)); Pos += 8; return v; }
        float F32() { Need(4); var v = BinaryPrimitives.ReadSingleLittleEndian(d.AsSpan(Pos)); Pos += 4; return v; }
        double F64() { Need(8); var v = BinaryPrimitives.ReadDoubleLittleEndian(d.AsSpan(Pos)); Pos += 8; return v; }

        void Need(long n)
        {
            if (n < 0 || Pos + n > d.Length) throw new InvalidDataException($"Unexpected end of data at {Pos}");
        }

        void Pad(int len) { Pos += (4 - len % 4) % 4; }

        string Str()
        {
            int n = I32();
            Need(n);
            var s = Encoding.UTF8.GetString(d, Pos, n);
            Pos += n; Pad(n);
            return s;
        }

        byte[] TypeInfo(uint header, int shiftCount)
        {
            // typed containers: per typed field (array: 1, dictionary: key + value) a 2-bit kind
            int start = Pos;
            for (int f = 0; f < shiftCount; f++)
            {
                uint kind = (header >> (16 + 2 * f)) & 3;
                if (kind == 1) U32();          // builtin type
                else if (kind is 2 or 3) Str(); // class name / script path
            }
            return d[start..Pos];
        }

        public object? Var()
        {
            uint h = U32();
            var t = (VType)(h & 0xFF);
            bool f64 = (h & Flag64) != 0;
            switch (t)
            {
                case VType.Nil: return null;
                case VType.Bool: return U32() != 0;
                case VType.Int: return f64 ? I64() : I32();
                case VType.Float: return f64 ? F64() : F32();
                case VType.String: return Str();
                case VType.StringName: return new GStringName(Str());
                case VType.RID: return new GRid((ulong)I64());
                case VType.Dictionary:
                {
                    var dict = new GDict { Header = h };
                    dict.TypeInfo = TypeInfo(h, 2);
                    uint n = U32();
                    dict.SharedFlag = (n & 0x80000000) != 0;
                    n &= 0x7FFFFFFF;
                    for (uint i = 0; i < n; i++)
                    {
                        var k = Var();
                        var v = Var();
                        dict.Items.Add(new(k, v));
                    }
                    return dict;
                }
                case VType.Array:
                {
                    var arr = new GArray { Header = h };
                    arr.TypeInfo = TypeInfo(h, 1);
                    uint n = U32();
                    arr.SharedFlag = (n & 0x80000000) != 0;
                    n &= 0x7FFFFFFF;
                    for (uint i = 0; i < n; i++) arr.Items.Add(Var());
                    return arr;
                }
                case VType.PackedByteArray:
                {
                    int n = I32(); Need(n);
                    var p = new GPacked(t) { Bytes = d[Pos..(Pos + n)] };
                    Pos += n; Pad(n);
                    return p;
                }
                case VType.PackedInt32Array:
                case VType.PackedInt64Array:
                {
                    int n = I32();
                    var p = new GPacked(t);
                    for (int i = 0; i < n; i++) p.Ints.Add(t == VType.PackedInt32Array ? I32() : I64());
                    return p;
                }
                case VType.PackedFloat32Array:
                case VType.PackedFloat64Array:
                {
                    int n = I32();
                    var p = new GPacked(t);
                    for (int i = 0; i < n; i++) p.Reals.Add(t == VType.PackedFloat32Array ? F32() : F64());
                    return p;
                }
                case VType.PackedStringArray:
                {
                    int n = I32();
                    var p = new GPacked(t);
                    for (int i = 0; i < n; i++)
                    {
                        int len = I32(); Need(len);
                        // stored with a trailing NUL
                        int strLen = len > 0 && d[Pos + len - 1] == 0 ? len - 1 : len;
                        p.Strings.Add(Encoding.UTF8.GetString(d, Pos, strLen));
                        Pos += len; Pad(len);
                    }
                    return p;
                }
                case VType.PackedVector2Array:
                case VType.PackedVector3Array:
                case VType.PackedColorArray:
                case VType.PackedVector4Array:
                {
                    int n = I32();
                    var p = new GPacked(t) { Is64 = f64 && t != VType.PackedColorArray };
                    int c = p.Components;
                    for (int i = 0; i < n * c; i++) p.Reals.Add(p.Is64 ? F64() : F32());
                    return p;
                }
                default:
                {
                    int cnt = MathCount(t);
                    if (cnt == 0) throw new InvalidDataException($"Unsupported Variant type {t} at {Pos - 4}");
                    bool intMath = IsIntMath(t);
                    bool wide = f64 && !intMath && t != VType.Color;
                    var vals = new double[cnt];
                    for (int i = 0; i < cnt; i++) vals[i] = intMath ? I32() : wide ? F64() : F32();
                    return new GMath(t, vals, wide);
                }
            }
        }
    }

    // ---------------------------------------------------------------- encode

    public static byte[] Encode(object? value)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        Write(w, value);
        w.Flush();
        return ms.ToArray();
    }

    static void Pad(BinaryWriter w, int len)
    {
        for (int i = (4 - len % 4) % 4; i > 0; i--) w.Write((byte)0);
    }

    static void Str(BinaryWriter w, string s)
    {
        var b = Encoding.UTF8.GetBytes(s);
        w.Write(b.Length);
        w.Write(b);
        Pad(w, b.Length);
    }

    static void Write(BinaryWriter w, object? v)
    {
        switch (v)
        {
            case null: w.Write((uint)VType.Nil); break;
            case bool b: w.Write((uint)VType.Bool); w.Write(b ? 1u : 0u); break;
            case int i: Write(w, (long)i); break;
            case long l:
                if (l is > int.MaxValue or < int.MinValue) { w.Write((uint)VType.Int | Flag64); w.Write(l); }
                else { w.Write((uint)VType.Int); w.Write((int)l); }
                break;
            case float f: Write(w, (double)f); break;
            case double d:
                if ((double)(float)d != d && !double.IsNaN(d)) { w.Write((uint)VType.Float | Flag64); w.Write(d); }
                else { w.Write((uint)VType.Float); w.Write((float)d); }
                break;
            case string s: w.Write((uint)VType.String); Str(w, s); break;
            case GStringName sn: w.Write((uint)VType.StringName); Str(w, sn.Value); break;
            case GRid rid: w.Write((uint)VType.RID); w.Write(rid.Id); break;
            case GDict dict:
                w.Write(dict.Header);
                w.Write(dict.TypeInfo);
                w.Write((uint)dict.Items.Count | (dict.SharedFlag ? 0x80000000u : 0));
                foreach (var kv in dict.Items) { Write(w, kv.Key); Write(w, kv.Value); }
                break;
            case GArray arr:
                w.Write(arr.Header);
                w.Write(arr.TypeInfo);
                w.Write((uint)arr.Items.Count | (arr.SharedFlag ? 0x80000000u : 0));
                foreach (var it in arr.Items) Write(w, it);
                break;
            case GPacked p: WritePacked(w, p); break;
            case GMath m:
            {
                bool intMath = IsIntMath(m.Type);
                w.Write((uint)m.Type | (m.Is64 ? Flag64 : 0));
                foreach (var x in m.V)
                {
                    if (intMath) w.Write((int)x);
                    else if (m.Is64) w.Write(x);
                    else w.Write((float)x);
                }
                break;
            }
            default: throw new InvalidDataException($"Cannot encode {v.GetType().Name}");
        }
    }

    static void WritePacked(BinaryWriter w, GPacked p)
    {
        bool wide = p.Is64 && p.Type is VType.PackedVector2Array or VType.PackedVector3Array or VType.PackedVector4Array;
        w.Write((uint)p.Type | (wide ? Flag64 : 0));
        switch (p.Type)
        {
            case VType.PackedByteArray:
                w.Write(p.Bytes.Length); w.Write(p.Bytes); Pad(w, p.Bytes.Length); break;
            case VType.PackedInt32Array:
                w.Write(p.Ints.Count); foreach (var x in p.Ints) w.Write((int)x); break;
            case VType.PackedInt64Array:
                w.Write(p.Ints.Count); foreach (var x in p.Ints) w.Write(x); break;
            case VType.PackedFloat32Array:
                w.Write(p.Reals.Count); foreach (var x in p.Reals) w.Write((float)x); break;
            case VType.PackedFloat64Array:
                w.Write(p.Reals.Count); foreach (var x in p.Reals) w.Write(x); break;
            case VType.PackedStringArray:
                w.Write(p.Strings.Count);
                foreach (var s in p.Strings)
                {
                    var b = Encoding.UTF8.GetBytes(s);
                    w.Write(b.Length + 1); w.Write(b); w.Write((byte)0); Pad(w, b.Length + 1);
                }
                break;
            default:
                w.Write(p.Count);
                foreach (var x in p.Reals) { if (wide) w.Write(x); else w.Write((float)x); }
                break;
        }
    }

    // ---------------------------------------------------------------- helpers

    public static string TypeName(object? v) => v switch
    {
        null => "Nil",
        bool => "bool",
        long => "int",
        double => "float",
        string => "String",
        GStringName => "StringName",
        GDict d => $"Dictionary [{d.Count}]",
        GArray a => $"Array [{a.Count}]",
        GPacked p => $"{p.Type} [{p.Count}]",
        GMath m => m.Type.ToString(),
        GRid => "RID",
        _ => v.GetType().Name,
    };

    /// <summary>Deep copy (used for "discard" without re-reading and for tests).</summary>
    public static object? Clone(object? v) => v switch
    {
        GDict d => CloneDict(d),
        GArray a => CloneArr(a),
        GPacked p => ClonePacked(p),
        GMath m => new GMath(m.Type, (double[])m.V.Clone(), m.Is64),
        GStringName s => new GStringName(s.Value),
        GRid r => new GRid(r.Id),
        _ => v,
    };

    static GDict CloneDict(GDict d)
    {
        var c = new GDict { Header = d.Header, SharedFlag = d.SharedFlag, TypeInfo = d.TypeInfo };
        foreach (var kv in d.Items) c.Items.Add(new(Clone(kv.Key), Clone(kv.Value)));
        return c;
    }

    static GArray CloneArr(GArray a)
    {
        var c = new GArray { Header = a.Header, SharedFlag = a.SharedFlag, TypeInfo = a.TypeInfo };
        foreach (var it in a.Items) c.Items.Add(Clone(it));
        return c;
    }

    static GPacked ClonePacked(GPacked p)
    {
        var c = new GPacked(p.Type) { Is64 = p.Is64, Bytes = (byte[])p.Bytes.Clone() };
        c.Ints.AddRange(p.Ints); c.Reals.AddRange(p.Reals); c.Strings.AddRange(p.Strings);
        return c;
    }
}
