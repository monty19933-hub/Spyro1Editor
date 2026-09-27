using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

namespace Spyro.Editor.Core.Analysis;

internal enum NativeCollectibleQueryResult : uint
{
    Available = 0, AlreadyEarned = 1, Retired = 2, OutOfRange = 3, InvalidLedger = 4
}

/// <summary>
/// Detached read-only MIPS-I state query. Uses the collection leaf's exact
/// manifest/slot/pointer/ABI validation, but never writes the ledger or reads
/// player proximity. A future reload-suppression caller must query BEFORE
/// visual/pickup work. No retirement, live Ready issuer or lifetime authority.
/// a0 = exact compiled ledger base, a1 = unsigned stable bit ID, v0 = result.
/// Register contract matches the collection leaf; no calls or stack accesses.
/// Concurrent mutation must be excluded for the entire query: read-only is
/// not an atomic snapshot of the independently updated bitmap/count metadata.
/// </summary>
internal sealed class NativeCollectibleStateQueryKernel
{
    private readonly byte[] bytes;
    private NativeCollectibleStateQueryKernel(byte[] bytes, NativeByteExtent owner, uint entryPc,
        uint ledgerAddress, string manifestSha256, int ordinarySlot, int liveFootprintBytes)
    {
        this.bytes = bytes.ToArray(); Owner = owner; EntryPc = entryPc;
        CallerLedgerAddress = ledgerAddress; ManifestSha256 = manifestSha256;
        OrdinarySlot = ordinarySlot; LiveFootprintBytes = liveFootprintBytes;
    }
    internal NativeByteExtent Owner { get; }
    internal uint EntryPc { get; }
    internal uint CallerLedgerAddress { get; }
    internal string ManifestSha256 { get; }
    internal int OrdinarySlot { get; }
    internal int LiveFootprintBytes { get; }
    internal int LedgerByteLength => NativeCollectibleSelectedSlotResidence.ReservationBytes;
    internal int StackByteLength => 0;
    internal bool NativeResidenceIssued => false;
    internal bool DispatchActivated => false;
    internal bool PublicationApproved => false;
    internal bool PersistentSaveChanges => false;
    internal bool ConcurrentMutationSafe => false;
    internal byte[] CopyBytes() => bytes.ToArray();

    internal static NativeCollectibleStateQueryKernel Compile(DetachedCollectibleManifest manifest, int ordinarySlot,
        NativeByteExtent codeCapacity, uint codeAddress, uint callerLedgerAddress)
    {
        var body = NativeCollectibleCollectionKernel.CompileStateQuery(manifest, ordinarySlot,
            codeCapacity, codeAddress, callerLedgerAddress);
        return new(body.Bytes, body.Owner, body.EntryPc, body.LedgerAddress,
            body.ManifestSha256, body.OrdinarySlot, body.LiveFootprintBytes);
    }
}
