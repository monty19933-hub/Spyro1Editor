using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;

int checks = 0;
void Check(bool value, string why) { checks++; if (!value) throw new Exception(why); }
void Refuse(Action action, string why)
{
    try { action(); }
    catch (Exception e) when (e is InvalidDataException or ArgumentException or IOException or InvalidOperationException) { checks++; return; }
    throw new Exception("Expected refusal: " + why);
}
Check(NativeCollectibleStartupResearchPlan.ImageName == "collectible-startup-bootstrap-reservation-r01.bin" &&
    NativeCollectibleStartupResearchPlan.CueName == "collectible-startup-bootstrap-reservation-r01.cue", "fixed artifact names");
Check(NativeCollectibleStartupResearchPlan.ExecutableLba == 53875 &&
    NativeCollectibleStartupResearchPlan.ChangedSectorLbas.SequenceEqual(new[] { 53875, 54078 }), "three spans in TWO raw sectors");
Check(typeof(NativeCollectibleStartupResearchPlan).GetConstructors().Length == 0, "no public arbitrary-bytes constructor");
Refuse(() => NativeCollectibleStartupResearchPublisher.Publish(null!, "unused"), "no untyped recipe");
Refuse(() => NativeCollectibleStartupResearchPublisher.Verify(null!, "unused"), "no untyped verifier");
Refuse(() => NativeCollectibleStartupResearchPlan.RequireExactExecutable(new byte[0x66000]), "fabricated final executable");
if (args.Length == 0)
{
    Console.WriteLine($"COLLECTIBLE_STARTUP_RESEARCH_SOURCE_FREE_OK checks={checks} sourceReads=0 writes=0"); return;
}
if (args.Length < 3 || args[0] != "--source" || args.Length is not (3 or 5))
    throw new ArgumentException("Usage: no args | --source <original-USA-BIN> <original-CUE> [--publish <new-dir> | --verify <dir> | --failure-tests <new-dir>]");
if (args.Length == 5 && args[3] is not ("--publish" or "--verify" or "--failure-tests")) throw new ArgumentException("Unknown explicit mode.");
var plan = NativeCollectibleStartupResearchPlan.Capture(args[1], args[2]);
Check(plan.ResearchOnly && plan.ResearchDiscoveryPublicationEnabled && !plan.NativeInstallationApproved &&
    !plan.ReadyStateIssued && !plan.LifetimeProven && !plan.CardIoApproved && !plan.NormalCreateBinApproved &&
    !plan.RuntimeVerified && !plan.WholeEngineFitProven, "research authority is separate from all product/lifetime gates");
Refuse(plan.RequireNativeInstallation, "research permission cannot install a native owner");
byte[] original = plan.CopyOriginalExecutable(), proposed = plan.CopyExecutable();
Check(plan.RevertExecutableExact(proposed).SequenceEqual(original), "complete original executable inverse");
byte[] detached = plan.CopyExecutable(); detached[0] ^= 1;
Check(plan.CopyExecutable().SequenceEqual(proposed), "executable defensive copy");
Refuse(() => plan.ExpectedSector(-1, new byte[2352]), "negative raw sector");
Refuse(() => plan.ExpectedSector(0, new byte[2351]), "short raw sector");
Refuse(() => plan.ExpectedSector(53875, new byte[2352]), "changed source executable beforeimage");
Refuse(() => NativeCollectibleStartupResearchPlan.RequireExactExecutable(proposed[..^1]), "truncated executable");
Refuse(() => NativeCollectibleStartupResearchPlan.RequireExactExecutable([.. proposed, 0]), "extended executable");
foreach (int at in new[] { 0, 0x10, 0x800, 0x65DA4, 0x65E40, 0x65E6C, 0x65FFF })
{
    byte[] bad = proposed.ToArray(); bad[at] ^= 1;
    Refuse(() => plan.RevertExecutableExact(bad), "entry/reserve/stub/complement inverse mutation");
    Refuse(() => NativeCollectibleStartupResearchPlan.RequireExactExecutable(bad), "fixed afterimage mutation");
}
Check(Enumerable.Range(0, original.Length).Where(i => i is not (>= 0x10 and < 0x14) &&
    i is not (>= 0x65DA4 and < 0x65DA8) && i is not (>= 0x65E40 and < 0x65E6C)).All(i => original[i] == proposed[i]),
    "complete EXE complement unchanged");
