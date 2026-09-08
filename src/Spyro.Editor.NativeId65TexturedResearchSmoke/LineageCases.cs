using Spyro.Editor.Core.Exporting;
using static Inspection;

internal static class LineageCases
{
    internal static void Free()
    {
        foreach (var type in new[] { Type("NativeId65NormalResourceRevision"), typeof(NativeId65NormalBodyPlan) })
        {
            var p = Property(type, "TextureAncestor");
            Check(p.PropertyType == typeof(NativeId65FreshTerrainTexturePlan) && p.SetMethod == null &&
                p.GetMethod is { IsAssembly: true, IsStatic: false }, "Exact internal readonly actual Texture ancestor, no public capability.");
        }
        Check(typeof(NativeId65NormalBodyPlan).GetConstructors().Length == 0, "Body retains private issuance.");
        Check(typeof(NativeId65NormalBodyPlan).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(m => m.ReturnType == typeof(Task<NativeId65NormalBodyPlan>)).Select(m => m.Name).Order().SequenceEqual(new[] { "CaptureAsync", "CaptureTexturedAsync" }),
            "Existing Body factories are not widened by reference retention.");
    }

    internal static async Task Source(string path, NativeId65FreshDocumentCompilation compilation)
    {
        var fresh = Get<NativeId65FreshSceneBundle>(compilation, "FreshResourceAncestor");
        var old = Get<NativeId65NormalBodyPlan>(compilation.DiscPlan, "BodyAncestor");
        object revision = Call(null, Type("NativeId65NormalResourceRevision"), "From", fresh)!;
        Check(Get<NativeId65FreshTerrainTexturePlan?>(revision, "TextureAncestor") == null &&
            Get<NativeId65FreshTerrainTexturePlan?>(old, "TextureAncestor") == null, "Actual untextured revision/body retain null.");
        Check(old.PlanSha256 == "48f1f0b4f7740c8a6277a4b440990e1fd26cb609db2153c3c24fc61d70dbbb94" &&
            old.LevelDataSha256 == "b4abbdd09f794510d9b9341d7c7111ddd0b9068411bab30e70e7477b4f8c096c", "Original untextured body bytes/Plan unchanged.");
        var dark = NativeStaticTerrainTextureSource.Capture(path, 14, 0);
        var gnasty = NativeStaticTerrainTextureSource.Capture(path, 70, 0);
        var repeatedSource = NativeStaticTerrainTextureSource.Capture(path, 14, 1);
        var fixedTexture = await NativeId65FreshTerrainTexturePlan.CaptureAsync(path, fresh, dark, gnasty);
        var selected = await NativeId65FreshTerrainTexturePlan.CaptureSelectedAsync(path, fresh, dark, gnasty);
        var sameSource = await NativeId65FreshTerrainTexturePlan.CaptureSelectedAsync(path, fresh, repeatedSource, repeatedSource);
        foreach (var texture in new[] { fixedTexture, selected, sameSource })
        {
            var stage = await NativeId65NormalUploadStaging.CaptureTexturedAsync(path, texture);
            var body = await NativeId65NormalBodyPlan.CaptureTexturedAsync(path, texture, stage);
            revision = Call(null, Type("NativeId65NormalResourceRevision"), "From", texture)!;
            Check(ReferenceEquals(Get<object>(revision, "TextureAncestor"), texture) &&
                ReferenceEquals(Get<object>(body, "TextureAncestor"), texture), "Revision/Body retain the exact publicly issued Texture object.");
            var disc = await NativeId65FreshDiscPlan.CaptureTexturedAsync(path, body, 19);
            Check(ReferenceEquals(Get<object>(disc, "BodyAncestor"), body) &&
                ReferenceEquals(texture.MaterialSources[0], texture == sameSource ? repeatedSource : dark) &&
                ReferenceEquals(texture.MaterialSources[1], texture == sameSource ? repeatedSource : gnasty),
                "Disc→Body→Texture and both ordered source references stay actual; same source twice is valid.");
            Call(texture, "RequireAncestry", fresh, texture.MaterialSources[0], texture.MaterialSources[1]);
            Call(disc, "RequireAncestry", body);
            if (ReferenceEquals(texture, fixedTexture))
                Check(body.LevelDataSha256 == "347ab532881afb3975088e0872792192d8567b0abe05e7708f271741b9568cdd" &&
                    disc.PlanSha256 == "b037b67223806a8dfd6844be35a5564079e3aa462336d718c67eae6855385fcf",
                    "Fixed-a independent body and pre-retention Disc serialization remain exact.");
            if (ReferenceEquals(texture, selected))
            {
                var darkAgain = NativeStaticTerrainTextureSource.Capture(path, 14, 0);
                var equal = await NativeId65FreshTerrainTexturePlan.CaptureSelectedAsync(path, fresh, darkAgain, gnasty);
                Check(!ReferenceEquals(equal, selected) && equal.PlanSha256 == selected.PlanSha256, "Genuine equal Texture reissuance.");
                var equivalentBody = await NativeId65NormalBodyPlan.CaptureTexturedAsync(path, equal, stage);
                Check(equivalentBody.PlanSha256 == body.PlanSha256 && equivalentBody.CopyLevelData().SequenceEqual(body.CopyLevelData()) &&
                    ReferenceEquals(Get<object>(equivalentBody, "TextureAncestor"), equal),
                    "Existing body/staging hash-equivalent pairing stays accepted while each Body records its own Texture.");
                Refuse<InvalidDataException>(() => Call(disc, "RequireAncestry", equivalentBody), "New Disc ref cannot be replaced by equal body.");
                Refuse<InvalidDataException>(() => Call(selected, "RequireAncestry", fresh, darkAgain, gnasty), "Equal source handle is not retained reference.");
                var own = await NativeId65FreshDiscPlan.CaptureTexturedAsync(path, equivalentBody, 19);
                Check(own.PlanSha256 == disc.PlanSha256, "Equivalent body may still issue its own identical Disc.");
                var reversed = await NativeId65FreshTerrainTexturePlan.CaptureSelectedAsync(path, fresh, gnasty, dark);
                await RefuseAsync<InvalidDataException>(() => NativeId65NormalBodyPlan.CaptureTexturedAsync("must-not-open", reversed, stage),
                    "Actually different revision still refuses old pairing before source I/O.");
            }
        }
    }
}
