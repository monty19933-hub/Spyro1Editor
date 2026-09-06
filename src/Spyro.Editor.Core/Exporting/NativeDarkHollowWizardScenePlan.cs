using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Exporting;

/// <summary>Source-owned diagnostic scene stage, not native behavior or publication approval.</summary>
public sealed class NativeDarkHollowWizardScenePlan
{
    private const int SceneOffset=0x194000,SceneLength=0x11800;
    private readonly byte[] _originalScene,_originalLevel,_finalLevel;
    private readonly NativeMobySceneAppendPlan _plan;
    private NativeDarkHollowWizardScenePlan(byte[] dark,byte[] wizard,byte[] executable)
    {
        Require(dark,"a937aecb3626ffd2311ba136684a34c9f36877054b52557b4524fcacee0e2d97");
        Require(wizard,"9973493f865b5e86bc17b32c5af9e3d7596e25615618c007ed28e0379161f2d8");
        Require(executable,"a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998");
        if(Word(dark,0x18)!=SceneOffset||Word(dark,0x1C)!=SceneLength||Word(wizard,0x18)!=0x1CE800||Word(wizard,0x1C)!=SceneLength)
            throw new InvalidDataException("Native scene descriptor owner changed.");
        _originalLevel=dark.ToArray();_originalScene=dark.AsSpan(SceneOffset,SceneLength).ToArray();
        byte[] donor=wizard.AsSpan(0x1CE800,SceneLength).ToArray();
        Require(_originalScene,"c48c344d3809f1ea5761e968ce6f18b02d5c7bbb82fcec4a7fd453343dbb84f8");
        Require(donor,"1e204311534f03f09804d55ca833c62a1a3cc3665dc21d88de743d820103a951");
        Require(donor.AsSpan(0x6388,0x58),"59401eafeab453fd9d26f14eedca58390653dc5f4fcc3d2560f7b3e8fc7afa82");
        Require(donor.AsSpan(0xC350,0x50),"5b3d762f6117ea30e959a21bf2778f3442b03eba93125c9a0e0a642cc58d345b");
        Require(executable.AsSpan(0x42CC4,0xA0),"f3f404687b98b5208f97274a1932c967ba8d0bd04337011b53d831f25f592335");
        Require(executable.AsSpan(0x46F4,0x168),"7bfb42fba56df4b4772f4a32f0114940090e0f4029456ed37ea8ac5e871be6d7");
        NativeAddressSpace original=new(NativeAddressKind.Scene,"dark-scene:"+Hash(_originalScene),NativeAddressRevision.Original,SceneLength);
        NativeAddressSpace donorSpace=new(NativeAddressKind.Scene,"wizard-scene:"+Hash(donor),NativeAddressRevision.Original,SceneLength);
        NativeAddressSpace exe=new(NativeAddressKind.ExecutableFile,"retail-exe:"+Hash(executable),NativeAddressRevision.Original,executable.Length);
        NativeAddressSpace final=new(NativeAddressKind.Scene,"dark-wizard-owned-scene-v1",NativeAddressRevision.Final,SceneLength);
        Dictionary<NativeAddressSpace,byte[]> sources=new(){[original]=_originalScene,[donorSpace]=donor,[exe]=executable};
        byte[] Read(NativeByteExtent extent)=>sources.TryGetValue(extent.Space,out var bytes)
            ?bytes.AsSpan(checked((int)extent.Offset),checked((int)extent.ByteLength)).ToArray()
            :throw new InvalidDataException("Unbound native scene source address domain.");
        NativeMobyDependencyRead R(NativeAddressSpace domain,int offset,int length)=>new("native",new(domain,offset,length),Hash(Read(new(domain,offset,length))));
        var source=NativeMobySceneAppendSource.Capture(original,Hash(_originalScene),Read);
        if(source.PlacedCount!=128||source.RowTableOffset!=0x8C38||source.PropertiesOffset!=0xE038||
            source.PropertiesEnd!=0xEED4||source.FixupOffset!=0x10EF0||source.FixupCount!=147||source.DynamicCapacity!=91)
            throw new InvalidDataException("Native Dark scene component/fixup/pool schema changed.");
        for(int i=0;i<128;i++)if(BinaryPrimitives.ReadUInt16LittleEndian(_originalScene.AsSpan(0x8C38+i*0x58+0x36))==0x11B)
            throw new InvalidDataException("Native Dark unexpectedly already owns a Wizard instance.");
        // These are actual donor pointer-fixup obligations, not a caller's list
        // of convenient zeros. The row remains pod-detached (FF), and both route
        // positions remain donor coordinates: this is NOT placement approval.
        if(Word(donor,0x6388)!=0xC350||Word(donor,0xC350)!=0xC378||donor[0x6388+0x43]!=0xFF||Word(donor,0xFC3C)!=191)
            throw new InvalidDataException("Native Wizard private pointer/pod/fixup schema changed.");
        int[] donorFixups=Enumerable.Range(0,191).Select(i=>checked((int)Word(donor,0xFC40+i*4))).ToArray();
        if(donorFixups.Count(f=>f==0x6388)!=1||!donorFixups.Where(f=>f>=0xC350&&f<0xC3A0).SequenceEqual(new[]{0xC350}))
            throw new InvalidDataException("Native Wizard source has an unclosed private pointer obligation.");
        List<NativeMobyDependencyNode> schema=[];
        AddSchema("scene-schema.dynamic-allocator",exe,0x42CC4,0xA0);
        AddSchema("scene-schema.native-loader",exe,0x46F4,0x168);
        AddSchema("scene-schema.donor-fixups",donorSpace,0xFC3C,4+191*4);
        var shared=NativeMobyDependencyClosureCompiler.Compile(schema,schema.Select(n=>n.Id),Read);
        var template=NativeMobyDependencyInstanceTemplate.Capture("native-dark-wizard-scene-v1",
            [new("wizard",6,0x11B,R(donorSpace,0x6388,0x58),R(donorSpace,0xC350,0x50),0,[],true)],[],shared,Read);
        _plan=NativeMobySceneAppendPlan.Compile(source,final,[final],exe,
            [new(new("wizard-128",template),[new("wizard",0,0x28)])],Read);
        if(_plan.PlacedCountAfter!=129||_plan.FixupCountAfter!=149||_plan.DynamicCapacityAfter!=90||
            _plan.Allocation.Rows.Single().Row.Offset!=0xB838||_plan.Allocation.Rows.Single().Properties.Offset!=0xEED4)
            throw new InvalidDataException("Native Dark appended private ownership accounting changed.");
        _finalLevel=dark.ToArray();_plan.CopyScene().CopyTo(_finalLevel,SceneOffset);
        SourceDataSha256=Hash(dark);DonorDataSha256=Hash(wizard);FinalLevelSha256=Hash(_finalLevel);
        return;
        void AddSchema(string id,NativeAddressSpace domain,int offset,int length)
        {
            var read=R(domain,offset,length);
            schema.Add(new(id,NativeMobyDependencyKind.NativeRoutine,NativeMobyDependencyResolution.ImportFromSource,
                NativeMobyDependencySharing.Immutable,read.ExpectedSha256,[],[read],c=>new(c.Read("native"),[],[])));
        }
    }
    public static NativeDarkHollowWizardScenePlan Capture(string imagePath)
    {
        using var image=new FileStream(imagePath,FileMode.Open,FileAccess.Read,FileShare.Read);
        RequireOriginal(image);var layout=DiscImage.DetectLayout(imagePath);
        if(layout.SectorSize!=2352||layout.UserOffset!=24)throw new InvalidDataException("Native scene issuer requires original MODE2/2352 geometry.");
        byte[] header=DiscImage.ReadFileBytes(image,layout,37,0,0x800);
        if(Word(header,14*8)!=0xF01000||Word(header,14*8+4)!=0x28D800||Word(header,40*8)!=0x2F6B000||Word(header,40*8+4)!=0x28A000)
            throw new InvalidDataException("Native Dark/Wizard data WAD owners changed.");
        byte[] dark=DiscImage.ReadFileBytes(image,layout,37,0xF01000,0x28D800);
        byte[] wizard=DiscImage.ReadFileBytes(image,layout,37,0x2F6B000,0x28A000);
        byte[] executable=DiscImage.ReadFileBytes(image,layout,53875,0,0x66000);
        var result=new NativeDarkHollowWizardScenePlan(dark,wizard,executable);
        RequireOriginal(image);return result;
    }
    public string SourceImageSha256=>NativeMobySourceInventory.OriginalImageSha256;
    public string SourceDataSha256 { get; }
    public string DonorDataSha256 { get; }
    public int PlacedCountBefore=>_plan.PlacedCountBefore;
    public int PlacedCountAfter=>_plan.PlacedCountAfter;
    public int FixupCountAfter=>_plan.FixupCountAfter;
    public int DynamicCapacityBefore=>_plan.DynamicCapacityBefore;
    public int DynamicCapacityAfter=>_plan.DynamicCapacityAfter;
    public int SceneByteLength=>SceneLength;
    public bool BehaviorClosureProven=>false;
    public bool RuntimeFitProven=>false;
    public bool PublicationApproved=>false;
    public bool PlacementApproved=>false;
    public string FinalSceneSha256=>_plan.FinalSha256;
    public string FinalLevelSha256 { get; }
    public string GraphFingerprint=>_plan.Graph.Fingerprint;
    public byte[] CopyOriginalScene()=>_originalScene.ToArray();
    public byte[] CopyScene()=>_plan.CopyScene();
    public byte[] CopyOriginalLevel()=>_originalLevel.ToArray();
    public byte[] CopyScenePatchedLevel()=>_finalLevel.ToArray();
    public void VerifyOriginalAndFinal(ReadOnlySpan<byte> original,ReadOnlySpan<byte> final)=>_plan.Verify(original,final);
    internal NativeSkyRelocationPayload CopyOriginalAddressPayload(int patchIndex)=>
        new(patchIndex,37,0x1095000,14,0,SceneLength,_plan.CopyScene(),3,false);
    private static void RequireOriginal(FileStream image)
    {
        image.Position=0;
        if(image.Length!=NativeMobySourceInventory.OriginalImageByteLength||
            Convert.ToHexStringLower(SHA256.HashData(image))!=NativeMobySourceInventory.OriginalImageSha256||
            image.Length!=NativeMobySourceInventory.OriginalImageByteLength||image.Position!=NativeMobySourceInventory.OriginalImageByteLength)
            throw new InvalidDataException("Native scene issuer requires the stable exact original image preimage.");
    }
    private static void Require(ReadOnlySpan<byte> bytes,string expected)
    {if(Hash(bytes)!=expected)throw new InvalidDataException("Native scene source certificate preimage changed.");}
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static uint Word(ReadOnlySpan<byte> bytes,int offset)=>BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
}
