using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

namespace Spyro.Editor.Core.Analysis;

internal sealed record NativeCollectibleSaveSelection(uint Reserve,uint Bound,uint Controller,uint Slot,uint Marker);

/// <summary>
/// Validated detached result bytes owned by one exact compiler instance.
/// Validation is not evidence that emitted instructions or real I/O executed.
/// No public constructor, byte-array authority or native residence is issued.
/// </summary>
internal sealed class NativeCollectiblePreparedSaveTail
{
    private readonly NativeCollectibleSaveTailKernel owner;
    private readonly byte[] oldTail,record;
    private NativeCollectiblePreparedSaveTail(NativeCollectibleSaveTailKernel owner,byte[] oldTail,byte[] record)
    { this.owner=owner;this.oldTail=oldTail;this.record=record; }
    internal bool NativeExecutionProven => false;
    internal bool NativeIoApproved => false;
    internal byte[] CopyOldTail()=>oldTail.ToArray();
    internal byte[] CopyLedger()=>record.AsSpan(0,512).ToArray();
    internal byte[] CopyTail()=>record.AsSpan(0x200,0x100).ToArray();
    internal byte[] CopyDigest()=>record.AsSpan(0x300,32).ToArray();
    internal void RequireOwner(NativeCollectibleSaveTailKernel kernel)
    { if(!ReferenceEquals(owner,kernel))throw new InvalidDataException("Prepared snapshot belongs to another actual kernel owner."); }
    internal static NativeCollectiblePreparedSaveTail Inspect(NativeCollectibleSaveTailKernel owner,ReadOnlySpan<byte> oldTail,
        ReadOnlySpan<byte> record,ReadOnlySpan<byte> capturedLive)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if(oldTail.Length!=0x100||record.Length!=0x340||capturedLive.Length!=512||!record[..512].SequenceEqual(capturedLive)||
            Word(record,0x320)!=NativeCollectibleSaveTailKernel.PreparedMagic||
            record[0x324..].IndexOfAnyExcept((byte)0)>=0)throw new InvalidDataException("Incomplete detached prepared record.");
        var manifest=NativeCollectibleCardLoadResearchComposition.FixedManifest();
        var before=DetachedCollectibleSaveCodec.Inspect(oldTail[..0x83],manifest).RequireCompatible();
        ValidateLedger(record[..512],requireDirty:true);
        if(!SHA256.HashData(oldTail[..0x83]).AsSpan().SequenceEqual(record.Slice(0x50,32))||
            (before.CopySlotBits(0)[0]&record[0x90])!=before.CopySlotBits(0)[0])
            throw new InvalidDataException("Old complete envelope digest or earned ancestry differs from captured live state.");
        byte[] bits=[record[0x90],before.CopySlotBits(1)[0],before.CopySlotBits(2)[0]];
        byte[] expected=oldTail.ToArray();
        DetachedCollectibleSaveCodec.Serialize(DetachedCollectibleSave.FromValidatedBits(manifest,bits)).CopyTo(expected,0);
        if(!expected.AsSpan().SequenceEqual(record.Slice(0x200,0x100))||
            !SHA256.HashData(expected.AsSpan(0,0x83)).AsSpan().SequenceEqual(record.Slice(0x300,32)))
            throw new InvalidDataException("Prepared transfer, other slots, suffix or full-envelope digest changed.");
        return new(owner,oldTail.ToArray(),record.ToArray());
    }
    // Canonical ABI equality, with only documented live mutable fields exempt.
    // Native sentinel first16 is opaque; the remaining reservation is not scratch.
    internal static void ValidateLedger(ReadOnlySpan<byte> ledger,bool requireDirty)
    {
        if(ledger.Length!=512||ledger[0x16]!=1||ledger[0x17]!=0||ledger[0x18]>1||ledger[0x19]>1||
            requireDirty&&ledger[0x19]!=1||Word(ledger,0x8C)==0||Word(ledger,0x7C)==0||Word(ledger,0x80)>Word(ledger,0x7C))
            throw new InvalidDataException("Live save snapshot phase, tuple, epoch or generation is invalid.");
        var manifest=NativeCollectibleCardLoadResearchComposition.FixedManifest();
        byte[] envelope=DetachedCollectibleSaveCodec.Serialize(DetachedCollectibleSave.FromValidatedBits(manifest,[ledger[0x90],0,0]));
        byte[] expected=DetachedCollectibleLedgerImage.Hydrate(NativeCollectibleSelectedSlotResidence.Describe(manifest,0),manifest,envelope,
            ledger[0x18],BinaryPrimitives.ReadUInt16LittleEndian(ledger[0x1A..]),Word(ledger,0x8C)).CopyBytes();
        expected[0x19]=ledger[0x19]; ledger.Slice(0x50,32).CopyTo(expected.AsSpan(0x50)); ledger.Slice(0x7C,8).CopyTo(expected.AsSpan(0x7C));
        if(!ledger[0x10..].SequenceEqual(expected.AsSpan(0x10)))throw new InvalidDataException("Complete live save ABI differs from the fixed manifest.");
    }
    internal static uint Word(ReadOnlySpan<byte> bytes,int at)=>BinaryPrimitives.ReadUInt32LittleEndian(bytes[at..]);
}

