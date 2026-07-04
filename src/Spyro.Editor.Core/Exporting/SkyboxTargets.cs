namespace Spyro.Editor.Core.Exporting;

internal static class SkyboxTargets
{
    public static IReadOnlyList<SkyboxTargetRecord> ForPreset(string presetId)
    {
        return presetId switch
        {
            "Custom" or "StoneHillNight" => TargetRecords,
            "StoneHillStableNight" => TargetDeepNight,
            "StoneHillBestNight" => FullNight().Concat(TargetBaseSkyLoaded).Concat(TargetBaseSkyPortal).Concat(TargetLoadedPeriwinkleA).ToArray(),
            "StoneHillNightKeeper" => FullNight()
                .Concat(TargetBaseSkyLoaded)
                .Concat(TargetBaseSkyPortal)
                .Concat(TargetLoadedPeriwinkleA)
                .Concat(TargetBestWaterABCDeep)
                .Concat(TargetFlyInPeriwinkleADeep)
                .ToArray(),
            _ => throw new InvalidOperationException($"Unsupported native skybox preset: {presetId}")
        };
    }

    private static SkyboxTargetRecord R(string label, int wordCount, long? portal = null, long? loaded = null, string rgbHex = "")
    {
        return new SkyboxTargetRecord(label, wordCount, portal, loaded, rgbHex);
    }

    private static IReadOnlyList<SkyboxTargetRecord> TargetRecords { get; } =
    [
        R("best22-a", 22, 0x74B8C, 0x91350),
        R("best22-b", 22, 0x78670, 0x94E34),
        R("best22-c", 22, 0x79AA8, 0x9626C)
    ];

    private static IReadOnlyList<SkyboxTargetRecord> TargetExtras { get; } =
    [
        R("anchor19-a", 19, 0x73638, 0x8FDFC),
        R("anchor19-b", 19, 0x74378, 0x90B3C),
        R("anchor19-c", 19, 0x76DD8, 0x9359C)
    ];

    private static IReadOnlyList<SkyboxTargetRecord> TargetDeepNight { get; } = TargetExtras
        .Concat(TargetRecords)
        .Concat([
            R("upper13", 7, 0x78338, 0x94AFC),
            R("upper15", 7, 0x78DE0, 0x955A4)
        ])
        .ToArray();

    private static IReadOnlyList<SkyboxTargetRecord> TargetFlyInSlabs { get; } =
    [
        R("flyin-wedge-6a19c", 2, 0x6A19C),
        R("flyin-wedge-6adc8", 2, 0x6ADC8),
        R("flyin-wedge-6b3fc", 3, 0x6B3FC),
        R("flyin-slab-5de90", 1, 0x5DE90),
        R("flyin-slab-5de98", 3, 0x5DE98),
        R("flyin-slab-5df0c", 5, 0x5DF0C),
        R("flyin-slab-5f120", 2, 0x5F120),
        R("flyin-slab-5f12c", 1, 0x5F12C),
        R("flyin-triangle-5f13c", 1, 0x5F13C),
        R("flyin-slab-5f248", 1, 0x5F248),
        R("flyin-slab-5f250", 3, 0x5F250),
        R("flyin-slab-5f2c4", 6, 0x5F2C4),
        R("flyin-triangle-5f2dc", 1, 0x5F2DC),
        R("flyin-slab-5f38c", 5, 0x5F38C),
        R("flyin-triangle-5f3a8", 1, 0x5F3A8),
        R("flyin-slab-5f3ac", 1, 0x5F3AC),
        R("flyin-slab-5f66c", 1, 0x5F66C),
        R("flyin-slab-5f674", 1, 0x5F674),
        R("flyin-slab-5fa30", 1, 0x5FA30),
        R("flyin-slab-5fa38", 1, 0x5FA38),
        R("flyin-slab-5faa4", 1, 0x5FAA4),
        R("flyin-slab-5faac", 5, 0x5FAAC),
        R("flyin-slab-5fdd0", 1, 0x5FDD0),
        R("flyin-slab-5fdd8", 4, 0x5FDD8)
    ];

