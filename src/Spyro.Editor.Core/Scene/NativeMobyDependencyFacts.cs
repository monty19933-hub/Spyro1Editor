using System.Buffers.Binary;

namespace Spyro.Editor.Core.Scene;

/// <summary>
/// Native instance facts, independent of user-editable names and inferred roles.
/// A nonzero pointer is a dependency to resolve, not a ban on importing the actor.
/// Only the row-only, self-contained candidate route requires no such dependencies.
/// This does not prove executable dispatch, textures, sound or dynamic spawn closure.
/// </summary>
public sealed record NativeMobyDependencyFacts(
    ushort ActorId,
    byte RenderRadius,
    uint PropertiesPointer,
    bool PropertiesPointerKnown,
    uint LegacyAssociatedWord,
    bool HasActiveLinks)
{
    public const int NativeRecordLength = 0x58;

    public bool RequiresPropertiesClosure => !PropertiesPointerKnown || PropertiesPointer != 0;
    public bool RequiresAssociatedDataClosure => LegacyAssociatedWord != 0;
    public bool CanUseSelfContainedRowCandidate =>
        !RequiresPropertiesClosure && !RequiresAssociatedDataClosure && !HasActiveLinks;

    public string RowOnlyDependencyReason => !PropertiesPointerKnown
        ? "Native properties-pointer evidence is absent from this cache; refresh source analysis before a row-only candidate."
        : PropertiesPointer != 0
        ? "Native properties (+0x00) require a planned properties extent, internal pointers and fixups; this is not a self-contained row."
        : LegacyAssociatedWord != 0
        ? "The legacy associated word (+0x08) is nonzero and requires its own dependency evidence; it is not the native properties pointer."
        : HasActiveLinks
        ? "Native route, helper, reward or companion links require a dependency bundle."
        : "No properties, associated-word or active-link dependency was found in this native instance; behavior closure still needs separate evidence.";

    public static NativeMobyDependencyFacts FromMoby(Moby moby)
    {
        ArgumentNullException.ThrowIfNull(moby);
        if (moby.SourceByte36 is < 0 or > 255 || moby.SourceByte37 is < 0 or > 255 || moby.Type is < 0 or > 255)
            throw new InvalidDataException("Native actor identity and render radius must be byte values.");
        return new((ushort)(moby.SourceByte36 | (moby.SourceByte37 << 8)), (byte)moby.Type,
            moby.PropertiesPointer, moby.HasPropertiesPointerEvidence, moby.SpecialDataPointer,
            moby.Links.Any(MobyLinkTraversal.IsActiveMoveLink));
    }

    public static NativeMobyDependencyFacts FromRecord(ReadOnlySpan<byte> record, bool hasActiveLinks = false)
    {
        if (record.Length != NativeRecordLength)
            throw new InvalidDataException($"Native Moby dependency inspection requires exactly 0x{NativeRecordLength:X} bytes.");
        return new(BinaryPrimitives.ReadUInt16LittleEndian(record[0x36..]), record[0x50],
            BinaryPrimitives.ReadUInt32LittleEndian(record), true,
            BinaryPrimitives.ReadUInt32LittleEndian(record[0x08..]), hasActiveLinks);
    }

    /// <summary>
    /// Checks the final authored class, not its name, render radius, or donor's
    /// former root. A remap requires an explicit class declaration in the
    /// committed package recipe. Root presence alone is not behavior proof.
    /// </summary>
    public bool TryValidateActorBinding(
        ushort donorActorId,
        IReadOnlyList<NativeMobyActorRootBinding> effectiveRoots,
        IReadOnlySet<ushort> declaredRemapActorIds,
        out string reason)
    {
        if (ActorId != donorActorId && !declaredRemapActorIds.Contains(ActorId))
        {
            reason = $"Final native class 0x{ActorId:X4} differs from donor class 0x{donorActorId:X4} and is not a declared actor remap in this dependency recipe.";
            return false;
        }
        if (!effectiveRoots.Any(root => root.ActorId == ActorId && root.RootOffset != 0))
        {
            reason = $"Final native class 0x{ActorId:X4} has no actor root in the effective destination dependency plan.";
            return false;
        }
        reason = "The final class has its declared actor root; executable behavior and other runtime dependencies require separate closure evidence.";
        return true;
    }
}

public sealed record NativeMobyActorRootBinding(ushort ActorId, uint RootOffset);
