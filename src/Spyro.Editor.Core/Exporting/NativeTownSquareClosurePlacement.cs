using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Exporting;

/// <summary>Closed research recipes, never caller-authored coordinates or ownership.</summary>
internal enum NativeTownSquareClosureRecipe { PropertiesAndSoundV2, SpatiallySeparatedV3 }

internal static class NativeTownSquareClosurePlacement
{
    internal const string ClearProfile = "native-closure.peace-ae.townsquare.experimental-spatial-separation.v3";
    internal const int CollisionOffset = 0x10A5C0, CollisionLength = 0x5FAE8;
    internal const int PatchHalfWidth = 512, ClearColumnHeight = 1536, MinimumCenterSeparation = 3072;
    internal const string CollisionHash = "84901b6b9faa2f7fb0fce1d3aaa7e00fadf49e2d4bd7b2bcb2a0bb9a77397e2f";

    internal static string ProfileFor(NativeTownSquareClosureRecipe recipe) => recipe switch
    {
        NativeTownSquareClosureRecipe.PropertiesAndSoundV2 => NativeTownSquareLockedChestClosureCompiler.Profile,
        NativeTownSquareClosureRecipe.SpatiallySeparatedV3 => ClearProfile,
        _ => throw new InvalidDataException("Unknown native closure recipe.")
    };

    internal static NativeTownSquarePlacementPoint[] Locations(NativeTownSquareClosureRecipe recipe)
    {
        _=ProfileFor(recipe);
        NativeTownSquarePlacementPoint[] old = [new(130150,111032,11264),new(132198,111032,11264),new(131788,112261,11776),
            new(132198,112261,11776),new(131583,111237,11776),new(131788,111647,11776),new(132402,111851,11776)];
        return recipe==NativeTownSquareClosureRecipe.PropertiesAndSoundV2?old:
            old.Select(p=>new NativeTownSquarePlacementPoint(p.X-10518,p.Y-8744,p.Z-3072)).ToArray();
    }

    internal static byte[] BuildPayload(byte[] data,byte[] executable)
        =>Encode(CaptureCertificate(data,executable,NativeTownSquareClosureRecipe.SpatiallySeparatedV3).Locations);

    internal static byte[] Encode(IReadOnlyList<NativeTownSquarePlacementPoint> points)
    {
        if(points.Count!=7)throw new InvalidDataException("Native family placement requires exactly seven instances.");
        byte[] result=new byte[84];
        for(int i=0;i<points.Count;i++)
        {Put(result,i*12,points[i].X);Put(result,i*12+4,points[i].Y);Put(result,i*12+8,points[i].Z);}
        return result;
    }

    internal static NativeTownSquarePlacementPoint Decode(byte[] payload,int instance)
    {
        if(payload.Length!=84||instance is <0 or >6)throw new InvalidDataException("Native family placement payload shape changed.");
        return new(I32(payload,instance*12),I32(payload,instance*12+4),I32(payload,instance*12+8));
    }

