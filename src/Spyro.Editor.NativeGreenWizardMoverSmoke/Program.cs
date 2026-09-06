using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;

int checks = 0;
if (args.Length > 1)
    throw new ArgumentException("Usage: NativeGreenWizardMoverSmoke [original.bin]");

Require(NativeGreenWizardMoverMath.ScaleArgument(96, 0) == 96, "ordinary step");
Require(NativeGreenWizardMoverMath.ScaleArgument(96, 3) == 144, "three mode multiplier");
Require(NativeGreenWizardMoverMath.ScaleArgument(96, 4) == 192, "four mode multiplier");
Require(NativeGreenWizardMoverMath.ScaleArgument(9, 3) == 13, "arithmetic half rounding");
Require(NativeGreenWizardMoverMath.ScaleArgument(96, 99) == 96, "native default branch, not inferred frame rate");
Require(NativeGreenWizardMoverMath.SettleZ(1000, 1600, 0) == 1600, "positive 600 snaps");
Require(NativeGreenWizardMoverMath.SettleZ(1000, 1601, 0) == 1250, "positive 601 steps");
Require(NativeGreenWizardMoverMath.SettleZ(1000, 400, 0) == 400, "negative 600 snaps");
Require(NativeGreenWizardMoverMath.SettleZ(1000, 399, 0) == 750, "negative 601 steps");
Require(NativeGreenWizardMoverMath.SettleZ(1000, 1500, 100) == 1600, "signed ground offset included");
Require(NativeGreenWizardMoverMath.SettleZ(1000, 1700, -100) == 1600, "negative signed ground offset");
Require(NativeGreenWizardMoverMath.SettleZ(23552, 23552, 0) == 23552, "no fixed205 origin lift");
Require(NativeGreenWizardMoverMath.ArrivalMetric(0, 0, 256, 0) == 256, "arrival256");
Require(NativeGreenWizardMoverMath.ArrivalMetric(0, 0, 257, 0) == 257, "arrival257");
Require(NativeGreenWizardMoverMath.ArrivalMetric(0, 0, 8, 8) == 11, "native nonEuclidean arrival metric");
Require(NativeGreenWizardMoverMath.Turn(0, 128, 0) == new NativeGreenWizardHeading(8, false), "halfturn direction");
Require(NativeGreenWizardMoverMath.Turn(0, 255, 0) == new NativeGreenWizardHeading(255, true), "negative wrap");
Require(NativeGreenWizardMoverMath.Turn(255, 0, 0) == new NativeGreenWizardHeading(0, true), "positive wrap");
Require(NativeGreenWizardMoverMath.Turn(0, 15, 0) == new NativeGreenWizardHeading(8, true), "heading15 inside threshold");
Require(NativeGreenWizardMoverMath.Turn(0, 16, 0) == new NativeGreenWizardHeading(8, false), "heading16 outside threshold");
Require(NativeGreenWizardMoverMath.Turn(0, 128, 3).Yaw == 12, "scaled heading12");
Require(NativeGreenWizardMoverMath.Turn(0, 128, 4).Yaw == 16, "scaled heading16");

// Independently expressed circular-distance oracle covers every byte pair,
// including the exact half-turn, both wraps and the pre-step return threshold.
foreach (int mode in new[] { 0, 3, 4, 99 })
{
    int turnLimit = mode == 3 ? 12 : mode == 4 ? 16 : 8;
    for (int current = 0; current < 256; current++)
    {
        for (int desired = 0; desired < 256; desired++)
        {
            int clockwise = (desired + 256 - current) % 256;
            int counterclockwise = (current + 256 - desired) % 256;
            int distance = Math.Min(clockwise, counterclockwise);
            int increment = Math.Min(distance, turnLimit);
            int yaw = current;
            if (distance >= increment / 2 + 1)
                yaw += clockwise <= counterclockwise ? increment : -increment;
            var expected = new NativeGreenWizardHeading((byte)((yaw + 256) % 256), distance < 16);
            Require(NativeGreenWizardMoverMath.Turn((byte)current, (byte)desired, mode) == expected,
                "exhaustive wrapped heading oracle");
        }
    }
}
for (int delta = -1200; delta <= 1200; delta++)
{
    int expected = delta >= -600 && delta <= 600 ? 10000 + delta : 10000 + Math.Sign(delta) * 250;
    Require(NativeGreenWizardMoverMath.SettleZ(10000, 10000 + delta, 0) == expected,
        "two-sided settling boundary oracle");
}

