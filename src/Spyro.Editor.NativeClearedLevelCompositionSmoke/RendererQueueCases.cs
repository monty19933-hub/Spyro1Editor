using System.Reflection;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;

internal static class RendererQueueCases
{
    private const uint Hp=0x800718F4, Close=0x80071AF4, Normal=0x80071CF4;
    internal static void Run(Action<bool,string> check)
    {
        var seed=SceneDocument.New();
        var face=seed.Surfaces[0] with {Vertices=[new(1000,1000,522),new(1240,1000,522),new(1240,1240,522)],Material=new(10,66)};
        var untextured=face with {Vertices=[new(1000,1000,522),new(1064,1000,522),new(1000,1064,522)],Material=null};
        SceneDocument Document(int colored,int textured)=>seed with {Surfaces=[
            ..Enumerable.Range(0,colored).Select(_=>untextured with{Id=Guid.NewGuid().ToString("N")}),
            ..Enumerable.Range(0,textured).Select(_=>face with{Id=Guid.NewGuid().ToString("N")})]};
        bool Accepted(SceneDocument doc)
        {
            string before=doc.Fingerprint();
            var type=typeof(NativeAuthoredSpawn).Assembly.GetType("Spyro.Editor.Core.Exporting.NativeAuthoredMesh")!;
            bool accepted;
            try{_=type.GetMethod("Compile",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[doc,new[]{new AuthoredMaterial(10,66)}]);accepted=true;}
            catch(TargetInvocationException e)when(e.InnerException is InvalidDataException){accepted=false;}
            check(doc.Fingerprint()==before,"renderer admission preserves document");
            return accepted;
        }
        check(Accepted(Document(0,125)),"125 shared triangles fit one sector header and terminated HQ queue");
        check(!Accepted(Document(0,126)),"126 textured triangles refuse batched HQ terminator overflow");
        check(Accepted(Document(127,0)),"127 direct triangles fit terminated sector queue");
        check(Accepted(Document(128,0)),"128 direct triangles batch into bounded sector queues");
        check(Accepted(Document(64,63)),"mixed maximum respects both independent native queues");
        check(Accepted(Document(65,63)),"mixed scene safely batches shared direct and textured vertices");
        check(!Accepted(Document(0,242)),"failed 121 textured quads refuse before native export");
        check(Accepted(Document(0,50)),"bounded 25-quad runtime fixture admitted");

        // Replay the native queue producers, including the zero written at the
        // current cursor. This detects address ownership, not mesh count fields.
        Dictionary<uint,uint> ram=[];
        void SectorQueue(int count){uint cursor=Hp;for(int i=0;i<count;i++){ram[cursor]=0x8008C000u+(uint)i*116u|3u;cursor+=4;}ram[cursor]=0;}
        void CloseQueue(int faces){uint cursor=Close;for(int i=0;i<faces;i++){ram[cursor]=0x20023000u+(uint)i*29u;cursor+=4;ram[cursor]=0x8008C060u+(uint)i*116u|2u;cursor+=4;}ram[cursor]=0;}
        SectorQueue(127);ram[Close]=0xA5A5A5A5;
        check(ram[Hp+127*4]==0&&ram[Close]==0xA5A5A5A5,"HP terminator stays before close queue");
        ram.Clear();SectorQueue(128);check(ram[Close]==0,"128th HP terminator overwrites close queue negative control");
        ram.Clear();CloseQueue(63);ram[Normal]=0xA5A5A5A5;
        check(ram[Close+126*4]==0&&ram[Normal]==0xA5A5A5A5,"close-HQ terminator stays before normal queue");
        ram.Clear();CloseQueue(64);check(ram[Normal]==0,"64th close-HQ terminator overwrites normal queue negative control");
        ram.Clear();SectorQueue(242);CloseQueue(1);
        uint invalidSector=ram[Hp+128*4]&~3u;
        check(invalidSector==0x20023000&&invalidSector+8==0x20023008,
            "failed HP queue walks compact HQ sector header as invalid pointer");
    }
}
