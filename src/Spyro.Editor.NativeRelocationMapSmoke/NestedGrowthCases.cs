using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;

internal static class NestedGrowthCases
{
    internal static void Run(Action<bool, string> check)
    {
        byte[] source = Fixture();
        byte[] saved = source.ToArray();
        var requests = new[] { Payload(1, 0x400, 0, 0x20), Payload(2, 0x800, 0, 0x28), Payload(3, 0x400, 0, 0x14) };
        byte[] result = Expand(source, requests, 0x1800);
        check(source.SequenceEqual(saved), "Multi-subfile growth mutated its original input.");
        check(result.Length == source.Length + 0x1800, "Independent subfile rounding lost sector alignment.");
        // Independent literal splice oracle: each subfile gets its own sector,
        // including zero fill after the final source byte, then the next begins.
        byte[] expected = new byte[source.Length + 0x1800];
        source.AsSpan(0, 0x1000).CopyTo(expected);
        source.AsSpan(0x1000, 0x400).CopyTo(expected.AsSpan(0x1000));
        requests[0].Bytes.CopyTo(expected, 0x1400);
        source.AsSpan(0x1400, 0x400).CopyTo(expected.AsSpan(0x1420));
        source.AsSpan(0x1800, 0x800).CopyTo(expected.AsSpan(0x2000));
        requests[1].Bytes.CopyTo(expected, 0x2800);
        source.AsSpan(0x2000, 0x400).CopyTo(expected.AsSpan(0x3000));
        requests[2].Bytes.CopyTo(expected, 0x3400);
        source.AsSpan(0x2400, 0x400).CopyTo(expected.AsSpan(0x3414));
        Word(expected, 0x0C, 0x1000); Word(expected, 0x10, 0x2000); Word(expected, 0x14, 0x1000);
        Word(expected, 0x18, 0x3000); Word(expected, 0x1C, 0x1000);
        Word(expected, 0x50, 0x2000); Word(expected, 0x54, 0x2100);
        check(result.SequenceEqual(expected), "Multi-subfile output differs from the independent splice/header oracle.");
        check(Expand(source, requests.Reverse().ToArray(), 0x1800).SequenceEqual(expected), "Payload order changed the composed output.");
        Reject(source, requests, 0x800, "sum of exact deltas rounded only once");
        Reject(source, requests, 0x2000, "excess growth");
        foreach ((int field, uint value) in new (int, uint)[] {
            (0x1D0, 0x2100), (0x0C, 0x804), (0x10, 0x1804), (0x24, 0x800),
            (0x50, 0), (0x54, 0x1800), (0x54, 0x1400), (0x54, 0x2000), (0x54, 0x1901),
            (0x58, 0), (0x154, 1) })
        {
            byte[] malformed = source.ToArray(); Word(malformed, field, value);
            if (field == 0x58) Word(malformed, 0x5C, 0x1A00);
            Reject(malformed, requests, 0x1800, $"malformed native header field {field:X}");
        }
        foreach (Request bad in new[] {
            requests[0] with { ModelBlockOffset = -4 }, requests[0] with { ModelBlockOffset = 3 },
            requests[0] with { OriginalLength = -4 }, requests[0] with { OriginalLength = 0x900 },
            requests[0] with { OriginalLength = 0x24 }, requests[0] with { Bytes = new byte[3] },
            requests[0] with { SubfileIndex = 10 }, requests[0] with { RequireLengthPrefix = true },
            requests[0] with { OriginalLength = 4, RequireLengthPrefix = true } })
            Reject(source, [bad, requests[1]], 0x1000, "malformed payload");
        Reject(source, [requests[0], requests[0] with { PatchIndex = 7 }, requests[1]], 0x1000, "duplicate zero-length insertion point");
        Reject(source, [requests[0], requests[1] with { PatchIndex = 1 }], 0x1000, "duplicate payload identity");
        Reject(source, [requests[0] with { OriginalLength = 0x20 }, requests[0] with { PatchIndex = 7, ModelBlockOffset = 0x410 }, requests[1]], 0x1000, "overlapping source extents");
        Reject(source, [requests[0], requests[1] with { ModelBlockOffset = 0xFC, OriginalLength = 8 }], 0x1000, "root intersecting replaced package bytes");
        // Insertion at model start moves existing roots, but the model
        // descriptor continues to identify the start of its complete output.
        byte[] prefix = Expand(source, [requests[0], requests[1] with { ModelBlockOffset = 0 }], 0x1000);
        check(Word(prefix, 0x10) == 0x2000 && Word(prefix, 0x50) == 0x2028 && Word(prefix, 0x54) == 0x2128,
            "Model-prefix insertion confused the subfile anchor with an existing root target.");
        foreach ((int dataBytes, int modelBytes, int sceneBytes) in new[] {
            (4, 4, 4), (0x7FC, 0x800, 0x804), (0x804, 0x7FC, 0x800),
            (0x8000, 0x10004, 0x4004), (0x100000, 0x20000, 0x10000) })
        {
            int[] sizes = [dataBytes, modelBytes, sceneBytes];
            int[] sectors = sizes.Select(n => (n + 2047) / 2048).ToArray();
            Request[] appends = sizes.Select((n, i) => Payload(i + 1, 0x800, 0, n)).ToArray();
            byte[] expanded = Expand(source, appends, sectors.Sum() * 2048);
            int extra = 0;
            for (int subfile = 0; subfile < 4; subfile++)
            {
                int start = 0x800 + subfile * 0x800;
                check(source.AsSpan(start, 0x800).SequenceEqual(expanded.AsSpan(start + extra, 0x800)), "Large simultaneous growth lost a source subfile.");
                check(Word(expanded, subfile * 8) == start + extra, "Large simultaneous growth lost a descriptor offset.");
                if (subfile != 0)
                {
                    check(expanded.AsSpan(start + extra + 0x800, sizes[subfile - 1]).SequenceEqual(appends[subfile - 1].Bytes),
                        "Large simultaneous growth lost an appended payload.");
                    extra += sectors[subfile - 1] * 2048;
                }
            }
            check(Word(expanded, 0x50) == 0x1800 + sectors[0] * 2048 && Word(expanded, 0x54) == 0x1900 + sectors[0] * 2048,
                "Large simultaneous growth mapped roots with another subfile's delta.");
        }

        void Reject(byte[] bytes, Request[] changes, int growth, string reason)
        {
            byte[] before = bytes.ToArray(); bool rejected = false;
            try { _ = Expand(bytes, changes, growth); } catch (Exception e) when (e is InvalidDataException or OverflowException or InvalidOperationException) { rejected = true; }
            check(rejected && bytes.SequenceEqual(before), $"Multi-subfile expansion accepted {reason}, or changed its source on refusal.");
        }
    }

