using System.Security.Cryptography;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Enables the retail game's complete-but-unreachable inventory level-warp handler on
/// disposable Create Swap Test images. This is deliberately exact-USA and test-only.
/// </summary>
public static class TestLevelWarpPatch
{
    public const string PatchKind = "swap-test-level-warp-activation";
    public const string SupportedExecutableName = "SCUS_942.28";
    public const string SupportedExecutableSha256 = "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998";
    public const uint RuntimeAddress = 0x8002D834;
    public const long ExecutableFileOffset = 0x1E034;
    public const string ActivationSequence = "R1, R2, L1, L2, R1, L1, R2, L2";

    private static readonly byte[] ExpectedBefore = [0x37, 0xB6, 0x00, 0x08, 0x00, 0x00, 0x00, 0x00];
    private static readonly byte[] ExpectedSafeFallthrough =
    [
        0x04, 0x00, 0x02, 0x24, // li v0,4
        0x24, 0x00, 0x82, 0x10, // beq a0,v0,0x8002D8D4 (false for cheat id 1)
        0x63, 0x00, 0x02, 0x24, // delay slot: li v0,99
        0x37, 0xB6, 0x00, 0x08  // j 0x8002D8DC (common exit)
    ];

    // sw a0,0x61C(gp) writes cheat id 1 to g_LevelCheatActive (0x80075880).
    // sw zero,0x688(gp) clears cheat_HomeworldSelected (0x800758EC), making
    // repeated warps safe. Retail _gp is 0x80075264.
    private static readonly byte[] Replacement = [0x1C, 0x06, 0x84, 0xAF, 0x88, 0x06, 0x80, 0xAF];

    public static bool IsPatch(MobySourcePatch patch) =>
        string.Equals(patch.Kind, PatchKind, StringComparison.OrdinalIgnoreCase);

    public static string TargetSelectionText(int levelId)
    {
        ValidateLevelId(levelId);
        return $"{ButtonForValue(levelId / 10)}, then {ButtonForValue(levelId % 10)}";
    }

    public static bool TryGetTargetLevelId(MobySourcePatch patch, out int levelId)
    {
        levelId = -1;
        const string prefix = "target-level-id:";
        if (!IsPatch(patch) || !patch.RecordOffset.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        return int.TryParse(patch.RecordOffset[prefix.Length..], out levelId) && IsValidLevelId(levelId);
    }

    internal static MobySourcePatch CreateGuarded(
        FileStream sourceImage,
        DiscLayout layout,
        LevelDefinition level)
    {
        ValidateLevelId(level.LevelId);
        DiscFileRecord executable = FindSupportedExecutable(sourceImage, layout);
        VerifyExactRetailExecutable(sourceImage, layout, executable);
        VerifyBytes(sourceImage, layout, executable, ExpectedBefore, "preimage");
        VerifyBytes(sourceImage, layout, executable, ExecutableFileOffset + ExpectedBefore.Length, ExpectedSafeFallthrough, "case-1 safe fallthrough");

        return new MobySourcePatch(
            Label: $"Enable fast Swap Test entry to {level.DisplayName}",
            Kind: PatchKind,
            LevelKey: level.Key,
            MobyLabel: $"Fast entry to {level.DisplayName}",
            TrueIndex: -1,
            RecordOffset: $"target-level-id:{level.LevelId}",
            WadRelativeOffset: $"exe:0x{RuntimeAddress:X8}",
            ImageOffset: $"0x{DiscImage.ConvertFileOffsetToImageOffset(layout, executable.Lba, ExecutableFileOffset):X}",
            ByteLength: Replacement.Length,
            BeforeHexPreview: ToHex(ExpectedBefore),
            AfterHexPreview: ToHex(Replacement),
            Description: $"Disposable Create Swap Test only. In Inventory, enter {ActivationSequence}, then {TargetSelectionText(level.LevelId)} to load {level.DisplayName} (level {level.LevelId}) from this candidate. Normal Create BIN is unchanged.");
    }

    internal static void VerifyBeforeWrite(FileStream image, DiscLayout layout)
    {
        DiscFileRecord executable = FindSupportedExecutable(image, layout);
        VerifyBytes(image, layout, executable, ExpectedBefore, "preimage immediately before write");
        VerifyBytes(image, layout, executable, ExecutableFileOffset + ExpectedBefore.Length, ExpectedSafeFallthrough, "case-1 safe fallthrough immediately before write");
    }

    internal static void VerifyWritten(FileStream image, DiscLayout layout)
    {
        DiscFileRecord executable = FindSupportedExecutable(image, layout);
        VerifyBytes(image, layout, executable, Replacement, "written readback");
        VerifyBytes(image, layout, executable, ExecutableFileOffset + Replacement.Length, ExpectedSafeFallthrough, "case-1 safe fallthrough readback");
    }

    private static DiscFileRecord FindSupportedExecutable(FileStream image, DiscLayout layout) =>
        DiscImage.FindRootFileRecord(image, layout, name =>
            string.Equals(name, SupportedExecutableName, StringComparison.OrdinalIgnoreCase));

    private static void VerifyExactRetailExecutable(FileStream image, DiscLayout layout, DiscFileRecord executable)
    {
        byte[] bytes = DiscImage.ReadFileBytes(image, layout, executable.Lba, 0, executable.Size);
        string sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        if (!string.Equals(sha256, SupportedExecutableSha256, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Fast Swap Test level entry is guarded to the exact verified USA {SupportedExecutableName} (SHA-256 {SupportedExecutableSha256}); selected executable SHA-256 was {sha256}. No level-warp patch was planned.");
        }
    }

    private static void VerifyBytes(
        FileStream image,
        DiscLayout layout,
        DiscFileRecord executable,
        byte[] expected,
        string guard) =>
        VerifyBytes(image, layout, executable, ExecutableFileOffset, expected, guard);

    private static void VerifyBytes(
        FileStream image,
        DiscLayout layout,
        DiscFileRecord executable,
        long executableOffset,
        byte[] expected,
        string guard)
    {
        byte[] actual = DiscImage.ReadFileBytes(image, layout, executable.Lba, executableOffset, expected.Length);
        if (!actual.SequenceEqual(expected))
        {
            throw new InvalidOperationException(
                $"Fast Swap Test level entry {guard} failed at {SupportedExecutableName}+0x{executableOffset:X}: expected {ToHex(expected)}, found {ToHex(actual)}. No unsafe executable write was accepted.");
        }
    }

    private static void ValidateLevelId(int levelId)
    {
        if (!IsValidLevelId(levelId))
            throw new InvalidOperationException($"Level id {levelId} cannot be selected by the retail level-warp handler.");
    }

    private static bool IsValidLevelId(int levelId) =>
        levelId is >= 10 and <= 64 &&
        levelId / 10 is >= 1 and <= 6 &&
        levelId % 10 is >= 0 and <= 5;

    private static string ButtonForValue(int value) => value switch
    {
        0 => "Circle",
        1 => "Cross",
        2 => "Square",
        3 => "Triangle",
        4 => "Right",
        5 => "Down",
        6 => "Left",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "The retail level-warp handler has no button for this value.")
    };

    private static string ToHex(IEnumerable<byte> bytes) =>
        string.Join(' ', bytes.Select(value => $"{value:X2}"));
}
