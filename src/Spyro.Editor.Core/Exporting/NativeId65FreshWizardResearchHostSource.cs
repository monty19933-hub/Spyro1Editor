using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Scene;

[assembly: InternalsVisibleTo("Spyro.Editor.NativeId65FreshWizardHostSmoke")]

namespace Spyro.Editor.Core.Exporting;

/// <summary>Actual scene/resource ancestry and fixed original-source capture only.</summary>
internal sealed class NativeId65FreshWizardResearchHostSource
{
    private NativeId65FreshWizardResearchHostSource(NativeId65FreshWizardResearchScenePlan scene,
        NativeId65FreshWizardResearchFallbackResourcePlan fallback,byte[] town,byte[] wizard,byte[] exe)
    {
        RequireAncestry(scene,fallback);
        if(!exe.AsSpan().SequenceEqual(scene.CompilationAncestor.FreshResourceAncestor.ResourceChainAncestor.Resources.Surface.CopySourceExecutable())||
            scene.FamilyAncestor.DonorLevelId!=33||scene.FamilyAncestor.DonorTrueIndex!=6||
            NativeId65FreshWizardHostLayout.Hash(scene.FamilyAncestor.CopyOwner("wizard.row"))!="59401eafeab453fd9d26f14eedca58390653dc5f4fcc3d2560f7b3e8fc7afa82"||
            NativeId65FreshWizardHostLayout.Hash(scene.FamilyAncestor.CopyOwner("wizard.private"))!="5b3d762f6117ea30e959a21bf2778f3442b03eba93125c9a0e0a642cc58d345b")
            throw new InvalidDataException("Host lost actual Compilation/33-T6 source family ancestry.");
        Scene=scene;Fallback=fallback;Layout=NativeId65FreshWizardHostLayout.Compile(town,wizard,exe);
    }
    internal NativeId65FreshWizardResearchScenePlan Scene{get;}
    internal NativeId65FreshWizardResearchFallbackResourcePlan Fallback{get;}
    internal NativeId65FreshWizardHostLayout Layout{get;}
    internal static void RequireAncestry(NativeId65FreshWizardResearchScenePlan scene,NativeId65FreshWizardResearchFallbackResourcePlan fallback)
    {
        scene.RequireAncestry(scene.CompilationAncestor,scene.FamilyAncestor,scene.ResourceAncestor);
        fallback.RequireAncestry(scene.ResourceAncestor);
        scene.ResourceAncestor.RequireCompilationProfile();
        scene.RequireExactScene(scene.CopyScene());
        fallback.RequireExactOutputs(fallback.CopyUploadPayload(),fallback.CopyDataComponents(),fallback.CopyModelPayload());
        if(scene.PlacedCount!=1||scene.NativeNominalCapacity!=32||scene.SourceImageSha256!=fallback.SourceImageSha256||
            scene.CanonicalDocumentSha256!=fallback.CanonicalDocumentSha256)
            throw new InvalidDataException("Host needs actual fixed A/P1/N32 and same R0 fallback ancestry.");
    }
    internal static NativeId65FreshWizardResearchHostSource Capture(FileStream image,NativeId65FreshWizardResearchScenePlan scene,
        NativeId65FreshWizardResearchFallbackResourcePlan fallback,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();var layout=DiscImage.DetectLayout(image);
        var wad=DiscImage.FindRootFileRecord(image,layout,n=>n.Equals("WAD.WAD",StringComparison.OrdinalIgnoreCase));
        var exeRecord=DiscImage.FindRootFileRecord(image,layout,n=>n.Equals("SCUS_942.28",StringComparison.OrdinalIgnoreCase));
        if(layout.SectorSize!=2352||layout.UserOffset!=24||wad.Lba!=37||wad.Size!=0x6927000||exeRecord.Lba!=53875||exeRecord.Size!=0x66000)
            throw new InvalidDataException("Host requires exact original physical source owners.");
        byte[] directory=Read(37,0,0x800);
        NativeId65FreshWizardHostLayout.Require(directory,0x800,"15d3e9c45f07e27a0ddd3f3a49b23a077e2c7750021cc89c55b7b16c551186cd");
        if(NativeId65FreshWizardHostLayout.Word(directory,15*8)!=0x118E800||NativeId65FreshWizardHostLayout.Word(directory,15*8+4)!=0xF800||
            NativeId65FreshWizardHostLayout.Word(directory,39*8)!=0x2F5B000||NativeId65FreshWizardHostLayout.Word(directory,39*8+4)!=0x10000)
            throw new InvalidDataException("Town/Wizard overlay native directory entry changed.");
        return new(scene,fallback,Read(37,0x118E800,0xF800),Read(37,0x2F5B000,0x10000),Read(53875,0,0x66000));
        byte[] Read(int lba,long at,int n)
        {token.ThrowIfCancellationRequested();var b=DiscImage.ReadFileBytes(image,layout,lba,at,n);if(b.Length!=n)throw new InvalidDataException("Incomplete Host source read.");token.ThrowIfCancellationRequested();return b;}
    }
}

