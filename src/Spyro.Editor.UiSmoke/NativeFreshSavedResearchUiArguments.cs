// Private test dispatch only. Parse validates shape without touching paths.
internal static class NativeFreshSavedResearchUiArguments
{
    private const string Free = "--fresh-saved-research-ui";
    private const string Publish = "--fresh-saved-research-publish-reviewed";
    private const string FamilyPublish = "--fresh-saved-research-family-publish-reviewed";
    private const string LockedChestPublish = "--fresh-saved-research-locked-chest-publish-reviewed";
    private const string TiledFamilyPublish = "--fresh-saved-research-tiled-family-publish-reviewed";
    internal static bool Handles(string[] args) => args.Any(a => a.StartsWith("--fresh-saved-research", StringComparison.Ordinal));
    internal static bool RequirePublication(string[] args)
    {
        if (args.Length == 1 && args[0] == Free) return false;
        if (args.Length == 5 && (args[0] is Publish or FamilyPublish or LockedChestPublish or TiledFamilyPublish) && args.Skip(1).All(a =>
            !string.IsNullOrWhiteSpace(a) && !a.Contains('\0') && !a.StartsWith('-') && Path.IsPathFullyQualified(a))) return true;
        throw new ArgumentException("Usage: UiSmoke --fresh-saved-research-ui OR --fresh-saved-research-publish-reviewed|--fresh-saved-research-family-publish-reviewed|--fresh-saved-research-locked-chest-publish-reviewed|--fresh-saved-research-tiled-family-publish-reviewed absolute-original.bin absolute-original.cue existing-absolute-private-fixture-parent absent-absolute-output");
    }
    internal static bool RequireFamilyPublication(string[] args) => RequirePublication(args) && args[0] == FamilyPublish;
    internal static bool RequireLockedChestPublication(string[] args) => RequirePublication(args) && args[0] == LockedChestPublish;
    internal static bool RequireTiledFamilyPublication(string[] args) => RequirePublication(args) && args[0] == TiledFamilyPublish;
    internal static void Run()
    {
        int checks = 0;
        Check(!RequirePublication([Free]) && !RequireFamilyPublication([Free]) &&
            RequirePublication([Publish, "/not-opened.bin", "/not-opened.cue", "/not-inspected-parent", "/not-created-output"]) &&
            !RequireFamilyPublication([Publish, "/not-opened.bin", "/not-opened.cue", "/not-inspected-parent", "/not-created-output"]) &&
            RequireFamilyPublication([FamilyPublish, "/not-opened.bin", "/not-opened.cue", "/not-inspected-parent", "/not-created-output"]),
            "Exactly one free or five explicit publication tokens; the old and family arms stay distinct.");
        Check(Handles([Free]) && Handles([Publish]) && Handles([FamilyPublish]) && Handles(["--fresh-saved-research-unknown"]) &&
            !Handles(["--source"]) && !Handles(["--fresh-native-document-only"]) && !Handles(["--fresh-material-inspection-only"]), "New prefix never aliases old or source-only modes.");
        string[] chest = [LockedChestPublish, "/not-opened.bin", "/not-opened.cue", "/not-inspected-parent", "/not-created-output"];
        Check(Handles(chest) && RequirePublication(chest) && RequireLockedChestPublication(chest) && !RequireFamilyPublication(chest) &&
            !RequireLockedChestPublication([Free]) &&
            !RequireLockedChestPublication([Publish, "/bin", "/cue", "/parent", "/out"]) &&
            !RequireLockedChestPublication([FamilyPublish, "/bin", "/cue", "/parent", "/out"]),
            "Only the explicit five-token locked-chest route selects schema5; old material/schema3/free routes cannot alias it.");
        string[] tiled = [TiledFamilyPublish, "/not-opened.bin", "/not-opened.cue", "/not-inspected-parent", "/not-created-output"];
        Check(Handles(tiled) && RequirePublication(tiled) && RequireTiledFamilyPublication(tiled) &&
            !RequireFamilyPublication(tiled) && !RequireLockedChestPublication(tiled) && !RequireTiledFamilyPublication([Free]) &&
            !RequireTiledFamilyPublication([Publish, "/bin", "/cue", "/parent", "/out"]) &&
            !RequireTiledFamilyPublication([FamilyPublish, "/bin", "/cue", "/parent", "/out"]) &&
            !RequireTiledFamilyPublication(chest),
            "Only the explicit five-token tiled-family arm selects the full four-tile schema5 publication; no old route aliases it.");
        foreach (var bad in new[] { Array.Empty<string>(), new[] { Free, "extra" }, new[] { Free, Free },
            new[] { Publish }, new[] { Publish, "/bin", "/cue", "/parent" }, new[] { Publish, "/bin", "/cue", "/parent", "/out", "extra" },
            new[] { Publish, Publish, "/cue", "/parent", "/out" }, new[] { Publish, "/bin", "/cue", "/parent", Free },
            new[] { Free, Publish, "/bin", "/cue", "/parent", "/out" }, new[] { "--fresh-saved-research-unknown" },
            new[] { Publish + "=/bin", "/cue", "/parent", "/out" }, new[] { Free, "--fresh-material-inspection-only" },
            new[] { FamilyPublish }, new[] { FamilyPublish, "/bin", "/cue", "/parent" },
            new[] { FamilyPublish, "/bin", "/cue", "/parent", "/out", "extra" },
            new[] { FamilyPublish, FamilyPublish, "/cue", "/parent", "/out" },
            new[] { FamilyPublish, Publish, "/cue", "/parent", "/out" },
            new[] { FamilyPublish, "/bin", "/cue", "/parent", Free },
            new[] { Free, FamilyPublish, "/bin", "/cue", "/parent", "/out" },
            new[] { Publish, FamilyPublish, "/bin", "/cue", "/parent", "/out" },
            new[] { FamilyPublish + "=/bin", "/cue", "/parent", "/out" },
            new[] { LockedChestPublish }, new[] { LockedChestPublish, "/bin", "/cue", "/parent" },
            new[] { LockedChestPublish, "/bin", "/cue", "/parent", "/out", "extra" },
            new[] { LockedChestPublish, FamilyPublish, "/cue", "/parent", "/out" },
            new[] { Free, LockedChestPublish, "/bin", "/cue", "/parent", "/out" },
            new[] { LockedChestPublish + "=/bin", "/cue", "/parent", "/out" },
            new[] { LockedChestPublish.ToUpperInvariant(), "/bin", "/cue", "/parent", "/out" },
            new[] { LockedChestPublish + "-extra", "/bin", "/cue", "/parent", "/out" },
            new[] { TiledFamilyPublish }, new[] { TiledFamilyPublish, "/bin", "/cue", "/parent" },
            new[] { TiledFamilyPublish, "/bin", "/cue", "/parent", "/out", "extra" },
            new[] { TiledFamilyPublish, TiledFamilyPublish, "/cue", "/parent", "/out" },
            new[] { TiledFamilyPublish, LockedChestPublish, "/cue", "/parent", "/out" },
            new[] { Free, TiledFamilyPublish, "/bin", "/cue", "/parent", "/out" },
            new[] { TiledFamilyPublish + "=/bin", "/cue", "/parent", "/out" },
            new[] { TiledFamilyPublish.ToUpperInvariant(), "/bin", "/cue", "/parent", "/out" },
            new[] { TiledFamilyPublish + "-extra", "/bin", "/cue", "/parent", "/out" } })
            Refuse(bad);
        foreach (string mode in new[] { Publish, FamilyPublish, LockedChestPublish, TiledFamilyPublish })
        for (int i = 1; i < 5; i++)
        foreach (string value in new[] { "", " ", "relative", "\0", "/contains\0nul", "--unknown", Free, Publish, FamilyPublish, LockedChestPublish, TiledFamilyPublish })
        {
            string[] bad = [mode, "/bin", "/cue", "/parent", "/out"]; bad[i] = value; Refuse(bad);
        }
        Console.WriteLine($"FRESH_SAVED_RESEARCH_UI_ARGUMENTS_OK checks={checks} sourceOpened=false nativeOutputWritten=false");
        void Refuse(string[] bad)
        {
            try { RequirePublication(bad); } catch (ArgumentException) { checks++; return; }
            throw new InvalidDataException("Malformed saved-research UI command accepted.");
        }
        void Check(bool condition, string message) { if (!condition) throw new InvalidDataException(message); checks++; }
    }
}
