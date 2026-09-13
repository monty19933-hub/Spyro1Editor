using System.Text.Json;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using static Cases;

internal static class MixedSavedCases
{
    internal const string Expected = "98b4c9d2ee5cef75f733367b3910f01a29f44fceb4b57d6b1056de23d6ea4e46";

    internal static NativeFreshLevelDocumentIntent Document(int music = 19, int secondEntry = 70)
    {
        var floor = NativeId65FreshDocumentIntent.Create(8448, 8448, 8704, 2048, 2048,
            8960, 8960, 64, new(80, 128, 112), new(128, 96, 64), music);
        return NativeFreshLevelDocumentIntent.Create(floor, [NativeFreshWizardRouteIntent.Create(
            "11111111111111111111111111111111", 9984, 9984, 8704, 9472, 9984)])
            .WithFloorMaterials(NativeFreshFloorMaterialsIntent.Create(Retail(14, 1), Retail(secondEntry, 1)));
    }

    internal static async Task Free()
    {
        var (_, saved) = Save("mixed-source-preflight", Document());
        var error = await Throws<IOException>(() =>
            NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync(Missing, Missing, saved.Snapshot));
        Check(Leaves(error)[0].Message.StartsWith("Textured research source path is missing", StringComparison.Ordinal),
            "Supported saved mixed document reaches original-source authentication through the existing public factory.");
        var invalid = Document(20);
        var (_, bad) = Save("mixed-unsupported-shape", invalid);
        var refused = await Throws<InvalidDataException>(() =>
            NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync(Missing, Missing, bad.Snapshot));
        Check(refused.Message.Contains("separatedB", StringComparison.Ordinal),
            "Unsupported mixed geometry refuses before original source I/O; no implicit recipe substitution.");
    }

    internal static async Task Source(string bin, string cue)
    {
        var (project, saved) = Save("mixed-saved-publication", Document());
        var plan = await NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync(bin, cue, saved.Snapshot);
        plan.RequireMixedAncestry(plan.Saved, plan.MixedDisc!, plan.MixedClearance!);
        var binding = NativeId65FreshResearchPublicationBinding.FromSavedLevel(plan);
        Check(ReferenceEquals(binding.SavedLevelPlan, plan) && binding.WizardPlan is null,
            "Mixed publication retains the aggregate project guard and committed-failure classification.");
        await Throws<InvalidDataException>(() => { _ = plan.MaterialCompilation; return Task.CompletedTask; });
        await Throws<InvalidDataException>(() => {
            plan.RequireMixedAncestry(saved, plan.MixedDisc!, plan.MixedClearance!); return Task.CompletedTask;
        });
        Check(ReferenceEquals(plan.SavedSnapshot, saved.Snapshot) && plan.ExpectedImageSha256 == Expected &&
            plan.ExpectedChangedRawSectors == 1290 && !plan.RuntimeVerified,
            "Actual saved aggregate compiles to the independent mixed full-image reference without runtime claims.");
        byte[] canonical = plan.CopyCanonicalDocument();
        Check(canonical.SequenceEqual(NativeFreshLevelDocumentCodec.SerializeCanonical(saved.Document)),
            "All materials, stable placement ID, route and Base fields survive the saved export pipeline.");
        var verification = new NativeId65FreshDiscVerification(Expected, 1290);
        byte[] receipt = plan.BuildReceipt(verification);
        using (var parsed = JsonDocument.Parse(receipt))
        {
            var root = parsed.RootElement;
            Check(root.GetProperty("savedRevisionBound").GetBoolean() &&
                root.GetProperty("variant").GetString() == "schema2-two-retail-materials-wizard-p1-n32" &&
                root.GetProperty("saved").GetProperty("rawSha256").GetString() == saved.Snapshot.ContentSha256 &&
                root.GetProperty("saved").GetProperty("manifestSha256").GetString() == saved.Snapshot.ManifestSha256 &&
                root.GetProperty("saved").GetProperty("canonicalSha256").GetString() == Hash(canonical) &&
                !root.GetProperty("runtimeVerified").GetBoolean(), "Receipt binds the actual raw and canonical saved revision.");
        }
        var reopened = NativeFreshLevelDocumentStore.Capture(project);
        var repeated = await NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync(bin, cue, reopened);
        Check(!ReferenceEquals(reopened, saved.Snapshot) && repeated.PlanSha256 == plan.PlanSha256 &&
            repeated.BuildReceipt(verification).SequenceEqual(receipt), "Reopen and re-export are deterministic without ancestor substitution.");
        await Throws<InvalidDataException>(() => {
            plan.RequireMixedAncestry(plan.Saved, repeated.MixedDisc!, plan.MixedClearance!); return Task.CompletedTask;
        });
        await Throws<InvalidDataException>(() => {
            plan.RequireMixedAncestry(plan.Saved, plan.MixedDisc!, repeated.MixedClearance!); return Task.CompletedTask;
        });
        receipt[0] ^= 1; canonical[0] ^= 1;
        Check(plan.BuildReceipt(verification)[0] != receipt[0] && plan.CopyCanonicalDocument()[0] != canonical[0],
            "Mixed receipt and aggregate bytes are defensive copies.");
        await Throws<InvalidDataException>(() => { plan.BuildReceipt(new(Expected, 1253)); return Task.CompletedTask; });
        File.AppendAllText(saved.Snapshot.Path, " \n");
        await Throws<InvalidDataException>(() => plan.RequireFreshAsync());
        var newSnapshot = NativeFreshLevelDocumentStore.Capture(project);
        var newRevision = await NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync(bin, cue, newSnapshot);
        Check(newRevision.PlanSha256 != plan.PlanSha256 && newRevision.ExpectedImageSha256 == Expected &&
            newRevision.CopyCanonicalDocument().SequenceEqual(plan.CopyCanonicalDocument()) &&
            !newRevision.BuildReceipt(verification).SequenceEqual(plan.BuildReceipt(verification)),
            "Raw-only revision changes its saved-bound plan and receipt, not the authored content or native image.");
        var (_, same) = Save("mixed-same-material", Document(secondEntry: 14));
        var samePlan = await NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync(bin, cue, same.Snapshot);
        var materialSources = samePlan.MixedDisc!.ExecutableAncestor.MixedResourceAncestor!.MaterialSources;
        Check(samePlan.ExpectedImageSha256 == "02655e30f9ee5d74166ff8b8b5f5bf0aadc6a0d86c47cc692b4e4f35df97277a" &&
            ReferenceEquals(materialSources[0], materialSources[1]) &&
            samePlan.CopyCanonicalDocument().SequenceEqual(NativeFreshLevelDocumentCodec.SerializeCanonical(same.Document)),
            "Equal material slots retain both authored bindings while reusing the exact captured source.");
        Console.WriteLine($"MIXED_SAVED_SOURCE_OK checks={Checks} image={Expected} nativeImageWritten=false root={project.RootPath}");
    }
}
