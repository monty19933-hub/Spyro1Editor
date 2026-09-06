using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;

[assembly: InternalsVisibleTo("Spyro.Editor.NativeId65EngineProfileSmoke")]

namespace Spyro.Editor.Core.Analysis;

public sealed record NativeId65CompanionHelperCall(uint CallerRuntimeAddress, uint TargetRuntimeAddress);

/// <summary>
/// Exact clean-USA prerequisites for a future ID65 Town-native engine profile.
/// This does not emit code, allocate a new level, remove scenery, approve
/// callback no-ops, or relax existing laboratory/save/music/publication gates.
/// </summary>
public sealed class NativeId65EngineProfileContract
{
    public const string ProfileId = "native-id65-town-engine-prerequisites-clean-usa-v1";
    public const string OriginalImageSha256 = "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37";
    private const long OriginalImageByteLength = 661547040;
    private const string ExecutableSha256 = "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998";
    private const string OverlaySha256 = "9bc923cc8d27703537b81b01f51fde351e58aabee87d38a8afc630f1031878a5";
    private const string HeaderSha256 = "15d3e9c45f07e27a0ddd3f3a49b23a077e2c7750021cc89c55b7b16c551186cd";
    private readonly byte[] _executable;
    private readonly byte[] _overlay;
    private readonly byte[] _townData;

    private NativeId65EngineProfileContract(byte[] executable, byte[] overlay, byte[] data,
        NativeId65DetachedEngineInitializer initializer, NativeId65CompanionRequirements companion,
        IEnumerable<NativeId65LocalCompanionRegistration> registrations,
        IEnumerable<ushort> sharedClasses, IEnumerable<NativeId65EngineEvidence> evidence)
    {
        _executable = executable.ToArray();
        _overlay = overlay.ToArray();
        _townData = data.ToArray();
        CopyBufferAddress = initializer.CopyBufferAddress;
        Callbacks = Array.AsReadOnly(initializer.Callbacks.ToArray());
        RequiredCompanion = companion;
        LocalCompanionRegistrations = Array.AsReadOnly(registrations.ToArray());
        SharedPeteClasses = Array.AsReadOnly(sharedClasses.ToArray());
        Evidence = Array.AsReadOnly(evidence.ToArray());
        var helpers = new List<NativeId65CompanionHelperCall>();
        // The complete containing callback and this exact case span are hashed
        // before this linear scan. These are observed direct JAL roots, not a
        // claim to have reconstructed every indirect or transitive dependency.
        for (uint pc = 0x80082FB8; pc < 0x80083B4C; pc += 4)
        {
            uint word = Word(overlay, checked((int)(pc - 0x8007AA38)));
            if (word >> 26 != 3)
                continue;
            uint target = 0x80000000 | ((word & 0x03FFFFFF) << 2);
            if (target < 0x80010000 || target >= 0x8000F800 + executable.Length)
                throw new InvalidDataException("Companion direct helper is outside the bound native executable.");
            helpers.Add(new(pc, target));
        }
        CompanionDirectHelperCalls = helpers.AsReadOnly();
        ContractSha256 = Hash(JsonSerializer.SerializeToUtf8Bytes(new
        {
            ProfileId, OriginalImageSha256, CopyBufferAddress, Callbacks,
            companionPackage = companion.PackageSha256,
            localRegistrations = LocalCompanionRegistrations,
            sharedClasses = SharedPeteClasses, Evidence, CompanionDirectHelperCalls,
            PendingPrerequisites
        }));
    }

