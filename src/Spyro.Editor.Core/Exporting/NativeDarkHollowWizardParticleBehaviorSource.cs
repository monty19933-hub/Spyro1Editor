using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Exporting;

internal enum NativeParticleBehaviorOperandKind { ParticleBase, StackHalfword }
internal sealed record NativeParticleBehaviorOperandPatch(uint SourcePc, uint BeforeWord, uint AfterWord, NativeParticleBehaviorOperandKind Kind);
internal sealed record NativeParticleBehaviorBridge(uint DestinationPc, NativeByteExtent Owner);
internal sealed record NativeParticleBehaviorContinuation(uint SourcePc, uint OriginalTargetPc, uint DestinationPc);
internal sealed record NativeParticleBehaviorHook(int Selector, bool IsTick, NativeMobyDependencyRead Read, uint BeforeWord, uint SourceEntryPc);

public sealed class NativeDarkHollowWizardParticleBehaviorSource
{
    private const uint OverlayBase = 0x8007AA38;
    private const string WizardSha = "5d3996111567ffac6eca08d340f2f691824bb7969ba2a0cd95e9346ccfe157e1";
    private const string DarkSha = "9920aa47b94005166f457b5b36df6dedd1f2400f91698bac21cdb01d8590ef32";
    private const string ExeSha = "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998";
    private readonly Func<NativeByteExtent, byte[]> _read;
    private static readonly uint[] BridgeTargets = [0x8008442C, 0x80084448, 0x80084450, 0x8008553C];

