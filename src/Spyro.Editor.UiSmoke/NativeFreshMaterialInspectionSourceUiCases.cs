using System.Runtime.InteropServices;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Editing;
using F = NativeFreshMaterialInspectionUiFixture;

// Private explicit ORIGINAL-only headless tests; no candidate or source writes.
internal static class NativeFreshMaterialInspectionSourceUiCases
{
    private const string Original = "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37";
    private const string CueSha = "05142502c75d3a0b6374003a8fe41893d6faf0ad55863ff0b62a265a1b152766";
    private static int checks;

    internal static void Run(string bin, string cue)
    {
        checks = 0;
        string root = Directory.CreateTempSubdirectory("spyro-material-inspection-source-ui-").FullName;
        List<F> fixtures = []; List<Exception> failures = [];
        FileStream? held = null, heldCue = null;
        try
        {
            held = File.OpenRead(bin); Authenticate(held, 661547040, Original);
            heldCue = File.OpenRead(cue); Authenticate(heldCue, 88, CueSha);
            NativeFreshFloorMaterialsIntent[] pairs =
            [
                F.Pair(), F.Pair(70, 1, 14, 0), F.Pair(14, 0, 14, 0),
                NativeFreshFloorMaterialsIntent.Create(NativeFreshFloorMaterialIntent.BaseFloorColor(),
                    NativeFreshFloorMaterialIntent.RetailTerrain(F.Variant, 70, 1))
            ];
            for (int index = 0; index < pairs.Length; index++)
                Success(Add("success-" + index, F.Document().WithFloorMaterials(pairs[index])), root, index == 0);
            var foreign = Add("foreign-actual-request");
            var foreignWork = new ForeignActualMaterialInspectionWork();
            foreign.Window.MaterialInspectionWorkForTesting = foreignWork;
            F.Drain(foreign.Start());
            Outcome(foreign, foreignWork.Actual != null && foreignWork.Requested != null && foreignWork.Reissued != null &&
                !ReferenceEquals(foreignWork.Requested, foreignWork.Reissued) &&
                ReferenceEquals(foreignWork.Requested.Document, foreignWork.Reissued.Document) &&
                ReferenceEquals(foreignWork.Requested.Materials, foreignWork.Reissued.Materials) &&
                foreignWork.Requested.OriginalImagePath == foreignWork.Reissued.OriginalImagePath &&
                ReferenceEquals(foreignWork.Actual.Request, foreignWork.Reissued) &&
                foreign.Window.MaterialInspectionFailureForTesting is InvalidDataException requestError &&
                requestError.Message == "Inspection belongs to its exact request, not interchangeable equal fields or hashes." &&
                foreign.NoImages && foreign.Window.MaterialInspectionOwnedBitmapsForTesting.All(bitmap => bitmap == null),
                "genuine equal-field foreign request result rejected without byte authority or partial UI installation");
            const string runtimeRefusal = "Selected native record has unclosed animation/scroll/source-frame semantics or stale scene evidence.";
            const string layoutRefusal = "Native texture depth/shape/page/orientation is outside the canonical static subset.";
            foreach (var bad in new[] { (16, 1, runtimeRefusal), (40, 70, runtimeRefusal), (70, 12, layoutRefusal) })
            {
                var f = Add($"refusal-{bad.Item1}-{bad.Item2}", F.Document().WithFloorMaterials(F.Pair(14, 0, bad.Item1, bad.Item2)));
                F.Drain(f.Start());
                Outcome(f, f.Window.MaterialInspectionFailureForTesting is InvalidDataException error && error.Message == bad.Item3 &&
                    f.NoImages && f.Window.MaterialInspectionOwnedBitmapsForTesting.All(bitmap => bitmap == null),
                    "exact real second-source combined refusal installs no first-slot result or fallback");
            }
            foreach (string stale in new[] { "raw", "aba", "base", "route", "project", "image", "cue", "cancel", "close" })
                Stale(Add("stale-" + stale), stale);
            var releaseChanged = Add("release-source-change");
            releaseChanged.OnRelease = () => releaseChanged.SelectedSource = (bin, cue + ".changed-selection-only");
            F.Drain(releaseChanged.Start());
            Outcome(releaseChanged, releaseChanged.Window.MaterialInspectionFailureForTesting is InvalidDataException &&
                releaseChanged.NoImages, "actual owner-release callback change is checked before bitmap installation");
            BitmapFailure(Add("bitmap-failure"));
            CleanupNotificationFailure(Add("cleanup-notification"));
        }
        catch (Exception error) { failures.Add(error); }
        finally
        {
            foreach (var f in fixtures) Attempt(f.Close);
            foreach (var f in fixtures) Attempt(f.RequireProtected);
            Attempt(() => { if (held != null) Authenticate(held, 661547040, Original); });
            Attempt(() => AuthenticatePath(bin, 661547040, Original));
            Attempt(() => { if (heldCue != null) Authenticate(heldCue, 88, CueSha); });
            Attempt(() => AuthenticatePath(cue, 88, CueSha));
            Attempt(() => held?.Dispose()); Attempt(() => heldCue?.Dispose());
            Console.WriteLine("MATERIAL_INSPECTION_SOURCE_UI_RETAINED path=" + root);
        }
        F.Throw(failures);
        Console.WriteLine($"FRESH_MATERIAL_INSPECTION_SOURCE_UI_OK checks={checks} originalCueHeldNamedFinalAndClosed=true nativeWindow=false publication=false");
        F Add(string id, NativeFreshLevelDocumentIntent? document = null)
        {
            var f = new F(root, id, document: document); fixtures.Add(f);
            f.SelectedSource = (bin, cue); return f;
        }
        void Attempt(Action action) { try { action(); } catch (Exception error) { failures.Add(error); } }
    }