    internal static void SingleSubfileBaseline()
    {
        foreach (int subfile in Enumerable.Range(0, 4))
        {
            byte[] bytes = Fixture();
            byte[] result = Expand(bytes, [Payload(subfile, 0x800, 0, 0x24)], 0x800);
            Console.WriteLine($"single-subfile-{subfile}={Convert.ToHexString(SHA256.HashData(result)).ToLowerInvariant()}");
        }
    }

    internal static void RunSource(string path, Action<bool, string> check)
    {
        const string sourceHash = "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37";
        using FileStream source = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        check(source.Length == 661547040 && Convert.ToHexString(SHA256.HashData(source)).ToLowerInvariant() == sourceHash,
            "Native header audit requires the exact original USA BIN.");
        byte[] wad = ReadLogical(0, 0x800);
        using IncrementalHash headersHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        for (int index = 10; index <= 78; index += 2)
        {
            int entryOffset = checked((int)Word(wad, index * 8));
            byte[] entry = ReadLogical(entryOffset, checked((int)Word(wad, index * 8 + 4)));
            headersHash.AppendData(entry, 0, 0x800);
            Request[] changes = [Payload(1, checked((int)Word(entry, 0x0C)), 0, 0x20),
                Payload(2, checked((int)Word(entry, 0x14)), 0, 0x28), Payload(3, checked((int)Word(entry, 0x1C)), 0, 0x14)];
            byte[] result = Expand(entry, changes, 0x1800);
            int cumulative = 0;
            for (int s = 0; s < 10; s++)
            {
                int offset = checked((int)Word(entry, s * 8)), length = checked((int)Word(entry, s * 8 + 4));
                if (offset == 0) break;
                int added = s is >= 1 and <= 3 ? 0x800 : 0;
                check(Word(result, s * 8) == offset + cumulative && Word(result, s * 8 + 4) == length + added,
                    $"Entry {index} descriptor {s} failed independent cumulative relocation.");
                check(entry.AsSpan(offset, length).SequenceEqual(result.AsSpan(offset + cumulative, length)),
                    $"Entry {index} subfile {s} source complement changed.");
                if (added != 0)
                {
                    byte[] payload = changes.Single(p => p.SubfileIndex == s).Bytes;
                    check(result.AsSpan(offset + cumulative + length, payload.Length).SequenceEqual(payload) &&
                        result.AsSpan(offset + cumulative + length + payload.Length, added - payload.Length).IndexOfAnyExcept((byte)0) < 0,
                        $"Entry {index} subfile {s} append/padding differs.");
                }
                cumulative += added;
            }
            for (int root = 0; root < 64; root++)
            {
                uint before = Word(entry, 0x50 + root * 4);
                check(Word(result, 0x50 + root * 4) == (before == 0 ? 0 : before + 0x800), $"Entry {index} root {root} incorrectly mapped.");
            }
            check(entry.AsSpan(0x150, 0x6B0).SequenceEqual(result.AsSpan(0x150, 0x6B0)), $"Entry {index} IDs/reserved header changed.");
            check(Expand(entry, changes.Reverse().ToArray(), 0x1800).SequenceEqual(result), $"Entry {index} output is order dependent.");
        }
        string hash = Convert.ToHexString(headersHash.GetHashAndReset()).ToLowerInvariant();
        check(hash == "ede176a1ed8158ba18bcbb3034567950a55e2d929eb9cf2d24eb170e1ce25ba9", "The 35 native header inventory hash changed.");
        Console.WriteLine($"native-header-count=35 combined-sha256={hash}");

        byte[] ReadLogical(int offset, int length)
        {
            byte[] bytes = new byte[length];
            for (int cursor = 0; cursor < length;)
            {
                int take = Math.Min(length - cursor, 0x800 - offset % 0x800);
                source.Position = (37L + offset / 0x800) * 2352 + 24 + offset % 0x800;
                source.ReadExactly(bytes.AsSpan(cursor, take)); cursor += take; offset += take;
            }
            return bytes;
        }
    }

