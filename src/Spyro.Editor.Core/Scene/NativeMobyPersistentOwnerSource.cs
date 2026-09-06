using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Scene;

/// <summary>
/// Exact retail source issuer for persistent row identities. Catalog metadata is
/// cross-checked by the existing native inventory and never issues authority.
/// Source rows/grouping are not dependency schemas or permission to emit/import.
/// </summary>
public sealed class NativeMobyPersistentOwnerSource
{
    private const string PolicyId = "native-retail-row-identity-only-v1";
    private const string ExecutableSha256 = "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998";
    private readonly NativeMobySourceInventory _inventory;
    private readonly NativeMobyPersistentOwnerLayout _layout;
    internal NativeMobyOwnerSourceBinding Binding { get; }
    public string SourceImageSha256 => Binding.SourceImageSha256;
    public int LevelId => Binding.LevelId;
    public int SaveSlot => Binding.SaveSlot;
    public int OriginalRowCount => Binding.OriginalRowCount;

    private NativeMobyPersistentOwnerSource(NativeMobySourceInventory inventory, NativeMobyOwnerSourceBinding binding)
    { _inventory = inventory; Binding = binding; _layout = new(this); }

    public static NativeMobyPersistentOwnerSource Capture(string sourceImagePath, LevelCatalog catalog, int targetLevelId)
    {
        NativeMobyPersistentOwnerLayout.RequireLevel(targetLevelId);
        ArgumentNullException.ThrowIfNull(catalog);
        // Complete independent source inventory validates all35 actual header/
        // scene chains, including every caller catalog locator/count. An omitted
        // or relabeled donor does not silently reduce the authority scope.
        var inventory = NativeMobySourceInventory.Capture(sourceImagePath, catalog);
        var slots = NativeLevelSlotOwnershipSource.CaptureAsync(sourceImagePath).GetAwaiter().GetResult();
        var slot = slots.Slots.Single(s => s.LevelId == targetLevelId);
        var target = inventory.Levels.Single(l => l.LevelId == targetLevelId);
        if (target.Rows.Count > 256 || target.DataWadEntry != slot.DataDirectoryIndex ||
            slot.SaveRows.Single(r => r.Kind == "object-retirement").SaveOffset != 0x10C + slot.ContinuousIndex * 32)
            throw new InvalidDataException("Source native rows do not fit the exact existing level/save owner binding.");
        byte[] originalRows = target.Rows.SelectMany(r => r.CopyRecord()).ToArray();
        var binding = new NativeMobyOwnerSourceBinding(NativeMobySourceInventory.OriginalImageSha256,
            targetLevelId, slot.ContinuousIndex, target.Rows.Count, Hash(originalRows));
        using FileStream image = File.Open(sourceImagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        RequireImage(image);
        DiscLayout disc = DiscImage.DetectLayout(sourceImagePath);
        if (disc.SectorSize != 2352 || disc.UserOffset != 24) throw new InvalidDataException("Persistent identity requires original MODE2/2352.");
        byte[] exe = DiscImage.ReadFileBytes(image, disc, 53875, 0, 0x66000);
        if (Hash(exe) != ExecutableSha256) throw new InvalidDataException("Persistent native consumer executable changed.");
        // Exact instruction owners: unclamped placed bit consumers, collected/
        // checkpoint reload, save copy callsites, and native word-copy helper.
        (int Offset, int Length, string Sha)[] certificates =
        [
            (0x2BFC0, 0x214, "9afcd3b814b7eec265302f63c0ceb4aae26c321f0571149aae3dcdb735d63b73"),
            (0x4AC8, 0x13C, "3a37e43ef8fcef2f4533c6f8f2ca34e4277ec66bfe1ab8f0f3120837668dd683"),
            (0x49C74, 0xAC, "4af69307b6482ceea3fcaf8487afea4d7ba187b15a465035e80c5108bcf061ea"),
            (0x4A014, 0x24, "45b12460b0eee08b1cb163de0d630bcb39a39e020b60cd310b91e8bdabe9a38f"),
            (0x4A204, 0x24, "531ddec5ba1cce5f57f3f925c65e6dfac3d8bf302e8e9b2996732588debf073c"),
            (0x7158, 0x54, "a3b8e30c7eb4c67ee12309f645026b8b9f522276e769b38295fc7e25d40667f5")
        ];
        foreach (var span in certificates)
            if (Hash(exe.AsSpan(span.Offset, span.Length)) != span.Sha) throw new InvalidDataException("Native owner/lifecycle consumer certificate changed.");
        RequireImage(image);
        return new(inventory, binding);
    }

    public NativeMobyPersistentOwnerLedger Empty() => _layout.Empty();
    public NativeMobyPersistentOwnerLedger Restore(ReadOnlySpan<byte> document) => _layout.Restore(document);
    public NativeMobyPersistentOwnerFamily BindFamily(IEnumerable<NativeMobyPersistentOwnerRoleSelection> selections)
    {
        var snapshot = NativeMobyPersistentOwnerLayout.Snapshot(selections, "source role selections");
        if (snapshot.Length == 0 || snapshot.Any(r => r == null)) throw new InvalidDataException("Source role selection is empty or missing.");
        var roles = snapshot.Select(selection =>
        {
            var row = Row(selection.DonorLevelId, selection.DonorTrueIndex);
            return new NativeMobyOwnerRoleIdentity(selection.RoleId, selection.DonorLevelId, selection.DonorTrueIndex, row.ActorId, row.SourceVariantId);
        }).ToArray();
        return new(PolicyId, roles, this);
    }

    internal NativeMobyPersistentOwnerFamily Rebind(string policy, IReadOnlyList<NativeMobyOwnerRoleIdentity> roles)
    {
        if (policy != PolicyId) throw new InvalidDataException("Unknown source identity policy does not prove native family semantics.");
        foreach (var role in roles)
        {
            var row = Row(role.DonorLevelId, role.DonorTrueIndex);
            if (role.NativeClass != row.ActorId || role.SourceVariantId != row.SourceVariantId)
                throw new InvalidDataException("Persisted role cannot rebind to a different or caller-forged native source variant.");
        }
        return new(PolicyId, roles, this);
    }

    private NativeMobySourceRow Row(int levelId, int index)
    {
        NativeMobyPersistentOwnerLayout.RequireLevel(levelId);
        var level = _inventory.Levels.Single(l => l.LevelId == levelId);
        if (index < 0 || index >= level.Rows.Count || index >= 256)
            throw new InvalidDataException("Source role does not identify a captured ordinary native row0..255.");
        return level.Rows[index];
    }
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void RequireImage(FileStream image)
    {
        if (image.Length != NativeMobySourceInventory.OriginalImageByteLength) throw new InvalidDataException("Persistent source image length changed.");
        image.Position = 0;
        string hash = Convert.ToHexStringLower(SHA256.HashData(image));
        if (image.Length != NativeMobySourceInventory.OriginalImageByteLength || image.Position != image.Length ||
            hash != NativeMobySourceInventory.OriginalImageSha256)
            throw new InvalidDataException("Persistent identity requires the complete unchanged original native source image.");
    }
}