byte[] independent = original.ToArray();
BinaryPrimitives.WriteUInt32LittleEndian(independent.AsSpan(0x10), 0x80075640);
BinaryPrimitives.WriteUInt32LittleEndian(independent.AsSpan(0x65DA4), 0xA00);
uint[] words = [0x3C02801F,0x3442F600,0x3C03801F,0x3463F800,0xAC400000,0x24420004,0x0043082B,0x1420FFFC,0,0x08016E38,0];
for (int i = 0; i < words.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(independent.AsSpan(0x65E40 + i * 4), words[i]);
Check(independent.SequenceEqual(proposed), "independent whole EXE recipe matches closed bootstrap ancestry");
using (var source = File.OpenRead(args[1]))
using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
{
    byte[] before = new byte[2352]; int changed = 0;
    for (int lba = 0; lba < source.Length / 2352; lba++)
    {
        source.ReadExactly(before); byte[] expected = before.ToArray();
        if (lba == 53875 || lba == 54078)
        {
            independent.AsSpan((lba - 53875) * 2048, 2048).CopyTo(expected.AsSpan(24, 2048));
            expected = RawMode2Form1SectorIntegrity.RebuildRawSectorCopy(expected, lba);
            RawMode2Form1SectorIntegrity.RequireRawSectorIntegrity(expected, lba);
            Check(expected.AsSpan(0, 24).SequenceEqual(before.AsSpan(0, 24)), "sync/MSF/XA complement retained");
        }
        if (!expected.SequenceEqual(before)) changed++;
        if (!plan.ExpectedSector(lba, before).SequenceEqual(expected)) throw new Exception("Independent expected-sector mismatch at " + lba);
        hash.AppendData(expected);
    }
    Check(changed == 2, "exactly two changed raw sectors, all other raw bytes unchanged");
    Check(Convert.ToHexStringLower(hash.GetHashAndReset()) == plan.ExpectedImageSha256, "independent complete expected image hash");
}
plan.RequireFresh();
Console.WriteLine($"COLLECTIBLE_STARTUP_RESEARCH_SOURCE_OK checks={checks} exe={plan.ExecutableSha256} image={plan.ExpectedImageSha256} changedRawSectors=2 writes=0");
if (args.Length == 5)
{
    if (args[3] == "--failure-tests")
    {
        RunFailureTests(Path.GetFullPath(args[4])); return;
    }
    var result = args[3] == "--publish" ? NativeCollectibleStartupResearchPublisher.Publish(plan, args[4]) :
        NativeCollectibleStartupResearchPublisher.Verify(plan, args[4]);
    Console.WriteLine($"COLLECTIBLE_STARTUP_RESEARCH_{(args[3] == "--publish" ? "PUBLISHED" : "VERIFIED")} image={result.ImagePath} sha256={result.ImageSha256} researchOnly=true nativeInstallationApproved=false");
}

void RunFailureTests(string root)
{
    // This opt-in branch alone writes isolated fixtures. It never mutates the
    // selected source. Retain every test image/partial/lease, including failures.
    if (File.Exists(root) || Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any())
        throw new IOException("Failure-test evidence directory must be new or empty.");
    Directory.CreateDirectory(root);
    string At(string name) => Path.Combine(root, name);
    NativeCollectibleStartupResearchFailure Failure(Action action, NativeCollectibleStartupResearchOutcome state, string label)
    {
        try { action(); }
        catch (NativeCollectibleStartupResearchFailure e)
        {
            Check(e.Outcome == state, label + " actual transaction state");
            Check(Directory.Exists(e.RetainedStageDirectory) && File.Exists(e.RetainedLeasePath), label + " retained stage/lease");
            Check(!File.Exists(Path.Combine(e.OutputDirectory, NativeCollectibleStartupResearchPlan.CueName)), label + " no runnable final CUE");
            return e;
        }
        throw new Exception("Expected typed publication failure: " + label);
    }
    void Flip(string path, long offset)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
        file.Position = offset; int before = file.ReadByte(); if (before < 0) throw new Exception("Test mutation outside fixture.");
        file.Position = offset; file.WriteByte((byte)(before ^ 1)); file.Flush(true);
    }
    string occupied = At("occupied"); Directory.CreateDirectory(occupied);
    File.WriteAllText(Path.Combine(occupied, "keep.txt"), "preexisting evidence");
    Refuse(() => NativeCollectibleStartupResearchPublisher.Publish(plan, occupied), "occupied output");
    Check(File.ReadAllText(Path.Combine(occupied, "keep.txt")) == "preexisting evidence" &&
        Directory.GetFileSystemEntries(occupied).Length == 1, "collision preserves existing evidence");
    string linkTarget = At("link-target"); Directory.CreateDirectory(linkTarget);
    string link = At("link"); Directory.CreateSymbolicLink(link, linkTarget);
    Refuse(() => NativeCollectibleStartupResearchPublisher.Publish(plan, link), "symbolic-link output");
    Refuse(() => NativeCollectibleStartupResearchPublisher.Publish(plan, Path.GetDirectoryName(args[1])!), "source tree overlap");
    using (var cancelled = new CancellationTokenSource())
    {
        cancelled.Cancel();
        try { NativeCollectibleStartupResearchPublisher.Publish(plan, At("cancelled"), cancelled.Token); throw new Exception("Expected cancellation."); }
        catch (OperationCanceledException) { checks++; }
        Check(!Directory.Exists(At("cancelled")), "preflight cancellation does not stage");
    }
    var partial = Failure(() => NativeCollectibleStartupResearchPublisher.ExercisePrecommitFailure(plan, At("partial"),
        NativeCollectibleStartupResearchFault.AfterFirstSector), NativeCollectibleStartupResearchOutcome.NotInvoked, "first sector failure");
    Check(new FileInfo(Path.Combine(partial.RetainedStageDirectory, NativeCollectibleStartupResearchPlan.ImageName)).Length == 2352,
        "exact partial raw-sector evidence retained");
    var badImage = Failure(() => NativeCollectibleStartupResearchPublisher.ExercisePrecommitFailure(plan, At("tampered-stage"),
        NativeCollectibleStartupResearchFault.AfterImageWrite, image => Flip(image, 2352 + 30)),
        NativeCollectibleStartupResearchOutcome.NotInvoked, "changed untouched raw sector");
    Check(badImage.InnerException!.ToString().Contains("raw sector 1", StringComparison.Ordinal), "complete complement readback catches staged corruption");
    var badReceipt = Failure(() => NativeCollectibleStartupResearchPublisher.ExercisePrecommitFailure(plan, At("tampered-receipt"),
        NativeCollectibleStartupResearchFault.BeforePublication, image => File.AppendAllText(
            Path.Combine(Path.GetDirectoryName(image)!, NativeCollectibleStartupResearchPlan.ReceiptName), "x")),
        NativeCollectibleStartupResearchOutcome.NotInvoked, "extended receipt");
    Check(badReceipt.InnerException!.ToString().Contains("exact size changed", StringComparison.Ordinal), "bounded companion EOF refusal");
    var blocked = Failure(() => NativeCollectibleStartupResearchPublisher.ExerciseTransactionCollision(plan, At("transaction-collision")),
        NativeCollectibleStartupResearchOutcome.Unconfirmed, "actual PublishNew lock collision");
    Check(File.Exists(Path.Combine(blocked.OutputDirectory, ".export-publication.lock")), "actual colliding lock retained");

    string success = At("successful-test-publication");
    var published = NativeCollectibleStartupResearchPublisher.Publish(plan, success);
    var verified = NativeCollectibleStartupResearchPublisher.Verify(plan, success);
    Check(published.ImageSha256 == plan.ExpectedImageSha256 && verified == published && published.ChangedRawSectors == 2,
        "actual fresh-only transaction and independent full verification");
    Check(Directory.GetFileSystemEntries(success).Length == 3, "success leaves exactly BIN/receipt/CUE");
    Refuse(() => NativeCollectibleStartupResearchPublisher.Publish(plan, success), "occupied complete set is not implicitly reused");
    byte[] cueBytes = File.ReadAllBytes(published.CuePath);
    File.AppendAllText(published.CuePath, "x");
    Refuse(() => NativeCollectibleStartupResearchPublisher.Verify(plan, success), "complete candidate CUE extension");
    File.WriteAllBytes(published.CuePath, cueBytes);
    Check(NativeCollectibleStartupResearchPublisher.Verify(plan, success).ImageSha256 == plan.ExpectedImageSha256,
        "restored exact selftest CUE verifies again");

    string fixtures = At("source-mutation-fixture"); Directory.CreateDirectory(fixtures);
    string fixtureBin = Path.Combine(fixtures, "source.bin"), fixtureCue = Path.Combine(fixtures, "source.cue");
    File.Copy(args[1], fixtureBin, overwrite: false);
    File.WriteAllText(fixtureCue, "FILE \"source.bin\" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n");
    var fixturePlan = NativeCollectibleStartupResearchPlan.Capture(fixtureBin, fixtureCue);
    byte[] fixtureCueBytes = File.ReadAllBytes(fixtureCue);
    var cueFailure = Failure(() => NativeCollectibleStartupResearchPublisher.ExercisePrecommitFailure(fixturePlan, At("source-cue-mutation"),
        NativeCollectibleStartupResearchFault.AfterFirstSector, _ => File.AppendAllText(fixtureCue, "\n")),
        NativeCollectibleStartupResearchOutcome.NotInvoked, "copied source CUE mutation");
    Check(cueFailure.InnerException!.ToString().Contains("source CUE changed", StringComparison.Ordinal), "named exact CUE guard catches mutation");
    File.WriteAllBytes(fixtureCue, fixtureCueBytes);
    var binFailure = Failure(() => NativeCollectibleStartupResearchPublisher.ExercisePrecommitFailure(fixturePlan, At("source-bin-mutation"),
        NativeCollectibleStartupResearchFault.AfterFirstSector, _ => Flip(fixtureBin, 0)),
        NativeCollectibleStartupResearchOutcome.NotInvoked, "copied source BIN mutation");
    Check(!binFailure.InnerException!.ToString().Contains("Deliberate startup", StringComparison.Ordinal), "real source guard, not forced exception, refused mutated BIN");
    Refuse(fixturePlan.RequireFresh, "mutated fixture remains rejected and retained");
    plan.RequireFresh();
    Console.WriteLine($"COLLECTIBLE_STARTUP_RESEARCH_IO_TESTS_OK checks={checks} evidence={root} successfulSelftest={published.ImagePath} originalSourceUnchanged=true");
}
