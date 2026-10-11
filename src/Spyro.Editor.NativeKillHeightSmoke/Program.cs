using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Universal;

// Kill heights of rebuilt retail slots (NativeKillHeights). Default: source-free, on a synthetic executable carrying the
// USA table. --source <USA.bin>: the real executable's table, its four consumers, and every normal slot's Start Blank.
int checks=0;
void Check(bool condition,string message){checks++;if(!condition)throw new Exception(message);}
void Refuse(Action action,string message){try{action();}catch(InvalidDataException){checks++;return;}throw new Exception(message);}
int Word(byte[] exe,int levelId)=>BinaryPrimitives.ReadInt32LittleEndian(exe.AsSpan(NativeKillHeights.Offset(levelId)));
int[] ids=Enumerable.Range(0,NativeKillHeights.Count).Select(i=>(i/6+1)*10+i%6).ToArray();
SceneDocument Floor(float z,float? spawn=null)=>SceneDocument.New("KILL HEIGHT") with
{
    Spawn=new(100,100,spawn??z+32),
    Surfaces=[new("f",[new(0,0,z),new(200,0,z),new(200,200,z),new(0,200,z)],new(128,128,128),null)],
};
var startBlank=SceneDocument.New("START BLANK");
// The editor's own starter floor is at 544 (start 576): it must survive in every slot.
Check(NativeKillHeights.Lowest(startBlank)==544,"Start Blank's starter floor is at 544");
// Only the kill word may change: every other byte of the staged executable stays.
void RequireOnlyWord(byte[] before,byte[] after,int levelId,string what)
{
    int at=NativeKillHeights.Offset(levelId);
    for(int i=0;i<before.Length;i++)if((i<at||i>=at+4)&&before[i]!=after[i])throw new Exception(what+$": byte 0x{i:X} changed outside level {levelId}'s kill word");
    checks++;
}

