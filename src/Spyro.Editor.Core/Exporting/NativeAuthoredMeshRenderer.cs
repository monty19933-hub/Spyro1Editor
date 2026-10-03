using System.Buffers.Binary;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.Exporting;

/// <summary>Native LP/HP batches, with queue capacity proved over every face,
/// including the native all-sector camera fallback. Collision remains exact.</summary>
internal static class NativeAuthoredMeshRenderer
{
    private sealed record Face(Vector3f[] Points,ColorRgba[] Near,ColorRgba[] Far,int Material);
    private sealed class Batch
    {
        internal readonly List<Face> Faces=[];
        internal readonly List<Vector3f> Vertices=[];
        internal readonly List<(ColorRgba Near,ColorRgba Far)> Colors=[];
        internal bool Fits(Face f)
        {
            var v=Vertices.Concat(f.Points).Distinct().ToArray();
            int colors=Colors.Concat(f.Near.Zip(f.Far)).Distinct().Count();
            return v.Length<=64&&v.Length<=170&&colors<=64&&v.Length+colors+2*(Faces.Count+1)<=255&&
                v.Max(p=>p.X)-v.Min(p=>p.X)<=2047&&v.Max(p=>p.Y)-v.Min(p=>p.Y)<=2047&&v.Max(p=>p.Z)-v.Min(p=>p.Z)<=1023&&
                v.Max(p=>p.X)-v.Min(p=>p.X)+v.Max(p=>p.Y)-v.Min(p=>p.Y)+v.Max(p=>p.Z)-v.Min(p=>p.Z)<=8191;
        }
        internal void Add(Face f)
        {
            if(!Fits(f))throw new InvalidDataException("Authored native sector exceeds LP/HP vertex, color, offset, or coordinate capacity.");
            Faces.Add(f);
            foreach(var p in f.Points)if(!Vertices.Contains(p))Vertices.Add(p);
            foreach(var c in f.Near.Zip(f.Far))if(!Colors.Contains(c))Colors.Add(c);
        }
    }
    internal static (byte[] Environment,byte[] Visibility) Compile(SceneDocument doc,IReadOnlyList<AuthoredMaterial> materials,IReadOnlyList<NativeAuthoredMesh.Triangle> direct)
    {
        List<Face> faces=[];
        foreach(var s in doc.Surfaces.Where(s=>s.Material!=null))
        {
            int material=Enumerable.Range(0,materials.Count).Single(i=>materials[i]==s.Material);
            if(material>127)throw new InvalidDataException("Authored materials exceed the native 7-bit textured descriptor index.");
            var near=s.NearColors??Enumerable.Repeat(s.Color,s.Vertices.Length).ToArray();
            var far=s.FarColors??Enumerable.Repeat(s.Color,s.Vertices.Length).ToArray();
            foreach(var p in s.Vertices){_=Unit(p.X);_=Unit(p.Y);_=Unit(p.Z);}
            if(s.Vertices.Length==4&&CompatibleQuad(s.Vertices))faces.Add(new(s.Vertices,near,far,material));
            else
            {
                faces.Add(new(s.Vertices[..3],near[..3],far[..3],material));
                if(s.Vertices.Length==4)faces.Add(new([s.Vertices[0],s.Vertices[2],s.Vertices[3]],[near[0],near[2],near[3]],[far[0],far[2],far[3]],material));
            }
        }
        foreach(var t in direct)faces.Add(new([t.A,t.B,t.C],t.Near,t.Far,255));
        float ox=faces.Min(f=>f.Points.Min(p=>p.X)),oy=faces.Min(f=>f.Points.Min(p=>p.Y)),oz=faces.Min(f=>f.Points.Min(p=>p.Z));
        List<Batch> batches=[];
        // Spatial buckets prevent a long authored row from producing a chain of
        // mutually incompatible sector origins. Membership is deterministic.
        foreach(var bucket in faces.GroupBy(f=>((int)((f.Points.Min(p=>p.X)-ox)/1536),(int)((f.Points.Min(p=>p.Y)-oy)/1536),(int)((f.Points.Min(p=>p.Z)-oz)/768))))
        {
            Batch? batch=null;
            foreach(var f in bucket)
            {
                if(batch==null||!batch.Fits(f)){batch=new();batches.Add(batch);}
                batch.Add(f);
            }
        }
        int texturedFaces=faces.Count(f=>f.Material!=255),texturedSectors=batches.Count(b=>b.Faces.Any(f=>f.Material!=255));
        if(batches.Count>127||texturedFaces+texturedSectors+1>128)
            throw new InvalidDataException("Authored terrain exceeds native renderer queue capacity after batching: at most 127 visible sectors, and 127 textured faces plus their sector headers. Reduce geometry or material faces before export.");
        var sectors=new List<byte[]>();int cache=1;
        foreach(var batch in batches){sectors.Add(Encode(batch,cache));cache+=batch.Faces.Count;}
        int count=sectors.Count,total=8+count*4+sectors.Sum(s=>s.Length);byte[] env=new byte[total];W(env,0,(uint)total);W(env,4,(uint)count);
        int cursor=8+count*4;
        for(int i=0;i<count;i++){W(env,8+i*4,(uint)(cursor-4));sectors[i].CopyTo(env,cursor);cursor+=sectors[i].Length;}
        int list=(count+4)&~3,sky=16+list;byte[] visibility=new byte[sky+12];W(visibility,0,(uint)visibility.Length);W(visibility,4,(uint)(sky-4));W(visibility,8,1);W(visibility,12,12);
        visibility.AsSpan(16,list).Fill(255);for(int i=0;i<count;i++)visibility[16+i]=(byte)i;
        W(visibility,sky,1);W(visibility,sky+4,8);visibility.AsSpan(sky+8,4).Fill(255);
        return(env,visibility);
    }
    private static bool CompatibleQuad(Vector3f[] p)
    {
        double[] a=[p[1].X-p[0].X,p[1].Y-p[0].Y,p[1].Z-p[0].Z],b=[p[2].X-p[0].X,p[2].Y-p[0].Y,p[2].Z-p[0].Z],c=[p[3].X-p[0].X,p[3].Y-p[0].Y,p[3].Z-p[0].Z];
        double[] n=[a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]];
        if(n.Zip(c).Sum(x=>x.First*x.Second)!=0||n.All(x=>x==0))return false;
        int drop=Enumerable.Range(0,3).MaxBy(i=>Math.Abs(n[i]));int[] axes=Enumerable.Range(0,3).Where(i=>i!=drop).ToArray();
        double Coordinate(Vector3f v,int axis)=>axis==0?v.X:axis==1?v.Y:v.Z;
        int sign=0;
        for(int i=0;i<4;i++)
        {
            var x=p[i];var y=p[(i+1)%4];var z=p[(i+2)%4];
            double cross=(Coordinate(y,axes[0])-Coordinate(x,axes[0]))*(Coordinate(z,axes[1])-Coordinate(y,axes[1]))-(Coordinate(y,axes[1])-Coordinate(x,axes[1]))*(Coordinate(z,axes[0])-Coordinate(y,axes[0]));
            if(cross==0||sign!=0&&Math.Sign(cross)!=sign)return false;sign=Math.Sign(cross);
        }
        return true;
    }
    private static byte[] Encode(Batch batch,int cache)
    {
        int v=batch.Vertices.Count,c=batch.Colors.Count,f=batch.Faces.Count,lpv=28,lpc=lpv+4*v,lpf=lpc+4*c,hpv=lpf+8*f,hpfar=hpv+4*v,hpnear=hpfar+4*c,hpf=hpnear+4*c,end=hpf+16*f;
        byte[] s=new byte[end+4];int x=(int)batch.Vertices.Min(p=>p.X),y=(int)batch.Vertices.Min(p=>p.Y),z=(int)batch.Vertices.Min(p=>p.Z);
        int mx=(int)batch.Vertices.Max(p=>p.X),my=(int)batch.Vertices.Max(p=>p.Y),mz=(int)batch.Vertices.Max(p=>p.Z);
        W(s,0,(uint)(((x+mx)/2<<16)|((y+my)/2)));W(s,4,(uint)(((z+mz)/2<<16)|(mx-x+my-y+mz-z)));W(s,8,(uint)(x<<16|y));W(s,12,(uint)(z<<16|cache));
        s[16]=s[20]=(byte)v;s[17]=s[21]=(byte)c;s[18]=s[22]=(byte)f;W(s,24,uint.MaxValue);
        for(int i=0;i<v;i++){var p=batch.Vertices[i];uint packed=(uint)(((int)p.X-x)<<21|((int)p.Y-y)<<10|((int)p.Z-z));W(s,lpv+4*i,packed);W(s,hpv+4*i,packed);}
        for(int i=0;i<c;i++){W(s,lpc+4*i,Rgb(batch.Colors[i].Far));W(s,hpfar+4*i,Rgb(batch.Colors[i].Far));W(s,hpnear+4*i,Rgb(batch.Colors[i].Near));}
        for(int i=0;i<f;i++)
        {
            var face=batch.Faces[i];int[] vertex=face.Points.Select(p=>batch.Vertices.IndexOf(p)).ToArray(),color=face.Near.Zip(face.Far).Select(p=>batch.Colors.IndexOf(p)).ToArray();
            int[] lp=face.Points.Length==3?[0,1,2,2]:[0,1,3,2],hp=face.Points.Length==3?[2,1,0,0]:[0,1,2,3];
            uint Low(int[] indexes)=>(uint)(indexes[lp[0]]<<26|indexes[lp[1]]<<20|indexes[lp[2]]<<14|indexes[lp[3]]<<8);
            uint High(int[] indexes)=>(uint)(indexes[hp[0]]<<24|indexes[hp[1]]<<16|indexes[hp[2]]<<8|indexes[hp[3]]);
            W(s,lpf+i*8,Low(vertex));W(s,lpf+i*8+4,Low(color));W(s,hpf+i*16,High(vertex));W(s,hpf+i*16+4,High(color));W(s,hpf+i*16+8,(uint)face.Material);W(s,hpf+i*16+12,face.Material==255?128u:0u);
        }
        Id65EnvironmentSectorHeaderContract.Stamp(s.AsSpan(0,end));_=Id65EnvironmentSectorHeaderContract.ReadPointerSpan(s,s.Length,false,"authored batch");return s;
    }
    private static int Unit(float v){if(v!=MathF.Truncate(v)||v is <0 or >16383)throw new InvalidDataException("Native authored vertices require whole editor units.");return(int)v;}
    private static uint Rgb(ColorRgba c)=>(uint)(c.R|c.G<<8|c.B<<16);
    private static void W(Span<byte>b,int at,uint v)=>BinaryPrimitives.WriteUInt32LittleEndian(b[at..],v);
}