/// <summary>Neutral closed byte layout. Caller buffers can never issue the public Host capability.</summary>
internal sealed class NativeId65FreshWizardHostLayout
{
    internal const uint Base=0x8007AA38;
    private readonly byte[] _overlay,_initializer;
    private static readonly (string Package,uint Pc,int Length,string Hash)[] Specs=[
        ("main",0x8007D118,0x1C,"cb6c9b201f8e3bae8282fe1805342ed76ae068cc5538de7d027e68f13c80badb"),
        ("main",0x8007EAD0,0x1A0,"1c0de3f70b36876cbf526730df0219c206f9e5d5621551bd8abb062c83609fe5"),
        ("main",0x80082818,0x668,"fd6623d1a6520736d6fd684a1c72b00a5a01727be764f7395cbe75c466af6007"),
        ("main",0x800842CC,0x24,"749bb5e1e951b80c87b1b0ec16abeeb9e98b4f08ed097739df571f5ea5d13b3f"),
        ("main",0x8008434C,0x14,"6da3bd3db238aa4de38d5d16a9a61b0c2799a01d05c2071aa409abe3857e3cad"),
        ("main",0x80085A4C,0x4C,"3beab1570e37a77aae8d809b43dfa7e596057795f1bb45e0d4ecb857642ff04a"),
        ("main",0x80086A50,0x10,"5a4a9b563d9e2e78be98a498afcead4373655c5eb8044e84bd697f1c310cb172"),
        ("spawn26",0x80086DD8,0x94,"92b99a51a4ea53531a711626d9aa1d8f7fc7a5be120cfbf14255f95c68b080b4"),
        ("spawn26",0x800870F0,0x230,"31e3328b9f94ee5fc424bb4f566048b8a40efacbd99ed634d6cbe70bb3fd97b4"),
        ("spawn26",0x80087B08,0x38,"731646e82a5a26b929aff89ad7ae822be66c7fd56b920fb9eb8a7d83ee4f571a"),
        ("particles",0x80088370,0x98,"a2f5681329e63ab489abec1424ecc230f462f4af53bfc5d0e0ae94bb5a09afc4"),
        ("particles",0x800888E8,0xFC,"bdfa5cf0752d3ca410a216f83a31d196492bd22de836ccfba9255582a5c01a19"),
        ("particles",0x80089934,0x11C,"c3e0db5def4d5c5c63c10b3e8acf314ebabfa91c93f8a2087a268e78da2df6e3"),
        ("particles",0x80089F28,0xD0,"fbff816f4c054e52e663d05b0c170db721b3931ce7baac3bd93340837db075d8"),
        ("particles",0x800893D0,0x1C,"4dc69c85706e33ca50541a44b0c16548562367f214618868fcc31d6b93b4214f")];
    private NativeId65FreshWizardHostLayout(ReadOnlySpan<byte> townBytes,ReadOnlySpan<byte> wizardBytes,ReadOnlySpan<byte> exeBytes)
    {
        // All sizes precede hashing/cloning and codec access.
        if(townBytes.Length!=0xF800||wizardBytes.Length!=0x10000||exeBytes.Length!=0x66000)
            throw new InvalidDataException("Host needs complete bounded original Town/Wizard/EXE owners.");
        Require(townBytes,0xF800,"9bc923cc8d27703537b81b01f51fde351e58aabee87d38a8afc630f1031878a5");
        Require(wizardBytes,0x10000,"5d3996111567ffac6eca08d340f2f691824bb7969ba2a0cd95e9346ccfe157e1");
        Require(exeBytes,0x66000,"a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998");
        byte[] town=townBytes.ToArray(),wizard=wizardBytes.ToArray(),exe=exeBytes.ToArray();
        Require(town.AsSpan(0x248,0x288),0x288,"72901b134a94cfd71541e23cda95902a16814bb887bfe6b16e215fe6280b202e");
        Require(town.AsSpan(0x30FC,0x264),0x264,"0ffb33d9b0b797e469fb2a1003c1736e28ee380b42738f40f127d8e1a5f00bf4");
        Require(exe.AsSpan(0x5A470-0xF800,0x30),0x30,"7dba88624efe091d8b34a22b8874a2d12ab577858f7583a324d82f11d14d5c4f");
        Require(exe.AsSpan(0x155E4-0xF800,0x18),0x18,"c53e59924d4cb0ce0816bad8028aa1737121c1a3ce4d193965177301fedca31a");
        Require(exe.AsSpan(0x4BEE0,0xA0),0xA0,"f1d94157410333d6640a99043f9e175f6a41c2150a5bfa47028fdf4f54626be4");
        if(Word(exe,0x1BA4+13*4)!=0x8005A898||Word(exe,0x1CA8)!=0x8005B6E0)
            throw new InvalidDataException("Original Town/ID65 initializer dispatch owner changed.");
        var donorDomain=new NativeAddressSpace(NativeAddressKind.LevelData,"actual-Wizard-overlay",NativeAddressRevision.Original,wizard.Length);
        var exeDomain=new NativeAddressSpace(NativeAddressKind.ExecutableFile,"actual-original-exe",NativeAddressRevision.Original,exe.Length);
        byte[] Read(NativeByteExtent e)
        {
            byte[] b=e.Space==donorDomain?wizard:e.Space==exeDomain?exe:throw new InvalidDataException("Unbound Host source domain.");
            return b.AsSpan(checked((int)e.Offset),checked((int)e.ByteLength)).ToArray();
        }
        NativeMipsBehaviorFragmentPackage Capture(string id,uint[] entries,uint[] exits)=>NativeMipsBehaviorFragmentCodec.Capture(
            Specs.Where(s=>s.Package==id).Select(s=>new NativeMipsBehaviorFragmentSource($"{id}:{s.Pc:X8}",s.Pc,
                new("actual-Wizard",new(donorDomain,s.Pc-Base,s.Length),s.Hash))),entries,exits,Read);
        var main=Capture("main",[0x8007EAD0,0x80082818],[0x80086D8C]);
        var spawn=Capture("spawn26",[0x80086DD8,0x800870F0],[]);
        foreach(var caller in new (uint Pc,ushort Class,uint At,int N)[]{
            (0x8007EBDC,0x26,0x8007EBC8,0x1C),(0x80082DB4,0x11B,0x80082DA4,0x18),
            (0x80082DF0,0x11B,0x80082DDC,0x1C),(0x80082E38,0x11B,0x80082E24,0x1C)})
        {
            NativeDarkHollowWizardSpawn26Specialization.ValidateCallerProof(caller.Pc,caller.Class,wizard.AsSpan(checked((int)(caller.At-Base)),caller.N));
            if(!main.Transfers.Any(t=>t.SourcePc==caller.Pc&&t.Kind==NativeMipsBehaviorTransferKind.CallbackRegister))throw new InvalidDataException("Private class26 caller lost its actual native fragment.");
        }
        Require(wizard.AsSpan(checked((int)(0x80086E64-Base)),0x60),0x60,"53350a2947162a226dc7cdf3477dcb8d72af307891686b09cc738ea513c5c924");
        if(Word(exe,checked((int)(0x8003A720-0x8000F800)))!=0x24020010||!wizard.AsSpan(checked((int)(0x800870F0-Base)),12).SequenceEqual(Convert.FromHexString("0000728ec8e9000c21206002")))
            throw new InvalidDataException("Private class26 dead-v0/first-overwrite source proof changed.");
        var sf=spawn.Fragments.ToArray();var first=sf[0];byte[] specialized=first.CopyBytes();
        NativeDarkHollowWizardSpawn26Specialization.RewriteNativeDispatch(specialized.AsSpan(0x8C,8)).CopyTo(specialized,0x8C);
        sf[0]=new(first.Source,specialized,false);
        spawn=new(sf,spawn.Transfers.Where(t=>t.SourcePc!=0x80086E64&&t.SourcePc!=0x80086E6C)
            .Append(new(0x80086E64,0x800870F0,true,NativeMipsBehaviorTransferKind.Jump26,0x08021C3C,0xA2630056)).OrderBy(t=>t.SourcePc).ToArray(),
            spawn.StackAccesses,[0x80086DD8],[],Read);
        var capturedParticles=Capture("particles",[0x80088370,0x800888E8,0x80089934,0x80089F34],[0x800893EC,0x800893F4,0x8008A848]);
        SortedParticleSourcePcs=Array.AsReadOnly(capturedParticles.Fragments.Select(f=>f.Source.SourcePc).ToArray());
        uint[] order=Specs.Where(s=>s.Package=="particles").Select(s=>s.Pc).ToArray();
        if(!SortedParticleSourcePcs.SequenceEqual(order.Order())||SortedParticleSourcePcs.SequenceEqual(order))throw new InvalidDataException("Generic sorted source capture and closed particle order must remain distinct.");
        var particles=new NativeMipsBehaviorFragmentPackage(order.Select(pc=>capturedParticles.Fragments.Single(f=>f.Source.SourcePc==pc)).ToArray(),
            capturedParticles.Transfers,capturedParticles.StackAccesses,capturedParticles.EntryPcs,capturedParticles.InterceptedExits,Read);
        if(particles.Fragments.Count(f=>f.HasFallthrough)!=1||!particles.Fragments[^1].HasFallthrough||particles.Fragments[^1].Source.SourcePc!=0x800893D0)
            throw new InvalidDataException("Exact condition fallthrough must remain a generated eight-byte thunk.");
        var packages=new[]{("main",main),("spawn26",spawn),("particles",particles)};
        var sizes=new List<NativeOwnedOverlayBlockSize>();
        foreach(var (id,p) in packages)sizes.Add(new(id,p.Fragments.Sum(f=>f.ByteLength+(f.HasFallthrough?8:0))));
        foreach(string id in new[]{"main-leave","tick-next-leave","tick-delete-leave","spawn-leave"})sizes.Add(new(id,0x98));
        foreach(string id in new[]{"main26","main11B","tick07","tick41"})sizes.Add(new(id,0x9C));
        foreach(string id in new[]{"spawn07","spawn41"})sizes.Add(new(id,0xA0));
        sizes.Add(new("effect-call",8));sizes.Add(new("dispatch",0x44));
        AppendLayout=NativeOwnedOverlayAppendLayout.Create("detached-fresh-Town-Host",town,Base,0x11000,sizes,"padding");
        int used=AppendLayout.UsedByteLength,finalLength=AppendLayout.FinalByteLength;
        var blocks=AppendLayout.Blocks.Select(b=>(b.Id,At:checked((int)b.Owner.Offset),N:checked((int)b.Owner.ByteLength))).ToArray();
        if(used!=0x10DB4||finalLength!=0x11000||blocks.Length!=16)throw new InvalidDataException("Closed Host ownership budget changed.");
        var byId=blocks.ToDictionary(b=>b.Id);var mapping=new Dictionary<uint,uint>();var fragments=new List<NativeFreshWizardHostFragment>();
        foreach(var (id,p) in packages)
        {
            int at=byId[id].At;
            foreach(var f in p.Fragments)
            {
                fragments.Add(new(id,f.Source.SourcePc,f.ByteLength,f.Source.Read.ExpectedSha256,at,f.HasFallthrough));
                for(int i=0;i<f.ByteLength;i+=4)mapping.Add(f.Source.SourcePc+(uint)i,Base+(uint)(at+i));
                at+=f.ByteLength+(f.HasFallthrough?8:0);
            }
        }
        var finalDomain=AppendLayout.Domain;
        uint Pc(string id)=>Base+(uint)byId[id].At;
        NativeByteExtent Entry(uint pc)=>new(finalDomain,pc-Base,4);
        var payloads=new Dictionary<string,byte[]>();var transfers=new List<NativeFreshWizardHostTransfer>();var reservations=new List<NativeByteExtent>();
        foreach(var (id,p) in packages)
        {
            var bindings=new List<NativeMipsBehaviorExternalBinding>();
            foreach(var t in p.Transfers)
            {
                if(t.Kind==NativeMipsBehaviorTransferKind.ReturnRegister||t.TargetPc is uint own&&p.OwnsPc(own))continue;
                if(t.Kind==NativeMipsBehaviorTransferKind.Call26)
                {
                    if(t.TargetPc is not uint global||global<0x80010000||global>=0x80075800)throw new InvalidDataException("Unowned original executable call.");
                    bindings.Add(new(t.SourcePc,global,global,new(exeDomain,global-0x8000F800,4),new("actual-exe",new(exeDomain,0,exe.Length),Hash(exe)),0x8000F800));continue;
                }
                uint target;
                if(t.Kind==NativeMipsBehaviorTransferKind.CallbackRegister)
                    target=t.SourcePc switch{0x8007EBDC or 0x80082DB4 or 0x80082DF0 or 0x80082E38=>mapping[0x80086DD8],
                        0x8007EC60 or 0x80082840 or 0x8008728C=>Pc("effect-call"),_=>throw new InvalidDataException("Unknown Host callback site.")};
                else target=t.TargetPc switch{0x80086D8C=>Pc("main-leave"),0x800893EC=>Pc("tick-delete-leave"),
                    0x800893F4=>Pc("tick-next-leave"),0x8008A848=>Pc("spawn-leave"),_=>throw new InvalidDataException("Unknown Host continuation.")};
                bindings.Add(new(t.SourcePc,t.TargetPc,target,Entry(target)));
            }
            var owner=byId[id];var linked=p.Link(new(finalDomain,owner.At,finalLength-owner.At),Pc(id),bindings);byte[] b=linked.CopyBytes();
            if(b.Length!=owner.N||linked.SourceToFinalPc.Any(k=>mapping[k.Key]!=k.Value))throw new InvalidDataException("Codec source/final map changed.");
            payloads.Add(id,b);reservations.AddRange(linked.UnmaterializedExternalTargets);
            foreach(var t in p.Transfers.Where(t=>t.Kind!=NativeMipsBehaviorTransferKind.ReturnRegister))
            {
                uint finalPc=t.Kind==NativeMipsBehaviorTransferKind.Fallthrough?mapping[t.SourcePc-4]+4:mapping[t.SourcePc];
                int at=checked((int)(finalPc-Base)),local=at-owner.At;uint after=Word(b,local);
                uint target=DecodeTarget(after,finalPc);uint? before=t.Kind==NativeMipsBehaviorTransferKind.Fallthrough?null:Word(wizard,checked((int)(t.SourcePc-Base)));
                if(Word(b,local+4)!=t.DelayWord)throw new InvalidDataException("Retained native delay word changed.");
                transfers.Add(new(id,t.Kind.ToString(),t.SourcePc,t.TargetPc,at,finalPc,target,before,after,t.DelayWord));
            }
        }
        foreach(var (id,target) in new (string,uint)[]{("main-leave",0x80087258),("tick-next-leave",0x80088F20),("tick-delete-leave",0x80088F18),("spawn-leave",0x80089E74)})payloads[id]=Leave(target);
        var roles=new[]{new NativeFreshWizardHostRole("main26",Pc("main26"),0x8007EAD0),new("main11B",Pc("main11B"),0x80082818),
            new("tick07",Pc("tick07"),0x80088370),new("tick41",Pc("tick41"),0x800888E8),new("spawn07",Pc("spawn07"),0x80089934),new("spawn41",Pc("spawn41"),0x80089F34)};
        foreach(var role in roles)payloads[role.Id]=Adapter(role.Id.StartsWith("main",StringComparison.Ordinal)?"main":role.Id.StartsWith("tick",StringComparison.Ordinal)?"tick":"spawn",mapping[role.SourcePc]);
        payloads["effect-call"]=Bytes([Jump(0x80088F68),0]);
        payloads["dispatch"]=Bytes([I(9,0,2,0x26),I(5,4,2,3),0,Jump(Pc("main26")),0,I(9,0,2,0x11B),I(5,4,2,3),0,Jump(Pc("main11B")),0,
            0x28820101,I(5,2,0,3),0x288200FF,Jump(0x8007DC70),0,Jump(0x8007DB40),0]);
        // Declarations come from captured package roots and these code-owned
        // adapter/thunk roles, never from a requested reservation or an arbitrary
        // interior instruction. Materialization discharges the separate requests.
        var entries=new List<NativeOwnedOverlayEntry>();
        foreach(var (id,p) in packages)
            foreach(uint sourcePc in p.EntryPcs)entries.Add(new($"{id}:{sourcePc:X8}",id,Entry(mapping[sourcePc])));
        foreach(var role in roles)entries.Add(new(role.Id,role.Id,Entry(role.RuntimePc)));
        foreach(string id in new[]{"main-leave","tick-next-leave","tick-delete-leave","spawn-leave","effect-call","dispatch"})
            entries.Add(new(id,id,Entry(Pc(id))));
        _overlay=AppendLayout.Materialize(AppendLayout.Blocks.Where(b=>!b.IsPadding)
            .Select(b=>new NativeOwnedOverlayBlock(b.Id,b.Owner,payloads[b.Id])),entries,reservations);
        var owners=AppendLayout.Blocks.Select(b=>new NativeWizardOverlayCodeOwner(b.Id,checked((int)b.Owner.Offset),
            Base+checked((uint)b.Owner.Offset),checked((int)b.Owner.ByteLength),
            Hash(_overlay.AsSpan(checked((int)b.Owner.Offset),checked((int)b.Owner.ByteLength))))).ToList();
        var patches=new List<NativeFreshWizardHostPatch>();
        void Patch(string id,int at,byte[] after)
        {var patch=new NativeFreshWizardHostPatch(id,at,Base+(uint)at,town.AsSpan(at,after.Length),after);after.CopyTo(_overlay,at);patches.Add(patch);}
        if(Word(town,0x30FC)!=0x28820101||Word(town,0x3100)!=0x1040004D)throw new InvalidDataException("Original Town compare entry changed.");
        Patch("class-dispatch",0x30FC,Bytes([Jump(Pc("dispatch")),0]));
        foreach(var (at,id,before) in new (int,string,uint)[]{(0x264,"tick07",0x80088F20),(0x34C,"tick41",0x80088F20),(0x3A8,"spawn07",0x80089E74),(0x490,"spawn41",0x80089E74)})
        {if(Word(town,at)!=before)throw new InvalidDataException("Original Town default particle table changed.");Patch(id,at,Bytes([Pc(id)]));}
        byte[] townInverse=_overlay[..0xF800];foreach(var patch in patches)patch.CopyBeforeBytes().CopyTo(townInverse,patch.Offset);
        if(!townInverse.SequenceEqual(town)||!_overlay.AsSpan(0xF494,0x36C).SequenceEqual(town.AsSpan(0xF494)))throw new InvalidDataException("Original Town inverse/tail changed.");
        foreach(var f in fragments)
        {
            byte[] inverse=_overlay.AsSpan(f.OverlayOffset,f.ByteLength).ToArray();
            foreach(var t in transfers.Where(t=>t.SourcePc>=f.SourcePc&&t.SourcePc<f.SourcePc+f.ByteLength))Put(inverse,checked((int)(t.SourcePc-f.SourcePc)),t.BeforeWord!.Value);
            if(!inverse.AsSpan().SequenceEqual(wizard.AsSpan(checked((int)(f.SourcePc-Base)),f.ByteLength)))throw new InvalidDataException("Full original donor fragment inverse/complement changed.");
        }
        _initializer=RewriteInitializer(exe.AsSpan(0x4B098,0x98),finalLength);
        InitializerPatch=new("conditional-resident-copy",0x4B098,0x8005A898,exe.AsSpan(0x4B098,0x98),_initializer);
        OverlaySha256=Hash(_overlay);InitializerSha256=Hash(_initializer);
        if(OverlaySha256!="ad6d952cbf70ab8b1eab53f7637ffd0235271b7b20696956c9406f36f714fa7e"||
            InitializerSha256!="d7f0e7c9ed86467f0749db689b2bf1f1c4df27587eb357821ef8947d7f70466b"||
            mapping.Count!=980||transfers.Count!=168||fragments.Count!=15||patches.Sum(p=>p.ByteLength)!=24)
            throw new InvalidDataException("Independent full Host/initializer golden or complete field budget changed.");
        Owners=owners.AsReadOnly();Roles=Array.AsReadOnly(roles);Fragments=fragments.AsReadOnly();Transfers=transfers.AsReadOnly();
        TownPatches=patches.AsReadOnly();Mapping=new ReadOnlyDictionary<uint,uint>(mapping);
    }
    internal bool SourceCertified=>false;
    internal NativeOwnedOverlayAppendLayout AppendLayout{get;}
    internal string OverlaySha256{get;}
    internal string InitializerSha256{get;}
    internal IReadOnlyList<uint> SortedParticleSourcePcs{get;}
    internal IReadOnlyList<NativeWizardOverlayCodeOwner> Owners{get;}
    internal IReadOnlyList<NativeFreshWizardHostRole> Roles{get;}
    internal IReadOnlyList<NativeFreshWizardHostFragment> Fragments{get;}
    internal IReadOnlyList<NativeFreshWizardHostTransfer> Transfers{get;}
    internal IReadOnlyList<NativeFreshWizardHostPatch> TownPatches{get;}
    internal IReadOnlyDictionary<uint,uint> Mapping{get;}
    internal NativeFreshWizardHostPatch InitializerPatch{get;}
    internal byte[] CopyOverlay()=>_overlay.ToArray();
    internal byte[] CopyInitializer()=>_initializer.ToArray();
    internal void RequireExact(ReadOnlySpan<byte> overlay,ReadOnlySpan<byte> initializer)
    {if(!overlay.SequenceEqual(_overlay)||!initializer.SequenceEqual(_initializer))throw new InvalidDataException("Complete Host/initializer owner or complement changed.");}
    internal static NativeId65FreshWizardHostLayout Compile(ReadOnlySpan<byte> town,ReadOnlySpan<byte> wizard,ReadOnlySpan<byte> exe)=>new(town,wizard,exe);
    internal static byte[] RewriteInitializer(ReadOnlySpan<byte> original,int overlayLength)
    {
        if(overlayLength!=0x11000)throw new InvalidDataException("Only actual closed Host padded length selects its copy anchor.");
        Require(original,0x98,"fe4669229aedbbc8ff698d57a237327af36025aaeda0ff5cc2e1c806fd23e50b");
        uint target=Base+(uint)overlayLength,high=(target+0x8000)>>16;List<uint> words=[0x3C018007,0x8C22596C,0x3C018008,0x2442FFBF,
            0x14400004,0x3C028009,0x3C020000|(high&65535),0x10000002,0x24420000|(target&65535),0x24429ECC,0xAC2285D8,0x3C018007];
        for(int at=0x10;at<0x90;at+=0x10)
        {if(Word(original,at+8)!=0x3C018007)throw new InvalidDataException("Shared original callback base changed.");words.AddRange([Word(original,at),Word(original,at+4),Word(original,at+12)]);}
        words.AddRange([Word(original,0x90),Word(original,0x94)]);
        if(words.Count!=38||words[^2]!=0x08016DB8||words[^1]!=0)throw new InvalidDataException("Original resident default-tail continuation changed.");return Bytes(words);
    }
    private static readonly int[] Registers=Enumerable.Range(1,31).Where(r=>r!=29).ToArray();
    private static byte[] Adapter(string role,uint target)
    {
        List<uint> w=[I(9,29,29,-0x2B0)];for(int i=0;i<Registers.Length;i++)w.Add(I(0x2B,29,Registers[i],0x230+i*4));
        w.AddRange([0x4010,I(0x2B,29,8,0x2A8),0x4012,I(0x2B,29,8,0x2AC)]);
        if(role=="main")w.AddRange([Move(23,30),I(9,0,30,1)]);
        else if(role=="tick")w.AddRange([Move(21,18),Move(18,16)]);
        else if(role=="spawn")foreach(var (to,from) in new[]{(22,21),(21,23),(23,22)})w.Add(I(0x23,29,to,0x230+Array.IndexOf(Registers,from)*4));
        else throw new InvalidDataException("Unknown closed Town role.");w.AddRange([Jump(target),0]);return Bytes(w);
    }
    private static byte[] Leave(uint target)
    {
        List<uint> w=[I(0x23,29,8,0x2A8),0,0x01000011,I(0x23,29,8,0x2AC),0,0x01000013];
        for(int i=0;i<Registers.Length;i++)w.Add(I(0x23,29,Registers[i],0x230+i*4));w.AddRange([Jump(target),I(9,29,29,0x2B0)]);return Bytes(w);
    }
    private static uint I(int op,int rs,int rt,int imm)=>(uint)(op<<26|rs<<21|rt<<16)|(ushort)(short)imm;
    private static uint Move(int to,int from)=>(uint)(from<<21|to<<11|0x21);
    private static uint Jump(uint pc)
    {if(pc%4!=0||pc<0x80000000||pc>=0x80200000)throw new InvalidDataException("Unaligned/unowned recipe target.");return 0x08000000|((pc>>2)&0x3FFFFFF);}
    private static uint DecodeTarget(uint w,uint pc)
    {uint op=w>>26;if(op is 2 or 3)return((pc+4)&0xF0000000)|((w&0x3FFFFFF)<<2);if(op is 1 or 4 or 5 or 6 or 7)return unchecked(pc+4+(uint)((short)w*4));throw new InvalidDataException("Unencoded native transfer.");}
    private static byte[] Bytes(IEnumerable<uint> words)
    {uint[] array=words.ToArray();byte[] b=new byte[array.Length*4];for(int i=0;i<array.Length;i++)Put(b,i*4,array[i]);return b;}
    internal static uint Word(ReadOnlySpan<byte> b,int at)=>BinaryPrimitives.ReadUInt32LittleEndian(b[at..]);
    private static void Put(Span<byte> b,int at,uint word)=>BinaryPrimitives.WriteUInt32LittleEndian(b[at..],word);
    internal static string Hash(ReadOnlySpan<byte> b)=>Convert.ToHexStringLower(SHA256.HashData(b));
    internal static void Require(ReadOnlySpan<byte> b,int length,string hash)
    {if(b.Length!=length||Hash(b)!=hash)throw new InvalidDataException("Complete Host source owner/pin changed.");}
}
