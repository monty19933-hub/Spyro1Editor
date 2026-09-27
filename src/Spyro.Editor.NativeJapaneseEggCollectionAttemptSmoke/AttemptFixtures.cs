using System.Buffers.Binary;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

internal static class AttemptFixtures
{
    internal static NativeJapaneseEggVisualPlacement[] Placements =>
        [new("egg-a",72704,47104,6656,32,1),new("egg-b",72704,49152,6656,96,1)];
    internal static Guid Id(int n) => Guid.Parse(n.ToString("x32"));
    internal static DetachedCollectibleManifest Manifest(int count) => DetachedCollectibleManifest.CreateNew(
        Guid.Parse("12345678-1111-2222-3333-444444444444"),Enumerable.Range(1,count).Select(Id));
    internal static NativeJapaneseEggAuthoredClassesPlan Authored(NativeJapaneseEggVisualPlacement[]? placements = null)
    {
        byte[] old=Fixtures.Model(),model=new byte[0x2C8];old.AsSpan(0,0x88).CopyTo(model);
        Fixtures.Put(model,0x3C,70u<<16|2);Fixtures.Put(model,0x5C,0x130);
        Fixtures.Put(model,0x60,0xFF000248);Fixtures.Put(model,0x68,0xFF000250);
        for(int i=0;i<70;i++){Fixtures.Put(model,0x88+4*i,0xFF102030);Fixtures.Put(model,0x1A0+4*i,0x102030);}
        old.AsSpan(0x90,16).CopyTo(model.AsSpan(0x2B8));
        var asset=NativeJapaneseEggModelAssetPlan.Fixture(model);var source=NativeJapaneseEggRgbEditSource.Fixture(asset);
        byte[] usaOverlay=Fixtures.UsaOverlay();Fixtures.Put(usaOverlay,0x32C4,0x080215E0);
        var visual=NativeJapaneseEggVisualBehaviorPlan.Fixture(asset,Fixtures.Resource(),Fixtures.JapaneseOverlay(),usaOverlay,Fixtures.Executable());
        var integration=NativeJapaneseEggArtisansIntegrationPlan.Fixture(asset,visual,Fixtures.Resource(),Fixtures.Scene(0xF5),
            NativeJapaneseObjectAppearance.DecodeFixture(Fixtures.Profiles()),placements??Placements);
        var a=source.CreateAuthored(Enumerable.Repeat(0x332211u,70).ToArray(),Enumerable.Repeat(0x665544u,70).ToArray());
        var b=source.CreateAuthored(Enumerable.Repeat(0xCCBBAAu,70).ToArray(),Enumerable.Repeat(0xFFEEDDu,70).ToArray());
        return NativeJapaneseEggAuthoredClassesPlan.Fixture(NativeJapaneseEggAuthoredClassReservation.Fixture([0,1,0x1FE,0x1FF]),integration,a,b);
    }
    internal static NativeJapaneseEggAuthoredClassesPlan Authored(NativeRegionalSourceSession jp,NativeRegionalSourceSession usa)
    {
        var integration=NativeJapaneseEggArtisansIntegrationPlan.Capture(jp,usa,Placements);
        var source=NativeJapaneseEggRgbEditSource.Capture(NativeJapaneseEggModelAssetPlan.Capture(jp,usa),usa);
        var a=source.CreateAuthored(Enumerable.Repeat(0x332211u,70).ToArray(),Enumerable.Repeat(0x665544u,70).ToArray());
        var b=source.CreateAuthored(Enumerable.Repeat(0xCCBBAAu,70).ToArray(),Enumerable.Repeat(0xFFEEDDu,70).ToArray());
        return NativeJapaneseEggAuthoredClassesPlan.Capture(usa,NativeJapaneseEggAuthoredClassReservation.Capture(usa),integration,a,b);
    }
    internal static byte[] Words(params uint[] words)
    {byte[] b=new byte[words.Length*4];for(int i=0;i<words.Length;i++)BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(i*4),words[i]);return b;}
    // Independent observed predicate/helper instructions, not production emission.
    internal static NativeJapaneseEggPickupPredicateSource Predicate() => NativeJapaneseEggPickupPredicateSource.Fixture(
        Words(0x2664000C,0x0C005BDD,0x03C02821,0x284201F4,0x10400C0B,0,
            0x86630038,0x8E620014,0x3C048008,0x8C8428A0,0x00431823,0x00641023,0x18400005,0x284201F4,
            0x14400007,0,0x080243E9,0,0x00831023,0x284201F4,0x10400BFB,0,0x3C028008,0x8C422A90,
            0,0x30422000,0x14400BF5,0x2402000F,0x3C038008,0x8C632910,0,0x14620006,0x02602821,
            0x3C028008,0x8C422914,0,0x14400BEB,0),
        Words(0x8C810000,0x8C820004,0x8CA60000,0x8CA70004,0x00C13022,0x04C10002,
            0x00E23822,0x00063022,0x04E10002,0,0x00073822,0x00C70822,0x04210006,0,0x00060840,
            0x00C13020,0x000630C2,0x03E00008,0x00C71020,0x00070840,0x00E13820,0x000738C2,0x03E00008,0x00C71020));
    // Manual ABI fixture remains independent of any production initializer.
    internal static byte[] Ledger(DetachedCollectibleSave save,int slot,uint generation=0)
    {
        var m=save.Manifest;int raw=(m.LifetimeCount+7)/8,aligned=(raw+15)&~15;
        byte[] b=Enumerable.Repeat((byte)0xA5,0x200).ToArray();b.AsSpan(0x10,0x80+aligned*2).Clear();
        U32(b,0x10,0x524C4353);U16(b,0x14,1);b[0x16]=1;b[0x17]=(byte)slot;b[0x18]=1;
        U16(b,0x1A,0x2345);m.SerializeCanonical().AsSpan(0x10,16).CopyTo(b.AsSpan(0x20));
        Convert.FromHexString(m.Sha256).CopyTo(b,0x30);b.AsSpan(0x50,0x20).Fill(0x5A);
        U32(b,0x70,m.Revision);U16(b,0x74,m.LifetimeCount);U16(b,0x76,raw);U16(b,0x78,m.ActiveCount);
        U16(b,0x7A,save.EarnedActiveInstances(slot).Count);U32(b,0x7C,generation);U32(b,0x80,123);U32(b,0x8C,456);
        save.CopySlotBits(slot).CopyTo(b,0x90);
        foreach(var entry in m.Entries.Where(e=>!e.Retired))b[0x90+aligned+entry.BitIndex/8]|=(byte)(1<<(entry.BitIndex%8));
        return b;
    }
    internal static void U16(byte[] b,int at,int value)=>BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(at),(ushort)value);
    internal static void U32(byte[] b,int at,uint value)=>BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(at),value);
}
