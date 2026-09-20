using System.Collections;
using System.Reflection;
using System.Security.Cryptography;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;

// Actual bounded schematic rendering only: no Window document admission, ROM,
// source resolver, compiler or publication. Reflection keeps the first RED
// executable against the old renderer, rather than failing on a missing API.
internal static class NativeFreshFamilyPreviewCases
{
    private const string DefaultNotice = "native default—resolved during source compilation";
    private static int checks;
    // Pinned by the unchanged renderer's563 runtime RED, before implementation.
    // These are the current headless Skia/font environment's complete PNG bytes.
    private static readonly IReadOnlyDictionary<string, string> LegacyRenderSha256 =
        new Dictionary<string, string>
        {
            ["schema1-640x480"] = "ae18900bcf527160e2a6efb2b6cc4ea8cd4bd5114840ab6c20a84145a6a20074",
            ["schema1-360x360"] = "fb0d988abd8263154aa6f7a42aecad6f98ca9fffadef7b3a13e2b7d166487f1d",
            ["schema2-640x480"] = "22836258cc9c54845d5e4ec00712f17945dad502c1cf733bbc3f9917af7c63a6",
            ["schema2-360x360"] = "6529e8ad6ab960786f9440de61de94a24e82d7bbb75496cd7a095d3310cb2010",
            ["schema3-640x480"] = "49966492d4d752924ea910cb77e9e999b6dd1f1a25b9f4a994471c99a2cef5c3",
            ["schema3-360x360"] = "7e92677163098c6a1ef7b7bc6ba5cc394ed287b906f9f89f86a05dca52d80f2a",
            ["schema4-640x480"] = "b84e11335d4c08c6030e406dd4ef3f023cb6139c85ecde066838ef7edc8201a9",
            ["schema4-360x360"] = "5ee4888adbb1897fd6a0059f73eda526a4a5570cfcd39ae8a1222972ddbc904d"
        };
    private static readonly IReadOnlyDictionary<string, (Rect Bounds, double CaptionTop)> LegacyGeometry =
        new Dictionary<string, (Rect, double)>
        {
            ["schema1-640x480"] = (new(79.34315789473683, 181.05263157894737, 481.3136842105264, 277.8947368421052), 446),
            ["schema1-360x360"] = (new(40, 179.16859122401848, 280, 161.66281755196303), 326),
            ["schema2-640x480"] = (new(95.38694736842106, 150.31578947368422, 449.22610526315793, 259.36842105263156), 424),
            ["schema2-360x360"] = (new(42.89852631578947, 140.8421052631579, 274.20294736842106, 158.3157894736842), 304),
            ["schema3-640x480"] = (new(95.38694736842106, 150.31578947368422, 449.22610526315793, 259.36842105263156), 424),
            ["schema3-360x360"] = (new(42.89852631578947, 140.8421052631579, 274.20294736842106, 158.3157894736842), 304),
            ["schema4-640x480"] = (new(53.27199999999999, 96, 533.4560000000001, 308), 424),
            ["schema4-360x360"] = (new(40, 109.16859122401847, 280, 161.66281755196303), 304)
        };

