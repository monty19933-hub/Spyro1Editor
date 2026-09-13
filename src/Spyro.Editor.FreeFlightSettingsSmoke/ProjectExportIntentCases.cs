using Spyro.Editor.Core.Persistence;

internal static class ProjectExportIntentCases
{
    internal static void Run(Action<bool, string> check, Action<Action, string> refuse)
    {
        string root = Directory.CreateTempSubdirectory("spyro-flight-export-intent-").FullName;
        try
        {
            string first = Directory.CreateDirectory(Path.Combine(root, "first")).FullName;
            string second = Directory.CreateDirectory(Path.Combine(root, "second")).FullName;
            var absent = NativeFreeFlightProjectExportIntent.Capture(first);
            check(!absent.RequiresRebuild && absent.EnabledLevelId == null && !absent.HasExplicitSelection(14), "absent flight intent leaves ordinary export unchanged");
            absent.RequireCurrent(first + Path.DirectorySeparatorChar);
            absent.RequireOrdinaryEditCompatibility(true);
            refuse(() => absent.RequireCurrent(second), "absent settings in another project do not own this export");
            check(!Directory.EnumerateFileSystemEntries(first).Any(), "intent capture/current checks never create settings");

            var saved = NativeFreeFlightSettingsStore.Save(NativeFreeFlightSettingsStore.Capture(first), 14, true);
            refuse(() => absent.RequireCurrent(first), "absent to present is stale");
            var on = NativeFreeFlightProjectExportIntent.Capture(first);
            check(on.RequiresRebuild && on.EnabledLevelId == NativeFreeFlightProjectExportIntent.SupportedEnabledLevelId && on.HasExplicitSelection(14), "actual saved Toasty intent requires enabled rebuild");
            on.RequireOrdinaryEditCompatibility(false);
            refuse(() => on.RequireOrdinaryEditCompatibility(true), "enabled flight cannot silently overwrite other edits before coexistence proof");
            byte[] exactOn = File.ReadAllBytes(saved.Path);
            NativeFreeFlightSettingsStore.Save(NativeFreeFlightSettingsStore.Capture(second), 14, true);
            refuse(() => on.RequireCurrent(second), "equal saved bytes in another project do not transfer ownership");
            File.AppendAllText(saved.Path, "\n");
            refuse(() => on.RequireCurrent(first), "even semantic equality cannot substitute stale raw settings");
            File.WriteAllBytes(saved.Path, exactOn);
            on.RequireCurrent(first);

            saved = NativeFreeFlightSettingsStore.Save(saved, 14, false);
            var off = NativeFreeFlightProjectExportIntent.Capture(first);
            check(off.RequiresRebuild && off.EnabledLevelId == null && off.HasExplicitSelection(14), "explicit OFF requires full original rebuild even without other edits");
            off.RequireOrdinaryEditCompatibility(true);
            refuse(() => on.RequireCurrent(first), "old ON cannot publish over newly saved OFF");
            saved = NativeFreeFlightSettingsStore.Save(saved, 11, false);
            var multiOff = NativeFreeFlightProjectExportIntent.Capture(first);
            check(multiOff.RequiresRebuild && multiOff.EnabledLevelId == null && multiOff.HasExplicitSelection(11) && multiOff.HasExplicitSelection(14), "multiple disabled retail selections remain scoped and explicit");
            saved = NativeFreeFlightSettingsStore.Save(saved, 14, true);
            check(NativeFreeFlightProjectExportIntent.Capture(first).EnabledLevelId == 14, "disabled other levels do not replace the one enabled target");
            saved = NativeFreeFlightSettingsStore.Save(saved, 11, true);
            refuse(() => NativeFreeFlightProjectExportIntent.Capture(first), "multiple enabled targets cannot be stacked into a single-target executable recipe");
            saved = NativeFreeFlightSettingsStore.Save(saved, 14, false);
            refuse(() => NativeFreeFlightProjectExportIntent.Capture(first), "unsupported enabled level is not silently mapped to Toasty");
            saved = NativeFreeFlightSettingsStore.Save(saved, 11, false);
            var final = NativeFreeFlightProjectExportIntent.Capture(first);
            File.Delete(saved.Path);
            refuse(() => final.RequireCurrent(first), "deleted saved choice invalidates a pending export");
            File.WriteAllText(saved.Path, "{}");
            refuse(() => NativeFreeFlightProjectExportIntent.Capture(first), "corrupt intent is not default OFF");
            check(File.ReadAllText(saved.Path) == "{}", "refusals preserve settings rather than repair them");
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