if(args.Length==0)
{
    // The documented USA table is the one the hash names; the values in editor units.
    byte[] table=new byte[NativeKillHeights.Count*4];
    for(int i=0;i<NativeKillHeights.Count;i++)BinaryPrimitives.WriteInt32LittleEndian(table.AsSpan(4*i),NativeKillHeights.UsaWorldUnits[i]);
    Check(Convert.ToHexStringLower(SHA256.HashData(table))==NativeKillHeights.UsaTableSha256,"UsaWorldUnits is the USA table");
    var expected=new Dictionary<int,float>{[10]=32,[21]=687.5f,[22]=781.25f,[23]=1000,[24]=954.6875f,[30]=1125,[31]=500,[32]=2000,[33]=937.5f,
        [41]=256,[43]=1152,[50]=656.25f,[51]=1375,[52]=1406.25f,[53]=1500,[54]=1562.5f};
    byte[] usa=new byte[0x66000];
    for(int i=0;i<usa.Length;i++)usa[i]=(byte)(i*31+7);   // a recognisable complement
    table.CopyTo(usa,NativeKillHeights.TableOffset);
    foreach(int id in ids)Check(NativeKillHeights.ReadOriginal(usa,id)==(expected.TryGetValue(id,out float v)?v:64),$"level {id}'s original height");

    // Index: (world - 1) x 6 + level for the 36 words, nothing else.
    Check(ids.Select(NativeKillHeights.Index).SequenceEqual(Enumerable.Range(0,36)),"36 indexes in table order");
    foreach(int bad in new[]{9,16,19,26,66,70,-1})
    {
        bool refused=false;try{NativeKillHeights.Index(bad);}catch(ArgumentOutOfRangeException){refused=true;}
        Check(refused,$"level {bad} has no kill height");
    }

    // Start Blank in every slot: lowered to 288 (544 - 256) where the slot's is higher, and only that word changes.
    foreach(int id in ids)
    {
        byte[] staged=usa.ToArray();
        float? lowered=NativeKillHeights.LowerFor(usa,staged,id,startBlank);
        int original=NativeKillHeights.UsaWorldUnits[NativeKillHeights.Index(id)];
        Check(Word(staged,id)==Math.Min(original,288*16),$"Start Blank in level {id}");
        Check(lowered==(original>288*16?(float?)288:null),$"level {id} reports its new height only when lowered");
        Check(NativeKillHeights.Read(staged,id)<544,$"Start Blank's floor stands above level {id}'s kill height");
        RequireOnlyWord(usa,staged,id,"Start Blank");
        NativeKillHeights.RequireOwned(usa,staged,new HashSet<int>{id});checks++;
        if(lowered!=null)Refuse(()=>NativeKillHeights.RequireOwned(usa,staged,new HashSet<int>()),$"level {id}'s lowered word is undeclared without its rebuilt level");
        Check(NativeKillHeights.Warning(usa,id,startBlank)==null,$"no warning for Start Blank in level {id}");
    }

    // Ice Cavern (1,000): Molten Crater's floors (624..652) -> 368; low floors stop at 64; high floors keep 1,000.
    byte[] ice=usa.ToArray();
    var molten=Floor(652) with{Surfaces=[..Floor(652).Surfaces,new("g",[new(0,0,624),new(10,0,624),new(0,10,624)],new(1,2,3),null)]};
    Check(NativeKillHeights.LowerFor(usa,ice,23,molten)==368&&Word(ice,23)==368*16,"Molten Crater in Ice Cavern: 624 - 256");
    Check(NativeKillHeights.LowerFor(usa,ice,23,molten)==368&&Word(ice,23)==368*16,"lowering again is the same word (idempotent)");
    foreach(var (z,height) in new[]{(320f,64f),(300f,64f),(100f,64f),(-500f,64f),(1255f,999f),(1300f,1000f),(2000f,1000f)})
    {
        byte[] staged=usa.ToArray();NativeKillHeights.LowerFor(usa,staged,23,Floor(z));
        Check(NativeKillHeights.Read(staged,23)==height,$"Ice Cavern with floors at {z}: {height}");
    }
    Check(NativeKillHeights.For(16000,300)==NativeKillHeights.FloorWorldUnits&&NativeKillHeights.For(16000,float.MinValue)==NativeKillHeights.FloorWorldUnits,"never under 1,024 world units");
    // The start counts as a floor: a start below the faces lowers further.
    {byte[] staged=usa.ToArray();NativeKillHeights.LowerFor(usa,staged,23,Floor(900,spawn:600));Check(NativeKillHeights.Read(staged,23)==344,"a start below the faces counts");}
    // Never raised: Artisans keeps 32 even for floors at 20 (64 would be higher); Terrace Village (256) goes to 64.
    {byte[] staged=usa.ToArray();Check(NativeKillHeights.LowerFor(usa,staged,10,Floor(20))==null&&staged.SequenceEqual(usa),"Artisans keeps its own 32");}
    {byte[] staged=usa.ToArray();Check(NativeKillHeights.LowerFor(usa,staged,41,Floor(300))==64,"Terrace Village 256 -> 64");}
    Check(NativeKillHeights.Minimum(usa,23)==64&&NativeKillHeights.Minimum(usa,10)==32&&NativeKillHeights.Minimum(usa,41)==64&&NativeKillHeights.Minimum(usa,65)==64,"minimums");

    // Conflicts: an edited staged word is refused, and the refusal leaves the staged executable alone.
    {
        byte[] staged=usa.ToArray();BinaryPrimitives.WriteInt32LittleEndian(staged.AsSpan(NativeKillHeights.Offset(23)),9999);byte[] before=staged.ToArray();
        Refuse(()=>NativeKillHeights.LowerFor(usa,staged,23,startBlank),"an edited kill height is not overwritten");
        Check(staged.SequenceEqual(before),"refusal is atomic");
        Refuse(()=>NativeKillHeights.LowerFor(usa,ice.ToArray(),23,Floor(900)),"a word lowered for another scene is a conflict");
    }
    // The original must be the USA table; the staged executable must be complete.
    {
        byte[] edited=usa.ToArray();edited[NativeKillHeights.Offset(32)]^=1;
        Refuse(()=>NativeKillHeights.ReadOriginal(edited,23),"edited source table refused (read)");
        Refuse(()=>NativeKillHeights.LowerFor(edited,edited.ToArray(),23,startBlank),"edited source table refused (lower)");
        Refuse(()=>NativeKillHeights.Minimum(edited,23),"edited source table refused (minimum)");
        Refuse(()=>NativeKillHeights.RequireOwned(edited,usa,new HashSet<int>()),"edited source table refused (window)");
        Refuse(()=>NativeKillHeights.LowerFor(usa,new byte[0x60000],23,startBlank),"short staged executable refused");
    }
    // The window: rebuilt levels' words only, only downwards, never under 1,024.
    {
        byte[] two=usa.ToArray();
        NativeKillHeights.LowerFor(usa,two,23,startBlank);NativeKillHeights.LowerFor(usa,two,32,Floor(700));
        Check(Word(two,23)==288*16&&Word(two,32)==444*16,"Ice Cavern and High Caves rebuilt in one game");
        NativeKillHeights.RequireOwned(usa,two,new HashSet<int>{23,32});checks++;
        Refuse(()=>NativeKillHeights.RequireOwned(usa,two,new HashSet<int>{23}),"High Caves' word needs its rebuilt level");
        void Put(byte[] b,int id,int v)=>BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(NativeKillHeights.Offset(id)),v);
        byte[] raised=usa.ToArray();Put(raised,23,17000);
        Refuse(()=>NativeKillHeights.RequireOwned(usa,raised,new HashSet<int>{23}),"a raised kill height is refused");
        byte[] deep=usa.ToArray();Put(deep,23,1000);
        Refuse(()=>NativeKillHeights.RequireOwned(usa,deep,new HashSet<int>{23}),"a kill height under 1,024 is refused");
        NativeKillHeights.RequireOwned(usa,usa,new HashSet<int>{23,32});checks++;
    }
    // Warnings: only faces under the height Create BIN can reach.
    Check(NativeKillHeights.Warning(usa,23,molten)==null,"Molten Crater in Ice Cavern needs no warning");
    Check(NativeKillHeights.Warning(usa,23,Floor(20)) is { } w1&&w1.StartsWith("1 face is below height 64,",StringComparison.Ordinal),"floor at 20 in Ice Cavern: warned");
    Check(NativeKillHeights.Warning(usa,10,Floor(20)) is { } w2&&w2.Contains("below height 32"),"floor at 20 in Artisans: warned");
    Check(NativeKillHeights.Warning(usa,10,Floor(40))==null,"floor at 40 in Artisans: fine");
    Console.WriteLine($"NATIVE_KILL_HEIGHT_SOURCE_FREE_OK checks={checks}");
    return;
}
if(args.Length!=2||args[0]!="--source")throw new ArgumentException("Usage: NativeKillHeightSmoke [--source <original USA BIN>]");
{
    byte[] exe=UniversalDiscImage.Read(args[1]).Exe;
    Check(Convert.ToHexStringLower(SHA256.HashData(exe.AsSpan(NativeKillHeights.TableOffset,NativeKillHeights.Count*4)))==NativeKillHeights.UsaTableSha256,"the disc's table is the USA table");
    foreach(int id in ids)Check(NativeKillHeights.ReadOriginal(exe,id)*16==NativeKillHeights.UsaWorldUnits[NativeKillHeights.Index(id)],$"level {id}'s word");
    // Every instruction addressing the table (low half 0xE9A4 after lui 0x8007) is one of the four consumers, each loading
    // table[level index (0x80075964)] for a signed compare.
    uint U(int at)=>BinaryPrimitives.ReadUInt32LittleEndian(exe.AsSpan(at));
    int At(uint ram)=>(int)(ram-0x8000F800);
    var sites=new List<uint>();
    for(int at=0x800;at+4<=exe.Length;at+=4)if((U(at)&0xFC00FFFF)==(0x8C00E9A4&0xFC00FFFF)||(U(at)&0xFC00FFFF)==0x2400E9A4)sites.Add((uint)(at+0x8000F800));
    Check(sites.SequenceEqual(new uint[]{0x80042030,0x80042C68,0x8004A080,0x8004A54C}),"exactly the four known table loads");
    foreach(uint site in sites)
    {
        int at=At(site);
        Check(U(at)==0x8C22E9A4&&U(at-4)==0x00220821&&U(at-8)==0x3C018007&&U(at-12)==0x00021080,$"0x{site:X8}: lw v0,table[index*4]");
        Check(Enumerable.Range(1,6).Any(k=>U(at-4*k)==0x8C425964),$"0x{site:X8}: index from 0x80075964");
        Check(U(at+8) is 0x0062182Au or 0x0044102Au,$"0x{site:X8}: signed compare");
    }
    // Every normal slot: the retail binding plus the kill word, and nothing else.
    var census=NativeClearedRetailBinding.Census(exe);
    Check(census.Count==30,"30 normal slots");
    foreach(var profile in census)
    {
        var target=new LevelDefinition{Key="slot"+profile.LevelId,LevelId=profile.LevelId,SourceWadEntry=10+2*profile.ContinuousIndex};
        byte[] prepared=NativeClearedRetailBinding.Prepare(exe,exe,target);
        Check(Word(prepared,profile.LevelId)==Word(exe,profile.LevelId),$"level {profile.LevelId}: the retail binding leaves the kill table alone");
        byte[] both=prepared.ToArray();NativeKillHeights.LowerFor(exe,both,profile.LevelId,startBlank);
        RequireOnlyWord(prepared,both,profile.LevelId,"Start Blank on the real executable");
        Check(NativeKillHeights.Read(both,profile.LevelId)==Math.Min(NativeKillHeights.ReadOriginal(exe,profile.LevelId),288),$"level {profile.LevelId}'s Start Blank height");
        NativeKillHeights.RequireOwned(exe,both,new HashSet<int>{profile.LevelId});checks++;
    }
    Console.WriteLine($"NATIVE_KILL_HEIGHT_SOURCE_OK checks={checks}");
}