    private NativeDarkHollowWizardParticleBehaviorSource(byte[] wizard, byte[] dark, byte[] executable)
    {
        Require(wizard, 0x10000, WizardSha); Require(dark, 0xB000, DarkSha); Require(executable, 0x66000, ExeSha);
        var donor = new NativeAddressSpace(NativeAddressKind.LevelData, "particle-donor:" + WizardSha, NativeAddressRevision.Original, wizard.Length);
        var target = new NativeAddressSpace(NativeAddressKind.LevelData, "particle-target:" + DarkSha, NativeAddressRevision.Original, dark.Length);
        var exe = new NativeAddressSpace(NativeAddressKind.ExecutableFile, "particle-exe:" + ExeSha, NativeAddressRevision.Original, executable.Length);
        _read = extent =>
        {
            byte[] bytes = extent.Space == donor ? wizard : extent.Space == target ? dark : extent.Space == exe ? executable :
                throw new InvalidDataException("Unbound particle source canonical address domain.");
            return bytes.AsSpan(checked((int)extent.Offset), checked((int)extent.ByteLength)).ToArray();
        };
        ExecutableRead = new("particle-executable", new(exe, 0, executable.Length), ExeSha);
        List<NativeMobyBehaviorFragmentEvidence> certificates = [];
        void Certificate(string id, byte[] bytes, uint basis, uint pc, int length, string sha)
        {
            Require(bytes.AsSpan(checked((int)(pc - basis)), length), length, sha);
            certificates.Add(new(id, pc, length, sha));
        }
        Certificate("donor-tick-context", wizard, OverlayBase, 0x80087B40, 0x80, "54b8d1235fdcd25f7c07dfeb4a517395fad0849d42197319b8c0713d0bd68827");
        Certificate("donor-spawn-context", wizard, OverlayBase, 0x80089450, 0x90, "531d28fed98c00022f91d0b4ad53ff4455105208d7781389dd9bd8241f910203");
        Certificate("target-tick-context", dark, OverlayBase, 0x80083274, 0x80, "8e85f98b307cc55baa8142a03e2e2c41b64a522f007d7ec29108feb23ed423f4");
        Certificate("target-spawn-context", dark, OverlayBase, 0x800844A0, 0x8C, "deb3935f37cc5a2b2c9fabe4513fa32191b58c850ae52b96df994477521f34e9");
        Certificate("target-tick-continuations", dark, OverlayBase, 0x8008442C, 0x74, "cfb3d347f636a55db2b681378853fef906edf3b18ba5f74015cd339196ed6fcf");
        Certificate("target-spawn-continuation", dark, OverlayBase, 0x8008553C, 0x58, "35108364279f292ac1c03998e2188fefd8ee2f2f6292d91444877e57c169e3ce");
        Certificate("target-callback-initializer", executable, 0x8000F800, 0x8005A878, 0x20, "a4e1d56356d71a62cf4dcd80e14318f8f8f0022b32133796021b3350c22b0ae7");
        Certificate("target-callback-initializer-shared-tail", executable, 0x8000F800, 0x8005B6D8, 0x28, "21cda117e3a5dd3a99cbfd588443a031501ca79b148102d67c8261d15dfdf2b2");
        ContextCertificates = Array.AsReadOnly(certificates.ToArray());

        (string Id, uint Pc, int Length, string Sha)[] spans =
        [
            ("particle07-tick", 0x80088370, 0x98, "a2f5681329e63ab489abec1424ecc230f462f4af53bfc5d0e0ae94bb5a09afc4"),
            ("particle41-tick", 0x800888E8, 0xFC, "bdfa5cf0752d3ca410a216f83a31d196492bd22de836ccfba9255582a5c01a19"),
            ("particle07-spawn", 0x80089934, 0x11C, "c3e0db5def4d5c5c63c10b3e8acf314ebabfa91c93f8a2087a268e78da2df6e3"),
            ("particle07-tail-and41-spawn", 0x80089F28, 0xD0, "fbff816f4c054e52e663d05b0c170db721b3931ce7baac3bd93340837db075d8")
        ];
        var fragments = spans.Select(s => new NativeMipsBehaviorFragmentSource(s.Id, s.Pc,
            new("particle-fragment", new(donor, s.Pc - OverlayBase, s.Length), s.Sha))).ToArray();
        var originalPackage = NativeMipsBehaviorFragmentCodec.Capture(fragments,
            [0x80088370, 0x800888E8, 0x80089934, 0x80089F28, 0x80089F34], [], _read);
        OperandPatches = Array.AsReadOnly(CreatePatches());
        var rewritten = originalPackage.Fragments.Select(f => new NativeMipsCapturedBehaviorFragment(f.Source,
            NativeParticleBehaviorOperandRewriter.Apply(f.CopyBytes(), f.Source.SourcePc,
                OperandPatches.Where(p => p.SourcePc >= f.Source.SourcePc && p.SourcePc < f.Source.SourcePc + f.ByteLength).ToArray()), f.HasFallthrough)).ToArray();
        uint AfterWord(uint pc)
        {
            var f = rewritten.Single(f => pc >= f.Source.SourcePc && pc < f.Source.SourcePc + f.ByteLength);
            return Word(f.CopyBytes(), checked((int)(pc - f.Source.SourcePc)));
        }
        Package = new(rewritten, originalPackage.Transfers.Select(t => t with { DelayWord = t.HasDelaySlot ? AfterWord(t.SourcePc + 4) : t.DelayWord }).ToArray(),
            originalPackage.StackAccesses.Select(s => s with { Offset = s.Offset - 0x40 }).ToArray(),
            originalPackage.EntryPcs, originalPackage.InterceptedExits, _read);
        if (Package.InstructionCount != 224 || Package.Fragments.Any(f => f.HasFallthrough) ||
            Package.Transfers.Any(t => t.Kind is NativeMipsBehaviorTransferKind.CallbackRegister or NativeMipsBehaviorTransferKind.ReturnRegister) ||
            originalPackage.StackAccesses.Count != 12 || originalPackage.StackAccesses.Any(s => s.ByteWidth != 2 || s.Offset is not (0x70 or 0x72 or 0x74)) ||
            OperandPatches.Count != 49 || OperandPatches.Count(p => p.Kind == NativeParticleBehaviorOperandKind.ParticleBase) != 37)
            throw new InvalidDataException("Particle source instruction, scratch or control schema changed.");
        Require(rewritten[0].CopyBytes(), 0x98, "9e2b85a2ed04e469655995982c7f2f4690cb6c414dfa7b080f1a7270c84a08e4");
        Require(rewritten[1].CopyBytes(), 0xFC, "010d86e840c90f458fd1d99fcfdb6149d87b38d129fcb158889c43b732fd0a75");
        SourceFragments = Array.AsReadOnly(spans.Select(s => new NativeMobyBehaviorFragmentEvidence(s.Id, s.Pc, s.Length, s.Sha)).ToArray());
        Continuations = Array.AsReadOnly<NativeParticleBehaviorContinuation>(
        [new(0x8008837C, 0x800893EC, 0x80084448), new(0x8008838C, 0x800893EC, 0x80084448),
         new(0x80088400, 0x800893F4, 0x80084450), new(0x800889DC, 0x800893D0, 0x8008442C),
         new(0x80089F2C, 0x8008A848, 0x8008553C), new(0x80089FF0, 0x8008A848, 0x8008553C)]);
        foreach (var c in Continuations)
            if (!Package.Transfers.Any(t => t.SourcePc == c.SourcePc && t.TargetPc == c.OriginalTargetPc))
                throw new InvalidDataException("Particle continuation source edge changed.");
        uint[] calls = Package.Transfers.Where(t => t.Kind == NativeMipsBehaviorTransferKind.Call26).Select(t => t.TargetPc!.Value).Distinct().Order().ToArray();
        if (!calls.SequenceEqual(new uint[] { 0x80017BFC, 0x80053570, 0x8006272C }))
            throw new InvalidDataException("Particle direct executable call frontier changed.");
        Hooks = Array.AsReadOnly<NativeParticleBehaviorHook>(
        [Hook(7, true, 0x8007AB90, 0x80084450, 0x80088370), Hook(0x41, true, 0x8007AC78, 0x80084450, 0x800888E8),
         Hook(7, false, 0x8007ACD4, 0x8008553C, 0x80089934), Hook(0x41, false, 0x8007ADBC, 0x8008553C, 0x80089F34)]);
        NativeParticleBehaviorHook Hook(int selector, bool tick, uint pc, uint before, uint entry)
        {
            int offset = checked((int)(pc - OverlayBase));
            if (Word(dark, offset) != before) throw new InvalidDataException("Particle destination hook is not the certified default continuation.");
            return new(selector, tick, new("particle-destination-hook", new(target, offset, 4), Hash(dark.AsSpan(offset, 4))), before, entry);
        }
    }

