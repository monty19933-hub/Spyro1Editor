using System.Text.Json;
using System.Text.Json.Serialization;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Levels;

// Read-only. Output goes to stdout; this tool cannot create a BIN, alter a cache,
// update an approval or infer an instance template from an unplaced resource.
if (args.Length is < 2 or > 4 || args.Length == 3 && args[2] is not ("--json" or "--matrix") ||
    args.Length == 4 && args[2] != "--target")
{
    Console.Error.WriteLine("Usage: NativeMobyInventoryTool <original.bin> <catalog-workspace> [--json | --matrix | --target <level-key>]");
    return 2;
}
try
{
    var inventory = NativeMobySourceInventory.Capture(args[0], LevelCatalog.Load(args[1]));
    var json = new JsonSerializerOptions { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
    if (args.Length == 3 && args[2] == "--matrix")
        Console.WriteLine(JsonSerializer.Serialize(NativeMobyPortabilityMatrix.Create(inventory), json));
    else if (args.Length == 3) Console.WriteLine(JsonSerializer.Serialize(inventory, json));
    else if (args.Length == 4)
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            inventory.SourceImageSha256,
            inventory.Scope,
            Target = LevelCatalog.NormalizeKey(args[3]),
            Classes = inventory.Classes.Select(c => c.ActorId).Concat(inventory.SharedModels.Registrations.Select(r => r.ActorId))
                .Distinct().Order().Select(id => inventory.DescribeDestination(id, args[3])).ToArray()
        }, json));
    else
    {
        Console.WriteLine($"Native source discovery: {inventory.Levels.Count} levels, {inventory.Levels.Sum(l => l.Rows.Count)} placed rows, " +
            $"{inventory.Classes.Count} placed/locally registered classes.");
        Console.WriteLine($"{inventory.Classes.Count(c => c.PlacedRowCount == 0)} classes have a local registration but no placed donor row.");
        Console.WriteLine($"{inventory.SharedModels.Registrations.Count} shared PETE model roots are also available for inspection; none are placed templates.");
        Console.WriteLine(inventory.Scope);
        foreach (var level in inventory.Levels)
            Console.WriteLine($"{level.LevelKey}: {level.Rows.Count} rows, {level.Registrations.Count} local roots, " +
                $"{level.Rows.Count(r => !r.HasLocalModelRegistration)} rows without a local root; import approval: none from this inventory.");
    }
    return 0;
}
catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException)
{
    Console.Error.WriteLine($"Native inventory refused: {error.Message}");
    return 1;
}
