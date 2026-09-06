using System.Security.Cryptography;
using System.Text.Json;

namespace Spyro.Editor.Core.Exporting;

public sealed record NativeFreeFlightInstructionEvidence(string Name, uint RuntimeAddress,
    int ExecutableOffset, int ByteLength, string BeforeHex, string AfterHex);

/// <summary>
/// Exact-USA, detached executable stage. This is not a BIN publisher or a
/// dependency/memory certificate. Callers must merge player animations 33..35
/// into the target and prove runtime fit before composing/publishing an output.
/// </summary>
public sealed class NativeFreeFlightPatchPlan
{
    public const string Profile = "native-free-flight-destination-executable-v1";
    public const string SourceImageSha256 = "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37";
    public const string SourceExecutableSha256 = "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998";
    private const int ExecutableLength = 0x66000;
    private readonly byte[] _sourceExecutable;
    private readonly IReadOnlyList<NativeFreeFlightWindow> _windows;

    private NativeFreeFlightPatchPlan(byte[] executable, int target, bool enabled)
    {
        _sourceExecutable = executable.ToArray();
        _windows = NativeFreeFlightPatchBuilder.Build(target);
        TargetLevelId = target;
        Enabled = enabled;
        RequiredPlayerAnimationIds = Array.AsReadOnly(enabled ? new[] { 33, 34, 35 } : []);
        Instructions = Array.AsReadOnly((enabled ? _windows : [])
            .Select(w => new NativeFreeFlightInstructionEvidence(w.Name, w.Address, w.Offset,
                w.Before.Length, Convert.ToHexStringLower(w.Before), Convert.ToHexStringLower(w.After))).ToArray());
        PlanSha256 = Hash(JsonSerializer.SerializeToUtf8Bytes(new
        {
            Profile, SourceImageSha256, SourceExecutableSha256, TargetLevelId, Enabled,
            Instructions, RequiredPlayerAnimationIds, PublicationAuthorized, RuntimeVerified
        }));
    }

    public int TargetLevelId { get; }
    public bool Enabled { get; }
    public string PlanSha256 { get; }
    public IReadOnlyList<NativeFreeFlightInstructionEvidence> Instructions { get; }
    public IReadOnlyList<int> RequiredPlayerAnimationIds { get; }
    public bool PublicationAuthorized => false;
    public bool RuntimeVerified => false;
    public byte[] CopySourceExecutable() => _sourceExecutable.ToArray();