    public string SourceImageSha256 => OriginalImageSha256;
    public string ContractSha256 { get; }
    public uint CopyBufferAddress { get; }
    public IReadOnlyList<NativeId65EngineCallback> Callbacks { get; }
    public NativeId65CompanionRequirements RequiredCompanion { get; }
    public IReadOnlyList<NativeId65LocalCompanionRegistration> LocalCompanionRegistrations { get; }
    public IReadOnlyList<ushort> SharedPeteClasses { get; }
    public IReadOnlyList<NativeId65EngineEvidence> Evidence { get; }
    public IReadOnlyList<NativeId65CompanionHelperCall> CompanionDirectHelperCalls { get; }
    public bool SourceCertified => true;
    public bool IndependentLevelSupported => false;
    public bool NoOpCallbacksAuthorized => false;
    public bool CompleteNativeDependencyClosureProven => false;
    public bool PublicationApproved => false;
    public bool RuntimeVerified => false;
    public IReadOnlyList<string> PendingPrerequisites { get; } = Array.AsReadOnly(new[]
    {
        "Independently emitted native level data, scene, collision, properties, fixups and dynamic arena.",
        "Class120 local texture pixels/palette ownership and native renderer/VRAM preservation.",
        "Class120 local sound descriptor indices 0x48/0x49 resolved through the native direct descriptor/bank consumer, not global selector mapping.",
        "Companion particles0x0C/0x42 and the other native player-generated particle selector closures.",
        "Companion auxiliary state, dynamic actor/particle occupancy and peak transient RAM ownership.",
        "Complete state/consumer closure before any balloon callback can be omitted or replaced.",
        "Typed clean-source ID65 classifier, music, target/name and resource-owner composition.",
        "Normal two-ended entry/arrival/exit, zero-egg save/death/reload and separate gameplay acceptance."
    });

    public byte[] CopyExecutable() => _executable.ToArray();
    public byte[] CopyOverlay() => _overlay.ToArray();
    public byte[] CopyTownData() => _townData.ToArray();

    /// <summary>
    /// Exact comparison of these three captured owners, not physical-disc
    /// freshness, a complete dependency certificate or publication authority.
    /// </summary>
    public void RequireCapturedDomains(ReadOnlySpan<byte> executable,
        ReadOnlySpan<byte> overlay, ReadOnlySpan<byte> data)
    {
        if (!executable.SequenceEqual(_executable) || !overlay.SequenceEqual(_overlay) ||
            !data.SequenceEqual(_townData))
            throw new InvalidDataException("Native engine captured source owner or its untouched complement changed.");
    }

