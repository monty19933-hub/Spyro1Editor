using System.Reflection;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;
using F = NativeFreshMaterialInspectionUiFixture;

// Real Store capture and actual headless window commands. Schema5 is neutral
// persistence only until the editor can render/edit the complete family union.
internal static class FamilyDocumentAdmissionCases
{
    private const string Legacy = "Legacy floor (v1)", Aggregate = "Level aggregate";
    private const string FamilyId = "33333333333333333333333333333333";
    private const string TileId = "44444444444444444444444444444444";

    internal static void Run(string root)
    {
        int checks = 0, refused = 0, supported = 0;
        foreach (bool tiled in new[] { false, true })
        foreach (bool family in new[] { false, true })
        foreach (bool reopen in new[] { false, true })
        foreach (bool rawDraft in new[] { false, true })
        {
            string id = $"family-admission-{tiled}-{family}-{reopen}-{rawDraft}".ToLowerInvariant();
            WithFixture(id, fixture =>
            {
                if (!reopen) Switch(fixture, Legacy);
                // An external neutral writer saves schema5 while this existing
                // window retains its own legacy/schema2 applied document.
                SaveExternal(fixture, NewDocument(tiled, family));
                int decisions = 0;
                fixture.Window.DecisionForTesting = _ =>
                { decisions++; return Task.FromResult(FreshDocumentDecision.Discard); };
                if (rawDraft) fixture.Set("FreshHeading", "unapplied draft");
                var before = new WindowState(fixture);
                if (reopen) fixture.Fire("FreshDocumentReopen");
                else Switch(fixture, Aggregate);

                string status = fixture.Find<TextBlock>("FreshDocumentStatus").Text ?? "";
                Check(status.Contains("schema-5", StringComparison.Ordinal) &&
                    status.Contains("neutral", StringComparison.Ordinal) &&
                    status.Contains("cannot edit or render", StringComparison.Ordinal),
                    id + ": unsupported stored schema must be explained, not adopted or partially rendered; " +
                    $"observed schema={fixture.Window.CurrentAggregateIntent?.SchemaVersion}; status={status}");
                before.RequireUnchanged(fixture, Check);
                Check(decisions == (rawDraft ? 1 : 0), id + ": discard permission never discards a draft on admission failure");
                Check(fixture.SourceCalls == 0 && fixture.OwnerAttempts == 0,
                    id + ": no source selection, native capture or research ownership");
                fixture.RequireProtected();
                refused++;
            });
        }

        // Existing versions still enter, apply, save and reopen through the
        // same admission point. A new guard must not disable their workflows.
        foreach (int schema in new[] { 1, 2, 3, 4 })
        {
            WithFixture("family-admission-supported-" + schema, fixture =>
            {
                Switch(fixture, Legacy);
                var expected = OldDocument(schema);
                SaveExternal(fixture, expected);
                Switch(fixture, Aggregate);
                RequireExpected();
                fixture.Fire("FreshDocumentApply");
                fixture.Fire("FreshDocumentSave");
                fixture.Fire("FreshDocumentReopen");
                RequireExpected();
                Check(!fixture.Window.HasUnsavedChanges && fixture.SourceCalls == 0 && fixture.OwnerAttempts == 0,
                    "schema" + schema + ": original neutral UI remains clean and source-free");
                fixture.RequireProtected();
                supported++;
                void RequireExpected() => Check(fixture.Window.CurrentAggregateIntent is { } actual &&
                    actual.SchemaVersion == schema && NativeFreshLevelDocumentCodec.SerializeCanonical(actual)
                        .SequenceEqual(fixture.ExpectedDocument), "schema" + schema + ": complete original aggregate admitted without upgrade or projection");
            });
        }
        Console.WriteLine($"FRESH_FAMILY_DOCUMENT_ADMISSION_OK checks={checks} refused={refused} legacySchemas={supported} sourceOpened=false nativeOutputWritten=false");

        void Check(bool condition, string message)
        { if (!condition) throw new InvalidDataException(message); checks++; }
        void WithFixture(string id, Action<F> test)
        {
            var fixture = new F(root, id);
            List<Exception> failures = [];
            try { test(fixture); }
            catch (Exception error) { failures.Add(error); }
            finally
            {
                Attempt(fixture.Close);
                Attempt(fixture.RequireProtected);
            }
            F.Throw(failures);
            void Attempt(Action action) { try { action(); } catch (Exception error) { failures.Add(error); } }
        }
    }

