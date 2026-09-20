using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;

namespace Spyro.Editor.Core.Exporting;

internal sealed record NativeFreshLockedChestPlacementIntent(int KeyX,int KeyY,int KeyZ,int ChestX,int ChestY,int ChestZ);
internal sealed record NativeFreshChestCoordinateWrite(int SceneOffset,int Before,int After);

/// <summary>
/// Source-relative two-anchor placement: the key moves independently; the chest
/// and its five rewards retain their complete native relative arrangement.
/// Centre containment is not a footprint, approach or native-lifecycle proof.
/// </summary>
internal sealed class NativeFreshLockedChestPlacementPlan
{
    private readonly byte[] _scene,_body;
    private NativeFreshLockedChestPlacementPlan(NativeFreshLockedChestResearchBody body,
        NativeFreshLockedChestPlacementIntent intent)
    {
        BodyAncestor=body;Intent=intent;RequireAncestry(body,intent);
        var source=body.SceneAncestor;
        var baseBody=body.ResourceAncestor.BaseBodyAncestor;var baseScene=baseBody.HostAncestor.SceneAncestor;
        var floor=baseScene.CompilationAncestor.Intent;
        if(baseScene.Producer==NativeFreshWizardSceneProducer.SameLayoutMaterialAuthored)
        {
            NativeId65FreshWizardResearchExecutableSource.RequireWideFamilyMaterialBody(baseBody);
            if(source.Allocation.Rows.Count!=9||source.Layout.SceneByteLength!=0x3800)
                throw new InvalidDataException("Wide chest placement requires its actual nine-row source owner.");
        }
        else
        {
            if(baseScene.Producer!=NativeFreshWizardSceneProducer.MaterialAuthored)
                throw new InvalidDataException("Chest placement requires an actual retained material Scene producer.");
            if(floor.FloorX!=8448||floor.FloorY!=8448||floor.FloorZ!=8704||floor.FloorWidth!=2048||floor.FloorDepth!=2048||
                floor.SpawnZ!=9216||source.Allocation.Rows.Count!=9||source.Layout.SceneByteLength!=0x3800)
                throw new InvalidDataException("Chest placement requires the actual existing separated-B floor and nine-row source owner.");
        }
        if(intent.KeyZ!=floor.FloorZ||intent.ChestZ!=floor.FloorZ||
            new[]{intent.KeyX,intent.KeyY,intent.KeyZ,intent.ChestX,intent.ChestY,intent.ChestZ}.Any(v=>(v&15)!=0)||
            (intent.KeyX==intent.ChestX&&intent.KeyY==intent.ChestY))
            throw new InvalidDataException("Chest/key anchors must be distinct, native-grid aligned and on the actual floor elevation.");
        byte[] before=source.CopyScene();_scene=before.ToArray();
        int chestRow=checked((int)source.Allocation.Rows[3].Row.Offset);
        int[] oldChest=[Word(before,chestRow+12),Word(before,chestRow+16),Word(before,chestRow+20)];
        int[] newChest=[intent.ChestX,intent.ChestY,intent.ChestZ],newKey=[intent.KeyX,intent.KeyY,intent.KeyZ];
        List<NativeFreshChestCoordinateWrite> writes=[];int margin=int.MaxValue;
        for(int i=2;i<9;i++)
        {
            var owner=source.Allocation.Rows[i];int row=checked((int)owner.Row.Offset),properties=checked((int)owner.Properties.Offset);
            if(row!=0xD4+i*0x58||Word(before,row)!=properties||
                BinaryPrimitives.ReadUInt16LittleEndian(before.AsSpan(row+0x36))!=(i==2?0xAD:i==3?0xAE:0xD))
                throw new InvalidDataException("Chest placement lost its exact ordered source classes, private owners or row extents.");
            int[] position=new int[3];
            for(int axis=0;axis<3;axis++)
            {
                long value=i==2?newKey[axis]:(long)newChest[axis]+Word(before,row+12+axis*4)-oldChest[axis];
                if(value is <0 or >262128)throw new InvalidDataException("Chest translation escaped finite native world coordinates.");
                position[axis]=(int)value;
                Write(row+12+axis*4,position[axis]);
                if(i>=4)
                {
                    int at=properties+4+axis*4;
                    if(Word(before,at)!=Word(before,row+12+axis*4))
                        throw new InvalidDataException("Reward private XYZ is not its actual complete source row coordinate owner.");
                    Write(at,position[axis]);
                }
            }
            if(position[0]<=floor.FloorX||position[0]>=floor.FloorX+floor.FloorWidth||
                position[1]<=floor.FloorY||position[1]>=floor.FloorY+floor.FloorDepth||
                position[2]<floor.FloorZ||position[2]>floor.SpawnZ)
                throw new InvalidDataException("A translated chest-family centre escaped the actual floor/height interval.");
            margin=Math.Min(margin,new[]{position[0]-floor.FloorX,floor.FloorX+floor.FloorWidth-position[0],
                position[1]-floor.FloorY,floor.FloorY+floor.FloorDepth-position[1]}.Min());
        }
        if(writes.Count!=36||writes.Select(w=>w.SceneOffset).Distinct().Count()!=36)
            throw new InvalidDataException("Chest placement requires exactly36 disjoint coordinate words.");
        CoordinateWrites=writes.AsReadOnly();MinimumCentreBoundaryMargin=margin;
        byte[] inverse=_scene.ToArray();foreach(var field in writes)Put(inverse,field.SceneOffset,field.Before);
        source.RequireExactScene(inverse);
        byte[] oldBody=body.CopyLevelData();int sceneAt=Word(oldBody,0x18);
        if(sceneAt!=0x102000||Word(oldBody,0x1C)!=_scene.Length||!oldBody.AsSpan(sceneAt,_scene.Length).SequenceEqual(before))
            throw new InvalidDataException("Chest placement Body does not own the actual complete source-coordinate scene.");
        // Reuse the neutral replacement grammar; no second allocator, copied
        // resource importer or mutable caller-supplied row template is admitted.
        var layout=new NativeLevelDataGrowthLayout(oldBody.AsSpan(0,0x800),oldBody.Length,
            [new(0,0,0,0,0,_scene.Length,_scene,3,false)]);
        _body=layout.Expand(oldBody);
        if(layout.Growth!=0||!_body.AsSpan(0,sceneAt).SequenceEqual(oldBody.AsSpan(0,sceneAt))||
            !_body.AsSpan(sceneAt+_scene.Length).SequenceEqual(oldBody.AsSpan(sceneAt+_scene.Length)))
            throw new InvalidDataException("Chest placement changed a non-scene Body owner.");
        SceneSha256=Hash(_scene);LevelDataSha256=Hash(_body);
        PlanSha256=Hash(JsonSerializer.SerializeToUtf8Bytes(new{Profile="fresh-chest-two-anchor-placement-v1",
            Body=body.PlanSha256,Intent,CoordinateWrites,SceneSha256,LevelDataSha256,MinimumCentreBoundaryMargin,
            SourceCertified,AuthoredCoordinatesProven,CentreContainmentProven,FootprintClearanceProven,
            CompleteNativeBehaviorProven,FinalExecutableIssued,RuntimeVerified,PublicationApproved}));
        void Write(int at,int value){writes.Add(new(at,Word(before,at),value));Put(_scene,at,value);}
    }
    internal NativeFreshLockedChestResearchBody BodyAncestor{get;}
    internal NativeFreshLockedChestPlacementIntent Intent{get;}
    internal IReadOnlyList<NativeFreshChestCoordinateWrite> CoordinateWrites{get;}
    internal int MinimumCentreBoundaryMargin{get;}
    internal string SceneSha256{get;}
    internal string LevelDataSha256{get;}
    internal string PlanSha256{get;}
    internal bool SourceCertified=>true;
    internal bool AuthoredCoordinatesProven=>true;
    internal bool CentreContainmentProven=>true;
    internal bool FootprintClearanceProven=>false;
    internal bool CompleteNativeBehaviorProven=>false;
    internal bool FinalExecutableIssued=>false;
    internal bool RuntimeVerified=>false;
    internal bool PublicationApproved=>false;
    internal byte[] CopyScene()=>_scene.ToArray();
    internal byte[] CopyLevelData()=>_body.ToArray();
    internal void RequireAncestry(NativeFreshLockedChestResearchBody body,NativeFreshLockedChestPlacementIntent intent)
    {
        ArgumentNullException.ThrowIfNull(body);ArgumentNullException.ThrowIfNull(intent);
        if(!ReferenceEquals(body,BodyAncestor)||!ReferenceEquals(intent,Intent))
            throw new InvalidDataException("Chest placement belongs to another actual Body/intent pair.");
        body.RequireAncestry(body.ResourceAncestor,body.HostAncestor,body.SceneAncestor);
        body.RequireExactLevelData(body.CopyLevelData());
    }
    internal void RequireExactScene(ReadOnlySpan<byte> scene)
    {RequireAncestry(BodyAncestor,Intent);if(!scene.SequenceEqual(_scene))throw new InvalidDataException("Complete placed chest scene changed.");}
    internal void RequireExactLevelData(ReadOnlySpan<byte> body)
    {RequireAncestry(BodyAncestor,Intent);if(!body.SequenceEqual(_body))throw new InvalidDataException("Complete placed chest Body changed.");}
    internal byte[] RevertExactScene(ReadOnlySpan<byte> scene)
    {
        RequireExactScene(scene);byte[] inverse=scene.ToArray();
        foreach(var field in CoordinateWrites)Put(inverse,field.SceneOffset,field.Before);
        BodyAncestor.SceneAncestor.RequireExactScene(inverse);return inverse;
    }
    internal byte[] RevertExactLevelData(ReadOnlySpan<byte> body)
    {
        RequireExactLevelData(body);byte[] inverse=body.ToArray();
        RevertExactScene(body.Slice(0x102000,0x3800)).CopyTo(inverse,0x102000);
        BodyAncestor.RequireExactLevelData(inverse);return inverse;
    }
    internal static NativeFreshLockedChestPlacementPlan Compose(NativeFreshLockedChestResearchBody body,
        NativeFreshLockedChestPlacementIntent intent)
    {ArgumentNullException.ThrowIfNull(body);ArgumentNullException.ThrowIfNull(intent);return new(body,intent);}
    private static int Word(ReadOnlySpan<byte> b,int at)=>BinaryPrimitives.ReadInt32LittleEndian(b[at..]);
    private static void Put(Span<byte> b,int at,int value)=>BinaryPrimitives.WriteInt32LittleEndian(b[at..],value);
    private static string Hash(ReadOnlySpan<byte> b)=>Convert.ToHexStringLower(SHA256.HashData(b));
}
