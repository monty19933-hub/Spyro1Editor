using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Detached fixed-profile successor, never a mutation/promotion of published
/// R01. Source capture owns the same original bootstrap/reservation and adds a
/// guarded Gate admission plus an inline resident reset invalidation. Fixtures
/// cannot issue source composition; no writer or public payload route exists.
/// </summary>
internal sealed class NativeCollectibleCardAdmissionPlan
{
    internal const int ResetOffset = 0x2F00, ResetByteLength = 0x50;
    private static readonly int[] AnchorOffsets = [0x3030,0x1EAFC,0x23E24];
    private readonly byte[] originalExecutable,readerExecutable,executable,overlay;
    private NativeCollectibleCardAdmissionPlan(NativeCollectibleCardLoadPlan reader,NativeCollectibleCardLoadResearchComposition? ancestry)
    {
        Reader=reader;Ancestry=ancestry;SourceCertified=ancestry is not null;
        originalExecutable=reader.CopyOriginalExecutable();readerExecutable=ancestry?.CopyExecutable()??reader.CopyExecutable();
        if(SourceCertified)
        {
            ancestry!.RequireAncestry();
            if(!ReferenceEquals(ancestry.Card,reader)||!ancestry.RevertExecutableExact(readerExecutable).AsSpan().SequenceEqual(originalExecutable))
                throw new InvalidDataException("Admission lost actual reader/bootstrap original executable ancestry.");
            RequireResetOriginal(originalExecutable);
            byte[] card=reader.CopyOriginalOverlay();
            if(Word(card,0x174)!=0x3C028008||Word(card,0x178)!=0x8C428D78||
                Hash(originalExecutable.AsSpan(checked((int)(0x800339C0-0x8000F800)),0x3C))!="9a5e6fa72cef17674582c0f47e6abee767b205bfdfd377cddfd970fc6f927e99")
                throw new InvalidDataException("Admission card-entry/original phase-three dispatch seam changed.");
        }
        Kernel=NativeCollectibleLiveAdmissionKernel.Compile(reader);overlay=Kernel.CopyOverlay();executable=readerExecutable.ToArray();
        foreach(int offset in AnchorOffsets)
        {
            Put(executable,offset,0x3C020000|((Kernel.Layout.CopyAnchor+0x8000)>>16));
            Put(executable,offset+4,0x24420000|(Kernel.Layout.CopyAnchor&0xFFFF));
        }
        // Fixtures have no authentic resident reset function. Only complete
        // source capture can install the independently reviewed inline window.
        if(SourceCertified)CopyResetWords().CopyTo(executable,ResetOffset);
        ExecutableSha256=Hash(executable);OverlaySha256=Hash(overlay);
        if(!RevertExecutableExact(executable).AsSpan().SequenceEqual(originalExecutable)||
            !RevertOverlayExact(overlay).AsSpan().SequenceEqual(reader.CopyOriginalOverlay()))
            throw new InvalidDataException("Admission complete original inverse failed.");
    }
    internal NativeCollectibleCardLoadPlan Reader { get; }
    internal NativeCollectibleLiveAdmissionKernel Kernel { get; }
    internal NativeCollectibleCardLoadResearchComposition? Ancestry { get; }
    internal bool SourceCertified { get; }
    internal bool ReadyAdmissionEmitted => true;
    internal bool PublicationApproved => false;
    internal bool NativeResidenceIssued => false;
    internal bool RuntimeVerified => false;
    internal bool LifetimeProven => false;
    internal bool CardWrites => false;
    internal bool CollectorEnabled => false;
    internal string ExecutableSha256 { get; }
    internal string OverlaySha256 { get; }
    internal static NativeCollectibleCardAdmissionPlan CreateFixture() =>
        new(NativeCollectibleCardLoadPlan.CreateFixture(NativeCollectibleCardLoadResearchComposition.FixedManifest(),0),null);
    internal static NativeCollectibleCardAdmissionPlan Capture(NativeRegionalSourceSession source)
    {
        ArgumentNullException.ThrowIfNull(source);
        try
        {
            var manifest=NativeCollectibleCardLoadResearchComposition.FixedManifest();
            var reader=NativeCollectibleCardLoadPlan.Capture(source,manifest,0);
            var bootstrap=NativeCollectibleColdBootBootstrap.Create(NativeCollectibleReservationMechanics.Create(
                NativeCollectibleSelectedSlotResidence.Capture(source,manifest,0)));
            return new(reader,NativeCollectibleCardLoadResearchComposition.Create(reader,bootstrap));
        }
        finally { source.VerifyUnchanged(); }
    }
    internal void RequireSourceComposition()
    {
        if(!SourceCertified||Ancestry is null||!Reader.SourceCertified||!ReferenceEquals(Ancestry.Card,Reader))
            throw new InvalidDataException("Admission composition requires its actual source-issued reader/bootstrap, not a fixture.");
        Ancestry.RequireAncestry();RequireResetOriginal(originalExecutable);
        if(!RevertExecutableExact(executable).AsSpan().SequenceEqual(originalExecutable)||
            !RevertOverlayExact(overlay).AsSpan().SequenceEqual(Reader.CopyOriginalOverlay())||
            Kernel.Layout.FinalByteLength!=0xD000||Kernel.Layout.CopyAnchor!=0x80087A38)
            throw new InvalidDataException("Admission source composition geometry or exact inverse changed.");
    }
    internal byte[] CopyOriginalExecutable() => originalExecutable.ToArray();
    internal byte[] CopyOriginalOverlay() => Reader.CopyOriginalOverlay();
    internal byte[] CopyExecutable() => executable.ToArray();
    internal byte[] CopyOverlay() => overlay.ToArray();
    internal byte[] RevertToReaderOverlayExact(ReadOnlySpan<byte> bytes)
    {
        if(!bytes.SequenceEqual(overlay))throw new InvalidDataException("Admission overlay inverse requires its complete exact afterimage.");
        return Reader.CopyOverlay();
    }
    internal byte[] RevertOverlayExact(ReadOnlySpan<byte> bytes) => Reader.RevertExact(RevertToReaderOverlayExact(bytes));
    internal byte[] RevertToReaderExecutableExact(ReadOnlySpan<byte> bytes)
    {
        if(!bytes.SequenceEqual(executable))throw new InvalidDataException("Admission EXE inverse requires its complete exact afterimage.");
        byte[] result=bytes.ToArray();
        foreach(int offset in AnchorOffsets)readerExecutable.AsSpan(offset,8).CopyTo(result.AsSpan(offset,8));
        if(SourceCertified)readerExecutable.AsSpan(ResetOffset,ResetByteLength).CopyTo(result.AsSpan(ResetOffset,ResetByteLength));
        if(!result.AsSpan().SequenceEqual(readerExecutable))throw new InvalidDataException("Admission EXE inverse lost exact parent complement.");
        return result;
    }
    internal byte[] RevertExecutableExact(ReadOnlySpan<byte> bytes)
    {
        byte[] parent=RevertToReaderExecutableExact(bytes);
        return Ancestry is null?Reader.RevertExecutableExact(parent):Ancestry.RevertExecutableExact(parent);
    }
    internal static byte[] CopyResetWords()
    {
        uint[] words=[0x3C018007,0xAC205860,0xAC205750,0xAC205810,0xAC20587C,0xAC2056C8,0xAC205830,0xAC2058E8,
            0xAC22580C,0xAC205838,0xAC20583C,0x8C2155A4,0,0x2421F600,0x14200003,0,0x3C018020,0xA020F616,0x3C018007,0];
        byte[] bytes=new byte[ResetByteLength];for(int i=0;i<words.Length;i++)Put(bytes,i*4,words[i]);return bytes;
    }
    private static void RequireResetOriginal(ReadOnlySpan<byte> bytes)
    {
        NativeCollectibleColdBootBootstrap.RequireOriginalSource(bytes);
        if(Hash(bytes.Slice(ResetOffset,ResetByteLength))!="1e50b4e2d3b702c380c05ae4018cebdf4800b32493b5772e4a2fc1da598f554f"||
            Word(bytes,ResetOffset-4)!=0x2402FFFF)
            throw new InvalidDataException("Admission native reset compacting requires exact store/order/base/v0 source evidence.");
    }
    private static uint Word(ReadOnlySpan<byte> bytes,int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
    private static void Put(Span<byte> bytes,int offset,uint word) => BinaryPrimitives.WriteUInt32LittleEndian(bytes[offset..],word);
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
