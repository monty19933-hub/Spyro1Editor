using System.Buffers.Binary;
using System.Text.Json;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.Core.Exporting;

public static partial class TerrainPatchExporter
{
    // Structural edits own one complete terrain subfile. Its allocation and all
    // later subfile addresses stay fixed; only authenticated zero padding may
    // be consumed. HP, LP, visibility, collision and lookup change together.
    private static NativeTerrainLevelReplacement? AddCoupledTopologyPatches(FileStream image, DiscLayout layout, byte[] ram,
        CollisionPatchContext? context, IReadOnlyDictionary<string, SourceSectorLocation> locations, LevelDefinition level,
        AssetSubfileInfo modelInfo, JsonElement edits, Dictionary<long, TerrainPatch> patches,
        IReadOnlyDictionary<int, NativeTerrainTextureLowDetailReplacement>? privateAppearance=null)
    {
        JsonElement[] structural = edits.EnumerateArray().Where(edit => IsStructuralTerrainEdit(JsonValue.GetString(edit,"structureEditMode"))).ToArray();
        var geometryLpOwners=new Dictionary<int,(SceneSectorHeader Header,int ModelOffset)>();
        foreach(var owner in locations.Values.DistinctBy(l=>l.WadOffset))
        {
            int runtime=checked((int)ParseRequiredLong(owner.SectorOffset,"sector offset"));
            var header=ReadSceneSectorHeader(ram,runtime);
            var lpPoints=Enumerable.Range(0,header.NumLpVertices).Select(i=>ToCollisionPoint(DecodeSceneVertex(ReadUInt32(ram,GetSceneVertexOffset(header,"lp",i)),header))).ToHashSet();
            foreach(var patch in patches.Values.Where(p=>p.Kind=="visual-hp-topology-safe"))
            {
                long at=ParseRequiredLong(patch.WadRelativeOffset,"HP point offset")-owner.WadOffset;
                if(at<0||at>=header.SizeBytes)continue;
                var original=ToCollisionPoint(DecodeSceneVertex(BinaryPrimitives.ReadUInt32LittleEndian(HexToBytes(patch.BeforeHexPreview)),header));
                if(!lpPoints.Contains(original))
                    geometryLpOwners[int.Parse(owner.RuntimeKey.Split(':')[0],System.Globalization.CultureInfo.InvariantCulture)]
                        =(header,checked((int)(owner.WadOffset-modelInfo.AbsoluteWadOffset)));
            }
        }
        if (structural.Length == 0 && geometryLpOwners.Count==0) return null;
        if (context == null) throw new InvalidOperationException("Terrain add/remove needs the complete native collision and lookup tables. No terrain BIN was built.");
        var inventory=BuildTerrainHpTopologyInventory(image,layout,ram,locations);
        var removedKeys=structural.Where(e=>IsRemoveFaceEdit(JsonValue.GetString(e,"structureEditMode")))
            .Select(e=>JsonValue.GetString(e,"runtimeKey")).ToHashSet(StringComparer.OrdinalIgnoreCase);
        int length = checked((int)modelInfo.SubfileSize);
        byte[] before = ReadWadBytes(image,layout,modelInfo.AbsoluteWadOffset,length), staged = before.ToArray();
        foreach (TerrainPatch patch in patches.Values)
        {
            long at = ParseRequiredLong(patch.WadRelativeOffset,"patch offset") - modelInfo.AbsoluteWadOffset;
            if (at >= 0 && at + patch.ByteLength <= length)
                HexToBytes(patch.AfterHexPreview).CopyTo(staged,checked((int)at));
        }
        List<byte[]> components = [];
        List<int> offsets = [];
        int cursor = 0;
        // Retail terrain has texture, environment, occlusion, surface, collision,
        // sky components. The remaining allocation is authenticated zero padding.
        for (int i=0;i<6;i++)
        {
            if (cursor+4>length) throw new InvalidOperationException("Terrain component header exceeds its native allocation.");
            int size=BinaryPrimitives.ReadInt32LittleEndian(staged.AsSpan(cursor));
            if(size<4 || (size&3)!=0 || size>length-cursor) throw new InvalidOperationException("Terrain component length is invalid; no topology was exported.");
            offsets.Add(cursor); components.Add(staged.AsSpan(cursor,size).ToArray()); cursor+=size;
        }
        byte[] originalEntry=ReadWadBytes(image,layout,modelInfo.AssetOffset,checked((int)modelInfo.AssetSize));
        var soundOwner=new NativeLevelSoundLayout(originalEntry);
        int usedEnd=checked(soundOwner.SoundEnd-(int)modelInfo.SubfileOffset);
        if(usedEnd<cursor||usedEnd>length||staged.AsSpan(usedEnd).IndexOfAnyExcept((byte)0)>=0)
            throw new InvalidOperationException("Terrain topology needs verified zero subfile padding after the complete native portal/particle/sound owners. No terrain BIN was built.");
        components.Add(staged.AsSpan(cursor,usedEnd-cursor).ToArray());
        byte[] environment=components[1], visibility=components[2], collision=components[4];
        int sourceSectors=Read(environment,4);
        if(sourceSectors<1 || sourceSectors>255 || 8+sourceSectors*4>environment.Length) throw new InvalidOperationException("Native environment sector directory is invalid.");
        int triangleCount=Read(collision,4), flagCount=Read(collision,8);
        int treeAt=checked(4+Read(collision,12)), blocksAt=checked(4+Read(collision,16));
        int trianglesAt=checked(4+Read(collision,20)), assignmentsAt=checked(4+Read(collision,24)), flagsAt=checked(4+Read(collision,28));
        if(treeAt!=32 || treeAt>=blocksAt || blocksAt>=trianglesAt || triangleCount!=context.Table.TriangleCount ||
            trianglesAt+triangleCount*12!=assignmentsAt || assignmentsAt+triangleCount>flagsAt || flagsAt+flagCount>collision.Length)
            throw new InvalidOperationException("Native topology collision partitions are not exact.");
        byte[] blocks=collision.AsSpan(blocksAt,trianglesAt-blocksAt).ToArray();
        int used=blocks.Length; while(used>=2 && BinaryPrimitives.ReadUInt16LittleEndian(blocks.AsSpan(used-2))==0)used-=2;
        NativeCollisionIndexSnapshot index=NativeCollisionIndexCodec.Decode(collision.AsSpan(treeAt,blocksAt-treeAt),blocks,used,triangleCount,false);
        Dictionary<NativeCollisionCell,List<int>> cells=index.Cells.ToDictionary(p=>p.Key,p=>p.Value.OrderedTriangleIndexes.ToList());
        List<(byte[] Words,byte Assignment,byte Flag,string Key)> appendedTriangles=[];
        List<(byte[] Bytes,int SourceSector)> newSectors=[];
        Dictionary<int,(SceneSectorHeader Header,int ModelOffset)> lpRebuilds=geometryLpOwners;
        HashSet<int>? portalWalkIn=null;
        foreach(JsonElement edit in structural)
        {
            string key=JsonValue.GetString(edit,"runtimeKey");
            if(!string.Equals(JsonValue.GetString(edit,"detail"),"hp",StringComparison.OrdinalIgnoreCase) || !locations.TryGetValue(key,out SourceSectorLocation? location))
                throw new InvalidOperationException($"{key}: structural terrain must bind to one original high-detail native face.");
            int runtime=JsonValue.GetInt32(edit,"sectorOffset",-1), sourceFaceAt=JsonValue.GetInt32(edit,"faceOffset",-1);
            if(runtime<0 || sourceFaceAt<runtime) throw new InvalidOperationException($"{key}: native structural identity is missing.");
            SceneSectorHeader sector=ReadSceneSectorHeader(ram,runtime);
            int modelSector=checked((int)(location.WadOffset-modelInfo.AbsoluteWadOffset));
            int modelFace=checked(modelSector+sourceFaceAt-runtime);
            if(modelFace<modelSector || modelFace+16>modelSector+sector.SizeBytes) throw new InvalidOperationException($"{key}: structural face exceeds its source sector.");
            var originalXY=ReadVector2Array(edit,"originalPoints"); var originalZ=ReadFloatArray(edit,"originalZ");
            var targetXY=ReadVector2Array(edit,"editedPoints"); var targetZ=ReadFloatArray(edit,"editedZ");
            var vertices=NormalizeVertexIndexes(ReadIntArray(edit,"vertexIndexes"),originalXY.Count);
            if(originalXY.Count is <3 or >4 || originalZ.Count!=originalXY.Count || targetXY.Count!=originalXY.Count || targetZ.Count!=originalXY.Count ||
                !VertexIndexesMatchOriginal(sector,"hp",vertices,originalXY.Count,originalXY,originalZ))
                throw new InvalidOperationException($"{key}: structural points no longer authenticate the original native face.");
            SpyroCollisionPoint[] oldPoints=originalXY.Select((p,i)=>ToCollisionPoint(new Vector3f(p.X,p.Y,originalZ[i]))).ToArray();
            SpyroCollisionPoint[] newPoints=targetXY.Select((p,i)=>ToCollisionPoint(new Vector3f(p.X,p.Y,targetZ[i]))).ToArray();
            bool remove=IsRemoveFaceEdit(JsonValue.GetString(edit,"structureEditMode"));
            List<(SpyroCollisionTriangle Source,SpyroCollisionPoint[] Target)> triangles=[];
            int[][][] partitions=oldPoints.Length==3 ? [[[0,1,2]]] : [[[0,1,2],[0,2,3]],[[0,1,3],[1,2,3]]];
            int[][]? partition=partitions.FirstOrDefault(parts=>parts.All(part=>context.TrianglesByKey.TryGetValue(
                CollisionTriangleKey(part.Select(i=>oldPoints[i]).ToArray()),out var hits)&&hits.Count(t=>context.GroupIndexesByTriangleIndex.ContainsKey(t.Index)&&!IsZeroAreaCollisionTriangle3D(t))==1));
            if(partition==null)
            {
                // Scenery (trim, wall decoration): the game has no collision on this face at all, so removing or
                // copying it changes only what is drawn. A face with any collision on it still needs its owner.
                bool ownTriangle=partitions.SelectMany(parts=>parts).Any(part=>context.TrianglesByKey.ContainsKey(CollisionTriangleKey(part.Select(i=>oldPoints[i]).ToArray())));
                portalWalkIn??=PortalWalkInTriangleIndexes(image,layout,level);
                if(ownTriangle||!TerrainSceneryFaceRule.HasNoCollisionOnFace(oldPoints.Select(p=>((double)p.X,(double)p.Y,(double)p.Z)).ToArray(),
                    context.Table.Triangles.Where(t=>!portalWalkIn.Contains(t.Index)).Select(t=>t.Points)))
                    throw new InvalidOperationException($"{key}: every visible triangle needs an exact native collision owner; no terrain BIN was built.");
            }
            foreach(int[] part in partition??[])
            {
                var source=context.TrianglesByKey[CollisionTriangleKey(part.Select(i=>oldPoints[i]).ToArray())]
                    .Single(t=>context.GroupIndexesByTriangleIndex.ContainsKey(t.Index)&&!IsZeroAreaCollisionTriangle3D(t));
                Dictionary<SpyroCollisionPoint,SpyroCollisionPoint> targets=oldPoints.Select((p,j)=>(p,j)).ToDictionary(p=>p.p,p=>newPoints[p.j]);
                triangles.Add((source,source.Points.Select(p=>targets[p]).ToArray()));
            }
            if(remove)
            {
                int ownerIndex=JsonValue.GetInt32(edit,"sectorIndex",-1);
                if(ownerIndex<0||ownerIndex>=sourceSectors)throw new InvalidOperationException("Removed face has no native visibility sector.");
                foreach(var (triangle,_) in triangles)
                {
                    var alias=inventory.Faces.FirstOrDefault(f=>!removedKeys.Contains(f.Label)&&triangle.Points.All(f.Points.Contains));
                    if(alias!=null)throw new InvalidOperationException($"{key}: collision is also owned by visible face {alias.Label}; remove the complete shared surface or keep it. No terrain BIN was built.");
                }
                lpRebuilds[ownerIndex]=(sector,modelSector);
                byte first=staged[modelFace]; staged.AsSpan(modelFace,4).Fill(first);
                foreach(var (source,_) in triangles)
                {
                    int at=trianglesAt+source.Index*12;
                    uint x=(uint)source.P1.X,y=(uint)source.P1.Y,z=(uint)source.P1.Z|(source.ZWord&0xC000);
                    Put(collision,at,x);Put(collision,at+4,y);Put(collision,at+8,z);
                    foreach(var list in cells.Values)list.RemoveAll(id=>id==source.Index);
                }
            }
            else
            {
                int sourceSector=JsonValue.GetInt32(edit,"sectorIndex",-1);
                if(sourceSector<0 || sourceSector>=sourceSectors)throw new InvalidOperationException($"{key}: source sector is outside native visibility directory.");
                byte[] face=staged.AsSpan(modelFace,16).ToArray();
                int texture=JsonValue.GetInt32(edit,"textureIdEdited",-1);if(texture>=0)ApplyTextureIdToHpFaceBytes(face,texture);
                byte[] copied=BuildCoupledCopiedSector(ram,sector,face,vertices,newPoints);
                ApplyCoupledPrivateAppearance(copied,privateAppearance);
                newSectors.Add((copied,-1));
                foreach(var(source,target) in triangles)
                {
                    int slot=checked(triangleCount+appendedTriangles.Count);
                    if(slot>=0x8000)throw new InvalidOperationException("Native collision triangle count exceeds its 15-bit lookup identity.");
                    if(!HasPreservedCollisionWinding(source.Points,target)||!TryBuildCollisionTriangleWordsCyclic(target,source.ZWord&0xC000,out uint x,out uint y,out uint z,out _))
                        throw new InvalidOperationException($"{key}: copied collision would reverse winding or exceed packed native coordinates.");
                    byte[] words=new byte[12];Put(words,0,x);Put(words,4,y);Put(words,8,z);
                    byte flag=source.Index<flagCount?collision[flagsAt+source.Index]:(byte)255;
                    if(flag!=255 && flag>63)throw new InvalidOperationException($"{key}: source collision flag {flag} has no supported surface-prefix issuer.");
                    appendedTriangles.Add((words,collision[assignmentsAt+source.Index],flag,key));
                    foreach(var cell in CollisionTouchedCellsFor(target))
                    {
                        NativeCollisionCell keyCell=new(cell.X,cell.Y,cell.Z);
                        if(!cells.TryGetValue(keyCell,out var list))cells[keyCell]=list=[];
                        list.Add(slot);
                    }
                }
            }
        }
        Dictionary<int,byte[]> sectorReplacements=[];
        foreach(var (owner,item) in lpRebuilds)
        {
            var rebuiltLp=RebuildCoupledLowDetail(staged,item.ModelOffset,item.Header,ram,privateAppearance);
            if(rebuiltLp.Count==0)continue;
            byte[] first=rebuiltLp[0];
            int oldHpAt=28+4*(item.Header.NumLpVertices+item.Header.NumLpColours+2*item.Header.NumLpFaces);
            int newHpAt=28+4*(first[16]+first[17]+2*first[18]);
            byte[] replacement=new byte[newHpAt+item.Header.SizeBytes-oldHpAt];
            staged.AsSpan(item.ModelOffset,28).CopyTo(replacement);
            first.AsSpan(28,newHpAt-28).CopyTo(replacement.AsSpan(28));
            replacement[16]=first[16];replacement[17]=first[17];replacement[18]=first[18];
            var lpHeader=ReadSceneSectorHeader(first,0);
            for(int v=0;v<replacement[16];v++)
            {
                var point=DecodeSceneVertex(ReadUInt32(first,28+4*v),lpHeader);
                Put(replacement,28+4*v,SetSceneVertexWord(0,item.Header,new Vector2f(point.X,point.Y),point.Z));
            }
            staged.AsSpan(item.ModelOffset+oldHpAt,item.Header.SizeBytes-oldHpAt).CopyTo(replacement.AsSpan(newHpAt));
            Id65EnvironmentSectorHeaderContract.Stamp(replacement);
            sectorReplacements[owner]=replacement;
            foreach(byte[] lpSector in rebuiltLp.Skip(1))newSectors.Add((lpSector,owner));
        }
        if(sourceSectors+newSectors.Count>255)throw new InvalidOperationException("The native visibility lists cannot address another terrain sector.");
        // Commit staged remove-face words into the original environment before
        // extending its authenticated pointer directory.
        environment=staged.AsSpan(offsets[1],components[1].Length).ToArray();
        AssignCoupledFaceCache(environment,newSectors.Select(s=>s.Bytes).ToArray());
        components[1]=AppendCoupledSectors(ReplaceCoupledSectors(environment,sectorReplacements),newSectors.Select(s=>s.Bytes).ToArray());
        components[2]=AppendCoupledVisibility(visibility,sourceSectors,newSectors.Select(s=>s.SourceSector).ToArray());
        Dictionary<NativeCollisionCell,IReadOnlyList<int>> sequences=cells.Where(p=>p.Value.Count>0).ToDictionary(p=>p.Key,p=>(IReadOnlyList<int>)p.Value.ToArray());
        int finalTriangleCount=checked(triangleCount+appendedTriangles.Count);
        // The index codec authenticates old list order against the expanded row
        // identity range before inserting any new membership.
        var expandedIndex=NativeCollisionIndexCodec.Decode(index.CopyTree(),index.CopyBlocks(),index.UsedBlockBytes,finalTriangleCount,false);
        NativeCollisionIndexSnapshot rebuilt;
        try{rebuilt=NativeCollisionIndexCodec.Rebuild(expandedIndex,sequences);}
        catch(InvalidDataException){rebuilt=NativeCollisionIndexCodec.Create(sequences,finalTriangleCount,false);}
        byte[] tree=rebuilt.CopyTree(),newBlocks=rebuilt.CopyBlocks();
        int newTriangles=32+tree.Length+newBlocks.Length,newAssignments=newTriangles+finalTriangleCount*12,newFlags=newAssignments+((finalTriangleCount+3)&~3);
        if(collision.AsSpan(assignmentsAt+triangleCount,flagsAt-assignmentsAt-triangleCount).IndexOfAnyExcept((byte)0)>=0)
            throw new InvalidOperationException("Native collision assignment alignment contains unknown data.");
        byte[] newCollision=new byte[newFlags+collision.Length-flagsAt];collision.AsSpan(0,32).CopyTo(newCollision);
        tree.CopyTo(newCollision,32);newBlocks.CopyTo(newCollision,32+tree.Length);
        collision.AsSpan(trianglesAt,triangleCount*12).CopyTo(newCollision.AsSpan(newTriangles));
        collision.AsSpan(assignmentsAt,triangleCount).CopyTo(newCollision.AsSpan(newAssignments));
        for(int i=0;i<appendedTriangles.Count;i++)
        {
            appendedTriangles[i].Words.CopyTo(newCollision,newTriangles+12*(triangleCount+i));
            newCollision[newAssignments+triangleCount+i]=appendedTriangles[i].Assignment;
        }
        collision.AsSpan(flagsAt).CopyTo(newCollision.AsSpan(newFlags));
        Put(newCollision,0,(uint)newCollision.Length);Put(newCollision,4,(uint)finalTriangleCount);
        Put(newCollision,12,28);Put(newCollision,16,(uint)(28+tree.Length));
        Put(newCollision,20,(uint)(newTriangles-4));Put(newCollision,24,(uint)(newAssignments-4));Put(newCollision,28,(uint)(newFlags-4));
        components[4]=newCollision;
        int total=components.Sum(c=>c.Length);
        int growth=total>length?UniversalLevelData.Align(total-length,0x800):0;
        byte[] after=new byte[checked(length+growth)];cursor=0;foreach(byte[] component in components){component.CopyTo(after,cursor);cursor+=component.Length;}
        var surfaceAssignments=appendedTriangles.Select((item,i)=>(item,i)).Where(p=>p.item.Flag!=255)
            .Select(p=>new NativeTerrainSurfaceFlagAssignment(triangleCount+p.i,p.item.Flag,p.item.Key,"Copied source surface")).ToArray();
        if(surfaceAssignments.Length>0)
        {
            int collisionOffset=components.Take(4).Sum(c=>c.Length);
            if(!NativeTerrainSurfaceFlagPromoter.TryBuild(after,modelInfo.AbsoluteWadOffset,modelInfo.AbsoluteWadOffset+collisionOffset,
                surfaceAssignments,[],out var promotion,out string reason)||promotion==null)
                throw new InvalidOperationException($"Copied collision surface flags could not be preserved: {reason}");
            promotion.After.CopyTo(after,checked((int)(promotion.WadOffset-modelInfo.AbsoluteWadOffset)));
        }
        _=NativeCollisionIndexCodec.Decode(tree,newBlocks,rebuilt.UsedBlockBytes,finalTriangleCount,false);
        // Padding is disc allocation, not automatic RAM ownership. Authenticate
        // the actual native loader binding and retain the normal buffer floor.
        var exeOwner=DiscImage.FindRootFileRecord(image,layout,n=>n.Equals("SCUS_942.28",StringComparison.OrdinalIgnoreCase));
        byte[] executable=DiscImage.ReadFileBytes(image,layout,exeOwner.Lba,0,exeOwner.Size);
        // An object step may already have edited this image's executable data (gem totals, portals).
        // The terrain consumers themselves must be untouched.
        _=NativeId65AuthoredSurfaceConsumers.CaptureConsumerSpans(executable);
        uint copyBuffer=UniversalExeBindings.Read(executable,level.LevelId)[UniversalExeBindings.CopyBufferGlobal].Value;
        byte[] emittedEntry=originalEntry.ToArray();
        foreach(var patch in patches.Values)
        {
            long at=ParseRequiredLong(patch.WadRelativeOffset,"patch offset")-modelInfo.AssetOffset;
            if(at>=0&&at+patch.ByteLength<=emittedEntry.Length)HexToBytes(patch.AfterHexPreview).CopyTo(emittedEntry,checked((int)at));
        }
        if(growth>0)
        {
            var body=new UniversalLevelData(emittedEntry);body.GrowSubfile(1,growth);emittedEntry=body.Bytes;
        }
        after.CopyTo(emittedEntry,checked((int)modelInfo.SubfileOffset));
        uint nativeEnd=UniversalMobyComposer.RuntimeEnd(new UniversalLevelData(originalEntry),copyBuffer);
        uint emittedEnd=UniversalMobyComposer.RuntimeEnd(new UniversalLevelData(emittedEntry),copyBuffer);
        if(GameRam.Exceeds(new UniversalLevelData(emittedEntry),copyBuffer,new UniversalLevelData(originalEntry),copyBuffer))
            throw new InvalidOperationException($"Terrain topology exceeds native working memory by {GameRam.Shortfall(new UniversalLevelData(emittedEntry),copyBuffer)} bytes; no terrain BIN was built.");
        if(growth>0)
        {
            foreach(long at in patches.Keys.Where(at=>at>=modelInfo.AssetOffset&&at<modelInfo.AssetOffset+modelInfo.AssetSize).ToArray())patches.Remove(at);
            return new NativeTerrainLevelReplacement(modelInfo.AssetWadIndex,originalEntry,emittedEntry,nativeEnd,emittedEnd){LevelId=level.LevelId};
        }
        foreach(long at in patches.Keys.Where(at=>at>=modelInfo.AbsoluteWadOffset&&at<modelInfo.AbsoluteWadOffset+length).ToArray())patches.Remove(at);
        AddPatch(image,layout,patches,modelInfo.AbsoluteWadOffset,before,after,"terrain-coupled-topology","terrain-topology",
            $"Compose {structural.Length} terrain add/remove edit(s) and {geometryLpOwners.Count} distant-sector geometry rebuild(s) with HP/LP geometry, native visibility and collision lookup; preserve fixed subfile allocation.");
        return null;
    }

