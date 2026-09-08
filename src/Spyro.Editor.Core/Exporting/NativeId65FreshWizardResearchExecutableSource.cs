using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Music;
using Spyro.Editor.Core.Scene;

[assembly: InternalsVisibleTo("Spyro.Editor.NativeId65FreshWizardExecutableSmoke")]

namespace Spyro.Editor.Core.Exporting;

/// <summary>Private actual-Body issuer; neutral bytes/metadata cannot create it.</summary>
internal sealed class NativeId65FreshWizardResearchExecutableSource
{
    private NativeId65FreshWizardResearchExecutableSource(NativeId65FreshWizardResearchBodyPlan body,byte[] original,byte[] town)
    {
        RequireBody(body);var host=body.HostAncestor;var scene=host.SceneAncestor;
        if(!original.AsSpan().SequenceEqual(scene.CompilationAncestor.FreshResourceAncestor.ResourceChainAncestor.Resources.Surface.CopySourceExecutable()))
            throw new InvalidDataException("Executable source is not the actual retained Compilation source.");
        NativeId65FreshWizardExecutableLayout.ValidateSourceWindows(original,town);
        byte[] sceneBytes=scene.CopyScene();scene.RequireExactScene(sceneBytes);
        var patch=host.InitializerPatch;var requirement=host.RequiredInitializerDispatch;
        if(patch.Offset!=0x4B098||patch.RuntimePc!=0x8005A898||patch.ByteLength!=0x98||
            requirement.ExecutableOffset!=0x1CA8||requirement.SourceWord!=0x8005B6E0||requirement.RequiredWord!=0x8005A898)
            throw new InvalidDataException("Actual Host initializer/table requirement escaped its fixed source owner.");
        Body=body;
        Layout=NativeId65FreshWizardExecutableLayout.Compile(original,patch.CopyBeforeBytes(),patch.CopyAfterBytes(),
            sceneBytes,scene.CompilationAncestor.Intent.MusicTrackId);
        Evidence=Array.AsReadOnly(NativeId65FreshWizardExecutableLayout.SourceWindows.Concat(new[]{
            new NativeId65EngineEvidence("actual-issued-P1-static-census-row","actual-issued-scene",0xD4,0x58,Layout.Census.SceneRowSha256),
            new("actual-issued-Host-initializer","actual-issued-Host",patch.Offset,patch.ByteLength,patch.AfterSha256)}).ToArray());
    }
    internal NativeId65FreshWizardResearchBodyPlan Body{get;}
    internal NativeId65FreshWizardExecutableLayout Layout{get;}
    internal IReadOnlyList<NativeId65EngineEvidence> Evidence{get;}
    internal static void RequireBody(NativeId65FreshWizardResearchBodyPlan body)
    {
        ArgumentNullException.ThrowIfNull(body);body.RequireAncestry(body.HostAncestor);
        body.RequireExactOutputs(body.CopyLevelData(),body.CopyOverlay(),body.CopyInitializerPatch(),body.CopyTransferSourceImage());
        body.HostAncestor.SceneAncestor.ResourceAncestor.RequireCompilationProfile();
    }
    internal static NativeId65FreshWizardResearchExecutableSource Capture(FileStream image,NativeId65FreshWizardResearchBodyPlan body,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();RequireBody(body);var layout=DiscImage.DetectLayout(image);
        var wad=DiscImage.FindRootFileRecord(image,layout,n=>n.Equals("WAD.WAD",StringComparison.OrdinalIgnoreCase));
        var executable=DiscImage.FindRootFileRecord(image,layout,n=>n.Equals("SCUS_942.28",StringComparison.OrdinalIgnoreCase));
        if(layout.SectorSize!=2352||layout.UserOffset!=24||wad.Lba!=37||wad.Size!=0x6927000||executable.Lba!=53875||executable.Size!=0x66000)
            throw new InvalidDataException("Executable capture requires exact original physical source owners.");
        return new(body,Read(53875,0,0x66000),Read(37,0x118E800,0xF800));
        byte[] Read(int lba,long offset,int length)
        {token.ThrowIfCancellationRequested();byte[] bytes=DiscImage.ReadFileBytes(image,layout,lba,offset,length);if(bytes.Length!=length)throw new InvalidDataException("Incomplete executable source owner.");token.ThrowIfCancellationRequested();return bytes;}
    }
}

