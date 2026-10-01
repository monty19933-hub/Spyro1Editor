using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Universal;

public sealed record UniversalSurveyCell(string Destination, int ClassId, string SourceLevelKey, int SourceRow,
    string Outcome, string? Detail, int? RamNeeded, int? RamHeadroom)
{
    public string DependencyExportStatus { get; init; } = "not-tested";
    public string RuntimeStatus { get; init; } = "not-tested";
    public IReadOnlyList<UniversalClassImport> Dependencies { get; init; } = [];
}

/// <summary>Dry-run composition of every placed class (default variant) into every retail destination.</summary>
public static class UniversalMobySurvey
{
    public static List<UniversalSurveyCell> Run(UniversalSourceLibrary lib, LevelCatalog catalog, Action<string>? progress = null)
    {
        var defaults = UniversalMobyCatalogBuilder.DefaultVariants(lib, catalog);
        List<UniversalSurveyCell> cells = [];
        foreach (var dest in catalog.Levels.Where(l => l.SourceWadEntry > 0))
        {
            progress?.Invoke(dest.Key);
            var d = lib.Get(dest.Key);
            var vram = UniversalMobyExport.DefaultProtectedVram(lib.ImagePath, dest).ToList();
            foreach (var v in defaults)
            {
                var src = lib.Get(v.SourceLevelKey);
                var row = src.Data.Row(v.SourceRow);
                var p = new UniversalMobyPlacement(v.SourceLevelKey, v.SourceRow,
                    BitConverter.ToInt32(row, 0x0C), BitConverter.ToInt32(row, 0x10), BitConverter.ToInt32(row, 0x14));
                try
                {
                    var r = UniversalMobyComposer.Compose(dest, d.Data.Bytes, d.Overlay.Bytes.ToArray(), lib.Disc.Exe,
                        lib.Get, lib.SharedClasses, vram, [p], sharedModelPackage: lib.SharedModelPackage);
                    cells.Add(new(dest.Key, v.ClassId, v.SourceLevelKey, v.SourceRow, "ok", null,
                        (int)(r.Ram.FinalEnd - r.Ram.OriginalEnd), r.Ram.OriginalHeadroom) { DependencyExportStatus = r.ParticleDependencies.Any(p => p.Status == "dependency-unverified") || r.UnresolvedObjectLinks > 0 ? "composed-dependency-unverified" : "composed-static-only", Dependencies = r.Classes });
                }
                catch (UniversalBudgetException e)
                {
                    cells.Add(new(dest.Key, v.ClassId, v.SourceLevelKey, v.SourceRow, "ram", e.Message,
                        (int)(e.Budget.FinalEnd - e.Budget.OriginalEnd), e.Budget.OriginalHeadroom));
                }
                catch (Exception e)
                {
                    string kind = e.Message.Contains("texture", StringComparison.OrdinalIgnoreCase) || e.Message.Contains("CLUT") ? "vram"
                        : e.Message.Contains("arena") || e.Message.Contains("dynamic object") ? "slots"
                        : e.Message.Contains("registration slot") ? "model-slots" : "error";
                    cells.Add(new(dest.Key, v.ClassId, v.SourceLevelKey, v.SourceRow, kind, e.Message, null, null));
                }
            }
        }
        return cells;
    }
}
