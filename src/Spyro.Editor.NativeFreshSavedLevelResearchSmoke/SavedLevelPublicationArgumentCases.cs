using Spyro.Editor.Core.Persistence;
using static Cases;
using P = SavedLevelPublicationCases;

// PRIVATE tests-first, tiny owned paths only. No source or physical mode invoked.
internal static class SavedLevelPublicationArgumentCases
{
    internal static async Task Free()
    {
        string[] valid = [P.Modes[0], "original.bin", "original.cue", "/private/tmp/test-project", "test-project", "/private/tmp/test-output"];
        foreach (string mode in P.Modes)
        {
            var args = valid.ToArray(); args[0] = mode;
            Check(P.Parse(args).Mode == mode && P.IsMode(args), "Exact six-token supported mode.");
        }
        foreach (var args in new[] { Array.Empty<string>(), valid[..5], valid.Concat(new[] { "extra" }).ToArray(),
            new[] { "--unknown", "b", "c", "/p", "id", "/o" }, new[] { "--source", "b", "c" } })
            await Throws<ArgumentException>(() => { P.Parse(args); return Task.CompletedTask; });
        foreach (int slot in Enumerable.Range(1, 5))
            foreach (string invalid in new[] { "", " ", "\0", "prefix\0suffix", "--source", P.Modes[1] })
            {
                var args = valid.ToArray(); args[slot] = invalid;
                await Throws<ArgumentException>(() => { P.Parse(args); return Task.CompletedTask; });
            }
        foreach (int slot in new[] { 3, 5 })
        { var args = valid.ToArray(); args[slot] = "relative"; await Throws<ArgumentException>(() => { P.Parse(args); return Task.CompletedTask; }); }
        foreach (string id in new[] { "UPPER", "bad space", "../other", new string('a', 65) })
        { var args = valid.ToArray(); args[4] = id; await Throws<ArgumentException>(() => { P.Parse(args); return Task.CompletedTask; }); }

        // Resolve only actually observed temp-ancestor links; do not weaken CLI.
        string root = Path.GetFullPath(Root);
        for (var parent = Directory.GetParent(root); parent != null; parent = parent.Parent)
            if (parent.LinkTarget != null)
                root = Path.Combine(parent.ResolveLinkTarget(true)!.FullName, Path.GetRelativePath(parent.FullName, root));
        string project = Path.Combine(root, "physical-argument-project"), inputs = Path.Combine(root, "physical-argument-inputs");
        Directory.CreateDirectory(project); Directory.CreateDirectory(inputs);
        string bin = Path.Combine(inputs, "tiny.bin"), cue = Path.Combine(inputs, "tiny.cue"), output = Path.Combine(root, "absent-physical-args-output");
        File.WriteAllBytes(bin, [1]); File.WriteAllBytes(cue, [2]);
        Check(P.RequireLocation(output, bin, cue, project, false) == output && !Directory.Exists(output), "Preflight is non-writing.");
        foreach (string blocked in new[] { project, Path.Combine(project, "inside"), inputs, bin, cue })
            await Throws<InvalidOperationException>(() => { P.RequireLocation(blocked, bin, cue, project, false); return Task.CompletedTask; });
        await Throws<InvalidDataException>(() => { P.RequireLocation(Path.Combine(root, "missing-parent", "out"), bin, cue, project, false); return Task.CompletedTask; });
        string occupied = Path.Combine(root, "occupied-physical-args-output"); Directory.CreateDirectory(occupied);
        await Throws<InvalidDataException>(() => { P.RequireLocation(occupied, bin, cue, project, false); return Task.CompletedTask; });
        string dangling = Path.Combine(root, "dangling-physical-args-output");
        File.CreateSymbolicLink(dangling, Path.Combine(root, "missing-physical-target"));
        await Throws<InvalidDataException>(() => { P.RequireLocation(dangling, bin, cue, project, false); return Task.CompletedTask; });
        await Throws<InvalidDataException>(() => { P.RequireLocation(output, bin, cue, project, true); return Task.CompletedTask; });
        // Exercise THIS CLI's acquired-owner failure path, not just the shared
        // SourceOwners component. Invalid tiny CUE refuses before any saved
        // capture/native Candidate. Final checks must still inspect both names
        // and held descriptors, then close every acquired owner.
        var early = await Throws<IOException>(() => P.RunAsync(
            [P.Modes[0], bin, cue, project, "tiny-project", output]));
        var errors = Leaves(early);
        Check(errors.Count == 5 && errors.All(e => e is InvalidDataException) &&
            errors[0].Message == errors[3].Message && errors[0].Message == errors[4].Message &&
            errors[1].Message == errors[2].Message && errors[0].Message != errors[1].Message,
            "Actual CUE primary, held/current BIN failures, then held/current CUE failures, once each.");
        foreach (var (path, expected) in new[] { (bin, (byte)1), (cue, (byte)2) })
        {
            using var reopened = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
            Check(reopened.Length == 1 && reopened.ReadByte() == expected && reopened.ReadByte() == -1 &&
                reopened.Position == 1 && reopened.Length == 1, "Every tiny held owner closed and exact original fixture bytes unchanged.");
        }
        Check(!Directory.EnumerateFileSystemEntries(project).Any(), "Early refusal never captured/saved a project.");
        Check(!Directory.Exists(output), "Argument-only controls never create an output.");
        Console.WriteLine("SAVED_LEVEL_PHYSICAL_ARGUMENTS_OK originalOpened=false tinyInputsOpened=true candidateCaptureReached=false nativeOutput=false");
    }
}
