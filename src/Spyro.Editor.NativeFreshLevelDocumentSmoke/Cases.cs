using System.Text;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Primitives;

internal static class Cases
{
    internal const string Id = "45db7a08fe7049709a4d0b9dc59aca51";
    internal const string Id2 = "00000000000000000000000000000001";
    internal const string BaseJson = "{\"kind\":\"spyro-native-fresh-intent\",\"schema\":1,\"floor\":{\"x\":8448,\"y\":8448,\"z\":8704,\"width\":1024,\"depth\":1024},\"spawn\":{\"x\":8960,\"y\":8960,\"heading\":64},\"display\":{\"floorRgb\":[80,128,112],\"skyRgb\":[128,96,64]},\"musicTrackId\":19}";
    internal const string PlacementJson = "{\"id\":\"45db7a08fe7049709a4d0b9dc59aca51\",\"tag\":\"wizard-route\",\"origin\":{\"x\":8704,\"y\":8960,\"z\":8909},\"routeEnd\":{\"x\":9216,\"y\":8960}}";
    internal const string Canonical = "{\"kind\":\"spyro-native-fresh-level\",\"schema\":1,\"base\":" + BaseJson + ",\"placements\":[" + PlacementJson + "]}";
    internal static int Checks, Refusals;
    internal static NativeId65FreshDocumentIntent Base(int track = 19) => NativeId65FreshDocumentIntent.Create(
        8448, 8448, 8704, 1024, 1024, 8960, 8960, 64, new ColorRgba(80, 128, 112), new ColorRgba(128, 96, 64), track);
    internal static NativeFreshWizardRouteIntent Wizard(string id = Id, int x = 8704, int y = 8960,
        int z = 8909, int ex = 9216, int ey = 8960) => NativeFreshWizardRouteIntent.Create(id, x, y, z, ex, ey);
    internal static NativeFreshLevelDocumentIntent Document(params NativeFreshPlacementIntent[] values) => NativeFreshLevelDocumentIntent.Create(Base(), values);
    internal static NativeFreshLevelDocumentIntent Parse(string json) => NativeFreshLevelDocumentCodec.Parse(Encoding.UTF8.GetBytes(json));
    internal static string Serialize(NativeFreshLevelDocumentIntent value) => Encoding.UTF8.GetString(NativeFreshLevelDocumentCodec.SerializeCanonical(value));
    internal static void Check(bool value, string label)
    { if (!value) throw new InvalidDataException(label); Checks++; }
    internal static void Reject(Action action, string label)
    {
        try { action(); }
        catch (Exception error) when (error is InvalidDataException or ArgumentException) { Checks++; Refusals++; return; }
        throw new InvalidDataException("Expected data/argument refusal: " + label);
    }
}
