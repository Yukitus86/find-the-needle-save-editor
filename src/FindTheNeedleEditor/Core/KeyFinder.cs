using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace FindTheNeedleEditor.Core;

/// <summary>
/// Finds the save password in the running game: every text string in its memory (UTF-8 and Godot's UTF-32)
/// is tried as open_encrypted_with_pass() password. A candidate must decrypt the first block to the store_var
/// length prefix and then match the file's MD5 – so a hit is certain.
/// </summary>
public static class KeyFinder
{
    public static string? Find(byte[] saveFile, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        if (!SaveCrypto.LooksEncrypted(saveFile)) return null;
        var md5 = saveFile.AsSpan(4, 16).ToArray();
        long len = BinaryPrimitives.ReadInt64LittleEndian(saveFile.AsSpan(20));
        var iv = saveFile.AsSpan(28, 16).ToArray();
        var first = saveFile.AsSpan(44, 16).ToArray();
        var all = saveFile.AsSpan(44).ToArray();
        uint expectPrefix = (uint)(len - 4);

        var proc = Process.GetProcessesByName("FindTheNeedle").FirstOrDefault()
                   ?? throw new InvalidOperationException("game not running");
        IntPtr h = OpenProcess(0x0410, false, proc.Id);
        if (h == IntPtr.Zero) throw new InvalidOperationException("cannot open game process");

        var regions = new List<(long Base, long Size)>();
        long addr = 0;
        while (VirtualQueryEx(h, (IntPtr)addr, out var mbi, (uint)Marshal.SizeOf<MEMORY_BASIC_INFORMATION>()) != 0)
        {
            long b = (long)mbi.BaseAddress, s = (long)mbi.RegionSize;
            if (mbi.State == 0x1000 && (mbi.Protect & 0x101) == 0 && mbi.Protect != 0 && s < 1L << 30) regions.Add((b, s));
            addr = b + s;
            if (addr <= 0) break;
        }
        long total = regions.Sum(r => r.Size), done = 0;
        var tried = new ConcurrentDictionary<string, byte>();
        string? found = null;

        bool Try(string pass)
        {
            if (!tried.TryAdd(pass, 0)) return false;
            var key = Encoding.ASCII.GetBytes(Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(pass))).ToLowerInvariant());
            using var aes = Aes.Create();
            aes.Key = key;
            var ks = aes.EncryptEcb(iv, PaddingMode.None);
            uint prefix = (uint)((ks[0] ^ first[0]) | (ks[1] ^ first[1]) << 8 | (ks[2] ^ first[2]) << 16 | (ks[3] ^ first[3]) << 24);
            if (prefix != expectPrefix) return false;
            var plain = aes.DecryptCfb(all, iv, PaddingMode.None, 128);
            return MD5.HashData(plain.AsSpan(0, (int)len)).AsSpan().SequenceEqual(md5);
        }

        foreach (var (b, s) in regions)
        {
            ct.ThrowIfCancellationRequested();
            var buf = new byte[s];
            if (ReadProcessMemory(h, (IntPtr)b, buf, (IntPtr)s, out _))
            {
                Parallel.For(0, (int)s, new ParallelOptions { CancellationToken = ct }, (i, state) =>
                {
                    if (found is not null) { state.Stop(); return; }
                    // UTF-8 / ASCII run, NUL terminated
                    if (IsP(buf[i]) && (i == 0 || !IsP(buf[i - 1])))
                    {
                        int o = i; while (o < buf.Length && IsP(buf[o]) && o - i < 100) o++;
                        if (o - i >= 8 && o - i < 100 && (o >= buf.Length || buf[o] == 0))
                        {
                            var str = Encoding.ASCII.GetString(buf, i, o - i);
                            if (Try(str)) found = str;
                        }
                    }
                    // UTF-32 run (Godot String), 4-byte aligned
                    if ((i & 3) == 0 && IsP32(buf, i) && (i < 4 || !IsP32(buf, i - 4)))
                    {
                        var sb = new StringBuilder();
                        int o = i; while (IsP32(buf, o) && sb.Length < 100) { sb.Append((char)buf[o]); o += 4; }
                        if (sb.Length >= 8 && sb.Length < 100 && Try(sb.ToString())) found = sb.ToString();
                    }
                });
            }
            done += s;
            progress?.Report((double)done / total);
            if (found is not null) break;
        }
        CloseHandle(h);
        return found;
    }

    static bool IsP(byte b) => b >= 0x20 && b < 0x7f;
    static bool IsP32(byte[] b, int o) => o + 4 <= b.Length && b[o] >= 0x20 && b[o] < 0x7f && b[o + 1] == 0 && b[o + 2] == 0 && b[o + 3] == 0;

    [StructLayout(LayoutKind.Sequential)]
    struct MEMORY_BASIC_INFORMATION { public IntPtr BaseAddress; public IntPtr AllocationBase; public uint AllocationProtect; public ushort PartitionId; public IntPtr RegionSize; public uint State; public uint Protect; public uint Type; }
    [DllImport("kernel32")] static extern IntPtr OpenProcess(int access, bool inherit, int pid);
    [DllImport("kernel32")] static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32")] static extern int VirtualQueryEx(IntPtr h, IntPtr a, out MEMORY_BASIC_INFORMATION m, uint l);
    [DllImport("kernel32")] static extern bool ReadProcessMemory(IntPtr h, IntPtr a, byte[] b, IntPtr s, out IntPtr r);
}
