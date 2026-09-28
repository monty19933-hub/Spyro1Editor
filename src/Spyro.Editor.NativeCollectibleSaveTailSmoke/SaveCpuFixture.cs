using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;
using F = AttemptFixtures;

// Real emitted admission and guarded pickup; only native card service and
// CD/model loading boundaries are supplied. No emulator, card or disc writes.
internal sealed class SaveCpuFixture
{
    internal const uint Live = 0x801FF600, Stop = 0x80010000, Stack = 0x801FFCA0;
    internal RenderProbeCpu Cpu { get; } = new() { StrictSignedArithmetic = true };
    internal NativeJapaneseEggLifecycleOverlayPlan Lifecycle { get; }
    internal NativeJapaneseEggAuthoredClassesPlan Authored { get; }
    internal byte[] Envelope { get; }
    internal SaveCpuFixture(byte firstBits = 1, byte secondBits = 2, byte thirdBits = 1)
    {
        var manifest = NativeCollectibleCardLoadResearchComposition.FixedManifest();
        var mappings = new[] { new NativeJapaneseEggCollectibleIdentity("egg-a",Guid.Parse("11111111-1111-1111-1111-111111111111")),
            new NativeJapaneseEggCollectibleIdentity("egg-b",Guid.Parse("22222222-2222-2222-2222-222222222222")) };
        Authored = F.Authored(); Lifecycle = NativeJapaneseEggLifecycleOverlayPlan.FixtureGuarded(Authored,manifest,0,mappings,F.Predicate());
        var card = NativeCollectibleCardAdmissionPlan.CreateFixture(); var c = Cpu;
        c.Copy(0x8000F800,card.CopyExecutable()); c.Copy(0x8007AA38,card.CopyOverlay());
        c.Copy(NativeCollectibleCardLoadPlan.PollPc,Convert.FromHexString("0780023c505b42240000428c0000000006004014000000000780023c585b428c0000000030004010ffff02240780083c505b08250000078d0780033c545b638c0c008014000000000780023c585b428c0000000013004014080003250000628c00000000fdff401000000000b39d0108000000000780023c585b428c0000000008004014000000000200c010000000000000c3ac1400a01021100000c49d01080000a7ac0600c010000000000780023c985b42240000428c000000000000c2ac0600a010000000000780023c945b42240000428c000000000000a2ac0780013c585b20ac010002240800e00300000000"));
        c.Copy(NativeCollectibleCardLoadPlan.ReadPc,F.Words(0x3C088007,0x24090003,0xAD095B50,0xAD005B58,0x24020001,0x03E00008,0));
        c.Copy(NativeCollectibleCardLoadPlan.ShutdownPc,F.Words(0x03E00008,0));
        byte[] prefix = new byte[0x1400]; new Random(728).NextBytes(prefix); prefix[0x7E] = 0xCD; prefix[0x7F] = 0xAB;
        c.Copy(0x80160000,prefix); c.Copy(Live,new byte[512]);
        foreach (var (a,v) in new(uint,uint)[] { (0x800755A4,0xA00),(0x80078D14,1),(0x80078D18,0),(0x80078D1C,0),(0x80078D20,0xABCD),
            (0x80078D78,2),(0x80078D7C,5),(0x80078D8C,0),(0x80078D94,1),(0x80078DA0,0),(0x80078DC4,0x80160000),(0x800785E8,0x80162000) }) c.Put(a,v);
        c.Regs[29] = Stack; c.Regs[31] = Stop; c.Run(card.Kernel.BeginPc,Stop);
        if (c.Regs[2] != 1) throw new Exception("Admission Begin did not accept fixture read.");
        Envelope = DetachedCollectibleSaveCodec.Serialize(DetachedCollectibleSave.FromValidatedBits(manifest,new[] { firstBits,secondBits,thirdBits }));
        byte[] block = new byte[0x2000]; prefix.CopyTo(block,0); Envelope.CopyTo(block,0x1400); c.Copy(card.Reader.ReadBufferAddress,block);
        c.Put(0x80075B50,0); c.Put(0x80075B58,1); c.Put(0x80075B94,3); c.Put(0x80075B98,0);
        c.Run(card.Kernel.GatePc,0x8007CC08);
        if (c.Get(Live+0x16,1) != 1 || c.Get(Live+0x90,1) != firstBits) throw new Exception("Real loader/admission failed.");
        c.Copy(0x8000F800,Lifecycle.CopyExecutable()); c.Put(0x800755A4,0xA00); c.Copy(0x8007AA38,Lifecycle.CopyOverlay());
        c.Copy(Lifecycle.Projection.SceneAddress,Authored.CopyScene());
        foreach (var model in Lifecycle.Projection.Models)
        {
            var b = Authored.Bindings.Single(b => b.RowId == model.RowId); c.Copy(model.RootAddress,b.Edit.CopyPackage());
            foreach (var field in model.LoaderFields) c.Put(model.RootAddress+(uint)field.PackageFieldOffset,field.ProjectedLoadedWord);
            c.Put(0x80076378+model.AuthoredClass*4u,model.RootAddress); c.Put(model.RowAddress,model.PrivateAddress);
            c.Put(model.RowAddress+0x34,0xFFFF,2); c.Put(model.RowAddress+0x51,1,1);
        }
        foreach (var (a,v) in Lifecycle.LiveGuard!.ExpectedSceneGlobals) c.Put(a,v);
        c.Put(0x800756CC,1); c.Put(0x800756C4,1); c.Put(0x80075778,0);
        foreach (uint a in new uint[] { 0x80078C4C,0x80078AD0,0x80078AD4,0x80078A58,0x80078A5C,0x80078A60 }) c.Put(a,0);
        c.Copy(0x80052568,F.Words(0x84850034,0x3C018007,0x24215778,0x04A0000C,0x8C210000,0x00052880,0x00A12820,
            0x20A2FFFC,0x20450004,0x8CA20000,0,0x1444FFFC,0,0x8C820004,0,0xACA20000,0x3C018007,
            0x24215890,0x8C210000,0,0x00240822,0x18200003,0x2002FFFD,0x03E00008,0xA0820048));
    }
    internal void Pickup(int index)
    {
        var row = Lifecycle.Binding.Rows[index]; var c = Cpu;
        foreach (var (a,o) in new(uint,uint)[] { (0x80078A58,12),(0x80078A5C,16),(0x80078A60,20) }) c.Put(a,c.Get(row.RowAddress+o));
        c.Regs[19] = row.RowAddress; c.Regs[29] = 0x801FFDF8; c.Regs[31] = Stop;
        c.Run(Authored.Integration.VisualAncestor.EntryPc,0x80085780);
        if (c.Get(row.RowAddress+0x48,1) != 0xFD) throw new Exception("Real guarded pickup did not retire its row.");
    }
    internal byte[] Ledger() => Bytes(Cpu,Live,512);
    internal static byte[] Bytes(RenderProbeCpu c,uint at,int length) => Enumerable.Range(0,length).Select(i => (byte)c.Get(at+(uint)i,1)).ToArray();
}
