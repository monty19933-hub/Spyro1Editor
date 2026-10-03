using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Universal;
namespace Spyro.Editor.Core.Exporting;

public static partial class NativePortalRouteRuntime
{
    // Return routing may change an imported class009 call inside the immutable
    // bundle. Transfer custody only across that existing authenticated operation;
    // no particle body, chain, table or unrelated source instruction can change.
    internal static UniversalCollectionStarContinuation ReissueParticleContinuation(UniversalSourceLibrary donors,
        byte[] before,NativePortalRouteOverlay routed,UniversalCollectionStarContinuation continuation)
    {
        int offset=checked((int)(continuation.Start-UniversalOverlay.Base));
        if(offset<0||offset+continuation.Length>before.Length||offset+continuation.Length>routed.Overlay.Length||
            Convert.ToHexStringLower(SHA256.HashData(before.AsSpan(offset,continuation.Length)))!=continuation.Sha)
            throw new InvalidDataException("Return routing lost its pre-edit issued particle bundle.");
        // This validates exact native packet/closure and exact appended stub;
        // the caller already checked every actual native/imported class009 owner.
        byte[] replay=routed.Overlay.ToArray();before.AsSpan(offset,continuation.Length).CopyTo(replay.AsSpan(offset));
        PatchReturnHomeCalls(donors,replay,routed.ReturnStubPointer);
        if(!replay.AsSpan(offset,continuation.Length).SequenceEqual(routed.Overlay.AsSpan(offset,continuation.Length)))
            throw new InvalidDataException("Return routing changed an unowned particle bundle instruction or table.");
        var packets=AuthenticateReturnOwners(donors).Values.ToArray();
        for(int at=offset;at<offset+continuation.Length;at+=4)
        {
            if(before.AsSpan(at,4).SequenceEqual(routed.Overlay.AsSpan(at,4)))continue;
            if(BinaryPrimitives.ReadUInt32LittleEndian(before.AsSpan(at,4))!=UniversalMips.EncodeJ(3,TransitionAddress)||
                BinaryPrimitives.ReadUInt32LittleEndian(routed.Overlay.AsSpan(at,4))!=UniversalMips.EncodeJ(3,routed.ReturnStubPointer)||
                at<44||at+8>before.Length||!packets.Any(p=>before.AsSpan(at-44,52).SequenceEqual(p)))
                throw new InvalidDataException("Particle custody permits only its exact source Return Home call redirection.");
        }
        return new(continuation.Entry,continuation.Start,continuation.Length,
            Convert.ToHexStringLower(SHA256.HashData(routed.Overlay.AsSpan(offset,continuation.Length))),continuation.Native);
    }
}
