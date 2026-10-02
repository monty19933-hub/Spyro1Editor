using Spyro.Editor.Core.Persistence;

namespace Spyro.Editor.App.Views;

public partial class MainWindow
{
    internal Func<string, Task>? FreeFlightExportDelayOverrideForTesting { get; set; }

    private static bool HasOrdinarySavedEdits(EditedLevelExportTarget target) =>
        target.HasPortalRoutes || target.HasObjectEdits || target.HasTerrainEdits || target.HasCustomTerrainTextures ||
        target.HasNativeTerrainTextureRelocations || target.HasLevelTextEdit || target.HasLevelMusicEdit || target.HasSkyboxEdit;

    private void RequireFreeFlightExportOwner(NativeFreeFlightProjectExportIntent? intent,
        SourceDiscOperationIdentity owner, string? selectedImage, string? selectedSkyImage)
    {
        intent?.RequireCurrent(_workspace.RootPath);
        if (TryGetFreeFlightSettingsExportBlockReason(out string reason))
            throw new InvalidOperationException(reason);
        // Captured AFTER the command's normal Save and Build Safety, whose
        // legitimate generation changes must not invalidate their own export.
        if (!IsCurrentSourceDiscOperationIdentity(owner) || selectedImage != _discImagePathBox.Text ||
            selectedSkyImage != _skyboxDiscImagePathBox.Text)
            throw new InvalidOperationException("Fly Anywhere export lost its project, level, edits or selected-source ownership. Start Create BIN again.");
    }
}