    // This validator is also exercised against the historical occupied site. The production
    // factory invokes it only for the closed V3 recipe; caller-authored sites are not accepted.
    internal static NativeTownSquarePlacementCertificate CaptureCertificate(byte[] data,byte[] executable,NativeTownSquareClosureRecipe recipe)
    {
        string profile=ProfileFor(recipe);
        if(data.Length!=0x2E2000||executable.Length!=0x66000||I32(data,8)!=0xDE800||I32(data,24)!=0x1D0000||I32(data,0x1D016C)!=107)
            throw new InvalidDataException("Native placement source domains changed.");
        int component=I32(data,8);
        for(int i=0;i<4;i++){int size=I32(data,component);if(size<4||component+(long)size>data.Length)throw new InvalidDataException("Native collision component chain changed.");component+=size;}
        if(component!=CollisionOffset||I32(data,component)!=CollisionLength||Hash(data.AsSpan(component,CollisionLength))!=CollisionHash)
            throw new InvalidDataException("Native placement collision source identity changed.");
        // Native lookup defaults to FF when the triangle has no explicit flag; masked63
        // returns -1 without dereferencing a special surface. No water/material guessing.
        if(Hash(executable.AsSpan(0x3E278,0x2C))!="46fa7263be2e2f47871c91710e16d8b9356efe1f402fa89bdb68350afa4b9d28"||
            Hash(executable.AsSpan(0x47B80,0x48))!="12a806592d9fdae28f2b5774dc8c773d2887f10c2154c496cf647f7f2277f7f2")
            throw new InvalidDataException("Native dry-surface flag/default consumer changed.");
        NativeTownSquarePlacementPoint[] positions=Locations(recipe);
        int ground=positions[1].Z;
        NativeTownSquarePlacementPoint landing=new(positions[1].X,positions[1].Y-2048,ground+358);
        var all=positions.Append(landing).ToArray();
        byte[] rows=data.AsSpan(0x1D0170,107*0x58).ToArray();
        List<NativeTownSquarePlacementNeighbor> neighbors=[];
        for(int i=0;i<107;i++)
        {
            ReadOnlySpan<byte> row=rows.AsSpan(i*0x58,0x58);
            ushort klass=BinaryPrimitives.ReadUInt16LittleEndian(row[0x36..]);int radius=row[0x50];
            var origin=new NativeTownSquarePlacementPoint(BinaryPrimitives.ReadInt32LittleEndian(row[12..]),BinaryPrimitives.ReadInt32LittleEndian(row[16..]),BinaryPrimitives.ReadInt32LittleEndian(row[20..]));
            double nearest=all.Min(p=>Math.Sqrt(SquaredDistance(p,origin)));
            RequireCenterSeparation(nearest);
            neighbors.Add(new(i,klass,radius,origin,nearest,nearest-MinimumCenterSeparation,Hash(row)));
        }
        int body=component+4,count=I32(data,body),flagCount=I32(data,body+4);
        int tree=body+I32(data,body+8),blocks=body+I32(data,body+12),tri=body+I32(data,body+16),flags=body+I32(data,body+24);
        if(count!=19808||flagCount!=10500||tree<body+0x1C||blocks<=tree||tri<=blocks||tri+count*12>component+CollisionLength||flags<tri+count*12||flags+flagCount>component+CollisionLength)
            throw new InvalidDataException("Native terrain triangle/index/flag domains changed.");
        byte[] blockBytes=data.AsSpan(blocks,tri-blocks).ToArray();int used=blockBytes.Length;
        while(used>=2&&BinaryPrimitives.ReadUInt16LittleEndian(blockBytes.AsSpan(used-2))==0)used-=2;
        var index=NativeCollisionIndexCodec.Decode(data.AsSpan(tree,blocks-tree),blockBytes,used,count);
        NativeTownSquarePlacementTriangle[] triangles=Enumerable.Range(0,count).Select(i=>ReadTriangle(data,tri+i*12,i,i<flagCount?data[flags+i]:(byte)0xFF)).ToArray();
        List<NativeTownSquarePlacementPatch> patches=[];
        for(int i=0;i<all.Length;i++)
        {
            int[] supported=ValidateFlatPatch(triangles,all[i].X,all[i].Y,ground);
            List<NativeTownSquarePlacementBinding> bindings=[];
            foreach(int t in supported)
            {
                var triangle=triangles[t];
                for(int y=(all[i].Y-PatchHalfWidth)>>12;y<=(all[i].Y+PatchHalfWidth)>>12;y++)
                for(int x=(all[i].X-PatchHalfWidth)>>12;x<=(all[i].X+PatchHalfWidth)>>12;x++)
                {
                    double loX=Math.Max(all[i].X-PatchHalfWidth,x*4096),hiX=Math.Min(all[i].X+PatchHalfWidth,(x+1)*4096);
                    double loY=Math.Max(all[i].Y-PatchHalfWidth,y*4096),hiY=Math.Min(all[i].Y+PatchHalfWidth,(y+1)*4096);
                    if(Math.Abs(Area(ClipBox(triangle.Points,loX,hiX,loY,hiY)))<0.001)continue;
                    NativeCollisionCell cell=new(x,y,ground>>12);
                    if(!index.Cells.TryGetValue(cell,out var binding)||!binding.OrderedTriangleIndexes.Contains(t))
                        throw new InvalidDataException("Native placement support triangle is not reachable from its actual collision cell.");
                    bindings.Add(new(t,tri+t*12,triangle.Flag,cell,tree+binding.TreePointerByteOffset,blocks+binding.SourceBlockWordOffset*2));
                }
            }
            patches.Add(new(i<7?107+i:-1,all[i],ground,PatchHalfWidth,ClearColumnHeight,supported,bindings.ToArray()));
        }
        return new(profile,new(-10518,-8744,-3072),positions,landing,Hash(rows),CollisionHash,
            Hash(executable.AsSpan(0x3E278,0x2C)),Hash(executable.AsSpan(0x47B80,0x48)),PatchHalfWidth,MinimumCenterSeparation,neighbors.ToArray(),patches.ToArray(),
            "Experimental center-separation research policy, NOT a physics/mesh footprint certificate or guarantee of flawless placement. Every imported instance center and proposed separate landing is over3072 RAW units horizontally from all107 original native centers, with no vertical-distance exemption. Ground tests prove only the exact authored ±512 dry-support squares and1536-high columns. Native render-radius bytes are retained as source evidence, never converted into physical dimensions. Full visual extent, moving native actors, collisions, key/chest interactions, effects and camera behavior remain mandatory runtime gates; normal Create BIN stays disabled.");
    }

