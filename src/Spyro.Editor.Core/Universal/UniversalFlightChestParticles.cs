using System.Security.Cryptography;
namespace Spyro.Editor.Core.Universal;

/// <summary>Issued original Flight particle context; static/source evidence only.</summary>
public sealed record UniversalFlightChestParticleProof(string SourceLevelKey,string DestinationLevelKey,int TypeId,
    uint SourceSpawn,uint DestinationSpawn,uint SourceUpdate,uint DestinationUpdate,bool EmptyBody,
    string TextureOwnership,string Status);

internal static class UniversalFlightChestParticles
{
    // These profiles are issued only through the complete original source/course
    // certificate. Independent exact symbolic proofs cover all constructor/update
    // branches across both original compiler layouts. Shared native allocator,
    // free/vector/RNG and descriptor renderer belong to the full original EXE.
    internal static (UniversalParticleDependency Dependency,UniversalFlightChestParticleProof Proof) Inspect(
        UniversalFlightChestCertificate certificate,int type,UniversalOverlay source,UniversalOverlay destination,
        UniversalLevelData sourceData,UniversalLevelData destinationData,byte[] exe)
    {
        if(type is not(1 or 2 or 70))throw new InvalidDataException("Flight chest particle type is outside its issued source profile.");
        certificate.RequireBundle(source,destination,certificate.Classes,exe);
        certificate.RequireParticleInputs(sourceData,destinationData);
        UniversalFlightChestOwnership.RequireParticleTextures(sourceData,destinationData);
        bool empty=type==70;
        string status=empty?"source-certified-no-op":"source-certified-native-flight";
        var proof=new UniversalFlightChestParticleProof(certificate.SourceKey,certificate.DestinationKey,type,
            source.Bindings[0x800758E4].Value,destination.Bindings[0x800758E4].Value,
            source.Bindings[0x800756BC].Value,destination.Bindings[0x800756BC].Value,empty,
            empty?"No particle body or texture read.":"Destination native first tile; issued pixels and full palette equal the original source.",status);
        return(new(certificate.SourceKey,type,status,empty
            ?"This particle request performs no native allocation, update or texture read."
            :"Uses the destination's native Flight particle behavior and matching texture."),proof);
    }
}
