using System.Buffers.Binary;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Exporting;

/// <summary>Authored triangles use the original executable's packed LP/HP and collision consumers.
/// Sector guards, full cell membership and per-face material bindings are generated together.</summary>
internal static class NativeAuthoredMesh
{
    internal sealed record Triangle(Vector3f A, Vector3f B, Vector3f C, ColorRgba Color, int Material, ColorRgba[] Near, ColorRgba[] Far);
    internal static byte[] Compile(SceneDocument document, IReadOnlyList<AuthoredMaterial> materials)
    {
        document.Validate();
        List<Triangle> triangles = [];
        foreach (var s in document.Surfaces)
        {
            int material = s.Material == null ? 255 : materials.IndexOf(s.Material);
            if (material < 0) throw new InvalidDataException("An authored material was omitted.");
            var near=s.NearColors??Enumerable.Repeat(s.Color,s.Vertices.Length).ToArray();
            var far=s.FarColors??Enumerable.Repeat(s.Color,s.Vertices.Length).ToArray();
            triangles.Add(new(s.Vertices[0], s.Vertices[1], s.Vertices[2], s.Color,material,[near[0],near[1],near[2]],[far[0],far[1],far[2]]));
            if (s.Vertices.Length == 4) triangles.Add(new(s.Vertices[0],s.Vertices[2],s.Vertices[3],s.Color,material,[near[0],near[2],near[3]],[far[0],far[2],far[3]]));
        }
        if (triangles.Count is < 1 or > 255) throw new InvalidDataException("The native visibility list supports at most 255 authored triangles.");
        int n = triangles.Count;
        const int span = 116;
        byte[] environment = new byte[8+n*(4+span)]; W(environment,0,(uint)environment.Length); W(environment,4,(uint)n);
        for (int i=0;i<n;i++)
        {
            int at=8+n*4+i*span; W(environment,8+i*4,(uint)(at-4));
            EncodeSector(environment.AsSpan(at,span),triangles[i]);
        }
        int list=(n+4)&~3, skyAt=16+list;
        byte[] visibility=new byte[skyAt+12]; W(visibility,0,(uint)visibility.Length);W(visibility,4,(uint)(skyAt-4));W(visibility,8,1);W(visibility,12,12);
        visibility.AsSpan(16,list).Fill(255);for(int i=0;i<n;i++)visibility[16+i]=(byte)i;
        W(visibility,skyAt,1);W(visibility,skyAt+4,8);visibility.AsSpan(skyAt+8,4).Fill(255);
        byte[] special=new byte[8];W(special,0,8);
        Dictionary<NativeCollisionCell,List<int>> cells=[];
        for(int i=n-1;i>=0;i--)
        {
            var t=triangles[i];var vs=new[]{t.A,t.B,t.C};
            for(int z=Raw(vs.Min(v=>v.Z))>>12;z<=Raw(vs.Max(v=>v.Z))>>12;z++)
            for(int y=Raw(vs.Min(v=>v.Y))>>12;y<=Raw(vs.Max(v=>v.Y))>>12;y++)
            for(int x=Raw(vs.Min(v=>v.X))>>12;x<=Raw(vs.Max(v=>v.X))>>12;x++)
            {var cell=new NativeCollisionCell(x,y,z);if(!cells.TryGetValue(cell,out var rows))cells.Add(cell,rows=[]);rows.Add(i);}
        }
        var index=NativeCollisionIndexCodec.Create(cells.Select(p=>new KeyValuePair<NativeCollisionCell,IReadOnlyList<int>>(p.Key,p.Value)),n);
        byte[] tree=index.CopyTree(),blocks=index.CopyBlocks();int start=32+tree.Length+blocks.Length,assign=start+n*12,flags=assign+Align4(n);
        byte[] collision=new byte[flags+Align4(n)];W(collision,0,(uint)collision.Length);W(collision,4,(uint)n);W(collision,8,(uint)n);W(collision,12,28);
        W(collision,16,(uint)(28+tree.Length));W(collision,20,(uint)(start-4));W(collision,24,(uint)(assign-4));W(collision,28,(uint)(flags-4));
        tree.CopyTo(collision,32);blocks.CopyTo(collision,32+tree.Length);
        for(int i=0;i<n;i++)
        {
            var t=triangles[i];var a=t.A;var b=t.B;var c=t.C;
            if((b.X-a.X)*(c.Y-a.Y)-(b.Y-a.Y)*(c.X-a.X)>0)(b,c)=(c,b);
            W(collision,start+i*12,Axis(a.X,b.X,c.X));W(collision,start+i*12+4,Axis(a.Y,b.Y,c.Y));W(collision,start+i*12+8,Axis(a.Z,b.Z,c.Z));
        }
        collision.AsSpan(flags,n).Fill(0x3F);
        byte[] sky=new byte[16];W(sky,0,16);W(sky,4,Rgb(document.SkyColor));
        byte[] bundle=[..environment,..visibility,..special,..collision,..sky];
        if(bundle.Length>0x20000)throw new InvalidDataException("Authored surface exceeds the bounded native surface allocation.");
        return bundle;
    }
    private static void EncodeSector(Span<byte> s,Triangle t)
    {
        Vector3f[] v=[t.A,t.B,t.C];int x=v.Min(v=>Unit(v.X)),y=v.Min(v=>Unit(v.Y)),z=v.Min(v=>Unit(v.Z));
        int maxX=v.Max(v=>Unit(v.X)),maxY=v.Max(v=>Unit(v.Y)),maxZ=v.Max(v=>Unit(v.Z));
        int radius=maxX-x+maxY-y+maxZ-z;
        if(radius==0)throw new InvalidDataException("An authored triangle is degenerate.");
        var ab=new Vector3f(t.B.X-t.A.X,t.B.Y-t.A.Y,t.B.Z-t.A.Z);var ac=new Vector3f(t.C.X-t.A.X,t.C.Y-t.A.Y,t.C.Z-t.A.Z);
        if(ab.Y*ac.Z-ab.Z*ac.Y==0 && ab.Z*ac.X-ab.X*ac.Z==0 && ab.X*ac.Y-ab.Y*ac.X==0)throw new InvalidDataException("An authored triangle has zero area.");
        W(s,0,(uint)(((x+maxX)/2<<16)|((y+maxY)/2)));W(s,4,(uint)(((z+maxZ)/2<<16)|radius));W(s,8,(uint)((x<<16)|y));W(s,12,(uint)(z<<16));
        new byte[]{3,3,1,0,3,3,1,8}.CopyTo(s[16..]);W(s,24,uint.MaxValue);
        for(int i=0;i<3;i++)
        {
            int dx=Unit(v[i].X)-x,dy=Unit(v[i].Y)-y,dz=Unit(v[i].Z)-z;
            if(dx>2047||dy>2047||dz>1023)throw new InvalidDataException("Authored sector vertex exceeds native packed coordinates.");
            uint vertex=(uint)((dx<<21)|(dy<<10)|dz);W(s,28+i*4,vertex);W(s,60+i*4,vertex);
            W(s,40+i*4,Rgb(t.Far[i]));W(s,72+i*4,Rgb(t.Far[i]));W(s,84+i*4,Rgb(t.Near[i]));
        }
        uint low=(1u<<20)|(2u<<14)|(2u<<8), high=(1u<<16)|(2u<<24);
        W(s,52,low);W(s,56,low);W(s,96,high);W(s,100,high);W(s,104,(uint)t.Material);W(s,108,0x80);
        Id65EnvironmentSectorHeaderContract.Stamp(s[..112]);
        var header=Id65EnvironmentSectorHeaderContract.ReadPointerSpan(s,s.Length,false,"authored triangle");
        if(header.ExactSectorByteLength!=112||header.PointerGapByteLength!=4)throw new InvalidDataException("Authored triangle sector lost its native prefetch guard.");
    }
    private static uint Axis(float a,float b,float c)
    {
        int origin=Unit(a),db=Unit(b)-origin,dc=Unit(c)-origin;
        if(db is <-256 or >255||dc is <-256 or >255)throw new InvalidDataException("An authored collision triangle spans more than its exact native delta range. Subdivide it before export.");
        return (uint)origin|(((uint)db&511)<<14)|(((uint)dc&511)<<23);
    }
    private static int Raw(float value)=>checked(Unit(value)*16);
    private static int Unit(float value)
    {if(value!=MathF.Truncate(value)||value is <0 or >16383)throw new InvalidDataException("Native authored vertices require whole editor units (the exact 16-unit game grid).");return (int)value;}
    private static int Align4(int x)=>(x+3)&~3;
    private static uint Rgb(ColorRgba c)=>(uint)(c.R|c.G<<8|c.B<<16);
    private static void W(Span<byte>b,int at,uint v)=>BinaryPrimitives.WriteUInt32LittleEndian(b[at..],v);
    private static int IndexOf<T>(this IReadOnlyList<T> list,T item){for(int i=0;i<list.Count;i++)if(EqualityComparer<T>.Default.Equals(list[i],item))return i;return -1;}
}