    internal sealed record Request(int PatchIndex, int WadLba, long OriginalWadOffset, int StorageWadEntry,
        int ModelBlockOffset, int OriginalLength, byte[] Bytes, int SubfileIndex, bool RequireLengthPrefix);
    internal static Request Payload(int subfile, int offset, int length, int bytes) =>
        new(subfile, 37, 0x800 + 0x800 + subfile * 0x800 + offset, 10, offset, length,
            Enumerable.Repeat((byte)(0xC0 + subfile), bytes).ToArray(), subfile, false);
    internal static byte[] Fixture()
    {
        byte[] bytes = new byte[0x2800];
        for (int i = 0; i < 4; i++) { Word(bytes, i * 8, (uint)(0x800 + i * 0x800)); Word(bytes, i * 8 + 4, 0x800); }
        Word(bytes, 0x50, 0x1800); Word(bytes, 0x54, 0x1900);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x152), 0x01F4);
        for (int i = 0x800; i < bytes.Length; i++) bytes[i] = (byte)(i % 251);
        return bytes;
    }
    internal static byte[] Expand(byte[] bytes, Request[] requests, int growth)
    {
        Assembly core = typeof(NativeTerrainTextureRecordAppendBuilder).Assembly;
        Type payloadType = core.GetType("Spyro.Editor.Core.Exporting.NativeSkyRelocationPayload", true)!;
        Array payloads = Array.CreateInstance(payloadType, requests.Length);
        for (int i = 0; i < requests.Length; i++) payloads.SetValue(JsonSerializer.Deserialize(JsonSerializer.Serialize(requests[i]), payloadType), i);
        try { return (byte[])core.GetType("Spyro.Editor.Core.Exporting.NativeSkyWadRelocator", true)!
            .GetMethod("ExpandNestedLevelEntry", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [bytes, payloads, growth])!; }
        catch (TargetInvocationException exception) when (exception.InnerException != null)
        { ExceptionDispatchInfo.Capture(exception.InnerException).Throw(); throw; }
    }
    internal static void Word(byte[] bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
    internal static uint Word(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset));
}
