using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

[assembly: InternalsVisibleTo("Spyro.Editor.UniversalParticleNoOpSmoke")]

namespace Spyro.Editor.Core.Universal;

public sealed record UniversalNativeParticleNoOpProof(uint SourceReadPc, string SourceOverlaySha256,
    uint CallbackPc, int CallbackLength, string CallbackSha256, uint TableAddress, uint NoOpTarget);

/// <summary>One source-owned linked-actor class argument is harmless only when type64 reaches
/// the authenticated native no-allocation tail and the following native callee kills a3.
/// This does not certify other particle types, actor classification or general runtime behavior.</summary>
internal static class UniversalNativeParticleNoOpOwnership
{
    private sealed record Caller(string Sha, uint Read);
    private sealed record Callback(uint Pc, int Length, string Sha, uint Table, uint Target);
    private const uint KillCallee = 0x8003851C;
    private const string KillSha = "59e192e6d66136d6e1d4b4603a404d7f28eacd048a93491f0ee53530bcf9a572";
    // Exact original-USA source packet and callback certificates. They include native control,
    // register saves/restores and the no-allocation loop, not only one convenient opcode.
    private static readonly Caller[] Callers =
    [
        new("9bc923cc8d27703537b81b01f51fde351e58aabee87d38a8afc630f1031878a5", 0x800852b8u), // townsquare
        new("299f4822489c48736c9f46bb9556e82952fc1693f5f9f42bc2e6415c1908aebc", 0x800886a8u), // peacekeepers
        new("8aa4036028de3ea9cdc6ee93b4d96cb7dc2b9fa5f818b6e748c0c2bda946cc0f", 0x80088ae4u), // drycanyon
        new("a50a830556b071b7b7afd6f30232b7e4d8be2ab3329dff24a4332e19ed5ff5a6", 0x800825e8u), // icecavern
        new("73c7fd7ba2083507edbeca38393c825072ec2bd92e0bbbe9c3df5f5010aa8de9", 0x80080bf4u), // doctorshemp
        new("98e135e18876ea094ca9484b33cfb38438e2b99b72ee4d6d66e907722c744a1d", 0x800893b0u), // magiccrafters
        new("f7c9a174f64959fb53b4058f9786df223a9c142b2289563ca31edc6a24c6d383", 0x800886e0u), // alpineridge
        new("6c153366d9b3dd6d25a7aa8e8d1ea6708bdf97b04ee35912b11df0f8b9913140", 0x80087544u), // highcaves
        new("5d3996111567ffac6eca08d340f2f691824bb7969ba2a0cd95e9346ccfe157e1", 0x80085404u), // wizardpeak
        new("1ee0030607139491b6d076c389837d5b6c0a38e87d324fec2e14b58b47f95aef", 0x80081cb0u), // blowhard
        new("6bed996a20df44803ec4eefe434294082c3f5814ce56080e0594bab95eeb287f", 0x8008552cu), // beastmakers
        new("285fc63da6b536af2b41c19e41d1db03a980007226dc55e5d930a1998f39c50e", 0x80080bf8u), // terracevillage
        new("3b37eb8b4b0f5fb74e7d263670705e20c807f2d8f1bbf03f34a461eb1c3e2ff6", 0x80080e28u), // mistybog
        new("f7363f0f2a03e6ddcc906fd68c914905c3704e49e058220941ac9a0ece488405", 0x80084b00u), // treetops
        new("97a960c4c4e9f6933d3dc4ab19a31987a105f33dfdfd98433021977e7778543f", 0x80085cf8u), // metalhead
        new("dcbe76c16c51393e784037af420c7fb7c00cecd526ed4d890ae5f1b257cdb352", 0x80087870u), // dreamweavers
        new("a8b99270c37a37363824cf1fd2081c092968abef62df9e74d70ae0017b984727", 0x80083370u), // darkpassage
        new("e0f1f9e14726e4ca9d41e9e8bca112290263f00ce85413a7e389ab59c9e358c3", 0x80082e88u), // loftycastle
        new("e714a2c4aff4a7e8d4980fa091540f4fdcd320b4fe4c532fa8c6c3d36fd7fbfc", 0x80083d54u), // hauntedtowers
        new("e09f6cb0adeb6275b04549507ed5d8424c1f6045ecfde0f50424f6ba58713f49", 0x800826acu), // jacques
        new("1d6f62cc80f515c73efc50b9c8435a36cb51b5e2bcea8823005ce05ed856fad9", 0x800843c4u), // gnorccove
        new("55d0845c750c0fcbd13d69633d1481d288bb3b02ce33bf60e3b17866111739dd", 0x80081ca4u), // twilightharbor
        new("9b8b9677df191c917ed45896192db86c82cdcad2853ba1cd7dfd6feca3be43a4", 0x8008247cu), // gnastygnorc
        new("2117b3b14116b98035d9b003df3f299ef127ee34558f700af2be9fcb7e165a6e", 0x80081b84u), // gnastysloot
    ];
    private static readonly Callback[] Callbacks =
    [
        new(0x800873E0u, 4672, "7c0e1b4d629f6925843e580f43a0b41e241d0f0b5c802701d3b3451d1179eddd", 0x8007AD18u, 0x800885C8u), // artisans
        new(0x800892C4u, 4340, "3a72d61ab7e60ade8474a856482f05dcdf02834f47576655a964b0561ff6c4cd", 0x8007ADC8u, 0x8008A360u), // stonehill
        new(0x800844A0u, 4340, "3c8bb2b250a0f63124d46b7b4e5238ed325172efb9fcc50a9015968d93d1b908", 0x8007ACB8u, 0x8008553Cu), // darkhollow
        new(0x80088F68u, 3940, "0f41d76490950c9ee0b2c3be847ff9b3f360b4852ce674e7acc04120c975c931", 0x8007ADC4u, 0x80089E74u), // townsquare
        new(0x8008391Cu, 4340, "7d70f9bbc4453c72f945a7c8d299ba0a2295d9d30524dcbe1fbb3605d4e0c86f", 0x8007AE0Cu, 0x800849B8u), // toasty
        new(0x80084128u, 3496, "97bdb38470b49e99e5b872b820e387f08ef92f5778cbb7ad47f28c037962c2c8", 0x8007ACA8u, 0x80084E78u), // sunnyflight
        new(0x8008BFF0u, 4020, "73c3300f23403ea80dd10c4e2d8fb2134b84a726af9078e0e4b38b43e110a022", 0x8007B710u, 0x8008CF4Cu), // peacekeepers
        new(0x8008C540u, 4288, "35cd90a754e441660498b531bd75ed2716a37a3b8f2b1e375c24162867136532", 0x8007B5B0u, 0x8008D5A8u), // drycanyon
        new(0x8008A9A8u, 4432, "1e1885e9ec02ace9fb9cb8ae76c04bb03587ab0d1bd9bc516ad3a55d0f8786ac", 0x8007B590u, 0x8008BAA0u), // clifftown
        new(0x80085184u, 4316, "28421651b23f6227697ccda3c288537b368d9961c1eb083c687889866197524c", 0x8007B360u, 0x80086208u), // icecavern
        new(0x8008611Cu, 4340, "cca239bd900dafe3603bf6ca8bb29c265e4bf0329c17c0abadb808b0745d5317", 0x8007AD30u, 0x800871B8u), // doctorshemp
        new(0x80082F58u, 3224, "aed1a075ffb81db097d6f94611d73562c5c0ade311ad14fa1014ee3e5f8b1118", 0x8007ACA8u, 0x80083B98u), // nightflight
        new(0x8008D2D0u, 4920, "369de6435b9746f1ceef7edabd554d415f9d68f8f24a51b3b59e2ebcdd86b162", 0x8007B708u, 0x8008E5B0u), // magiccrafters
        new(0x8008C9D8u, 5352, "43bd6a02a7ec0cd376bff48bdccac1787efdcec26974cf723dd7e269bede1bbb", 0x8007B998u, 0x8008DE68u), // alpineridge
        new(0x8008B0B0u, 5772, "0a63c7f07cd608d221aad8716353cf6044cef39e172b107ee4887ad6c842da42", 0x8007B4A4u, 0x8008C6E4u), // highcaves
        new(0x80089450u, 5200, "f23903defa0503222eeaf71cb3ea3632a168f1c42d46779db75398c6b53e28dd", 0x8007B620u, 0x8008A848u), // wizardpeak
        new(0x80085F40u, 5468, "b75de9e0abd363e034c05782e38f3aef611770f4ae8744bef9f42130363363e1", 0x8007ADA0u, 0x80087444u), // blowhard
        new(0x800836F8u, 3224, "644ec5fd7986821be6d582adca9243548a587e68b1e465709d63df066f45867d", 0x8007ACA8u, 0x80084338u), // crystalflight
        new(0x80089AB8u, 4280, "35bcc4e2d7280edaf7bcf6021d97d4c99f11ff7d9f09ada2cc54ad0b353b380c", 0x8007B4DCu, 0x8008AB18u), // beastmakers
        new(0x800866D8u, 4716, "be2f70563137c96de55fc12bf81b50436c9c2a8940ccf2cebf4837fb40cce0e0", 0x8007B454u, 0x800878ECu), // terracevillage
        new(0x800861CCu, 3940, "f2485d80c53612bd9f7bc93256f700bd983f77586b60a65d229d7fd58491aa03", 0x8007AE54u, 0x800870D8u), // mistybog
        new(0x8008869Cu, 4524, "192048b2b77caf7c4ffc86b8af7bc17b6eaed291e0fef4b62beb17999b1cfbe0", 0x8007B510u, 0x800897F0u), // treetops
        new(0x800894B0u, 4588, "b59270eb5dce504ae8dea80843a679cffa7cead3c3caa7997f5db0fc67101141", 0x8007B5E8u, 0x8008A644u), // metalhead
        new(0x80083BACu, 3224, "c54b76f72e5a897df5d4675a6b0ffa234a4162ca7acda34c8c2cf5da3e002621", 0x8007ACA8u, 0x800847ECu), // wildflight
        new(0x8008AA24u, 4372, "71045cabe8cdd6147ab5ba7ae2bd3c04e055e21bc98177cf716f4f2548d86d28", 0x8007B710u, 0x8008BAE0u), // dreamweavers
        new(0x80086D38u, 5020, "37358ca0fb4dba01e829c46d5e94aae7146fb550b20680c95327f16361eaa96e", 0x8007B370u, 0x8008807Cu), // darkpassage
        new(0x80086438u, 4836, "09ea1b0016b59d0ed37c20d5a4e3f7f5a32ef6e1fc75717146d18001cd16dba6", 0x8007B354u, 0x800876C4u), // loftycastle
        new(0x800881D8u, 5704, "50b00ff3a9a569328d9192195bc323f515da5c1f9e0e7fb84e1ed08dc70b3a4a", 0x8007B388u, 0x800897C8u), // hauntedtowers
        new(0x80085254u, 4340, "69dc4590d7d8a075dad4a63cd078650fbc602a1288a2ca0a32c06472391e2ecf", 0x8007ADC8u, 0x800862F0u), // jacques
        new(0x80083B8Cu, 3496, "f7c9fe63e7ac172af1586c034dc490431056d5361fda875b5257a8623669078e", 0x8007ACA8u, 0x800848DCu), // icyflight
        new(0x80084EA0u, 3648, "d378788ee6da8e9b8cd9bab3ae07433dc2ef68ff2282a64ca753afddf20793e8", 0x8007ACA8u, 0x80085C88u), // gnastysworld
        new(0x8008747Cu, 4588, "f9b549de8d1cece23f3a3612bf8354be196f904a3f433b5465ec0c56ce1d25a5", 0x8007B3A0u, 0x80088610u), // gnorccove
        new(0x80084EACu, 4440, "8f90a0a5e798998a6ecaa168d6937be540e3a61bde05431127dc62f86ae7a324", 0x8007ACD4u, 0x80085FACu), // twilightharbor
        new(0x80084634u, 3712, "0e661ddbfcc24f7ef2959811993ceba8def29253931e4881761d1d0b161e6655", 0x8007AC04u, 0x8008545Cu), // gnastygnorc
        new(0x80085230u, 4148, "7ea5898b6e2ea88ef6aa561a205dc065f097b32ef53adfa16711c7a4213e11f0", 0x8007ABECu, 0x8008620Cu), // gnastysloot
    ];

