using System.Security.Cryptography;

namespace Spyro.Editor.Core.Analysis;

public sealed record NativeSimpleMobyConsumerEvidence(uint RuntimeAddress, int ExecutableOffset, int ByteLength, string Sha256);
public sealed record NativeSharedMobyReadAheadOwner(ushort ActorId, int RegistrationSlot, uint RootPeteOffset,
    int OwnerRelativeOffset, int ByteLength, string Sha256)
{
    public bool PipelineValuesDiscarded => true;
    public bool WholeFollowingModelRequired => false;
}

/// <summary>
/// Source-only closure of original USA PETE class22, with the renderer's external
/// read-ahead classified separately as class54 header bytes. Not Japanese F5,
/// an arbitrary shared-model certificate, or a destination/import approval.
/// </summary>
public sealed class NativeUsaSharedEggModel
{
    public const int PeteHeaderWadOffset = 0x7B8800;
    public const int RuntimeCopyWadOffset = 0x7B9000;
    public const int RuntimeCopyByteLength = 0x39C48;
    public const uint RootPeteOffset = 0x3382C;
    public const string ExpectedPackageSha256 = "b4cf4f6265b275450c5e039d0096f723064057612df6faa9bf9ab46ebff50714";
    private const string HeaderHash = "b9a4de193b5cf0bd9592de15c57aa5daa3713c3575026154b45402f3aceef6f1";
    private const string RuntimeCopyHash = "1703ac37e59e480909b6f66dc856d4e5f3ce80551f18cbc8c06129c3b54dd35d";
    private const string ReadAheadHash = "76df2a96a85a7aedab18e72c11dd27bc9fb7948518fdea7817ddf6a72096fd4c";
    private static readonly NativeSimpleMobyConsumerEvidence[] Consumers =
    [
        new(0x800133E0, 0x3BE0, 0x26C, "fede3c414aea2dce0372fce574549d961123cf6456a7a291acef48b9c4af8598"),
        new(0x80022FEC, 0x137EC, 0x11C, "74978cee8e81a0ca2062f9242b53073591e9f830d35eae77ad36ea74852459ca"),
        new(0x80023268, 0x13A68, 0x6F0, "c9cef2546764b5b14a58c4d9bc33a938512d378758258b97a469ef900f80cfa8")
    ];
    private NativeUsaSharedEggModel(string sourceHash, NativeSimpleGouraudModelGeometry geometry,
        NativeSharedMobyReadAheadOwner readAhead)
    { SourceImageSha256 = sourceHash; Geometry = geometry; ReadAheadOwner = readAhead; }

    public string SourceImageSha256 { get; }
    public ushort ActorId => 0x22;
    public int RegistrationSlot => 3;
    public int RootWadOffset => checked(PeteHeaderWadOffset + (int)RootPeteOffset);
    public string PeteHeaderSha256 => HeaderHash;
    public string RuntimeCopySha256 => RuntimeCopyHash;
    public NativeSimpleGouraudModelGeometry Geometry { get; }
    public NativeSharedMobyReadAheadOwner ReadAheadOwner { get; }
    public IReadOnlyList<NativeSimpleMobyConsumerEvidence> ConsumerEvidence => Array.AsReadOnly(Consumers);
    public bool SourceGeometryClosureProven => true;
    public bool SourceReadWindowClosureProven => true;
    public bool NativeImportApproved => false;
    public bool DestinationRuntimeVerified => false;

    public static NativeUsaSharedEggModel Capture(NativeRegionalSourceSession source)
    {
        ArgumentNullException.ThrowIfNull(source);
        Require(source.Revision == NativeDiscRevision.UsaScus94228, "Shared egg22 requires the exact original USA source.");
        foreach (var consumer in Consumers)
            Require(Hash(source.ReadExecutableRange(consumer.ExecutableOffset, consumer.ByteLength)) == consumer.Sha256,
                "Native simple-model consumer changed.");
        byte[] header = source.ReadWadRange(PeteHeaderWadOffset, 0x800);
        byte[] payload = source.ReadWadRange(RuntimeCopyWadOffset, RuntimeCopyByteLength);
        Require(Hash(header) == HeaderHash && Hash(payload) == RuntimeCopyHash, "Complete shared PETE source owners changed.");
        var inventory = NativeSharedMobySourceInventory.Decode(header, payload);
        var root = inventory.Registrations.Single(r => r.ActorId == 0x22);
        Require(root.Slot == 3 && root.RootPeteOffset == RootPeteOffset, "Original shared egg registration changed.");
        // Decode against the complete native-copy owner, not an adjacent-root slice.
        var geometry = NativeSimpleGouraudModelGeometry.Decode(payload.AsSpan(root.RootPayloadOffset));
        Require(geometry.PackageSha256 == ExpectedPackageSha256 && geometry.PackageByteLength == 0x5BC &&
            geometry.Vertices.Count == 117 && geometry.Faces.Count == 115 && geometry.PackedRgbWords.Count == 45 &&
            geometry.Faces.All(f => !f.IsTriangle), "Native-derived ordinary egg geometry changed.");
        var nextOwner = inventory.Registrations.Single(r => r.ActorId == 0x54);
        // Equality identifies the ALREADY DERIVED external read. It never sets
        // the egg package length from the distance between registration roots.
        Require(nextOwner.Slot == 4 && nextOwner.RootPeteOffset == 0x33DE8 &&
            root.RootPayloadOffset + geometry.PackageByteLength == nextOwner.RootPayloadOffset &&
            geometry.FaceReadAheadSha256 == ReadAheadHash, "Discarded face read-ahead lost its independent class54 header owner.");
        var readAhead = new NativeSharedMobyReadAheadOwner(0x54, nextOwner.Slot, nextOwner.RootPeteOffset, 0, 8, ReadAheadHash);
        source.VerifyUnchanged();
        return new(source.SourceImageSha256, geometry, readAhead);
    }

    private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