/// <summary>Neutral fixed sequential byte recipe; never a public source capability.</summary>
internal sealed class NativeId65FreshWizardExecutableLayout
{
    private readonly NativeId65FreshExecutablePlan _prior;
    private readonly byte[] _final;
    private static readonly NativeId65EngineEvidence[] Windows=[
        new("native-incoming-index-derivation","EXE",0x5DFC,0xAC,"2d0aaeee6f758724c5452abc6d53a5066e7072784f2f12e6e67687f241e0a34d"),
        new("native-yellow-value-selection","EXE",0x2C264,0x6C,"50301f8f6b36e773351ebfe0a4c101799286e12015381da101cb4ae581e9c95f"),
        new("native-treasure-target-consumer","EXE",0xC8CC,0x34,"f9504fc22bd7daca311dafd8843738ccda340eac012b575f115fe1e26a930b58"),
        new("complete-original-36-target-table","EXE",0x5FC38,0x48,"641867a8c3d0c8af833f4ce678cd27ff7520e7ddafa4cecfd19c7a388f625ee6"),
        new("source-Town-spawn-including-postallocator-prefix","WAD",0x118E800L+0xC86C,0xB7C,"3c8dcb5a5a28fd197ad04c9512fbe00519bee1097d62dd27d4461931f53bb225")];
    private NativeId65FreshWizardExecutableLayout(byte[] original,byte[] before,byte[] initializer,NativeFreshWizardStaticRewardCensus census,int track)
    {
        Census=census;_prior=NativeId65FreshExecutablePlan.Compile(original,track);byte[] current=_prior.CopyExecutable();
        // The old compiler owns this substitution. A second original-preimage
        // table patch would be incorrect, even though its desired word matches.
        if(Word(original,0x1CA8)!=0x8005B6E0||Word(current,0x1CA8)!=0x8005A898)
            throw new InvalidDataException("Existing fresh compiler must already own the exact ID65 table requirement.");
        var stages=_prior.SequentialStages.ToList();
        Stage("actual-Host-conditional-resident-initializer",0x4B098,before,initializer);
        HostLabMusicExecutableSha256=Hash(current);
        byte[] target=new byte[2];BinaryPrimitives.WriteUInt16LittleEndian(target,checked((ushort)census.StaticTreasureTarget));
        Stage("static-one-Wizard-slot35-treasure-target",census.TargetFileOffset,[0,0],target);
        byte[] prior=_prior.CopyExecutable();
        if(!current.AsSpan(0,0x4B098).SequenceEqual(prior.AsSpan(0,0x4B098))||
            !current.AsSpan(0x4B130,0x5FC7E-0x4B130).SequenceEqual(prior.AsSpan(0x4B130,0x5FC7E-0x4B130))||
            !current.AsSpan(0x5FC80).SequenceEqual(prior.AsSpan(0x5FC80)))
            throw new InvalidDataException("New executable stages changed an old compiler owner/complement.");
        byte[] inverse=current.ToArray();
        foreach(var stage in stages.TakeLast(2).Reverse())ApplySequential(inverse,stage.FileOffset,Convert.FromHexString(stage.AfterHex),Convert.FromHexString(stage.BeforeHex));
        if(!inverse.SequenceEqual(prior)||!_prior.RevertExact(inverse).SequenceEqual(original))
            throw new InvalidDataException("Complete executable stage inverse did not restore actual original source.");
        _final=current;ExecutableSha256=Hash(current);SequentialStages=stages.AsReadOnly();
        var folded=new List<NativeId65FreshExecutableWindow>();
        for(int at=0;at<original.Length;)
        {
            if(original[at]==current[at]){at++;continue;}
            int start=at++;while(at<original.Length&&original[at]!=current[at])at++;
            folded.Add(new("final-original-diff-"+folded.Count,start,Convert.ToHexString(original.AsSpan(start,at-start)),Convert.ToHexString(current.AsSpan(start,at-start))));
        }
        byte[] replay=original.ToArray();
        foreach(var stage in folded)ApplySequential(replay,stage.FileOffset,Convert.FromHexString(stage.BeforeHex),Convert.FromHexString(stage.AfterHex));
        if(!replay.SequenceEqual(current))throw new InvalidDataException("Folded executable fields lost their complete original complement.");
        FoldedWindows=folded.AsReadOnly();
        void Stage(string name,int offset,ReadOnlySpan<byte> old,ReadOnlySpan<byte> after)
        {ApplySequential(current,offset,old,after);stages.Add(new(name,offset,Convert.ToHexString(old),Convert.ToHexString(after)));}
    }
    internal static IReadOnlyList<NativeId65EngineEvidence> SourceWindows=>Array.AsReadOnly(Windows);
    internal bool SourceCertified=>false;
    internal int SelectedBuiltInTrack=>_prior.SelectedBuiltInTrack;
    internal string PriorFreshExecutableSha256=>_prior.FinalSha256;
    internal string HostLabMusicExecutableSha256{get;}
    internal string ExecutableSha256{get;}
    internal NativeFreshWizardStaticRewardCensus Census{get;}
    internal IReadOnlyList<NativeId65FreshExecutableWindow> SequentialStages{get;}
    internal IReadOnlyList<NativeId65FreshExecutableWindow> FoldedWindows{get;}
    internal byte[] CopyExecutable()=>_final.ToArray();
    internal void RequireExactExecutable(ReadOnlySpan<byte> bytes)
    {if(!bytes.SequenceEqual(_final))throw new InvalidDataException("Executable owner or untouched complement changed.");}
    internal byte[] RevertExact(ReadOnlySpan<byte> bytes)
    {
        RequireExactExecutable(bytes);byte[] inverse=bytes.ToArray();
        foreach(var stage in SequentialStages.TakeLast(2).Reverse())ApplySequential(inverse,stage.FileOffset,Convert.FromHexString(stage.AfterHex),Convert.FromHexString(stage.BeforeHex));
        return _prior.RevertExact(inverse);
    }
    internal static NativeId65FreshWizardExecutableLayout Compile(ReadOnlySpan<byte> original,ReadOnlySpan<byte> before,
        ReadOnlySpan<byte> initializer,ReadOnlySpan<byte> scene,int track)
    {
        if(MusicTrackCatalog.Find(track)?.IsSelectable!=true)throw new ArgumentOutOfRangeException(nameof(track),"Unknown or reserved native music track.");
        if(original.Length!=0x66000||before.Length!=0x98||initializer.Length!=0x98||scene.Length!=0x3000)
            throw new InvalidDataException("Only complete bounded original/Host/P1 executable inputs are admitted.");
        NativeId65FreshExecutablePlan.RequireOriginal(original);
        if(!before.SequenceEqual(original.Slice(0x4B098,0x98))||Hash(initializer)!="d7f0e7c9ed86467f0749db689b2bf1f1c4df27587eb357821ef8947d7f70466b")
            throw new InvalidDataException("Complete original initializer preimage or closed Host afterimage changed.");
        var census=StaticCensus(original,scene);
        return new(original.ToArray(),before.ToArray(),initializer.ToArray(),census,track);
    }
    internal static void ApplySequential(Span<byte> bytes,int offset,ReadOnlySpan<byte> before,ReadOnlySpan<byte> after)
    {
        if(before.Length<=0||before.Length!=after.Length||offset<0||offset>bytes.Length-before.Length||!bytes.Slice(offset,before.Length).SequenceEqual(before))
            throw new InvalidDataException("Executable stage failed its exact bounded sequential preimage.");
        after.CopyTo(bytes[offset..]);
    }
    internal static void ValidateSourceWindows(ReadOnlySpan<byte> original,ReadOnlySpan<byte> town)
    {
        NativeId65FreshExecutablePlan.RequireOriginal(original);
        if(town.Length!=0xF800||Hash(town)!="9bc923cc8d27703537b81b01f51fde351e58aabee87d38a8afc630f1031878a5")
            throw new InvalidDataException("Complete original Town executable evidence changed.");
        foreach(var window in Windows)
        {
            var owner=window.Domain=="EXE"?original:town;int at=checked((int)(window.Domain=="EXE"?window.Offset:window.Offset-0x118E800));
            if(Hash(owner.Slice(at,window.ByteLength))!=window.Sha256)throw new InvalidDataException("Native static census source evidence changed.");
        }
    }
    private static NativeFreshWizardStaticRewardCensus StaticCensus(ReadOnlySpan<byte> source,ReadOnlySpan<byte> scene)
    {
        if(Word(scene,0xCC)!=0xEB8||Word(scene,0xD0)!=1||Word(scene,0xD4)!=0xF88||Word(scene,0xF84)!=0x54||
            Word(scene,0xFD8)!=8||Word(scene,0xFDC)!=0||scene.Slice(0x12C,0xE58).ContainsAnyExcept((byte)0))
            throw new InvalidDataException("Static census needs the actual fixed one-row P1/N32 shape, not dynamic capacity as placed owners.");
        var row=scene.Slice(0xD4,0x58);
        if(BinaryPrimitives.ReadUInt16LittleEndian(row[0x36..])!=0x11B||row[0x3A]!=0x7F||row[0x53]!=0x56)
            throw new InvalidDataException("Static census is only the source kind7F Wizard with deterministic initial reward selector56.");
        // Complete original SHA is checked first. The pinned branch sequence
        // compares56, jumps to BAB4, and sets a0=10 in that jump's delay slot.
        // This is extraction of a fixed source rule, not executing an award.
        if(Word(source,0x2C290)!=0x24020056||Word(source,0x2C29C)!=0x14620003||
            Word(source,0x2C2A4)!=0x0800EEAD||Word(source,0x2C2A8)!=0x2404000A||
            Word(source,0x2C2B4)!=0x0C00EE15||Word(source,0x2C2B8)!=0x02002821)
            throw new InvalidDataException("Native yellow selector/value/link-argument-delay source changed.");
        const int slot=35;int value=(short)(Word(source,0x2C2A8)&0xFFFF),count=checked((int)Word(scene,0xD0));
        int target=checked(count*value),offset=0x5FC38+slot*2;
        if(target!=10||target>short.MaxValue||BinaryPrimitives.ReadUInt16LittleEndian(source[offset..])!=0)
            throw new InvalidDataException("Static target escaped original signed native slot35 width/preimage.");
        return new(count,0x11B,0x7F,0x56,value,target,slot,offset,0xD4,Hash(row));
    }
    private static uint Word(ReadOnlySpan<byte> bytes,int at)=>BinaryPrimitives.ReadUInt32LittleEndian(bytes[at..]);
    internal static string Hash(ReadOnlySpan<byte> bytes)=>NativeId65FreshExecutablePlan.Hash(bytes);
}
