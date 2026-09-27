using Spyro.Editor.Core.Analysis;

namespace Spyro.Editor.App.Views;

// Shared presentation only; no compiler, model or placement authority is issued.
internal static class NativeMobyCataloguePresentation
{
    internal static string RelatedNames(IReadOnlyList<string> names, NativeMobyCatalogueEntryKind kind) => names.Count == 0 ? "" :
        $"Placed names associated with this class {(kind == NativeMobyCatalogueEntryKind.SharedRegistration ? "across source levels" : "in this source level")}: " +
        string.Join("; ", names) + ". This association does not prove equivalent models or behavior.";

    internal static string Dependencies(NativeMobyCatalogueDependencyStatus? status)
    {
        if (status == null) return "Source-bound dependency evidence was not captured for this listing.";
        string scoped = status.ExactSourceCapabilities.Count == 0
            ? "No scoped compiler evidence is indexed for this exact source variant."
            : "Scoped evidence for this exact source variant:\n\n" + string.Join("\n\n", status.ExactSourceCapabilities.Select(capability =>
                $"{capability.DestinationScope}\n{string.Join(" ", capability.ValidatedCapabilities)}\nRemaining limits: {string.Join(" ", capability.RemainingGates)}"));
        return scoped + "\n\nRequirements for arbitrary cross-level placement remain unresolved. This means more evidence is needed, not that transfer is impossible.\n\n" +
            string.Join("\n\n", status.UnresolvedDependencies.Select(requirement => $"{Title(requirement.Id)}: {requirement.RequiredEvidence}")) +
            "\n\nIndexed evidence does not enable placement; existing verified templates and compiler recipes retain their own gates.";
    }

    private static string Title(string id) => id switch
    {
        "model-package-and-relocation" => "Models and animation",
        "texture-clut-vram" => "Textures and palettes",
        "behavior-overlay-and-globals" => "Behavior and shared variables",
        "properties-pointers-and-instance-ownership" => "Instance properties and pointers",
        "routes-links-and-collision" => "Routes, links and collision",
        "sound-and-spu" => "Sound",
        "children-effects-and-rewards" => "Spawned objects, effects and rewards",
        "player-and-shared-resources" => "Player and shared resources",
        "persistent-state-and-reentry" => "Saving, death and re-entry",
        "ram-vram-spu-and-dynamic-capacity" => "Runtime memory capacity",
        "physical-output-and-runtime" => "Disc output and gameplay validation",
        _ => id
    };
}
