using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Editing;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Source-only binding of the actual tiled Body to its unchanged experimental
/// executable. Preserves the exact placed-child/Host chain and the parent's
/// target20 assumptions. Retained native consumer bytes and revised address
/// accounting do not certify complete loading, runtime, peak fit or publication.
/// </summary>
internal sealed class NativeFreshTiledFamilyExecutablePlan
{
    private const string Profile="source-bound-tiled-family-retained-experimental-executable-v1";
    private const int DataAt=0xE6000,DataLength=0x1000,SceneAt=0x102000,SceneLength=0x3800;
    private readonly byte[] _body,_priorBody,_executable,_original;

    private NativeFreshTiledFamilyExecutablePlan(NativeFreshTiledFamilyBodyPlan body,
        NativeFreshLockedChestExperimentalExecutable parent)
    {
        BodyAncestor=body;ParentExecutableAncestor=parent;RequireAncestry(body,parent);
        _body=body.CopyLevelData();_priorBody=PlacementAncestor.CopyLevelData();_executable=parent.CopyExecutable();
        body.RequireExactLevelData(_body);PlacementAncestor.RequireExactLevelData(_priorBody);
        parent.RequireExactExecutable(_executable);
        byte[] scene=body.CopyScene();PlacementAncestor.RequireExactScene(scene);
        if(_body.Length!=0x20F000||_priorBody.Length!=_body.Length||scene.Length!=SceneLength||
            Word(_body,8)!=DataAt||Word(_body,12)!=DataLength||Word(_body,24)!=SceneAt||Word(_body,28)!=SceneLength||
            !_body.AsSpan(SceneAt,SceneLength).SequenceEqual(scene)||!scene.AsSpan().SequenceEqual(PlacementAncestor.CopyScene())||
            !_body.AsSpan(0,DataAt).SequenceEqual(_priorBody.AsSpan(0,DataAt))||
            !_body.AsSpan(DataAt+DataLength).SequenceEqual(_priorBody.AsSpan(DataAt+DataLength))||
            !body.RevertExactLevelData(_body).AsSpan().SequenceEqual(_priorBody)||
            !_body.AsSpan(DataAt,0xCB8).SequenceEqual(TerrainAncestor.CopyUsedData())||
            _body.AsSpan(DataAt+0xCB8,0x348).IndexOfAnyExcept((byte)0)>=0)
            throw new InvalidDataException("Tiled executable requires the complete new Body, unchanged placed Scene and exact immediate placed-child inverse.");
        int cursor=0x800;
        for(int i=0;i<8;i++)
        {
            int at=Word(_body,i*8),length=Word(_body,i*8+4);
            if(at!=cursor||length<=0||length>_body.Length-at||
                Word(_priorBody,i*8)!=at||Word(_priorBody,i*8+4)!=length||
                i!=1&&!_body.AsSpan(at,length).SequenceEqual(_priorBody.AsSpan(at,length)))
                throw new InvalidDataException("Tiled executable lost a complete retained descriptor or non-data subfile owner.");
            cursor=checked(at+length);
        }
        if(cursor!=_body.Length)throw new InvalidDataException("Tiled executable Body descriptors omit a retained owner.");
        var memory=body.RuntimeMemory;
        if(memory.AnalyzedDataSha256!=Hash(_body)||memory.CopyBufferAddress!=HostAncestor.AppendLayout.CopyAnchor||
            memory.UsedLevelDataBytes!=0xCB8||memory.LevelDataSectorPaddingBytes!=0x348||
            memory.ModelBytesRead!=0x1B000||memory.ModelRamStart!=0x8008D6F0||memory.SceneBytesRead!=SceneLength||
            memory.SceneRamStart!=0x800A86F0||memory.SceneReadEnd!=0x800ABEF0||memory.PlacedMobyCount!=9||
            memory.DynamicMobyCapacity!=32||memory.NativeSourceCertified||memory.RuntimeFitProven||memory.PeakTransientFitProven)
            throw new InvalidDataException("Tiled executable requires the actual revised CB8 used-data/model/Scene accounting, without an inherited runtime certificate.");

        var source=HostAncestor.FamilySourceAncestor;
        var domain=source.ResourceInputs["key.special-renderer-and-global-routines"].Single().Extent.Space;
        if(domain.Kind!=NativeAddressKind.ExecutableFile||domain.Revision!=NativeAddressRevision.Original||domain.ByteLength!=0x66000)
            throw new InvalidDataException("Tiled executable lost its actual complete Original executable owner.");
        _original=source.ReadSource(new(domain,0,domain.ByteLength));
        NativeId65FreshExecutablePlan.RequireOriginal(_original);
        if(_executable.Length!=_original.Length||!parent.RevertExactOriginal(_executable).AsSpan().SequenceEqual(_original)||
            parent.SourceImageSha256!=TerrainAncestor.SourceImageSha256||!parent.SourceCertified||!parent.Experimental||
            parent.KnownSelectorCount!=7||parent.KnownDeterministicSelectorSubtotal!=20||parent.ExperimentalTreasureTarget!=20||
            parent.Rows.Count!=9||parent.TargetSlot!=35||parent.TargetFileOffset!=0x5FC7E||
            BinaryPrimitives.ReadUInt16LittleEndian(_executable.AsSpan(parent.TargetFileOffset))!=20||
            !parent.GrassPassiveDispatchPreserved||parent.KeyZeroContributionProven||parent.CompleteStaticCensusProven||
            parent.RuntimeDropGuarantee||parent.CompleteNativeDependencyClosureProven||parent.RuntimeVerified||parent.PublicationApproved||
            parent.Rows[1].Premise!="retained-passive-dispatch-zero"||parent.Rows[2].Premise!="experimental-key-zero-not-certified")
            throw new InvalidDataException("Tiled executable lost the actual parent target20, retained Grass, explicit key-zero/census premises or complete Original inverse.");
        // Re-read the Original and current windows. The previous Body provides
        // the known native code extents, never its old A6C address certificate.
        var parser=NativeId65AuthoredSurfaceConsumers.Capture(_original).Single(e=>e.ExecutableFileOffset==0x3558);
        List<NativeId65EngineEvidence> evidence=[new(parser.Id,"EXE",parser.ExecutableFileOffset,parser.ByteLength,parser.Sha256)];
        foreach(string name in new[]{"used-data-end-return-and-model-CD-owner","local-model-clear-registration-and-scene-request",
            "normal-scene-entry-call-and-initialization-order"})
            evidence.Add(body.ResourceAncestor.BaseBodyAncestor.Evidence.Single(e=>e.Name==name&&e.Domain=="EXE"));
        foreach(var item in evidence)
        {
            int at=checked((int)item.Offset);
            if(Hash(_original.AsSpan(at,item.ByteLength))!=item.Sha256||
                !_executable.AsSpan(at,item.ByteLength).SequenceEqual(_original.AsSpan(at,item.ByteLength)))
                throw new InvalidDataException("Tiled executable retained Original/current parser, model or Scene consumer changed.");
        }
        Evidence=Array.AsReadOnly(evidence.ToArray());
        CanonicalDocumentSha256=TerrainAncestor.CanonicalDocumentSha256;
        LevelDataSha256=Hash(_body);ExecutableSha256=Hash(_executable);
        PlanSha256=Hash(JsonSerializer.SerializeToUtf8Bytes(new{
            Profile,SourceImageSha256=parent.SourceImageSha256,CanonicalDocumentSha256,
            Terrain=TerrainAncestor.PlanSha256,Body=body.PlanSha256,ParentExecutable=parent.PlanSha256,
            Placement=PlacementAncestor.PlanSha256,Host=HostAncestor.PlanSha256,LevelDataSha256,ExecutableSha256,
            PriorBodySha256=Hash(_priorBody),OriginalExecutableSha256=Hash(_original),HeaderSha256=Hash(_body.AsSpan(0,0x800)),
            body.SceneSha256,memory.CopyBufferAddress,memory.UsedLevelDataBytes,memory.LevelDataSectorPaddingBytes,
            memory.ModelBytesRead,memory.ModelRamStart,memory.SceneBytesRead,memory.SceneRamStart,memory.SceneReadEnd,
            parent.KnownSelectorCount,parent.KnownDeterministicSelectorSubtotal,parent.ExperimentalTreasureTarget,
            parent.Rows,parent.TargetPatch,parent.GrassDispatchTraceSha256,Evidence,
            SourceCertified,Experimental,FullNativeLoaderProven,KeyZeroContributionProven,CompleteStaticCensusProven,
            RuntimeDropGuarantee,CompleteNativeDependencyClosureProven,PeakRuntimeMemoryProven,RuntimeVerified,PublicationApproved}));
        RequireExactOutputs(_body,_executable);
    }

