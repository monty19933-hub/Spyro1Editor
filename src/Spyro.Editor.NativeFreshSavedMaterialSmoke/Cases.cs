using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Persistence;
using Spyro.Editor.Core.Primitives;

internal static class Cases
{
    internal const string Missing = "saved-material-must-not-open-original.bin";
    internal const string Original = "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37";
    internal const string Variant = "spyro1-usa-retail-mode2-2352-v1";
    internal const string LockName = ".native-fresh-level.writer.lock";
    internal static string Root = "";
    internal static int Checks, Refusals;

    internal static NativeId65FreshDocumentIntent Base(int heading = 64, int music = 19) =>
        NativeId65FreshDocumentIntent.Create(8448, 8448, 8704, 1024, 1024, 8960, 8960,
            heading, new ColorRgba(80, 128, 112), new ColorRgba(128, 96, 64), music);
    internal static NativeFreshRetailTerrainMaterialIntent Retail(int entry, int id) =>
        NativeFreshFloorMaterialIntent.RetailTerrain(Variant, entry, id);
    internal static NativeFreshLevelDocumentIntent Document(int firstEntry = 14, int firstId = 1,
        int secondEntry = 70, int secondId = 1, int heading = 64, int music = 19) =>
        NativeFreshLevelDocumentIntent.Create(Base(heading, music), [])
            .WithFloorMaterials(NativeFreshFloorMaterialsIntent.Create(Retail(firstEntry, firstId), Retail(secondEntry, secondId)));
    internal static (EditorProjectLayout Project, NativeFreshSavedLevelDocumentReadContext Saved) Save(
        string label, NativeFreshLevelDocumentIntent? document = null)
    {
        string root = Path.Combine(Root, label + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var project = new EditorProjectLayout(root, "saved-material-smoke");
        File.WriteAllText(project.ManifestPath, "{\"schemaVersion\":1,\"projectId\":\"saved-material-smoke\",\"unrelated\":true}");
        var saved = NativeFreshLevelDocumentStore.Save(NativeFreshLevelDocumentStore.Capture(project), document ?? Document());
        return (project, NativeFreshSavedLevelDocumentReadContext.Capture(saved));
    }
    internal static void Check(bool condition, string message)
    { if (!condition) throw new InvalidDataException(message); Checks++; }
    internal static async Task<T> Throws<T>(Func<Task> action, string? contains = null) where T : Exception
    {
        try { await action(); }
        catch (T error)
        {
            Check(contains == null || error.Message.Contains(contains, StringComparison.OrdinalIgnoreCase), "Exact refusal boundary: " + contains);
            Refusals++; return error;
        }
        throw new InvalidDataException("Expected " + typeof(T).Name);
    }
    internal static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    internal static List<Exception> Leaves(Exception error) => error is AggregateException group
        ? group.InnerExceptions.SelectMany(Leaves).ToList()
        : error is IOException && error.InnerException is AggregateException nested ? Leaves(nested) : [error];
    internal static void RequireOriginal(Stream source)
    {
        Check(source.CanRead && source.CanSeek && source.Length == 661547040, "Whole original source capability and length.");
        source.Position = 0;
        Check(Convert.ToHexStringLower(SHA256.HashData(source)) == Original && source.Position == 661547040 &&
            source.ReadByte() == -1 && source.Position == 661547040 && source.Length == 661547040, "Whole original SHA and true EOF.");
    }
    internal static void ThrowFailures(List<Exception> errors)
    {
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new IOException("Smoke plus final original errors.", new AggregateException(errors));
    }
}
