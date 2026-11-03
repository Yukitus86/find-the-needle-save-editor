using System.Diagnostics;
using System.IO;

namespace FindTheNeedleEditor.Core;

/// <summary>One slot_N.dat file as shown in the sidebar.</summary>
public sealed class SlotInfo
{
    public required string Path { get; init; }
    public int Index { get; init; }
    public DateTime LastWrite { get; init; }
    public double Money { get; init; }
    public long Needles { get; init; }
    public int NeedlesInPile { get; init; }
    public double RunSeconds { get; init; }
    public string Pile { get; init; } = "";
    public string? Error { get; init; }

    public string Title => Loc.T("SlotTitle", Index + 1);
    public string MoneyText => Error is null ? "$ " + Fmt.Money(Money) : "–";
    public string NeedleText => Error is null ? $"{Needles} / {NeedlesInPile}" : "–";
    public string PlayTimeText => Fmt.Duration(RunSeconds);
    public string LastPlayedText => LastWrite.ToString("g", Loc.Instance.Culture);
}

public sealed class BackupInfo
{
    public required string Path { get; init; }
    public string Name => System.IO.Path.GetFileName(Path);
    public DateTime Time { get; init; }
    public long Size { get; init; }
    public string TimeText => Time.ToString("G", Loc.Instance.Culture);
    public string SizeText => $"{Size / 1024.0:0.0} KB";
}

public static class SaveStore
{
    public static string DefaultRoot => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Godot", "app_userdata", "Haystack Incremental");

    public static string SavesDir(string root) => System.IO.Path.Combine(root, "saves");
    public static string BackupDir(string root) => System.IO.Path.Combine(root, "EditorBackups");
    public static string ProfilePath(string root) => System.IO.Path.Combine(root, "profile.cfg");

    public static bool IsGameRunning()
    {
        try { return Process.GetProcessesByName("FindTheNeedle").Length > 0; }
        catch (Exception) { return false; }
    }

    public static List<SlotInfo> ListSlots(string root)
    {
        var list = new List<SlotInfo>();
        var dir = SavesDir(root);
        if (!Directory.Exists(dir)) return list;
        foreach (var f in Directory.GetFiles(dir, "slot_*.dat").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            var name = System.IO.Path.GetFileNameWithoutExtension(f);
            int.TryParse(name.AsSpan(5), out int idx);
            try
            {
                var doc = SaveDoc.Load(f);
                list.Add(new SlotInfo
                {
                    Path = f, Index = idx, LastWrite = File.GetLastWriteTime(f),
                    Money = doc.State.Num("money"), Needles = doc.State.Int("needles_found"),
                    NeedlesInPile = doc.PileNeedleCount, RunSeconds = doc.State.Num("run_secs"),
                    Pile = doc.Meta?.Str("pile_size") ?? "",
                });
            }
            catch (Exception ex)
            {
                list.Add(new SlotInfo { Path = f, Index = idx, LastWrite = File.GetLastWriteTime(f), Error = ex.Message });
            }
        }
        return list;
    }

    /// <summary>Copies the slot (and its .bak) into EditorBackups. Returns the backup path.</summary>
    public static string Backup(string root, string slotPath, string tag = "")
    {
        var dir = BackupDir(root);
        Directory.CreateDirectory(dir);
        var stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        var name = System.IO.Path.GetFileNameWithoutExtension(slotPath);
        var target = System.IO.Path.Combine(dir, $"{name}_{stamp}{tag}.dat");
        int n = 1;
        while (File.Exists(target)) target = System.IO.Path.Combine(dir, $"{name}_{stamp}{tag}_{n++}.dat");
        File.Copy(slotPath, target);
        return target;
    }

    public static List<BackupInfo> ListBackups(string root)
    {
        var dir = BackupDir(root);
        if (!Directory.Exists(dir)) return [];
        return Directory.GetFiles(dir, "*.dat")
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.LastWriteTime)
            .Select(f => new BackupInfo { Path = f.FullName, Time = f.LastWriteTime, Size = f.Length })
            .ToList();
    }

    /// <summary>Slot file a backup belongs to ("slot_0_2026-...dat" -> saves\slot_0.dat).</summary>
    public static string SlotForBackup(string root, string backupPath)
    {
        var name = System.IO.Path.GetFileNameWithoutExtension(backupPath);
        int us = name.IndexOf('_', 5);
        var slot = us > 0 ? name[..us] : "slot_0";
        return System.IO.Path.Combine(SavesDir(root), slot + ".dat");
    }

    /// <summary>Writes atomically: temp file, then replace.</summary>
    public static void WriteAtomic(string path, byte[] data)
    {
        var tmp = path + ".editor.tmp";
        File.WriteAllBytes(tmp, data);
        File.Move(tmp, path, overwrite: true);
    }
}
