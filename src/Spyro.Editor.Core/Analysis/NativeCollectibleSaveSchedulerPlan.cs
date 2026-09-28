using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.Core.Analysis;

/// <summary>
/// Closed detached reassignment of TWO existing typed Artisans padding owners.
/// No allocation grows, no EXE hook is installed, and no model-tail free code
/// is borrowed. The only native edge changed in this afterimage is the restored
/// particle JR RA/NOP. Source identity is not installation, coherent instruction
/// cache, interrupt exclusion, asynchronous retention or native save success.
/// </summary>
internal sealed class NativeCollectibleSaveSchedulerPlan
{
    internal const uint OverlayBase=0x8007AA38,HookPc=0x800873D8;
    private readonly byte[] before,after,executable,data;
    private NativeCollectibleSaveSchedulerPlan(NativeRegionalSourceSession source,NativeCollectibleSaveWorkspacePlan workspace,
        NativeJapaneseEggLifecycleOverlayPlan lifecycle)
    {
        lifecycle.RequireWorkspaceCurrent(workspace);workspace.RequireCurrent(workspace.Ancestry);
        workspace.Ancestry.RequireSourceComposition();
        if(workspace.Profile!=NativeCollectibleSaveWorkspacePlan.WorkspaceProfile.Controller||workspace.Controller is not{SourceCertified:true})
            throw new InvalidDataException("Scheduler requires the exact source-issued controller workspace.");
        byte[] original=source.ReadExecutableRange(0,source.ExecutableByteLength);
        NativeJapaneseEggMemoryReport.RequireSourceExecutable(original);
        if(!original.AsSpan().SequenceEqual(workspace.Ancestry.CopyOriginalExecutable())||
            !source.ReadWadEntry(9).AsSpan().SequenceEqual(workspace.Ancestry.Authored.Integration.CopyOriginalOverlay())||
            !source.ReadWadEntry(10).AsSpan().SequenceEqual(workspace.Ancestry.Authored.Integration.CopyOriginalLevelData()))
            throw new InvalidDataException("Scheduler ancestry differs from complete original EXE/overlay/data owners.");
        foreach(var span in Evidence)
            if(Hash(original.AsSpan(checked((int)(span.Pc-0x8000F800)),span.ByteLength))!=span.Sha256)
                throw new InvalidDataException("Scheduler native source seam changed.");
        Workspace=workspace;Lifecycle=lifecycle;before=lifecycle.CopyOverlay();executable=workspace.Ancestry.CopyExecutable();data=workspace.CopyLevelData();
        var authored=workspace.Ancestry.Authored.OverlayLayout.Blocks.Single(b=>b.Id=="alignment"&&b.IsPadding);
        var final=lifecycle.Layout.Blocks.Single(b=>b.Id=="alignment"&&b.IsPadding);
        if(authored.Owner.Offset!=0xE240||authored.Owner.ByteLength!=0x5C0||final.Owner.Offset!=0xFDF8||final.Owner.ByteLength!=0x208||
            before.Length!=0x10000||lifecycle.Layout.CopyAnchor!=0x8008AA38||
            !before.AsSpan(0xE240,0x5C0).SequenceEqual(workspace.Ancestry.Authored.CopyOverlay().AsSpan(0xE240,0x5C0))||
            before.AsSpan(0xE240,0x5C0).ContainsAnyExcept((byte)0)||before.AsSpan(0xFDF8,0x208).ContainsAnyExcept((byte)0)||
            BinaryPrimitives.ReadUInt32LittleEndian(before.AsSpan(0xC9A0))!=0x03E00008||
            BinaryPrimitives.ReadUInt32LittleEndian(before.AsSpan(0xC9A4))!=0)
            throw new InvalidDataException("Exact two typed padding owners and original restored particle epilogue required.");
        Kernel=NativeCollectibleSaveSchedulerKernel.Compile(workspace,lifecycle);
        FinalDomain=new(NativeAddressKind.LevelData,"save-scheduler:"+lifecycle.PlanSha256+":"+workspace.PlanSha256,NativeAddressRevision.Final,before.Length);
        MainOwner=new(FinalDomain,authored.Owner.Offset,authored.Owner.ByteLength);
        FinalOwner=new(FinalDomain,final.Owner.Offset,final.Owner.ByteLength);
        StateOwner=new(FinalDomain,0xFFC0,0x40);
        byte[] main=NativeOwnedOverlayMaterializer.Materialize(MainOwner,OverlayBase,
            [new("scheduler-main",MainOwner,Kernel.CopyMainBytes())],
            [new("scheduler-entry","scheduler-main",new(FinalDomain,MainOwner.Offset,4))],[]);
        var helper=new NativeByteExtent(FinalDomain,FinalOwner.Offset,FinalOwner.ByteLength-StateOwner.ByteLength);
        byte[] tail=NativeOwnedOverlayMaterializer.Materialize(FinalOwner,OverlayBase,
            [new("scheduler-helpers-table",helper,Kernel.CopyFinalBytes().AsSpan(0,0x1C8).ToArray()),new("scheduler-zero-state",StateOwner,new byte[0x40])],
            [new("scheduler-context","scheduler-helpers-table",new(FinalDomain,helper.Offset,4))],[]);
        after=before.ToArray();main.CopyTo(after,(int)MainOwner.Offset);tail.CopyTo(after,(int)FinalOwner.Offset);
        BinaryPrimitives.WriteUInt32LittleEndian(after.AsSpan(0xC9A0),0x08000000|((NativeCollectibleSaveSchedulerKernel.EntryPc>>2)&0x3FFFFFF));
        for(int i=0;i<after.Length;i++)
            if(!(i>=0xC9A0&&i<0xC9A8||i>=0xE240&&i<0xE800||i>=0xFDF8)&&after[i]!=before[i])
                throw new InvalidDataException("Scheduler modified an unassigned retained byte.");
        OverlaySha256=Hash(after);OriginalOverlaySha256=Hash(before);_=RevertOverlayExact(after);
    }
    internal static IReadOnlyList<NativeCollectibleSaveTailKernel.SourceSpan> Evidence{get;}=Array.AsReadOnly(new[]{
        new NativeCollectibleSaveTailKernel.SourceSpan(0x80031DF0,0x48,"6bbfb05642a44d3da883b9ce55534cdb54cd607b6ede178fc0ddb74e1e78528f"),
        new NativeCollectibleSaveTailKernel.SourceSpan(0x800320C0,0x14C,"ae97041649bb88d7d99fb83532950defa2316190fdf140e89faa0e3ec9a25471"),
        new NativeCollectibleSaveTailKernel.SourceSpan(0x800324B8,0x20,"891d7ae4ca4ffa8eff88d1634f7fcb03635d0119388bfc39e2edac786fbce07c"),
        new NativeCollectibleSaveTailKernel.SourceSpan(0x800662BC,0x60,"e23d1f4697a7870a5e1e16faef2d1f384818bd3771c69619b2388bdf1ef9fed9"),
        new NativeCollectibleSaveTailKernel.SourceSpan(0x800665B8,0x7C,"40b72a2bba8ce136e0b4fa5516b6c19bc54495fecd5339e04c9de30d7e011a32")});
    internal NativeCollectibleSaveWorkspacePlan Workspace{get;}
    internal NativeJapaneseEggLifecycleOverlayPlan Lifecycle{get;}
    internal NativeCollectibleSaveSchedulerKernel Kernel{get;}
    internal NativeAddressSpace FinalDomain{get;}
    internal NativeByteExtent MainOwner{get;}
    internal NativeByteExtent FinalOwner{get;}
    internal NativeByteExtent StateOwner{get;}
    internal string OverlaySha256{get;}
    internal string OriginalOverlaySha256{get;}
    internal bool SourceCertified=>true;
    internal bool HooksEmittedInDetachedOverlay=>true;
    internal bool HooksInstalled=>false;
    internal bool RuntimeVerified=>false;
    internal bool CardWrites=>false;
    internal bool NormalCreateBinApproved=>false;
    internal bool PublicationApproved=>false;
    internal bool CacheCoherenceProven=>false;
    internal bool IrqExclusionProven=>false;
    internal bool NativeSessionAuthenticated=>false;
    internal bool NativeFullStackProven=>false;
    internal bool NativeAsyncRetentionProven=>false;
    internal byte[] CopyOverlay()=>after.ToArray();
    internal byte[] CopyExecutable()=>executable.ToArray();
    internal byte[] CopyLevelData()=>data.ToArray();
    internal byte[] RevertOverlayExact(ReadOnlySpan<byte> value)
    {if(!value.SequenceEqual(after))throw new InvalidDataException("Scheduler inverse requires the complete exact afterimage.");return before.ToArray();}
    internal void RequireCurrent(NativeCollectibleSaveWorkspacePlan workspace,NativeJapaneseEggLifecycleOverlayPlan lifecycle)
    {
        if(!ReferenceEquals(Workspace,workspace)||!ReferenceEquals(Lifecycle,lifecycle))throw new InvalidDataException("Exact scheduler issuer instances required.");
        lifecycle.RequireWorkspaceCurrent(workspace);
        if(!lifecycle.CopyOverlay().AsSpan().SequenceEqual(before)||!workspace.CopyLevelData().AsSpan().SequenceEqual(data))throw new InvalidDataException("Scheduler complete ancestry changed.");
    }
    internal static NativeCollectibleSaveSchedulerPlan Capture(NativeRegionalSourceSession source,NativeCollectibleSaveWorkspacePlan workspace,NativeJapaneseEggLifecycleOverlayPlan lifecycle)
    {
        if(source is null||workspace is null||lifecycle is null)throw new InvalidDataException("Source, exact workspace and exact recompiled lifecycle required.");
        try{return new(source,workspace,lifecycle);}finally{source.VerifyUnchanged();}
    }
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
}
