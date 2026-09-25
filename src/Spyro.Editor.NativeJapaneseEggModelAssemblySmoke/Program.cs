using System.Buffers.Binary;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;

int checks = 0;
void Check(bool condition, string message)
{ if (!condition) throw new Exception(message); checks++; }
void Refuse(Action action, string message)
{
    try { action(); } catch (InvalidDataException) { checks++; return; }
    throw new Exception("Expected refusal: " + message);
}
static uint Word(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset));
static void Put(byte[] bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
static byte[] ModelFixture()
{
    byte[] b = new byte[0xA0]; Put(b, 0, 1);
    for (int i = 4; i < 0x14; i += 4) Put(b, i, uint.MaxValue);
    Put(b, 0x34, 0x70); Put(b, 0x38, 0x3C); Put(b, 0x3C, 0x00010002);
    b[0x42] = 2; b[0x44] = b[0x45] = 3;
    Put(b, 0x54, 0x18); Put(b, 0x58, 0xC); Put(b, 0x5C, 0x1C);
    Put(b, 0x60, 0xFF000020); Put(b, 0x64, 0x01000000);
    Put(b, 0x68, 0xFF000028); Put(b, 0x6C, 0x01000000);
    Put(b, 0x70, 8); Put(b, 0x74, (1u << 13) | (2u << 4));
    Put(b, 0x7C, 8); Put(b, 0x80, (1u << 13) | (2u << 4));
    Put(b, 0x88, 0xFF010203); Put(b, 0x8C, 0x00010203);
    Put(b, 0x90, 1); Put(b, 0x94, 0x00020003); Put(b, 0x98, 1); Put(b, 0x9C, 0x00020003);
    return b;
}
static byte[] ResourceFixture(int modelLength)
{
    byte[] b = new byte[0x2800 + modelLength]; int cursor = 0x800;
    for (int i = 0; i < 5; i++)
    { int length = i == 2 ? modelLength : 0x800; Put(b, i * 8, (uint)cursor); Put(b, i * 8 + 4, (uint)length); cursor += length; }
    for (int i = 0x800; i < b.Length; i++) b[i] = (byte)(i * 37 + 11);
    for (int i = 0; i < 3; i++) { Put(b, 0x50 + i * 4, (uint)(0x1800 + i * 4)); BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(0x150 + i * 2), (ushort)i); }
    return b;
}
void Verify(NativeJapaneseEggModelAssetPlan asset, NativeJapaneseEggModelAssemblyPlan plan, byte[] before)
{
    byte[] after = plan.CopyLevelData(), package = asset.Model.CopyPackage();
    int rootField = 0x50 + plan.RegistrationSlot * 4, classField = 0x150 + plan.RegistrationSlot * 2;
    Check(plan.PackageOffset == Word(before, 0x10) + Word(before, 0x14), "Append after the whole old model allocation.");
    Check(plan.ModelGrowthBytes == (package.Length + 0x7FF & ~0x7FF) && after.Length == before.Length + plan.ModelGrowthBytes,
        "Only the complete model allocation grows, sector rounded once.");
    Check(Word(before, rootField) == 0 && Word(after, rootField) == plan.PackageOffset &&
        BinaryPrimitives.ReadUInt16LittleEndian(after.AsSpan(classField)) == 0xF5, "One new root and exact F5 class.");
    Check(plan.CopyModelPackage().SequenceEqual(package) && NativeJapaneseEggModel.DecodeFixture(plan.CopyModelPackage()).CopyPackage().SequenceEqual(package),
        "Complete installed package reads back through the existing geometry decoder unchanged.");
    Check(plan.Graph.References.Count == 1 && plan.Graph.References[0].Before == 0 && plan.Graph.References[0].After == plan.PackageOffset &&
        ReferenceEquals(plan.Graph.References[0].Target.Space, plan.FinalDomain), "Existing dependency resolver owns the actual Final registration.");
    Check(plan.Graph.Installs.Count == 2 && plan.Graph.Nodes.Count(n => n.Preserved.Count != 0) ==
        Enumerable.Range(0, 10).Count(i => Word(before, i * 8 + 4) != 0), "Graph materializes only package/root and preserves every original subfile.");
    HashSet<int> changedHeader = [.. Enumerable.Range(rootField, 4), .. Enumerable.Range(classField, 2)];
    for (int i = 0; i < 10; i++)
    {
        int length = (int)Word(before, i * 8 + 4); if (length == 0) continue;
        int oldOffset = (int)Word(before, i * 8), newOffset = (int)Word(after, i * 8);
        Check(after.AsSpan(newOffset, length).SequenceEqual(before.AsSpan(oldOffset, length)), "Preserved original descriptor owner " + i);
        if (newOffset != oldOffset) changedHeader.UnionWith(Enumerable.Range(i * 8, 4));
        if (Word(before, i * 8 + 4) != Word(after, i * 8 + 4)) changedHeader.UnionWith(Enumerable.Range(i * 8 + 4, 4));
    }
    Check(Enumerable.Range(0, 0x800).Where(i => !changedHeader.Contains(i)).All(i => before[i] == after[i]), "Every unowned header byte is preserved.");
    Check(!after.AsSpan(plan.PackageOffset + package.Length, plan.ModelGrowthBytes - package.Length).ContainsAnyExcept((byte)0),
        "Only zero-filled alignment follows the complete package.");
    Check(plan.PointerPlacements.Count == 8 && asset.PointerFields.Count == 8, "Eight exact pointer field owners, not a word scan.");
    foreach (var p in plan.PointerPlacements)
    {
        var original = asset.PointerFields.Single(f => f.PackageFieldOffset == p.PackageFieldOffset);
        Check(p.OriginalFieldOffset == asset.SourceRootDataOffset + p.PackageFieldOffset && p.FinalFieldOffset == plan.PackageOffset + p.PackageFieldOffset &&
            p.OriginalTargetOffset == asset.SourceRootDataOffset + original.PackageTargetOffset && p.FinalTargetOffset == plan.PackageOffset + original.PackageTargetOffset,
            "Typed complete-package map owns field and target " + p.PackageFieldOffset.ToString("X"));
        Check(Word(after, (int)p.FinalFieldOffset) == p.SourceWord && p.SourceWord == original.SourceWord, "Source-form field is never pre-rebased " + p.PackageFieldOffset.ToString("X"));
    }
    Check(Word(after, plan.PackageOffset + 0x50) == 0, "Valid zero-relative near-face pointer remains zero.");
    Check(plan.RevertExact(after).SequenceEqual(before), "Full-resource exact inverse restores all bytes.");
    foreach (int offset in new[] { 0, rootField, classField, plan.PackageOffset + 0x34, plan.PackageOffset + 0x50,
        plan.PackageOffset + package.Length - 1, plan.PackageOffset + package.Length, 0x800, after.Length - 1 })
    { byte[] bad = after.ToArray(); bad[offset] ^= 1; Refuse(() => plan.RevertExact(bad), "changed afterimage byte " + offset.ToString("X")); }
    byte[] detached = plan.CopyLevelData(); detached[plan.PackageOffset] ^= 1;
    Check(plan.CopyLevelData().SequenceEqual(after), "Returned complete resource is detached.");
    Check(!plan.NativeImportApproved && !plan.RuntimeVerified && !plan.PublicationApproved && !asset.NativeImportApproved && !asset.RuntimeVerified && !asset.PublicationApproved,
        "Model assembly confers no behavior, runtime, export or publication approval.");
}
void VerifyProjection(NativeJapaneseEggModelAssetPlan asset)
{
    byte[] before = asset.Model.CopyPackage();
    foreach (uint root in new uint[] { 0x80000000, 0x80060000, 0x80180000, (uint)(0x80200000 - before.Length) })
    {
        var projected = asset.ProjectLoaderAddresses(root);
        foreach (var field in asset.PointerFields)
        {
            var p = projected.Single(p => p.PackageFieldOffset == field.PackageFieldOffset);
            uint target = root + (uint)field.PackageTargetOffset;
            Check(p.SourceWord == field.SourceWord && p.RuntimeTargetAddress == target, "Pure loader address target.");
            Check(field.Encoding == NativeJapaneseEggPointerEncoding.PackedFrameModelDataRelative
                ? (p.ProjectedLoadedWord & 0x1FFFFF) == (target & 0x1FFFFF) && (p.ProjectedLoadedWord & 0xFFE00000) == (field.SourceWord & 0xFFE00000)
                : p.ProjectedLoadedWord == target, "Exact loader projection preserves frame flags and rebases pointers.");
        }
    }
    foreach (uint root in new uint[] { 0, 0x7FFFFFFC, 0x80000001, 0x801FFFFC, 0x80200000, uint.MaxValue })
        Refuse(() => asset.ProjectLoaderAddresses(root), "out-of-domain runtime root " + root.ToString("X"));
    Check(asset.Model.CopyPackage().SequenceEqual(before), "Projection never writes native package bytes.");
}
if (args.Length == 3 && args[0] == "--source")
{
    using var japan = NativeRegionalSourceSession.Open(args[1], NativeDiscRevision.JapanScps10083);
    using var usa = NativeRegionalSourceSession.Open(args[2], NativeDiscRevision.UsaScus94228);
    Refuse(() => NativeJapaneseEggModelAssetPlan.Capture(usa, usa), "USA cannot authenticate JP donor");
    Refuse(() => NativeJapaneseEggModelAssetPlan.Capture(japan, japan), "JP cannot authenticate USA loader");
    var asset = NativeJapaneseEggModelAssetPlan.Capture(japan, usa);
    Check(asset.SourceCertified && asset.JapaneseSourceImageSha256 == japan.SourceImageSha256 && asset.UsaSourceImageSha256 == usa.SourceImageSha256,
        "Issuer authenticates separate exact donor and consumer images.");
    Check(asset.Compatibility is { RelocatedLoaderJumpWords: 2, RelocatedVertexJumpWords: 10, ScratchAddressWords: 2, ClippingConstantWords: 2 },
        "Pinned loader and vertex-consumer comparisons distinguish address/clip changes from package grammar.");
    Check(asset.Model.PackageByteLength == 0x808 && asset.Model.PackageSha256 == NativeJapaneseEggModel.SourcePackageSha256,
        "Only the proved native source package is contributed.");
    Check(asset.PointerFields.Select(p => p.PackageTargetOffset).SequenceEqual(new[] { 0x70, 0x3C, 0x70, 0x45C, 0x318, 0x574, 0x68C, 0x748 }),
        "Independent source pointer-owner oracle, including both animation frames.");
    VerifyProjection(asset);
    var plans = new List<NativeJapaneseEggModelAssemblyPlan>();
    foreach (var resource in new[] { NativeJapaneseEggUsaResource.Artisans, NativeJapaneseEggUsaResource.TownSquare })
    {
        var plan = asset.AssembleOriginalUsaResource(usa, resource); plans.Add(plan);
        byte[] before = usa.ReadWadEntry(plan.SourceWadEntry);
        Verify(asset, plan, before);
        Check(plan.SourceCertified && plan.ModelGrowthBytes == 0x1000, "Exact resource assembly preserves source ancestry and sector growth.");
        var compatibility = plan.Graph.Nodes.Single(n => n.Id == "jp.f5.usa-loader-grammar");
        Check(compatibility.Reads.Count == 4 && plan.Graph.Nodes.Single(n => n.Id == NativeJapaneseEggModelAssetPlan.PackageNodeId)
            .Dependencies.Contains(compatibility.Id), "Exact regional native consumer evidence is a required model dependency.");
        Refuse(() => NativeJapaneseEggModelAssemblyPlan.Compile(asset, plan.CopyLevelData(), plan.SourceWadEntry, true), "appended body cannot impersonate original source");
        byte[] tampered = before.ToArray(); tampered[0x800] ^= 1;
        Refuse(() => NativeJapaneseEggModelAssemblyPlan.Compile(asset, tampered, plan.SourceWadEntry, true), "same-size edited resource cannot impersonate original source");
        Refuse(() => NativeJapaneseEggModelAssemblyPlan.Compile(asset, before, plan.SourceWadEntry, false), "actual issuer cannot lose source ancestry through fixture flag");
        Console.WriteLine($"resource={resource} entry={plan.SourceWadEntry} slot={plan.RegistrationSlot} root=0x{plan.PackageOffset:X} package=0x{plan.PackageByteLength:X} growth=0x{plan.ModelGrowthBytes:X} before={plan.OriginalResourceSha256} after={plan.FinalResourceSha256} graph={plan.DependencyGraphSha256}");
    }
    Check(plans[0].PackageOffset != plans[1].PackageOffset && !ReferenceEquals(plans[0].FinalDomain, plans[1].FinalDomain) &&
        plans[0].CopyModelPackage().SequenceEqual(plans[1].CopyModelPackage()), "Same source model has two independent destination roots/owners without pointer pre-rebasing.");
    Refuse(() => asset.AssembleOriginalUsaResource(japan, NativeJapaneseEggUsaResource.Artisans), "JP resource is not a USA destination");
    Refuse(() => asset.AssembleOriginalUsaResource(usa, (NativeJapaneseEggUsaResource)999), "unproved destination cannot acquire source capability");
    japan.VerifyUnchanged(); usa.VerifyUnchanged();
    Console.WriteLine($"JP_EGG_MODEL_ASSEMBLY_SOURCE_OK checks={checks}");
}
else if (args.Length == 0)
{
    byte[] donor = ModelFixture(); var asset = NativeJapaneseEggModelAssetPlan.Fixture(donor);
    donor[0] = 0;
    Check(!asset.SourceCertified && asset.Compatibility == null && asset.JapaneseSourceImageSha256 == null && asset.UsaSourceImageSha256 == null,
        "Neutral fixtures cannot mint either region identity or consumer proof.");
    VerifyProjection(asset);
    byte[] small = ResourceFixture(0x800), large = ResourceFixture(0x1000);
    var a = NativeJapaneseEggModelAssemblyPlan.Compile(asset, small, -1, false);
    var b = NativeJapaneseEggModelAssemblyPlan.Compile(asset, large, -2, false);
    Verify(asset, a, small); Verify(asset, b, large);
    Check(a.PackageOffset == 0x2000 && b.PackageOffset == 0x2800 && !ReferenceEquals(a.FinalDomain, b.FinalDomain),
        "The same immutable model has separate real owners/roots in two complete destination resources.");
    var repeated = NativeJapaneseEggModelAssemblyPlan.Compile(asset, small, -1, false);
    Check(a.CopyLevelData().SequenceEqual(repeated.CopyLevelData()) && a.DependencyGraphSha256 == repeated.DependencyGraphSha256 &&
        !ReferenceEquals(a.FinalDomain, repeated.FinalDomain), "Deterministic output does not alias independent address owners.");
    Refuse(() => NativeJapaneseEggModelAssemblyPlan.Compile(asset, a.CopyLevelData(), -1, false), "cannot duplicate F5 in one table");
    Refuse(() => NativeJapaneseEggModelAssemblyPlan.Compile(NativeJapaneseEggModelAssetPlan.Fixture(ModelFixture(), [0xF5]), small, -1, false), "shared class collision");
    Refuse(() => NativeJapaneseEggModelAssemblyPlan.Compile(asset, small, -1, true), "cannot relabel fixture as exact source");
    Refuse(() => asset.ReadSource(new(new NativeAddressSpace(asset.OriginalDataDomain.Kind, asset.OriginalDataDomain.OwnerIdentity,
        asset.OriginalDataDomain.Revision, asset.OriginalDataDomain.ByteLength), 0, 4)), "equal labels are not exact source ownership");
    byte[] invalid = small.ToArray(); Put(invalid, 0x10, uint.MaxValue);
    Refuse(() => NativeJapaneseEggModelAssemblyPlan.Compile(asset, invalid, -1, false), "malformed current descriptor");
    Console.WriteLine($"JP_EGG_MODEL_ASSEMBLY_SOURCE_FREE_OK checks={checks}");
}
else throw new ArgumentException("Usage: no arguments for source-free cases; --source <exact-JP-BIN> <exact-USA-BIN> for authenticated composition.");
