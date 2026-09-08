using Spyro.Editor.Core.Exporting;

internal static class ShapeCases
{
    internal static void Free(Action<bool, string> check)
    {
        foreach (int scene in new[] { 0x2800, 0x3000 })
        {
            NativeId65FreshDiscPlan.RequireCurrentUntexturedShape(0xA38, 0x1000, 0xDE000, 0xF800, scene);
            NativeId65FreshDiscPlan.RequireCurrentTexturedShape(NativeId65NormalResourceProfile.DirectLqTwoStaticMaterials,
                0xA38, 0x1000, 0xDE000, 0xF800, scene);
            check(true, "Both neutral shapes can share A38; neither scalar call issues native/source authority.");
            Reject(() => NativeId65FreshDiscPlan.RequireCurrentTexturedShape(
                NativeId65NormalResourceProfile.UntexturedGouraudTwoTriangles, 0xA38, 0x1000, 0xDE000, 0xF800, scene), check,
                "Equal used length never substitutes the actual textured revision discriminator.");
        }
        foreach (int profile in new[] { -1, 2, int.MaxValue })
            Reject(() => NativeId65FreshDiscPlan.RequireCurrentTexturedShape((NativeId65NormalResourceProfile)profile,
                0xA38, 0x1000, 0xDE000, 0xF800, 0x3000), check, "Unknown profile cannot enter the closed shape.");
        int[] valid = [0xA38, 0x1000, 0xDE000, 0xF800, 0x3000];
        for (int field = 0; field < valid.Length; field++)
        foreach (int value in new[] { -1, 0, valid[field]-1, valid[field]+1, int.MaxValue })
        {
            int[] changed = valid.ToArray(); changed[field] = value;
            Reject(() => NativeId65FreshDiscPlan.RequireCurrentTexturedShape(NativeId65NormalResourceProfile.DirectLqTwoStaticMaterials,
                changed[0], changed[1], changed[2], changed[3], changed[4]), check,
                "No shape neighbor, overflow or new allocation is silently admitted.");
        }
        Reject(() => NativeId65FreshDiscPlan.RequireCurrentTexturedShape(NativeId65NormalResourceProfile.DirectLqTwoStaticMaterials,
            0x8C8, 0x1000, 0xDE000, 0xF800, 0x3000), check, "Old used length is not the texture metadata shape.");
    }

    internal static void Reject(Action action, Action<bool, string> check, string text, string? contains = null)
    {
        try { action(); }
        catch (InvalidDataException e) when (contains == null || e.Message.Contains(contains, StringComparison.OrdinalIgnoreCase))
        { check(true, text); return; }
        throw new InvalidDataException("Expected exact refusal: " + text);
    }
}
