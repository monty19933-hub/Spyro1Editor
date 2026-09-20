using System.Reflection;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
using static Program;

internal static class RevisionCases
{
    internal static void Free()
    {
        Type? revision = typeof(NativeId65NormalBodyPlan).Assembly.GetType("Spyro.Editor.Core.Exporting.NativeId65NormalResourceRevision");
        Check(revision is not null && !revision.IsPublic && revision.IsSealed,
            "The shared revision is internal and sealed, not a caller-implemented source-authority interface.");
        Check(revision!.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .All(c => c.IsPrivate), "No caller/Core metadata constructor can manufacture a revision from bytes, hashes or labels.");
        Type? tiledMaterial = revision.Assembly.GetType("Spyro.Editor.Core.Exporting.NativeId65TiledMaterialPlan");
        Check(tiledMaterial is not null && !tiledMaterial.IsPublic && tiledMaterial.IsSealed &&
            tiledMaterial.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).All(c => c.IsPrivate),
            "Tiled materials also require the actual internal sealed private issuer, never caller metadata.");
        MethodInfo[] factories = revision.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(m => m.ReturnType == revision).ToArray();
        Check(factories.Length == 4 && factories.All(m => !m.IsPublic && m.GetParameters().Length == 1) &&
            factories.Where(m => m.Name == "From").Select(m => m.GetParameters()[0].ParameterType).ToHashSet().SetEquals(new[]
            { typeof(NativeId65FreshSceneBundle), typeof(NativeId65FreshTerrainTexturePlan) }) &&
            factories.Count(m => m.Name == "From") == 2 &&
            factories.Single(m => m.Name == "FromTiled").GetParameters()[0].ParameterType == typeof(NativeId65FreshSceneBundle) &&
            factories.Single(m => m.Name == "FromTiledMaterials").GetParameters()[0].ParameterType == tiledMaterial,
            "The four closed legacy/texture/tiled factories accept exact private upstream types, never raw bytes or caller authority.");
        foreach (Type type in new[] { typeof(NativeId65NormalBodyPlan), typeof(NativeId65NormalUploadStaging) })
        {
            Check(type.IsSealed && type.GetConstructors().Length == 0, "Public stage/body remain private source issuers.");
            var profile = type.GetProperty("ResourceProfile");
            Check(profile?.PropertyType == typeof(NativeId65NormalResourceProfile) && profile.GetMethod is not null && profile.SetMethod is null,
                "Instance resource discriminator is read-only and cannot be supplied independently of private construction.");
            Check(type.GetMethods().Count(m => m.Name == "CaptureAsync") == 1 &&
                type.GetMethods().Count(m => m.Name == "CaptureTexturedAsync") == 1,
                "Named textured API avoids ambiguous old null calls and preserves the one original CaptureAsync signature.");
            foreach (MethodInfo method in type.GetMethods().Where(m => m.Name is "CaptureAsync" or "CaptureTexturedAsync"))
                Check(!method.GetParameters().Any(p => p.ParameterType == typeof(NativeStaticTerrainTextureAllocation) ||
                    p.ParameterType == typeof(NativeId65NormalResourceProfile) || p.ParameterType == typeof(byte[])),
                    "Neutral allocation, caller profile and raw byte arrays are not source-issued construction routes.");
        }
        Check(Enum.GetValues<NativeId65NormalResourceProfile>().SequenceEqual(new[]
        { NativeId65NormalResourceProfile.UntexturedGouraudTwoTriangles, NativeId65NormalResourceProfile.DirectLqTwoStaticMaterials,
            NativeId65NormalResourceProfile.UntexturedGouraudFlatTiles,
            NativeId65NormalResourceProfile.DirectLqTwoStaticMaterialsFlatTiles }) &&
            Enum.GetValues<NativeId65NormalResourceProfile>().Select(value => (int)value).SequenceEqual(new[] { 0, 1, 2, 3 }),
            "The discriminator preserves old numeric profiles and appends the separately issued tiled-material variant.");
        Reject(() => NativeId65NormalUploadStaging.CaptureTexturedAsync("must-not-open.bin", null!).GetAwaiter().GetResult(),
            "Missing exact texture revision refuses before source I/O; FileNotFound cannot satisfy this test.");
        Reject(() => NativeId65NormalBodyPlan.CaptureTexturedAsync("must-not-open.bin", null!, null!).GetAwaiter().GetResult(),
            "Missing exact texture/staging pair refuses before original source I/O.");
        // Keep the legacy call forms unchanged: no casts are needed to select
        // them after the new named API exists.
        Reject(() => NativeId65NormalUploadStaging.CaptureAsync("must-not-open.bin", null!).GetAwaiter().GetResult(),
            "Old source-null call remains unambiguous and refuses before I/O.");
        Reject(() => NativeId65NormalBodyPlan.CaptureAsync("must-not-open.bin", null!, null!).GetAwaiter().GetResult(),
            "Old body source-null call remains unambiguous and refuses before I/O.");
    }
}
