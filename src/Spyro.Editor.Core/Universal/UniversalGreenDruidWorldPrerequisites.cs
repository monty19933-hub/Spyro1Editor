using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Universal;
using Spyro.Editor.Core.Analysis;

namespace Spyro.Editor.Core.Universal;

/// <summary>Inert original group0 resource certificate. This does not admit placement,
/// install a particle handler, or authorize an original-world-bank guard exception.</summary>
public sealed class UniversalGreenDruidWorldPrerequisites
{
    private readonly UniversalLevelData _source;
    private readonly UniversalOverlay _overlay;
    private readonly byte[][] _animationRecords;
    private readonly int[] _animationRoles;
    private readonly byte[][] _sectors;
    public const string DataSha = "507c7b327bdfe9e4c230bef7d93da5d8c935a9a9f033ad99de22a5af8dfd0ddf";
    public const string OverlaySha = "f7c9a174f64959fb53b4058f9786df223a9c142b2289563ca31edc6a24c6d383";
    public const string ExeSha = "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998";
    private static readonly int[] ActorRows = [5,48];
    private static readonly int[] ActorClasses = [0x10E,0x148];
    private static readonly int[] SectorIds = [170];
    private static readonly int[] StaticCollisionIds=[4207,4217,4218,4219,4220,4229,4259,4290,4291,4292,4293,4294,4295,4296,4297,4298,4299,4300,4301,4302,4303,4304,4312,4313,4314,4315,4316,4319];
    public int FixedCollisionTriangleCount=>StaticCollisionIds.Length;
    public IReadOnlyList<int> RequiredFixedSourceTriangles=>StaticCollisionIds.ToArray();
    public byte[] CopyFixedCollision(IReadOnlyList<int> deltaRaw)
    {
        Require(deltaRaw.Count==3&&deltaRaw.All(d=>d%16==0),"whole-unit fixed collision translation");var c=World(_source)[4];int ta=4+I(c,20);byte[] output=new byte[StaticCollisionIds.Length*12];
        for(int n=0;n<StaticCollisionIds.Length;n++)for(int a=0;a<3;a++){uint old=U(c,ta+StaticCollisionIds[n]*12+a*4);int v=checked((int)(old&16383)+deltaRaw[a]/16);Require(v is >=0 and <16384,"fixed collision base world bounds");W(output,n*12+a*4,(int)((old&~16383u)|(uint)v));}
        for(int n=0;n<StaticCollisionIds.Length;n++)foreach(var v in GreenDruidAnimatedCollisionStorage.Points(output,n))Require(v.All(a=>a is >=0 and <16384),"all fixed collision vertices in native world");return output;
    }
    private static readonly int[] MaterialIds = [21,28,43];
    /// <summary>Detached original records and complete right-half GPU upload;
    /// no address relocation, host capacity or native-renderer admission.</summary>
    public IReadOnlyList<SourceMaterial> CopyMaterials()
    {
        var table=World(_source)[0];int count=I(table,4);return MaterialIds.Select(id=>new SourceMaterial(id,[..table.AsSpan(8+id*16,16),..table.AsSpan(8+count*16+id*168,168)])).ToArray();
    }
    public byte[] CopySourceVram()=>_source.Vram().ToArray();
    public sealed record SourceMaterial(int SourceMaterialId,byte[] Record);
    public int CollisionTriangleCount => 16;
    public int CollisionFrameCount => 7;
    public int GeometryFrameCount => 2;
    public int CommandCount => 100;
    public IReadOnlyList<int> NativeRuntimeBankSlots => new[]{0,1,4,2,8,5,3};
    public bool ForeignPlacementAdmitted => false;
    public IReadOnlyList<int> RequiredSourceRows => ActorRows.ToArray();
    public IReadOnlyList<int> RequiredSourceSectors => SectorIds.ToArray();
    public IReadOnlyList<int> RequiredSourceMaterials => MaterialIds.ToArray();
    private UniversalGreenDruidWorldPrerequisites(UniversalLevelData source,UniversalOverlay overlay,byte[][] records,int[] roles,byte[][] sectors)
    { _source=new(source.Bytes.ToArray());_overlay=new(overlay.Bytes.ToArray(),overlay.Bindings.ToDictionary());_animationRecords=records;_animationRoles=roles;_sectors=sectors; }
    private static int I(ReadOnlySpan<byte> b,int at)=>BinaryPrimitives.ReadInt32LittleEndian(b[at..]);
    private static uint U(ReadOnlySpan<byte> b,int at)=>BinaryPrimitives.ReadUInt32LittleEndian(b[at..]);
    private static void W(Span<byte> b,int at,int v)=>BinaryPrimitives.WriteInt32LittleEndian(b[at..],v);
    private static string H(ReadOnlySpan<byte> b)=>Convert.ToHexStringLower(SHA256.HashData(b));
    private static void Require(bool ok,string why){if(!ok)throw new InvalidDataException("Green Druid world prerequisite: "+why);}
    private static byte[][] World(UniversalLevelData d)
    { var sf=d.Subfiles()[1];int at=sf.Offset;List<byte[]> parts=[];for(int n=0;n<6;n++){int size=I(d.Bytes,at);Require(size>=4&&size%4==0&&at+size<=sf.Offset+sf.Length,"world component bounds");parts.Add(d.Bytes.AsSpan(at,size).ToArray());at+=size;}return parts.ToArray(); }
    private static byte[][] Animations(UniversalLevelData d)
    {var sf=d.Subfiles()[3];int at=sf.Offset+0x88;List<byte[]> parts=[];for(int n=0;n<7;n++){int size=I(d.Bytes,at);Require(size>=8&&size%4==0&&at+size<=sf.Offset+sf.Length,"animation component bounds");parts.Add(d.Bytes.AsSpan(at,size).ToArray());at+=size;}Require(at==sf.Offset+d.Scene().MobyComponent,"native seven-bank scene prefix");return parts.ToArray();}
    public static UniversalGreenDruidWorldPrerequisites CaptureSource(string sourceKey,UniversalLevelData source,UniversalOverlay overlay,byte[] exe)
    {
        Require(sourceKey=="alpineridge"&&H(source.Bytes)==DataSha&&H(overlay.Bytes)==OverlaySha&&H(exe)==ExeSha,"complete original source/native owners");
        byte[] bindings=new byte[overlay.Bindings.Count*16];int cursor=0;foreach(var b in overlay.Bindings.OrderBy(b=>b.Key))foreach(uint v in new[]{b.Value.Global,b.Value.LuiAddress,b.Value.AddiuAddress,b.Value.Value}){W(bindings,cursor,(int)v);cursor+=4;}Require(overlay.Bindings.All(b=>b.Key==b.Value.Global)&&H(bindings)=="461f8f271b5653a1f4268d41cd3aadba45d27dc24cf06013965da839c2212850","complete source callback load-site identities");
        Require(overlay.Update==0x8007BB00&&overlay.Init==0x8008A36C&&overlay.CopyBuffer==0x8008DEC0&&overlay.Bindings[UniversalExeBindings.ParticleSpawnGlobal].Value==0x8008C9D8&&overlay.Bindings[UniversalExeBindings.ParticleUpdateGlobal].Value==0x8008AF54,"source callback identities");
        for(int n=0;n<ActorRows.Length;n++)Require(source.CaptureRow(sourceKey,ActorRows[n]).ClassId==ActorClasses[n],"source actor class");
        var d=source.CaptureRow(sourceKey,5);var scenery=source.CaptureRow(sourceKey,48);
        Require(d.Properties.Length==148&&I(d.Properties,4)==0&&I(d.Properties,8)==48&&I(d.Properties,12)==-1&&I(d.Properties,16)==1&&d.InternalPointers.SequenceEqual(new[]{(0,92)})&&d.Properties[92]==3&&d.ExternalRowPointers.Count==0&&d.DroppedExternalPointers==0,"druid5 group0/target48/mode1 and complete three-node path");
        Require(scenery.Properties.Length==4&&I(scenery.Properties,0)==-1,"scenery48 initial native cache");
        var banks=Animations(source);List<byte[]> records=[];List<int> roles=[];
        for(int role=0;role<7;role++)
        {
            var b=banks[role];int count=I(b,4);int[] ptrs=Enumerable.Range(0,count).Select(n=>4+I(b,8+n*4)).ToArray();
            foreach(int at in ptrs.Where(at=>b[at]==0))
            {
                Require(role is 2 or 3 or 4,"unexpected group0 world role");int end=ptrs.Where(x=>x>at).DefaultIfEmpty(b.Length).Min();byte[] record=b.AsSpan(at,end-at).ToArray();
                Require(record.Length>=812&&I(record,8)==812&&record[1]==2&&record[2]==0&&record[3]==1,"original paused command0 and exact100-command owner");
                int stride=role==4?16*12:(int)(U(record,4)>>16),frames=role==4?7:2;
                Require(stride>0&&record.Length==812+stride*frames,"complete keyframe bank");
                for(int cmd=0;cmd<100;cmd++){int p=12+cmd*8;Require(record[p+2]<100&&record[p+3]<100&&record[p+5]<frames&&record[p+6]<frames,"command branch and both frame indices bounded");}
                records.Add(record);roles.Add(role);
            }
        }
        Require(roles.Count(r=>r==2)==1&&roles.Count(r=>r==3)==1&&roles.Count(r=>r==4)==1,"all three group0 record owners required");
        Require(records.Where((_,i)=>roles[i]==4).Single().AsSpan(4,4).SequenceEqual(new byte[]{16,0,0,0}),"complete original16-triangle interval");
        var world=World(source);byte[] env=world[1];byte[][] sectors=SectorIds.Select(id=>{int at=4+I(env,8+id*4),end=4+I(env,8+(id+1)*4);return env.AsSpan(at,end-at).ToArray();}).ToArray();
        for(int n=0;n<1;n++)foreach(int role in new[]{2,3})
        {byte[] record=records.Where((_,i)=>roles[i]==role).Single(r=>(U(r,4)&65535)==SectorIds[n]);Require((U(record,4)>>16)==48&&sectors[n][role==2?20:16]==30,"exact twelve animated vertices within the complete thirty-vertex native tier");}
        var used=sectors.SelectMany(s=>Enumerable.Range(0,s[22]).Select(n=>(int)(U(s,28+4*s[23]+4*s[20]+8*s[21]+16*n+8)&127))).Distinct().Order().ToArray();Require(used.SequenceEqual(MaterialIds),"all authored native material owners");
        int countMaterials=I(world[0],4);foreach(int id in MaterialIds){byte[] material=[..world[0].AsSpan(8+id*16,16),..world[0].AsSpan(8+countMaterials*16+id*168,168)];_=GreenDruidWorldMaterialStorage.Describe(material);}
        byte[] collision=world[4];int flag=4+I(collision,28);Require(collision.AsSpan(flag,16).IndexOfAnyExcept((byte)63)<0,"all animated collision surface flags");
        int tri=4+I(collision,20),assignment=4+I(collision,24),flagsCount=I(collision,8);Require(StaticCollisionIds.All(id=>id>=flagsCount&&id<I(collision,4)),"fixed faces retain native defaultFF surface flags");
        Require(StaticCollisionIds.All(id=>collision[assignment+id] is 16 or 255),"fixed collision has only source occlusion16 or unrestricted context");
        var sector=sectors.Single();int vertex=28+4*sector[23],face=vertex+4*sector[20]+8*sector[21];int[] origin=[(int)(U(sector,8)>>16),(int)(U(sector,8)&65535),(int)(U(sector,12)>>16)];
        int[][] points=Enumerable.Range(0,30).Select(n=>{uint w=U(sector,vertex+n*4);return new[]{origin[0]+(int)(w>>21),origin[1]+(int)((w>>10)&2047),origin[2]+(int)(w&1023)};}).ToArray();HashSet<int> matched=[];
        for(int f=8;f<22;f++){int[] ids=sector.AsSpan(face+f*16,4).ToArray().Select(b=>(int)b).ToArray();Require(ids.All(i=>i is >=12 and <30),"all fixed native face vertices retained");var found=StaticCollisionIds.Where(id=>GreenDruidAnimatedCollisionStorage.Points(collision.AsSpan(tri+id*12,12).ToArray(),0).All(p=>ids.Any(i=>p.SequenceEqual(points[i])))).ToArray();Require(found.Length==2,"two exact original collision triangles own each fixed quad");matched.UnionWith(found);}
        Require(matched.SetEquals(StaticCollisionIds),"all fixed collision owners correspond to issued source faces");
        return new(source,overlay,records.ToArray(),roles.ToArray(),sectors);
    }
    /// <summary>Enrolls only original ordinals, never destination rows. Still inert.</summary>
    public UniversalRowTemplate Enroll(int sourceRow)
    {
        int index=Array.IndexOf(ActorRows,sourceRow);Require(index>=0,"unenrolled world variant");var t=_source.CaptureRow("alpineridge",sourceRow);
        UniversalSourceRowIndex[] links=sourceRow==5?[new(8,48,0x148)]:[];
        return t with{SourceRowIndices=links,SourcePrerequisiteRows=ActorRows.Where(r=>r!=sourceRow).Select(r=>new UniversalSourcePrerequisite(r,ActorClasses[Array.IndexOf(ActorRows,r)])).ToArray()};
    }
    /// <summary>Copies source-relative sector geometry and absolute collision frames
    /// under the native sixteen-raw-unit grid; no host bytes or native cache changed.</summary>
    public Translation CopyTranslated(IReadOnlyList<int> deltaRaw)
    {
        Require(deltaRaw.Count==3&&deltaRaw.All(d=>d%16==0),"common whole-unit world translation");int[] delta=deltaRaw.Select(d=>d/16).ToArray();byte[][] sectors=_sectors.Select(s=>s.ToArray()).ToArray();
        foreach(byte[] s in sectors)
        {
            foreach(int at in new[]{0,8}){uint old=U(s,at);int x=checked((int)(old>>16)+delta[0]),y=checked((int)(old&65535)+delta[1]);Require(x is >=0 and <16384&&y is >=0 and <16384,"translated sector origin/center bounds");W(s,at,(x<<16)|y);}
            foreach(int at in new[]{4,12}){uint old=U(s,at);int z=checked((int)(old>>16)+delta[2]);Require(z is >=0 and <16384,"translated sector altitude bounds");W(s,at,(z<<16)|(int)(old&65535));}
        }
        byte[][] records=_animationRecords.Select(r=>r.ToArray()).ToArray();
        for(int n=0;n<records.Length;n++)
        {
            byte[] r=records[n];if(_animationRoles[n]==4){for(int at=812;at<r.Length;at+=4){uint old=U(r,at);int p=checked((int)(old&16383)+delta[((at-812)/4)%3]);Require(p is >=0 and <16384,"translated collision base bounds");W(r,at,(int)((old&~16383u)|(uint)p));}}
            else
            {
                int s=Array.IndexOf(SectorIds,(int)(U(r,4)&65535));int[] origin=[(int)(U(sectors[s],8)>>16),(int)(U(sectors[s],8)&65535),(int)(U(sectors[s],12)>>16)];
                for(int at=812;at<r.Length;at+=4){uint p=U(r,at);int[] xyz=[(int)(p>>21),(int)((p>>10)&2047),(int)(p&1023)];Require(Enumerable.Range(0,3).All(a=>origin[a]+xyz[a] is >=0 and <16384),"complete translated geometry keyframe bounds");}
            }
        }
        byte[] collision=records.Where((_,i)=>_animationRoles[i]==4).Single();
        static int Signed9(uint v){int n=(int)(v&511);return (n&256)!=0?n-512:n;}
        for(int at=812;at<collision.Length;at+=12)for(int axis=0;axis<3;axis++){uint w=U(collision,at+axis*4);int a=(int)(w&16383),b=a+Signed9(w>>14),c=a+Signed9(w>>23);Require(a is >=0 and <16384&&b is >=0 and <16384&&c is >=0 and <16384,"every translated animated collision vertex stays in the native world");}
        List<TranslatedActor> actors=[];
        foreach(int row in ActorRows)
        {
            var t=Enroll(row);byte[] rr=t.Row.ToArray(),props=t.Properties.ToArray();for(int a=0;a<3;a++){int v=checked(I(rr,12+a*4)+deltaRaw[a]);Require(v is >=0 and <262144,"translated actor bounds");W(rr,12+a*4,v);}
            if(row==5){int head=92;for(int node=0;node<3;node++)for(int a=0;a<3;a++){int at=head+8+node*16+a*4;int v=checked(I(props,at)+deltaRaw[a]);Require(v is >=0 and <262144,"translated complete native path bounds");W(props,at,v);}}
            actors.Add(new(row,rr,t with{Properties=props}));
        }
        return new(deltaRaw.ToArray(),actors.ToArray(),sectors,records,_animationRoles.ToArray());
    }
    /// <summary>The animation bank has signed9-bit Z deltas. The native
    /// initializer rotates each triangle to the lowest-Z vertex and packs
    /// unsigned8-bit Z deltas for collision queries. Do not confuse those formats.</summary>
    public byte[][] CopyCanonicalCollisionFrames(IReadOnlyList<int> deltaRaw)
    {
        var translated=CopyTranslated(deltaRaw);var record=translated.AnimationRecords.Where((_,i)=>translated.Roles[i]==4).Single();List<byte[]> result=[];
        static int Signed9(uint v){int n=(int)(v&511);return (n&256)!=0?n-512:n;}
        for(int frame=0;frame<7;frame++)
        {
            byte[] output=new byte[16*12];
            for(int triangle=0;triangle<16;triangle++)
            {
                uint[] words=Enumerable.Range(0,3).Select(axis=>U(record,812+frame*16*12+triangle*12+axis*4)).ToArray();
                int[] a=words.Select(w=>(int)(w&16383)).ToArray(),b=words.Select((w,i)=>a[i]+Signed9(w>>14)).ToArray(),c=words.Select((w,i)=>a[i]+Signed9(w>>23)).ToArray();
                if(b[2]<a[2]&&b[2]<=c[2])(a,b,c)=(b,c,a);else if(c[2]<a[2]&&c[2]<b[2])(a,b,c)=(c,a,b);
                for(int axis=0;axis<3;axis++)
                {
                    int db=b[axis]-a[axis],dc=c[axis]-a[axis];Require(a[axis] is >=0 and <16384&&(axis==2?db is >=0 and <256&&dc is >=0 and <256:db is >=-256 and <256&&dc is >=-256 and <256),"canonical native collision delta bounds");
                    uint packed=(uint)a[axis]|(axis==2?(uint)(db<<16)|(uint)(dc<<24):((uint)db&511)<<14|((uint)dc&511)<<23);W(output,triangle*12+axis*4,(int)packed);
                }
            }
            result.Add(output);
        }
        return result.ToArray();
    }
    public sealed record TranslatedActor(int SourceRow,byte[] Row,UniversalRowTemplate Template);
    public sealed record Translation(int[] DeltaRaw,IReadOnlyList<TranslatedActor> Actors,byte[][] Sectors,byte[][] AnimationRecords,int[] Roles);
    /// <summary>Produces a resource map only, after assigning actual appended owners.
    /// This is not a certificate for native renderer queues or particle8 lifetime.</summary>
    public Translation Rebind(Translation translated,byte groupId,IReadOnlyList<int> destinationSectors,int firstTriangle,IReadOnlyList<int> physicalRows)
    {
        var expected=CopyTranslated(translated.DeltaRaw);
        Require(translated.Roles.SequenceEqual(expected.Roles)&&translated.AnimationRecords.Length==expected.AnimationRecords.Length&&translated.Sectors.Length==expected.Sectors.Length&&translated.Actors.Count==2,"issued translation resource topology");
        for(int n=0;n<expected.AnimationRecords.Length;n++)Require(translated.AnimationRecords[n].AsSpan().SequenceEqual(expected.AnimationRecords[n]),"issued complete command/frame resource");
        for(int n=0;n<expected.Sectors.Length;n++)Require(translated.Sectors[n].AsSpan().SequenceEqual(expected.Sectors[n]),"issued translated source sector");
        for(int n=0;n<2;n++){var a=translated.Actors[n];var b=expected.Actors[n];Require(a.SourceRow==b.SourceRow&&a.Row.AsSpan().SequenceEqual(b.Row)&&a.Template.SourceLevelKey==b.Template.SourceLevelKey&&a.Template.SourceRowIndex==b.Template.SourceRowIndex&&a.Template.Row.AsSpan().SequenceEqual(b.Template.Row)&&a.Template.Properties.AsSpan().SequenceEqual(b.Template.Properties)&&a.Template.InternalPointers.SequenceEqual(b.Template.InternalPointers)&&a.Template.SourceRowIndices.SequenceEqual(b.Template.SourceRowIndices)&&a.Template.SourcePrerequisiteRows.SequenceEqual(b.Template.SourcePrerequisiteRows)&&a.Template.ExternalRowPointers.SequenceEqual(b.Template.ExternalRowPointers)&&a.Template.DroppedExternalPointers==b.Template.DroppedExternalPointers,"issued initial actor/path/ordinal/source identity");}
        Require(destinationSectors.Count==1&&destinationSectors.Distinct().Count()==1&&destinationSectors.All(s=>s is >=0 and <255),"one unique byte-visible native sector owners");
        Require(firstTriangle>=0&&firstTriangle+16<=65535,"native animated collision interval");
        Require(physicalRows.Count==2&&physicalRows.Distinct().Count()==2&&physicalRows.All(r=>r is >=0 and <=255),"both actor owners including byte-indexed particle8 target");
        Require(groupId==0,"original druid compares group0 explicitly; first slice preserves that context");
        var records=translated.AnimationRecords.Select(r=>r.ToArray()).ToArray();for(int n=0;n<records.Length;n++){records[n][0]=groupId;if(translated.Roles[n]==4)W(records[n],4,(firstTriangle<<16)|16);else{int id=(int)(U(records[n],4)&65535),slot=Array.IndexOf(SectorIds,id);Require(slot>=0,"unenrolled source sector");W(records[n],4,(int)(U(records[n],4)&0xFFFF0000u)|destinationSectors[slot]);}}
        var actors=translated.Actors.Select(a=>{byte[] p=a.Template.Properties.ToArray();if(a.SourceRow==5)W(p,4,groupId);return a with{Row=a.Row.ToArray(),Template=a.Template with{Properties=p}};}).ToArray();
        return translated with{Actors=actors,Sectors=translated.Sectors.Select(s=>s.ToArray()).ToArray(),AnimationRecords=records,Roles=translated.Roles.ToArray(),DeltaRaw=translated.DeltaRaw.ToArray()};
    }
}
