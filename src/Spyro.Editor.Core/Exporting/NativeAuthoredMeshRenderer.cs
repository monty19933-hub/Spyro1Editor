using System.Buffers.Binary;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.Exporting;

/// <summary>Native LP/HP batches. The per-frame subdivision queues are proved for the whole level when every
/// textured face and its sector fit at once, and otherwise checked by simulating the game's own tests over camera
/// poses. Collision remains exact.</summary>
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
        // Small levels keep the original packing (spatial buckets filled in authoring order, the same bytes as
        // before): when every textured face and one header per textured sector fit a subdivision queue at once, no
        // camera can overflow it. Larger levels get compact sectors like retail ones (a few hundred units across)
        // with their true radius, because the game culls whole sectors against the view and a sprawling sector keeps
        // far-off faces in the per-frame queues: faces are split at the median along the longest axis until each
        // part fits one sector and is at most SectorSpan across (the span grows when that needs more sectors than
        // the game holds), and the queues are then checked by simulation. Membership is deterministic.
        List<Batch> batches=Bucketed(faces);
        bool proved=batches.Count<=MaxSectors&&TexturedLoad(batches)<=SubdivisionQueueCapacity;
        if(!proved)
            for(float span=SectorSpan;;span*=1.25f)
            {
                batches=[];Split(faces,span,batches);
                if(batches.Count<=MaxSectors||span>16384)break;
            }
        if(batches.Count>MaxSectors)
            throw new InvalidDataException($"Authored terrain needs {batches.Count} sectors after batching; the game's sector queues hold {MaxSectors}. Reduce geometry.");
        int totalFaces=batches.Sum(b=>b.Faces.Count);
        if(totalFaces>MaxFaces)
            throw new InvalidDataException($"Authored terrain has {totalFaces} drawn faces; the game's per-face table holds {MaxFaces}. Reduce geometry.");
        RequireSubdivisionQueues(batches);
        var sectors=new List<byte[]>();int cache=1;
        foreach(var batch in batches){sectors.Add(Encode(batch,cache,!proved));cache+=batch.Faces.Count;}
        int count=sectors.Count,total=8+count*4+sectors.Sum(s=>s.Length);byte[] env=new byte[total];W(env,0,(uint)total);W(env,4,(uint)count);
        int cursor=8+count*4;
        for(int i=0;i<count;i++){W(env,8+i*4,(uint)(cursor-4));sectors[i].CopyTo(env,cursor);cursor+=sectors[i].Length;}
        int list=(count+4)&~3,sky=16+list;byte[] visibility=new byte[sky+12];W(visibility,0,(uint)visibility.Length);W(visibility,4,(uint)(sky-4));W(visibility,8,1);W(visibility,12,12);
        visibility.AsSpan(16,list).Fill(255);for(int i=0;i<count;i++)visibility[16+i]=(byte)i;
        W(visibility,sky,1);W(visibility,sky+4,8);visibility.AsSpan(sky+8,4).Fill(255);
        return(env,visibility);
    }
    // Per-frame native queues (USA sector pass 0x80025A30.., face pass 0x80026C80.. quads, 0x80027244.. triangles):
    // - a sector is near when its header sphere passes the view-frustum test (culled when d+r <= 0, when
    //   4(|x|-0.78r) >= 3(d+0.59r) or when 32(|y|-0.875r) >= 17(d+0.44r); d depth, x/y across, in units) and d-r is
    //   under the near-detail distance (2048 units); with at most 127 sectors neither sector list can overflow;
    // - a front-facing textured face of a near sector (the GTE NCLIP test runs first) whose SZ sum (quad: four
    //   corners; triangle: a+b+2c; 4 SZ per unit) is above 0 and under 0x2000 is queued for subdivision, with one
    //   header per sector and queue: the close queue (+0x1E00, 127 entries before the normal queue) when a corner's
    //   SZ is under 0x140, otherwise the normal queue (+0x2000); untextured faces carry the no-subdivision flag;
    // - one cache byte per face from 0x8006FCF4 + sector cache index, below the near list at +0x1C00.
    // The queues are checked by running those tests for camera poses over the whole level volume: a 128-unit grid
    // from below the lowest face to 768 above the highest, 24 headings and pitches from 60 degrees down to 30 up.
    // Faces with a corner near or behind the camera count whichever way they face, triangles take the smaller
    // weighting, and the two queues together must keep 16 of one queue's entries spare (a margin for poses
    // between grid points). Measured in the emulator (CrashLevelSmoke's village, 128 poses), the real queues
    // never exceeded this model's total.
    // A queue holds 127 entries before its terminator; when every textured face of the level and one header per
    // textured sector fit that, no camera can overflow it (exact, no margin needed).
    internal const int MaxSectors=127, MaxFaces=5000, SubdivisionQueueCapacity=127, SubdivisionQueueEntries=126, SubdivisionMargin=16;
    private const float NearDistance=2048, PoseSpacing=128;

    /// <summary>The last compile's sector count and heaviest simulated subdivision-queue load (for smoke tests).</summary>
    internal static (int Sectors,int QueueLoad,string Where) LastReport{get;private set;}
    internal static IReadOnlyList<QueueSector> LastQueueSectors{get;private set;}=[];
    /// <summary>The last compile's heaviest load per grid column (x, y from the grid origin in PoseSpacing steps).</summary>
    internal static (float X,float Y,int[,] Load) LastLoadMap{get;private set;}
    internal sealed record QueueSector(float X,float Y,float Z,float R,float[] Corners,int[] Counts,float[] Normals);
    private static int TexturedLoad(List<Batch> batches)=>
        batches.Sum(b=>b.Faces.Count(f=>f.Material!=255))+batches.Count(b=>b.Faces.Any(f=>f.Material!=255));
    private static List<Batch> Bucketed(List<Face> faces)
    {
        // Spatial buckets prevent a long authored row from producing a chain of mutually incompatible sector origins.
        float ox=faces.Min(f=>f.Points.Min(p=>p.X)),oy=faces.Min(f=>f.Points.Min(p=>p.Y)),oz=faces.Min(f=>f.Points.Min(p=>p.Z));
        List<Batch> batches=[];
        foreach(var bucket in faces.GroupBy(f=>((int)((f.Points.Min(p=>p.X)-ox)/1536),(int)((f.Points.Min(p=>p.Y)-oy)/1536),(int)((f.Points.Min(p=>p.Z)-oz)/768))))
        {
            Batch? batch=null;
            foreach(var f in bucket)
            {
                if(batch==null||!batch.Fits(f)){batch=new();batches.Add(batch);}
                batch.Add(f);
            }
        }
        return batches;
    }
    private static void RequireSubdivisionQueues(List<Batch> batches)
    {
        int load=TexturedLoad(batches);
        LastReport=(batches.Count,load,"everywhere");LastQueueSectors=[];LastLoadMap=default;
        if(load<=SubdivisionQueueCapacity)return;
        var sectors=batches.Where(b=>b.Faces.Any(f=>f.Material!=255)).Select(b=>
        {
            int x=(int)b.Vertices.Min(p=>p.X),y=(int)b.Vertices.Min(p=>p.Y),z=(int)b.Vertices.Min(p=>p.Z),mx=(int)b.Vertices.Max(p=>p.X),my=(int)b.Vertices.Max(p=>p.Y),mz=(int)b.Vertices.Max(p=>p.Z);
            var faces=b.Faces.Where(f=>f.Material!=255).ToArray();
            return new QueueSector((x+mx)/2,(y+my)/2,(z+mz)/2,Radius(b.Vertices,(x+mx)/2,(y+my)/2,(z+mz)/2),
                faces.SelectMany(f=>f.Points.SelectMany(p=>new[]{p.X,p.Y,p.Z})).ToArray(),faces.Select(f=>f.Points.Length).ToArray(),
                faces.SelectMany(f=>{var a=f.Points[0];var u=f.Points[1];var v=f.Points[2];
                    float ux=u.X-a.X,uy=u.Y-a.Y,uz=u.Z-a.Z,vx=v.X-a.X,vy=v.Y-a.Y,vz=v.Z-a.Z;
                    return new[]{uy*vz-uz*vy,uz*vx-ux*vz,ux*vy-uy*vx};}).ToArray());
        }).ToArray();
        LastQueueSectors=sectors;
        float x0=sectors.Min(s=>s.X-s.R),y0=sectors.Min(s=>s.Y-s.R),x1=sectors.Max(s=>s.X+s.R),y1=sectors.Max(s=>s.Y+s.R);
        float z0=batches.Min(b=>b.Vertices.Min(p=>p.Z))-128,z1=batches.Max(b=>b.Vertices.Max(p=>p.Z))+768;
        const int Headings=24;float[] pitches=[-60,-45,-30,-18,-8,0,10,30];
        var bases=new List<(float[] F,float[] R,float[] U)>();
        for(int h=0;h<Headings;h++)foreach(float pitch in pitches)
        {
            double a=h*2*Math.PI/Headings,b=pitch*Math.PI/180;
            float[] f=[(float)(Math.Cos(b)*Math.Cos(a)),(float)(Math.Cos(b)*Math.Sin(a)),(float)Math.Sin(b)];
            float[] r=[(float)Math.Sin(a),(float)-Math.Cos(a),0];
            float[] u=[r[1]*f[2]-r[2]*f[1],r[2]*f[0]-r[0]*f[2],r[0]*f[1]-r[1]*f[0]];
            bases.Add((f,r,u));
        }
        int nx=(int)((x1-x0)/PoseSpacing)+1,ny=(int)((y1-y0)/PoseSpacing)+1,nz=(int)((z1-z0)/PoseSpacing)+1;
        int worst=0;string? where=null;object gate=new();var map=new int[nx,ny];
        Parallel.For(0,nx,i=>
        {
            float px=x0+i*PoseSpacing;var reach=new List<QueueSector>();
            for(int j=0;j<ny;j++)for(int k=0;k<nz;k++)
            {
                float py=y0+j*PoseSpacing,pz=z0+k*PoseSpacing;
                // A sector can only be queued from here if it can be both in the frustum and under 512 deep.
                reach.Clear();
                foreach(var s in sectors){float dx=s.X-px,dy=s.Y-py,dz=s.Z-pz,lim=2.3f*(512+s.R)+2.4f*s.R;if(dx*dx+dy*dy+dz*dz<lim*lim)reach.Add(s);}
                if(reach.Count==0)continue;
                foreach(var (f,r,u) in bases)
                {
                    int normal=0,close=0;
                    foreach(var s in reach)
                    {
                        float dx=s.X-px,dy=s.Y-py,dz=s.Z-pz,d=dx*f[0]+dy*f[1]+dz*f[2],x=Math.Abs(dx*r[0]+dy*r[1]+dz*r[2]),y=Math.Abs(dx*u[0]+dy*u[1]+dz*u[2]),R=s.R;
                        if(d+R<=0||4*(x-0.78125f*R)>=3*(d+0.59375f*R)||32*(y-0.875f*R)>=17*(d+0.4375f*R)||d-R>=NearDistance||d-R>=512)continue;
                        int sn=0,sc=0,at=0;
                        for(int n=0;n<s.Counts.Length;n++)
                        {
                            int corners=s.Counts[n];float sum=0,min=float.MaxValue;bool near=false;
                            for(int c=0;c<corners;c++)
                            {
                                int o=(at+c)*3;float sz=Math.Max(0,4*((s.Corners[o]-px)*f[0]+(s.Corners[o+1]-py)*f[1]+(s.Corners[o+2]-pz)*f[2]));
                                sum+=sz;min=Math.Min(min,sz);if(sz<0x140)near=true;
                            }
                            if(corners==3)sum+=min;
                            int a3=at*3;at+=corners;
                            if(sum<=0||sum>=0x2000)continue;
                            if(!near&&(px-s.Corners[a3])*s.Normals[n*3]+(py-s.Corners[a3+1])*s.Normals[n*3+1]+(pz-s.Corners[a3+2])*s.Normals[n*3+2]<=0)continue;
                            if(near)sc++;else sn++;
                        }
                        if(sn>0)normal+=sn+1;if(sc>0)close+=sc+1;
                    }
                    int load=normal+close;
                    if(load>map[i,j])map[i,j]=load;
                    if(load>worst)lock(gate)if(load>worst){worst=load;where=$"({px:0}, {py:0}, {pz:0})";}
                }
            }
        });
        LastReport=(batches.Count,worst,where??"nowhere");LastLoadMap=(x0,y0,map);
        if(worst>SubdivisionQueueEntries-SubdivisionMargin)
            throw new InvalidDataException($"Too many textured tiles close together near {where}: a camera there can need {worst} of the game's {SubdivisionQueueEntries} close-up texture slots (keep {SubdivisionMargin} spare). Use larger tiles there, or untextured faces.");
    }

    internal const float SectorSpan=640;
    private static void Split(List<Face> faces,float span,List<Batch> output)
    {
        var points=faces.SelectMany(f=>f.Points).ToArray();
        float[] extent=[points.Max(p=>p.X)-points.Min(p=>p.X),points.Max(p=>p.Y)-points.Min(p=>p.Y),points.Max(p=>p.Z)-points.Min(p=>p.Z)];
        if(extent.Max()<=span||faces.Count==1)
        {
            Batch batch=new();
            if(faces.All(f=>{if(!batch.Fits(f))return false;batch.Add(f);return true;})){output.Add(batch);return;}
            if(faces.Count==1)throw new InvalidDataException("Authored native sector exceeds LP/HP vertex, color, offset, or coordinate capacity.");
        }
        int axis=extent[0]>=extent[1]&&extent[0]>=extent[2]?0:extent[1]>=extent[2]?1:2;
        float Centre(Face f)=>f.Points.Average(p=>axis==0?p.X:axis==1?p.Y:p.Z);
        var ordered=faces.Select((f,i)=>(f,i)).OrderBy(t=>Centre(t.f)).ThenBy(t=>t.i).Select(t=>t.f).ToList();
        int half=ordered.Count/2;
        Split(ordered[..half],span,output);Split(ordered[half..],span,output);
    }
    /// <summary>The header's bounding radius: the farthest corner from the header centre, rounded up (retail
    /// headers store this; the game culls and picks the detail tier with it).</summary>
    private static int Radius(List<Vector3f> vertices,int cx,int cy,int cz)
    {
        double r=vertices.Max(p=>Math.Sqrt((p.X-cx)*(p.X-cx)+(p.Y-cy)*(p.Y-cy)+(double)(p.Z-cz)*(p.Z-cz)));
        int radius=(int)Math.Ceiling(r)+1;
        if(radius>0x1FFF)throw new InvalidDataException("Authored native sector is too large for its header radius.");
        return radius;
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
    private static byte[] Encode(Batch batch,int cache,bool trueRadius)
    {
        int v=batch.Vertices.Count,c=batch.Colors.Count,f=batch.Faces.Count,lpv=28,lpc=lpv+4*v,lpf=lpc+4*c,hpv=lpf+8*f,hpfar=hpv+4*v,hpnear=hpfar+4*c,hpf=hpnear+4*c,end=hpf+16*f;
        byte[] s=new byte[end+4];int x=(int)batch.Vertices.Min(p=>p.X),y=(int)batch.Vertices.Min(p=>p.Y),z=(int)batch.Vertices.Min(p=>p.Z);
        int mx=(int)batch.Vertices.Max(p=>p.X),my=(int)batch.Vertices.Max(p=>p.Y),mz=(int)batch.Vertices.Max(p=>p.Z);
        W(s,0,(uint)(((x+mx)/2<<16)|((y+my)/2)));W(s,4,(uint)(((z+mz)/2<<16)|(trueRadius?Radius(batch.Vertices,(x+mx)/2,(y+my)/2,(z+mz)/2):mx-x+my-y+mz-z)));W(s,8,(uint)(x<<16|y));W(s,12,(uint)(z<<16|cache));
        s[16]=s[20]=(byte)v;s[17]=s[21]=(byte)c;s[18]=s[22]=(byte)f;W(s,24,uint.MaxValue);
        for(int i=0;i<v;i++){var p=batch.Vertices[i];uint packed=(uint)(((int)p.X-x)<<21|((int)p.Y-y)<<10|((int)p.Z-z));W(s,lpv+4*i,packed);W(s,hpv+4*i,packed);}
        for(int i=0;i<c;i++){W(s,lpc+4*i,Rgb(batch.Colors[i].Far));W(s,hpfar+4*i,Rgb(batch.Colors[i].Far));W(s,hpnear+4*i,Rgb(batch.Colors[i].Near));}
        for(int i=0;i<f;i++)
        {
            var face=batch.Faces[i];int[] vertex=face.Points.Select(p=>batch.Vertices.IndexOf(p)).ToArray(),color=face.Near.Zip(face.Far).Select(p=>batch.Colors.IndexOf(p)).ToArray();
            // HP quads test (0,1,3), then (2,1,3); LP stores the
            // corresponding GPU strip (0,1,3,2). Reverse the authored
            // perimeter, as the established HP triangle path does, so both
            // tiers accept the same side. Colors follow their vertex corners.
            int[] lp=face.Points.Length==3?[2,1,0,0]:[3,2,0,1],hp=face.Points.Length==3?[2,1,0,0]:[3,2,1,0];
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