    public static async Task<NativeId65EngineProfileContract> CaptureAsync(
        string originalImagePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(originalImagePath);
        string path = Path.GetFullPath(originalImagePath);
        await using FileStream image = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        await RequireImageAsync(image, cancellationToken);
        DiscLayout layout = DiscImage.DetectLayout(path);
        DiscFileRecord wad = DiscImage.FindRootFileRecord(image, layout,
            n => n.Equals("WAD.WAD", StringComparison.OrdinalIgnoreCase));
        DiscFileRecord exe = DiscImage.FindRootFileRecord(image, layout,
            n => n.Equals("SCUS_942.28", StringComparison.OrdinalIgnoreCase));
        if (layout.SectorSize != 2352 || layout.UserOffset != 24 || wad.Lba != 37 ||
            wad.Size != 0x6927000 || exe.Lba != 53875 || exe.Size != 0x66000)
            throw new InvalidDataException("Native engine requires exact original USA WAD/EXE ISO extents.");

        byte[] header = DiscImage.ReadFileBytes(image, layout, wad.Lba, 0, 0x800);
        byte[] executable = DiscImage.ReadFileBytes(image, layout, exe.Lba, 0, exe.Size);
        RequireHash(header, HeaderSha256, "complete WAD header");
        RequireHash(executable, ExecutableSha256, "complete retail executable");
        if (!header.AsSpan(79 * 8, 16).SequenceEqual(new byte[16]) ||
            Word(header, 81 * 8) == 0 || Word(executable, 0x1CA8) != 0x8005B6E0)
            throw new InvalidDataException("Reserved ID65 pair/default dispatcher is not native empty storage.");
        if (Word(header, 15 * 8) != 0x118E800 || Word(header, 15 * 8 + 4) != 0xF800 ||
            Word(header, 16 * 8) != 0x119E000 || Word(header, 16 * 8 + 4) != 0x2E2000)
            throw new InvalidDataException("Town source engine/data owners changed.");

        byte[] overlay = DiscImage.ReadFileBytes(image, layout, wad.Lba, 0x118E800, 0xF800);
        byte[] data = DiscImage.ReadFileBytes(image, layout, wad.Lba, 0x119E000, 0x2E2000);
        RequireHash(overlay, OverlaySha256, "complete Town overlay");
        RequireHash(data, NativeId65EngineProfileDecoder.TownDataSha256, "complete Town data");
        var initializer = NativeId65EngineProfileDecoder.DecodeInitializer(
            executable.AsSpan(0x4B098, 0x98), executable.AsSpan(0x4BEE0, 0x18));
        var companion = NativeId65EngineProfileDecoder.CaptureExactCompanion(data);

        byte[] peteHeader = DiscImage.ReadFileBytes(image, layout, wad.Lba, 0x7B8800, 0x800);
        byte[] peteCopy = DiscImage.ReadFileBytes(image, layout, wad.Lba, 0x7B9000, 0x39C48);
        var shared = NativeMobyModelLoaderCertificate.CaptureRetail(executable, peteHeader, peteCopy);
        if (shared.SharedClasses.Count != 54 || shared.SharedClasses.Contains((ushort)0x78))
            throw new InvalidDataException("Required companion cannot be claimed as a shared PETE class.");

        var registrations = new List<NativeId65LocalCompanionRegistration>();
        for (int index = 0; index < 35; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int entry = 10 + index * 2;
            long offset = Word(header, entry * 8);
            int length = checked((int)Word(header, entry * 8 + 4));
            byte[] localHeader = DiscImage.ReadFileBytes(image, layout, wad.Lba, offset, 0x800);
            registrations.Add(NativeId65EngineProfileDecoder.DecodeLocalRegistration(
                (index / 6 + 1) * 10 + index % 6, localHeader, length));
        }

        var evidence = new List<NativeId65EngineEvidence>();
        foreach (var binding in ExecutableEvidence)
        {
            RequireHash(executable.AsSpan((int)binding.Offset, binding.ByteLength), binding.Sha256, binding.Name);
            evidence.Add(binding);
        }
        foreach (var callback in initializer.Callbacks)
        {
            int offset = checked((int)(callback.RuntimeAddress - 0x8007AA38));
            if (offset < 0 || callback.CodeByteLength <= 0 ||
                offset + (long)callback.CodeByteLength > initializer.CopyBufferAddress - 0x8007AA38)
                throw new InvalidDataException("Callback code owner crosses the source copy-buffer boundary.");
            RequireHash(overlay.AsSpan(offset, callback.CodeByteLength), callback.CodeSha256, callback.Name);
            evidence.Add(new(callback.Name, "WAD", callback.CodeWadOffset, callback.CodeByteLength, callback.CodeSha256));
        }
        RequireHash(overlay.AsSpan(0x8580, 0xB94),
            "19c9ba78c6b34584e058e7f268c517422d453e0406906411583d01c585d3a11f", "complete class120 dispatcher case");
        evidence.Add(new("class120-dispatcher-case-not-standalone-function", "WAD", 0x1196D80, 0xB94,
            "19c9ba78c6b34584e058e7f268c517422d453e0406906411583d01c585d3a11f"));
        evidence.Add(new("class120-complete-local-package", "WAD", 0x1367C7C, 0x2930, companion.PackageSha256));
        evidence.Add(new("shared-PETE-header-class-inventory", "WAD", 0x7B8800, peteHeader.Length, Hash(peteHeader)));
        evidence.Add(new("shared-PETE-complete-native-copy", "WAD", 0x7B9000, peteCopy.Length, Hash(peteCopy)));
        // Complete image freshness is checked on the same open handle after
        // every sparse read. No caller-supplied catalog or receipt is authority.
        await RequireImageAsync(image, cancellationToken);
        return new(executable, overlay, data, initializer, companion, registrations, shared.SharedClasses, evidence);
    }