    private static string Sha(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void RequireKill(byte[] exe)
    {
        int offset = checked((int)(KillCallee - UniversalExeBindings.ExeBase));
        if (exe.Length != 0x66000 || Sha(exe.AsSpan(offset, 24)) != KillSha)
            throw new InvalidDataException("Particle no-op ownership requires the authenticated next-callee a3 kill.");
    }
    private static Callback RequireCallback(UniversalOverlay actual)
    {
        if (!actual.Bindings.TryGetValue(UniversalExeBindings.ParticleSpawnGlobal, out var binding))
            throw new InvalidDataException("Particle no-op ownership requires its actual final dispatch binding.");
        uint pc = binding.Value;
        foreach (var c in Callbacks.Where(c => c.Pc == pc))
        {
            if (!actual.Contains(pc) || (ulong)pc + (uint)c.Length > actual.End ||
                !actual.Contains(c.Table + 64 * 4) ||
                Sha(actual.Bytes.Slice((int)(pc - UniversalOverlay.Base), c.Length)) != c.Sha) continue;
            if (actual.Word(c.Table + 64 * 4) != c.Target)
                throw new InvalidDataException("Particle type64 no longer selects its certified no-allocation tail.");
            return c;
        }
        throw new InvalidDataException("Particle callback lacks an authenticated native type64 no-op owner.");
    }

    // Kept separate so source-free tests exercise producer/consumer/control fences without
    // manufacturing a native source certificate. MarkOwned always authenticates the owners too.
    internal static bool ProveCaller(Func<uint, uint> word, IReadOnlySet<uint> included,
        IReadOnlyList<uint> controlTargets, uint pc)
    {
        if (pc < 80 || Enumerable.Range(-19, 29).Any(i => !included.Contains((uint)((long)pc + i * 4)))) return false;
        if (controlTargets.Any(t => t > pc - 80 && t <= pc + 36)) return false;
        uint W(int delta) => word((uint)((long)pc + delta));
        int actor = UniversalMips.Rs(W(-4));
        if (actor is < 16 or > 23 || W(-8) != 0x8E220000 ||
            W(-4) != UniversalMips.EncodeI(9, actor, 6, 12) || W(0) != 0x84470036 ||
            W(4) != 0x3C028007 || W(8) != 0x8C4258E4 || W(12) != 0 ||
            W(16) != 0x0040F809 || W(20) != 0x24E7FFAD ||
            W(24) != (uint)((actor << 21) | (4 << 11) | 0x21) || W(28) != 0x00002821 ||
            W(32) != UniversalMips.EncodeJ(3, KillCallee) || W(36) != 0x00003021 ||
            W(-76) != UniversalMips.EncodeJ(3, 0x8003ABC0) ||
            W(-72) != UniversalMips.EncodeI(40, actor, 2, 0x52) ||
            W(-28) != 0x24040008 || W(-16) != 0x24050040) return false;
        // The native engine's returned actor is stored through the callee-saved property
        // pointer before this straight block reloads it. Preserve R3000 load delays.
        var stores = new[] { -68, -64 }.Where(d => W(d) == 0xAE220000).ToArray();
        if (stores.Length != 1) return false;
        for (int d = stores[0] + 4; d < 16; d += 4)
        {
            uint w = W(d);
            if (UniversalMips.IsBranch(w)) return false;
            if (!UniversalMips.IsStore(w) && (UniversalMips.Op(w) == 0 ? UniversalMips.Rd(w) : UniversalMips.Rt(w)) == 17) return false;
        }
        return true;
    }

    internal static IReadOnlyList<UniversalNativeParticleNoOpProof> MarkOwned(UniversalAsmPart part,
        UniversalOverlay source, UniversalOverlay actualDestination, byte[] executable)
    {
        string sha = Sha(source.Bytes);
        var sourceCases = Callers.Where(c => c.Sha == sha).ToArray();
        if (sourceCases.Length == 0) return [];
        var included = part.Items.Where(i => i.Word != null && (i.SourcePc ?? i.Orig) != null)
            .Select(i => (i.SourcePc ?? i.Orig)!.Value).ToHashSet();
        List<uint> targets = [.. part.Tables.Values.SelectMany(t => t)];
        foreach (var item in part.Items)
            if (item.Word is uint w && (item.SourcePc ?? item.Orig) is uint pc &&
                (UniversalMips.IsPcRelative(w) || UniversalMips.Op(w) == 2))
                targets.Add(UniversalMips.BranchTarget(w, pc));
        List<(int Index, UniversalNativeParticleNoOpProof Proof)> owned = [];
        foreach (var c in sourceCases)
        {
            var indices = Enumerable.Range(0, part.Items.Count).Where(i => (part.Items[i].SourcePc ?? part.Items[i].Orig) == c.Read).ToArray();
            if (indices.Length == 0 || indices.All(i => part.Items[i].ClassReadOwned)) continue;
            if (!ProveCaller(source.Word, included, targets, c.Read))
                throw new InvalidDataException($"Particle no-op class read at 0x{c.Read:X8} lacks its complete linked-actor/control owner.");
            // Every retained source instruction must still be the native packet. Generated code
            // and edited delays cannot borrow the certificate from their metadata alone.
            foreach (uint pc in Enumerable.Range(-19, 29).Select(i => (uint)((long)c.Read + i * 4)))
            {
                var copies = part.Items.Where(i => (i.SourcePc ?? i.Orig) == pc).ToArray();
                if (copies.Length == 0 || copies.Any(i => i.Word != source.Word(pc) || i.Fix.Kind != UniversalFixKind.None))
                    throw new InvalidDataException("Particle no-op caller differs from its authenticated source packet.");
            }
            RequireKill(executable); RequireCallback(source);
            var callback = RequireCallback(actualDestination);
            foreach (int index in indices)
                owned.Add((index, new(c.Read, sha, callback.Pc, callback.Length, callback.Sha, callback.Table, callback.Target)));
        }
        foreach (var o in owned) part.Items[o.Index] = part.Items[o.Index] with { ClassReadOwned = true };
        return owned.Select(o => o.Proof).Distinct().ToArray();
    }

    internal static void RequireFinalDestination(IReadOnlyList<UniversalNativeParticleNoOpProof> proofs,
        UniversalOverlay finalDestination, byte[] executable)
    {
        if (proofs.Count == 0) return;
        RequireKill(executable);
        var actual = RequireCallback(finalDestination);
        if (proofs.Any(p => p.CallbackPc != actual.Pc || p.CallbackLength != actual.Length ||
            p.CallbackSha256 != actual.Sha || p.TableAddress != actual.Table || p.NoOpTarget != actual.Target))
            throw new InvalidDataException("Final mixed-import particle dispatch differs from the proved no-op owner.");
    }
}