    public static async Task<NativeFreeFlightPatchPlan> CaptureAsync(string originalImagePath,
        int targetLevelId, bool enabled, CancellationToken cancellationToken = default)
    {
        _ = NativeFreeFlightPatchBuilder.Build(targetLevelId);
        ArgumentException.ThrowIfNullOrWhiteSpace(originalImagePath);
        using FileStream image = new(originalImagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        await RequireOriginalAsync(image, cancellationToken);
        DiscLayout layout = DiscImage.DetectLayout(originalImagePath);
        DiscFileRecord executable = DiscImage.FindRootFileRecord(image, layout, name => name == "SCUS_942.28");
        if (layout.SectorSize != 2352 || layout.UserOffset != 24 || executable.Lba != 53875 || executable.Size != ExecutableLength)
            throw new InvalidDataException("Free Flight source executable domain changed.");
        byte[] bytes = DiscImage.ReadFileBytes(image, layout, executable.Lba, 0, executable.Size);
        if (Hash(bytes) != SourceExecutableSha256)
            throw new InvalidDataException("Free Flight source executable hash changed.");
        RequireConsumers(bytes);
        await RequireOriginalAsync(image, cancellationToken);
        return new(bytes, targetLevelId, enabled);
    }

    public byte[] ApplyToExecutable(ReadOnlySpan<byte> composedExecutable)
    {
        if (composedExecutable.Length != ExecutableLength)
            throw new InvalidDataException("Free Flight executable length changed.");
        byte[] result = composedExecutable.ToArray();
        // Disabled exports rebuild from source; they do not silently accept a
        // previously patched movement engine as a retail-default preimage.
        RequireConsumers(result);
        foreach (var window in _windows)
            RequireBytes(result, window, window.Before, "preimage");
        if (Enabled)
            foreach (var window in _windows)
                window.After.CopyTo(result, window.Offset);
        return result;
    }

    public byte[] RevertExecutable(ReadOnlySpan<byte> composedExecutable)
    {
        if (!Enabled)
            return ApplyToExecutable(composedExecutable);
        if (composedExecutable.Length != ExecutableLength)
            throw new InvalidDataException("Free Flight executable length changed.");
        byte[] result = composedExecutable.ToArray();
        foreach (var window in _windows)
            RequireBytes(result, window, window.After, "afterimage");
        foreach (var window in _windows)
            window.Before.CopyTo(result, window.Offset);
        RequireConsumers(result);
        return result;
    }

    private static async Task RequireOriginalAsync(FileStream image, CancellationToken cancellationToken)
    {
        image.Position = 0;
        if (image.Length != 661547040 ||
            Convert.ToHexStringLower(await SHA256.HashDataAsync(image, cancellationToken)) != SourceImageSha256 ||
            image.Length != 661547040 || image.Position != image.Length)
            throw new InvalidDataException("Free Flight requires the unchanged exact original USA BIN.");
    }

    private static void RequireBytes(ReadOnlySpan<byte> executable, NativeFreeFlightWindow window,
        byte[] expected, string role)
    {
        if (!executable.Slice(window.Offset, expected.Length).SequenceEqual(expected))
            throw new InvalidDataException($"Free Flight {window.Name} {role} changed.");
    }

    private static void RequireConsumers(ReadOnlySpan<byte> executable)
    {
        foreach (var (offset, length, hash, name) in ConsumerBindings)
        {
            if (executable.Length != ExecutableLength || Hash(executable.Slice(offset, length)) != hash)
                throw new InvalidDataException("Free Flight native consumer changed: " + name);
        }
    }

    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    // Complete affected state/movement/reset functions plus the named native
    // consumer spans and the vector helper reached by permission's fallback.
    private static readonly (int Offset, int Length, string Hash, string Name)[] ConsumerBindings =
    [
        (0x40C0, 0x1C8, "e2f73855211aeae760cc766e3e4ff40910c048c64a7ae3f86477cf139d923624", "retail classifier"),
        (0x5CC70, 0x2D, "9f502dce2b68cb8497072c5c9c92d3f914710cdbbcbc9c25c86cbe29a4241e0b", "state animation map"),
        (0x31E70, 0x2974, "51bc8b945b30f188e5d8b48a51c7148c504a453da28ba204622dcd564d1d6121", "state transitions"),
        (0x347E4, 0x3B7C, "8bf295cd4202b6aaad490a2b87df209c43b1d6f508dd10068f50c22ada2d35e4", "player movement"),
        (0x7F58, 0x34, "a80b10978f8b9cf51fdb19dd9d67b776c3285c209aaf023ccfbccf71913d7398", "permission fallback vector addition"),
        (0x3AB0C, 0x94, "e64d1468e828d6ffaae7775449e281cd19e7b16922b355a48bf12a0c0f052c7a", "state caller register continuation"),
        (0x2D994, 0x14, "82085cdfa691731203a7cae1490dd4c47129752e42237ba7a04ac00c193bc629", "state caller first callee entry"),
        (0x2E178, 0x90, "ca06cac431ba357d847d8d8ed6a697ad59935f99123f2e09b10fba09516c50e4", "3D movement vector"),
        (0x2F268, 0x1360, "e180abd727c8b0e686909da5184139e322fd2eaafab721dac6912bbe6829770a", "state initialization"),
        (0x14304, 0xB8, "41edaefb9cb6878f35c39a03a03cde72240eda99b98e5dce9e16b1444c2e481f", "root0 rendering"),
        (0x3B424, 0x214, "9d80836708239aa7f8b5a8888b955928ae0051ca8eb8ed00b4b7e9098e84a3c2", "player reset")
    ];
}
