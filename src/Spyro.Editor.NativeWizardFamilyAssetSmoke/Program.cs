using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;

if (args.Length != 0 && (args.Length != 2 || args[0] != "--source"))
    throw new ArgumentException("Usage: NativeWizardFamilyAssetSmoke [--source original.bin]");
int checks = 0, refusals = 0;
PrivateStorageCases.Free(Check);
Check(typeof(NativeWizardFamilyAssetPlan).GetConstructors().Length == 0, "source plan has no caller constructor");
Check(typeof(NativeWizardFamilyAssetPlan).IsSealed, "source plan cannot be overridden");
Reject(() => NativeWizardFamilyAssetPlan.CaptureAsync(" ").GetAwaiter().GetResult(), "empty original path");
using (var cancelled = new CancellationTokenSource())
{
    cancelled.Cancel();
    try { await NativeWizardFamilyAssetPlan.CaptureAsync("source-must-not-be-opened.bin", cancelled.Token); throw new Exception("Expected cancellation"); }
    catch (OperationCanceledException) { Check(true, "cancellation precedes source path IO"); }
}
Reject(() => NativeWizardFamilyAssetSource.ValidateCapturedBytes([], [], [], [], []), "absent complete source owners");
var footprint = NativeWizardFamilyAssetSource.ValidateFootprint(0x7025, 0x7A, 32, 96, 63, 127);
Check(footprint == (0x58110, 0x700A0, 16, 32), "independent native 4bpp footprint arithmetic");
foreach (var bad in new (int Clut, int Page, int U0, int V0, int U1, int V1)[]
{
    (0x7025,0xFA,32,96,63,127),
    (0x7000,0x7A,32,96,63,127), (0x7025,0,32,96,63,127),
    (0x7025,0x7A,33,96,64,127), (0x7025,0x7A,32,96,62,127),
    (0x7025,0x7A,-2,96,63,127), (0x7025,0x7A,32,-1,63,127),
    (0x7025,0x7A,32,96,256,127), (0x7025,0x7A,32,96,63,256),
    (0x7025,0x7A,64,96,63,127), (0x7025,0x7A,32,128,63,127),
    (0xFFFF,0x7A,32,96,63,127)
})
    Reject(() => NativeWizardFamilyAssetSource.ValidateFootprint((ushort)bad.Clut, (ushort)bad.Page,
        bad.U0, bad.V0, bad.U1, bad.V1), "bounded whole source pixel/CLUT owner");
Check(NativeWizardFamilyAssetSource.ValidateFootprint(0x7822, 0x38, 160, 192, 163, 195) ==
    (0x70050, 0x78040, 2, 4), "small eight-byte bolt pixels retained, not dropped");
Console.WriteLine($"NATIVE_WIZARD_FAMILY_ASSET_FREE_OK checks={checks} refusals={refusals}");

