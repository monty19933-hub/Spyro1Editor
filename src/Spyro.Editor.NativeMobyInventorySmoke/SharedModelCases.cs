using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;

// Source-free discovery grammar and exact-source assertions. The caller owns
// image capture; these cases never open an image, write files or issue a model
// package, placement or export certificate. Increasing nonaliased roots are a
// premise of this observed USA layout, not a claim about all PETE formats.
internal static class SharedModelCases
{
    internal static void Run(Action<bool, string> check)
    {
        var (header, payload) = Fixture();
        byte[] originalHeader = header.ToArray(), originalPayload = payload.ToArray();
        var shared = NativeSharedMobySourceInventory.Decode(header, payload);
        check(shared.HeaderByteLength == 0x800 && shared.RuntimeModelDataByteLength == 0x80,
            "Shared discovery retains the complete header and runtime-copy owner lengths.");
        check(shared.HeaderSha256 == Hash(header) && shared.RuntimeModelDataSha256 == Hash(payload),
            "Shared discovery hashes exact full owners, including bytes outside registration roots.");
        check(shared.Registrations.Count == 3 &&
              shared.Registrations.Select(root => root.Slot).SequenceEqual(new[] { 0, 1, 2 }) &&
              shared.Registrations.Select(root => root.ActorId).SequenceEqual(new ushort[] { 0, 0x22, 0xAD }),
            "Shared roots preserve native slot order and distinct class identity.");
        check(shared.Registrations[1].RootPeteOffset == 0x820 && shared.Registrations[1].RootPayloadOffset == 0x20,
            "PETE-relative and runtime-copy-relative root offsets are distinct address domains.");
        check(!shared.NativeImportApproved && !shared.ModelPackageExtentsProven,
            "A shared registration never grants placement, import or inferred package ownership.");
        check(shared.CopyHeader().SequenceEqual(originalHeader) &&
              shared.CopyRuntimeModelData().SequenceEqual(originalPayload),
            "Shared inventory exposes complete source copies without dropping unknown bytes.");

        header[0x700] ^= 0x80;
        payload[^1] ^= 0x80;
        Put(header, 4 + 8, 0x830);
        check(shared.HeaderSha256 == Hash(originalHeader) &&
              shared.RuntimeModelDataSha256 == Hash(originalPayload) &&
              shared.Registrations[1].RootPeteOffset == 0x820 &&
              shared.CopyHeader().SequenceEqual(originalHeader) &&
              shared.CopyRuntimeModelData().SequenceEqual(originalPayload),
            "Mutating caller arrays cannot change a captured shared inventory.");
        byte[] exposedHeader = shared.CopyHeader(), exposedPayload = shared.CopyRuntimeModelData();
        exposedHeader[4] ^= 0xFF;
        exposedPayload[0] ^= 0xFF;
        check(shared.CopyHeader().SequenceEqual(originalHeader) &&
              shared.CopyRuntimeModelData().SequenceEqual(originalPayload),
            "Returned arrays are defensive copies, not writable inventory owners.");
        bool immutableRoots = false;
        try { ((IList<NativeSharedMobySourceRegistration>)shared.Registrations).Clear(); }
        catch (NotSupportedException) { immutableRoots = true; }
        check(immutableRoots && shared.Registrations.Count == 3,
            "The registration collection cannot be edited through a mutable interface.");
        var repeated = NativeSharedMobySourceInventory.Decode(originalHeader, originalPayload);
        check(repeated.HeaderSha256 == shared.HeaderSha256 &&
              repeated.RuntimeModelDataSha256 == shared.RuntimeModelDataSha256 &&
              repeated.Registrations.SequenceEqual(shared.Registrations),
            "Repeated decoding has deterministic complete-owner identity.");

        Reject([], originalPayload, "Empty shared header is not a table.");
        Reject(originalHeader[..0x203], originalPayload, "Truncated registration table refuses.");
        Reject(originalHeader[..^1], originalPayload, "Partial shared header refuses.");
        Reject([.. originalHeader, 0], originalPayload, "Unowned header suffix refuses.");
        Reject(originalHeader, [], "Missing runtime-copy owner refuses.");
        Reject(originalHeader, originalPayload[..^1], "Truncated runtime-copy owner refuses.");
        Reject(originalHeader, [.. originalPayload, 0, 0, 0, 0], "Unowned runtime-copy suffix refuses.");
        HeaderMutation(0, 0, "Zero native runtime-copy length refuses.");
        HeaderMutation(0, uint.MaxValue, "Unsigned native copy-length overflow refuses.");
        HeaderMutation(0, 0x800004, "Native copy budget overflow refuses before reading roots.");
        HeaderMutation(0, 0x7F, "Native copy length must remain aligned and exact.");
        HeaderMutation(4, 0, "Missing shared player root refuses.");
        HeaderMutation(8, 1, "Shared slot zero cannot impersonate a nonplayer class.");
        HeaderMutation(4, 0x804, "Shared player root retains the observed payload-start ownership.");
        HeaderMutation(4 + 8, 0x7FC, "Shared root cannot address the PETE header.");
        HeaderMutation(4 + 8, 0x821, "Unaligned shared root refuses.");
        HeaderMutation(4 + 8, 0x880, "One-past-copy shared root refuses.");
        HeaderMutation(4 + 8, 0x874, "A root with fewer than sixteen owned header bytes refuses.");
        HeaderMutation(4 + 8, uint.MaxValue, "Signed or overflowing shared root refuses.");
        HeaderMutation(4 + 8, 0x800, "Aliased shared roots are not silently collapsed into inferred packages.");
        HeaderMutation(4 + 8 * 2, 0x810, "Unordered shared roots need another source grammar.");
        HeaderMutation(8 + 8, 512, "Shared class must fit the actual 512-pointer model table.");
        HeaderMutation(8 + 8, uint.MaxValue, "Oversized shared class refuses without truncation.");
        HeaderMutation(8 + 8 * 2, 0x22, "Duplicate shared class cannot overwrite an earlier registration.");
        HeaderMutation(8 + 8, 0, "Shared player class cannot be registered a second time.");
        HeaderMutation(8 + 8 * 3, 0x77, "A null shared root cannot retain a hidden class.");
        HeaderMutation(4 + 8 * 4, 0x860, "A registration beyond the first terminator refuses.");

        var (nearHeader, nearPayload) = Fixture();
        Put(nearHeader, 4 + 8, 0x804);
        Put(nearHeader, 4 + 16, 0x808);
        var nearby = NativeSharedMobySourceInventory.Decode(nearHeader, nearPayload);
        check(nearby.Registrations.Count == 3 && !nearby.ModelPackageExtentsProven,
            "Nearby roots do not invent four-byte packages from adjacent offsets.");
        Put(nearHeader, 4 + 16, 0x870);
        check(NativeSharedMobySourceInventory.Decode(nearHeader, nearPayload).Registrations[2].RootPayloadOffset == 0x70,
            "A final root with exactly sixteen readable bytes remains discovery-only.");

        byte[] fullHeader = new byte[0x800], fullPayload = new byte[64 * 16];
        Put(fullHeader, 0, (uint)fullPayload.Length);
        for (int slot = 0; slot < 64; slot++)
        {
            Put(fullHeader, 4 + slot * 8, (uint)(0x800 + slot * 16));
            Put(fullHeader, 8 + slot * 8, (uint)slot);
        }
        Reject(fullHeader, fullPayload, "An unterminated table cannot borrow a sentinel from unowned header padding.");
        check(originalHeader.SequenceEqual(shared.CopyHeader()) && originalPayload.SequenceEqual(shared.CopyRuntimeModelData()),
            "Every malformed input leaves the earlier immutable inventory unchanged.");

        void HeaderMutation(int offset, uint value, string message)
        {
            byte[] changed = originalHeader.ToArray();
            Put(changed, offset, value);
            Reject(changed, originalPayload, message);
        }

        void Reject(byte[] candidateHeader, byte[] candidatePayload, string message)
        {
            bool rejected = false;
            try { _ = NativeSharedMobySourceInventory.Decode(candidateHeader, candidatePayload); }
            catch (InvalidDataException) { rejected = true; }
            check(rejected, message);
        }
    }

