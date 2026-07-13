using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Exporting;

public static class LifeChestRuntimeLayout
{
    // g_Buffers.m_LevelScene for each USA level containing a native Life Chest.
    // Values are derived from the overlay copy buffer, LoadLevelData's parsed end,
    // and model-data size; nine clean RAM captures independently match the result.
    private static readonly IReadOnlyDictionary<string, uint> RuntimeBases =
        new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase)
        {
            ["artisans"] = 0x8016313C,
            ["stonehill"] = 0x80173320,
            ["darkhollow"] = 0x801284E4,
            ["townsquare"] = 0x8017B3D4,
            ["peacekeepers"] = 0x80173F90,
            ["drycanyon"] = 0x80164E34,
            ["clifftown"] = 0x8016C388,
            ["icecavern"] = 0x80166C0C,
            ["magiccrafters"] = 0x801731B4,
            ["wizardpeak"] = 0x80170574,
            ["beastmakers"] = 0x80178C4C,
            ["terracevillage"] = 0x801672A4,
            ["mistybog"] = 0x801758BC,
            ["treetops"] = 0x8016E4F4,
            ["metalhead"] = 0x80166940,
            ["dreamweavers"] = 0x80171450,
            ["darkpassage"] = 0x801740F0,
            ["loftycastle"] = 0x80159EE8,
            ["hauntedtowers"] = 0x80175034,
            ["jacques"] = 0x801644F0,
            ["gnorccove"] = 0x801730B4,
            ["twilightharbor"] = 0x8016F0F4
        };

    public static IEnumerable<string> SupportedLevelKeys => RuntimeBases.Keys;

    public static bool TryGetRuntimeBase(string levelKey, out uint runtimeBase)
    {
        string normalizedLevelKey = LevelCatalog.NormalizeKey(levelKey);
        return RuntimeBases.TryGetValue(normalizedLevelKey, out runtimeBase);
    }
}