    private static NativeFreshLevelDocumentIntent OldDocument(int schema)
    {
        var material = F.Document();
        return schema switch
        {
            1 => NativeFreshLevelDocumentIntent.Create(material.Base, material.Placements),
            2 => material,
            3 => material.UpgradeForNativeMobys(),
            4 => material.UpgradeToTiled(TileId),
            _ => throw new ArgumentOutOfRangeException(nameof(schema))
        };
    }

    private static NativeFreshLevelDocumentIntent NewDocument(bool tiled, bool family)
    {
        var document = OldDocument(tiled ? 4 : 2).WithPlacements([]).UpgradeForFamilies();
        if (!family) return document;
        var source = NativeFreshLockedChestFamilyReference.Create(F.Variant,
            NativeFreshLockedChestFamilyReference.SupportedDefinitionId);
        var destinations = NativeFreshChestRewardDestinationOverrides.Create(
            [new(NativeFreshChestRewardRole.Reward2, 10454, 10503, 9216)]);
        return document.WithPlacements([NativeFreshLockedChestFamilyPlacementIntent.Create(FamilyId, source,
            8961, 9729, 8705, 9473, 8961, 8705, destinations)]);
    }

    private static void SaveExternal(F fixture, NativeFreshLevelDocumentIntent document)
    {
        NativeFreshLevelDocumentStore.Save(NativeFreshLevelDocumentStore.Capture(fixture.Project), document);
        fixture.ExpectedDocument = NativeFreshLevelDocumentCodec.SerializeCanonical(document);
        var captured = NativeFreshLevelDocumentStore.Capture(fixture.Project);
        F.Require(captured.Intent != null && NativeFreshLevelDocumentCodec.SerializeCanonical(captured.Intent)
            .SequenceEqual(fixture.ExpectedDocument), "external fixture is a complete parseable saved document, not a parser error");
    }

    private static void Switch(F fixture, string mode)
    { fixture.Find<ComboBox>("FreshDocumentMode").SelectedItem = mode; F.Pump(); }

    private sealed class WindowState
    {
        private readonly NativeId65FreshDocumentIntent? legacy, previewLegacy;
        private readonly NativeFreshLevelDocumentIntent? aggregate, previewAggregate;
        private readonly object? session, snapshot, forkSource;
        private readonly Dictionary<Control, object?> raw;
        private readonly (string Image, string Cue) source;
        private readonly bool dirty;
        private readonly string? selected;

        internal WindowState(F fixture)
        {
            var window = fixture.Window;
            legacy = window.CurrentIntent; aggregate = window.CurrentAggregateIntent;
            previewLegacy = window.Preview.Intent; previewAggregate = window.Preview.AggregateIntent;
            session = Field(window, "_aggregateSession"); snapshot = Field(window, "_snapshot");
            forkSource = Field(window, "_forkSourceSnapshot");
            selected = window.Preview.SelectedEditorId;
            raw = window.GetLogicalDescendants().OfType<Control>()
                .Where(control => control is TextBox or ComboBox or ListBox)
                .ToDictionary(control => control, Value);
            source = fixture.SelectedSource; dirty = window.HasUnsavedChanges;
        }

        internal void RequireUnchanged(F fixture, Action<bool, string> check)
        {
            var window = fixture.Window;
            check(ReferenceEquals(legacy, window.CurrentIntent) && ReferenceEquals(aggregate, window.CurrentAggregateIntent) &&
                ReferenceEquals(session, Field(window, "_aggregateSession")) && ReferenceEquals(snapshot, Field(window, "_snapshot")) &&
                ReferenceEquals(forkSource, Field(window, "_forkSourceSnapshot")), "failed admission retains actual session and source snapshots");
            check(ReferenceEquals(previewLegacy, window.Preview.Intent) && ReferenceEquals(previewAggregate, window.Preview.AggregateIntent) &&
                selected == window.Preview.SelectedEditorId, "failed admission does not replace the preview or placement selection");
            check(raw.All(pair => Equals(pair.Value, Value(pair.Key))), "failed admission retains every raw field and form selection");
            check(source == fixture.SelectedSource && dirty == window.HasUnsavedChanges,
                "failed admission retains project source selection and unsaved state");
        }

        private static object? Value(Control control) => control switch
        { TextBox text => text.Text, ComboBox box => box.SelectedItem, ListBox list => list.SelectedItem, _ => null };
        private static object? Field(NativeFreshDocumentWindow window, string name) =>
            typeof(NativeFreshDocumentWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(window);
    }
}