    private static void Success(F f, string root, bool frameAndReplacement)
    {
        var applied = f.Window.CurrentAggregateIntent!;
        F.Drain(f.Start());
        var work = f.Window.MaterialInspectionResultForTesting;
        Outcome(f, work != null && f.Window.MaterialInspectionFailureForTesting == null &&
            ReferenceEquals(work.Request.Document, applied) && f.ReleaseAttempts == 1 &&
            !f.Window.MaterialInspectionBusyForTesting, "complete actual source result associates with unchanged applied document");
        int count = 0;
        var bitmaps = new List<WriteableBitmap>();
        for (int slot = 0; slot < 2; slot++)
        {
            var preview = work!.GetPreview(slot);
            var image = f.Find<Image>($"FreshMaterial{slot}Preview");
            if (preview == null)
            {
                Check(image.Source == null && f.Find<TextBlock>($"FreshMaterial{slot}Inspection").Text ==
                    "Base floor color; no source inspection needed", "color slot has no fabricated source or bitmap");
                continue;
            }
            count++;
            var bitmap = image.Source as WriteableBitmap ?? throw new InvalidDataException("Expected actual UI bitmap owner.");
            bitmaps.Add(bitmap);
            string expected = Golden(preview.Source.SourceWadEntry, preview.Source.SourceTextureId);
            Check(Hash(ReadBitmap(bitmap)) == expected && Hash(preview.CopyRgba().SelectMany(p => new[] { p.R, p.G, p.B, p.A }).ToArray()) == expected,
                "actual installed tight RGBA and actual source wrapper match independent row0 literal");
            Check(f.Find<TextBlock>($"FreshMaterial{slot}Inspection").Text!.Contains($"WAD {preview.Source.SourceWadEntry} / texture {preview.Source.SourceTextureId}"),
                "ordered source label matches actual source, not selected source guesses");
        }
        Check(count == work!.CapturedSlotCount && count <= 2 &&
            f.Window.MaterialInspectionOwnedBitmapsForTesting.Count(bitmap => bitmap != null) == count &&
            f.InspectionStatus == "Source thumbnail only; not the projected floor or a native output approval." &&
            f.Window.Preview.MaterialPreviewNotice == "Selected materials are not rendered in this schematic.",
            "at most two real owners and truthful separate diagnostic; schematic remains unrendered");
        if (frameAndReplacement)
        {
            f.Find<Control>("FreshMaterialInspectionStatus").BringIntoView(); F.Pump();
            f.Window.InvalidateVisual(); F.Pump();
            using var frame = f.Window.CaptureRenderedFrame() ?? throw new InvalidDataException("Expected actual headless Window frame.");
            string path = Path.Combine(root, "actual-source-thumbnails-window.png"); frame.Save(path);
            Console.WriteLine("MATERIAL_INSPECTION_WINDOW_FRAME path=" + path);
            var replacementWork = new HoldingActualMaterialInspectionWork();
            f.Window.MaterialInspectionWorkForTesting = replacementWork;
            Task? replacement = null; List<Exception> failures = [];
            try
            {
                replacement = f.Start(); F.Wait(replacementWork.Ready.Task);
                Check(f.NoImages, "old images/result cleared while second genuine result delivery is held");
                foreach (var bitmap in bitmaps) RequireDisposed(bitmap);
            }
            catch (Exception error) { failures.Add(error); }
            finally
            {
                replacementWork.Released.TrySetResult();
                if (replacement != null) try { F.Drain(replacement); } catch (Exception error) { failures.Add(error); }
                if (failures.Count != 0 && f.Window.MaterialInspectionFailureForTesting is { } actualFailure &&
                    !failures.Any(e => ReferenceEquals(e, actualFailure))) failures.Add(actualFailure);
            }
            F.Throw(failures);
            Outcome(f, f.Window.MaterialInspectionResultForTesting != null && f.ReleaseAttempts == 2 &&
                f.Window.MaterialInspectionOwnedBitmapsForTesting.Count(bitmap => bitmap != null) == 2, "new real pair replaces without retaining four owners");
            bitmaps = f.Window.MaterialInspectionOwnedBitmapsForTesting.OfType<WriteableBitmap>().ToList();
            int sourceCalls = f.SourceCalls;
            f.Set("FreshMaterial0TextureId", "1");
            Check(f.NoImages && f.SourceCalls == sourceCalls, "raw edit clears source diagnostics without another source resolution");
            foreach (var bitmap in bitmaps) RequireDisposed(bitmap);
        }
        else
        {
            f.Window.Close(); F.Pump();
            Check(!f.Window.IsVisible && f.NoImages, "clean close clears both actual source diagnostics");
            foreach (var bitmap in bitmaps) RequireDisposed(bitmap);
        }
    }