    internal NativeFreshTiledFamilyBodyPlan BodyAncestor{get;}
    internal NativeFreshLockedChestExperimentalExecutable ParentExecutableAncestor{get;}
    internal NativeFreshTiledFamilyTerrainRevision TerrainAncestor=>BodyAncestor.TerrainAncestor;
    internal NativeFreshLockedChestPlacementPlan PlacementAncestor=>BodyAncestor.PlacementAncestor;
    internal NativeFreshLockedChestResearchHostExtension HostAncestor=>BodyAncestor.HostAncestor;
    internal NativeFreshLevelDocumentIntent DocumentAncestor=>TerrainAncestor.IntentAncestor.DocumentAncestor;
    internal IReadOnlyList<NativeId65EngineEvidence> Evidence{get;}
    internal string CanonicalDocumentSha256{get;}
    internal string LevelDataSha256{get;}
    internal string ExecutableSha256{get;}
    internal string PlanSha256{get;}
    internal bool SourceCertified=>true;
    internal bool Experimental=>ParentExecutableAncestor.Experimental;
    internal bool FullNativeLoaderProven=>false;
    internal bool KeyZeroContributionProven=>ParentExecutableAncestor.KeyZeroContributionProven;
    internal bool CompleteStaticCensusProven=>ParentExecutableAncestor.CompleteStaticCensusProven;
    internal bool RuntimeDropGuarantee=>ParentExecutableAncestor.RuntimeDropGuarantee;
    internal bool CompleteNativeDependencyClosureProven=>false;
    internal bool PeakRuntimeMemoryProven=>false;
    internal bool RuntimeVerified=>false;
    internal bool PublicationApproved=>false;
    internal byte[] CopyLevelData()=>_body.ToArray();
    internal byte[] CopyExecutable()=>_executable.ToArray();
    internal void RequireAncestry(NativeFreshTiledFamilyBodyPlan body,NativeFreshLockedChestExperimentalExecutable parent)
    {
        ArgumentNullException.ThrowIfNull(body);ArgumentNullException.ThrowIfNull(parent);
        if(!ReferenceEquals(body,BodyAncestor)||!ReferenceEquals(parent,ParentExecutableAncestor)||
            !ReferenceEquals(body.PlacementAncestor,parent.PlacementAncestor)||!ReferenceEquals(body.HostAncestor,parent.HostAncestor))
            throw new InvalidDataException("Tiled executable retains its actual new Body/parent executable and common placement/Host, not equal replacements.");
        body.RequireAncestry(body.TerrainAncestor);
        TerrainAncestor.IntentAncestor.RequireAncestry(DocumentAncestor);
        parent.RequireAncestry(body.PlacementAncestor,body.HostAncestor);
    }
    internal void RequireExactOutputs(ReadOnlySpan<byte> body,ReadOnlySpan<byte> executable)
    {
        RequireAncestry(BodyAncestor,ParentExecutableAncestor);
        if(!body.SequenceEqual(_body)||!executable.SequenceEqual(_executable))
            throw new InvalidDataException("Complete tiled executable composition Body/executable afterimage or complement changed.");
    }
    internal byte[] RevertExactLevelData(ReadOnlySpan<byte> body)
    {
        RequireExactOutputs(body,_executable);byte[] inverse=BodyAncestor.RevertExactLevelData(body);
        if(!inverse.AsSpan().SequenceEqual(_priorBody))throw new InvalidDataException("Tiled executable Body inverse lost its actual immediate placed child.");
        PlacementAncestor.RequireExactLevelData(inverse);return inverse;
    }
    internal byte[] RevertExactExecutable(ReadOnlySpan<byte> executable)
    {
        RequireExactOutputs(_body,executable);ParentExecutableAncestor.RequireExactExecutable(executable);
        return ParentExecutableAncestor.CopyExecutable();
    }
    internal byte[] RevertExactOriginal(ReadOnlySpan<byte> executable)
    {
        byte[] original=ParentExecutableAncestor.RevertExactOriginal(RevertExactExecutable(executable));
        if(!original.AsSpan().SequenceEqual(_original))throw new InvalidDataException("Tiled executable inverse lost its actual complete Original owner.");
        return original;
    }
    internal static NativeFreshTiledFamilyExecutablePlan Compose(NativeFreshTiledFamilyBodyPlan body,
        NativeFreshLockedChestExperimentalExecutable parent)
    {ArgumentNullException.ThrowIfNull(body);ArgumentNullException.ThrowIfNull(parent);return new(body,parent);}
    private static int Word(ReadOnlySpan<byte> bytes,int at)=>BinaryPrimitives.ReadInt32LittleEndian(bytes[at..]);
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
}
