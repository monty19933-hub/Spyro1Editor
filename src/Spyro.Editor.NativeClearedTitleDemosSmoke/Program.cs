using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Universal;

int checks=0,byteChecks=0;
void Check(bool condition,string message){checks++;if(!condition)throw new Exception(message);}
void Refuse(Action action,string message){try{action();}catch(InvalidDataException){checks++;return;}throw new Exception(message);}
uint Word(byte[] b,int at)=>BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(at));
void Put(byte[] b,int at,uint value)=>BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(at),value);
if(args.Length==0)
{
    Refuse(()=>NativeClearedTitleDemos.Prepare(new byte[0x66000],new byte[0x66000],new HashSet<int>()),"Synthetic source cannot issue demo replacement");
    Console.WriteLine($"NATIVE_CLEARED_TITLE_DEMOS_SMOKE_OK {checks} source-free checks.");return;
}
byte[] exe=UniversalDiscImage.Read(args[0]).Exe;
int[] levels=[11,24,55,33],frames=[860,1100,920,990];
for(int i=0;i<4;i++){Check(Word(exe,0x5F67C+i*4)==levels[i],"Exact sourcelevel");Check(Word(exe,0x5F68C+i*4)==frames[i],"Exact sourceframecount");}
// Independent source instruction witness: one index global, native slot*4
// metadata, slot*16 pose and modulo4 progression. Exact loaded field widths.
var witnesses=new Dictionary<int,uint>{[0x48A4]=0x8C6357B8,[0x48AC]=0x00031900,[0x48B8]=0x8C22EE9C,
    [0x48CC]=0x8C22EEA0,[0x48E0]=0x8C22EEA4,[0x48F4]=0x8C22EEA8,
    [0x233E4]=0x8C4257B8,[0x233EC]=0x00021080,[0x233F8]=0x8C22EE7C,
    [0x23D10]=0x8C4257B8,[0x23D18]=0x00021080,[0x23D24]=0x8C22EE8C,
    [0x23EE0]=0x8C4257B8,[0x23EEC]=0x00021080,[0x23EF8]=0x8C22EE8C,
    [0x24028]=0x8C6357B8,[0x24034]=0x24630001,[0x24038]=0x30630003,[0x24040]=0xAC2357B8};
foreach(var (offset,word) in witnesses)Check(Word(exe,offset)==word,"Source consumer witness");
// Complete original code census of direct loads/stores of gDemoIndex.
int[] indexReferences=[0x48A4,0x233E4,0x23D10,0x23EE0,0x24028,0x24040];
var found=new List<int>();
for(int at=0x800;at<0x5EF00;at+=4)
{
    uint w=Word(exe,at);
    if((w>>26 is 35 or 43)&&(ushort)w==0x57B8)found.Add(at);
    Check(!((w>>26 is 35 or 43)&&((w>>21)&31)==28&&(ushort)w==0x554),"No hidden nativeGP demo-index access");
}
Check(found.SequenceEqual(indexReferences),"All direct native demo-index uses accounted for");
for(int bits=0;bits<16;bits++)
{
    int[] choices=[10,11,24,33];var cleared=choices.Where((id,i)=>(bits&(1<<i))!=0).ToHashSet();
    byte[] staged=exe.ToArray();staged[0x100]=0xA5;
    byte[] result=NativeClearedTitleDemos.Prepare(exe,staged,cleared);
    for(int i=0;i<4;i++)
    {
        int donor=cleared.Contains(levels[i])?2:i;
        Check(Word(result,0x5F67C+i*4)==levels[donor],"Correctretiredlevel");
        Check(Word(result,0x5F68C+i*4)==frames[donor],"Correctretiredduration");
        Check(result.AsSpan(0x5F69C+i*16,16).SequenceEqual(exe.AsSpan(0x5F69C+donor*16,16)),"Completeoriginaldonorpose");
    }
    for(int at=0;at<exe.Length;at++)if(at<0x5F67C||at>=0x5F6DC){byteChecks++;if(result[at]!=staged[at])throw new Exception("Unowned executable byte changed");}
    Check(NativeClearedTitleDemos.Prepare(exe,result,cleared).SequenceEqual(result),"Ownedreplacementidempotent");
    Check(NativeClearedTitleDemos.Prepare(exe,result,new HashSet<int>()).SequenceEqual(staged),"Undoallretirements exact");
}
foreach(var (start,length) in new[]{(0x48A0,0x84),(0x233E0,0x4C),(0x23D0C,0x38),(0x23EDC,0x30),(0x24024,0x20)})
    for(int at=start;at<start+length;at+=4)
    {
        byte[] bad=exe.ToArray();bad[at]^=1;byte[] before=bad.ToArray();
        Refuse(()=>NativeClearedTitleDemos.Prepare(exe,bad,new HashSet<int>{11}),"Everyconsumerwordguarded");
        Check(bad.SequenceEqual(before),"Refusalatomic");
    }
for(int at=0x5F67C;at<0x5F6DC;at++)
{
    byte[] bad=exe.ToArray();bad[at]^=0x80;
    Refuse(()=>NativeClearedTitleDemos.Prepare(exe,bad,new HashSet<int>{11,24,33}),"Everyrecordbyteguarded");
}
{
    var mixed=exe.ToArray();Put(mixed,0x5F67C,55);
    Refuse(()=>NativeClearedTitleDemos.Prepare(exe,mixed,new HashSet<int>{11}),"Mixedlevel/duration/pose cannotbe relabeled");
}
foreach(int id in new[]{55,15,65,-1})Refuse(()=>NativeClearedTitleDemos.Prepare(exe,exe,new HashSet<int>{id}),"Fallbackcannotbe cleared/invalidscope");
if(args.Length>1)
{
    Directory.CreateDirectory(args[1]);File.WriteAllText(Path.Combine(args[1],"title-demo-source-contract.json"),JsonSerializer.Serialize(new{
        profile=NativeClearedTitleDemos.ProfileId,checks,byteChecks,originalLevels=levels,originalFrames=frames,
        indexReferences=indexReferences.Select(at=>"0x"+(0x8000F800u+(uint)at).ToString("X8")),
        outputSha256=Convert.ToHexStringLower(SHA256.HashData(NativeClearedTitleDemos.Prepare(exe,exe,new HashSet<int>{11,24,33}))),runtime="not-tested"},new JsonSerializerOptions{WriteIndented=true}));
}
Console.WriteLine($"NATIVE_CLEARED_TITLE_DEMOS_SOURCE_OK {checks} semantic/census checks +{byteChecks} untouched-byte checks; runtime not-tested.");
