using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace FindTheNeedleEditor.Core;

/// <summary>
/// Godot FileAccessEncrypted ("GDEC"), as written by FileAccess.open_encrypted_with_pass():
/// "GDEC" | MD5(plain) 16 | uint64 plainLength | IV 16 | AES-256-CFB128(plain, zero padded to 16).
/// The key is the 32 ASCII chars of md5_text(password).
/// </summary>
public static class SaveCrypto
{
    /// <summary>Password the game passes to open_encrypted_with_pass for its slot files.</summary>
    public const string GamePassword = "b88b32d1fb83a23ac1facd11a612222774cca9aed41a87ed";

    const uint Magic = 0x43454447; // "GDEC"

    /// <summary>Passwords found later by the key finder (e.g. after a game update), tried after GamePassword.</summary>
    public static List<string> ExtraPasswords { get; } = [];

    /// <summary>Tries the known password, then the extra ones.</summary>
    public static byte[] DecryptAny(byte[] file, out string usedPassword)
    {
        Exception? last = null;
        foreach (var p in ExtraPasswords.Prepend(GamePassword).Distinct())
        {
            try { var d = Decrypt(file, p); usedPassword = p; return d; }
            catch (InvalidDataException ex) when (ex.Message.StartsWith("Checksum")) { last = ex; }
        }
        throw last ?? new InvalidDataException("Checksum mismatch – unknown encryption key or damaged save.");
    }

    static byte[] KeyFor(string password) =>
        Encoding.ASCII.GetBytes(Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(password))).ToLowerInvariant());

    public static bool LooksEncrypted(byte[] file) =>
        file.Length >= 44 && BinaryPrimitives.ReadUInt32LittleEndian(file) == Magic;

    public static byte[] Decrypt(byte[] file, string password = GamePassword)
    {
        if (!LooksEncrypted(file)) throw new InvalidDataException("No GDEC header – not a Find The Needle save.");
        var md5 = file.AsSpan(4, 16).ToArray();
        long len = BinaryPrimitives.ReadInt64LittleEndian(file.AsSpan(20));
        var iv = file.AsSpan(28, 16).ToArray();
        var ct = file.AsSpan(44).ToArray();
        if (len < 0 || len > ct.Length || ct.Length % 16 != 0)
            throw new InvalidDataException("Damaged save: length field does not match the file size.");

        using var aes = Aes.Create();
        aes.Key = KeyFor(password);
        var plain = aes.DecryptCfb(ct, iv, PaddingMode.None, 128);
        var data = plain.AsSpan(0, (int)len).ToArray();
        if (!MD5.HashData(data).AsSpan().SequenceEqual(md5))
            throw new InvalidDataException("Checksum mismatch – unknown encryption key or damaged save.");
        return data;
    }

    public static byte[] Encrypt(byte[] data, string password = GamePassword)
    {
        int padded = (data.Length + 15) / 16 * 16;
        var buf = new byte[padded];
        data.CopyTo(buf, 0);
        var iv = RandomNumberGenerator.GetBytes(16);

        using var aes = Aes.Create();
        aes.Key = KeyFor(password);
        var ct = aes.EncryptCfb(buf, iv, PaddingMode.None, 128);

        var outp = new byte[44 + ct.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(outp, Magic);
        MD5.HashData(data).CopyTo(outp, 4);
        BinaryPrimitives.WriteInt64LittleEndian(outp.AsSpan(20), data.Length);
        iv.CopyTo(outp, 28);
        ct.CopyTo(outp, 44);
        return outp;
    }

    /// <summary>store_var framing: uint32 length, then the encoded Variant.</summary>
    public static object? ParseStoreVar(byte[] data)
    {
        if (data.Length < 8) throw new InvalidDataException("Save is empty.");
        int len = BinaryPrimitives.ReadInt32LittleEndian(data);
        if (len != data.Length - 4) throw new InvalidDataException($"Variant length {len} does not match data size {data.Length - 4}.");
        int pos = 4;
        var v = GVariant.Decode(data, ref pos);
        if (pos != data.Length) throw new InvalidDataException($"Trailing bytes after Variant ({data.Length - pos}).");
        return v;
    }

    public static byte[] BuildStoreVar(object? root)
    {
        var body = GVariant.Encode(root);
        var outp = new byte[body.Length + 4];
        BinaryPrimitives.WriteInt32LittleEndian(outp, body.Length);
        body.CopyTo(outp, 4);
        return outp;
    }
}
