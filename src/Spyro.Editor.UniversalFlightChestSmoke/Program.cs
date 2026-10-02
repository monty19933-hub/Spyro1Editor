using System.Text.Json;

using System.Buffers.Binary;

using System.Security.Cryptography;

using Spyro.Editor.Core.Levels;

using Spyro.Editor.Core.Universal;

if (args.Length != 3) throw new ArgumentException("USA BIN catalogDirectory NEWreport");
if (File.Exists(args[2])) throw new IOException("Preserve existing reports");
UniversalSourceLibrary.RequireOriginal(args[0]);
var lib = new UniversalSourceLibrary(args[0], LevelCatalog.Load(args[1]));
string[] keys = ["sunnyflight", "nightflight", "crystalflight", "wildflight", "icyflight"];
int checks = 0;
var labels = new List<string>();
var results = new List<object>();
void Check(bool ok, string why)
{
    if (!ok) throw new Exception(why);
    checks++;
    labels.Add(why);
}

void Reject(Action a, string why)
{
    try
    {
        a();
    }
    catch (Exception ex) when (ex is InvalidDataException or UniversalFlightDependencyException)
    {
        checks++;
        labels.Add(why);
        return;
    }
    throw new Exception("Expected bounded refusal:" + why);
}

int ChestRow(string k) => Enumerable.Range(0, lib.Get(k).Data.Scene().RowCount).First(i => BitConverter.ToUInt16(lib.Get(k).Data.Row(i), 0x36) == 0x12B);
int Category(string k) => BitConverter.ToInt32(lib.Get(k).Data.CaptureRow(k, ChestRow(k)).Properties, 4);