    internal static void Run(string root)
    {
        checks = 0;
        Directory.CreateDirectory(root);
        LegacyReferences(root);
        var original = NativeFreshFamilyDocumentSessionCases.Document();
        var family = original.Placements.OfType<NativeFreshLockedChestFamilyPlacementIntent>().Single();
        foreach (bool tiled in new[] { false, true })
        foreach (int explicitCount in new[] { 0, 1, 5 })
        {
            NativeFreshChestRewardDestinationOverrides? destinations = explicitCount == 0 ? null :
                NativeFreshChestRewardDestinationOverrides.Create(Enumerable.Range(0, explicitCount).Select(i =>
                    new NativeFreshChestRewardDestinationOverride((NativeFreshChestRewardRole)i,
                        10000 + i * 17, 10200 + i * 19, 9100 + i * 13)).ToArray());
            var document = Replace(original, family.WithRewardDestinations(destinations));
            if (tiled) document = Tiled(document);
            using var frame = new Frame(document, family.EditorId, 640, 480);
            var families = Items(Read(frame.Preview, "FamilyPreviews"));
            Check(families.Length == 1, "One authored family, not seven synthetic authored rows.");
            Check(Read(frame.Preview, "FamilyPreviews") is IList { IsReadOnly: true },
                "The family presentation is a read-only snapshot, not mutable editor state.");
            var shown = families.Single();
            Check(Value<string>(shown, "EditorId") == family.EditorId && Value<bool>(shown, "IsSelected") &&
                Value<string>(shown, "DefinitionId") == family.Source.DefinitionId,
                "Preview retains selected parent ID and exact neutral family definition.");
            Check(frame.Preview.PlacementPreviewCaption ==
                "1 authored route + 1 native marker + 1 authored family (XY only)",
                "Family is not counted as a route or native marker.");
            Check(Value<string>(frame.Preview, "FamilyPreviewNotice").Contains("XY", StringComparison.Ordinal),
                "Family notice explicitly describes schematic scope.");
            var members = Items(Read(shown, "Members"));
            Check(Read(shown, "Members") is IList { IsReadOnly: true }, "Semantic member list is immutable.");
            Check(members.Select(m => Value<string>(m, "Role")).SequenceEqual(
                new[] { "key", "chest", "reward.1", "reward.2", "reward.3", "reward.4", "reward.5" }),
                "All seven semantic members have a stable, complete mapping.");
            Check(Value<int>(members[0], "X") == family.KeyX && Value<int>(members[0], "Y") == family.KeyY &&
                Value<int>(members[0], "Z") == family.KeyZ &&
                Value<int>(members[1], "X") == family.ChestX && Value<int>(members[1], "Y") == family.ChestY &&
                Value<int>(members[1], "Z") == family.ChestZ, "Both independent anchor XYZ values remain exact.");
            for (int i = 0; i < 5; i++)
            {
                var member = members[i + 2];
                Check(Value<string>(member, "Label") == $"Reward{i + 1}", "Named reward, not a native row ordinal.");
                bool nativeDefault = i >= explicitCount;
                Check(Value<bool>(member, "IsNativeDefault") == nativeDefault, "Default versus explicit remains authored intent.");
                if (nativeDefault)
                    Check(Read(member, "X") == null && Read(member, "Y") == null && Read(member, "Z") == null &&
                        Read(member, "ProjectedPoint") == null &&
                        Value<string>(member, "Description").Contains(DefaultNotice, StringComparison.Ordinal),
                        "Native defaults have a complete label but no invented coordinates or plotted marker.");
                else
                    Check(Value<int>(member, "X") == 10000 + i * 17 && Value<int>(member, "Y") == 10200 + i * 19 &&
                        Value<int>(member, "Z") == 9100 + i * 13 && Read(member, "ProjectedPoint") is Point &&
                        Value<string>(member, "Description").Contains("WORLD", StringComparison.Ordinal),
                        "Only explicit destinations have exact numeric WORLD positions and schematic projection.");
            }
            Check(members.Count(m => Read(m, "ProjectedPoint") is Point) == explicitCount + 2,
                "Two anchor markers plus exactly the explicitly authored reward destinations.");
            var viewport = Value<Rect>(frame.Preview, "FamilyMarkerViewport");
            Check(members.Where(m => Read(m, "ProjectedPoint") is Point && !Value<bool>(m, "IsOutsideView"))
                .All(m => viewport.Deflate(7).Contains(Value<Point>(m, "ProjectedPoint"))),
                "Visible markers fit completely inside the bounded clip region.");
            Check(frame.Preview.FloorSchematicBounds is { } floor && floor.Top >= 76 &&
                floor.Bottom < frame.Preview.SchematicCaptionTop && floor.Left >= 0 && floor.Right <= frame.Preview.Bounds.Width,
                "Both content variants fit the actual floor above the family legend.");
            if (explicitCount == 5)
            {
                var labels = Items(Read(frame.Preview, "FamilyMarkerLabels"));
                var visible = labels.Where(label => Value<bool>(label, "IsVisible")).ToArray();
                Check(labels.Length == 7 && visible.Length > 0 && visible.Length < labels.Length,
                    "Crowded text is suppressed without removing any of the seven markers/member descriptions.");
                Check(visible.SelectMany((left, i) => visible.Skip(i + 1).Select(right =>
                        !Value<Rect>(left, "MeasuredBounds").Intersects(Value<Rect>(right, "MeasuredBounds")))).All(value => value),
                    "Visible marker text uses actual measured nonintersecting rectangles.");
                Check(labels.All(label => Value<Point>(label, "Position") ==
                    Value<Point>(members.Single(member => Value<string>(member, "Role") == Value<string>(label, "Role")),
                        "ProjectedPoint") + new Vector(8, -14)),
                    "Label collision handling does not nudge text anchors, markers or authored WORLD coordinates.");
            }
            _ = frame.Save(Path.Combine(root, $"family-preview-{(tiled ? "tiled" : "legacy")}-{explicitCount}.png"));
            frame.Preview.SetAggregate(document, original.Placements[0].EditorId);
            Check(!Value<bool>(Items(Read(frame.Preview, "FamilyPreviews")).Single(), "IsSelected"),
                "Selecting a different parent does not select a hidden child or lose the family.");
        }
        FixedWorldAndOffView(original, family, root);
        MultipleAndReset(original, family);
        Console.WriteLine($"FRESH_FAMILY_PREVIEW_OK checks={checks} semanticMembers=7 contentVariants=2 " +
            $"explicitCounts=0,1,5 oldGeometryReferences=8 oldPixelReferenceChecks={(OperatingSystem.IsMacOS() ? 8 : 0)} " +
            "sourceOpened=false nativeOutputWritten=false");
    }

