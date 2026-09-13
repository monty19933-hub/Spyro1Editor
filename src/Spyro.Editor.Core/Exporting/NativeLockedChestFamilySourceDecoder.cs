using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Exact original-byte inputs and private schemas for the native Peace key/chest
/// family. Original Town support reads are evidence inputs, not destination
/// residency. No graph, final domain, relocation, image certificate or import
/// authority is issued here. The destination producer must close those owners.
/// </summary>
internal sealed class NativeLockedChestFamilySource
{
    private const string PackageSha256="8edc9e9ac1e5fb1a92224d2f5f8ca541371940c1ae3f9873c02f6b802d6e22e7";
    private static readonly int[] OwnerLengths=[0x29E800,0x12800,0x2E2000,0xF800,0x66000];
    private static readonly string[] OwnerHashes=[
        "c140bd422471c66cd57bd85b29912819f798f5b080779153674714889306c9d3",
        "299f4822489c48736c9f46bb9556e82952fc1693f5f9f42bc2e6415c1908aebc",
        "7ddbf6d9a6ee6c0f64c8564a89e374ca0ca234ef608ec812aae68e8176de1dc0",
        "9bc923cc8d27703537b81b01f51fde351e58aabee87d38a8afc630f1031878a5",
        "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998"];
    private readonly NativeAddressSpace[] _domains;
    private readonly Dictionary<NativeAddressSpace,byte[]> _originals;