foreach (string sk in keys) foreach (string dk in keys)
    {

        if (Category(sk) != Category(dk)) continue;
        var src = lib.Get(sk);
        var dst = lib.Get(dk);
        int sr = ChestRow(sk), dr = ChestRow(dk);
        var tr = dst.Data.Row(dr);
        var p = new UniversalMobyPlacement(sk, sr, BitConverter.ToInt32(tr, 12), BitConverter.ToInt32(tr, 16), BitConverter.ToInt32(tr, 20), Yaw: tr[0x46]);
        var removed = new HashSet<int> {
 dr }
       ;
        var cert = UniversalFlightChestOwnership.Capture(dk, dst.Data, dst.Overlay, dst.Data, dst.Overlay, [p], removed, lib.Get, lib.Disc.Exe) ?? throw new Exception("issuance missing");
        Check(true, "issued exact original source/context " + sk + "→" + dk);
        var classes = cert.Classes.ToArray();
        var map = classes.Select((c, i) => (c, m: 0x1FD - i)).ToDictionary(x => x.c, x => x.m);
        var maps = new Dictionary<string, IReadOnlyDictionary<int, int>> {
 {
 sk, map }
 }
       ;
        var certs = new Dictionary<string, UniversalFlightChestCertificate> {
 {
 sk, cert }
 }
       ;

        foreach (int type in new[] {
 1, 2, 70 }
)
        {
            var issued = UniversalFlightChestParticles.Inspect(cert, type, src.Overlay, dst.Overlay, src.Data, dst.Data, lib.Disc.Exe);
            Check(issued.Proof.EmptyBody == (type == 70), "issued exact native Flight particle profile " + sk + "→" + dk + ":" + type);
            Check(issued.Dependency.Status.StartsWith("source-certified"), "source marker distinct from gameplay " + sk + "→" + dk + ":" + type);
        }

        foreach (int type in new[] {
 0, 3, 71 }
) Reject(() => UniversalFlightChestParticles.Inspect(cert, type, src.Overlay, dst.Overlay, src.Data, dst.Data, lib.Disc.Exe), "unissued particle type refused " + sk + ":" + type);

        var bundle = UniversalBehaviorBundleBuilder.Build(dst.Overlay, [(sk, src.Overlay, classes)], 0x80190000, classMaps: maps, nativeExecutable: lib.Disc.Exe, flightChests: certs);
        Check(bundle.Code.Length > 0, "raw Bundle issued five-class mapping " + sk + "→" + dk);
        foreach (bool identity in new[] {
 false, true }
       )
        {
            var cm = new Dictionary<string, IReadOnlyDictionary<int, int>> {
 {
 sk, classes.ToDictionary(c => c, c => identity ? c : map[c]) }
 }
           ;
            Reject(() => UniversalBehaviorBundleBuilder.Build(dst.Overlay, [(sk, src.Overlay, classes)], 0x80190000, classMaps: cm, nativeExecutable: lib.Disc.Exe), "raw Bundle without certificate " + sk + "→" + dk + " identity=" + identity);
        }

        foreach (var cb in src.Overlay.Bindings)
        {
            var ob = src.Overlay.Bindings.ToDictionary(x => x.Key, x => x.Value);
            ob[cb.Key] = cb.Value with
            {
                Value = cb.Value.Value + 4
            }
           ;
            Reject(() => cert.RequireSource(new(src.Overlay.Bytes.ToArray(), ob)), "source callback value " + sk + ":" + cb.Key);
            ob[cb.Key] = cb.Value with
            {
                LuiAddress = cb.Value.LuiAddress + 4
            }
           ;
            Reject(() => cert.RequireSource(new(src.Overlay.Bytes.ToArray(), ob)), "source callback instruction address " + sk + ":" + cb.Key);
        }

        byte[] altered = src.Overlay.Bytes.ToArray();
        altered[0] ^= 1;
        Reject(() => cert.RequireSource(new(altered, src.Overlay.Bindings)), "source full byte mutation " + sk);
        byte[] engine = lib.Disc.Exe.ToArray();
        engine[0x47BC8] ^= 1;
        Reject(() => cert.RequireBundle(src.Overlay, dst.Overlay, classes, engine), "native renderer owner mutation " + sk);
        Reject(() => cert.RequireBundle(src.Overlay, lib.Get("darkhollow").Overlay, classes, lib.Disc.Exe), "normal destination cannot reuse context certificate " + sk);
        Reject(() => cert.RequireBundle(src.Overlay, dst.Overlay, classes.Take(4).ToArray(), lib.Disc.Exe), "missing child closure " + sk);
        Reject(() => cert.RequireBundle(src.Overlay, dst.Overlay, [.. classes, 0x159], lib.Disc.Exe), "mixed bonus child closure " + sk);

        UniversalAsmPart Part() => UniversalBehaviorBundleBuilder.BuildPrunedUpdate(src.Overlay, classes, "fixture", map).Part;

        var originalPart = Part();
        cert.Patch(originalPart, src.Overlay, map);
        Check(originalPart.Items.Any(i => i.Word == UniversalMips.EncodeI(9, 0, 4, map[cert.BonusChildClass])), "issued dynamic spawn mapped " + sk);

        var before = Part();
        var spawn = before.Items.FindIndex(i => i.Word != null && cert.SpawnCalls.Contains(i.Orig ?? i.SourcePc ?? 0));
        int start = spawn - 4;
        while (start > 0 && before.Items[start].Word == null) start--;
        var packetItem = before.Items[start];
        uint pc = packetItem.Orig ?? packetItem.SourcePc ?? throw new Exception("source packet missing");

        var changed = Part();
        changed.Items[start] = changed.Items[start] with
        {
            Word = changed.Items[start].Word ^ 1
        }
       ;
        Reject(() => cert.Patch(changed, src.Overlay, map), "emitted source word mutation " + sk);

        var reordered = Part();
        (reordered.Items[start], reordered.Items[start + 1]) = (reordered.Items[start + 1], reordered.Items[start]);
        Reject(() => cert.Patch(reordered, src.Overlay, map), "emitted reordered packet " + sk);

        var insertion = Part();
        insertion.Items.Insert(start + 1, new(UniversalMips.EncodeI(9, 4, 4, 1), default, null, null));
        Reject(() => cert.Patch(insertion, src.Overlay, map), "inserted live spawn consumer " + sk);

        var label = Part();
        label.Items.Insert(start + 1, new(null, default, null, "newInterior"));
        label.Items.Add(new(UniversalMips.EncodeJ(2, 0), new(UniversalFixKind.JumpLabel, Label: "newInterior"), null, null));
        Reject(() => cert.Patch(label, src.Overlay, map), "inserted incoming consumer label " + sk);

        var missing = Part();
        missing.Items.RemoveAt(spawn);
        Reject(() => cert.Patch(missing, src.Overlay, map), "missing issued call " + sk);

        var fix = Part();
        int bi = fix.Items.FindIndex(i => i.Fix.Kind == UniversalFixKind.Branch && i.Orig != null && cert.SpawnCalls.Contains(i.Orig.Value + 0x18));
        Check(bi >= 0, "source branch-delay spawn branch found " + sk);
        fix.Items[bi] = fix.Items[bi] with
        {
            Fix = fix.Items[bi].Fix with
            {
                Orig = fix.Items[bi].Fix.Orig + 4
            }
        }
       ;
        Reject(() => cert.Patch(fix, src.Overlay, map), "wrong relocated original branch target " + sk);

        var incoming = Part();
        incoming.Items.Add(new(UniversalMips.EncodeJ(2, 0), new(UniversalFixKind.Jump, pc + 4), null, null));
        Reject(() => cert.Patch(incoming, src.Overlay, map), "new direct incoming interior " + sk);

        var template = src.Data.CaptureRow(sk, sr);
        foreach (int offset in new[] {
 0x18, 0x3C, 0x3D, 0x3E, 0x3F, 0x40, 0x41, 0x42, 0x48, 0x4B, 0x54 }
       )
        {
            var row = template.Row.ToArray();
            row[offset] ^= 1;
            Reject(() => cert.RequireRow(row, template.Properties, 0x12B), "authored state mutation " + sk + ":" + offset.ToString("X2"));
        }

        var ps = template.Properties.ToArray();
        ps[0] ^= 1;
        Reject(() => cert.RequireRow(template.Row, ps, 0x12B), "authored selector mutation " + sk);
        ps = template.Properties.ToArray();
        ps[4] ^= 1;
        Reject(() => cert.RequireRow(template.Row, ps, 0x12B), "authored objective category mutation " + sk);
        var unrestricted = template.Row.ToArray();
        unrestricted[0x4A] = 255;
        cert.RequireRow(unrestricted, template.Properties, 0x12B);
        Check(true, "native unrestricted culling allowed " + sk);

        var result = UniversalMobyComposer.Compose(dst.Level, dst.Data.Bytes, dst.Overlay.Bytes.ToArray(), lib.Disc.Exe, lib.Get, lib.SharedClasses, UniversalMobyExport.DefaultProtectedVram(args[0], dst.Level).ToArray(), [p], removedRows: [dr], sharedModelPackage: lib.SharedModelPackage, playerAnimationSource: lib.PlayerAnimations);
        var output = new UniversalLevelData(result.LevelData);
        cert.RequireFinal(output, result.Overlay, result.NewRowIndices.Single(), result.Classes.Single(c => c.ClassId == 0x12B).DestinationClassId);
        Check(true, "live Composer final course/row/particle footprint guard " + sk + "→" + dk);
        if (sk == dk)
        {
            Check(result.Overlay.AsSpan(0, checked((int)(dst.Overlay.CopyBuffer - UniversalOverlay.Base))).SequenceEqual(dst.Overlay.Bytes[..checked((int)(dst.Overlay.CopyBuffer - UniversalOverlay.Base))]), "native same-level callback/behavior bytes exact unchanged " + sk);
            Check(!result.Classes.Any(c => c.BehaviorImported), "native controls copy no source handlers " + sk);
        }

        var overlay = result.Overlay.ToArray();
        overlay[0x200] ^= 1;
        Reject(() => cert.RequireFinal(output, overlay, dr, result.Classes.Single(c => c.ClassId == 0x12B).DestinationClassId), "final native context mutation " + sk);
        var bad = new UniversalLevelData(result.LevelData.ToArray());
        int controller = Enumerable.Range(0, bad.Scene().RowCount).Single(i => BitConverter.ToUInt16(bad.Row(i), 0x36) == 0x166);
        var rowController = bad.Row(controller);
        rowController[0x48] ^= 1;
        rowController.CopyTo(bad.Bytes, bad.Subfiles()[3].Offset + bad.Scene().RowsOffset + controller * UniversalLevelData.RowStride);
        Reject(() => cert.RequireFinal(bad, result.Overlay, dr, result.Classes.Single(c => c.ClassId == 0x12B).DestinationClassId), "final controller state mutation " + sk);

        foreach (int type in new[] {
 1, 2 }
)
        {
            var sourceTile = UniversalParticleDependencies.Records(src.Data)[type];
            var destTile = UniversalParticleDependencies.Records(output)[type];
            var changedTile = destTile.ToArray();
            changedTile[4] ^= 1;
            Check(!UniversalParticleDependencies.EquivalentTexture(sourceTile, src.Data.Vram(), changedTile, output.Vram()), "particle descriptor footprint mutation " + sk + "→" + dk + ":" + type);
            int page = BitConverter.ToUInt16(destTile, 6), depth = page >> 7 & 3, u = Math.Min(destTile[0], destTile[4]), x = depth == 0 ? u / 2 : u << (depth - 1), y = Math.Min(destTile[1], destTile[5]);
            int pixel = (((page & 16) != 0 ? 256 : 0) + y) * 1024 + (page & 15) * 128 - 1024 + x;
            var pixelData = new UniversalLevelData(result.LevelData.ToArray());
            pixelData.Vram()[pixel] ^= 1;
            Reject(() => cert.RequireFinal(pixelData, result.Overlay, dr, result.Classes.Single(c => c.ClassId == 0x12B).DestinationClassId), "final particle sampled pixel mutation " + sk + "→" + dk + ":" + type);
            if (depth != 2)
            {
                int clut = BitConverter.ToUInt16(destTile, 2), palette = (clut >> 6) * 1024 + (clut & 63) * 32 - 1024;
                var paletteData = new UniversalLevelData(result.LevelData.ToArray());
                paletteData.Vram()[palette] ^= 1;
                Reject(() => cert.RequireFinal(paletteData, result.Overlay, dr, result.Classes.Single(c => c.ClassId == 0x12B).DestinationClassId), "final particle palette mutation " + sk + "→" + dk + ":" + type);
            }
        }

        var staged = new UniversalLevelData(dst.Data.Bytes.ToArray());
        int stagedPosition = staged.Subfiles()[3].Offset + staged.Scene().RowsOffset + dr * UniversalLevelData.RowStride + 12;
        foreach (int offset in new[]{
0,4,8}
        ) BinaryPrimitives.WriteInt32LittleEndian(staged.Bytes.AsSpan(stagedPosition + offset), -480000);

        var stagedCert = UniversalFlightChestOwnership.Capture(dk, staged, dst.Overlay, dst.Data, dst.Overlay, [p], removed, lib.Get, lib.Disc.Exe) ?? throw new Exception("native removal staging refused");
        Check(true, "issued exact normal native soft-removal staging " + sk + "→" + dk);

        var stagedResult = UniversalMobyComposer.Compose(dst.Level, staged.Bytes, dst.Overlay.Bytes.ToArray(), lib.Disc.Exe, lib.Get, lib.SharedClasses, UniversalMobyExport.DefaultProtectedVram(args[0], dst.Level).ToArray(), [p], removedRows: [dr], sharedModelPackage: lib.SharedModelPackage, playerAnimationSource: lib.PlayerAnimations, removedTreasureAlreadyApplied: true);
        Check(stagedResult.LevelData.SequenceEqual(result.LevelData), "normal staged removal final level equals direct replacement " + sk + "→" + dk);
        Check(stagedResult.Overlay.SequenceEqual(result.Overlay), "normal staged removal final handlers equal direct replacement " + sk + "→" + dk);

        foreach (int offset in new[]{
0,4,8}
)
        {
            var badStaged = new UniversalLevelData(staged.Bytes.ToArray());
            badStaged.Bytes[stagedPosition + offset] ^= 1;
            Reject(() => UniversalFlightChestOwnership.Capture(dk, badStaged, dst.Overlay, dst.Data, dst.Overlay, [p], removed, lib.Get, lib.Disc.Exe), "non-native soft-remove coordinate refused " + sk + ":" + offset);
        }

        var outsideStaged = new UniversalLevelData(staged.Bytes.ToArray());
        outsideStaged.Bytes[stagedPosition - 12 + 0x48] ^= 1;
        Reject(() => UniversalFlightChestOwnership.Capture(dk, outsideStaged, dst.Overlay, dst.Data, dst.Overlay, [p], removed, lib.Get, lib.Disc.Exe), "soft removal cannot hide another state edit " + sk);

        results.Add(new
        {
            Source = sk,
            Destination = dk,
            result.Ram,
            result.Classes,
            ParticleStatus = "source-certified only; runtime pending",
            result.FlightChestParticleProofs,
            Runtime = "not-tested"
        }
);

    }

File.WriteAllText(args[2], JsonSerializer.Serialize(new
{
    Scope = "Private focused positive Flight12B original-category source/context/emitted/post-edit guards with raw Bundle,13Composer cells/native controls. Scoped source particle proof issued; runtime pending.",
    Checks = checks,
    ChecksByName = labels,
    Cells = results,
    CoreSha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(typeof(UniversalMobyComposer).Assembly.Location)))
}
, new JsonSerializerOptions
{
    WriteIndented = true
}
));
Console.WriteLine("FLIGHT_CHEST_OWNERSHIP_GUARDS_OK checks=" + checks);