    private static void LegacyReferences(string root)
    {
        if (!OperatingSystem.IsMacOS())
            Console.WriteLine("FAMILY_PREVIEW_PIXEL_PROVENANCE_SKIP platform=non-macOS " +
                "reason=563-pixel-goldens-belong-to-macOS-Avalonia12.0.3-headless-Skia-default-font-environment " +
                "pixelEquivalenceVerified=false exactGeometryAndCaptionChecks=true referencesNotRefreshed=true");
        var withMaterials = NativeFreshMaterialInspectionUiFixture.Document();
        var one = NativeFreshLevelDocumentIntent.Create(withMaterials.Base, withMaterials.Placements);
        var two = one.WithFloorMaterials(withMaterials.FloorMaterials!);
        var three = two.UpgradeForNativeMobys().WithPlacements(two.Placements.Append(
            NativeFreshMobyPlacementIntent.Create("33333333333333333333333333333333",
                NativeFreshRetailMobyReference.Create(NativeFreshMaterialInspectionUiFixture.Variant, 10, 121, 0x1F5),
                9152, 9152, 8704, 64)));
        var four = three.UpgradeToTiled("44444444444444444444444444444444");
        foreach (var document in new[] { one, two, three, four })
        foreach (var size in new[] { new PixelSize(640, 480), new PixelSize(360, 360) })
        {
            using var frame = new Frame(document, document.Placements[0].EditorId, size.Width, size.Height);
            string name = $"schema{document.SchemaVersion}-{size.Width}x{size.Height}";
            string actual = frame.Save(Path.Combine(root, $"family-preview-old-{name}.png"));
            var bounds = frame.Preview.FloorSchematicBounds!.Value;
            Console.WriteLine($"FAMILY_PREVIEW_OLD_REFERENCE {name} sha256={actual} bounds={bounds} " +
                $"captionTop={frame.Preview.SchematicCaptionTop}");
            if (OperatingSystem.IsMacOS())
                Check(actual == LegacyRenderSha256[name], $"Unchanged schemas1–4 complete rendered pixels in the pinned macOS headless environment: {name}.");
            var expectedGeometry = LegacyGeometry[name];
            Check(bounds == expectedGeometry.Bounds && frame.Preview.SchematicCaptionTop == expectedGeometry.CaptionTop,
                $"Exact old floor bounds and caption-top property values: {name}.");
            string caption = document.SchemaVersion < 3 ? "2 authored routes (XY only)" :
                "2 authored routes + 1 native marker (XY only)";
            Check(frame.Preview.PlacementPreviewCaption == caption, "Old exact caption remains unchanged.");
        }
    }

