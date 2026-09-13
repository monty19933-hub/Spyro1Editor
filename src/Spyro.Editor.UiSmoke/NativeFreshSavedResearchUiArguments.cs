// Private test dispatch only. Parse validates shape without touching paths.
internal static class NativeFreshSavedResearchUiArguments
{
    private const string Free = "--fresh-saved-research-ui";
    private const string Publish = "--fresh-saved-research-publish-reviewed";
    private const string FamilyPublish = "--fresh-saved-research-family-publish-reviewed";
    internal static bool Handles(string[] args) => args.Any(a => a.StartsWith("--fresh-saved-research", StringComparison.Ordinal));
    internal static bool RequirePublication(string[] args)
    {
        if (args.Length == 1 && args[0] == Free) return false;
        if (args.Length == 5 && (args[0] is Publish or FamilyPublish) && args.Skip(1).All(a =>
            !string.IsNullOrWhiteSpace(a) && !a.Contains('\0') && !a.StartsWith('-') && Path.IsPathFullyQualified(a))) return true;
        throw new ArgumentException("Usage: UiSmoke --fresh-saved-research-ui OR --fresh-saved-research-publish-reviewed|--fresh-saved-research-family-publish-reviewed absolute-original.bin absolute-original.cue existing-absolute-private-fixture-parent absent-absolute-output");
    }
    internal static bool RequireFamilyPublication(string[] args) => RequirePublication(args) && args[0] == FamilyPublish;
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
            new[] { FamilyPublish + "=/bin", "/cue", "/parent", "/out" } })
            Refuse(bad);
        foreach (string mode in new[] { Publish, FamilyPublish })
        for (int i = 1; i < 5; i++)
        foreach (string value in new[] { "", " ", "relative", "\0", "/contains\0nul", "--unknown", Free, Publish, FamilyPublish })
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
