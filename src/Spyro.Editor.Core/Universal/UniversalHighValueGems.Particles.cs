using System.Security.Cryptography;

namespace Spyro.Editor.Core.Universal;

public static partial class UniversalHighValueGems
{
    private sealed record ParticleOwner(uint Spawn, int SpawnLength, string SpawnHash, uint SpawnTable, uint SpawnCase,
        uint Update, int UpdateLength, string UpdateHash, uint UpdateTable, uint UpdateCase);
    // Complete original-USA per-level spawn/update function owners and their
    // independent type12 jump-table words. Prepared/fresh overlays retain these.
    private static readonly ParticleOwner[] ParticleOwners =
    [
        new(0x800873E0, 4672, "7c0e1b4d629f6925843e580f43a0b41e241d0f0b5c802701d3b3451d1179eddd", 0x8007AD18, 0x800878B8,
            0x80086134, 4780, "7de51293f568ab1c7fae1b23910473606cd468499666b8eb99be432fb9fe9950", 0x8007ABD4, 0x800865E0), // artisans
        new(0x800892C4, 4340, "3a72d61ab7e60ade8474a856482f05dcdf02834f47576655a964b0561ff6c4cd", 0x8007ADC8, 0x800897A4,
            0x80088098, 4652, "946daaee9191c099678ad893e36e71dad90cd8a8345e6548d8bfc6e6d08bb7b2", 0x8007AC84, 0x8008853C), // stonehill
        new(0x800844A0, 4340, "3c8bb2b250a0f63124d46b7b4e5238ed325172efb9fcc50a9015968d93d1b908", 0x8007ACB8, 0x80084980,
            0x80083274, 4652, "42568934fe492f56cb58c8d22e803ccfb605b994789d917c86ae8a245b074af8", 0x8007AB74, 0x80083718), // darkhollow
        new(0x80088F68, 3940, "0f41d76490950c9ee0b2c3be847ff9b3f360b4852ce674e7acc04120c975c931", 0x8007ADC4, 0x800892C0,
            0x80087E20, 4424, "1279e327bdbf92e03c991d619cf3190f3015bb63493f13855355588c0ba78cdc", 0x8007AC80, 0x800881E8), // townsquare
        new(0x8008391C, 4340, "7d70f9bbc4453c72f945a7c8d299ba0a2295d9d30524dcbe1fbb3605d4e0c86f", 0x8007AE0C, 0x80083DFC,
            0x800826F0, 4652, "ec1f2bc97ba963faf1c1d9f475cec9dbeb2c2caa04ebd45412f8713a39a16f4c", 0x8007ACC8, 0x80082B94), // toasty
        new(0x80084128, 3496, "97bdb38470b49e99e5b872b820e387f08ef92f5778cbb7ad47f28c037962c2c8", 0x8007ACA8, 0x80084480,
            0x80083330, 3576, "5b552f09141d3c85b5a989046be6ee29bd3bb986f91f654eb66cfcc55148c8e6", 0x8007AB68, 0x800836F0), // sunnyflight
        new(0x8008BFF0, 4020, "73c3300f23403ea80dd10c4e2d8fb2134b84a726af9078e0e4b38b43e110a022", 0x8007B710, 0x8008C338,
            0x8008AE28, 4552, "4222aa7ae72bafa344b89844ba31b817b9e1917ed1636d957ef17fd76b015b9b", 0x8007B5CC, 0x8008B1F0), // peacekeepers
        new(0x8008C540, 4288, "35cd90a754e441660498b531bd75ed2716a37a3b8f2b1e375c24162867136532", 0x8007B5B0, 0x8008C898,
            0x8008B1C0, 4992, "96af90c6df628ca033961af9b9198f1c75df231973ee5c81d9194b30d36b3f78", 0x8007B46C, 0x8008B580), // drycanyon
        new(0x8008A9A8, 4432, "1e1885e9ec02ace9fb9cb8ae76c04bb03587ab0d1bd9bc516ad3a55d0f8786ac", 0x8007B590, 0x8008AE90,
            0x80089714, 4756, "183abf53c2f53f068142802ea9120c39c1a401e5f4891ad38b7bca25ba7f38cf", 0x8007B44C, 0x80089BB8), // clifftown
        new(0x80085184, 4316, "28421651b23f6227697ccda3c288537b368d9961c1eb083c687889866197524c", 0x8007B360, 0x800854DC,
            0x80083F2C, 4696, "f82d5ef453f6de13c6930f3b5fb8a372b482bda1c905ce5cb11fbed095b27d49", 0x8007B21C, 0x800842F8), // icecavern
        new(0x8008611C, 4340, "cca239bd900dafe3603bf6ca8bb29c265e4bf0329c17c0abadb808b0745d5317", 0x8007AD30, 0x800865FC,
            0x80084EF0, 4652, "4e2b551053b324fdb318d679aadb8394a542fd38c1b0a9807640e97808b710db", 0x8007ABEC, 0x80085394), // doctorshemp
        new(0x80082F58, 3224, "aed1a075ffb81db097d6f94611d73562c5c0ade311ad14fa1014ee3e5f8b1118", 0x8007ACA8, 0x800832A8,
            0x80082300, 3160, "1a36a7fec27dc0734f81044681e5be79fa7837e0bab5a767b9d3c8744d18f61e", 0x8007AB68, 0x800826C8), // nightflight
        new(0x8008D2D0, 4920, "369de6435b9746f1ceef7edabd554d415f9d68f8f24a51b3b59e2ebcdd86b162", 0x8007B708, 0x8008D8E4,
            0x8008BE98, 5176, "05bfef437e27d833b264f03752acc6a12608623eb7b038f482292713d6c390b4", 0x8007B5C4, 0x8008C520), // magiccrafters
        new(0x8008C9D8, 5352, "43bd6a02a7ec0cd376bff48bdccac1787efdcec26974cf723dd7e269bede1bbb", 0x8007B998, 0x8008D04C,
            0x8008AF54, 6788, "4e56bf96819f2f5aa5aae7338ba8487250c1edbc7d48c1d5858ea7c8a7f0a639", 0x8007B854, 0x8008BA80), // alpineridge
        new(0x8008B0B0, 5772, "0a63c7f07cd608d221aad8716353cf6044cef39e172b107ee4887ad6c842da42", 0x8007B4A4, 0x8008B72C,
            0x80089454, 7260, "5acbe0b27fbf6788274973e5a0b9982dc25d26bc500dd9e44068bdff71b455db", 0x8007B360, 0x80089F88), // highcaves
        new(0x80089450, 5200, "f23903defa0503222eeaf71cb3ea3632a168f1c42d46779db75398c6b53e28dd", 0x8007B620, 0x80089BC8,
            0x80087B40, 6416, "935981a96079258363abca5275bfe87f97c90b3fa8a0b12b9c514e404c196675", 0x8007B4DC, 0x800885C0), // wizardpeak
        new(0x80085F40, 5468, "b75de9e0abd363e034c05782e38f3aef611770f4ae8744bef9f42130363363e1", 0x8007ADA0, 0x800866DC,
            0x80084830, 5904, "696d7cc1bdeac1afc2b47280333f9b3c923ab8e7328dcfe7be4255b9c0ac35f4", 0x8007AC5C, 0x80084F9C), // blowhard
        new(0x800836F8, 3224, "644ec5fd7986821be6d582adca9243548a587e68b1e465709d63df066f45867d", 0x8007ACA8, 0x80083A48,
            0x80082AA0, 3160, "9a3af5f332dbc387f7d9b54048b30b5ebd5f142d52ac21baf606939a95d3eeb5", 0x8007AB68, 0x80082E68), // crystalflight
        new(0x80089AB8, 4280, "35bcc4e2d7280edaf7bcf6021d97d4c99f11ff7d9f09ada2cc54ad0b353b380c", 0x8007B4DC, 0x80089F90,
            0x800888F8, 4544, "50d19b3d42528fd4fa677dab256782e3747f8d4646acd6a9610fecb007dbb17f", 0x8007B398, 0x80088D94), // beastmakers
        new(0x800866D8, 4716, "be2f70563137c96de55fc12bf81b50436c9c2a8940ccf2cebf4837fb40cce0e0", 0x8007B454, 0x80086CDC,
            0x800853AC, 4908, "1889589d267d45d58a451753630598f734e21d7c62c1b545283cef128591eef8", 0x8007B310, 0x800858E8), // terracevillage
        new(0x800861CC, 3940, "f2485d80c53612bd9f7bc93256f700bd983f77586b60a65d229d7fd58491aa03", 0x8007AE54, 0x80086524,
            0x80085084, 4424, "8b3ed43d65fe8393b4fa0c40ce16c8bd741fc705953efab3b43da9b51efa66a4", 0x8007AD10, 0x8008544C), // mistybog
        new(0x8008869C, 4524, "192048b2b77caf7c4ffc86b8af7bc17b6eaed291e0fef4b62beb17999b1cfbe0", 0x8007B510, 0x80088B84,
            0x80087400, 4764, "b53e7a43ac976599c682c27fc278280b2b54a2b7dd41657a8fb90d6f7b9095a0", 0x8007B3CC, 0x800878A4), // treetops
        new(0x800894B0, 4588, "b59270eb5dce504ae8dea80843a679cffa7cead3c3caa7997f5db0fc67101141", 0x8007B5E8, 0x80089998,
            0x80088178, 4920, "1a1f880f1c6cdeb067b57042769ea835688cc41aee77e7a9ea44a0db6df395a9", 0x8007B4A4, 0x80088610), // metalhead
        new(0x80083BAC, 3224, "c54b76f72e5a897df5d4675a6b0ffa234a4162ca7acda34c8c2cf5da3e002621", 0x8007ACA8, 0x80083EFC,
            0x80082F54, 3160, "06e5c1b26159c473c3ba755f813dac9cf6746f4f2eff19b7afe96e08bba846bb", 0x8007AB68, 0x8008331C), // wildflight
        new(0x8008AA24, 4372, "71045cabe8cdd6147ab5ba7ae2bd3c04e055e21bc98177cf716f4f2548d86d28", 0x8007B710, 0x8008AEFC,
            0x800897FC, 4648, "95bb59a57176b14e410571fb5032d0c90b47d2f947299262ca4eea30b848533d", 0x8007B5CC, 0x80089C98), // dreamweavers
        new(0x80086D38, 5020, "37358ca0fb4dba01e829c46d5e94aae7146fb550b20680c95327f16361eaa96e", 0x8007B370, 0x80087210,
            0x800857FC, 5436, "0de2b6053564c8aa12aaa5c447964561dbe2088f63719d595f936d74ce1eb9cf", 0x8007B22C, 0x80085C98), // darkpassage
        new(0x80086438, 4836, "09ea1b0016b59d0ed37c20d5a4e3f7f5a32ef6e1fc75717146d18001cd16dba6", 0x8007B354, 0x80086920,
            0x800850A0, 5016, "9c7dedc5a3ea3ea3ea9b8bcc4f9e61d0715a5eab40f822c5388dad1080cc133b", 0x8007B210, 0x80085538), // loftycastle
        new(0x800881D8, 5704, "50b00ff3a9a569328d9192195bc323f515da5c1f9e0e7fb84e1ed08dc70b3a4a", 0x8007B388, 0x800887CC,
            0x80086754, 6788, "e5af2729cd0e3666ee092c2993b007d3430e44699a67f97258a762e5da0a99a8", 0x8007B244, 0x80086C84), // hauntedtowers
        new(0x80085254, 4340, "69dc4590d7d8a075dad4a63cd078650fbc602a1288a2ca0a32c06472391e2ecf", 0x8007ADC8, 0x80085734,
            0x80084028, 4652, "8cbbb51ba6e05ffb63042b1e2fdd60dfe13922c02f07b425a9bca53661989b3d", 0x8007AC84, 0x800844CC), // jacques
        new(0x80083B8C, 3496, "f7c9fe63e7ac172af1586c034dc490431056d5361fda875b5257a8623669078e", 0x8007ACA8, 0x80083EE4,
            0x80082D94, 3576, "647365a35880d30418947e3be78531d9e41e1f7f82d5a304c19d36fd70bf554b", 0x8007AB68, 0x80083154), // icyflight
        new(0x80084EA0, 3648, "d378788ee6da8e9b8cd9bab3ae07433dc2ef68ff2282a64ca753afddf20793e8", 0x8007ACA8, 0x800851F0,
            0x80083ED8, 4040, "18cb786ce6af040e029b90dbb3ed80e5be316dce44c5c96b15aa79fdc7db3acd", 0x8007AB64, 0x800842A0), // gnastysworld
        new(0x8008747C, 4588, "f9b549de8d1cece23f3a3612bf8354be196f904a3f433b5465ec0c56ce1d25a5", 0x8007B3A0, 0x80087964,
            0x80086144, 4920, "e023510727daabbb1821e56cd547723efa6518f5e1af6f0d1967bfc871369a2f", 0x8007B25C, 0x800865DC), // gnorccove
        new(0x80084EAC, 4440, "8f90a0a5e798998a6ecaa168d6937be540e3a61bde05431127dc62f86ae7a324", 0x8007ACD4, 0x80085394,
            0x80083B4C, 4960, "de614fce5e088ec3ecbbf7f376155ec23b930a591438f595054b200d9856175b", 0x8007AB90, 0x80083FF0), // twilightharbor
        new(0x80084634, 3712, "0e661ddbfcc24f7ef2959811993ceba8def29253931e4881761d1d0b161e6655", 0x8007AC04, 0x8008497C,
            0x800836F0, 3908, "748b8684bbddee13a30b3ad1be9bd5323f6caeb297ba1555b4bf6ad7ec8c8887", 0x8007AAC4, 0x80083AB0), // gnastygnorc
        new(0x80085230, 4148, "7ea5898b6e2ea88ef6aa561a205dc065f097b32ef53adfa16711c7a4213e11f0", 0x8007ABEC, 0x80085580,
            0x800840FC, 4404, "4ff093bc19889290e3faf487691c5793a0cb267026470988393b4c2ccbde89df", 0x8007AAAC, 0x800844BC), // gnastysloot
    ];

