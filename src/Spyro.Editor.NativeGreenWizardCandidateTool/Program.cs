using System.Globalization;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

try
{
    if (args.Length == 4 && args[0] == "--audit-route")
    {
        NativeGreenWizardRouteAudit audit = NativeGreenWizardRouteSurfaceAudit.Inspect(args[1], Request(args[2], args[3]));
        Console.WriteLine(JsonSerializer.Serialize(audit, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"NATIVE_GREEN_WIZARD_ROUTE_AUDIT_DONE accepted={audit.AllCorridorsAccepted} noImageWritten=true physicsFootprint=false runtime=false");
        return audit.AllCorridorsAccepted ? 0 : 3;
    }
    else if (args.Length == 4 && args[0] == "--plan")
    {
        NativeGreenWizardCandidateDiscPlan disc = NativeGreenWizardPropertiesCandidatePublisher.Compile(args[1], Request(args[2], args[3]));
        NativeGreenWizardPropertiesCandidatePlan plan = disc.Candidate;
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            profile = NativeGreenWizardPropertiesCandidatePlan.ProfileId, plan.PlanSha256,
            plan.SourceImageSha256, plan.Donor, plan.DonorTrueIndex, plan.DonorRowSha256, plan.DonorPayloadSha256,
            plan.Target, plan.Targets, plan.ResidentDependencies, plan.OutputSceneSha256, disc.AffectedRawSectors,
            plan.CandidateOnly, plan.RuntimeVerified, plan.NormalCreateBinReady, plan.RuntimeRequirements
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("NATIVE_GREEN_WIZARD_CANDIDATE_PLANNED noImageWritten=true runtime=false");
    }
    else if (args.Length == 6 && args[0] == "--publish")
    {
        NativeGreenWizardCandidatePublication result = NativeGreenWizardPropertiesCandidatePublisher.Publish(
            args[1], args[2], args[3], Request(args[4], args[5]));
        Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("NATIVE_GREEN_WIZARD_CANDIDATE_PUBLISHED runtime=false normalCreateBin=false");
    }
    else if (args.Length == 5 && args[0] == "--verify")
    {
        NativeGreenWizardCandidateDiscPlan plan = NativeGreenWizardPropertiesCandidatePublisher.Compile(args[1], Request(args[3], args[4]));
        using FileStream source = File.OpenRead(args[1]), candidate = File.OpenRead(args[2]);
        Console.WriteLine(JsonSerializer.Serialize(plan.Verify(source, candidate), new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("NATIVE_GREEN_WIZARD_CANDIDATE_VERIFIED imageOnly=true companionsVerified=false runtime=false");
    }
    else
    {
        Console.Error.WriteLine("Usage:\n  --audit-route <original.bin> <wizardpeak|magiccrafters> <target-indices>\n" +
            "  --plan <original.bin> <wizardpeak|magiccrafters> <target-indices>\n" +
            "  --publish <original.bin> <original.cue> <new-output-root> <wizardpeak|magiccrafters> <target-indices>\n" +
            "  --verify <original.bin> <candidate.bin> <wizardpeak|magiccrafters> <target-indices>\n" +
            "Donor is Wizard Peak T6. Targets are e.g. 17,24 or Magic Crafters 27. Every result remains an unproven candidate.");
        return 2;
    }
    return 0;
}
catch (Exception error) when (error is InvalidDataException or IOException or InvalidOperationException or ArgumentException or FormatException or OverflowException)
{
    Console.Error.WriteLine(error.Message);
    return 1;
}

static NativeGreenWizardPropertiesCandidateRequest Request(string level, string indices)
{
    string[] values = indices.Split(',', StringSplitOptions.TrimEntries);
    if (values.Length == 0 || values.Any(value => !int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out _)))
        throw new InvalidDataException("Target indices must be a comma-separated nonempty list of nonnegative decimal integers.");
    return new("wizardpeak", 6, level, values.Select(value => int.Parse(value, CultureInfo.InvariantCulture)).ToArray());
}