    private static readonly NativeId65EngineEvidence[] ExecutableEvidence =
    [
        new("initializer-dispatch", "EXE", 0x4AC70, 0x30, "7dba88624efe091d8b34a22b8874a2d12ab577858f7583a324d82f11d14d5c4f"),
        new("Town-ordered-complete-initializer", "EXE", 0x4B098, 0x98, "fe4669229aedbbc8ff698d57a237327af36025aaeda0ff5cc2e1c806fd23e50b"),
        new("default-tail-is-not-an-empty-initializer", "EXE", 0x4BEE0, 0x18, "19566edf84d04523d6cc943feb5107817297413669d3838b10ef868db744151d"),
        new("transition-initializer-call-order", "EXE", 0x5DE4, 0x1C, "09554c866ea59017f365cda874dddc38ee3f82a9e988f3d8a22a71991026adfe"),
        new("normal-loader-companion-spawn", "EXE", 0x4C58, 0x38, "b61080e732f4abf3db1f6721079adddb09e4e02b07f919c149a79a0159471ef8"),
        new("normal-generic-registration-loop", "EXE", 0x62CC, 0x68, "e3333647e8548d11fd19653ac0d706b78571d6dbe1fa439b6db10b5b59235a9b"),
        new("alternate-generic-registration-loop", "EXE", 0x5AC8, 0x6C, "d1be5d70481cc984a2e46e569c3869679e4e16895f8ce9721d1984331acdda1b"),
        new("active-list-builder", "EXE", 0x427EC, 0x1D4, "56e34ffbc80b8e79b1a15e37b0280f88df68bebe61d45d695fea9d2c960707ab"),
        new("animation-advance-and-queued-sounds", "EXE", 0x42AC0, 0x204, "2a3dcb9fa1ba59e0d2e740bcbd912b18155266fee95cf2c2e2f580cfeef72324"),
        new("dynamic-row-and-properties-allocator", "EXE", 0x42CC4, 0xA4, "6625282792e81215f1f35e1aa8b4240937206bcc8bd494eef9e000f65a4bbf9e"),
        new("native-row-defaults", "EXE", 0x2AF20, 0x7C, "e2d690c6097745e4fb354355436805a619c7f31b9d96fa088b90c0518bc6b056"),
        new("model-initial-frame-and-collider", "EXE", 0x42EA8, 0x324, "5483d5e337730567fd89f6118036d31f8c3a93fd281ff444b5d8149b4e2f03d5"),
        new("model-local-sound-descriptor-native-consumer", "EXE", 0x28D1C, 0xA0, "af902cc21e59f2794bc459d6cfaf528f36c28b73f39fc4221c1fe6d384331e91"),
        new("particle-pool-allocator", "EXE", 0x43D70, 0x98, "2eefe927534faee54464ea9af9e53a9382ff3fafa0f90d50b6a68982dce61ddb")
    ];

    private static async Task RequireImageAsync(FileStream image, CancellationToken cancellationToken)
    {
        if (image.Length != OriginalImageByteLength)
            throw new InvalidDataException("Native engine profile rejects non-original source image length.");
        image.Position = 0;
        string actual = Convert.ToHexStringLower(await SHA256.HashDataAsync(image, cancellationToken));
        if (actual != OriginalImageSha256 || image.Length != OriginalImageByteLength ||
            image.Position != OriginalImageByteLength)
            throw new InvalidDataException("Native engine profile rejects changed source identity, including laboratory/candidate inputs.");
    }

    private static void RequireHash(ReadOnlySpan<byte> bytes, string expected, string owner)
    {
        if (Hash(bytes) != expected)
            throw new InvalidDataException("Native engine source evidence changed: " + owner);
    }

    private static uint Word(ReadOnlySpan<byte> bytes, int offset) => NativeId65EngineProfileDecoder.Word(bytes, offset);
    private static string Hash(ReadOnlySpan<byte> bytes) => NativeId65EngineProfileDecoder.Hash(bytes);
}
