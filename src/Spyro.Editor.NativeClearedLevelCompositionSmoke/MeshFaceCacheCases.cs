using System.Buffers.Binary;
using System.Reflection;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Universal;

internal static class MeshFaceCacheCases
{
    private static uint Word(byte[] b,int o)=>BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(o));
    private static byte[] Compile(SceneDocument doc)
    {
        var type=typeof(NativeAuthoredSpawn).Assembly.GetType("Spyro.Editor.Core.Exporting.NativeAuthoredMesh")!;
        return (byte[])type.GetMethod("Compile",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[doc,Array.Empty<AuthoredMaterial>()])!;
    }
    internal static void Run(Action<bool,string> check)
    {
        var seed=SceneDocument.New();
        // Scratch ownership needs one bounded native triangle per editor face,
        // independently of the product's starter platform dimensions.
        seed=seed with {Surfaces=[seed.Surfaces[0] with {Vertices=[new(528,528,544),new(592,528,544),new(528,592,544)]}]};
        foreach(int count in new[]{2,32,255})
        {
            var doc=seed with {Surfaces=Enumerable.Range(0,count).Select(i=>seed.Surfaces[0] with {Id=Guid.NewGuid().ToString("N")}).ToArray()};
            byte[] mesh=Compile(doc);int n=(int)Word(mesh,4);
            check(n==count,"scratch fixture emits each HP triangle");
            var starts=Enumerable.Range(0,n).Select(i=>(int)(Word(mesh,4+(int)Word(mesh,8+4*i)+12)&65535)).ToArray();
            check(starts.SequenceEqual(Enumerable.Range(1,count)),"HP face scratch uses distinct native one-based ranges "+count);
            check(Enumerable.Range(0,n).All(i=>mesh[4+(int)Word(mesh,8+4*i)+22]==1),"scratch range length equals emitted HP face count");
            // Replay the native byte-address calculation, increment-in-delay-slot,
            // and sb -1(s6). Mixed clipping classifications must not overwrite peers.
            byte[] scratch=new byte[count+2];scratch[0]=0xA5;
            for(int i=0;i<count;i++)scratch[starts[i]]=(byte)(1+i%4);
            check(scratch[0]==0xA5&&Enumerable.Range(0,count).All(i=>scratch[starts[i]]==1+i%4),"native face classifications preserve reserved byte and every peer");
            var old=starts.Select(_=>0).ToArray();Array.Fill(scratch,(byte)0xA5);
            for(int i=0;i<count;i++)scratch[old[i]]=(byte)(1+i%4);
            check(scratch[0]!=0xA5&&scratch[1]==0xA5,"old zero-offset negative control corrupts sentinel and aliases every face");
            byte[] before=Compile(doc);var moved=doc with {Surfaces=doc.Surfaces.Select(s=>s with {Vertices=s.Vertices.Select(v=>v with {Z=v.Z+8}).ToArray()}).ToArray()};byte[] after=Compile(moved);
            check(Enumerable.Range(0,n).All(i=>{int at=4+(int)Word(before,8+4*i);return (Word(before,at+12)&65535)==(Word(after,at+12)&65535)&&Word(after,at+12)>>16==552;}),"scratch ownership retains independent Z-origin high halfword");
        }
    }
    internal static void RunSource(UniversalSourceLibrary donors,LevelCatalog catalog,Action<bool,string> check)
    {
        foreach(var (pc,hex) in new[]{(0x80026808u,"0780163CF4FCD6260C00228F"),(0x80026830u,"FFFF423020B0C202"),(0x80027168u,"1300C01E0100D622"),(0x80027700u,"FFFF6330"),(0x80027734u,"0019030022B0C302"),(0x8004B228u,"021E12000234120040006B8EFF00C630"),(0x80027260u,"8000AB312A00601D"),(0x80027554u,"030E010001002220320040107F002130")})
            check(donors.Disc.Exe.AsSpan(checked((int)(pc-0x8000F800)),hex.Length/2).SequenceEqual(Convert.FromHexString(hex)),"exact native scratch writer/subdivision consumer owner "+pc.ToString("X"));
        int sectors=0;
        foreach(var level in catalog.Levels)
        {
            var body=donors.Get(level.Key).Data;var sub=body.Subfiles()[1];byte[] bytes=body.Bytes;int env=sub.Offset+(int)Word(bytes,sub.Offset);int n=(int)Word(bytes,env+4);List<(int Start,int Count)> ranges=[];
            for(int i=0;i<n;i++)
            {
                int at=env+4+(int)Word(bytes,env+8+4*i),start=(int)(Word(bytes,at+12)&65535),count=bytes[at+22];sectors++;
                if(count==0)check(start==65535,"retail no-HP cache sentinel "+level.Key+"/"+i);else ranges.Add((start,count));
            }
            int cursor=1;bool exact=true;
            foreach(var r in ranges.OrderBy(r=>r.Start)){exact&=r.Start==cursor;cursor=r.Start+r.Count;}
            check(exact&&cursor<=0x1C00,"retail HP cache ranges tile from one without aliasing "+level.Key);
        }
        check(catalog.Levels.Count==35&&sectors>5000,"complete 35-level native scratch ownership census");
    }
}