    internal static void RequireCenterSeparation(double distance)
    {
        if(!double.IsFinite(distance)||distance<=MinimumCenterSeparation)
            throw new InvalidDataException("Native center-separation research policy rejects the occupied or too-close family site.");
    }

    internal static int[] ValidateFlatPatch(IReadOnlyList<NativeTownSquarePlacementTriangle> triangles,int x,int y,int ground)
    {
        List<(int Id,List<NativeTownSquarePlacementVector> Polygon)> flats=[];
        double loX=x-PatchHalfWidth,hiX=x+PatchHalfWidth,loY=y-PatchHalfWidth,hiY=y+PatchHalfWidth;
        foreach(var t in triangles)
        {
            var p=ClipBox(t.Points,loX,hiX,loY,hiY);
            if(p.Count==0)continue;
            bool flat=t.Points.All(q=>q.Z==ground)&&Area(t.Points)<0;
            if(flat&&Math.Abs(Area(p))>0.001)
            {
                if((t.Flag&0x3F)!=0x3F||t.ZFlags!=0)throw new InvalidDataException("Native placement touches a special or hazardous surface.");
                flats.Add((t.Index,p));
            }
            else if(Clip(Clip(p,q=>q.Z-ground-1),q=>ground+ClearColumnHeight-q.Z).Count>0)
                throw new InvalidDataException("Native placement clear column intersects nonfloor terrain.");
        }
        for(int i=0;i<flats.Count;i++)for(int j=i+1;j<flats.Count;j++)
            if(Math.Abs(Area(Intersect(flats[i].Polygon,flats[j].Polygon)))>0.01)
                throw new InvalidDataException("Native placement floor partition overlaps ambiguously.");
        if(Math.Abs(flats.Sum(f=>Math.Abs(Area(f.Polygon)))-4d*PatchHalfWidth*PatchHalfWidth)>0.01)
            throw new InvalidDataException("Native placement floor partition does not fully cover its footprint.");
        return flats.Select(f=>f.Id).Order().ToArray();
    }
    private static NativeTownSquarePlacementTriangle ReadTriangle(byte[] data,int offset,int index,byte flag)
    {
        uint x=U32(data,offset),y=U32(data,offset+4),z=U32(data,offset+8);
        var a=new NativeTownSquarePlacementVector((x&0x3FFF)*16,(y&0x3FFF)*16,(z&0x3FFF)*16);
        var b=new NativeTownSquarePlacementVector(a.X+Signed9(x>>14)*16,a.Y+Signed9(y>>14)*16,a.Z+((z>>16)&255)*16);
        var c=new NativeTownSquarePlacementVector(a.X+Signed9(x>>23)*16,a.Y+Signed9(y>>23)*16,a.Z+((z>>24)&255)*16);
        return new(index,a,b,c,flag,z&0xC000);
    }
    private static List<NativeTownSquarePlacementVector> ClipBox(IReadOnlyList<NativeTownSquarePlacementVector> p,double loX,double hiX,double loY,double hiY)
        =>Clip(Clip(Clip(Clip(p,q=>q.X-loX),q=>hiX-q.X),q=>q.Y-loY),q=>hiY-q.Y);
    private static List<NativeTownSquarePlacementVector> Clip(IReadOnlyList<NativeTownSquarePlacementVector> p,Func<NativeTownSquarePlacementVector,double> side)
    {
        List<NativeTownSquarePlacementVector> result=[];
        for(int i=0;i<p.Count;i++)
        {
            var a=p[i];var b=p[(i+1)%p.Count];double av=side(a),bv=side(b);bool ai=av>=0,bi=bv>=0;
            if(ai)result.Add(a);
            if(ai!=bi){double t=av/(av-bv);result.Add(new(a.X+t*(b.X-a.X),a.Y+t*(b.Y-a.Y),a.Z+t*(b.Z-a.Z)));}
        }
        return result;
    }
    private static List<NativeTownSquarePlacementVector> Intersect(IReadOnlyList<NativeTownSquarePlacementVector> polygon,IReadOnlyList<NativeTownSquarePlacementVector> bound)
    {
        var result=polygon.ToList();double sign=Math.Sign(Area(bound));
        for(int i=0;i<bound.Count&&result.Count>0;i++){var a=bound[i];var b=bound[(i+1)%bound.Count];result=Clip(result,q=>sign*((b.X-a.X)*(q.Y-a.Y)-(b.Y-a.Y)*(q.X-a.X)));}
        return result;
    }
    private static double Area(IReadOnlyList<NativeTownSquarePlacementVector> points)
    {double area=0;for(int i=0;i<points.Count;i++){var p=points[i];var q=points[(i+1)%points.Count];area+=p.X*q.Y-p.Y*q.X;}return area/2;}
    private static double SquaredDistance(NativeTownSquarePlacementPoint a,NativeTownSquarePlacementPoint b)=>(a.X-(double)b.X)*(a.X-(double)b.X)+(a.Y-(double)b.Y)*(a.Y-(double)b.Y);
    private static int Signed9(uint v)=>(v&256)!=0?(int)(v&511)-512:(int)(v&511);
    private static int I32(byte[] b,int o)=>BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(o,4));
    private static uint U32(byte[] b,int o)=>BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(o,4));
    private static void Put(byte[] b,int o,int value)=>BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(o),value);
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
}