    /// <summary>Type12 is a native colour-controlled collectible star in all 35
    /// retained particle dispatchers. Do not emit it into an edited/unproved owner.</summary>
    public static void RequireDestinationParticleOwner(UniversalOverlay overlay)
    {
        if (!overlay.Bindings.TryGetValue(UniversalExeBindings.ParticleSpawnGlobal, out var spawn) ||
            !overlay.Bindings.TryGetValue(UniversalExeBindings.ParticleUpdateGlobal, out var update))
            throw new InvalidDataException("High-value gem effects require actual destination particle bindings.");
        bool Exact(uint address, int length, string hash) => overlay.Contains(address) &&
            (ulong)address + (uint)length <= overlay.End &&
            Convert.ToHexStringLower(SHA256.HashData(overlay.Bytes.Slice((int)(address - UniversalOverlay.Base), length))) == hash;
        foreach (var owner in ParticleOwners.Where(p => p.Spawn == spawn.Value && p.Update == update.Value))
            if (Exact(owner.Spawn, owner.SpawnLength, owner.SpawnHash) &&
                Exact(owner.Update, owner.UpdateLength, owner.UpdateHash) &&
                overlay.Contains(owner.SpawnTable + 12 * 4) && overlay.Contains(owner.UpdateTable + 12 * 4) &&
                overlay.Word(owner.SpawnTable + 12 * 4) == owner.SpawnCase && overlay.Word(owner.UpdateTable + 12 * 4) == owner.UpdateCase)
                return;
        throw new InvalidDataException("High-value gem type12 effects lack certified native destination spawn/update/table owners.");
    }
}
