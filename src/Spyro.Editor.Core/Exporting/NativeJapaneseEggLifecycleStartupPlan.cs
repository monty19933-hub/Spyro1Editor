using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Persistence;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Closed source-only composition of the existing detached lifecycle overlay
/// and its exact-manifest/slot cold bootstrap. Only three bootstrap-owned EXE
/// ranges are copied; overlay/data and the lifecycle copy anchor stay exact.
/// The bootstrap leaves a zero, NOT-LOADED ledger. Source ancestry and modeled
/// normal/synchronous geometry do not issue BIOS/cache, IRQ, lifetime, native
/// residence, Ready, card or publication authority.
/// </summary>
internal sealed class NativeJapaneseEggLifecycleStartupPlan
{
    private const int EntryOffset = 0x10, ReserveOffset = 0x65DA4, TailOffset = 0x65E40, TailBytes = 0x1C0;
    private const int CopyOffset = NativeJapaneseEggArtisansIntegrationPlan.CopyBufferPatchOffset;
    private const uint PeteDirectoryBytes = 0x3A000;
    private readonly byte[] lifecycleExecutable, originalExecutable, executable, overlayBytes, data;

    private NativeJapaneseEggLifecycleStartupPlan(NativeJapaneseEggAuthoredClassesPlan authored,
        NativeJapaneseEggLifecycleOverlayPlan overlay, NativeCollectibleColdBootBootstrap bootstrap,
        DetachedCollectibleManifest manifest)
    {
        if (!authored.SourceCertified || !overlay.SourceCertified || !overlay.Binding.SourceCertified ||
            !overlay.Projection.SourceCertified || !bootstrap.SourceEvidenceBound ||
            !bootstrap.Reservation.SourceEvidenceBound || !bootstrap.Reservation.Residence.SourceEvidenceBound)
            throw new InvalidDataException("Startup composition requires complete source-issued lifecycle and bootstrap ancestry.");
        overlay.Binding.RequireCurrent(authored, manifest);
        overlay.Projection.RequireCurrent(authored);
        var residence = bootstrap.Reservation.Residence;
        residence.RequireManifest(manifest);
        if (overlay.Collector.OrdinarySlot != residence.OrdinarySlot || overlay.Query.OrdinarySlot != residence.OrdinarySlot ||
            overlay.Attempt.OrdinarySlot != residence.OrdinarySlot || overlay.Collector.ManifestSha256 != manifest.Sha256 ||
            overlay.Query.ManifestSha256 != manifest.Sha256 || overlay.Attempt.ManifestSha256 != manifest.Sha256 ||
            overlay.Collector.CallerLedgerAddress != bootstrap.LedgerStart || overlay.Query.CallerLedgerAddress != bootstrap.LedgerStart ||
            overlay.Attempt.CallerLedgerAddress != bootstrap.LedgerStart || overlay.Collector.LedgerByteLength != bootstrap.LedgerEnd - bootstrap.LedgerStart)
            throw new InvalidDataException("Lifecycle consumers and bootstrap require the same exact manifest, ordinary slot and ledger extent.");

        lifecycleExecutable = overlay.CopyExecutable();
        originalExecutable = authored.Integration.CopyOriginalExecutable();
        byte[] recoveredAuthored = overlay.RevertExecutableExact(lifecycleExecutable);
        byte[] recoveredOriginal = authored.Integration.RevertExecutableExact(recoveredAuthored);
        byte[] startup = bootstrap.CopyExecutable(), reserved = bootstrap.Reservation.CopyExecutable();
        byte[] pete = authored.Integration.CopyPeteHeader();
        if (!recoveredAuthored.AsSpan().SequenceEqual(authored.CopyExecutable()) ||
            !recoveredOriginal.AsSpan().SequenceEqual(originalExecutable) ||
            !bootstrap.Reservation.CopyOriginalExecutable().AsSpan().SequenceEqual(originalExecutable) ||
            !bootstrap.RevertExact(startup).AsSpan().SequenceEqual(originalExecutable) ||
            !bootstrap.RevertToReservationExact(startup).AsSpan().SequenceEqual(reserved))
            throw new InvalidDataException("Lifecycle and bootstrap must descend from the same complete exact original USA executable.");
        residence.RequireExactOriginal(originalExecutable, pete);
        NativeCollectibleColdBootBootstrap.RequireOriginalSource(originalExecutable);
        overlayBytes = overlay.CopyOverlay(); data = overlay.CopyLevelData();
        if (!overlay.RevertOverlayExact(overlayBytes).AsSpan().SequenceEqual(authored.CopyOverlay()) ||
            !data.AsSpan().SequenceEqual(authored.CopyLevelData()) ||
            lifecycleExecutable.Length != originalExecutable.Length || startup.Length != originalExecutable.Length ||
            bootstrap.EntryField.Offset != EntryOffset || bootstrap.EntryField.ByteLength != 4 ||
            bootstrap.TailOwner.Offset != TailOffset || bootstrap.TailOwner.ByteLength != TailBytes ||
            bootstrap.Reservation.ReserveAfterimage.Offset != ReserveOffset || bootstrap.Reservation.ReserveAfterimage.ByteLength != 4 ||
            Word(originalExecutable, ReserveOffset) != 0x800 || Word(startup, ReserveOffset) != 0xA00 ||
            Word(startup, 0x65DA8) != 0x200000 || Word(startup, EntryOffset) != bootstrap.EntryPc)
            throw new InvalidDataException("Startup composition lost exact data, overlay or executable patch ownership.");

        (int Offset, int Bytes)[] patches = [(EntryOffset, 4), (ReserveOffset, 4), (TailOffset, TailBytes)];
        executable = lifecycleExecutable.ToArray();
        int lastEnd = 0;
        foreach (var patch in patches)
        {
            if (patch.Offset < lastEnd || patch.Offset < CopyOffset + 8 && patch.Offset + patch.Bytes > CopyOffset ||
                !lifecycleExecutable.AsSpan(patch.Offset, patch.Bytes).SequenceEqual(originalExecutable.AsSpan(patch.Offset, patch.Bytes)))
                throw new InvalidDataException("Startup patches overlap another owner or no longer have their exact original preimages.");
            startup.AsSpan(patch.Offset, patch.Bytes).CopyTo(executable.AsSpan(patch.Offset, patch.Bytes));
            lastEnd = patch.Offset + patch.Bytes;
        }
        for (int i = 0; i < executable.Length; i++)
        {
            bool owned = patches.Any(p => i >= p.Offset && i < p.Offset + p.Bytes);
            if (!owned && (executable[i] != lifecycleExecutable[i] || startup[i] != originalExecutable[i]))
                throw new InvalidDataException("Complete startup complement differs from the paired lifecycle/original ancestry.");
        }
        if (!executable.AsSpan(CopyOffset, 8).SequenceEqual(lifecycleExecutable.AsSpan(CopyOffset, 8)))
            throw new InvalidDataException("Startup composition changed the final lifecycle copy-anchor owner.");

        // The source-only integration and residence independently own the same
        // exact PETE header/directory profile. Recompute the complete changed
        // resource at its final anchor for BOTH reservation values.
        if (pete.Length != 0x800 || Word(pete, 0) != 0x39C48 || authored.Integration.CopyPeteRuntime().Length != 0x39C48 ||
            Word(overlayBytes, checked((int)(authored.Integration.VisualAncestor.EntryPc - 0x8007AA38))) != 0x27BDFD50 ||
            overlay.Lifecycle.MaximumStackByteLength != 0x60)
            throw new InvalidDataException("Final modeled startup memory requires the exact PETE, adapter and lifecycle stack owners.");
        NativeJapaneseEggMemoryReport.Metrics Measure(uint reserve) => NativeJapaneseEggMemoryReport.Measure(
            overlay.Projection.CopyAnchor, checked((uint)authored.Integration.RuntimeMemory.UsedLevelDataBytes),
            Word(data, 12), Word(data, 20), Word(data, 28), PeteDirectoryBytes, Word(pete, 0),
            Word(executable, 0x65DA8), reserve, AdapterStackByteLength);
        OriginalReserveMemory = Measure(Word(originalExecutable, ReserveOffset));
        FinalMemory = Measure(Word(executable, ReserveOffset));
        NormalFloorReduction = checked(OriginalReserveMemory.NormalReservedStart - FinalMemory.NormalReservedStart);
        MaximumNestedStackBytes = Math.Max(0x28, overlay.Lifecycle.MaximumStackByteLength);
        WorstSynchronousStackLow = checked(FinalMemory.DispatchStackPointer - (uint)AdapterStackByteLength - (uint)MaximumNestedStackBytes);
        WorstSynchronousStackMargin = (long)WorstSynchronousStackLow - bootstrap.LedgerStart;
        LedgerStackMargin = (long)WorstSynchronousStackLow - bootstrap.LedgerEnd;
        if (!FinalMemory.ModeledExtentsFit || !OriginalReserveMemory.ModeledExtentsFit ||
            FinalMemory.SceneEnd != overlay.Projection.SceneEnd || OriginalReserveMemory.SceneEnd != overlay.Projection.SceneEnd ||
            OriginalReserveMemory.NormalGap != overlay.Projection.NormalGap || NormalFloorReduction != 0x200 ||
            FinalMemory.NormalGap != OriginalReserveMemory.NormalGap - 0x200 ||
            FinalMemory.NormalReservedStart != bootstrap.Reservation.ProposedGeometry.NormalFloor ||
            FinalMemory.PeteResidentStart != bootstrap.Reservation.ProposedGeometry.PeteResidentBase ||
            FinalMemory.PeteTemporaryStart != bootstrap.Reservation.ProposedGeometry.PeteTemporaryStart ||
            WorstSynchronousStackLow != FinalMemory.VisualTickStackLow - 0x38 ||
            WorstSynchronousStackMargin < 0 || LedgerStackMargin < 0)
            throw new InvalidDataException("Final startup load and synchronous lifecycle stack geometry disagrees or exceeds its exact owners.");

        OverlayAncestor = overlay; BootstrapAncestor = bootstrap; OrdinarySlot = residence.OrdinarySlot; ManifestSha256 = manifest.Sha256;
        FinalExecutableSha256 = Hash(executable);
        var domain = new NativeAddressSpace(NativeAddressKind.ExecutableFile, FinalExecutableSha256, NativeAddressRevision.Final, executable.Length);
        StartupOwners = Array.AsReadOnly(patches.Select(p => new NativeByteExtent(domain, p.Offset, p.Bytes)).ToArray());
        CopyAnchorOwner = new(domain, CopyOffset, 8);
        PlanSha256 = Hash(JsonSerializer.SerializeToUtf8Bytes(new
        {
            profile = "source-authored-egg-lifecycle-cold-startup-v1", authored = authored.PlanSha256,
            lifecycle = overlay.PlanSha256, bootstrap = bootstrap.ExecutableSha256,
            reserve = bootstrap.Reservation.ExecutableSha256, original = Hash(originalExecutable), pete = Hash(pete),
            ManifestSha256, OrdinarySlot, FinalExecutableSha256, finalOverlay = Hash(overlayBytes), finalData = Hash(data),
            FinalMemory, OriginalReserveMemory, NormalFloorReduction, AdapterStackByteLength, MaximumNestedStackBytes,
            WorstSynchronousStackLow, WorstSynchronousStackMargin, LedgerStackMargin
        }));
        _ = RevertToLifecycleExecutableExact(executable); _ = RevertToOriginalExecutableExact(executable);
    }