    public static NativeDarkHollowWizardParticleBehaviorSource Capture(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using FileStream image = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        RequireImage(image);
        var layout = DiscImage.DetectLayout(path);
        if (layout.SectorSize != 2352 || layout.UserOffset != 24) throw new InvalidDataException("Particle behavior requires original raw-disc geometry.");
        byte[] header = DiscImage.ReadFileBytes(image, layout, 37, 0, 0x800);
        if (Word(header, 13 * 8) != 0xEF6000 || Word(header, 13 * 8 + 4) != 0xB000 ||
            Word(header, 39 * 8) != 0x2F5B000 || Word(header, 39 * 8 + 4) != 0x10000)
            throw new InvalidDataException("Particle overlay source WAD owners changed.");
        var result = new NativeDarkHollowWizardParticleBehaviorSource(
            DiscImage.ReadFileBytes(image, layout, 37, 0x2F5B000, 0x10000),
            DiscImage.ReadFileBytes(image, layout, 37, 0xEF6000, 0xB000),
            DiscImage.ReadFileBytes(image, layout, 53875, 0, 0x66000));
        RequireImage(image); return result;
    }
    public string SourceImageSha256 => NativeMobySourceInventory.OriginalImageSha256;
    public string SourceOverlaySha256 => DarkSha;
    public string DonorOverlaySha256 => WizardSha;
    public IReadOnlyList<NativeMobyBehaviorFragmentEvidence> SourceFragments { get; }
    public IReadOnlyList<NativeMobyBehaviorFragmentEvidence> ContextCertificates { get; }
    public bool CompleteNativeClosureProven => false;
    public bool BehaviorInstalled => false;
    public bool PublicationApproved => false;
    internal NativeMipsBehaviorFragmentPackage Package { get; }
    internal NativeMobyDependencyRead ExecutableRead { get; }
    internal IReadOnlyList<NativeParticleBehaviorOperandPatch> OperandPatches { get; }
    internal IReadOnlyList<NativeParticleBehaviorContinuation> Continuations { get; }
    internal IReadOnlyList<NativeParticleBehaviorHook> Hooks { get; }
    internal byte[] ReadSourceExtent(NativeByteExtent extent) => _read(extent);
    internal void RequireOriginalDark(ReadOnlySpan<byte> bytes) => Require(bytes, 0xB000, DarkSha);
    internal byte[] CopyBridgeBytes(uint destinationPc)
    {
        if (!BridgeTargets.Contains(destinationPc)) throw new InvalidDataException("Uncertified particle native continuation; no similar-tail substitution.");
        byte[] bytes = new byte[8]; BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x08000000 | ((destinationPc >> 2) & 0x3FFFFFF)); return bytes;
    }
    internal NativeMipsBehaviorLinkedCode LinkInto(NativeByteExtent arena, uint runtimeBase, IReadOnlyList<NativeParticleBehaviorBridge> bridges)
    {
        ArgumentNullException.ThrowIfNull(arena); ArgumentNullException.ThrowIfNull(bridges);
        if (arena.Space.Kind != NativeAddressKind.LevelData || arena.Space.Revision != NativeAddressRevision.Final ||
            arena.Offset < 0xB000 || (ulong)runtimeBase != (ulong)OverlayBase + (ulong)arena.Offset)
            throw new InvalidDataException("Particle final code must append to the retained native Dark overlay and preserve its runtime mapping.");
        var snapshot = NativeMipsBehaviorFragmentCodec.BoundedSnapshot(bridges, 4, "particle bridge");
        if (snapshot.Length != 4 || snapshot.Any(b => b == null || b.Owner == null) ||
            !snapshot.Select(b => b.DestinationPc).Order().SequenceEqual(BridgeTargets))
            throw new InvalidDataException("Particle linker requires exactly its four native continuation keys.");
        foreach (var bridge in snapshot)
        {
            if (bridge.Owner.Space != arena.Space || bridge.Owner.ByteLength != 8 || bridge.Owner.Offset % 4 != 0 ||
                bridge.Owner.Offset < arena.Offset || bridge.Owner.EndExclusive > arena.EndExclusive)
                throw new InvalidDataException("Particle bridge has a detached or partial final owner.");
        }
        var ordered = snapshot.OrderBy(b => b.Owner.Offset).ToArray();
        if (ordered.Zip(ordered.Skip(1)).Any(p => p.First.Owner.EndExclusive > p.Second.Owner.Offset))
            throw new InvalidDataException("Particle continuation bridge byte owners overlap.");
        List<NativeMipsBehaviorExternalBinding> bindings = [];
        foreach (var transfer in Package.Transfers.Where(t => t.TargetPc is uint pc && !Package.OwnsPc(pc)))
        {
            if (transfer.Kind == NativeMipsBehaviorTransferKind.Call26)
            {
                uint pc = transfer.TargetPc!.Value;
                bindings.Add(new(transfer.SourcePc, pc, pc, new(ExecutableRead.Extent.Space, pc - 0x8000F800, 4), ExecutableRead, 0x8000F800));
            }
            else
            {
                var claim = Continuations.Single(c => c.SourcePc == transfer.SourcePc && c.OriginalTargetPc == transfer.TargetPc);
                var bridge = snapshot.Single(b => b.DestinationPc == claim.DestinationPc);
                long mapped = (long)runtimeBase + bridge.Owner.Offset - arena.Offset;
                if (mapped < 0x80000000 || mapped >= 0x80200000) throw new InvalidDataException("Particle bridge mapped address overflows native RAM.");
                bindings.Add(new(transfer.SourcePc, transfer.TargetPc, (uint)mapped, bridge.Owner));
            }
        }
        return Package.Link(arena, runtimeBase, bindings);
    }

    private static NativeParticleBehaviorOperandPatch[] CreatePatches()
    {
        (uint Pc, uint Word)[] memory =
        [
            (0x80088370,0x92420001),(0x80088384,0x92420002),(0x80088394,0x9242000A),(0x80088398,0x9243001D),
            (0x8008839C,0x92440001),(0x800883A4,0xA242000A),(0x800883C4,0xA2420009),(0x800883C8,0x96420003),
            (0x800883CC,0x92460001),(0x800883D0,0x96430017),(0x800883D4,0x96440019),(0x800883D8,0x9645001B),
            (0x800883E0,0xA6420003),(0x800883E4,0x96420005),(0x800883E8,0x96430007),(0x800883F0,0xA2460001),
            (0x800883FC,0xA6420005),(0x80088404,0xA6430007),
            (0x800888E8,0x86440009),(0x800888EC,0x86420003),(0x80088900,0x8642000B),(0x80088904,0x86430005),
            (0x80088918,0x8642000D),(0x8008891C,0x86430007),(0x80088930,0x96420003),(0x8008893C,0xA6420003),
            (0x80088940,0x96420005),(0x80088950,0xA6420005),(0x80088954,0x96420007),(0x80088968,0xA6420007),
            (0x80088998,0x96420009),(0x800889A8,0xA6420009),(0x800889AC,0x9642000B),(0x800889BC,0x9643000D),
            (0x800889C0,0xA642000B),(0x800889C4,0x92420001),(0x800889D4,0xA2420001)
        ];
        (uint Pc, uint Word)[] scratch =
        [(0x800888FC,0xA7A40070),(0x80088914,0xA7A20072),(0x8008892C,0xA7A20074),(0x80088944,0x97A30072),
         (0x80088958,0x97A30074),(0x8008896C,0x97A30070),(0x80088980,0xA7A30070),(0x80088984,0x97A30072),
         (0x80088994,0xA7A30072),(0x8008899C,0x97A30070),(0x800889B0,0x97A30072),(0x800889C8,0x97A40074)];
        return memory.Select(p => new NativeParticleBehaviorOperandPatch(p.Pc, p.Word, (p.Word & ~(31u << 21)) | (17u << 21), NativeParticleBehaviorOperandKind.ParticleBase))
            .Concat(scratch.Select(p => new NativeParticleBehaviorOperandPatch(p.Pc, p.Word, p.Word - 0x40, NativeParticleBehaviorOperandKind.StackHalfword))).OrderBy(p => p.SourcePc).ToArray();
    }
    private static void RequireImage(FileStream image)
    {
        if (image.Length != NativeMobySourceInventory.OriginalImageByteLength) throw new InvalidDataException("Particle behavior requires the original image length.");
        image.Position = 0;
        if (Convert.ToHexStringLower(SHA256.HashData(image)) != NativeMobySourceInventory.OriginalImageSha256 ||
            image.Length != NativeMobySourceInventory.OriginalImageByteLength || image.Position != NativeMobySourceInventory.OriginalImageByteLength)
            throw new InvalidDataException("Particle behavior source image changed before or during capture.");
    }
    private static void Require(ReadOnlySpan<byte> bytes, int length, string hash)
    { if (bytes.Length != length || Hash(bytes) != hash) throw new InvalidDataException("Particle native complete source certificate changed."); }
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static uint Word(ReadOnlySpan<byte> bytes, int at) => BinaryPrimitives.ReadUInt32LittleEndian(bytes[at..]);
}

