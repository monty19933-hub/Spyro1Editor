using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.Core.Analysis;

public sealed record NativeJapaneseEggRootLookup(string Stage,uint Pc,string RowRegister,string RootRegister,uint Instruction);

/// <summary>
/// Source-issued negative binding result and exact renderer-join requirements
/// for two RGB-only variants. This owns no model allocation, hook bytes, row
/// pointer, class alias or lifecycle callback. Native F5 remains class-shared.
/// </summary>
public sealed class NativeJapaneseEggInstanceRootResolution
{
    public const uint ClassTableAddress=0x80076378;
    public const uint F5ClassSlotAddress=ClassTableAddress+0xF5*4;
    public const uint RequiredRenderInterceptPc=0x80020A1C;
    public const int RequiredDisplacedByteLength=12;
    public const uint RequiredRenderJoinPc=0x80020A28;
    private static readonly NativeJapaneseEggRenderConsumer[] Windows=
    [
        new("registered-model-loop",0x80015ACC,0x68,"e3333647e8548d11fd19653ac0d706b78571d6dbe1fa439b6db10b5b59235a9b"),
        new("post-registration-scene-boundary",0x80015B34,0x4C,"556eae60eabc2d6d06e0c7bf20086d0d60cdf371a7668041a58af7362e1b2760"),
        new("animated-loader",0x800133E0,0x26C,"fede3c414aea2dce0372fce574549d961123cf6456a7a291acef48b9c4af8598"),
        new("initialization-route",0x8001417C,0x40,"b66b1386854754bc87337f24917bdb9dd8681be930dfa5f37fe12b7c5397e3b8"),
        new("animated-initializer",0x800526A8,0xB0,"6957574246c153803d760eb6e0001529b8016eaec09da84f94aa893cc5560c21"),
        new("animation-step",0x800522C0,0x1A4,"43915998682dbb7fe33175b21f638f2720ce954efe2dd3079f57bb51275be512"),
        new("render-root-select",0x80020A14,0x24,"f923ba46fda84cbf6237a93ec76f0e67f1426f666804392edcd245bb8aed6c36"),
        new("draw-command",0x80020BB0,0xFC,"15cb76c5611031dd1e9dc3c92f06be6037967dd217bcf21f97d413465b883614"),
        new("animated-rgb",0x800215C8,0x174,"4cd236569c340e49360938ad8397aed87f2a7518d13134c935fd7f32acb856a5")
    ];
    private static readonly NativeJapaneseEggRootLookup[] Sites=
    [
        new("initialization-route",0x80014194,"a0","v0",0x8C226378),
        new("animated-initializer",0x800526C0,"a0","at",0x8C210000),
        new("animation-frame-wrap",0x8005235C,"t5","t2",0x8D4A0000),
        new("animation-transition",0x800523AC,"t5","t2",0x8D4A0000),
        new("render-root-select",0x80020A24,"sp","t7",0x8DEF0000)
    ];
    private readonly byte[] _exe;
    private NativeJapaneseEggInstanceRootResolution(byte[] exe,NativeJapaneseEggRgbEditPlan first,NativeJapaneseEggRgbEditPlan second)
    {
        _exe=exe.ToArray();First=first;Second=second;
        foreach(var w in Windows)
            if(Hash(_exe.AsSpan(checked((int)(w.Pc-0x8000F800)),w.ByteLength))!=w.Sha256)
                throw new InvalidDataException("Exact native model-root consumer changed: "+w.Role);
        foreach(var s in Sites)if(Word(s.Pc)!=s.Instruction)
            throw new InvalidDataException("Native root lookup instruction changed: "+s.Stage);
        if(Word(RequiredRenderInterceptPc)!=0x000F7880||Word(RequiredRenderInterceptPc+4)!=0x01FC7820||
            Word(RequiredRenderInterceptPc+8)!=0x8DEF0000||Word(RequiredRenderJoinPc)!=0x31CD00FF)
            throw new InvalidDataException("Proposed renderer join no longer matches its exact native register ABI.");
        byte[] a=first.CopyPackage(),b=second.CopyPackage(),native=first.Source.AssetAncestor.Model.CopyPackage();
        var rgb=first.Fields.SelectMany(f=>Enumerable.Range(f.PackageOffset,3)).ToHashSet();
        if(rgb.Count!=420||a.Length!=native.Length||b.Length!=native.Length)
            throw new InvalidDataException("Variant pair does not have the exact bounded RGB-only package shape.");
        for(int i=0;i<native.Length;i++)if(!rgb.Contains(i)&&(a[i]!=native[i]||b[i]!=native[i]))
            throw new InvalidDataException("Variant topology, native CODE or another non-RGB byte changed.");
        PlanSha256=Hash(Encoding.UTF8.GetBytes("jp-f5-unbound-root-resolution-v1\n"+first.VariantIdentity+"\n"+second.VariantIdentity+
            "\n"+string.Join('\n',Windows.Select(w=>w.Sha256))+"\n"));
        uint Word(uint pc)=>BinaryPrimitives.ReadUInt32LittleEndian(_exe.AsSpan(checked((int)(pc-0x8000F800))));
    }
    public NativeJapaneseEggRgbEditPlan First{get;}
    public NativeJapaneseEggRgbEditPlan Second{get;}
    public string PlanSha256{get;}
    public IReadOnlyList<NativeJapaneseEggRenderConsumer> Consumers=>Array.AsReadOnly(Windows);
    public IReadOnlyList<NativeJapaneseEggRootLookup> RootLookups=>Array.AsReadOnly(Sites);
    public int DistinctRequestedContents=>First.VariantIdentity==Second.VariantIdentity?1:2;
    public bool SourceConsumersCertified=>true;
    public bool RgbOnlyTopologyPreserved=>true;
    public bool CoveredNativeConsumersUseSharedClassRoot=>true;
    public bool NativePerInstanceRootBindingProven=>false;
    public bool VariantResidencyOwned=>false;
    public bool SelectorHookImplemented=>false;
    public bool FinalPixelsVerified=>false;
    public bool PublicationApproved=>false;
    public IReadOnlyList<string> RequiredOwners{get;}=Array.AsReadOnly(new[]
    {
        "Allocate unregistered RGB-only packages through existing model-subfile growth and typed relocation; prove complete resource/scene complement and actual RAM ownership.",
        "Run the existing animated loader once per freshly loaded variant, after its source-form bytes arrive and before rendering; never reload already-rebased bytes or register a duplicate F5 class.",
        "Own an immutable row-identity-to-root mapping scoped to the loaded scene/level generation, with canonical fallback for all other rows and safe invalidation before unload/reload.",
        "Own the renderer interception at 80020A1C..24 and return at 80020A28: sp is row, gp is class table, t7 enters as class and leaves as root; preserve t6 packed animation and every other live register/GTE value.",
        "Keep initialization/animation on the canonical F5 model only while the RGB-only topology invariant holds; prove the hook ABI, delay slots and lifetime in native consumers before publication.",
        "Verify original-JP appearance or native GPU packet evidence and both intended RGB variants in actual gameplay; prescribed register outputs are not an installed hook or pixel proof."
    });
    public byte[] CopyConsumer(string role)
    {
        var w=Windows.SingleOrDefault(w=>w.Role==role)??throw new InvalidDataException("Unknown root-resolution consumer.");
        return _exe.AsSpan(checked((int)(w.Pc-0x8000F800)),w.ByteLength).ToArray();
    }
    public void RequireNativePerInstanceBinding()=>throw new InvalidDataException(
        "Both F5 rows still resolve one shared class root. Independent RGB packages require an owned per-row renderer selector and load-scoped variant residency; this report installs neither.");
    public static NativeJapaneseEggInstanceRootResolution Capture(NativeRegionalSourceSession usa,
        NativeJapaneseEggRgbEditPlan first,NativeJapaneseEggRgbEditPlan second)
    {
        ArgumentNullException.ThrowIfNull(usa);ArgumentNullException.ThrowIfNull(first);ArgumentNullException.ThrowIfNull(second);
        if(usa.Revision!=NativeDiscRevision.UsaScus94228||!first.SourceAncestryCertified||!second.SourceAncestryCertified||
            !ReferenceEquals(first.Source,second.Source)||first.Source.AssetAncestor.UsaSourceImageSha256!=usa.SourceImageSha256)
            throw new InvalidDataException("Root analysis requires one actual RGB source ancestry and the exact USA consumer source.");
        var result=new NativeJapaneseEggInstanceRootResolution(usa.ReadExecutableRange(0,usa.ExecutableByteLength),first,second);
        usa.VerifyUnchanged();return result;
    }
    private static string Hash(ReadOnlySpan<byte> b)=>Convert.ToHexStringLower(SHA256.HashData(b));
}
