using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;

namespace Spyro.Editor.Core.Exporting;

internal sealed record NativeGreenWizardRouteTargetAudit(int TrueIndex,
    NativeRoutePoint Origin, string OutputRowSha256, string GroundResolution,
    NativeRouteSurfaceReport? SurfaceCertificate);
internal sealed record NativeGreenWizardRouteAudit(string Profile, string AuditSha256,
    string SourceImageSha256, string ExistingV1PlanSha256, string ExistingV1OutputSceneSha256,
    string TargetLevelKey, string TargetDataSha256, string ModelSha256, long ModelWadOffset,
    string NativeExecutableSha256, IReadOnlyList<NativeGreenWizardRouteTargetAudit> Targets,
    bool AllCorridorsAccepted, bool CandidateWritten, bool RuntimeVerified, string Scope);

/// <summary>
/// Additive, read-only projection of an existing v1 plan. It never changes v1
/// compilation/publication, chooses a new route, writes a candidate, or upgrades
/// old structural evidence into placement or runtime acceptance.
/// </summary>
internal static class NativeGreenWizardRouteSurfaceAudit
{
    public const string Profile = "native-green-wizard-route-surface-audit-v1";

    public static NativeGreenWizardRouteAudit Inspect(string sourceImagePath,
        NativeGreenWizardPropertiesCandidateRequest request)
    {
        NativeGreenWizardCandidateSource source = NativeGreenWizardCandidateSource.Inspect(sourceImagePath);
        NativeGreenWizardPropertiesCandidatePlan plan = NativeGreenWizardPropertiesCandidateCompiler.Compile(source, request);
        using FileStream image = new(source.SourceImagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        DiscLayout layout = DiscImage.DetectLayout(source.SourceImagePath);
        if (layout.SectorSize != 2352 || layout.UserOffset != 24)
            throw new InvalidDataException("Route audit source sector layout changed.");
        DiscFileRecord wad = DiscImage.FindRootFileRecord(image, layout, n => n == "WAD.WAD");
        DiscFileRecord exe = DiscImage.FindRootFileRecord(image, layout, n => n == "SCUS_942.28");
        if (wad.Lba != 37 || exe.Lba != 53875 || exe.Size != 0x66000)
            throw new InvalidDataException("Route audit source ISO domains changed.");
        byte[] header = DiscImage.ReadFileBytes(image, layout, wad.Lba, plan.Target.DataEntry * 8L, 8);
        if (U32(header, 0) != plan.Target.DataWadOffset || U32(header, 4) != plan.Target.DataByteLength)
            throw new InvalidDataException("Route audit target native data binding changed.");
        byte[] data = DiscImage.ReadFileBytes(image, layout, wad.Lba, plan.Target.DataWadOffset, plan.Target.DataByteLength);
        if (Hash(data) != plan.Target.DataSha256)
            throw new InvalidDataException("Route audit target source data changed.");
        uint offset = U32(data, 8);
        uint length = U32(data, 12);
        if (offset < 0x20 || length < 32 || (ulong)offset + length > (ulong)data.Length)
            throw new InvalidDataException("Route audit native model subfile is outside its source owner.");
        NativeRouteSurfaceSnapshot terrain = NativeRouteSurfaceSnapshot.Capture(
            data.AsSpan((int)offset, (int)length).ToArray(), plan.Target.DataWadOffset + offset);
        byte[] executable = DiscImage.ReadFileBytes(image, layout, exe.Lba, 0, exe.Size);
        string exeSha = Hash(executable);
        if (exeSha != "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998")
            throw new InvalidDataException("Route audit native collision/surface consumer executable changed.");
        byte[] scene = plan.CopyOutputScene();
        List<NativeGreenWizardRouteTargetAudit> targets = [];
        foreach (var target in plan.Targets)
        {
            int row = target.PointerFieldSceneOffset;
            NativeRoutePoint origin = new(I32(scene, row + 12), I32(scene, row + 16), I32(scene, row + 20));
            int? ground = NativeRouteSurfaceCertificate.FindUniqueFlatGround(terrain, origin);
            var route = new[] { origin }
                .Concat(target.RoutePoints.Select(p => new NativeRoutePoint(p.X, p.Y, p.Z))).ToArray();
            // Authored research test dimensions, NOT dimensions inferred from
            // native renderer-radius bytes or a proven Wizard collision body.
            NativeRouteSurfaceReport? report = ground is { } z
                ? NativeRouteSurfaceCertificate.Evaluate(terrain, route, new(z, 512, 1536))
                : null;
            string resolution = ground is { } value
                ? $"Unique upward flat source datum Z={value}; origin-minus-datum={origin.Z - value}. Initial origin height is not certified."
                : "Unresolved: origin has no unique upward flat native ground datum. No corridor acceptance is available.";
            targets.Add(new(target.TrueIndex, origin, target.OutputRowSha256, resolution, report));
        }

        // Final entire-file binding catches late replacement/changes, including
        // protected bytes unrelated to the extracted terrain or instance rows.
        image.Position = 0;
        if (image.Length != NativeGreenWizardCandidateSource.OriginalImageByteLength ||
            Convert.ToHexStringLower(SHA256.HashData(image)) != source.SourceImageSha256 ||
            image.Length != NativeGreenWizardCandidateSource.OriginalImageByteLength ||
            image.Position != image.Length)
            throw new InvalidDataException("The complete route audit source changed during capture.");
        var frozen = Array.AsReadOnly(targets.ToArray());
        string sha = Hash(JsonSerializer.SerializeToUtf8Bytes(new
        {
            Profile, source.SourceImageSha256, plan.PlanSha256, plan.OutputSceneSha256,
            plan.Target.DataSha256, terrain.ModelSha256, terrain.ModelWadOffset,
            exeSha, targets = frozen, NativeRouteSurfaceCertificate.Scope
        }));
        return new(Profile, sha, source.SourceImageSha256, plan.PlanSha256,
            plan.OutputSceneSha256, plan.Target.LevelKey, plan.Target.DataSha256,
            terrain.ModelSha256, terrain.ModelWadOffset, exeSha, frozen,
            frozen.All(t => t.SurfaceCertificate?.Accepted == true), false, false,
            NativeRouteSurfaceCertificate.Scope);
    }

    private static uint U32(byte[] b, int o) => BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(o));
    private static int I32(byte[] b, int o) => BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(o));
    private static string Hash(ReadOnlySpan<byte> b) => NativeRouteSurfaceSnapshot.Hash(b);
}