var move = NativeGreenWizardMoverMath.ProposeUncorrected(1000, 2000, 3000, 4096, 0, 0, 0, 3000,
    NativeGreenWizardCorrectionObservation.None, NativeGreenWizardCorrectionObservation.None);
Require(move.X == 1096 && move.Y == 2000 && move.Z == 3000, "uncorrected trig step");
Require(move.TerrainProbeZ == 3684 && move.TerrainQueryRadius == 384 && move.ResidentQueryRadius == 384,
    "two distinct query radii and lifted terrain origin");
Require(move.FloorProbeZ == 4024 && move.FloorProbeRange == 5000, "floor search proposal");
Require(!move.PlacementCertified && !move.BodyEnvelopeCertified && !move.RuntimeVerified,
    "caller query assertions cannot grant placement authority");
Require(NativeGreenWizardMoverMath.ProposeUncorrected(1000, 2000, 3000, -1, 0, 0, 0, 3000,
    NativeGreenWizardCorrectionObservation.None, NativeGreenWizardCorrectionObservation.None).X == 999,
    "negative arithmetic shift differs from truncating division");
foreach (var correction in new[]
{
    NativeGreenWizardCorrectionObservation.Required,
    NativeGreenWizardCorrectionObservation.Unknown,
    (NativeGreenWizardCorrectionObservation)99
})
{
    Reject(() => NativeGreenWizardMoverMath.ProposeUncorrected(1000, 2000, 3000, 4096, 0, 0, 0, 3000,
        correction, NativeGreenWizardCorrectionObservation.None), "resident correction");
    Reject(() => NativeGreenWizardMoverMath.ProposeUncorrected(1000, 2000, 3000, 4096, 0, 0, 0, 3000,
        NativeGreenWizardCorrectionObservation.None, correction), "terrain correction");
}
Reject(() => NativeGreenWizardMoverMath.SettleZ(int.MaxValue, 0, 0), "coordinate");
Reject(() => NativeGreenWizardMoverMath.ScaleArgument(-1, 0), "argument");
Reject(() => NativeGreenWizardMoverMath.ScaleArgument(4097, 0), "argument");
Reject(() => NativeGreenWizardMoverMath.ProposeUncorrected(1000, 2000, 3000, 4097, 0, 0, 0, 3000,
    NativeGreenWizardCorrectionObservation.None, NativeGreenWizardCorrectionObservation.None), "trig coefficient");

byte[] package = Fixture();
var collider = NativeGreenWizardMoverContract.CaptureDetachedPackage(package, 0x28F5FDC);
Require(collider.ColliderByteLength == 16 && collider.ColliderWadOffset == 0x28F9580,
    "exact16 byte native owner");
Require(collider.BroadPhaseRadius == 2011 && collider.SphereRadius == 400 && collider.VertexIndex == 0,
    "collider not renderer or movement radius");
Require(collider.Frames.Count == 49 && collider.Frames.Count(f => !f.HasCollider) == 10,
    "all animation selector states");
Require(collider.Select(2, 4).HasCollider && !collider.Select(2, 5).HasCollider,
    "known null transition retained explicitly");