    /// <summary>
    /// Portal walk-in triangles (special surface type 6): the invisible plane across a portal's doorway. They are
    /// not ground or wall, and they move with the portal, so they do not make the doorway's trim faces solid.
    /// </summary>
    private static HashSet<int> PortalWalkInTriangleIndexes(FileStream image,DiscLayout layout,LevelDefinition level)
    {
        try
        {
            return PortalSourceDataLocator.LocateTerrainSurfaces(image,layout,level).CollisionSurfaceTriangles
                .Where(t=>t.HasSpecialSurface&&t.SurfaceType==6).Select(t=>t.TriangleIndex).ToHashSet();
        }
        catch(Exception ex) when (ex is InvalidDataException or InvalidOperationException or IOException)
        {
            return [];
        }
    }

    private static byte[] BuildCoupledCopiedSector(byte[] ram,SceneSectorHeader source,byte[] face,IReadOnlyList<int> ids,SpyroCollisionPoint[] points)
    {
        int n=points.Length, size=104+8*n;byte[] s=new byte[size];
        int x=points.Min(p=>p.X),y=points.Min(p=>p.Y),z=points.Min(p=>p.Z);
        int maxX=points.Max(p=>p.X),maxY=points.Max(p=>p.Y),maxZ=points.Max(p=>p.Z);
        int radius=maxX-x+maxY-y+maxZ-z;
        if(radius<=0||radius>4095)throw new InvalidOperationException("Copied terrain exceeds a native sector culling bound.");
        Put(s,0,(uint)(((x+maxX)/2<<16)|((y+maxY)/2)));Put(s,4,(uint)(((z+maxZ)/2<<16)|radius));Put(s,8,(uint)(x<<16|y));Put(s,12,(uint)(z<<16));
        new byte[]{(byte)n,4,1,0,(byte)n,4,1,0}.CopyTo(s,16);Put(s,24,uint.MaxValue);
        int hpVertices=52+4*n,hpFar=52+8*n,hpNear=hpFar+16,hpFace=hpNear+16;
        int sourceColors=GetHpVertexDataEndOffset(source);
        for(int i=0;i<n;i++)
        {
            int dx=points[i].X-x,dy=points[i].Y-y,dz=points[i].Z-z;
            if(dx>2047||dy>2047||dz>1023)throw new InvalidOperationException("Copied terrain exceeds native packed sector coordinates.");
            uint vertex=(uint)(dx<<21|dy<<10|dz);Put(s,28+4*i,vertex);Put(s,hpVertices+4*i,vertex);
        }
        for(int slot=0;slot<4;slot++)
        {
            var colors=NativeTerrainHpColorLayout.GetColorOffsets(sourceColors,source.NumHpColours,face[4+slot]);
            ram.AsSpan(colors.Table2Offset,4).CopyTo(s.AsSpan(28+4*n+4*slot));
            ram.AsSpan(colors.Table1Offset,4).CopyTo(s.AsSpan(hpFar+4*slot));ram.AsSpan(colors.Table2Offset,4).CopyTo(s.AsSpan(hpNear+4*slot));
        }
        int[] lpSlots=NativeHpToLpSlots(face);
        uint packed=0,lpColors=0,hp=0;
        for(int i=0;i<4;i++)
        {
            int id=ids.ToList().IndexOf(face[i]);if(id<0)throw new InvalidOperationException("Copied face slot is outside native points.");hp|=(uint)id<<(8*i);
            int lpId=ids.ToList().IndexOf(face[lpSlots[i]]);packed|=(uint)lpId<<(26-6*i);lpColors|=(uint)lpSlots[i]<<(26-6*i);
        }
        var flags=NativeHpToLpFlags(face);
        Put(s,44+4*n,packed|flags.Item1);Put(s,48+4*n,lpColors|flags.Item2);Put(s,hpFace,hp);Put(s,hpFace+4,0x03020100);
        face.AsSpan(8,8).CopyTo(s.AsSpan(hpFace+8));Id65EnvironmentSectorHeaderContract.Stamp(s.AsSpan(0,size-4));
        return s;
    }
    private static void ApplyCoupledPrivateAppearance(byte[] sector,IReadOnlyDictionary<int,NativeTerrainTextureLowDetailReplacement>? replacements)
    {
        if(replacements==null||replacements.Count==0)return;
        var companion=NativeTerrainTextureLowDetailCompanion.Build(sector[..^4],replacements);
        companion.After.CopyTo(sector,companion.LowDetailColorOffset);
    }
    private static IReadOnlyList<byte[]> RebuildCoupledLowDetail(byte[] staged,int at,SceneSectorHeader source,byte[] sourceRam,IReadOnlyDictionary<int,NativeTerrainTextureLowDetailReplacement>? replacements)
    {
        int lpFaces=at+28+4*(source.NumLpVertices+source.NumLpColours);

        for(int f=0;f<source.NumLpFaces;f++)
        {
            uint a=BinaryPrimitives.ReadUInt32LittleEndian(staged.AsSpan(lpFaces+8*f)),b=BinaryPrimitives.ReadUInt32LittleEndian(staged.AsSpan(lpFaces+8*f+4));
            uint v=a>>26&63,c=b>>26&63;
            Put(staged,lpFaces+8*f,(a&255)|(v<<26)|(v<<20)|(v<<14)|(v<<8));
            Put(staged,lpFaces+8*f+4,(b&255)|(c<<26)|(c<<20)|(c<<14)|(c<<8));
        }
        int hpVertices=at+GetSceneVertexOffset(source,"hp",0)-source.Offset;
        int hpColors=at+GetHpVertexDataEndOffset(source)-source.Offset;
        int hpFaces=hpColors+8*source.NumHpColours;
        List<(SpyroCollisionPoint[] Points,uint[] Colors,byte Flag0,byte Flag1)> faces=[];
        for(int f=0;f<source.NumHpFaces;f++)
        {
            byte[] bytes=staged.AsSpan(hpFaces+16*f,16).ToArray();
            if(bytes.AsSpan(0,4).IndexOfAnyExcept(bytes[0])<0)continue;
            var points=bytes.Take(4).Select(id=>ToCollisionPoint(DecodeSceneVertex(BinaryPrimitives.ReadUInt32LittleEndian(staged.AsSpan(hpVertices+4*id)),source))).ToArray();
            uint[] colors=bytes.Skip(4).Take(4).Select(id=>BinaryPrimitives.ReadUInt32LittleEndian(staged.AsSpan(hpColors+4*(source.NumHpColours+id)))).ToArray();
            if(replacements!=null)
            {
                int[] ids=bytes.Take(4).Distinct().Select(id=>(int)id).ToArray();
                var unique=ids.Select(id=>ToCollisionPoint(DecodeSceneVertex(BinaryPrimitives.ReadUInt32LittleEndian(staged.AsSpan(hpVertices+4*id)),source))).ToArray();
                // Reuse the same native companion issuer for each exact HP face,
                // then carry its four authored far-colour corners into LP-only output.
                byte[] nativeRam=new byte[source.Offset+source.SizeBytes];staged.AsSpan(at,source.SizeBytes).CopyTo(nativeRam.AsSpan(source.Offset));
                var temporary=BuildCoupledCopiedSector(nativeRam,source,bytes,ids,unique);
                ApplyCoupledPrivateAppearance(temporary,replacements);
                colors=Enumerable.Range(0,4).Select(i=>BinaryPrimitives.ReadUInt32LittleEndian(temporary.AsSpan(28+4*unique.Length+4*i))).ToArray();
            }
            var flags=NativeHpToLpFlags(bytes);
            int[] order=NativeHpToLpSlots(bytes);
            faces.Add((order.Select(i=>points[i]).ToArray(),order.Select(i=>colors[i]).ToArray(),flags.Item1,flags.Item2));
        }
        List<byte[]> sectors=[];
        List<List<(SpyroCollisionPoint[] Points,uint[] Colors,byte Flag0,byte Flag1)>> batches=[];
        foreach(var face in faces)
        {
            if(batches.Count==0)batches.Add([]);
            var batch=batches[^1];
            var points=batch.SelectMany(f=>f.Points).Concat(face.Points).Distinct().ToArray();
            int colors=batch.SelectMany(f=>f.Colors).Concat(face.Colors).Distinct().Count();
            if(batch.Count>0&&(points.Length>63||colors>63||points.Length+colors+2*(batch.Count+1)>255))batches.Add([]);
            batches[^1].Add(face);
        }
        foreach(var group in batches)
        {
            var batch=group.ToArray();
            var points=batch.SelectMany(f=>f.Points).Distinct().ToArray();
            uint[] colors=batch.SelectMany(f=>f.Colors).Distinct().ToArray();
            int n=points.Length,c=colors.Length,size=32+4*n+4*c+8*batch.Length;byte[] sector=new byte[size];
            int x=points.Min(p=>p.X),y=points.Min(p=>p.Y),z=points.Min(p=>p.Z),mx=points.Max(p=>p.X),my=points.Max(p=>p.Y),mz=points.Max(p=>p.Z);
            int radius=mx-x+my-y+mz-z;
            if(radius<=0||radius>4095)throw new InvalidOperationException("Remaining terrain exceeds the native LP sector bound.");
            Put(sector,0,(uint)(((x+mx)/2<<16)|((y+my)/2)));
            // Native selector80025B50..25BA8: bit14 suppresses the HP queue;
            // bit15 keeps this generated LP-only surface visible at close range.
            // Original Stone Hill LP-only sectors37/50 use the same0xC000 issuer.
            // Without bit14 the HP renderer enters an empty table layout which
            // its vertex/colour passes do not guard (8002686C..80026B34).
            Put(sector,4,(uint)(((z+mz)/2<<16)|radius|0xC000));Put(sector,8,(uint)(x<<16|y));Put(sector,12,(uint)(z<<16));
            new byte[]{(byte)n,(byte)c,(byte)batch.Length,0,0,0,0,0}.CopyTo(sector,16);Put(sector,24,uint.MaxValue);
            for(int i=0;i<n;i++)
            {
                int dx=points[i].X-x,dy=points[i].Y-y,dz=points[i].Z-z;
                if(dx>2047||dy>2047||dz>1023)throw new InvalidOperationException("Remaining terrain exceeds native LP coordinates.");
                Put(sector,28+4*i,(uint)(dx<<21|dy<<10|dz));
            }
            for(int i=0;i<c;i++)Put(sector,28+4*n+4*i,colors[i]);
            for(int f=0;f<batch.Length;f++)
            {
                uint vertices=0,shades=0;
                for(int corner=0;corner<4;corner++)
                {
                    vertices|=(uint)Array.IndexOf(points,batch[f].Points[corner])<<(26-6*corner);
                    shades|=(uint)Array.IndexOf(colors,batch[f].Colors[corner])<<(26-6*corner);
                }
                Put(sector,28+4*(n+c)+8*f,vertices|batch[f].Flag0);Put(sector,32+4*(n+c)+8*f,shades|batch[f].Flag1);
            }
            Id65EnvironmentSectorHeaderContract.Stamp(sector.AsSpan(0,size-4));sectors.Add(sector);
        }
        return sectors;
    }

