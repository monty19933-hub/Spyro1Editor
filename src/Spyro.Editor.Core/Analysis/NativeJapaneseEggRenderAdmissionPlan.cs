using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;

[assembly: InternalsVisibleTo("Spyro.Editor.NativeJapaneseEggRenderAdmissionSmoke")]

namespace Spyro.Editor.Core.Analysis;

public sealed record NativeJapaneseEggRenderConsumer(string Role,uint Pc,int ByteLength,string Sha256);
public sealed record NativeJapaneseEggPackedColor(int Index,uint PackedWord)
{
    public byte Red=>(byte)PackedWord;
    public byte Green=>(byte)(PackedWord>>8);
    public byte Blue=>(byte)(PackedWord>>16);
    // Native RGBC CODE byte, not alpha. Preserve it when editing RGB.
    public byte Code=>(byte)(PackedWord>>24);
}
public sealed record NativeJapaneseEggRenderRow(string Id,uint RowAddress,uint PrivateAddress,
    uint ModelRoot,uint AnimationHeader,byte NativeRenderMode,byte Animation,byte CurrentFrame,byte NextFrame);

/// <summary>
/// Read-only exact-source admission/ownership binding for the existing detached
/// two-F5 Artisans composition. It owns no writes and grants no launch, visible
/// pixels, collision, gameplay, independent colours or persistence approval.
/// Draw-command bindings are conditional on the native camera/frustum accepting
/// a row; draw-list membership alone does not establish screen visibility.
/// </summary>
public sealed class NativeJapaneseEggRenderAdmissionPlan
{
    private static readonly NativeJapaneseEggRenderConsumer[] Windows=
    [
        new("scene-arena",0x80013F24,0xA0,"6be393418c3b85ecb9bbf1766e38e513df9eb7287df6fb564a5bca121f37d034"),
        new("scene-fixup",0x80014040,0x44,"55a04ceec2d212d9cca6f6212f95245cf718670e373d72b0c309862c80f9abd6"),
        new("row-initialize-route",0x8001417C,0x40,"b66b1386854754bc87337f24917bdb9dd8681be930dfa5f37fe12b7c5397e3b8"),
        new("animated-row-initialize",0x800526A8,0xB0,"6957574246c153803d760eb6e0001529b8016eaec09da84f94aa893cc5560c21"),
        new("render-list",0x800521C0,0x100,"055bbc5310be05cf9a5b0c185f5b18ea5056520d5a2f4e23c2e44ad79aec5cf2"),
        new("animation-step",0x800522C0,0x1A4,"43915998682dbb7fe33175b21f638f2720ce954efe2dd3079f57bb51275be512"),
        new("model-register",0x80015ACC,0x68,"e3333647e8548d11fd19653ac0d706b78571d6dbe1fa439b6db10b5b59235a9b"),
        new("model-loader",0x800133E0,0x26C,"fede3c414aea2dce0372fce574549d961123cf6456a7a291acef48b9c4af8598"),
        new("animation-select",0x80020A14,0x24,"f923ba46fda84cbf6237a93ec76f0e67f1426f666804392edcd245bb8aed6c36"),
        new("draw-command",0x80020BB0,0xFC,"15cb76c5611031dd1e9dc3c92f06be6037967dd217bcf21f97d413465b883614"),
        new("animated-shading",0x800215C8,0x174,"4cd236569c340e49360938ad8397aed87f2a7518d13134c935fd7f32acb856a5"),
        new("animated-untextured-faces",0x800217C4,0x5F0,"3b53e014bc7ca817809d5c368164dc043fb7eb8da9b80852db8f11feab56c595"),
        new("row-mode-dispatch",0x8002173C,0x40,"c1a6548ce53146608108905e7fb51cfc67afb3ef24ea77df0302c75233a8d617"),
        new("opaque-row-mode",0x800226F8,0x184,"ac82d0948511320f53809ecb0d6aba17bd6d26e4c5304464e784bf5b58480699"),
        new("static-table-counterexample",0x80023104,0x134,"66c82cfa7abbb5923cab63cc59f4b78d55900fd2a4dcaa6c8504e663c11d4d47")
    ];
    private readonly byte[] _executable;
    private NativeJapaneseEggRenderAdmissionPlan(NativeJapaneseEggArtisansIntegrationPlan integration,bool certified)
    {
        if(integration.SourceCertified!=certified)throw new InvalidDataException("Render admission cannot change source ancestry.");
        Integration=integration;SourceCertified=certified;_executable=integration.CopyOriginalExecutable();
        if(certified)foreach(var window in Windows)
            if(Hash(_executable.AsSpan(checked((int)(window.Pc-0x8000F800)),window.ByteLength))!=window.Sha256)
                throw new InvalidDataException("Native render consumer changed: "+window.Role);
        byte[] package=integration.VisualAncestor.ModelAssembly.CopyModelPackage();
        var model=NativeJapaneseEggModel.DecodeFixture(package);
        if(certified&&model.PackageSha256!=NativeJapaneseEggModel.SourcePackageSha256)
            throw new InvalidDataException("Render admission requires the authenticated unmodified F5 donor package.");
        ModelRoot=checked(integration.RuntimeMemory.ModelRamStart+(uint)(integration.VisualAncestor.ModelAssembly.PackageOffset-
            Word(integration.CopyLevelData(),0x10)));
        AnimationHeader=ModelRoot+0x3C;
        NearColorAddress=ModelRoot+0x70+Word(package,0x54);FarColorAddress=ModelRoot+0x70+Word(package,0x5C);
        NearColors=Array.AsReadOnly(model.NearPackedShadingWords.Select((w,i)=>new NativeJapaneseEggPackedColor(i,w)).ToArray());
        FarColors=Array.AsReadOnly(model.FarPackedShadingWords.Select((w,i)=>new NativeJapaneseEggPackedColor(i,w)).ToArray());
        // This certificate narrows the geometry reader to ordinary Gouraud
        // triangles/quads. In particular it does not admit its alternate tri bit4.
        foreach(var face in model.NearFaces.Concat(model.FarFaces))
            if((Word(package,face.PackageOffset)&0xE)!=0)
                throw new InvalidDataException("F5 render admission covers only the source ordinary untextured face flags0/1.");
        byte[] scene=integration.CopyScene();List<NativeJapaneseEggRenderRow> rows=[];
        foreach(var row in integration.Rows)
        {
            ReadOnlySpan<byte> b=scene.AsSpan(row.RowSceneOffset,0x58);
            if(BinaryPrimitives.ReadUInt16LittleEndian(b[0x36..])!=0xF5||b[0x48]!=0||b[0x4A]!=0xFF||b[0x4B]!=0x94||
                b[0x3C]!=0||b[0x3D]!=0||b[0x3E]>=2||b[0x3F]>=2||b[0x40]>=64||b[0x47]!=4||b[0x50]!=0x28)
                throw new InvalidDataException("Row does not retain the exact admitted F5 animation/list/scale/radius fields.");
            rows.Add(new(row.Placement.Id,integration.RuntimeMemory.SceneRamStart+(uint)row.RowSceneOffset,
                integration.RuntimeMemory.SceneRamStart+(uint)row.PrivateSceneOffset,ModelRoot,AnimationHeader,b[0x4F],b[0x3C],b[0x3E],b[0x3F]));
        }
        Rows=rows.AsReadOnly();
    }
    public NativeJapaneseEggArtisansIntegrationPlan Integration{get;}
    public bool SourceCertified{get;}
    public uint ModelRoot{get;}
    public uint AnimationHeader{get;}
    public uint NearColorAddress{get;}
    public uint FarColorAddress{get;}
    public IReadOnlyList<NativeJapaneseEggPackedColor> NearColors{get;}
    public IReadOnlyList<NativeJapaneseEggPackedColor> FarColors{get;}
    public IReadOnlyList<NativeJapaneseEggRenderRow> Rows{get;}
    public IReadOnlyList<NativeJapaneseEggRenderConsumer> Consumers=>Array.AsReadOnly(Windows);
    public bool AnimatedListAndModelOwnersProven=>SourceCertified;
    public bool StaticDescriptorTableApplies=>false;
    public bool IndependentColorVariantsProven=>false;
    public bool LitPixelsVerified=>false;
    public bool ActivationApproved=>false;
    public bool PublicationApproved=>false;
    public void RequireIndependentColors()=>throw new InvalidDataException("Both rows share the same model RGB owners; opaque row4F is not an F5 tint selector. Distinct owned RGB variants require independently bound model roots or a proved per-instance renderer change.");
    public void RequireActivation()=>throw new InvalidDataException("Source draw-list/animation ownership is not native visible-pixel, gameplay, memory-policy or launch approval.");
    public byte[] CopyConsumer(string role)
    {
        if(!SourceCertified)throw new InvalidDataException("Fixtures do not carry native consumer evidence.");
        var owner=Windows.SingleOrDefault(w=>w.Role==role)??throw new InvalidDataException("Unknown native consumer role.");
        return _executable.AsSpan((int)(owner.Pc-0x8000F800),owner.ByteLength).ToArray();
    }
    public static NativeJapaneseEggRenderAdmissionPlan Capture(NativeRegionalSourceSession usa,NativeJapaneseEggArtisansIntegrationPlan integration)
    {
        ArgumentNullException.ThrowIfNull(usa);ArgumentNullException.ThrowIfNull(integration);
        if(usa.Revision!=NativeDiscRevision.UsaScus94228||!integration.SourceCertified||
            !usa.ReadExecutableRange(0,0x66000).AsSpan().SequenceEqual(integration.CopyOriginalExecutable()))
            throw new InvalidDataException("Render admission requires the actual exact-USA source and authenticated integration ancestry.");
        var result=new NativeJapaneseEggRenderAdmissionPlan(integration,true);usa.VerifyUnchanged();return result;
    }
    internal static NativeJapaneseEggRenderAdmissionPlan Fixture(NativeJapaneseEggArtisansIntegrationPlan integration)=>new(integration,false);
    private static uint Word(ReadOnlySpan<byte> b,int at)=>BinaryPrimitives.ReadUInt32LittleEndian(b[at..]);
    private static string Hash(ReadOnlySpan<byte> b)=>Convert.ToHexStringLower(SHA256.HashData(b));
}