internal enum NativeCollectibleSaveStage
{ NeedOldTailRead,ReadingOldTail,NeedPreparation,NeedOrdinaryWrite,WritingOrdinary,NeedOrdinaryRead,ReadingOrdinary,
  NeedTailWrite,WritingTail,NeedTailRead,ReadingTail,Verified,Committed,Failed,Invalidated }
internal sealed record NativeCollectibleSaveRequest(uint Operation,int FileOffset,int ByteLength,byte[]? WriteBytes);

/// <summary>
/// NON-I/O reference state machine for the future native transaction contract.
/// Scalar API/poll receipts and buffer bytes are supplied by a test adapter;
/// this is neither emitted native control nor proof of card completion, IRQ,
/// caller exclusivity, resident scratch, runtime or crash atomicity. It never
/// opens/writes a card, calls a service, edits live memory or patches source.
/// A commit returns a detached proposal. Failures never authorize cancellation
/// or release of an in-flight real native operation or its buffer owner.
/// The caller MUST invalidate this model before selection/reset/card re-entry
/// or replacement can end its externally tracked session, even if re-admission
/// reproduces identical bytes. Opaque epoch/tuple equality is only a consistency
/// check, never native session authentication. No native invalidation hook or
/// lease is supplied here; forgetting that signal is outside this model's proof.
/// </summary>
internal sealed class NativeCollectibleSaveTransaction
{
    private readonly NativeCollectibleSaveTailKernel owner;
    private readonly NativeCollectibleSaveSelection selection;
    private readonly byte[] ordinary,beginLedger;
    private byte[]? oldTail;
    private NativeCollectiblePreparedSaveTail? prepared;
    private bool requestIssued;
    private uint highestObservedGeneration;
    private byte highestObservedBitmap;
    private NativeCollectibleSaveTransaction(NativeCollectibleSaveTailKernel owner,ReadOnlySpan<byte> ordinary,
        ReadOnlySpan<byte> live,NativeCollectibleSaveSelection selection)
    {
        if(ordinary.Length!=0x600)throw new InvalidDataException("Complete ordinary serializer snapshot required.");
        NativeCollectiblePreparedSaveTail.ValidateLedger(live,true); RequireSelection(live,selection);
        this.owner=owner;this.ordinary=ordinary.ToArray();beginLedger=live.ToArray();this.selection=selection;
        highestObservedGeneration=NativeCollectiblePreparedSaveTail.Word(live,0x7C);highestObservedBitmap=live[0x90];
    }
    internal NativeCollectibleSaveStage Stage { get; private set; }=NativeCollectibleSaveStage.NeedOldTailRead;
    internal bool ReferenceModelOnly=>true;
    internal bool NativeControllerExecuted=>false;
    internal bool NativeResidenceIssued=>false;
    internal bool CardWrites=>false;
    internal bool RuntimeVerified=>false;
    internal bool PublicationApproved=>false;
    internal bool CrashAtomic=>false;
    internal bool RequiresExternalSessionInvalidation=>true;
    internal bool NativeSessionIdentityAuthenticated=>false;
    internal bool NativeCancellationApproved=>false;
    internal bool WouldReleaseNativeSuccess=>Stage==NativeCollectibleSaveStage.Committed;
    /// <summary>
    /// Terminal, idempotent revocation of this model only. It does not cancel
    /// native I/O, release buffers, edit live memory or undo a returned proposal.
    /// A new model requires a separately established external session lifetime;
    /// repeating the old ledger, marker or opaque epoch cannot revive this one.
    /// </summary>
    internal void InvalidateSession(){Stage=NativeCollectibleSaveStage.Invalidated;requestIssued=false;}
    internal static NativeCollectibleSaveTransaction Begin(NativeCollectibleSaveTailKernel owner,ReadOnlySpan<byte> ordinary,
        ReadOnlySpan<byte> live,NativeCollectibleSaveSelection selection)
    { ArgumentNullException.ThrowIfNull(owner);ArgumentNullException.ThrowIfNull(selection);return new(owner,ordinary,live,selection); }
    internal NativeCollectibleSaveRequest Request(ReadOnlySpan<byte> live,NativeCollectibleSaveSelection current,uint busy,uint latch)
    {
        try
        {
            RequireCurrent(live,current);
            if(requestIssued||busy!=0||latch!=0)throw new InvalidDataException("Request lacks exclusive drained ownership or is already issued.");
            var request=NextRequest();requestIssued=true;return request;
        }
        catch(InvalidDataException){Fail();throw;}
    }
    private NativeCollectibleSaveRequest NextRequest()=>Stage switch {
        NativeCollectibleSaveStage.NeedOldTailRead=>new(3,0x1400,0x100,null),
        NativeCollectibleSaveStage.NeedOrdinaryWrite=>new(4,0x200,0x600,ordinary.ToArray()),
        NativeCollectibleSaveStage.NeedOrdinaryRead=>new(3,0x200,0x600,null),
        NativeCollectibleSaveStage.NeedTailWrite=>new(4,0x1400,0x100,prepared!.CopyTail()),
        NativeCollectibleSaveStage.NeedTailRead=>new(3,0x1400,0x100,null),
        _=>throw new InvalidDataException("No new native request is owned in this model state.") };
    internal void Accepted(int result,uint busyBefore,uint latchBefore)
    {
        try
        {
            _=NextRequest();
            if(!requestIssued||result!=1||busyBefore!=0||latchBefore!=0)throw new InvalidDataException("Request was unissued/rejected or would erase another operation/completion.");
            requestIssued=false;
            Stage=Stage switch {
                NativeCollectibleSaveStage.NeedOldTailRead=>NativeCollectibleSaveStage.ReadingOldTail,
                NativeCollectibleSaveStage.NeedOrdinaryWrite=>NativeCollectibleSaveStage.WritingOrdinary,
                NativeCollectibleSaveStage.NeedOrdinaryRead=>NativeCollectibleSaveStage.ReadingOrdinary,
                NativeCollectibleSaveStage.NeedTailWrite=>NativeCollectibleSaveStage.WritingTail,
                _=>NativeCollectibleSaveStage.ReadingTail };
        }
        catch(InvalidDataException){Fail();throw;}
    }
    internal void Poll(int returned,uint operation,uint result,uint busyAfter,uint latchAfter,ReadOnlySpan<byte> readback,
        ReadOnlySpan<byte> live,NativeCollectibleSaveSelection current)
    {
        try
        {
            RequireCurrent(live,current);
            bool write=Stage is NativeCollectibleSaveStage.WritingOrdinary or NativeCollectibleSaveStage.WritingTail;
            if(Stage is not (NativeCollectibleSaveStage.ReadingOldTail or NativeCollectibleSaveStage.WritingOrdinary or
                NativeCollectibleSaveStage.ReadingOrdinary or NativeCollectibleSaveStage.WritingTail or NativeCollectibleSaveStage.ReadingTail))
                throw new InvalidDataException("No accepted operation owns this completion.");
            uint expected=write?4u:3u;
            if(returned==0)
            {
                if(busyAfter!=expected||latchAfter!=0||!readback.IsEmpty)throw new InvalidDataException("Pending operation ownership changed.");
                return; // Pending output words are explicitly not success evidence.
            }
            if(returned!=1||operation!=expected||result!=0||busyAfter!=0||latchAfter!=0||write&&!readback.IsEmpty)
                throw new InvalidDataException("Completion was idle/stale/wrong operation/failed/undrained.");
            switch(Stage)
            {
                case NativeCollectibleSaveStage.ReadingOldTail:
                    if(readback.Length!=0x100)throw new InvalidDataException("Old tail transfer is incomplete.");
                    oldTail=readback.ToArray();Stage=NativeCollectibleSaveStage.NeedPreparation;break;
                case NativeCollectibleSaveStage.WritingOrdinary:Stage=NativeCollectibleSaveStage.NeedOrdinaryRead;break;
                case NativeCollectibleSaveStage.ReadingOrdinary:
                    if(!readback.SequenceEqual(ordinary))throw new InvalidDataException("Complete ordinary readback differs, regardless of native checksum.");
                    Stage=NativeCollectibleSaveStage.NeedTailWrite;break;
                case NativeCollectibleSaveStage.WritingTail:Stage=NativeCollectibleSaveStage.NeedTailRead;break;
                case NativeCollectibleSaveStage.ReadingTail:
                    if(!readback.SequenceEqual(prepared!.CopyTail()))throw new InvalidDataException("Complete envelope and opaque suffix readback differs.");
                    Stage=NativeCollectibleSaveStage.Verified;break;
            }
        }
        catch(InvalidDataException){Fail();throw;}
    }
    internal void Prepared(NativeCollectiblePreparedSaveTail value)
    {
        ArgumentNullException.ThrowIfNull(value);
        try
        {
            if(Stage!=NativeCollectibleSaveStage.NeedPreparation)throw new InvalidDataException("Fresh accepted old-tail read must precede preparation.");
            value.RequireOwner(owner);
            if(!value.CopyOldTail().AsSpan().SequenceEqual(oldTail)||!value.CopyLedger().AsSpan(0x10).SequenceEqual(beginLedger.AsSpan(0x10)))
                throw new InvalidDataException("Prepared snapshot differs from this transaction's actual original owners.");
            prepared=value;Stage=NativeCollectibleSaveStage.NeedOrdinaryWrite;
        }
        catch(InvalidDataException){Fail();throw;}
    }
    internal byte[] Commit(ReadOnlySpan<byte> live,NativeCollectibleSaveSelection current)
    {
        try
        {
            if(Stage!=NativeCollectibleSaveStage.Verified)throw new InvalidDataException("Both exact readbacks must precede native success/dirty commit.");
            RequireCurrent(live,current);byte[] result=live.ToArray();
            uint generation=NativeCollectiblePreparedSaveTail.Word(beginLedger,0x7C);
            prepared!.CopyDigest().CopyTo(result,0x50);BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(0x80),generation);
            result[0x19]=(byte)(NativeCollectiblePreparedSaveTail.Word(live,0x7C)==generation&&live[0x90]==beginLedger[0x90]?0:1);
            Stage=NativeCollectibleSaveStage.Committed;return result;
        }
        catch(InvalidDataException){Fail();throw;}
    }
    private void Fail(){if(Stage!=NativeCollectibleSaveStage.Invalidated)Stage=NativeCollectibleSaveStage.Failed;}
    private void RequireCurrent(ReadOnlySpan<byte> live,NativeCollectibleSaveSelection current)
    {
        if(Stage is NativeCollectibleSaveStage.Invalidated or NativeCollectibleSaveStage.Failed or NativeCollectibleSaveStage.Committed)
            throw new InvalidDataException("This reference transaction is terminal and cannot observe another session.");
        NativeCollectiblePreparedSaveTail.ValidateLedger(live,true);RequireSelection(live,current);
        uint generation=NativeCollectiblePreparedSaveTail.Word(live,0x7C);byte bitmap=live[0x90];
        if(current!=selection||NativeCollectiblePreparedSaveTail.Word(live,0x8C)!=NativeCollectiblePreparedSaveTail.Word(beginLedger,0x8C)||
            !live.Slice(0x50,32).SequenceEqual(beginLedger.AsSpan(0x50,32))||
            NativeCollectiblePreparedSaveTail.Word(live,0x80)!=NativeCollectiblePreparedSaveTail.Word(beginLedger,0x80)||
            generation<highestObservedGeneration||(bitmap&highestObservedBitmap)!=highestObservedBitmap||
            generation==highestObservedGeneration&&bitmap!=highestObservedBitmap)
            throw new InvalidDataException("Save observation changed initial ancestry or rolled back previously observed generation/earned bits.");
        highestObservedGeneration=generation;highestObservedBitmap=bitmap;
    }
    private static void RequireSelection(ReadOnlySpan<byte> live,NativeCollectibleSaveSelection value)
    {
        if(value.Reserve!=0xA00||value.Bound!=1||value.Slot!=0||value.Controller!=(uint)live[0x18]*16||
            value.Marker!=BinaryPrimitives.ReadUInt16LittleEndian(live[0x1A..]))
            throw new InvalidDataException("Native selected-save tuple differs from the complete live snapshot.");
    }
}