    private static int[] NativeHpToLpSlots(byte[] hpFace)
    {
        // Original renderer 80026B78 reverses HP byte slots; 80026DD0 emits
        // corner strip0,1,3,2. LP80026404 consumes bits26,20,14,8 directly.
        bool flip=(hpFace[12]&2)!=0;
        if(!flip)return [3,2,0,1];
        if(hpFace.Take(4).Distinct().Count()==3)
        {
            if(hpFace[0]!=hpFace[1])throw new InvalidOperationException("Flipped triangle has no authenticated repeated native HP corner pair.");
            return [2,3,0,1];
        }
        return [3,0,2,1];
    }
    private static (byte,byte) NativeHpToLpFlags(byte[] hpFace)
    {
        byte material=hpFace[8];
        if(material!=255&&(material&128)!=0)
            throw new InvalidOperationException("Generated distant terrain requires an opaque source HP material; translucent texture blend ownership cannot be inferred.");
        // HP +0x0C bit2 and LP word0 bit7 both bypass NCLIP. Both place
        // OT depth in bits3..7. Generated LP uses transitionbias0; exact
        // source HP culling/material intent replaces unrelated coarse flags.
        return ((byte)((hpFace[12]&4)!=0?128:0),(byte)(hpFace[12]&248));
    }