    internal static void VerifySource(NativeSharedMobySourceInventory shared, Action<bool, string> check)
    {
        check(shared.HeaderByteLength == 0x800 && shared.RuntimeModelDataByteLength == 0x39C48,
            "Original PETE discovery retains the full native copy, including the declared-entry overhang.");
        check(shared.HeaderSha256 == "b9a4de193b5cf0bd9592de15c57aa5daa3713c3575026154b45402f3aceef6f1" &&
              shared.RuntimeModelDataSha256 == "1703ac37e59e480909b6f66dc856d4e5f3ce80551f18cbc8c06129c3b54dd35d",
            "Shared source owners equal the independently frozen native model witness.");
        check(shared.Registrations.Count == 54 && shared.Registrations[0] == new NativeSharedMobySourceRegistration(0, 0, 0x800),
            "Original source has exactly fifty-four shared registrations and its native player root.");
        check(shared.Registrations[3] == new NativeSharedMobySourceRegistration(3, 0x22, 0x3382C) &&
              shared.Registrations[10] == new NativeSharedMobySourceRegistration(10, 0xAD, 0x34A8C),
            "Retail Dragon Egg and Key retain their distinct native shared root identities.");
        check(shared.Registrations.All(root => root.ActorId < 512) &&
              shared.Registrations.Select(root => root.ActorId).Distinct().Count() == 54,
            "All actual shared classes remain unique in the native model table domain.");
        check(shared.CopyRuntimeModelData().Length + 0x800 == 0x3A448 &&
              Hash(shared.CopyHeader()) == shared.HeaderSha256 &&
              Hash(shared.CopyRuntimeModelData()) == shared.RuntimeModelDataSha256,
            "Copied source owners retain all 0x448 bytes beyond the declared PETE entry.");
        check(!shared.NativeImportApproved && !shared.ModelPackageExtentsProven,
            "Exact original shared discovery does not certify unused identities or portable model packages.");
    }

    private static (byte[] Header, byte[] Payload) Fixture()
    {
        byte[] header = new byte[0x800], payload = Enumerable.Range(0, 0x80).Select(i => (byte)i).ToArray();
        Put(header, 0, (uint)payload.Length);
        Put(header, 4, 0x800);
        Put(header, 12, 0x820); Put(header, 16, 0x22);
        Put(header, 20, 0x840); Put(header, 24, 0xAD);
        header[0x700] = 0xA6; // Unknown header bytes are retained, not declared free storage.
        return (header, payload);
    }

    private static void Put(byte[] bytes, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
