using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Spyro.Editor.Core.Exporting;

public static class CrossLevelCandidateRecipeFingerprint
{
    public const int CurrentVersion = 2;

    public static string Create(MobyActorPackageImportPreview preview)
    {
        return Create(preview.RecipeId, preview.RecipeStatus, preview.CopySegments, preview.RootEntries,
            preview.SourceLevelKey, preview.TargetLevelKey, preview.SourceImageSha256);
    }

    public static string Create(
        string recipeId,
        string recipeStatus,
        IEnumerable<MobyActorPackageCopyPreview> copySegments,
        IEnumerable<MobyActorPackageRootPreview> rootEntries,
        string sourceLevelKey = "",
        string targetLevelKey = "",
        string sourceImageSha256 = "")
    {
        StringBuilder builder = new();
        // V1 described addresses but did not identify the bytes actually tested.
        // Keep v2 deliberately incompatible with that unbound runtime evidence.
        builder.Append("v2|");
        AppendField(builder, recipeId);
        AppendField(builder, recipeStatus);
        AppendField(builder, sourceLevelKey);
        AppendField(builder, targetLevelKey);
        AppendField(builder, RequireSha256(sourceImageSha256, "source image"));
        MobyActorPackageCopyPreview[] copies = copySegments.ToArray();
        MobyActorPackageRootPreview[] roots = rootEntries.ToArray();
        if (copies.GroupBy(copy => ParseOffset(copy.TargetStart))
                .Any(group => group.Count() != 1) ||
            roots.GroupBy(root => ParseOffset(root.TargetRootSlot))
                .Any(group => group.Count() != 1))
        {
            throw new InvalidDataException("Duplicate package copy targets or actor-root slots cannot identify one candidate recipe.");
        }
        foreach (MobyActorPackageCopyPreview copy in copies
            .OrderBy(copy => copy.TargetStart, StringComparer.OrdinalIgnoreCase)
            .ThenBy(copy => copy.SourceStart, StringComparer.OrdinalIgnoreCase))
        {
            builder.Append("|copy:");
            AppendField(builder, copy.SourceStart);
            AppendField(builder, copy.TargetStart);
            AppendField(builder, copy.SourceWadRelativeOffset);
            AppendField(builder, copy.TargetWadRelativeOffset);
            AppendField(builder, copy.ByteLength.ToString(CultureInfo.InvariantCulture));
            AppendField(builder, copy.TargetSafety);
            AppendField(builder, RequireSha256(copy.BeforeSha256, "target preimage"));
            // Computed after relocation, so changed internal pointer rebases are
            // part of the imported payload identity as well as model/animation data.
            AppendField(builder, RequireSha256(copy.AfterSha256, "rebased donor payload"));
        }

        foreach (MobyActorPackageRootPreview root in roots
            .OrderBy(root => root.Operation, StringComparer.OrdinalIgnoreCase)
            .ThenBy(root => root.TargetRootSlot, StringComparer.OrdinalIgnoreCase))
        {
            builder.Append("|root:");
            AppendField(builder, root.Operation);
            AppendField(builder, root.TargetRootSlot);
            AppendField(builder, root.TargetRoot);
            AppendField(builder, root.TargetActorId);
            AppendField(builder, root.ExistingRoot);
            AppendField(builder, root.ExistingActorId);
            AppendField(builder, root.RootListWadRelativeOffset);
            AppendField(builder, root.ActorIdListWadRelativeOffset);
        }

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string RequireSha256(string value, string role)
    {
        string normalized = Normalize(value);
        if (normalized.Length != 64 || normalized.Any(character => !Uri.IsHexDigit(character)))
            throw new InvalidDataException($"A full SHA-256 of the {role} is required for content-bound candidate evidence.");
        return normalized;
    }

    private static long ParseOffset(string value)
    {
        string normalized = Normalize(value);
        bool hex = normalized.StartsWith("0x", StringComparison.Ordinal);
        if (!long.TryParse(hex ? normalized[2..] : normalized,
                hex ? NumberStyles.AllowHexSpecifier : NumberStyles.None,
                CultureInfo.InvariantCulture, out long offset) || offset < 0)
        {
            throw new InvalidDataException("A package target must have an exact nonnegative offset.");
        }
        return offset;
    }

    private static void AppendField(StringBuilder builder, string value)
    {
        string normalized = Normalize(value);
        builder.Append(normalized.Length.ToString(CultureInfo.InvariantCulture));
        builder.Append(':');
        builder.Append(normalized);
        builder.Append('|');
    }

    private static string Normalize(string value)
    {
        return (value ?? "").Trim().ToLowerInvariant();
    }
}
