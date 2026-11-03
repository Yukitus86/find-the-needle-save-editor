using FindTheNeedleEditor.Core;

// Usage:
//   RoundTrip <slot.dat> [...]          decrypt -> parse -> encode -> compare, encrypt round trip
//   RoundTrip --edit <testroot>         edit a COPY: money, needles, teleport, tech, profile; save; reload; verify
int fails = 0;
void Check(bool ok, string what) { Console.WriteLine($"  [{(ok ? "ok" : "FAIL")}] {what}"); if (!ok) fails++; }

if (args.Length == 2 && args[0] == "--findkey")
{
    var sw = System.Diagnostics.Stopwatch.StartNew();
    var pass = KeyFinder.Find(File.ReadAllBytes(args[1]));
    Console.WriteLine($"found: {pass ?? "(none)"} in {sw.Elapsed.TotalSeconds:0.0}s");
    return pass == SaveCrypto.GamePassword ? 0 : 1;
}

if (args.Length == 2 && args[0] == "--edit")
{
    var root = args[1];
    var slot = Path.Combine(SaveStore.SavesDir(root), "slot_0.dat");
    var doc = SaveDoc.Load(slot);
    int hidden = Enumerable.Range(0, doc.PileNeedleCount).Count(i => !doc.IsNeedleTaken(i));
    long found0 = doc.State.Int("needles_found");
    doc.State.SetNum("money", 1234567.5);
    for (int i = 0; i < doc.PileNeedleCount; i++) doc.TakeNeedle(i);
    var (nx, _, nz) = doc.NeedlePos(2);
    doc.SetPlayerPos(nx, doc.HeightAt(nx, nz) + 0.6, nz);
    doc.TechRanks!["belt_speed"] = 9L;
    doc.TechRanks!["jump_height"] = 1L;
    var backup = SaveStore.Backup(root, slot);
    doc.MarkEdited();
    SaveStore.WriteAtomic(slot, doc.Build());

    var re = SaveDoc.Load(slot);
    Check(Math.Abs(re.State.Num("money") - 1234567.5) < 1e-9, "money = 1234567.5");
    Check(re.Meta!.Num("money") == 1234567.5, "meta.money synced");
    Check(Enumerable.Range(0, re.PileNeedleCount).All(re.IsNeedleTaken), "all pile needles taken");
    Check(re.State.Int("needles_found") == found0 + hidden, $"needles_found {found0} + {hidden}");
    Check(Math.Abs(re.PlayerPos.X - nx) < 1e-4 && Math.Abs(re.PlayerPos.Z - nz) < 1e-4, "player teleported");
    Check(re.TechRanks!.Int("belt_speed") == 9 && re.TechRanks!.Int("jump_height") == 1, "tech ranks");
    Check(File.Exists(backup) && SaveDoc.Load(backup).State.Num("money") != 1234567.5, "backup holds old state");
    Check(re.State.Bool("debug_used"), "run marked (debug_used = true)");

    var prof = ProfileCfg.TryLoad(SaveStore.ProfilePath(root))!;
    var before = File.ReadAllLines(prof.Path).Where(l => !l.StartsWith("best_dug=")).ToList();
    prof.Set("best_dug", 99999);
    prof.Save();
    var after = File.ReadAllLines(prof.Path);
    Check(after.Contains("best_dug=99999.0"), "profile best_dug written");
    Check(after.Where(l => !l.StartsWith("best_dug=")).SequenceEqual(before), "profile other lines untouched");

    var prof2 = ProfileCfg.TryLoad(SaveStore.ProfilePath(root))!;
    var beforeTaint = File.ReadAllLines(prof2.Path);
    Check(prof2.MarkTainted(), "profile taint changes file");
    prof2.Save();
    var afterTaint = File.ReadAllLines(prof2.Path);
    Check(afterTaint.Contains("debug_tainted=true") && !afterTaint.Contains("debug_tainted=false"), "profile debug_tainted = true");
    Check(afterTaint.Length == beforeTaint.Length && afterTaint.Where((l, i) => l != beforeTaint[i]).Count() == 1, "only that one line changed");
    Check(!ProfileCfg.TryLoad(prof2.Path)!.MarkTainted(), "second mark is a no-op");
    return fails;
}

foreach (var path in args)
{
    try
    {
        var file = File.ReadAllBytes(path);
        var plain = SaveCrypto.Decrypt(file);
        var root = SaveCrypto.ParseStoreVar(plain);
        var again = SaveCrypto.BuildStoreVar(root);
        bool same = again.AsSpan().SequenceEqual(plain);
        var clone = SaveCrypto.BuildStoreVar(GVariant.Clone(root));
        bool cloneSame = clone.AsSpan().SequenceEqual(plain);
        var enc = SaveCrypto.Encrypt(again);
        var dec = SaveCrypto.Decrypt(enc);
        bool encOk = dec.AsSpan().SequenceEqual(plain) && enc.Length == file.Length;
        Console.WriteLine($"{Path.GetFileName(path)}: plain {plain.Length} B, re-encode identical={same}, clone identical={cloneSame}, encrypt/decrypt ok={encOk}");
        if (!same || !cloneSame || !encOk) fails++;
    }
    catch (Exception ex) { Console.WriteLine($"{path}: FAIL {ex.Message}"); fails++; }
}
return fails;
