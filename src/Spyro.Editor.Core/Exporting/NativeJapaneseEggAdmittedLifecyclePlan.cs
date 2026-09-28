using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Source-only fixed two-identity/slot-zero admission plus guarded Artisans
/// lifecycle. A common complete original EXE and exact disjoint field merge
/// bind the phase-specific overlays. No publisher, card writer, GUI route,
/// runtime, all-transition lifetime or IRQ-exclusion authority is issued.
/// </summary>
internal sealed class NativeJapaneseEggAdmittedLifecyclePlan
{
    internal sealed record EntryOwner(int Index,int OriginalLength,int FinalLength,string OriginalSha256,string FinalSha256);
    private readonly byte[] original,executable,card,artisans,data;
    private NativeJapaneseEggAdmittedLifecyclePlan(NativeCollectibleCardAdmissionPlan admission,
        NativeJapaneseEggAuthoredClassesPlan authored,NativeJapaneseEggLifecycleOverlayPlan lifecycle)
    {
        var manifest=NativeCollectibleCardLoadResearchComposition.FixedManifest();
        admission.RequireSourceComposition();
        if(!authored.SourceCertified||!lifecycle.SourceCertified||lifecycle.LiveGuard is not{SourceBound:true}||
            lifecycle.Collector.OrdinarySlot!=0||lifecycle.Collector.ManifestSha256!=manifest.Sha256||
            !lifecycle.Binding.Rows.Select(r=>r.InstanceId).SequenceEqual(FixedIdentities().Select(r=>r.InstanceId))||
            !lifecycle.Binding.Rows.Select(r=>r.RowId).SequenceEqual(new[]{"egg-a","egg-b"}))
            throw new InvalidDataException("Admitted lifecycle requires actual fixed source-issued guarded owners.");
        NativeJapaneseEggArtisansPlacementPlan.RequireFixedRecipe(authored.Integration.Rows.Select(r=>r.Placement).ToArray());
        lifecycle.Binding.RequireCurrent(authored,manifest);lifecycle.Projection.RequireCurrent(authored);
        // Reuse the actual normal-load/reservation geometry and common-source
        // merger. Guard is a leaf and adds no nested frame to this60-byte proof.
        Startup=NativeJapaneseEggLifecycleStartupPlan.Create(authored,lifecycle,admission.Ancestry!.Bootstrap,manifest);
        original=admission.CopyOriginalExecutable();
        if(!authored.Integration.CopyOriginalExecutable().AsSpan().SequenceEqual(original)||
            !Startup.RevertToOriginalExecutableExact(Startup.CopyExecutable()).AsSpan().SequenceEqual(original))
            throw new InvalidDataException("Card and Artisans must descend from the same complete original EXE.");
        byte[] lifecycleExe=lifecycle.CopyExecutable();executable=admission.CopyExecutable();
        const int anchor=0x4AF18;
        if(!executable.AsSpan(anchor,8).SequenceEqual(original.AsSpan(anchor,8)))
            throw new InvalidDataException("Artisans anchor collides with an admission-owned EXE patch.");
        for(int i=0;i<original.Length;i++)
            if((i<anchor||i>=anchor+8)&&lifecycleExe[i]!=original[i])
                throw new InvalidDataException("Artisans EXE changed outside its disjoint final copy-anchor pair.");
        lifecycleExe.AsSpan(anchor,8).CopyTo(executable.AsSpan(anchor,8));
        card=admission.CopyOverlay();artisans=lifecycle.CopyOverlay();data=lifecycle.CopyLevelData();
        Admission=admission;Authored=authored;Lifecycle=lifecycle;
        EntryOwners=Array.AsReadOnly(new[]{Entry(2,admission.CopyOriginalOverlay(),card),
            Entry(9,authored.Integration.CopyOriginalOverlay(),artisans),Entry(10,authored.Integration.CopyOriginalLevelData(),data)});
        ExecutableSha256=Hash(executable);RequireSourceComposition();
    }
    internal NativeCollectibleCardAdmissionPlan Admission{get;}
    internal NativeJapaneseEggAuthoredClassesPlan Authored{get;}
    internal NativeJapaneseEggLifecycleOverlayPlan Lifecycle{get;}
    internal NativeJapaneseEggLifecycleStartupPlan Startup{get;}
    internal IReadOnlyList<EntryOwner> EntryOwners{get;}
    internal string ExecutableSha256{get;}
    internal bool SourceCertified=>true;
    internal bool ResearchOnly=>true;
    internal bool GuardedCollectionEmitted=>true;
    internal bool RuntimeVerified=>false;
    internal bool CardWrites=>false;
    internal bool NormalCreateBinApproved=>false;
    internal bool PublicationApproved=>false;
    internal bool LifetimeProven=>false;
    internal bool InterruptSafetyProven=>false;
    internal static NativeJapaneseEggAdmittedLifecyclePlan Compose(NativeCollectibleCardAdmissionPlan admission,
        NativeJapaneseEggAuthoredClassesPlan authored,NativeJapaneseEggLifecycleOverlayPlan lifecycle)
    {ArgumentNullException.ThrowIfNull(admission);ArgumentNullException.ThrowIfNull(authored);ArgumentNullException.ThrowIfNull(lifecycle);return new(admission,authored,lifecycle);}
    internal static NativeJapaneseEggAdmittedLifecyclePlan Capture(NativeRegionalSourceSession japan,NativeRegionalSourceSession usa)
    {
        ArgumentNullException.ThrowIfNull(japan);ArgumentNullException.ThrowIfNull(usa);
        try
        {
            var admission=NativeCollectibleCardAdmissionPlan.Capture(usa);
            var placement=NativeJapaneseEggArtisansPlacementPlan.Capture(usa);
            var integration=NativeJapaneseEggArtisansIntegrationPlan.Capture(japan,usa,placement.Placements);placement.RequireIntegration(integration);
            var colors=NativeJapaneseEggRgbEditSource.Capture(NativeJapaneseEggModelAssetPlan.Capture(japan,usa),usa);
            var a=colors.CreateAuthored(Enumerable.Repeat(0x332211u,70).ToArray(),Enumerable.Repeat(0x665544u,70).ToArray());
            var b=colors.CreateAuthored(Enumerable.Repeat(0xCCBBAAu,70).ToArray(),Enumerable.Repeat(0xFFEEDDu,70).ToArray());
            var authored=NativeJapaneseEggAuthoredClassesPlan.Capture(usa,NativeJapaneseEggAuthoredClassReservation.Capture(usa),integration,a,b);
            var lifecycle=NativeJapaneseEggLifecycleOverlayPlan.CaptureGuarded(japan,usa,authored,
                NativeCollectibleCardLoadResearchComposition.FixedManifest(),0,FixedIdentities());
            return new(admission,authored,lifecycle);
        }
        finally{try{japan.VerifyUnchanged();}finally{usa.VerifyUnchanged();}}
    }
    internal void RequireSourceComposition()
    {
        Admission.RequireSourceComposition();Lifecycle.Binding.RequireCurrent(Authored,NativeCollectibleCardLoadResearchComposition.FixedManifest());
        if(!RevertExecutableExact(executable).AsSpan().SequenceEqual(original)||
            !RevertCardOverlayExact(card).AsSpan().SequenceEqual(Admission.CopyOriginalOverlay())||
            !RevertArtisansOverlayExact(artisans).AsSpan().SequenceEqual(Authored.Integration.CopyOriginalOverlay())||
            !RevertLevelDataExact(data).AsSpan().SequenceEqual(Authored.Integration.CopyOriginalLevelData())||
            !Startup.FinalMemory.ModeledExtentsFit||Startup.LedgerStackMargin<0||Lifecycle.Layout.FinalByteLength>0x10000)
            throw new InvalidDataException("Admitted lifecycle complete source inverses or final guarded geometry changed.");
    }
    internal byte[] CopyOriginalExecutable()=>original.ToArray();
    internal byte[] CopyExecutable()=>executable.ToArray();
    internal byte[] CopyCardOverlay()=>card.ToArray();
    internal byte[] CopyArtisansOverlay()=>artisans.ToArray();
    internal byte[] CopyLevelData()=>data.ToArray();
    internal byte[] RevertExecutableExact(ReadOnlySpan<byte> bytes)
    {
        if(!bytes.SequenceEqual(executable))throw new InvalidDataException("Merged EXE inverse requires complete exact afterimage.");
        byte[] parent=bytes.ToArray();original.AsSpan(0x4AF18,8).CopyTo(parent.AsSpan(0x4AF18,8));
        return Admission.RevertExecutableExact(parent);
    }
    internal byte[] RevertCardOverlayExact(ReadOnlySpan<byte> bytes)=>Admission.RevertOverlayExact(bytes);
    internal byte[] RevertArtisansOverlayExact(ReadOnlySpan<byte> bytes)=>Authored.Integration.RevertOverlayExact(Authored.RevertOverlayExact(Lifecycle.RevertOverlayExact(bytes)));
    internal byte[] RevertLevelDataExact(ReadOnlySpan<byte> bytes)
    {
        if(!bytes.SequenceEqual(data))throw new InvalidDataException("Complete exact level-data afterimage required.");
        return Authored.Integration.RevertDataExact(Authored.RevertDataExact(bytes));
    }
    private static NativeJapaneseEggCollectibleIdentity[] FixedIdentities()=>[
        new("egg-a",Guid.Parse("11111111-1111-1111-1111-111111111111")),new("egg-b",Guid.Parse("22222222-2222-2222-2222-222222222222"))];
    private static EntryOwner Entry(int index,byte[] before,byte[] after)=>new(index,before.Length,after.Length,Hash(before),Hash(after));
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
}
