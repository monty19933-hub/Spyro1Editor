using System.Buffers.Binary;
using System.Text.Json;
using Spyro.Editor.Core.Universal;

static class DestinationGemColourCases
{
    static readonly uint Start = UniversalOverlay.Base + 0x100;
    static UniversalOverlay Overlay(uint[] words)
    {
        byte[] bytes = new byte[0x400];
        for(int i=0;i<words.Length;i++) BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x100+i*4),words[i]);
        return new(bytes,new[]{UniversalExeBindings.UpdateGlobal,UniversalExeBindings.InitGlobal,UniversalExeBindings.CopyBufferGlobal}.ToDictionary(g=>g,g=>new UniversalExeBinding(g,0,0,Start)));
    }
    static uint[] Words() => [UniversalMips.EncodeI(35,19,18,0),0,UniversalMips.EncodeI(35,18,2,0x10),0,
        UniversalMips.EncodeI(33,2,3,0x36),0,(3u<<16)|(2u<<11)|(2u<<6),UniversalMips.EncodeI(15,0,1,0x8007),
        (1u<<21)|(2u<<16)|(1u<<11)|0x21,UniversalMips.EncodeI(35,1,7,unchecked((short)0xE330)),
        UniversalMips.EncodeI(15,0,2,0x8007),0,UniversalMips.JrRa,0];
    static HashSet<uint> Code(uint[] w) => Enumerable.Range(0,w.Length).Select(i=>Start+(uint)i*4).ToHashSet();
    internal static void Free(Action<bool,string> check)
    {
        bool Refuses(Action a){try{a();return false;}catch(InvalidDataException){return true;}}
        var w=Words(); var site=UniversalDestinationGemColourAdapter.ProveLoads(Overlay(w),Code(w),[],19).Single();
        check(site.Pc==Start+24&&site.LoadPc==Start+36&&site.Role=="native-actor-property-linked-class","linked native sparkle color owner proven");
        foreach(var mutation in new[]{0,1,2,3,4,6,7,8,9,10})
        {
            var bad=w.ToArray();bad[mutation]=mutation switch{
                0=>UniversalMips.EncodeI(35,19,18,4),1=>UniversalMips.EncodeI(9,0,18,0),
                2=>UniversalMips.EncodeI(35,18,2,4),3=>UniversalMips.EncodeI(9,0,2,0),4=>UniversalMips.EncodeI(33,2,3,0x38),
                6=>(3u<<16)|(3u<<11)|(2u<<6),7=>UniversalMips.EncodeI(15,0,1,0x8008),
                8=>(1u<<21)|(4u<<16)|(1u<<11)|0x21,10=>UniversalMips.EncodeI(9,7,2,0),_=>UniversalMips.EncodeI(35,2,7,unchecked((short)0xE330))};
            check(Refuses(()=>UniversalDestinationGemColourAdapter.ProveLoads(Overlay(bad),Code(bad),[],19)),"malformed color owner refused "+mutation);
        }
        foreach(int index in new[]{3,8,29,1,7,31})
        {
            var bad=w.ToArray();bad[6]=(3u<<16)|((uint)index<<11)|(2u<<6);bad[8]=(1u<<21)|((uint)index<<16)|(1u<<11)|0x21;
            check(Refuses(()=>UniversalDestinationGemColourAdapter.ProveLoads(Overlay(bad),Code(bad),[],19)),"otherwise matching aliased index refused "+index);
        }
        foreach(int cls in new[]{8,29,7,31})
        {
            var bad=w.ToArray();bad[4]=UniversalMips.EncodeI(33,2,cls,0x36);bad[6]=((uint)cls<<16)|(2u<<11)|(2u<<6);
            check(Refuses(()=>UniversalDestinationGemColourAdapter.ProveLoads(Overlay(bad),Code(bad),[],19)),"otherwise matching scratch/stack/output class refused "+cls);
        }
        var late=w.ToArray();late[4]=0;late[5]=w[4];
        check(Refuses(()=>UniversalDestinationGemColourAdapter.ProveLoads(Overlay(late),Code(late),[],19)),"uncommitted class load refused");
        var call=w.ToArray();call[2]=UniversalMips.EncodeJ(3,0x80010000);call[3]=0;
        check(Refuses(()=>UniversalDestinationGemColourAdapter.ProveLoads(Overlay(call),Code(call),[],19)),"call-clobbered linked actor provenance refused");
        var entered=w.Concat(new[]{UniversalMips.EncodeI(4,8,0,-10),0u}).ToArray();
        check(Refuses(()=>UniversalDestinationGemColourAdapter.ProveLoads(Overlay(entered),Code(entered),[],19)),"alternate jump-delay entry refused");
        var bypass=w.ToArray();bypass[3]=UniversalMips.EncodeI(4,8,0,2);bypass[4]=0;
        check(Refuses(()=>UniversalDestinationGemColourAdapter.ProveLoads(Overlay(bypass),Code(bypass),[],19)),"branch skipping linked class load refused");
        var maps=Enumerable.Range(0x53,5).Select(c=>new KeyValuePair<int,int>(c,0x1FD-(c-0x53))).ToArray();
        uint adapterBase=Start+0x100;
        var (adapter,labels)=UniversalBehaviorBundleBuilder.Assemble([UniversalDestinationGemColourAdapter.Build(maps,site,"colour")],adapterBase);
        byte[] combined=new byte[0x300];for(int i=0;i<w.Length;i++)BinaryPrimitives.WriteUInt32LittleEndian(combined.AsSpan(i*4),w[i]);adapter.CopyTo(combined,0x100);
        var patched=combined.ToArray();var metadata=new UniversalDestinationGemColourLoad(site.Pc,site.PatchWord,site.Delay,site.AddWord,site.LoadWord,site.LoadPc,site.ClassRegister,site.Role,labels["colour"]);
        byte[] native=new byte[0x400];combined.CopyTo(native,0x100);UniversalDestinationGemColourAdapter.Apply(native,[metadata]);native.AsSpan(0x100,0x300).CopyTo(patched);
        foreach(int offset in new[]{0x118,0x11C,0x120,0x124,0x128})
        {
            byte[] changed=combined.ToArray();byte[] changedOverlay=new byte[0x400];changed.CopyTo(changedOverlay,0x100);changedOverlay[offset]^=1;var before=changedOverlay.ToArray();
            check(Refuses(()=>UniversalDestinationGemColourAdapter.Apply(changedOverlay,[metadata]))&&changedOverlay.SequenceEqual(before),"changed native producer refused before publication "+offset);
        }
        for(int cls=0;cls<512;cls++)
        {
            StrictMachine Vm(byte[] code)
            {
                var vm=new StrictMachine(code,Start);for(int r=1;r<32;r++)vm.R[r]=(uint)(0xB0000000+r*0x10201);
                vm.R[3]=(uint)cls;vm.R[29]=0x801F0000;vm.R[31]=0x80010000;
                for(int c=0;c<512;c++)vm.Set32(0x8006E330+(uint)c*4,0xCC000000+(uint)c);
                return vm;
            }
            var original=Vm(combined);var actual=Vm(patched);original.Run(site.Pc,site.LoadPc+8);actual.Run(site.Pc,site.LoadPc+8);
            int canonical=maps.FirstOrDefault(p=>p.Value==cls).Key;if(canonical==0)canonical=cls;
            check(actual.R[7]==0xCC000000+(uint)canonical,"mapped sparkle canonical color "+cls);
            for(int r=0;r<32;r++)if(r!=7)check(actual.R[r]==original.R[r],"exact original producer register including RA/SP/load delay "+r);
        }
    }
    internal static void Source(UniversalSourceLibrary library,Spyro.Editor.Core.Levels.LevelCatalog catalog,string output,Action<bool,string> check)
    {
        List<object> evidence=[];
        foreach(var level in catalog.Levels.Where(l=>l.SourceWadEntry>0))
        {
            var owner=library.Get(level.Key).Overlay;var loads=UniversalDestinationGemColourAdapter.Analyze(owner);
            UniversalDestinationGemCollectionAdapter.RequireCurrentOwner(owner,owner,[]);check(true,"native destination color census "+level.Key);
            foreach(var site in loads)
            foreach(uint pc in new[]{site.Pc,site.Pc+4,site.LoadPc-4,site.LoadPc,site.LoadPc+4})
            {
                var bad=owner.Bytes.ToArray();bad[(int)(pc-UniversalOverlay.Base)]^=1;bool refused=false;
                try{UniversalDestinationGemCollectionAdapter.RequireCurrentOwner(new(bad,owner.Bindings),owner,[]);}catch(InvalidDataException){refused=true;}
                check(refused,"native color producer full owner mutation refused "+level.Key);
            }
            evidence.Add(new{level.Key,Loads=loads});
        }
        File.WriteAllText(output,JsonSerializer.Serialize(evidence,new JsonSerializerOptions{WriteIndented=true}));
    }
}