Reject(() => collider.RequireActive(2, 5), "null collider");
Reject(() => collider.Select(3, 0), "animation");
Reject(() => collider.Select(0, 19), "frame");
RejectPackage(package.Concat(new byte[4]).ToArray(), "package boundary");
RejectPackage(package[..^1], "package boundary");
Reject(() => NativeGreenWizardMoverContract.CaptureDetachedPackage(package, -1), "package boundary");
Reject(() => NativeGreenWizardMoverContract.CaptureDetachedPackage(package, long.MaxValue), "package boundary");
MutatePackage(0x35A4 + 4, 0x01900300, "shape");
MutatePackage(0x35A4 + 4, 0x01900201, "shape");
MutatePackage(0x14, 0x800035A8, "collider pointer");
MutatePackage(0x18, 0x800035A4, "null collider slot");
MutatePackage(0x3C, uint.MaxValue, "animation owner");
MutatePackage(0x44 + 0x24, 2u << 21, "selector");
MutatePackage(0x44 + 0x24, 1u << 21, "selector schedule");
byte[] scale = package.ToArray();
scale[0x44 + 5] = 2;
RejectPackage(scale, "scale");
byte[] count = package.ToArray();
count[0x44] = 20;
RejectPackage(count, "frame count");
byte[] overlap = package.ToArray();
Write32(overlap, 0x3C, 0x44);
RejectPackage(overlap, "animation owner");
package[0x35A4] ^= 1;
Require(collider.BroadPhaseRadius == 2011 && collider.Frames.Count == 49, "detached immutable capture");
Require(!collider.BodyEnvelopeCertified && !collider.SourceImageCertified,
    "detached parser is not source or body authority");

if (args.Length == 1)
{
    var source = NativeGreenWizardMoverContract.InspectSource(args[0]);
    Require(source.SourceImageSha256 == NativeGreenWizardCandidateSource.OriginalImageSha256, "exact original source");
    Require(source.Collider.PackageSha256 != collider.PackageSha256 && source.Collider.ColliderByteLength == 16,
        "actual source package distinct from synthetic parser fixture");
    Require(source.Collider.ColliderSha256 == "4fcf7e555a129092f9ab021641572ee5ec053fa4f1a1de148567335674586f25",
        "actual collider golden");
    Require(source.Bindings.Count == 19 && source.Bindings.All(b => b.Sha256.Length == 64), "complete source bindings");
    Require(source.ContractSha256 == NativeGreenWizardMoverContract.InspectSource(args[0]).ContractSha256, "source repeat");
    using var image = File.OpenRead(args[0]);
    var layout = DiscImage.DetectLayout(args[0]);
    byte[] exe = DiscImage.ReadFileBytes(image, layout, 53875, 0, 0x66000);
    byte[] overlay = DiscImage.ReadFileBytes(image, layout, 37, 0x2727000, 0x14000);
    byte[] data = DiscImage.ReadFileBytes(image, layout, 37, 0x273B000, 0x2BE800);
    foreach (int mode in new[] { 0, 3, 4 })
    {
        int speed = mode == 3 ? 144 : mode == 4 ? 192 : 96;
        for (int yaw = 0; yaw < 256; yaw++)
        {
            // Read source coefficients independently of the captured arrays;
            // floor division intentionally differs from C# truncating division.
            short cos = BinaryPrimitives.ReadInt16LittleEndian(exe.AsSpan(0x5D478 + yaw * 2));
            short sin = BinaryPrimitives.ReadInt16LittleEndian(exe.AsSpan(0x5D3F8 + yaw * 2));
            int expectedX = 88832 + (int)Math.Floor(cos * speed / 4096.0);
            int expectedY = 41851 + (int)Math.Floor(sin * speed / 4096.0);
            var step = source.ProposeUncorrected(88832, 41851, 23552, (byte)yaw, mode, 0, 23552,
                NativeGreenWizardCorrectionObservation.None, NativeGreenWizardCorrectionObservation.None);
            Require(step.X == expectedX && step.Y == expectedY && step.Z == 23552,
                "all native trig headings and multipliers match independent source oracle");
        }
    }
    foreach (var binding in source.Bindings.Where(b => b.Domain == "EXE"))
    {
        byte[] changed = exe.ToArray();
        changed[checked((int)binding.SourceOffset)] ^= 1;
        Reject(() => NativeGreenWizardMoverContract.ValidateExactDomains(changed, overlay, data), "executable");
    }
    foreach (var binding in source.Bindings.Where(b => b.Domain == "WAD" && b.SourceOffset < 0x273B000))
    {
        byte[] changedOverlay = overlay.ToArray();
        changedOverlay[checked((int)(binding.SourceOffset - 0x2727000))] ^= 1;
        Reject(() => NativeGreenWizardMoverContract.ValidateExactDomains(exe, changedOverlay, data), "overlay");
    }
    Require(BinaryPrimitives.ReadUInt32LittleEndian(overlay.AsSpan(0x39E8)) == 0x241E0001 &&
        BinaryPrimitives.ReadUInt32LittleEndian(overlay.AsSpan(0x51C)) == 0x800883A0,
        "native dispatcher initializes fifth-argument register and selects class11B handler");
    byte[] changedData = data.ToArray();
    changedData[0x9C] ^= 1;
    Reject(() => NativeGreenWizardMoverContract.ValidateExactDomains(exe, overlay, changedData), "data");
    Require(source.Collider.Select(2, 5).HasCollider == false && source.Collider.Select(0, 0).HasCollider,
        "actual source null/active selector schedule");
    image.Position = 0;
    Require(Convert.ToHexStringLower(SHA256.HashData(image)) == source.SourceImageSha256, "source unchanged after negatives");
    Console.WriteLine($"NATIVE_GREEN_WIZARD_MOVER_SOURCE_SMOKE_OK checks={checks} source=true correctedMovement=false bodyEnvelope=false candidateWritten=false runtime=false");
}
else
{
    Console.WriteLine($"NATIVE_GREEN_WIZARD_MOVER_SMOKE_OK checks={checks} sourceFree=true correctedMovement=false bodyEnvelope=false candidateWritten=false runtime=false");
}

