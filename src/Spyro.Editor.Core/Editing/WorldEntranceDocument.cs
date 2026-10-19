using System.Text.Json;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.Editing;

/// <summary>The project's way into its new world (<see cref="NewWorldDocument"/>): a doorway placed in one of the game's own
/// homeworlds that leads to the world's homeworld (70). Saved as its own small file next to <c>new-world.json</c>
/// (<see cref="FileName"/>), so projects without one, and older editors, are unaffected. Create BIN builds it into that
/// homeworld with <see cref="NativeWorldEntrance"/> when it is the world's <see cref="NewWorldDocument.EntryHomeworldKey"/>.</summary>
/// <param name="Position">The bottom middle of the opening, editor units.</param>
/// <param name="Heading">The way Spyro walks in, 0-255 (0 = east, 64 = north).</param>
/// <param name="Shape">A <see cref="DoorwayShapes"/> id.</param>
/// <param name="Scale"><see cref="DoorwayShapes.MinScale"/>..<see cref="DoorwayShapes.MaxScale"/>.</param>
public sealed record WorldEntranceDocument(int SchemaVersion, string HomeworldKey, Vector3f Position, int Heading, string Shape, float Scale)
{
    public const string FileName = "world-entrance.json";

    /// <summary>The homeworld's texture for the frame's front and back (its material number); -1: its own arches' texture.</summary>
    public int FaceTexture { get; init; } = -1;
    /// <summary>The texture of the frame's sides, top and inside; -1: as the front.</summary>
    public int SideTexture { get; init; } = -1;
    /// <summary>Spyro bumps into the frame.</summary>
    public bool SolidFrame { get; init; } = true;

    public void Validate()
    {
        if (SchemaVersion != 1 || !NewWorldDocument.HomeworldKeys.Contains(HomeworldKey) || Heading is < 0 or > 255 || DoorwayShapes.Find(Shape) == null ||
            Scale < DoorwayShapes.MinScale - 1e-4 || Scale > DoorwayShapes.MaxScale + 1e-4 || FaceTexture is < -1 or > 127 || SideTexture is < -1 or > 127 ||
            float.IsNaN(Position.X) || float.IsNaN(Position.Y) || float.IsNaN(Position.Z))
            throw new InvalidDataException("The project's New World entrance is not valid: one of the six homeworlds, a heading 0-255, a doorway shape and a size of 75-150%.");
    }

    /// <summary>What Create BIN builds.</summary>
    public WorldEntranceSpec Spec() => new(HomeworldKey, Position, Heading, Shape, Scale) { FaceTexture = FaceTexture, SideTexture = SideTexture, SolidFrame = SolidFrame };

    public static string PathFor(string workspace) => Path.Combine(workspace, FileName);

    public static WorldEntranceDocument? Read(string workspace)
    {
        string path = PathFor(workspace);
        if (!File.Exists(path)) return null;
        var entrance = JsonSerializer.Deserialize<WorldEntranceDocument>(File.ReadAllText(path), Json) ?? throw new InvalidDataException("The New World entrance file is empty.");
        entrance.Validate();
        return entrance;
    }

    public async Task SaveAsync(string workspace)
    {
        Validate();
        string path = PathFor(workspace), temporary = path + ".tmp";
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(this, Json));
        File.Move(temporary, path, overwrite: true);
    }

    public static void Delete(string workspace)
    {
        string path = PathFor(workspace);
        if (File.Exists(path)) File.Delete(path);
    }

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
}