    private static byte[] ReplaceCoupledSectors(byte[] original,IReadOnlyDictionary<int,byte[]> replacements)
    {
        if(replacements.Count==0)return original;
        int count=Read(original,4);List<byte[]> sectors=[];
        for(int i=0;i<count;i++)
        {
            int at=4+Read(original,8+4*i),end=i+1<count?4+Read(original,12+4*i):original.Length;
            if(at<8+4*count||end<at||end>original.Length)throw new InvalidOperationException("Native sector pointer partition is invalid.");
            if(!replacements.TryGetValue(i,out byte[]? replacement)){sectors.Add(original.AsSpan(at,end-at).ToArray());continue;}
            int exact=SceneSectorByteLength(original,at);
            if(exact>end-at||original.AsSpan(at+exact,end-at-exact).IndexOfAnyExcept((byte)0)>=0)
                throw new InvalidOperationException("Native sector has unknown pointer-gap bytes.");
            byte[] span=new byte[replacement.Length+end-at-exact];replacement.CopyTo(span,0);sectors.Add(span);
        }
        byte[] result=new byte[8+4*count+sectors.Sum(s=>s.Length)];Put(result,0,(uint)result.Length);Put(result,4,(uint)count);
        int cursor=8+4*count;for(int i=0;i<count;i++){Put(result,8+4*i,(uint)(cursor-4));sectors[i].CopyTo(result,cursor);cursor+=sectors[i].Length;}
        return result;
    }