void MutatePackage(int offset, uint value, string message)
{
    byte[] changed = package.ToArray();
    Write32(changed, offset, value);
    RejectPackage(changed, message);
}
void RejectPackage(byte[] bytes, string message) =>
    Reject(() => NativeGreenWizardMoverContract.CaptureDetachedPackage(bytes, 0x28F5FDC), message);
void Reject(Action action, string message)
{
    try
    {
        action();
    }
    catch (InvalidDataException error) when (error.Message.Contains(message, StringComparison.OrdinalIgnoreCase))
    {
        checks++;
        return;
    }
    throw new Exception("Expected rejection: " + message);
}
void Require(bool condition, string message)
{
    if (!condition)
        throw new Exception("Wizard mover regression: " + message);
    checks++;
}
static void Write32(byte[] bytes, int offset, uint value) =>
    BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
static byte[] Fixture()
{
    byte[] bytes = new byte[0x35B4];
    Write32(bytes, 0, 3);
    Write32(bytes, 0x14, 0x800035A4);
    Write32(bytes, 0x34, 0x238);
    int[] offsets = [0x44, 0x100, 0x19C];
    int[] counts = [19, 15, 15];
    for (int animation = 0; animation < 3; animation++)
    {
        int offset = offsets[animation];
        Write32(bytes, 0x38 + animation * 4, (uint)offset);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), (short)counts[animation]);
        bytes[offset + 5] = 1;
        for (int frame = 0; frame < counts[animation]; frame++)
            Write32(bytes, offset + 0x24 + frame * 8, animation == 2 && frame >= 5 ? 1u << 21 : 0);
    }
    Convert.FromHexString("DB070000010390010000000000000000").CopyTo(bytes, 0x35A4);
    return bytes;
}