    private static IReadOnlyList<SkyboxTargetRecord> TargetFlyInTriangles { get; } =
    [
        R("flyin-triangle-58ab8", 1, 0x58AB8),
        R("flyin-triangle-591c8", 1, 0x591C8),
        R("flyin-triangle-6a720", 1, 0x6A720),
        R("flyin-triangle-6ba28", 1, 0x6BA28),
        R("flyin-triangle-6c0d4", 1, 0x6C0D4),
        R("flyin-triangle-6c5e4", 1, 0x6C5E4),
        R("flyin-triangle-6ced4", 1, 0x6CED4),
        R("flyin-triangle-6d524", 1, 0x6D524),
        R("flyin-triangle-6da0c", 1, 0x6DA0C),
        R("flyin-triangle-6de6c", 1, 0x6DE6C),
        R("flyin-triangle-6e25c", 1, 0x6E25C),
        R("flyin-triangle-6e9e0", 1, 0x6E9E0),
        R("flyin-triangle-6f070", 1, 0x6F070),
        R("flyin-triangle-6f630", 1, 0x6F630),
        R("flyin-triangle-6fcfc", 1, 0x6FCFC),
        R("flyin-triangle-70370", 1, 0x70370),
        R("flyin-triangle-70d4c", 1, 0x70D4C),
        R("flyin-triangle-716fc", 1, 0x716FC)
    ];

    private static IReadOnlyList<SkyboxTargetRecord> TargetSpotCleanup { get; } =
    [
        R("flyin-spot-5f128", 1, 0x5F128),
        R("flyin-spot-5f130", 2, 0x5F130),
        R("flyin-spot-5f670", 1, 0x5F670),
        R("flyin-spot-5f678", 2, 0x5F678),
        R("flyin-spot-6a18c", 1, 0x6A18C),
        R("flyin-spot-6a724", 1, 0x6A724),
        R("flyin-spot-6a72c", 1, 0x6A72C),
        R("flyin-spot-6de5c", 2, 0x6DE5C),
        R("loaded-spot-7b9bc", 1, loaded: 0x7B9BC),
        R("loaded-spot-7d294", 1, loaded: 0x7D294),
        R("loaded-spot-81c50", 1, loaded: 0x81C50),
        R("loaded-spot-8bd40", 1, loaded: 0x8BD40),
        R("loaded-spot-8c418", 1, loaded: 0x8C418),
        R("loaded-spot-8c574", 1, loaded: 0x8C574),
        R("loaded-spot-8c6a4", 2, loaded: 0x8C6A4),
        R("loaded-spot-8cbd0", 1, loaded: 0x8CBD0),
        R("loaded-spot-93540", 1, loaded: 0x93540),
        R("loaded-spot-95e40", 1, loaded: 0x95E40),
        R("loaded-spot-97854", 1, loaded: 0x97854)
    ];

    private static IReadOnlyList<SkyboxTargetRecord> TargetRemainingTriangles { get; } =
    [
        R("flyin-final-58c40", 1, 0x58C40),
        R("flyin-final-59c3c", 1, 0x59C3C),
        R("flyin-final-5abe4", 2, 0x5ABE4),
        R("flyin-final-5c3c4", 2, 0x5C3C4),
        R("flyin-final-5df24", 1, 0x5DF24),
        R("flyin-final-5e93c", 2, 0x5E93C),
        R("loaded-final-72e30", 1, loaded: 0x72E30),
        R("loaded-final-825c4", 1, loaded: 0x825C4),
        R("loaded-final-8573c", 1, loaded: 0x8573C),
        R("loaded-final-8a2e4", 1, loaded: 0x8A2E4),
        R("loaded-final-91af8", 1, loaded: 0x91AF8),
        R("loaded-final-92548", 1, loaded: 0x92548),
        R("loaded-final-92f00", 1, loaded: 0x92F00),
        R("loaded-final-92f24", 1, loaded: 0x92F24),
        R("loaded-final-934d8", 1, loaded: 0x934D8),
        R("loaded-final-93ad4", 1, loaded: 0x93AD4),
        R("loaded-final-93e38", 1, loaded: 0x93E38),
        R("loaded-final-93ef4", 1, loaded: 0x93EF4),
        R("loaded-final-9440c", 1, loaded: 0x9440C),
        R("loaded-final-962f4", 1, loaded: 0x962F4)
    ];

    private static IReadOnlyList<SkyboxTargetRecord> TargetBaseSkyLoaded { get; } =
    [
        R("base-loaded-0f18", 56, loaded: 0x0F18),
        R("base-loaded-1260", 33, loaded: 0x1260),
        R("base-loaded-1500", 20, loaded: 0x1500)
    ];

    private static IReadOnlyList<SkyboxTargetRecord> TargetBaseSkyPortal { get; } =
    [
        R("base-portal-0c30", 20, 0x0C30),
        R("base-portal-1170", 20, 0x1170),
        R("base-portal-2130", 20, 0x2130),
        R("base-portal-23d0", 20, 0x23D0),
        R("base-portal-2670", 20, 0x2670),
        R("base-portal-2910", 20, 0x2910),
        R("base-portal-2bb0", 20, 0x2BB0)
    ];

