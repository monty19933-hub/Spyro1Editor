using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;

int checks = 0;
void Check(bool value, string why) { if (!value) throw new Exception(why); checks++; }
uint W(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset));
byte[] Words(params uint[] words)
{
    byte[] bytes = new byte[words.Length * 4];
    for (int i = 0; i < words.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i * 4), words[i]);
    return bytes;
}
const uint Gate = 0x8007DA54, Continue = 0x80085780, Stop = 0x80010000;
const uint BootSp = 0x801FFFF8, DispatchSp = BootSp - 0x20 - 0x20 - 0x1C0, FrameSp = DispatchSp - 0x2B0;
byte[] gate = Words(0x92620048, 0, 0x2C420080, 0x10401F47, 0);
foreach (uint state in new uint[] { 0, 1, 0x7F, 0x80, 0xFD, 0xFF })
{
    var cpu = new RenderProbeCpu(); cpu.Copy(Gate, gate); cpu.Regs[19] = 0x80170000;
    cpu.Put(0x80170048, state, 1); cpu.Run(Gate, state < 0x80 ? Gate + 0x14 : Continue);
    Check(cpu.Writes.Count == 0 && cpu.Reads.SequenceEqual(new[] { 0x80170048u }), "delayed state guard only reads +48 for " + state);
    Check(cpu.Regs[2] == (state < 0x80 ? 1u : 0u), "FD/FF skipped, state below80 admitted to dispatch");
}
Check(FrameSp - Math.Max(0x28u, 0x40u) == 0x801FFB08 && FrameSp - 0x40 - 0x801FF800 == 0x308,
    "sequential visual/attempt call high-water, not sum of non-overlapping frames");
