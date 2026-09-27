using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;
using Spyro.Editor.Core.Persistence;
using Abi = Spyro.Editor.Core.Analysis.NativeCollectibleSelectedSlotResidence;

namespace Spyro.Editor.Core.Analysis;

/// <summary>
/// Immutable host research image of the selected-slot ABI, built only from an
/// internally inspected exact detached envelope and its exact residence manifest.
/// The image contains the ABI Ready byte for isolated collector composition; it
/// issues no native Ready, RAM, selected-save binding, card or installation authority.
/// It must never be used as fallback for rejected bytes or copied into live RAM.
/// </summary>
internal sealed class DetachedCollectibleLedgerImage
{
    private readonly byte[] bytes, envelope;

    private DetachedCollectibleLedgerImage(byte[] bytes, byte[] envelope, Abi residence,
        int controllerIndex, ushort nativeFileMarker, uint sessionEpoch, int activeCount, int earnedActiveCount)
    {
        this.bytes = bytes.ToArray(); this.envelope = envelope.ToArray();
        ManifestSha256 = residence.ManifestSha256; OrdinarySlot = residence.OrdinarySlot;
        LiveFootprintBytes = residence.Sizing.LiveFootprintBytes; ControllerIndex = controllerIndex;
        NativeFileMarker = nativeFileMarker; SessionEpoch = sessionEpoch;
        ActiveCount = activeCount; EarnedActiveCount = earnedActiveCount;
        EnvelopeSha256 = Hash(this.envelope); ImageSha256 = Hash(this.bytes);
    }

    internal int ByteLength => bytes.Length;
    internal int LiveFootprintBytes { get; }
    internal int OrdinarySlot { get; }
    internal int ControllerIndex { get; }
    // This small native marker is explicitly not a unique file/content identity.
    internal ushort NativeFileMarker { get; }
    internal uint SessionEpoch { get; }
    internal int ActiveCount { get; }
    internal int EarnedActiveCount { get; }
    internal string ManifestSha256 { get; }
    internal string EnvelopeSha256 { get; }
    internal string ImageSha256 { get; }
    internal bool DetachedEnvelopeValidated => true;
    internal bool NativeBindingValidated => false;
    internal bool ReadyAdmissionAuthorized => false;
    internal bool NativeResidenceIssued => false;
    internal bool Installed => false;
    internal bool LifetimeProven => false;
    internal bool CardIoApproved => false;
    internal bool PersistentSaveChanges => false;
    internal bool PublicationApproved => false;
    internal byte[] CopyBytes() => bytes.ToArray();
    internal byte[] CopyValidatedEnvelope() => envelope.ToArray();

    /// <summary>
    /// Slot is fixed by residence. Controller is the index 0/1, not native 0/16.
    /// Marker bounds and opaque epoch copying do not authenticate a native tuple.
    /// The entire uint epoch range is retained, including 0 and MaxValue: no
    /// existing ABI assigns zero special meaning, and hydration never increments it.
    /// No caller-provided digest, cached count or read-result State is accepted.
    /// </summary>
    internal static DetachedCollectibleLedgerImage Hydrate(Abi residence, DetachedCollectibleManifest manifest,
        ReadOnlySpan<byte> rawEnvelope, int controllerIndex, uint nativeFileMarker, uint sessionEpoch)
    {
        ArgumentNullException.ThrowIfNull(residence); ArgumentNullException.ThrowIfNull(manifest);
        residence.RequireManifest(manifest);
        if ((uint)controllerIndex > 1 || nativeFileMarker > ushort.MaxValue)
            throw new InvalidDataException("Detached ledger image requires controller index 0/1 and a non-narrowed 16-bit native marker; these bounds prove no native binding.");

        // Inspect retains an immutable snapshot and validates all three slots,
        // exact schema/padding/checksum and manifest before exposing any state.
        DetachedCollectibleReadResult inspected = DetachedCollectibleSaveCodec.Inspect(rawEnvelope, manifest);
        DetachedCollectibleSave state = inspected.RequireCompatible();
        byte[] envelope = inspected.CopyOriginalBytes();
        byte[] selected = residence.CopySelectedSlotBits(state), active = residence.CopyActiveMask();
        int activeCount = 0, earnedActiveCount = 0;
        for (int index = 0; index < active.Length; index++)
        {
            activeCount += BitOperations.PopCount((uint)active[index]);
            earnedActiveCount += BitOperations.PopCount((uint)(selected[index] & active[index]));
        }

        // These checks make the serialized field limits explicit even if a
        // future detached schema grows beyond this unchanged residence profile.
        ushort lifetime = Fit16(manifest.LifetimeCount), bitmapBytes = Fit16(residence.Sizing.RawBitmapBytes),
            active16 = Fit16(activeCount), earned16 = Fit16(earnedActiveCount);
        if (selected.Length != residence.Sizing.AlignedBitmapBytes || active.Length != selected.Length ||
            residence.ActiveMaskOffset + active.Length > Abi.ReservationBytes ||
            activeCount != manifest.ActiveCount || earnedActiveCount > activeCount)
            throw new InvalidDataException("Detached ledger image exceeds its exact residence or manifest count owners.");

        // Complete zero initialization includes the guard and unused reservation
        // tail, but grants no authority to overwrite a native live guard/sentinel.
        byte[] image = new byte[Abi.ReservationBytes];
        Put32(image, Abi.MagicOffset, Abi.AbiMagic); Put16(image, Abi.VersionOffset, Abi.AbiVersion);
        image[Abi.PhaseOffset] = Abi.ReadyPhase;
        image[Abi.OrdinarySlotOffset] = checked((byte)residence.OrdinarySlot);
        image[Abi.ControllerOffset] = checked((byte)controllerIndex);
        Put16(image, Abi.NativeFileOffset, checked((ushort)nativeFileMarker));
        manifest.SerializeCanonical().AsSpan(0x10, 16).CopyTo(image.AsSpan(Abi.ContentIdOffset, 16));
        Convert.FromHexString(manifest.Sha256).CopyTo(image, Abi.ManifestDigestOffset);
        SHA256.HashData(envelope).CopyTo(image, Abi.EnvelopeDigestOffset);
        Put32(image, Abi.RevisionOffset, manifest.Revision);
        Put16(image, Abi.LifetimeCountOffset, lifetime); Put16(image, Abi.BitmapBytesOffset, bitmapBytes);
        Put16(image, Abi.ActiveCountOffset, active16); Put16(image, Abi.EarnedActiveCountOffset, earned16);
        Put32(image, Abi.SessionEpochOffset, sessionEpoch);
        selected.CopyTo(image, residence.SelectedBitsOffset); active.CopyTo(image, residence.ActiveMaskOffset);
        // Dirty/status/mutation-generation/transaction-generation/operation/failure
        // remain zero: this is a clean host snapshot, not evidence of card durability.
        return new(image, envelope, residence, controllerIndex, checked((ushort)nativeFileMarker), sessionEpoch, activeCount, earnedActiveCount);
    }

    internal void RequireNativeInstallation() => throw new InvalidDataException(
        "Detached ledger image only: native selected-save admission, live RAM/guard ownership, lifetime, concurrency and installation remain unissued.");

    private static ushort Fit16(int value) => (uint)value <= ushort.MaxValue ? (ushort)value :
        throw new InvalidDataException("Detached ledger count exceeds its 16-bit ABI field.");
    private static void Put16(byte[] bytes, int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), value);
    private static void Put32(byte[] bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