    internal NativeJapaneseEggLifecycleOverlayPlan OverlayAncestor { get; }
    internal NativeCollectibleColdBootBootstrap BootstrapAncestor { get; }
    internal int OrdinarySlot { get; }
    internal string ManifestSha256 { get; }
    internal string FinalExecutableSha256 { get; }
    internal string PlanSha256 { get; }
    internal IReadOnlyList<NativeByteExtent> StartupOwners { get; }
    internal NativeByteExtent CopyAnchorOwner { get; }
    internal NativeJapaneseEggMemoryReport.Metrics OriginalReserveMemory { get; }
    internal NativeJapaneseEggMemoryReport.Metrics FinalMemory { get; }
    internal uint NormalFloorReduction { get; }
    internal int AdapterStackByteLength => 0x2B0;
    internal int MaximumNestedStackBytes { get; }
    internal uint WorstSynchronousStackLow { get; }
    internal long WorstSynchronousStackMargin { get; }
    internal long LedgerStackMargin { get; }
    internal bool SourceCertified => true;
    internal bool NativeResidenceIssued => false;
    internal bool DispatchActivated => false;
    internal bool ReadyIssued => false;
    internal bool PersistentSaveChanges => false;
    internal bool BiosLoadProven => false;
    internal bool InstructionCacheProven => false;
    internal bool InterruptSafetyProven => false;
    internal bool LifetimeProven => false;
    internal bool FullRuntimePeakProven => false;
    internal bool CardIoApproved => false;
    internal bool PublicationApproved => false;
    internal byte[] CopyExecutable() => executable.ToArray();
    internal byte[] CopyOverlay() => overlayBytes.ToArray();
    internal byte[] CopyLevelData() => data.ToArray();
    internal byte[] RevertToLifecycleExecutableExact(ReadOnlySpan<byte> after)
    {
        if (!after.SequenceEqual(executable)) throw new InvalidDataException("Startup inverse requires the complete exact executable afterimage.");
        return lifecycleExecutable.ToArray();
    }
    internal byte[] RevertToOriginalExecutableExact(ReadOnlySpan<byte> after)
    {
        _ = RevertToLifecycleExecutableExact(after);
        return originalExecutable.ToArray();
    }
    internal void RequireNativeInstallation() => throw new InvalidDataException(
        "Detached startup composition does not issue BIOS/cache/IRQ, native residence/lifetime, Ready, card or publication authority.");
    internal static NativeJapaneseEggLifecycleStartupPlan Create(NativeJapaneseEggAuthoredClassesPlan authored,
        NativeJapaneseEggLifecycleOverlayPlan overlay, NativeCollectibleColdBootBootstrap bootstrap, DetachedCollectibleManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(authored); ArgumentNullException.ThrowIfNull(overlay);
        ArgumentNullException.ThrowIfNull(bootstrap); ArgumentNullException.ThrowIfNull(manifest);
        return new(authored, overlay, bootstrap, manifest);
    }
    private static uint Word(byte[] bytes, int at) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at));
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
