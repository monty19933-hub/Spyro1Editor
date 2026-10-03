using System.Buffers.Binary;
using System.Reflection;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Primitives;

internal static class NativeBatchCases
{
    private static uint Word(byte[] b,int o)=>BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(o));
    private static byte[] Compile(SceneDocument doc)
    {
        var t=typeof(NativeAuthoredSpawn).Assembly.GetType("Spyro.Editor.Core.Exporting.NativeAuthoredMesh")!;
        return(byte[])t.GetMethod("Compile",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[doc,doc.Surfaces.Where(s=>s.Material!=null).Select(s=>s.Material!).Distinct().ToArray()])!;
    }
    internal static void Run(Action<bool,string> check)
    {
        ColorRgba Near(Vector3f p)=>new((byte)((int)p.X/16%256),(byte)((int)p.Y/16%256),44);
        ColorRgba Far(Vector3f p)=>new((byte)(255-Near(p).R),(byte)(255-Near(p).G),211);
        foreach(bool slope in new[]{false,true})
        {
            List<AuthoredSurface> faces=[];
            Vector3f P(int x,int y)=>new(x,y,522+(slope?(x-1000)/60:0));
            for(int x=1000;x<3640;x+=240)for(int y=1000;y<3640;y+=240)
            {
                Vector3f[] p=[P(x,y),P(x+240,y),P(x+240,y+240),P(x,y+240)];
                faces.Add(new(Guid.NewGuid().ToString("N"),p,new(128,112,80),new(10,66),p.Select(Near).ToArray(),p.Select(Far).ToArray()));
            }
            var doc=SceneDocument.New() with{Surfaces=faces};string before=doc.Fingerprint();byte[] mesh=Compile(doc);
            check(doc.Fingerprint()==before,"batching never changes authored face or corner identities");
            int sectors=(int)Word(mesh,4),hpFaces=0,texturedSectors=0,cache=1;
            var actual=new HashSet<string>();string Key(IEnumerable<Vector3f> p)=>string.Join(";",p.OrderBy(v=>v.X).ThenBy(v=>v.Y).ThenBy(v=>v.Z));
            for(int i=0;i<sectors;i++)
            {
                int s=4+(int)Word(mesh,8+4*i),v=mesh[s+16],c=mesh[s+17],f=mesh[s+18],h=mesh[s+23],lpc=s+28+4*v,lpf=lpc+4*c,hpv=s+28+4*h,hpfar=hpv+4*v,hpnear=hpfar+4*c,hpf=hpnear+4*c;
                check(v<=64&&v<=170&&c<=64&&h==v+c+2*f&&h<=255&&mesh[s+20]==v&&mesh[s+21]==c&&mesh[s+22]==f,"exact independent LP/HP table capacity and partition");
                check((Word(mesh,s+12)&65535)==cache,"batched HP cache ranges partition from one");cache+=f;
                int next=i+1<sectors?4+(int)Word(mesh,12+4*i):(int)Word(mesh,0);
                check(next==hpf+16*f+4&&Word(mesh,hpf+16*f)==0,"each batch retains exact native prefetch guard");
                Vector3f Decode(int at,int idx)
                {
                    uint w=Word(mesh,at+4*idx);return new((Word(mesh,s+8)>>16)+(w>>21),(Word(mesh,s+8)&65535)+((w>>10)&2047),(Word(mesh,s+12)>>16)+(w&1023));
                }
                for(int j=0;j<f;j++)
                {
                    uint w=Word(mesh,hpf+16*j),color=Word(mesh,hpf+16*j+4),low=Word(mesh,lpf+8*j),lc=Word(mesh,lpf+8*j+4);
                    int[] hv=[(int)(w>>24),(int)(w>>16&255),(int)(w>>8&255),(int)(w&255)];
                    int[] hc=[(int)(color>>24),(int)(color>>16&255),(int)(color>>8&255),(int)(color&255)];
                    int[] lv=[(int)(low>>26),(int)(low>>20&63),(int)(low>>14&63),(int)(low>>8&63)];
                    int[] li=[(int)(lc>>26),(int)(lc>>20&63),(int)(lc>>14&63),(int)(lc>>8&63)];
                    var native=hv.Select(n=>Decode(hpv,n)).ToArray();int[] packet=[0,1,3,2];
                    check(hv.All(n=>n<v)&&hc.All(n=>n<c)&&hv[2]!=hv[3]&&Word(mesh,hpf+16*j+8)==0&&Word(mesh,hpf+16*j+12)==0,"native quad indices and material/near-clip flags");
                    check(packet.Select(n=>native[n]).SequenceEqual(lv.Select(n=>Decode(s+28,n))),"LP and HP native packet strip corners agree");
                    double Area(int a,int b,int d)=>(native[b].X-native[a].X)*(native[d].Y-native[a].Y)-(native[b].Y-native[a].Y)*(native[d].X-native[a].X);
                    check(Area(0,1,3)<0&&Area(2,1,3)>0,"HP native NCLIP triples retain established upward triangle winding");
                    for(int k=0;k<4;k++)
                    {
                        uint Rgb(ColorRgba q)=>(uint)(q.R|q.G<<8|q.B<<16);
                        check(Word(mesh,hpnear+4*hc[k])==Rgb(Near(native[k]))&&Word(mesh,hpfar+4*hc[k])==Rgb(Far(native[k]))&&Word(mesh,lpc+4*li[k])==Rgb(Far(native[packet[k]])),"independent native color pools preserve every near/far corner");
                    }
                    actual.Add(Key(native));
                }
                hpFaces+=f;texturedSectors++;
            }
            check(sectors==4&&hpFaces==121&&hpFaces+texturedSectors+1==126,"121 tiled quads fit every all-visible HQ/HP queue after four spatial batches");
            check(actual.SetEquals(faces.Select(s=>Key(s.Vertices))),"every original authored quad represented once without duplicated or missing tiles");
            int col=0;for(int i=0;i<3;i++)col+=(int)Word(mesh,col);
            check(Word(mesh,col+4)==242&&Word(mesh,col+8)==242,"exact original two-triangle collision membership per authored quad");
            int rows=col+4+(int)Word(mesh,col+20);long area=0;
            for(int i=0;i<242;i++)
            {
                uint x=Word(mesh,rows+12*i),y=Word(mesh,rows+12*i+4),z=Word(mesh,rows+12*i+8);int Sign(uint n)=>(n&256)!=0?(int)n-512:(int)n;
                Vector3f[] p=[new(x&16383,y&16383,z&16383),new((x&16383)+Sign(x>>14&511),(y&16383)+Sign(y>>14&511),(z&16383)+(z>>16&255)),new((x&16383)+Sign(x>>23&511),(y&16383)+Sign(y>>23&511),(z&16383)+(z>>24))];
                long cross=(long)(p[1].X-p[0].X)*(long)(p[2].Y-p[0].Y)-(long)(p[1].Y-p[0].Y)*(long)(p[2].X-p[0].X);area-=cross;
                check(cross<0&&p.All(q=>q.Z==522+(slope?(q.X-1000)/60:0))&&faces.Any(s=>p.All(q=>s.Vertices.Contains(q))),"independent native collision retains tile vertices, upward winding, and source slope");
            }
            check(area==2L*2640*2640,"native collision area covers the complete authored floor");
        }
        var seed=SceneDocument.New();AuthoredSurface Quad(Vector3f[] p)=>new(Guid.NewGuid().ToString("N"),p,new(128,112,80),new(10,66));
        var direct=seed with{Surfaces=[Quad([new(1000,1000,522),new(1512,1000,522),new(1512,1512,522),new(1000,1512,522)]) with{Material=null}]};
        byte[] wide=Compile(direct);int wideSectors=(int)Word(wide,4),wideFaces=0;
        for(int i=0;i<wideSectors;i++)
        {
            int s=4+(int)Word(wide,8+4*i),f=wide[s+22],at=s+28+4*wide[s+23]+4*wide[s+20]+8*wide[s+21];wideFaces+=f;
            check(Enumerable.Range(0,f).All(j=>Word(wide,at+16*j+8)==255&&Word(wide,at+16*j+12)==128),"batched direct lattice retains FF/HQ-disable flags on every triangle");
        }
        check(wideFaces==128&&wideSectors<128,"wide direct terrain safely batches beyond former 127 one-face-sector limit");
        foreach(var p in new[]{new Vector3f[]{new(1000,1000,522),new(1240,1000,522),new(1240,1240,526),new(1000,1240,522)},new Vector3f[]{new(1000,1000,522),new(1240,1000,522),new(1100,1100,522),new(1000,1240,522)}})
        {
            byte[] m=Compile(seed with{Surfaces=[Quad(p)]});int s=4+(int)Word(m,8);
            check(m[s+22]==2,"nonplanar or concave quads retain original triangle rendering instead of changing the collision plane");
            int v=m[s+16],c=m[s+17],lp=s+28+4*v+4*c,hp=s+28+4*m[s+23]+4*v+8*c;
            for(int i=0;i<2;i++)
            {
                uint l=Word(m,lp+8*i),h=Word(m,hp+16*i);
                check(new[]{(int)(l>>26),(int)(l>>20&63),(int)(l>>14&63),(int)(l>>8&63)}.SequenceEqual(new[]{(int)(h>>24),(int)(h>>16&255),(int)(h>>8&255),(int)(h&255)}),"fallback triangles use identical LP/HP vertex winding");
            }
        }
    }
}
