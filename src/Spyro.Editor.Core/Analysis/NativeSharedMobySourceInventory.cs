using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Spyro.Editor.Core.Analysis;

/// <summary>
/// A shared model registration, not an owned model-package interval. The PETE
/// root includes the 0x800-byte header; the copied model payload does not.
/// </summary>
public sealed record NativeSharedMobySourceRegistration(int Slot, ushort ActorId, uint RootPeteOffset)
{
    public int RootPayloadOffset => checked((int)RootPeteOffset - 0x800);
}

/// <summary>
/// Immutable, inspect-only discovery of the observed USA shared PETE layout.
/// The internal decoder establishes bounded source bytes, not original-image
/// identity, unused-object identity, model-package extents or import authority.
/// Exact-image capture must bind this snapshot to its containing disc owner.
/// </summary>
public sealed class NativeSharedMobySourceInventory
{
    private const int PeteHeaderBytes = 0x800;
    private const int RegistrationSlots = 64;
    private const int MinimumRootHeaderBytes = 16;
    private const int MaximumRuntimeCopyBytes = 0x800000;
    private readonly byte[] _header;
    private readonly byte[] _runtimeModelData;

    private NativeSharedMobySourceInventory(byte[] header, byte[] runtimeModelData,
        IEnumerable<NativeSharedMobySourceRegistration> registrations)
    {
        _header = header;
        _runtimeModelData = runtimeModelData;
        HeaderSha256 = Hash(header);
        RuntimeModelDataSha256 = Hash(runtimeModelData);
        Registrations = Array.AsReadOnly(registrations.ToArray());
    }

    public int HeaderByteLength => _header.Length;
    public int RuntimeModelDataByteLength => _runtimeModelData.Length;
    public string HeaderSha256 { get; }
    public string RuntimeModelDataSha256 { get; }
    public IReadOnlyList<NativeSharedMobySourceRegistration> Registrations { get; }
    public bool NativeImportApproved => false;
    public bool ModelPackageExtentsProven => false;
    public byte[] CopyHeader() => _header.ToArray();
    public byte[] CopyRuntimeModelData() => _runtimeModelData.ToArray();

    internal static NativeSharedMobySourceInventory Decode(ReadOnlySpan<byte> peteHeader,
        ReadOnlySpan<byte> runtimeModelData)
    {
        if (peteHeader.Length != PeteHeaderBytes ||
            runtimeModelData.Length < MinimumRootHeaderBytes ||
            runtimeModelData.Length > MaximumRuntimeCopyBytes || runtimeModelData.Length % 4 != 0)
            throw new InvalidDataException("Shared PETE discovery requires one complete header and a bounded aligned runtime-copy owner.");

        // Decode and hash the same detached snapshots. The complete native copy
        // is the owner; the retail declared WAD entry is 0x448 bytes shorter.
        byte[] header = peteHeader.ToArray(), payload = runtimeModelData.ToArray();
        if (Word(header, 0) != payload.Length)
            throw new InvalidDataException("Shared PETE runtime-copy length does not match the complete supplied owner.");

        List<NativeSharedMobySourceRegistration> roots = [];
        HashSet<ushort> classes = [];
        bool terminated = false;
        uint previousRoot = 0;
        for (int slot = 0; slot < RegistrationSlots; slot++)
        {
            int field = 4 + slot * 8;
            uint root = Word(header, field), actor = Word(header, field + 4);
            if (root == 0)
            {
                terminated = true;
                if (actor != 0)
                    throw new InvalidDataException("A shared PETE terminator retains an unexplained class ID.");
                continue;
            }

            // Strict ordering and distinct roots are premises of the observed
            // USA layout, not claims about every possible PETE format. Even
            // here, adjacent roots do not establish individual package lengths.
            if (terminated || actor >= 512 || root < PeteHeaderBytes || root % 4 != 0 ||
                root <= previousRoot || root + (long)MinimumRootHeaderBytes > PeteHeaderBytes + (long)payload.Length ||
                !classes.Add((ushort)actor))
                throw new InvalidDataException("Shared PETE registration is hidden, aliased, unordered or outside its class/copy owner.");

            roots.Add(new NativeSharedMobySourceRegistration(slot, (ushort)actor, root));
            previousRoot = root;
        }

        if (!terminated || roots.Count == 0 ||
            roots[0] != new NativeSharedMobySourceRegistration(0, 0, PeteHeaderBytes))
            throw new InvalidDataException("Observed shared PETE layout requires its player root and an owned table terminator.");

        return new NativeSharedMobySourceInventory(header, payload, roots);
    }

    private static uint Word(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