    private static void FixedWorldAndOffView(NativeFreshLevelDocumentIntent original,
        NativeFreshLockedChestFamilyPlacementIntent family, string root)
    {
        // Even equality to an anchor is explicit authorship, never a default.
        var overrides = NativeFreshChestRewardDestinationOverrides.Create(new[]
        {
            new NativeFreshChestRewardDestinationOverride(NativeFreshChestRewardRole.Reward1,
                family.ChestX, family.ChestY, family.ChestZ),
            new NativeFreshChestRewardDestinationOverride(NativeFreshChestRewardRole.Reward2, 262128, 0, 262128)
        });
        var authored = family.WithRewardDestinations(overrides);
        foreach (bool tiled in new[] { false, true })
        {
            var document = Replace(original, authored);
            if (tiled) document = Tiled(document);
            using var frame = new Frame(document, family.EditorId, 360, 360);
            var before = Items(Read(Items(Read(frame.Preview, "FamilyPreviews")).Single(), "Members"));
            var offView = before[3];
            Check(Value<bool>(offView, "IsOutsideSurface") && Value<bool>(offView, "IsOutsideView") &&
                Value<int>(offView, "X") == 262128 && Value<int>(offView, "Y") == 0 && Value<int>(offView, "Z") == 262128,
                "Off-floor and off-view WORLD coordinates are reported, not clamped or discarded.");
            Check(Read(offView, "ProjectedPoint") is Point p &&
                !Value<Rect>(frame.Preview, "FamilyMarkerViewport").Contains(p), "Original projected point is retained outside clipping viewport.");
            Check(Value<string>(offView, "Description").Contains("outside authored surface", StringComparison.Ordinal) &&
                Value<string>(offView, "Description").Contains("outside view", StringComparison.Ordinal),
                "Out-of-view destinations have explicit human-readable callouts.");
            Check(!Value<bool>(before[2], "IsNativeDefault"), "Explicit value equal to an anchor is not normalized to default.");
            var moved = authored.WithAnchors(family.KeyX, family.KeyY, family.KeyZ,
                family.ChestX + 16, family.ChestY, family.ChestZ + 13);
            frame.Preview.SetAggregate(Replace(document, moved), family.EditorId);
            var after = Items(Read(Items(Read(frame.Preview, "FamilyPreviews")).Single(), "Members"));
            for (int i = 2; i < 7; i++)
                Check(Equals(Read(before[i], "X"), Read(after[i], "X")) &&
                    Equals(Read(before[i], "Y"), Read(after[i], "Y")) && Equals(Read(before[i], "Z"), Read(after[i], "Z")),
                    "Chest movement never translates fixed WORLD destinations or fabricates defaults.");
            var zOnly = authored.WithRewardDestinations(NativeFreshChestRewardDestinationOverrides.Create(new[]
            {
                new NativeFreshChestRewardDestinationOverride(NativeFreshChestRewardRole.Reward1,
                    family.ChestX, family.ChestY, 262128), overrides.Values[1]
            }));
            frame.Preview.SetAggregate(Replace(document, zOnly), family.EditorId);
            var zMember = Items(Read(Items(Read(frame.Preview, "FamilyPreviews")).Single(), "Members"))[2];
            Check(Value<int>(zMember, "Z") == 262128 &&
                Equals(Read(zMember, "ProjectedPoint"), Read(before[2], "ProjectedPoint")),
                "Exact authored Z is numeric only; it does not fake a projected native height.");
            _ = frame.Save(Path.Combine(root, $"family-preview-off-view-{(tiled ? "tiled" : "legacy")}.png"));
        }
    }