internal sealed record NativeTownSquarePlacementPoint(int X,int Y,int Z);
internal sealed record NativeTownSquarePlacementVector(double X,double Y,double Z);
internal sealed record NativeTownSquarePlacementTriangle(int Index,NativeTownSquarePlacementVector A,NativeTownSquarePlacementVector B,NativeTownSquarePlacementVector C,byte Flag,uint ZFlags)
{internal IReadOnlyList<NativeTownSquarePlacementVector> Points=>new[]{A,B,C};}
internal sealed record NativeTownSquarePlacementNeighbor(int TrueIndex,ushort NativeClass,int NativeRadiusByte,NativeTownSquarePlacementPoint Origin,double NearestCenterDistance,double CenterSeparationAbovePolicy,string RowSha256);
internal sealed record NativeTownSquarePlacementBinding(int Triangle,int TriangleDataOffset,byte Flag,NativeCollisionCell Cell,int LeafDataOffset,int GroupDataOffset);
internal sealed record NativeTownSquarePlacementPatch(int TrueIndex,NativeTownSquarePlacementPoint Center,int GroundZ,int HalfWidth,int ClearHeight,IReadOnlyList<int> Triangles,IReadOnlyList<NativeTownSquarePlacementBinding> Bindings);
internal sealed record NativeTownSquarePlacementCertificate(string Profile,NativeTownSquarePlacementPoint Translation,IReadOnlyList<NativeTownSquarePlacementPoint> Locations,NativeTownSquarePlacementPoint RecommendedLanding,string OriginalRowsSha256,string CollisionSha256,string NativeFlagDefaultConsumerSha256,string NativeNoSpecialSurfaceConsumerSha256,int TestSquareHalfWidth,int MinimumCenterSeparation,IReadOnlyList<NativeTownSquarePlacementNeighbor> NativeNeighbors,IReadOnlyList<NativeTownSquarePlacementPatch> Patches,string Scope);