    private NativeLockedChestFamilySource(NativeAddressSpace[] domains,byte[][] originals)
    {
        _domains=domains.ToArray();
        _originals=domains.Select((domain,i)=>(domain,bytes:originals[i])).ToDictionary(p=>p.domain,p=>p.bytes);
        var peace=domains[0];var donorOverlay=domains[1];var town=domains[2];var townOverlay=domains[3];var exe=domains[4];
        NativeMobyDependencyRead Read(NativeAddressSpace domain,int at,int length,string id="native")=>
            new(id,new(domain,at,length),Hash(_originals[domain].AsSpan(at,length)));
        PackageRead=Read(peace,0x1B3978,0x1008);
        if(PackageRead.ExpectedSha256!=PackageSha256||
            Word(_originals[peace],0x1B397C)!=0xFFFFFF2F)
            throw new InvalidDataException("Original native chest package or local sound selector changed.");
        HandlerReads=Array.AsReadOnly(new[]{Read(donorOverlay,0x919C,0x30,"setup"),
            Read(donorOverlay,0x9330,0x70C,"state"),Read(donorOverlay,0x3B64,0x434,"reward")});

        Dictionary<string,IReadOnlyList<NativeMobyDependencyRead>> inputs=new(StringComparer.Ordinal);
        void Input(string id,params NativeMobyDependencyRead[] reads)=>inputs.Add(id,Array.AsReadOnly(reads.ToArray()));
        void Single(string id,NativeAddressSpace domain,int at,int length)=>Input(id,Read(domain,at,length));
        // Preserve the original resource read IDs. Equivalence and destination
        // allocation remain in the Town consumer, including every target guard.
        foreach(var item in new[]{(0x28D1C,0xA0),(0x3BE0,0x26C),(0x34F0,0x68),(0x5FC0,0x98),(0x46278,0x82C)})
            Single("sound.native-routine."+item.Item1,exe,item.Item1,item.Item2);
        foreach(var sound in new[]{
            ("sound.key",0x160BC8,0x17286C,0x8E100,0x8E100,0x3C40),
            ("sound.reward",0x160D30,0x1729D4,0xAF2E0,0xAF2E0,0xB60),
            ("sound.chest-local54",0x160E20,0x172B50,0xC1B00,0xC5420,0x1430)})
        {
            Single(sound.Item1+".descriptor",town,sound.Item3,20);
            Single(sound.Item1+".sample",town,sound.Item5,sound.Item6);
            Input(sound.Item1+".equivalence",Read(peace,sound.Item2,20,"donor-descriptor"),
                Read(town,sound.Item3,20,"target-descriptor"),Read(peace,sound.Item4,sound.Item6,"donor-sample"),
                Read(town,sound.Item5,sound.Item6,"target-sample"));
        }
        foreach(int selector in new[]{0,0x37})Single("sound.map."+selector,town,0x172614+selector,1);
        Single("key.native-handler",townOverlay,0x9114,0x160);
        Single("key.special-renderer-and-global-routines",exe,0,0x1E034);
        Single("effects.native-registration",town,0x50,0x94);
        foreach(var debris in new[]{(0x135,0x1BF8E0,0x1CC804,0x224),(0x136,0x1BFB04,0x1CCA28,0x1D4),(0x137,0x1BFCD8,0x1CCBFC,0x2D8)})
        {
            string id="effects.native-debris."+debris.Item1;
            Single(id,town,debris.Item3,debris.Item4);
            Input(id+".equivalence",Read(peace,debris.Item2,debris.Item4,"donor"),Read(town,debris.Item3,debris.Item4,"target"));
        }
        for(int y=0;y<32;y++)Single("effects.native-debris-pixels."+y,town,0x58A70+y*0x400,16);
        Single("effects.native-debris-clut",town,0x78AA0,32);
        foreach(var item in new[]{(0xC86C,0xB7C),(0xE530,0xF64),(0x38C,0x144)})
            Single("effects.native-overlay."+item.Item1,townOverlay,item.Item1,item.Item2);
        foreach(var item in new[]{(0x4AC70,0x30),(0x4B0A0,0x90),(0x1BD8,4),(0x5ECE0,0x48)})
            Single("effects.native-executable."+item.Item1,exe,item.Item1,item.Item2);
        Single("key.particle-descriptor",town,0x1725C8,12);
        for(int y=0;y<32;y++)Single("key.particle-pixels."+y,town,0x78A80+y*0x400,16);
        Single("key.particle-clut",town,0x7BB40,32);
        var textureRegions=new[]{(0xE,192,192,0x4A2A),(0xE,192,224,0x4C2A),(0xF,0,160,0x5C2A),(0xF,0,192,0x5E2A)};
        for(int t=0;t<textureRegions.Length;t++)
        {
            var region=textureRegions[t];
            int[] pixels=TownSquareNativeLockedChestCandidateExporter.BuildTileOffsets((ushort)region.Item1,region.Item2,region.Item3);
            for(int y=0;y<32;y++)Single($"texture.{t}.{y}",peace,0x800+pixels[y*16],16);
            int palette=TownSquareNativeLockedChestCandidateExporter.BuildPaletteOffsets((ushort)region.Item4)[0];
            Single($"texture.{t}.32",peace,0x800+palette,32);
        }
        ResourceInputs=new ReadOnlyDictionary<string,IReadOnlyList<NativeMobyDependencyRead>>(inputs);

        byte[] donor=_originals[peace];
        int[] indices=[78,79,118,119,120,121,122];
        int[] offsets=[0xEA94,0xEA98,0xEF9C,0xEFC4,0xEFEC,0xF014,0xF03C];
        int[] lengths=[4,0x18,0x28,0x28,0x28,0x28,0x28];
        int fixupCount=checked((int)Word(donor,0x1C2800+0x11234));
        if(fixupCount<=0||0x11238L+fixupCount*4L>0x11800)
            throw new InvalidDataException("Native donor fixup table changed.");
        var fixups=Enumerable.Range(0,fixupCount).Select(i=>Word(donor,0x1C2800+0x11238+i*4)).ToArray();
        List<NativeMobyDependencyPrivateSchema> rows=[];
        for(int i=0;i<indices.Length;i++)
        {
            if(!fixups.Contains((uint)(0x8260+indices[i]*0x58))||fixups.Any(f=>f>=offsets[i]&&f<offsets[i]+lengths[i]))
                throw new InvalidDataException("Native key/chest/reward properties have an unknown internal pointer obligation.");
            ushort cls=(ushort)(i==0?0xAD:i==1?0xAE:0xD);
            int rowOffset=0x1CAA60+indices[i]*0x58;
            if(Word(donor,rowOffset)!=offsets[i]||BinaryPrimitives.ReadUInt16LittleEndian(donor.AsSpan(rowOffset+0x36))!=cls||
                (i>0&&Word(donor,0x1C2800+offsets[i])!=indices[i==1?0:1]))
                throw new InvalidDataException("Original key/chest/reward class, properties pointer or owner link changed.");
            rows.Add(new(i==0?"key":i==1?"chest":"reward."+(i-1),indices[i],cls,
                Read(peace,rowOffset,0x58),Read(peace,0x1C2800+offsets[i],lengths[i]),0x1C2800,
                Array.AsReadOnly<NativeMobyDependencyOwnerField>(i==0?[]:[new(0,i==1?0:1)]),i!=0));
        }
        PrivateRows=Array.AsReadOnly(rows.ToArray());
        var keyConsumer=inputs["key.native-handler"].Single();var stateConsumer=HandlerReads[1];
        MutableGlobals=Array.AsReadOnly(new[]{
            new NativeMobyDependencyMutableGlobal("key-state",exe,0x80075830,4,Array.AsReadOnly(new[]{keyConsumer,stateConsumer})),
            new NativeMobyDependencyMutableGlobal("tracked-chest-gem",exe,0x80075758,4,
                Array.AsReadOnly(new[]{stateConsumer,Read(exe,0x2C21C,0x14,"award-tracked-gem")}))});
        RequireRetainedOwners();
    }