    private static IReadOnlyList<SkyboxTargetRecord> TargetLoadedPeriwinkleA { get; } =
    [
        R("loaded-peri-a-8fb98", 1, loaded: 0x8FB98)
    ];

    private static IReadOnlyList<SkyboxTargetRecord> TargetBestWaterABCDeep { get; } =
    [
        R("best-water-a-0ae80", 2, loaded: 0xAE80, rgbHex: "#061B3D"),
        R("best-water-a-0aeb0", 2, loaded: 0xAEB0, rgbHex: "#061B3D"),
        R("best-water-a-0b8b0", 2, loaded: 0xB8B0, rgbHex: "#061B3D"),
        R("best-water-b-21328", 3, loaded: 0x21328, rgbHex: "#061B3D"),
        R("best-water-b-215dc", 2, loaded: 0x215DC, rgbHex: "#061B3D"),
        R("best-water-b-269c4", 2, loaded: 0x269C4, rgbHex: "#061B3D"),
        R("best-water-c-2ec9c", 2, loaded: 0x2EC9C, rgbHex: "#061B3D"),
        R("best-water-c-3c1b4", 2, loaded: 0x3C1B4, rgbHex: "#061B3D"),
        R("best-water-c-409d4", 2, loaded: 0x409D4, rgbHex: "#061B3D")
    ];

    private static IReadOnlyList<SkyboxTargetRecord> TargetFlyInPeriwinkleADeep { get; } =
    [
        R("flyin-peri-a-739d4", 1, 0x739D4, rgbHex: "#061B3D")
    ];

    private static IReadOnlyList<SkyboxTargetRecord> FullNight()
    {
        SkyboxTargetRecord[] main =
        [
            R("portal-bg-00", 7, 0x72060),
            R("portal-bg-01", 4, 0x722C4),
            R("portal-bg-02", 4, 0x724D0),
            R("portal-bg-03", 4, 0x72674),
            R("portal-bg-04", 5, 0x72860),
            R("portal-bg-05", 6, 0x72B38),
            R("portal-bg-06", 7, 0x72CE8),
            R("portal-bg-07", 5, 0x72E94),
            R("portal-bg-08", 6, 0x73168),
            R("cloud-73d6c", 17, 0x73D6C, 0x90530),
            R("cloud-75350", 21, 0x75350, 0x91B14),
            R("cloud-75890", 17, 0x75890, 0x92054),
            R("cloud-75eec", 25, 0x75EEC, 0x926B0),
            R("cloud-76798", 20, 0x76798, 0x92F5C),
            R("cloud-77368", 18, 0x77368, 0x93B2C),
            R("cloud-77788", 20, 0x77788, 0x93F4C),
            R("blue-77c64", 6, 0x77C64, 0x94428),
            R("cloud-77e60", 16, 0x77E60, 0x94624),
            R("blue-78ed8", 5, 0x78ED8, 0x9569C),
            R("cloud-79090", 18, 0x79090, 0x95854),
            R("blue-79418", 6, 0x79418, 0x95BDC),
            R("blue-79550", 10, 0x79550, 0x95D14),
            R("backdrop-79694", 7, 0x79694, 0x95E58),
            R("wedge-797f4", 2, 0x797F4, 0x95FB8),
            R("bright-7a284", 5, 0x7A284, 0x96A48),
            R("blue-7a518", 4, 0x7A518, 0x96CDC),
            R("wedge-7a5f4", 2, 0x7A5F4, 0x96DB8),
            R("cloud-7a86c", 16, 0x7A86C, 0x97030),
            R("blue-7aed0", 7, 0x7AED0, 0x97694),
            R("wedge-7af68", 2, 0x7AF68, 0x9772C),
            R("wedge-7afe0", 2, 0x7AFE0, 0x977A4),
            R("blue-7b168", 9, 0x7B168, 0x9792C),
            R("blue-7b2e8", 8, 0x7B2E8, 0x97AAC),
            R("bright-7b4b4", 5, 0x7B4B4, 0x97C78),
            R("blue-7b708", 5, 0x7B708, 0x97ECC)
        ];

        return main
            .Concat(TargetFlyInSlabs)
            .Concat(TargetFlyInTriangles)
            .Concat(TargetSpotCleanup)
            .Concat(TargetRemainingTriangles)
            .Concat(TargetDeepNight)
            .ToArray();
    }
}