    private static void Stale(F f, string kind)
    {
        var actual = new HoldingActualMaterialInspectionWork();
        f.Window.MaterialInspectionWorkForTesting = actual;
        var applied = f.Window.CurrentAggregateIntent!;
        Task? task = null; List<Exception> failures = [];
        try
        {
            task = f.Start(); F.Wait(actual.Ready.Task);
            Check(actual.Actual != null && !task.IsCompleted && f.NoImages && f.ReleaseAttempts == 0 &&
                ReferenceEquals(actual.Actual.Request.Document, applied), "held result is genuine completed source work, not a fake success");
            switch (kind)
            {
                case "raw": f.Set("FreshMaterial0TextureId", "1"); break;
                case "aba": f.Set("FreshMaterial0TextureId", "1"); f.Set("FreshMaterial0TextureId", "0"); break;
                case "base": f.Set("FreshHeading", "77"); break;
                case "route": f.Set("FreshWizardOriginZ", "8910"); break;
                case "project": f.CurrentProject = false; break;
                case "image": f.SelectedSource = (f.SelectedSource.Image + ".changed-selection-only", f.SelectedSource.Cue); break;
                case "cue": f.SelectedSource = (f.SelectedSource.Image, f.SelectedSource.Cue + ".changed-selection-only"); break;
                case "cancel": f.Fire("FreshMaterialsCancelInspect"); break;
                case "close": f.Window.Close(); F.Pump(); break;
            }
            Check(!task.IsCompleted && f.ReleaseAttempts == 0, "stale/cancel/close cannot release owner before delayed actual delivery");
        }
        catch (Exception error) { failures.Add(error); }
        finally
        {
            actual.Released.TrySetResult();
            if (task != null) try { F.Drain(task); } catch (Exception error) { failures.Add(error); }
            if (failures.Count != 0 && f.Window.MaterialInspectionFailureForTesting is { } actualFailure &&
                !failures.Any(e => ReferenceEquals(e, actualFailure))) failures.Add(actualFailure);
        }
        F.Throw(failures);
        Outcome(f, f.NoImages && f.Window.MaterialInspectionFailureForTesting is OperationCanceledException or InvalidDataException &&
            f.ReleaseAttempts == 1 && f.Window.MaterialInspectionOwnedBitmapsForTesting.All(bitmap => bitmap == null) &&
            ReferenceEquals(f.Window.CurrentAggregateIntent, applied),
            "actual ready result rejected by raw/generation/project/source/cancel/close fences; no bytes or document adoption");
    }

    private static void BitmapFailure(F f)
    {
        var primary = new IOException("actual UI before-second-bitmap refusal");
        WriteableBitmap? acquired = null;
        f.Window.MaterialInspectionCheckpointForTesting = phase =>
        {
            if (phase == "after-bitmap0") acquired = f.Window.MaterialInspectionOwnedBitmapsForTesting[0];
            if (phase == "before-bitmap1") throw primary;
        };
        F.Drain(f.Start());
        Outcome(f, acquired != null && ReferenceEquals(f.Window.MaterialInspectionFailureForTesting, primary) &&
            f.NoImages && f.Window.MaterialInspectionOwnedBitmapsForTesting.All(bitmap => bitmap == null),
            "real acquired first bitmap released on second-stage refusal, neither image installed");
        RequireDisposed(acquired!);
    }

