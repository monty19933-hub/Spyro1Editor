using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.Core.Universal;

public sealed class UniversalWhirlwindSoundBinding
{
    internal UniversalWhirlwindSoundBinding(string source, uint pc, byte descriptor, string overlay,
        uint update, uint init, uint copy, UniversalSoundReport report)
    { SourceLevelKey=source; ReadPc=pc; DestinationDescriptor=descriptor; OverlaySha256=overlay;
      Update=update; Init=init; CopyBuffer=copy; SoundImport=report; }
    public string SourceLevelKey { get; }
    public uint ReadPc { get; }
    public byte DestinationDescriptor { get; }
    public UniversalSoundReport SoundImport { get; }
    internal string OverlaySha256 { get; }
    internal uint Update { get; }
    internal uint Init { get; }
    internal uint CopyBuffer { get; }
}

/// <summary>Whirlwind global sound selector54 is a sound-map byte, not an actor class.
/// Import its exact native looping sample without changing the destination's global map,
/// and bind only its copied call argument to the resulting physical descriptor.</summary>
public static class UniversalWhirlwindSoundDependency
{
    private const string SampleHash="b2c9e6602fb72fb94c70e066910c80df280a4c9b466be4472d377c66e4248afb";
    private const string DescriptorRest="01000000320000080000000000000000";
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void RequireExecutable(byte[] executable)
    {
        if(executable.Length!=0x66000)throw new InvalidDataException("Whirlwind sound dependency requires the complete native executable owner.");
        foreach(var proof in new[]{(Offset:0x34F0,Length:0x68,Hash:"ecd95b895b0f1dd85b2aed1c926a6e5e818ac3c41ddaf38565e34459808f89a0"),
            (Offset:0x46278,Length:0x82C,Hash:"d3a63784c8dc509583c63499ccb7ad493ba8bf4af0aaeb4a6bd9fa5aa851a06d"),
            (Offset:0x475C4,Length:0x108,Hash:"3113f1c0a2eda190b6c44686ac8f613d2b0233461ee4e271e49e4cb03874e703")})
            if(Convert.ToHexStringLower(SHA256.HashData(executable.AsSpan(proof.Offset,proof.Length)))!=proof.Hash)
                throw new InvalidDataException("Whirlwind native sound-map producer or descriptor consumer changed.");
    }
    public static UniversalWhirlwindSoundBinding Import(string sourceKey, UniversalLevelData sourceData,
        UniversalOverlay sourceOverlay, UniversalLevelData destination, byte[] nativeExecutable)
    {
        _=UniversalWhirlwindIndexedRows.ProveSource(sourceKey,sourceData,sourceOverlay);RequireExecutable(nativeExecutable);
        var sw=UniversalDispatchAnalysis.FindUpdateSwitch(sourceOverlay);
        var trace=UniversalDispatchAnalysis.Trace(sourceOverlay,sw.TreeStart,0x12C,sw.Seeds,sw.ClassRegister);
        var owned=UniversalDispatchAnalysis.Closure(sourceOverlay,sw.Function,[trace.Stop],new HashSet<uint>{sw.TreeStart}).Instructions;
        var sites=owned.Where(pc=>sourceOverlay.Word(pc)==0x90440036).ToArray();
        if(sites.Length!=1)throw new InvalidDataException("Whirlwind source must own exactly one global54 sound selector read.");
        uint pc=sites[0];
        if(sourceOverlay.Word(pc-12)!=0x3C028007||sourceOverlay.Word(pc-8)!=0x8C4261D4||
            sourceOverlay.Word(pc-4)!=0x24060008||sourceOverlay.Word(pc+4)!=0x0C01569E||
            UniversalMips.Op(sourceOverlay.Word(pc+8))!=9||UniversalMips.Rt(sourceOverlay.Word(pc+8))!=7||UniversalMips.SImm(sourceOverlay.Word(pc+8))!=0x54)
            throw new InvalidDataException("Whirlwind global selector argument/consumer owner differs from native source.");
        var sound=new NativeLevelSoundLayout(sourceData.Bytes);byte index=sound.Sound.Map[0x36];
        if(index>=sound.Sound.Descriptors.Count)throw new InvalidDataException("Whirlwind original sound selector exceeds its descriptor owner.");
        var desc=sound.Sound.Descriptors[index];
        if(desc.Sample.Sha256!=SampleHash||desc.Sample.ByteLength!=9744||
            Convert.ToHexString(desc.CopyDescriptor().AsSpan(4))!=DescriptorRest)
            throw new InvalidDataException("Whirlwind original looping sound record differs from its certified native descriptor/sample.");
        byte[] originalMap=new NativeLevelSoundLayout(destination.Bytes).Sound.Map.ToArray();
        byte[] oneSlot=Enumerable.Repeat((byte)0xFF,20).ToArray();oneSlot[0]=1;oneSlot[1]=oneSlot[2]=oneSlot[3]=0;oneSlot[4]=index;
        var report=UniversalAssetImport.ImportSounds(oneSlot,sourceData,destination);
        var binding=new UniversalWhirlwindSoundBinding(sourceKey,pc,oneSlot[4],Hash(sourceOverlay.Bytes),
            sourceOverlay.Update,sourceOverlay.Init,sourceOverlay.CopyBuffer,report);
        RequireDestination(binding,destination);
        if(!new NativeLevelSoundLayout(destination.Bytes).Sound.Map.SequenceEqual(originalMap))
            throw new InvalidDataException("Whirlwind sound import modified another native global selector.");
        return binding;
    }
    public static void RequireDestination(UniversalWhirlwindSoundBinding binding,UniversalLevelData destination)
    {
        var sound=new NativeLevelSoundLayout(destination.Bytes).Sound;
        if(binding.DestinationDescriptor>=sound.Descriptors.Count)throw new InvalidDataException("Whirlwind destination sound descriptor is absent.");
        var desc=sound.Descriptors[binding.DestinationDescriptor];
        if(desc.Sample.Sha256!=SampleHash||desc.Sample.ByteLength!=9744||
            Convert.ToHexString(desc.CopyDescriptor().AsSpan(4))!=DescriptorRest)
            throw new InvalidDataException("Whirlwind destination descriptor/sample does not preserve its original looping sound.");
    }
    internal static IReadOnlyList<uint> Patch(UniversalAsmPart part, UniversalOverlay source,
        byte[] executable, UniversalWhirlwindSoundBinding binding)
    {
        RequireExecutable(executable);
        if(Hash(source.Bytes)!=binding.OverlaySha256||source.Update!=binding.Update||source.Init!=binding.Init||source.CopyBuffer!=binding.CopyBuffer)
            throw new InvalidDataException("Whirlwind copied sound packet lacks its source callback/overlay owner.");
        uint read=binding.ReadPc,start=read-28,end=read+8;
        var reads=Enumerable.Range(0,part.Items.Count).Where(i=>(part.Items[i].SourcePc??part.Items[i].Orig)==read).ToArray();
        if(reads.Length==0)throw new InvalidDataException("Whirlwind copied global sound selector is missing.");
        foreach(int index in reads)
        for(int n=-7;n<=2;n++)
        {
            int at=index+n;uint pc=(uint)((long)read+n*4);
            if(at<0||at>=part.Items.Count)throw new InvalidDataException("Whirlwind sound packet is truncated.");
            var item=part.Items[at];uint word=source.Word(pc);
            var expected=UniversalMips.IsPcRelative(word)?UniversalFixKind.Branch:UniversalMips.Op(word)==2?UniversalFixKind.Jump:UniversalFixKind.None;
            if((item.SourcePc??item.Orig)!=pc||item.Word!=word||item.Fix.Kind!=expected||
                (expected is UniversalFixKind.Branch or UniversalFixKind.Jump&&(item.Fix.Orig!=UniversalMips.BranchTarget(word,pc)||item.Fix.Label!=null)))
                throw new InvalidDataException("Whirlwind copied sound packet changed its instructions, adjacency or exact control targets.");
        }
        foreach(var item in part.Items)
        {
            if(item.Fix.Kind is not(UniversalFixKind.Branch or UniversalFixKind.Jump)||item.Fix.Orig<=start||item.Fix.Orig>end)continue;
            uint? pc=item.SourcePc??item.Orig;
            // Original actor-state branches may enter before the global-map load.
            // Preserve only their exact authenticated native word and target;
            // the sound-load/call interior accepts no new or external entry.
            if(pc==null||!source.Contains(pc.Value)||item.Word!=source.Word(pc.Value)||
                item.Fix.Orig!=UniversalMips.BranchTarget(source.Word(pc.Value),pc.Value)||
                (item.Fix.Kind==UniversalFixKind.Branch?!UniversalMips.IsPcRelative(source.Word(pc.Value)):UniversalMips.Op(source.Word(pc.Value))!=2)||
                (item.Fix.Orig>read-12&&(pc<start||pc>end)))
                throw new InvalidDataException("Whirlwind sound packet has an unowned interior entry.");
        }
        if(part.Tables.Values.SelectMany(x=>x).Any(pc=>pc>start&&pc<=end))throw new InvalidDataException("Whirlwind sound packet has an unowned table entry.");
        var labels=part.Items.Where(i=>(i.SourcePc??i.Orig)>start&&(i.SourcePc??i.Orig)<=end&&i.Label!=null).Select(i=>i.Label).ToHashSet();
        if(part.Items.Any(i=>i.Fix.Label!=null&&labels.Contains(i.Fix.Label)))throw new InvalidDataException("Whirlwind sound packet has an unowned label entry.");
        foreach(int index in reads)part.Items[index]=part.Items[index] with{Word=UniversalMips.EncodeI(9,0,4,binding.DestinationDescriptor),ClassReadOwned=true};
        return [read];
    }
}
