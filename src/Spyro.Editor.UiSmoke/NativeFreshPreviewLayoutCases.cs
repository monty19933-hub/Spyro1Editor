using Avalonia.Controls;
using Avalonia.LogicalTree;
using F = NativeFreshMaterialInspectionUiFixture;

internal static class NativeFreshPreviewLayoutCases
{
    internal static void Run(string root)
    {
        var fixture = new F(root, "preview-layout");
        List<Exception> failures = [];
        int checks = 0;
        try
        {
            foreach (string mode in new[] { "Level aggregate", "Legacy floor (v1)" })
            {
                fixture.Find<ComboBox>("FreshDocumentMode").SelectedItem = mode;
                foreach (var (width, height) in new[] { (1120, 800), (940, 680), (1400, 1000) })
                {
                    fixture.Window.Width = width; fixture.Window.Height = height; F.Pump();
                    var preview = fixture.Window.Preview;
                    var note = fixture.Find<TextBlock>("FreshDocumentPreviewNote");
                    // Both are immediate children of the same Grid. A parent
                    // viewport may scroll, but their own layout must not overlap.
                    Check(ReferenceEquals(preview.Parent, note.Parent), "Shared schematic layout parent.");
                    Check(note.Bounds.Top >= preview.Bounds.Bottom + 9.5,
                        $"Schematic must not cover its caption: {mode}/{width}x{height}; bottom={preview.Bounds.Bottom}; note={note.Bounds.Top}.");
                    Check(preview.Bounds.Height >= 360 && note.Bounds.Height > 0,
                        "Retain the existing minimum schematic and a real caption.");
                    var apply = fixture.Find<Button>("FreshDocumentApply");
                    var save = fixture.Find<Button>("FreshDocumentSave");
                    Check(ReferenceEquals(apply.Parent, save.Parent) &&
                        !apply.GetLogicalAncestors().OfType<ScrollViewer>().Any(),
                        "Apply stays beside Save outside the scrolling inspector in both document modes.");
                    Check(apply.Bounds.Width > 0 && apply.Bounds.Left >= 0 &&
                        apply.Bounds.Right <= ((Control)apply.Parent!).Bounds.Width,
                        "Apply remains fully reachable at default, minimum and enlarged window sizes.");
                }
            }
        }
        catch (Exception error) { failures.Add(error); }
        finally { Attempt(fixture.Close); Attempt(fixture.RequireProtected); }
        F.Throw(failures);
        Console.WriteLine($"FRESH_PREVIEW_LAYOUT_OK checks={checks} sourceOpened=false nativeWindow=false");
        void Check(bool condition, string message)
        { if (!condition) throw new InvalidDataException(message); checks++; }
        void Attempt(Action action) { try { action(); } catch (Exception error) { failures.Add(error); } }
    }
}
