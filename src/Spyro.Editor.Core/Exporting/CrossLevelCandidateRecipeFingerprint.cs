using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Spyro.Editor.Core.Exporting;

public static class CrossLevelCandidateRecipeFingerprint
{
    public static string Create(MobyActorPackageImportPreview preview)
    {
        return Create(preview.RecipeId, preview.RecipeStatus, preview.CopySegments, preview.RootEntries);
    }

    public static string Create(
        string recipeId,
        string recipeStatus,
        IEnumerable<MobyActorPackageCopyPreview> copySegments,
        IEnumerable<MobyActorPackageRootPreview> rootEntries)
    {
        StringBuilder builder = new();
        builder.Append("v1|");
        builder.Append(Normalize(recipeId));
        builder.Append('|');
        builder.Append(Normalize(recipeStatus));
        foreach (MobyActorPackageCopyPreview copy in copySegments.OrderBy(copy => copy.TargetStart, StringComparer.OrdinalIgnoreCase))
        {
            builder.Append("|copy:");
            builder.Append(Normalize(copy.SourceStart));
            builder.Append('>');
            builder.Append(Normalize(copy.TargetStart));
            builder.Append('+');
            builder.Append(copy.ByteLength.ToString(CultureInfo.InvariantCulture));
            builder.Append(':');
            builder.Append(Normalize(copy.TargetSafety));
        }

        foreach (MobyActorPackageRootPreview root in rootEntries
            .OrderBy(root => root.Operation, StringComparer.OrdinalIgnoreCase)
            .ThenBy(root => root.TargetRootSlot, StringComparer.OrdinalIgnoreCase))
        {
            builder.Append("|root:");
            builder.Append(Normalize(root.Operation));
            builder.Append(':');
            builder.Append(Normalize(root.TargetRootSlot));
            builder.Append('=');
            builder.Append(Normalize(root.TargetRoot));
            builder.Append('/');
            builder.Append(Normalize(root.TargetActorId));
        }

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string Normalize(string value)
    {
        return (value ?? "").Trim().ToLowerInvariant();
    }
}
