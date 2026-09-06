using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;

internal static class NativeGreenWizardRouteSurfaceCases
{
    public static void Run(string path)
    {
        int checks = 0;
        string before = FileHash();
        var audit = NativeGreenWizardRouteSurfaceAudit.Inspect(path, new("wizardpeak", 6, "magiccrafters", [27]));
        var report = audit.Targets.Single().SurfaceCertificate ?? throw new Exception("Missing source-bound T27 report.");
        Require(!audit.AllCorridorsAccepted && !audit.CandidateWritten && !audit.RuntimeVerified && !report.Accepted, "read-only hazard rejection");
        Require(audit.ExistingV1OutputSceneSha256 == "68f283f14267dabdb22432cadcdd7108f9b0f350d75fba910ec1c5658bde7d19" && audit.ExistingV1PlanSha256 == "fb9e28b4a4cbafa652b3df0104b56cc1e3f0b8066ce7bffb103cb19f702d4083", "v1 plan and scene exact");
        Require(audit.ModelSha256 == "6163a158a20940be6298b3169e6cb6fa4c56a5f3010fe33d00e28507cde89696" && audit.ModelWadOffset == 0x2822800 && report.CollisionSha256 == "b6b1a033a9ecad87ff72285402c09bb240e73ef411d8d59d8f6f5d880e45ba7e", "independent model/collision bindings");
        foreach (int t in new[]
        {
            334,
            350,
            382,
            383
        }

        )
            Require(report.Issues.Any(i => i.Code == "SpecialSurface" && i.Triangle == t && i.Descriptor is { Index: 1, Type: 0, Param1: 0, Param2: 0, WadOffset: 0x285A440, RawHex: "000000000000000000000000" }), "known pond triangle " + t);
        Require(report.Policy.GroundZ == 23552 && report.Route[0] == new NativeRoutePoint(88832, 41851, 23450) && report.Route[^1] == new NativeRoutePoint(82309, 38707, 23737), "actual origin and translated endpoint");
        Require(report.SupportBindings.Any(b => b.Triangle == 3220 && b.TriangleWadOffset == 0x2871B00 && b.Flag == 0xFF && b.FlagWadOffset == -1), "implicit ordinary native bank binding");
        using FileStream file = File.OpenRead(path);
        var layout = DiscImage.DetectLayout(path);
        byte[] model = DiscImage.ReadFileBytes(file, layout, 37, 0x2822800, 604160);
        var snapshot = NativeRouteSurfaceSnapshot.Capture(model, 0x2822800);
        var lifted = report.Route.Select((p, i) => i == 0 ? p with { Z = p.Z + 307 } : p).ToArray();
        var liftedReport = NativeRouteSurfaceCertificate.Evaluate(snapshot, lifted, report.Policy);
        Require(!liftedReport.Accepted && liftedReport.Issues.SequenceEqual(report.Issues), "r02 initial Z lift cannot erase water or change ground analysis");
        Require(liftedReport.CertificateSha256 != report.CertificateSha256, "lift remains bound as distinct authored intent");
        Require(NativeRouteSurfaceCertificate.Evaluate(snapshot, [report.Route[0]], report.Policy).Accepted, "source initial dry patch alone falsely suggests route safety");
        var repeat = NativeGreenWizardRouteSurfaceAudit.Inspect(path, new("wizardpeak", 6, "magiccrafters", [27]));
        Require(audit.AuditSha256 == repeat.AuditSha256, "deterministic recapture");
        // Physical writes remain outside this audit: rerun the established
        // source scene/ownership goldens without changing their v1 expectations.
        NativeGreenWizardPropertiesCandidateCases.Run(path);
        Require(FileHash() == before && before == NativeGreenWizardCandidateSource.OriginalImageSha256, "original unchanged");
        Console.WriteLine($"NATIVE_GREEN_WIZARD_ROUTE_SOURCE_SMOKE_OK checks={checks} knownWaterTriangles=4 initialPatchPass=true fullRoutePass=false originLiftPass=false originalUnchanged=true v1Goldens=true noImageWritten=true runtime=false");
        string FileHash()
        {
            using var s = File.OpenRead(path);
            return Convert.ToHexStringLower(SHA256.HashData(s));
        }

        void Require(bool value, string message)
        {
            if (!value)
                throw new Exception("Native route source regression: " + message);
            checks++;
        }
    }
}
