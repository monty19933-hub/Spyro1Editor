using Spyro.Editor.Core.Cache;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Workspace;

string? workspaceArg = null;
string? discArg = null;
bool force = false;

for (int i = 0; i < args.Length; i++)
{
    string arg = args[i];
    if (arg.Equals("--force", StringComparison.OrdinalIgnoreCase))
    {
        force = true;
        continue;
    }
    if ((arg.Equals("--disc", StringComparison.OrdinalIgnoreCase) || arg.Equals("--image", StringComparison.OrdinalIgnoreCase) || arg.Equals("--cue", StringComparison.OrdinalIgnoreCase))
        && i + 1 < args.Length)
    {
        discArg = args[++i];
        continue;
    }
    if (workspaceArg == null)
    {
        workspaceArg = arg;
        continue;
    }
    if (discArg == null)
        discArg = arg;
}

EditorWorkspace workspace = EditorWorkspace.Find(workspaceArg);
Console.WriteLine($"Workspace: {workspace.RootPath}");

if (!string.IsNullOrWhiteSpace(discArg))
{
    DiscImageSelection selection = DiscImageLocator.ConfigureImage(workspace, discArg);
    Console.WriteLine($"Selected disc image: {selection.ImagePath}");
    if (!selection.ImageExists)
    {
        Console.Error.WriteLine($"The selected BIN was not found. Keep the CUE and BIN in the same folder. Selected: {discArg}");
        return 2;
    }
}

string sourceImage = DiscImageLocator.FindImage(workspace);
Console.WriteLine(File.Exists(sourceImage)
    ? $"Source disc: {sourceImage}"
    : $"Source disc: not found. Use Open BIN/CUE in the editor first, or run this tool with --disc \"path-to-spyro.cue\".");
if (!File.Exists(sourceImage))
    return 2;

LevelCatalog catalog = LevelCatalog.Load(workspace.RootPath);
if (catalog.Levels.Count == 0)
{
    Console.Error.WriteLine("Missing spyro-level-catalog.json in the editor folder or support folder.");
    return 2;
}

PortableEditorCacheResult result = await PortableEditorCacheBuilder.BuildAsync(workspace, catalog, overwrite: force, fastReuseExistingCache: !force);
Console.WriteLine(result.ReusedExistingCache
    ? $"Cache reused: {result.MobyCacheCount}/{result.LevelCount} object maps, {result.OverlayCacheCount}/{result.LevelCount} terrain maps."
    : $"Cache built: {result.MobyCacheCount}/{result.LevelCount} object maps, {result.OverlayCacheCount}/{result.LevelCount} terrain maps.");
Console.WriteLine($"Cache folder: {result.CachePath}");

int loadedMobyFiles = 0;
int loadedMobys = 0;
foreach (LevelDefinition level in catalog.Levels)
{
    string mobyPath = Path.Combine(result.CachePath, $"{level.Key}-mobys.json");
    if (!File.Exists(mobyPath))
        continue;

    int count = MobyLoader.LoadCached(mobyPath).Count;
    loadedMobyFiles++;
    loadedMobys += count;
    Console.WriteLine($"  {level.DisplayName}: {count} objects");
}

if (loadedMobyFiles == 0 || loadedMobys == 0)
{
    Console.Error.WriteLine("No object maps loaded from the generated cache. The selected disc may not match the supported Spyro the Dragon layout.");
    return 3;
}

return 0;