    private static void MultipleAndReset(NativeFreshLevelDocumentIntent document,
        NativeFreshLockedChestFamilyPlacementIntent family)
    {
        var other = NativeFreshLockedChestFamilyPlacementIntent.Create("99999999999999999999999999999999",
            family.Source, family.KeyX, family.KeyY, family.KeyZ, family.ChestX, family.ChestY, family.ChestZ);
        using var frame = new Frame(document.WithPlacements(document.Placements.Append(other)), other.EditorId, 640, 480);
        var families = Items(Read(frame.Preview, "FamilyPreviews"));
        Check(families.Length == 2 && families.Sum(f => Items(Read(f, "Members")).Length) == 14 &&
            families.Count(f => Value<bool>(f, "IsSelected")) == 1 &&
            frame.Preview.PlacementPreviewCaption.Contains("2 authored families", StringComparison.Ordinal),
            "Every neutral family is accounted without implying two families are natively eligible.");
        var r03Members = Items(Read(families.Single(f => Value<string>(f, "EditorId") == family.EditorId), "Members"));
        Check(r03Members.Skip(2).Select(m => Value<bool>(m, "IsNativeDefault"))
                .SequenceEqual(new[] { true, false, true, true, true }) &&
            Value<string>(r03Members[3], "Role") == "reward.2" &&
            Value<int>(r03Members[3], "X") == 10454 && Value<int>(r03Members[3], "Y") == 10503 &&
            Value<int>(r03Members[3], "Z") == 9216,
            "The actual R03 single Reward2 override maps by semantic role, not by override-list position.");
        frame.Preview.Intent = document.Base;
        Check(Items(Read(frame.Preview, "FamilyPreviews")).Length == 0 &&
            Value<string>(frame.Preview, "FamilyPreviewNotice").Length == 0 && frame.Preview.SelectedEditorId == null,
            "Legacy view clears every family-only presentation field.");
    }

    private static NativeFreshLevelDocumentIntent Replace(NativeFreshLevelDocumentIntent document,
        NativeFreshLockedChestFamilyPlacementIntent family) => document.WithPlacements(document.Placements.Select(
            p => p.EditorId == family.EditorId ? family : p));

    private static NativeFreshLevelDocumentIntent Tiled(NativeFreshLevelDocumentIntent document)
    {
        var b = document.Base;
        var tiles = Enumerable.Range(0, 4).Select(i => NativeFreshFlatTileIntent.Create(
            (i + 5).ToString().PadLeft(32, '0'), b.FloorX + (i % 2) * 1536,
            b.FloorY + (i / 2) * 1536, b.FloorZ, 1536, 1536, b.FloorColor, document.FloorMaterials!));
        var content = NativeFreshTiledLevelContent.Create(tiles, b.SpawnX, b.SpawnY, b.SpawnZ,
            b.Heading, b.SkyColor, b.MusicTrackId);
        return NativeFreshLevelDocumentIntent.CreateTiled(content, document.Placements.Take(2))
            .UpgradeForFamilies().WithPlacements(document.Placements);
    }

    private static object? Read(object owner, string property) => owner.GetType().GetProperty(property,
        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.GetValue(owner) ??
        (owner.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public) != null
            ? null : throw new InvalidDataException($"Family preview is missing the complete inspectable {property} representation."));
    private static T Value<T>(object owner, string property) => Read(owner, property) is T value ? value :
        throw new InvalidDataException($"Family preview {property} must be {typeof(T).Name}.");
    private static object[] Items(object? value) => value is IEnumerable values ? values.Cast<object>().ToArray() :
        throw new InvalidDataException("Family preview must provide a bounded immutable member list.");
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidDataException("Family preview: " + message); checks++; }

    private sealed class Frame : IDisposable
    {
        internal readonly NativeFreshDocumentPreview Preview = new();
        private readonly Window window;
        internal Frame(NativeFreshLevelDocumentIntent document, string selected, int width, int height)
        {
            Preview.SetAggregate(document, selected);
            window = new Window { Width = width, Height = height, Content = Preview, WindowDecorations = WindowDecorations.None };
            window.Show(); Dispatcher.UIThread.RunJobs();
            Check(Preview.Bounds.Width == width && Preview.Bounds.Height == height, "Exact standalone schematic capture bounds.");
        }
        internal string Save(string path)
        {
            Dispatcher.UIThread.RunJobs();
            using var bitmap = window.CaptureRenderedFrame() ?? throw new InvalidDataException("Actual headless family frame required.");
            using var stream = new MemoryStream(); bitmap.Save(stream);
            byte[] bytes = stream.ToArray();
            File.WriteAllBytes(path, bytes);
            return Convert.ToHexStringLower(SHA256.HashData(bytes));
        }
        public void Dispose() { window.Close(); Dispatcher.UIThread.RunJobs(); }
    }
}