if (args.Length == 2)
{
    string path = args[1];
    using var image = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    Original();
    try
    {
        var plan = await NativeWizardFamilyAssetPlan.CaptureAsync(path);
        PrivateStorageCases.Source(plan,Check,Reject);
        byte[] block = plan.CopySourceAssetBlock();
        Check(plan.SourceImageSha256 == NativeMobySourceInventory.OriginalImageSha256 &&
            plan.DonorLevelId == 33 && plan.DonorTrueIndex == 6, "exact fixed issuer identity");
        Check(plan.SourceVariantId == NativeMobySourceInventory.OriginalImageSha256 +
            ":level-33:data-9973493f865b5e86bc17b32c5af9e3d7596e25615618c007ed28e0379161f2d8:row-6:59401eafeab453fd9d26f14eedca58390653dc5f4fcc3d2560f7b3e8fc7afa82", "actual inventory variant identity, not a hash prefix");
        Check(block.Length == 0x13274 && plan.BlockSha256 == "af13490b7dbb22fc32ab070e9662ca85faf112c9068816f750ac9b8627a2a147" &&
            Hash(block) == plan.BlockSha256, "independent pre-Core Python full source-form block golden");
        Check(plan.Owners.Count == 26 && plan.Textures.Count == 5 && plan.TextureReferences.Count == 61 &&
            plan.SoundRequirements.Count == 5, "complete fixed owner/reference accounting");
        int cursor = 0;
        foreach (var owner in plan.Owners)
        {
            Check(owner.BlockOffset == cursor && owner.ByteLength > 0 && owner.ByteLength % 4 == 0,
                "owner partition is contiguous and aligned");
            byte[] bytes = plan.CopyOwner(owner.Id);
            Check(bytes.Length == owner.ByteLength && Hash(bytes) == owner.Sha256 &&
                block.AsSpan(cursor, bytes.Length).SequenceEqual(bytes), "actual complete owner bytes");
            int readCursor = 0;
            foreach (var read in plan.SourceSpans.Where(s => s.OwnerId == owner.Id))
            {
                Check(read.OwnerOffset == readCursor && read.ByteLength > 0 &&
                    read.OwnerOffset + (long)read.ByteLength <= bytes.Length, "complete strided source partition");
                byte[] source = ReadWad(read.SourceWadOffset, read.ByteLength);
                Check(bytes.AsSpan(read.OwnerOffset, read.ByteLength).SequenceEqual(source) && Hash(source) == read.Sha256,
                    "independent raw-sector source owner, including referenced zeros");
                readCursor += read.ByteLength;
            }
            Check(readCursor == bytes.Length, "source reads cover every emitted byte");
            cursor += bytes.Length;
            bytes[0] ^= 1;
            Check(Hash(plan.CopyOwner(owner.Id)) == owner.Sha256, "owner defensive copy");
            byte[] corrupt = block.ToArray(); corrupt[owner.BlockOffset] ^= 1;
            Reject(() => plan.RequireExactSourceAssetBlock(corrupt), "each owner mutation refused");
        }
        Check(cursor == block.Length, "no unowned block suffix");
        Check(plan.CopyOwner("wizard.model").AsSpan().SequenceEqual(ReadWad(0x3106518, 0x35B4)) &&
            plan.CopyOwner("wizard.model").AsSpan(0x35AC, 8).SequenceEqual(ReadWad(0x3109AC4, 8)),
            "complete Wizard registered owner, not logical texture payload only");
        byte[] animation = plan.CopyOwner("player.animation8");
        Check(animation.Length == 0x3E1C && Word(animation, 0) == 8 &&
            plan.AnimationTextureHeaderSha256 == "eb181064a87358a7034dc805058a63544657d84173f1a513937f77645da3ec70" &&
            Hash(animation.AsSpan(0x0A, 0x22)) == plan.AnimationTextureHeaderSha256, "correct decimal10 / hex0A header boundary");
        Check(plan.Textures.Sum(t => t.RowBytes * t.Rows) == 2056 && plan.Textures.Count * 32 == 160,
            "all five native pixel and CLUT payloads");
        foreach (var reference in plan.TextureReferences)
            Check(plan.CopyOwner(reference.OwnerId).AsSpan(reference.FieldOffset, reference.ByteLength)
                .SequenceEqual(reference.CopySourceBytes()), "declared source-form texture field preserved");
        Check(plan.TextureReferences.Count(r => r.OwnerId == "wizard.model") == 51 &&
            plan.TextureReferences.Count(r => r.OwnerId == "bolt.model") == 9 &&
            plan.TextureReferences.Single(r => r.OwnerId == "particle7").FieldOffset == 4, "actual complete reference set");
        byte[] fieldCopy = plan.TextureReferences[0].CopySourceBytes(); fieldCopy[0] ^= 1;
        Check(!fieldCopy.SequenceEqual(plan.TextureReferences[0].CopySourceBytes()), "texture field defensive copy");
        Check(plan.SoundRequirements.Select(s => s.SourceDescriptorIndex).SequenceEqual(new[] {49,50,51,40,22}) &&
            plan.SoundRequirements.Select(s => s.Selector).SequenceEqual(new[] {0,1,2,0x1F,0x26}), "local model slots and global selectors separate");
        Check(plan.SoundRequirements.Take(3).All(s => s.Namespace == NativeWizardFamilySoundNamespace.LocalModelSlot) &&
            plan.SoundRequirements.Skip(3).All(s => s.Namespace == NativeWizardFamilySoundNamespace.GlobalSelector), "explicit audio namespaces");
        Check(plan.SoundRequirements.Select(s => s.ExactTownDescriptorIndex).SequenceEqual(new int?[] {null,66,67,null,22}) &&
            plan.SoundRequirements.Select(s => s.SupportedOneShotImport).SequenceEqual(new[] {true,true,true,true,false}),
            "Town exact equivalence versus supported import policy");
        Check(plan.CopyOwner("wizard.model").AsSpan(4, 3).SequenceEqual(new byte[] {49,50,51}) &&
            Word(plan.CopyOwner("global26.descriptor"), 12) == 0x20028, "no target rebinding or unsupported death descriptor normalization");
        Check(Word(plan.CopyOwner("wizard.row"), 0) == 0xC350 && Word(plan.CopyOwner("wizard.private"), 0) == 0xC378,
            "row/private retain original scene-relative encoding, not an installed scene");
        Check(plan.SceneReferences.Count == 2 && plan.SceneReferences[0].FieldOffset == 0 &&
            plan.SceneReferences[1].FieldOffset == 0 && plan.SceneReferences[1].TargetOwnerOffset == 0x28,
            "pointer field is private+0, target private+28");
        Check(plan.SoundReferences.Count == 8 && plan.SoundReferences.Count(r => r.OwnerId == "wizard.model") == 3 &&
            plan.SoundReferences.Count(r => r.ByteWidth == 4) == 5, "all original local selector/sample-offset patch fields declared");
        Check(plan.AnimationReferences.Count == 37 && plan.AnimationReferences.Count(r =>
            r.Domain == NativePlayerAnimationReferenceDomain.SharedPlayerGeometry) == 2,
            "bounded animation reference starts retain explicit external shared geometry domain");
        plan.RequireExactSourceAssetBlock(block);
        byte[] detached = plan.CopySourceAssetBlock(); detached[0] ^= 1;
        Check(Hash(plan.CopySourceAssetBlock()) == plan.BlockSha256, "whole block defensive copy");
        Reject(() => plan.CopyOwner("unknown"), "unknown owner refused");
        Reject(() => plan.RequireExactSourceAssetBlock(block[..^1]), "truncated source-form block");
        Reject(() => plan.RequireExactSourceAssetBlock(block.Concat(new byte[4]).ToArray()), "extra unowned output");
        byte[][] captured = [ReadWad(0x2F6B000,0x28A000), ReadWad(0x119E000,0x2E2000),
            ReadLogical(53875L*2048,0x66000), ReadWad(0x7B8800,0x800), ReadWad(0x7B9000,0x39C48)];
        NativeWizardFamilyAssetSource.ValidateCapturedBytes(captured[0],captured[1],captured[2],captured[3],captured[4]);
        Check(true, "complete captured source validation");
        byte[] directory = ReadWad(0,0x800);
        byte[] foreign = ReadWad(Word(directory,42*8),checked((int)Word(directory,42*8+4)));
        Reject(() => NativeWizardFamilyAssetSource.ValidateCapturedBytes(foreign,captured[1],captured[2],captured[3],captured[4]),
            "actual source34 cannot substitute for fixed33/T6 family");
        foreach (var (which, at) in new[] {(0,0x19EAC4),(0,0x1D4B88),(0,0x173518),(0,0x173A90),
            (0,0x800+0x58110),(0,0x800+0x700A0),(1,0x173000+10),(2,0x39A4),(3,4),(4,0x58)})
        {
            byte[] copy = captured[which].ToArray(); copy[at] ^= 1; byte[] original = captured[which]; captured[which] = copy;
            Reject(() => NativeWizardFamilyAssetSource.ValidateCapturedBytes(captured[0],captured[1],captured[2],captured[3],captured[4]), "source mutation/padding/certificate rejected");
            captured[which] = original;
        }
        Check(plan.BlockSha256 == Hash(plan.CopySourceAssetBlock()), "refused operations preserve complete frozen result");
    }
    finally { Original(); }
    Console.WriteLine($"NATIVE_WIZARD_FAMILY_ASSET_SOURCE_OK checks={checks} refusals={refusals} sourceUnchanged=true outputsWritten=0");

    void Original()
    {
        image.Position = 0;
        Check(image.Length == NativeMobySourceInventory.OriginalImageByteLength &&
            Convert.ToHexStringLower(SHA256.HashData(image)) == NativeMobySourceInventory.OriginalImageSha256 &&
            image.Position == image.Length && image.ReadByte() == -1, "whole original source SHA/length/actual EOF");
    }
    byte[] ReadWad(long offset,int n) => ReadLogical(37L*2048+offset,n);
    byte[] ReadLogical(long offset,int n)
    {
        byte[] bytes = new byte[n]; int cursor = 0;
        while(cursor<n)
        {
            int count = Math.Min(n-cursor,2048-(int)(offset%2048));
            image.Position = offset/2048*2352+24+offset%2048;
            image.ReadExactly(bytes.AsSpan(cursor,count));cursor+=count;offset+=count;
        }
        return bytes;
    }
}

void Check(bool ok,string label) { if(!ok) throw new InvalidDataException(label); checks++; }
void Reject(Action action,string label)
{
    try {action();} catch(Exception error) when(error is InvalidDataException or ArgumentException)
    {refusals++;Check(true,label);return;}
    throw new Exception("Expected refusal: "+label);
}
static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
static uint Word(ReadOnlySpan<byte> bytes,int at) => BinaryPrimitives.ReadUInt32LittleEndian(bytes[at..]);