    private static void CleanupNotificationFailure(F f)
    {
        var primary = new IOException("actual UI install observer refusal");
        var cleanup = new IOException("actual UI cleanup notification refusal");
        var owned = new List<WriteableBitmap>();
        f.Window.MaterialInspectionCheckpointForTesting = phase =>
        {
            if (phase == "before-install")
            {
                owned.AddRange(f.Window.MaterialInspectionOwnedBitmapsForTesting.OfType<WriteableBitmap>());
                throw primary;
            }
            if (phase == "before-dispose-bitmap0") throw cleanup;
        };
        F.Drain(f.Start());
        var leaves = f.Window.MaterialInspectionFailureForTesting is { } failure ? F.Leaves(failure) : [];
        Outcome(f, owned.Count == 2 && leaves.Count == 2 && ReferenceEquals(leaves[0], primary) &&
            ReferenceEquals(leaves[1], cleanup) && f.NoImages &&
            f.Window.MaterialInspectionOwnedBitmapsForTesting.All(bitmap => bitmap == null),
            "throwing notification cannot skip either actual bitmap disposal; exact ordered failures retained");
        foreach (var bitmap in owned) RequireDisposed(bitmap);
    }

    private static void RequireDisposed(WriteableBitmap bitmap)
    {
        try { using var locked = bitmap.Lock(); }
        catch (ObjectDisposedException) { checks++; return; }
        throw new InvalidDataException("Expected actual disposed bitmap to reject a new framebuffer lock.");
    }
    private static byte[] ReadBitmap(WriteableBitmap bitmap)
    {
        using ILockedFramebuffer locked = bitmap.Lock();
        if (locked.Format != PixelFormat.Rgba8888 || locked.Size.Width != 32 || locked.Size.Height != 32)
            throw new InvalidDataException("Unexpected actual bitmap format/size.");
        byte[] bytes = new byte[32 * 32 * 4];
        for (int row = 0; row < 32; row++) Marshal.Copy(IntPtr.Add(locked.Address, row * locked.RowBytes), bytes, row * 32 * 4, 32 * 4);
        return bytes;
    }
    private static string Golden(int wad, int id) => (wad, id) switch
    {
        (14, 0) => "32b6ed11158f07efcd99e58b2ad25d548785dc2246c2204077c8e5a134c7a0e8",
        (70, 1) => "912676823721b6edcb03f1d6b9c1e8c8794e71ef867a7ac66e840c88beb79283",
        _ => throw new InvalidDataException("No independent literal for this UI fixture.")
    };
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Authenticate(Stream stream, long length, string sha)
    {
        List<Exception> failures = [];
        string? digest = null;
        Observe(() => Require(stream.Length == length, "initial complete length changed"));
        Observe(() => stream.Position = 0);
        Observe(() => digest = Convert.ToHexStringLower(SHA256.HashData(stream)));
        // A failed hash contributes its actual failure, never a fabricated mismatch.
        if (digest != null) Observe(() => Require(digest == sha, "complete SHA changed"));
        Observe(() => Require(stream.Position == length, "position after complete digest changed"));
        Observe(() => Require(stream.ReadByte() == -1, "actual EOF check returned an extra byte"));
        Observe(() => Require(stream.Position == length, "final position changed"));
        Observe(() => Require(stream.Length == length, "final complete length changed"));
        F.Throw(failures);
        void Observe(Action action) { try { action(); } catch (Exception error) { failures.Add(error); } }
        static void Require(bool condition, string message)
        { if (!condition) throw new InvalidDataException("Original fixture " + message + "."); }
    }
    private static void AuthenticatePath(string path, long length, string sha)
    {
        FileStream? stream = null; List<Exception> failures = [];
        try { stream = File.OpenRead(path); Authenticate(stream, length, sha); }
        catch (Exception error) { failures.Add(error); }
        finally { try { stream?.Dispose(); } catch (Exception error) { failures.Add(error); } }
        F.Throw(failures);
    }
    private static void Outcome(F f, bool condition, string message)
    {
        if (condition) { checks++; return; }
        var assertion = new InvalidDataException("Inspection source UI: " + message);
        if (f.Window.MaterialInspectionFailureForTesting is { } actual)
            throw new IOException("Actual UI failure retained before failed expectation.", new AggregateException(actual, assertion));
        throw assertion;
    }
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidDataException("Inspection source UI: " + message); checks++; }
}