    internal IReadOnlyList<NativeMobyDependencyPrivateSchema> PrivateRows{get;}
    internal IReadOnlyList<NativeMobyDependencyMutableGlobal> MutableGlobals{get;}
    internal NativeMobyDependencyRead PackageRead{get;}
    internal IReadOnlyList<NativeMobyDependencyRead> HandlerReads{get;}
    internal IReadOnlyDictionary<string,IReadOnlyList<NativeMobyDependencyRead>> ResourceInputs{get;}
    internal byte[] CopyPackage()=>ReadSource(PackageRead.Extent);
    internal byte[] ReadSource(NativeByteExtent extent)
    {
        ArgumentNullException.ThrowIfNull(extent);
        if(!_originals.TryGetValue(extent.Space,out var bytes))
            throw new InvalidDataException("Locked-chest source read requires its actual retained Original address domain.");
        return bytes.AsSpan(checked((int)extent.Offset),checked((int)extent.ByteLength)).ToArray();
    }
    internal void RequireRetainedOwners()
    {
        for(int i=0;i<_domains.Length;i++)
            if(_originals[_domains[i]].Length!=OwnerLengths[i]||Hash(_originals[_domains[i]])!=OwnerHashes[i])
                throw new InvalidDataException("Complete locked-chest original source owner changed.");
        var reads=PrivateRows.SelectMany(r=>new[]{r.Row,r.Properties}).Append(PackageRead).Concat(HandlerReads)
            .Concat(ResourceInputs.Values.SelectMany(r=>r)).Concat(MutableGlobals.SelectMany(g=>g.Consumers));
        foreach(var read in reads)
            if(read.Extent.Space.Revision!=NativeAddressRevision.Original||Hash(ReadSource(read.Extent))!=read.ExpectedSha256)
                throw new InvalidDataException("Locked-chest retained schema/resource source read changed.");
    }
    internal static NativeLockedChestFamilySource DecodeOriginal(NativeAddressSpace peaceData,NativeAddressSpace peaceOverlay,
        NativeAddressSpace townData,NativeAddressSpace townOverlay,NativeAddressSpace executable,Func<NativeByteExtent,byte[]> readSource)
    {
        ArgumentNullException.ThrowIfNull(readSource);
        NativeAddressSpace[] domains=[peaceData,peaceOverlay,townData,townOverlay,executable];
        for(int i=0;i<domains.Length;i++)
        {
            ArgumentNullException.ThrowIfNull(domains[i]);
            if(domains[i].Kind!=(i==4?NativeAddressKind.ExecutableFile:NativeAddressKind.LevelData)||
                domains[i].Revision!=NativeAddressRevision.Original||domains[i].ByteLength!=OwnerLengths[i])
                throw new InvalidDataException("Locked-chest decoding requires all five complete Original source domains.");
        }
        if(domains.Distinct().Count()!=5)throw new InvalidDataException("Locked-chest source roles cannot alias address domains.");
        byte[][] originals=new byte[domains.Length][];
        for(int i=0;i<domains.Length;i++)
        {
            byte[] bytes=readSource(new(domains[i],0,domains[i].ByteLength))?.ToArray()??
                throw new InvalidDataException("Locked-chest original source read returned no bytes.");
            if(bytes.Length!=OwnerLengths[i]||Hash(bytes)!=OwnerHashes[i])
                throw new InvalidDataException("Complete locked-chest original source preimage changed.");
            originals[i]=bytes;
        }
        return new(domains,originals);
    }
    private static uint Word(ReadOnlySpan<byte> bytes,int offset)=>BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
}