    /// <summary>
    /// The low half of a sector header's Z word (+0x0C) is the sector's first index in the renderer's per-face
    /// cache: 1-based and contiguous over the high-detail faces of the retail sectors, 0xFFFF for a sector
    /// without high-detail faces. The game prepares that cache when the level loads, so an appended sector that
    /// shares an index (the writer used to leave 0) is never drawn. Each appended sector gets its own range
    /// after the level's last one, the way the authored-level writer numbers its sectors.
    /// </summary>
    private static void AssignCoupledFaceCache(byte[] environment,IReadOnlyList<byte[]> appended)
    {
        int count=Read(environment,4),next=1;
        for(int i=0;i<count;i++)
        {
            int at=4+Read(environment,8+4*i);
            int first=BinaryPrimitives.ReadUInt16LittleEndian(environment.AsSpan(at+12)),faces=environment[at+22];
            if(faces>0&&first!=0xFFFF)next=Math.Max(next,first+faces);
        }
        foreach(byte[] sector in appended)
        {
            int faces=sector[22];
            if(faces==0){BinaryPrimitives.WriteUInt16LittleEndian(sector.AsSpan(12),0xFFFF);continue;}
            if(next+faces>0xFFFF)throw new InvalidOperationException("The renderer's face cache cannot number another terrain sector; no terrain BIN was built.");
            BinaryPrimitives.WriteUInt16LittleEndian(sector.AsSpan(12),(ushort)next);next+=faces;
        }
    }