internal static class NativeParticleBehaviorOperandRewriter
{
    internal static byte[] Apply(ReadOnlySpan<byte> bytes, uint sourcePc, IReadOnlyList<NativeParticleBehaviorOperandPatch> patches)
    {
        ArgumentNullException.ThrowIfNull(patches);
        if (bytes.Length == 0 || bytes.Length > 0x380 || bytes.Length % 4 != 0 || sourcePc % 4 != 0 ||
            sourcePc < 0x80000000 || (ulong)sourcePc + (ulong)bytes.Length > 0x80200000)
            throw new InvalidDataException("Particle operand source exceeds its bounded aligned instruction owner.");
        if (patches.Count is < 0 or > 49) throw new InvalidDataException("Particle operand ledger exceeds49 typed fields.");
        var snapshot = NativeMipsBehaviorFragmentCodec.BoundedSnapshot(patches, 49, "particle operand");
        if (snapshot.Length != patches.Count || snapshot.Any(p => p == null) || snapshot.Select(p => p.SourcePc).Distinct().Count() != snapshot.Length)
            throw new InvalidDataException("Particle operand count or unique field ownership is invalid.");
        foreach (var patch in snapshot)
        {
            if (patch.SourcePc < sourcePc || patch.SourcePc % 4 != 0 || (ulong)patch.SourcePc + 4 > (ulong)sourcePc + (ulong)bytes.Length ||
                BinaryPrimitives.ReadUInt32LittleEndian(bytes[checked((int)(patch.SourcePc - sourcePc))..]) != patch.BeforeWord)
                throw new InvalidDataException("Particle operand exact source instruction preimage is absent.");
            uint op = patch.BeforeWord >> 26, rs = (patch.BeforeWord >> 21) & 31, low = patch.BeforeWord & 65535;
            uint expected = patch.Kind switch
            {
                NativeParticleBehaviorOperandKind.ParticleBase when rs == 18 && op is 0x21 or 0x24 or 0x25 or 0x28 or 0x29 => (patch.BeforeWord & ~(31u << 21)) | (17u << 21),
                NativeParticleBehaviorOperandKind.StackHalfword when rs == 29 && op is 0x25 or 0x29 && low is 0x70 or 0x72 or 0x74 => patch.BeforeWord - 0x40,
                _ => throw new InvalidDataException("Particle operand edit is outside the exact base/halfword semantic grammar.")
            };
            if (patch.AfterWord != expected) throw new InvalidDataException("Particle operand changes an unowned instruction field.");
        }
        byte[] result = bytes.ToArray();
        foreach (var p in snapshot) BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(checked((int)(p.SourcePc - sourcePc))), p.AfterWord);
        return result;
    }
}
