using System.Buffers.Binary;
using System.Reflection;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Primitives;

internal static class SubdivisionCases
{
    private static int Word(byte[] b,int o)=>BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(o));
    private static byte[] Compile(SceneDocument doc)
    {
        var type=typeof(NativeAuthoredSpawn).Assembly.GetType("Spyro.Editor.Core.Exporting.NativeAuthoredMesh")!;
        var materials=doc.Surfaces.Where(s=>s.Material!=null).Select(s=>s.Material!).Distinct().ToArray();
        return (byte[])type.GetMethod("Compile",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[doc,materials])!;
    }
    private static Vector3f[] Visual(byte[] mesh,int s,int start)
    {
        int x=Word(mesh,s+8)>>16,y=Word(mesh,s+8)&65535,z=Word(mesh,s+12)>>16;
        return Enumerable.Range(0,3).Select(i=>{uint w=(uint)Word(mesh,s+start+i*4);return new Vector3f(x+(w>>21),y+((w>>10)&2047),z+(w&1023));}).ToArray();
    }
    private static Vector3f[] Collision(byte[] mesh,int at)
    {
        uint x=(uint)Word(mesh,at),y=(uint)Word(mesh,at+4),z=(uint)Word(mesh,at+8);
        int Sign(uint v)=>(v&256)!=0?(int)v-512:(int)v;
        return [new(x&16383,y&16383,z&16383),new((x&16383)+Sign(x>>14&511),(y&16383)+Sign(y>>14&511),(z&16383)+(z>>16&255)),new((x&16383)+Sign(x>>23&511),(y&16383)+Sign(y>>23&511),(z&16383)+(z>>24))];
    }
    private static long Cross(Vector3f[] p)=>(long)(p[1].X-p[0].X)*(long)(p[2].Y-p[0].Y)-(long)(p[1].Y-p[0].Y)*(long)(p[2].X-p[0].X);
    internal static void Run(Action<bool,string> check)
    {
        var seed=SceneDocument.New();
        AuthoredSurface Quad(int side)=>new(Guid.NewGuid().ToString("N"),[new(520,520,544),new(520+side,520,544),new(520+side,520+side,544),new(520,520+side,544)],new(64,160,200),null);
        foreach(var(side,n) in new[]{(32,2),(64,2),(128,8),(200,32),(255,50),(448,98)})
        {
            var doc=seed with{Surfaces=[Quad(side)]};string hash=doc.Fingerprint();byte[] mesh=Compile(doc);
            check(Word(mesh,4)==n,"exact native lattice triangle count side"+side);
            check(doc.Surfaces.Count==1&&doc.Fingerprint()==hash,"editor face identity/count/document preserved side"+side);
            int collision=0;for(int part=0;part<3;part++)collision+=Word(mesh,collision);
            check(Word(mesh,collision+4)==n,"generated render and collision counts agree side"+side);
            int rows=collision+4+Word(mesh,collision+20);long total=0;
            Dictionary<(Vector3f A,Vector3f B),int> edges=[];
            void Edge(Vector3f a,Vector3f b){bool forward=a.X<b.X||a.X==b.X&&(a.Y<b.Y||a.Y==b.Y&&a.Z<b.Z);var key=forward?(a,b):(b,a);edges[key]=edges.GetValueOrDefault(key)+(forward?1:-1);}
            for(int i=0;i<n;i++)
            {
                int s=4+Word(mesh,8+4*i);var hp=Visual(mesh,s,60);var lp=Visual(mesh,s,28);var col=Collision(mesh,rows+i*12);
                check(hp.SequenceEqual(lp)&&hp.ToHashSet().SetEquals(col),"independent native LP/HP/collision lattice XYZ side"+side+"/"+i);
                check(hp.All(p=>p.X==MathF.Truncate(p.X)&&p.Y==MathF.Truncate(p.Y)&&p.Z==544)&&hp.Max(p=>p.X)-hp.Min(p=>p.X)<=64&&hp.Max(p=>p.Y)-hp.Min(p=>p.Y)<=64,"bounded exact native integer extent side"+side+"/"+i);
                check(Cross(hp)>0&&Cross(col)<0,"source visual orientation and native upward collision winding side"+side+"/"+i);total+=Cross(hp);
                for(int v=0;v<3;v++)Edge(hp[v],hp[(v+1)%3]);
                check((Word(mesh,s+12)&65535)==i+1&&Word(mesh,s+104)==255&&Word(mesh,s+108)==128,"generated scratch ownership and untextured packet binding side"+side+"/"+i);
            }
            check(total==2L*side*side,"generated triangle oriented area equals original quad side"+side);
            check(edges.Where(e=>e.Value!=0).All(e=>Math.Abs(e.Value)==1&&(e.Key.A.X==e.Key.B.X&&(e.Key.A.X==520||e.Key.A.X==520+side)||e.Key.A.Y==e.Key.B.Y&&(e.Key.A.Y==520||e.Key.A.Y==520+side))),"all internal edges cancel and surviving edges are original boundary side"+side);
        }
        // Linear corner fields can be checked directly at each emitted integer
        // point, independently of the compiler's interpolation weights.
        var gradient=Quad(200) with {NearColors=[new(0,0,44),new(200,0,44),new(200,200,44),new(0,200,44)],FarColors=[new(255,255,211),new(55,255,211),new(55,55,211),new(255,55,211)]};
        byte[] shaded=Compile(seed with{Surfaces=[gradient]});
        for(int i=0;i<Word(shaded,4);i++)
        {
            int s=4+Word(shaded,8+4*i);var points=Visual(shaded,s,60);
            for(int v=0;v<3;v++)
            {
                uint expectedNear=(uint)((int)(points[v].X-520)|((int)(points[v].Y-520)<<8)|44<<16),expectedFar=(uint)((int)(775-points[v].X)|((int)(775-points[v].Y)<<8)|211<<16);
                check((uint)Word(shaded,s+84+4*v)==expectedNear&&(uint)Word(shaded,s+72+4*v)==expectedFar&&(uint)Word(shaded,s+40+4*v)==expectedFar,"source-barycentric native near and both far tables "+i+"/"+v);
            }
        }
        var rounded=seed with {Surfaces=[new(Guid.NewGuid().ToString("N"),[new(520,520,544),new(775,520,544),new(520,775,544)],new(64,160,200),null,
            [new(11,55,88),new(244,0,149),new(1,3,67)],[new(251,15,31),new(2,149,219),new(127,33,99)])]};
        byte[] quantized=Compile(rounded);
        for(int i=0;i<Word(quantized,4);i++)
        {
            int s=4+Word(quantized,8+4*i);var points=Visual(quantized,s,60);
            for(int v=0;v<3;v++)
            {
                int bi=(int)(points[v].X-520)/51,ci=(int)(points[v].Y-520)/51,ai=5-bi-ci;
                bool exact=true;
                foreach(var(colors,offset) in new[]{(rounded.Surfaces[0].NearColors!,84),(rounded.Surfaces[0].FarColors!,72),(rounded.Surfaces[0].FarColors!,40)})
                {
                    uint rgb=(uint)Word(quantized,s+offset+4*v);
                    for(int channel=0;channel<3;channel++)
                    {
                        int Get(ColorRgba c)=>channel==0?c.R:channel==1?c.G:c.B;
                        int numerator=ai*Get(colors[0])+bi*Get(colors[1])+ci*Get(colors[2]);
                        exact&=Math.Abs((int)(rgb>>(8*channel)&255)*5-numerator)<=2;
                    }
                }
                check(exact,"source barycentric fractional color rounds only at final native byte "+i+"/"+v);
            }
        }
        var ramp=seed with{Surfaces=[new(Guid.NewGuid().ToString("N"),[new(520,520,560),new(720,520,544),new(520,720,560)],new(64,160,200),null)]};
        byte[] sloped=Compile(ramp);int co=0;for(int p=0;p<3;p++)co+=Word(sloped,co);int tr=co+4+Word(sloped,co+20);
        for(int i=0;i<Word(sloped,4);i++)
        {
            int s=4+Word(sloped,8+4*i);var hp=Visual(sloped,s,60);var col=Collision(sloped,tr+12*i);
            check(hp.ToHashSet().SetEquals(col)&&col[0].Z==col.Min(p=>p.Z)&&Cross(col)<0,"source-native unsigned Z16/24 packing and cyclic lowest origin "+i);
        }
        var firstCol=Collision(sloped,tr);uint oldZ=(uint)firstCol[0].Z|((uint)(firstCol[1].Z-firstCol[0].Z)<<14)|((uint)(firstCol[2].Z-firstCol[0].Z)<<23);
        check((oldZ&16383)+(oldZ>>16&255)!=firstCol[1].Z||(oldZ&16383)+(oldZ>>24)!=firstCol[2].Z,"former XY-packed Z negative control fails native slope decoder");
        bool sampled=false;
        for(int i=0;i<Word(sloped,4);i++)
        {
            var p=Collision(sloped,tr+i*12);double denominator=(p[1].Y-p[2].Y)*(p[0].X-p[2].X)+(p[2].X-p[1].X)*(p[0].Y-p[2].Y);
            double a=((p[1].Y-p[2].Y)*(560-p[2].X)+(p[2].X-p[1].X)*(560-p[2].Y))/denominator;
            double b=((p[2].Y-p[0].Y)*(560-p[2].X)+(p[0].X-p[2].X)*(560-p[2].Y))/denominator;
            double c=1-a-b;if(a>=0&&b>=0&&c>=0)sampled|=Math.Abs(a*p[0].Z+b*p[1].Z+c*p[2].Z-556.8)<0.0001;
        }
        check(sampled,"independently decoded native collision plane queries source slope at560/560");
        var textured=seed with{Surfaces=[Quad(200) with{Material=new(10,0)}]};byte[] tex=Compile(textured);
        check(Word(tex,4)==1,"planar textured quad emits one batched native sector");
        int ts=4+Word(tex,8),tf=ts+28+4*tex[ts+23]+4*tex[ts+20]+8*tex[ts+21];
        check(tex[ts+22]==1&&Word(tex,tf+8)==0&&Word(tex,tf+12)==0,"genuine textured quad descriptor enables native near clipping");
        // Colour tables of a textured sector: far tier (LP), first near table (up close, subdivided or whole), second (at
        // the near distance). A face whose far colours were never chosen keeps its near colours in both near tables; only
        // the untextured far tier takes the texture's average times them. Chosen far colours fill the first and LP.
        (uint[] Lp,uint[] First,uint[] Second) Tables(byte[] mesh)
        {
            int s=4+Word(mesh,8),lc=mesh[s+17],c=mesh[s+21],first=s+28+4*mesh[s+23]+4*mesh[s+20];
            uint[] Read(int at,int n)=>[..Enumerable.Range(0,n).Select(i=>(uint)Word(mesh,at+4*i))];
            return (Read(s+28+4*mesh[s+16],lc),Read(first,c),Read(first+4*c,c));
        }
        // Built levels store far colours equal to the near ones.
        ColorRgba[] shade=[..Enumerable.Repeat(new ColorRgba(100,120,140),4)];
        var plain=seed with{Surfaces=[Quad(200) with{Material=new(10,0),NearColors=shade,FarColors=shade}]};
        var derived=NativeAuthoredFarColors.Derive(plain,_=>new ColorRgba(64,128,255));
        check(derived.Surfaces[0].FarColors==shade&&derived.Surfaces[0].DistantColors!.All(c=>c==new ColorRgba(50,120,255)),"unchosen far colours: distant = texture average x near, far colours kept");
        var (farTier,nearFirst,nearSecond)=Tables(Compile(derived));
        check(farTier.SequenceEqual([0xFF7832u])&&nearFirst.SequenceEqual([0x8C7864u])&&nearSecond.SequenceEqual([0x8C7864u]),"derived distant colours fill only the far tier, both near tables keep the near colours");
        var chosen=seed with{Surfaces=[plain.Surfaces[0] with{FarColors=[..Enumerable.Repeat(new ColorRgba(30,40,50),4)]}]};
        check(NativeAuthoredFarColors.Derive(chosen,_=>new ColorRgba(64,128,255)).Surfaces[0].DistantColors==null,"chosen far colours get no derived distant colours");
        (farTier,nearFirst,nearSecond)=Tables(Compile(chosen));
        check(farTier.SequenceEqual([0x32281Eu])&&nearFirst.SequenceEqual([0x32281Eu])&&nearSecond.SequenceEqual([0x8C7864u]),"chosen far colours fill the far tier and the first near table");
        void Refuse(SceneDocument doc,string name)
        {
            string before=doc.Fingerprint();bool refused=false;try{_=Compile(doc);}catch(TargetInvocationException e)when(e.InnerException is InvalidDataException){refused=true;}
            check(refused&&doc.Fingerprint()==before,name+" refuses atomically");
        }
        Refuse(seed with{Surfaces=[Quad(1024)]},"generated per-face visibility budget overflow");
        // 230 copies of the starter triangle need 230 x 36 = 8,280 lattice triangles: over the level's 8,192
        // collision triangles (and 5,000 drawn faces).
        Refuse(seed with{Surfaces=Enumerable.Range(0,230).Select(_=>seed.Surfaces[0] with{Id=Guid.NewGuid().ToString("N")}).ToArray()},"combined generated collision budget overflow");
        Refuse(seed with{Surfaces=[new(Guid.NewGuid().ToString("N"),[new(520,520,544),new(651,521,544),new(520,584,544)],new(64,160,200),null)]},"long indivisible exact native lattice");
        Refuse(seed with{Surfaces=[Quad(64) with{Vertices=Quad(64).Vertices.Select(p=>p with{X=p.X+0.25f}).ToArray()}]},"fractional native vertices");
    }
}