    private static byte[] AppendCoupledSectors(byte[] original,IReadOnlyList<byte[]> sectors)
    {
        if(sectors.Count==0)return original;int n=Read(original,4),extra=4*sectors.Count;
        byte[] result=new byte[original.Length+extra+sectors.Sum(s=>s.Length)];original.AsSpan(0,8+4*n).CopyTo(result);
        original.AsSpan(8+4*n).CopyTo(result.AsSpan(8+4*n+extra));Put(result,0,(uint)result.Length);Put(result,4,(uint)(n+sectors.Count));
        for(int i=0;i<n;i++)Put(result,8+4*i,(uint)(Read(original,8+4*i)+extra));
        int cursor=original.Length+extra;for(int i=0;i<sectors.Count;i++){Put(result,8+4*(n+i),(uint)(cursor-4));sectors[i].CopyTo(result,cursor);cursor+=sectors[i].Length;}
        return result;
    }
    private static byte[] AppendCoupledVisibility(byte[] original,int sourceCount,IReadOnlyList<int> sources)
    {
        if(sources.Count==0)return original;int end=4+Read(original,4),n=Read(original,8);List<byte[]> groups=[];
        for(int i=0;i<n;i++)
        {
            int at=4+Read(original,12+4*i);if(at<12+4*n||at>=end)throw new InvalidOperationException("Native visibility group is invalid.");
            List<byte> ids=[];while(at<end&&original[at]!=255)ids.Add(original[at++]);if(at>=end)throw new InvalidOperationException("Native visibility group has no terminator.");
            for(int j=0;j<sources.Count;j++)if(sources[j]<0 || ids.Contains((byte)sources[j]))ids.Add(checked((byte)(sourceCount+j)));
            ids.Add(255);while((ids.Count&3)!=0)ids.Add(255);groups.Add(ids.ToArray());
        }
        for(int j=0;j<sources.Count;j++)if(!groups.Any(g=>g.Contains((byte)(sourceCount+j))))throw new InvalidOperationException("Copied terrain source has no native visible group.");
        int newEnd=12+4*n+groups.Sum(g=>g.Length);byte[] result=new byte[newEnd+original.Length-end];
        Put(result,0,(uint)result.Length);Put(result,4,(uint)(newEnd-4));Put(result,8,(uint)n);int cursor=12+4*n;
        for(int i=0;i<n;i++){Put(result,12+4*i,(uint)(cursor-4));groups[i].CopyTo(result,cursor);cursor+=groups[i].Length;}
        original.AsSpan(end).CopyTo(result.AsSpan(newEnd));return result;
    }
    private static int Read(byte[] bytes,int at)=>BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(at));
    private static void Put(byte[] bytes,int at,uint value)=>BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at),value);
}