if (args.Length == 0)
{
    Console.WriteLine($"JP_EGG_SCHEDULING_SOURCE_FREE_OK checks={checks} sourceReads=0 hookInstalled=false"); return;
}
if (args.Length != 3 || args[0] != "--source") throw new ArgumentException("Usage: no arguments | --source <exact-JP-BIN> <exact-USA-BIN>");
using var jp = NativeRegionalSourceSession.Open(args[1], NativeDiscRevision.JapanScps10083);
using var usa = NativeRegionalSourceSession.Open(args[2], NativeDiscRevision.UsaScus94228);
try
{
    NativeJapaneseEggVisualPlacement[] placements = [new("egg-a",72704,47104,6656,32,1), new("egg-b",72704,49152,6656,96,1)];
    var integration = NativeJapaneseEggArtisansIntegrationPlan.Capture(jp, usa, placements);
    var rgb = NativeJapaneseEggRgbEditSource.Capture(NativeJapaneseEggModelAssetPlan.Capture(jp, usa), usa);
    var authored = NativeJapaneseEggAuthoredClassesPlan.Capture(usa, NativeJapaneseEggAuthoredClassReservation.Capture(usa), integration,
        rgb.CreateAuthored(Enumerable.Repeat(0x332211u,70).ToArray(), Enumerable.Repeat(0x665544u,70).ToArray()),
        rgb.CreateAuthored(Enumerable.Repeat(0xCCBBAAu,70).ToArray(), Enumerable.Repeat(0xFFEEDDu,70).ToArray()));
    byte[] exe = integration.CopyOriginalExecutable(), overlay = authored.CopyOverlay(), scene = authored.CopyScene();
    void Pin(byte[] owner, uint ownerBase, uint start, int bytes, string hash)
    {
        Check(Convert.ToHexStringLower(SHA256.HashData(owner.AsSpan((int)(start - ownerBase), bytes))) == hash,
            "complete source instruction owner " + start.ToString("X8"));
    }
    Pin(exe,0x8000F800,0x80012204,0xD0,"dabba895a20eb86eeec00307ae5913c9fa6b51eea16bec9cd8b08acaa173c69e");
    Pin(exe,0x8000F800,0x80033A88,0x114,"a8a90f0c1f6c26f646761f697bfbe63c45eab79456894237be7f5d1b864183d2");
    Pin(overlay,0x8007AA38,0x8007D9C8,0xD4,"840778d29ab94103b71da35dbfe8c0b6e30a925bed6e89aab05551835779cce3");
    Pin(overlay,0x8007AA38,0x80085780,0x4C,"6184b1194497b8c179effe34b9de247ad493df9698c991c4758f05b48138e169");
    Pin(exe,0x8000F800,0x80051FEC,0x1D4,"56e34ffbc80b8e79b1a15e37b0280f88df68bebe61d45d695fea9d2c960707ab");
    Pin(exe,0x8000F800,0x800521C0,0x100,"055bbc5310be05cf9a5b0c185f5b18ea5056520d5a2f4e23c2e44ad79aec5cf2");
    Pin(exe,0x8000F800,0x800522C0,0x1A4,"43915998682dbb7fe33175b21f638f2720ce954efe2dd3079f57bb51275be512");
    Pin(exe,0x8000F800,0x8001EFC0,0x60,"78a9ecaeb9c324c4d6609d0b89538a6cbb3345990bbf5fa262eec5683a6481bb");
    Pin(exe,0x8000F800,0x80052568,0x64,"602fc31b1351212e5d5c72c39ac59cc86a9afd0226a9a0549192855588498505");
    Pin(exe,0x8000F800,0x800144C8,0x9C,"fae8117d233b5838e56ab55c326c6d86e6a078a9642ef252bb395e6edaec8990");
    Check(overlay.AsSpan((int)(Gate-0x8007AA38), gate.Length).SequenceEqual(gate), "source-free guard equals actual source owner");
    Check(W(exe,(int)(0x80012230-0x8000F800))==0x0C00CE17 && W(exe,(int)(0x8001227C-0x8000F800))==0x0C007B57 &&
        W(exe,(int)(0x8001EFC8-0x8000F800))==0x0C014870, "ordinary main update precedes normal render-list rebuild");
    var calls = new ExistingDetachedCallables(authored, jp, usa);
    Check(calls.MaximumStackBytes == 0x40, "existing attempt and predicate maximum is40");
    uint row = authored.Bindings[0].ProjectedRowAddress, other = authored.Bindings[1].ProjectedRowAddress;
    const uint UpdateList = 0x800700F4, DrawList = 0x80071EF4;
    uint restore = integration.VisualAncestor.CodeOwners.Single(o=>o.Id=="restore-frame").RuntimePc;
    Check(restore==0x80088B14 && W(overlay,(int)(0x80088AE4-0x8007AA38))==(0x08000000|((restore>>2)&0x3FFFFFF)) &&
        W(overlay,(int)(0x80088AE8-0x8007AA38))==0, "exact owned postvisual J/NOP edge reaches restore-frame80088B14");
    Check(W(overlay,(int)(0x80088C3C-0x8007AA38))==0x0802228E && W(overlay,(int)(0x80088C40-0x8007AA38))==0,
        "exact adapter-entry J/NOP edge reaches visual entry80088A38 after saving full frame");
    byte[] Snapshot(RenderProbeCpu cpu,uint at,int n)=>Enumerable.Range(0,n).Select(i=>(byte)cpu.Get(at+(uint)i,1)).ToArray();
    RenderProbeCpu Load()
    {
        var cpu = new RenderProbeCpu { StrictSignedArithmetic = true };
        cpu.Copy(0x8000F800,exe); cpu.Copy(0x8007AA38,overlay); cpu.Copy(authored.Projection.ProjectedSceneRamStart,scene);
        foreach(var binding in authored.Bindings)
        {
            // Public source-form package and its existing loader projections.
            cpu.Copy(binding.ProjectedRoot,binding.Edit.CopyPackage());
            foreach(var field in binding.LoaderFields) cpu.Put(binding.ProjectedRoot+(uint)field.PackageFieldOffset,field.ProjectedLoadedWord);
            cpu.Put(0x80076378+binding.AuthoredDestinationClass*4u,binding.ProjectedRoot);
            cpu.Put(binding.ProjectedRowAddress,binding.ProjectedPrivateAddress);
            cpu.Regs[4]=binding.ProjectedRowAddress;cpu.Run(0x800526A8,0x80052758);
            Check(cpu.Get(binding.ProjectedRowAddress+0x34,2)==0xFFFF,"actual animated initializer makes non-grid authored row");
            // Narrow two-row scheduling fixture: forced visible-list input,
            // not a GTE/real-frustum admission or full scene runtime claim.
            cpu.Put(binding.ProjectedRowAddress+0x51,1,1);
        }
        cpu.Put(0x80075828,row);cpu.Put(0x80075890,other+0x58);cpu.Put(other+0x58+0x48,0xFF,1);
        cpu.Put(0x800756CC,1);cpu.Put(0x800756C4,1);cpu.Put(0x80078A58,72704);cpu.Put(0x80078A5C,47104);cpu.Put(0x80078A60,6656);
        cpu.Copy(ExistingDetachedCallables.AttemptPc,calls.AttemptBytes);cpu.Copy(ExistingDetachedCallables.PredicateBase,calls.PredicateBytes);
        cpu.Copy(ExistingDetachedCallables.CollectorPc,calls.CollectorBytes);cpu.Copy(ExistingDetachedCallables.Ledger,calls.EmptyLedger);
        cpu.Copy(ExistingDetachedCallables.QueryPc,calls.QueryBytes);
        cpu.Regs[29]=DispatchSp+0x1C0;cpu.Regs[31]=Stop;
        cpu.Reads.Clear();cpu.Writes.Clear();cpu.Calls.Clear();cpu.Visited.Clear();return cpu;
    }
    List<uint> List(RenderProbeCpu cpu,uint at)
    {List<uint> result=[];for(int i=0;i<256&&cpu.Get(at+(uint)i*4)!=0;i++)result.Add(cpu.Get(at+(uint)i*4));if(result.Count==256)throw new Exception("Unterminated list");return result;}
    void Retire(RenderProbeCpu cpu,uint target)
    {
        cpu.Regs[4]=target;cpu.Regs[31]=Stop;cpu.Reads.Clear();cpu.Writes.Clear();cpu.Run(0x80052568,Stop);
        Check(cpu.Writes.SequenceEqual(new[]{target+0x48})&&cpu.Get(target+0x48,1)==0xFD,"native placed retirement exactly FD store");
    }
    var current=Load();
    current.Run(0x8007D9C8,restore);
    Check(current.Calls.Contains(0x80051FEC)&&current.Calls.Contains(0x800522C0)&&
        current.Visited.Contains(integration.VisualAncestor.EntryPc)&&current.Regs[29]==FrameSp,
        "actual callback builds update list, animates, then reaches postvisual pre-restore seam");
    Check(current.Calls.IndexOf(0x80051FEC)<current.Calls.IndexOf(0x800522C0)&&
        current.Calls.IndexOf(0x800522C0)<current.Calls.IndexOf(0x80016C58),"actual source call order precedes visual sine helper");
    Check(List(current,UpdateList).SequenceEqual(new[]{row,other}),"actual update list contains both forced-visible source rows");
    Check(current.Get(row+0x48,1)==0&&current.Get(row+0x49,1)!=0&&!current.Writes.Contains(row+0x48),
        "visual first tick initializes timer49, never live state48");
    byte[] savedFrame=Snapshot(current,FrameSp+0x230,0x80);
    current.Put(0x80078A60,current.Get(row+0x14));current.Regs[4]=row;current.Regs[5]=ExistingDetachedCallables.Ledger;current.Regs[31]=Stop;
    current.Writes.Clear();current.Run(ExistingDetachedCallables.AttemptPc,calls.PredicatePc);
    Check(current.Regs[29]==FrameSp-0x20,"attempt allocates exact20 before entering native predicate wrapper");
    current.Run(calls.PredicatePc,calls.PredicatePc+4);
    Check(current.Regs[29]==FrameSp-0x40&&current.Regs[29]==0x801FFB08,"executed nested predicate prologue reaches computed high-water");
    current.Run(calls.PredicatePc+4,Stop);
    Check(current.Regs[2]==0&&current.Get(ExistingDetachedCallables.Ledger+0x90,1)==1,"actual existing attempt collects at postvisual seam");
    Check(current.Writes.Count(a=>a>=ExistingDetachedCallables.Ledger&&a<ExistingDetachedCallables.Ledger+0x200)==8&&
        !current.Writes.Any(a=>a>=row&&a<other+0x58),"attempt retains its eight ledger stores and no native row writes before retirement");
    Check(current.Regs[29]==FrameSp&&current.Writes.Where(a=>a>=0x801FF800).Min()==FrameSp-0x30&&
        savedFrame.SequenceEqual(Snapshot(current,FrameSp+0x230,0x80)),"nested40 extent preserves adapter saved registers; deepest saved word at SP-30");
    Retire(current,row);current.Run(restore,Continue);
    Check(current.Regs[29]==DispatchSp&&current.Get(row+0x48,1)==0xFD,"full-register adapter restore preserves native FD and restores dispatch SP");
    int[] savedRegisters=Enumerable.Range(1,31).Where(r=>r!=29).ToArray();
    Check(savedRegisters.Select((r,i)=>current.Regs[r]==W(savedFrame,i*4)).All(v=>v)&&
        current.Hi==W(savedFrame,0x78)&&current.Lo==W(savedFrame,0x7C),"existing adapter restores all GPRs and HI/LO after attempt plus native retirement");
    current.Run(Continue,Stop);
    Check(current.Get(row+0x48,1)==0xFD&&current.Get(other+0x48,1)==0&&current.Get(other+0x49,1)!=0,
        "native continuation advances cursor and completes second visual row without resurrecting first");
    current.Regs[31]=Stop;current.Run(0x800521C0,Stop);
    Check(List(current,DrawList).SequenceEqual(new[]{other}),"same-frame normal render-list rebuild excludes exactly retired row");
    byte[] retiredRow=Snapshot(current,row,0x58),retiredPrivate=Snapshot(current,authored.Bindings[0].ProjectedPrivateAddress,12);
    current.Regs[29]=DispatchSp+0x1C0;current.Regs[31]=Stop;current.Run(0x8007D9C8,Stop);
    Check(List(current,UpdateList).SequenceEqual(new[]{other})&&retiredRow.SequenceEqual(Snapshot(current,row,0x58))&&
        retiredPrivate.SequenceEqual(Snapshot(current,authored.Bindings[0].ProjectedPrivateAddress,12)),"next callback rebuild excludes retired row before animation/visual dispatch");
    foreach(uint tick in new uint[]{1,3,4})
    {
        var cadence=Load();cadence.Put(0x800756CC,tick);cadence.Run(0x8007D9C8,Stop);
        Check(cadence.Calls.Count(pc=>pc==0x800522C0)==(tick<3?1:2),"source tick cadence animation-call count for "+tick);
        Check(cadence.Calls.IndexOf(0x800522C0)<cadence.Calls.IndexOf(0x80016C58),"all tick animation precedes visual dispatch for "+tick);
    }

    // Counterexample: a cached animation list does not inspect +48. It can
    // still write animation fields after FD, even though FD itself survives.
    var stale=Load();stale.Run(0x8007D9C8,Stop);Retire(stale,row);stale.Writes.Clear();
    stale.Regs[4]=UpdateList;stale.Regs[5]=0;stale.Regs[31]=Stop;stale.Run(0x800522C0,Stop);
    Check(stale.Get(row+0x48,1)==0xFD&&stale.Writes.Contains(row+0x3C)&&stale.Writes.Contains(row+0x40),
        "cached animation list may update retired row fields; marker alone is not scheduling proof");
    var beforeVisual=Load();beforeVisual.Regs[29]=DispatchSp;beforeVisual.Regs[19]=row;Retire(beforeVisual,row);
    beforeVisual.Regs[19]=row;beforeVisual.Run(integration.VisualAncestor.EntryPc,Continue);
    Check(beforeVisual.Get(row+0x48,1)==0xFD&&beforeVisual.Get(row+0x49,1)!=0,"direct visual helper after retirement does not overwrite FD, but still performs visual writes");

    // Re-created source-form scene rows have live state zero. The complete
    // native death CD read is pinned, not emulated by this detached source copy.
    var reloaded=Load();reloaded.Copy(ExistingDetachedCallables.Ledger,calls.EarnedFirstLedger);
    reloaded.Put(0x80078A58,0);reloaded.Put(0x80078A5C,0);reloaded.Regs[4]=row;reloaded.Regs[5]=ExistingDetachedCallables.Ledger;
    reloaded.Regs[29]=FrameSp;reloaded.Regs[31]=Stop;reloaded.Reads.Clear();reloaded.Writes.Clear();reloaded.Calls.Clear();
    reloaded.Run(ExistingDetachedCallables.AttemptPc,Stop);
    Check(reloaded.Regs[2]==9&&!reloaded.Calls.Contains(ExistingDetachedCallables.CollectorPc)&&
        !reloaded.Reads.Any(a=>a>=ExistingDetachedCallables.Ledger&&a<ExistingDetachedCallables.Ledger+0x200),
        "earned-out-of-range counterexample: NotEligible before any ledger read, not AlreadyEarned");
    reloaded.Regs[31]=Stop;reloaded.Run(0x800521C0,Stop);
    Check(reloaded.Get(row+0x48,1)==0&&List(reloaded,DrawList).Contains(row),"earned row visibly reenters list unless independently suppressed before visual/proximity");
    reloaded.Regs[4]=ExistingDetachedCallables.Ledger;reloaded.Regs[5]=0;reloaded.Regs[31]=Stop;
    reloaded.Reads.Clear();reloaded.Writes.Clear();reloaded.Calls.Clear();
    reloaded.Run(ExistingDetachedCallables.QueryPc,Stop);
    Check(reloaded.Regs[2]==1&&reloaded.Writes.Count==0&&reloaded.Calls.Count==0&&
        reloaded.Reads.All(a=>a>=ExistingDetachedCallables.Ledger+0x10&&a<ExistingDetachedCallables.Ledger+0xB0),
        "read-only query finds active earned identity independent of native proximity without mutation");

    // Test-only orchestration of the minimum previsual recipe, not an installed
    // dispatch hook: query identifies this active earned row, native FD retires
    // it, and the FIRST actual callback must never animate/initialize it.
    var suppressed=Load();suppressed.Copy(ExistingDetachedCallables.Ledger,calls.EarnedFirstLedger);
    byte[] uninitializedPrivate=Snapshot(suppressed,authored.Bindings[0].ProjectedPrivateAddress,12);
    byte timerBefore=(byte)suppressed.Get(row+0x49,1);
    suppressed.Regs[4]=ExistingDetachedCallables.Ledger;suppressed.Regs[5]=0;suppressed.Regs[31]=Stop;
    suppressed.Run(ExistingDetachedCallables.QueryPc,Stop);
    Check(suppressed.Regs[2]==1,"previsual recipe requires exact active-earned result, never a generic refusal");
    Retire(suppressed,row);suppressed.Regs[29]=DispatchSp+0x1C0;suppressed.Regs[31]=Stop;
    suppressed.Run(0x8007D9C8,Stop);suppressed.Regs[31]=Stop;suppressed.Run(0x800521C0,Stop);
    Check(List(suppressed,UpdateList).SequenceEqual(new[]{other})&&List(suppressed,DrawList).SequenceEqual(new[]{other})&&
        suppressed.Get(row+0x49,1)==timerBefore&&uninitializedPrivate.SequenceEqual(Snapshot(suppressed,authored.Bindings[0].ProjectedPrivateAddress,12)),
        "query-before-visual native suppression prevents first callback animation/private initialization and drawing");
    var retired=calls.RetiredFirst(authored,Check);reloaded.Copy(ExistingDetachedCallables.CollectorPc,retired.Code);
    reloaded.Copy(ExistingDetachedCallables.Ledger,retired.Ledger);reloaded.Regs[4]=ExistingDetachedCallables.Ledger;
    reloaded.Regs[5]=0;reloaded.Regs[31]=Stop;reloaded.Writes.Clear();reloaded.Run(ExistingDetachedCallables.CollectorPc,Stop);
    Check(reloaded.Regs[2]==2&&reloaded.Writes.Count==0,"retired identity is a separate no-write Retired outcome, not active AlreadyEarned");
    Console.WriteLine($"JP_EGG_SCHEDULING_SOURCE_OK checks={checks} sameFrameRetirement=true earnedOutOfRangeRespawns=true retiredIdentityDistinct=true nestedStackLow=801FFB08 hookInstalled=false lifetime=false");
}
finally { try { jp.VerifyUnchanged(); } finally { usa.VerifyUnchanged(); } }
