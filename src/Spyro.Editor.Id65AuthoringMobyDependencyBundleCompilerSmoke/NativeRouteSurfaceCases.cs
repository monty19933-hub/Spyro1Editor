using System.Buffers.Binary;
using Spyro.Editor.Core.Analysis;

internal static class NativeRouteSurfaceCases
{
    private static int _checks;
    public static void Run()
    {
        NativeRoutePoint[] route = [new(1024, 2048, 100), new(3072, 2048, 100)];
        NativeRouteSurfacePolicy policy = new(0, 128, 1024);
        var source = NativeRouteSurfaceSnapshot.Capture(Fixture(), 0x1000);
        var dry = NativeRouteSurfaceCertificate.Evaluate(source, route, policy);
        Require(dry.Accepted, "continuous dry corridor");
        Require(dry.CertificateSha256 == NativeRouteSurfaceCertificate.Evaluate(source, route, policy).CertificateSha256, "repeat");
        Require(dry.SupportBindings.Count > 0 && dry.Scope.Contains("not a physics", StringComparison.Ordinal), "bound scope");
        RejectReport(Fixture(flag: 1, descriptor: true), "SpecialSurface");
        var lifted = route.Select(p => p with { Z = p.Z + 307 }).ToArray();
        Require(!NativeRouteSurfaceCertificate.Evaluate(NativeRouteSurfaceSnapshot.Capture(Fixture(flag: 1, descriptor: true), 0x1000), lifted, policy).Accepted, "lifting origin does not erase route hazard");
        RejectCapture(Fixture(flag: 1), "descriptor");
        RejectReport(Fixture(zFlags: 0x4000), "UnsupportedZFlags");
        RejectReport(Fixture(zFlags: 0x8000), "UnsupportedZFlags");
        RejectReport(Fixture(flag: 0x7F), "UnsupportedFlagBits");
        RejectReport(Fixture(flag: 1, descriptor: true, surfaceType: 4), "SpecialSurface");
        RejectReport(Fixture(flag: 1, descriptor: true, surfaceType: 99), "SpecialSurface");
        RejectReport(Fixture(flag: 1, descriptor: true, surfaceType: -1), "SpecialSurface");
        RejectReport(Fixture(gap: true), "IncompleteSupport");
        RejectReport(Fixture(overlap: true), "OverlappingSupport");
        RejectReport(Fixture(missingReachability: true), "UnreachableSupport");
        RejectReport(Fixture(obstacle: true), "BlockedColumn");
        RejectReport(Fixture(slope: true), "UnsupportedSupportHeight");
        // Endpoints are dry: the extra water triangle intersects only the middle.
        RejectReport(Fixture(middleWater: true), "SpecialSurface");
        // Degenerate/zero-area vertical wall must not disappear in XY area tests.
        RejectReport(Fixture(wall: true), "BlockedColumn");
        Require(NativeRouteSurfaceCertificate.Evaluate(source, [route[0], route[0]], policy).Accepted, "stationary patch");
        Require(NativeRouteSurfaceCertificate.Evaluate(source, route.Reverse().ToArray(), policy).Accepted, "reverse corridor");
        Require(NativeRouteSurfaceCertificate.Evaluate(source, [new(1024, 1024, 0), new(3072, 3072, 0)], policy).Accepted, "diagonal swept square");
        foreach (bool water in new[]
        {
            false,
            true
        }

        )
        {
            var middle = NativeRouteSurfaceSnapshot.Capture(water ? Fixture(middleWater: true) : Fixture(gap: true), 0x1000);
            Require(route.All(p => NativeRouteSurfaceCertificate.Evaluate(middle, [p], policy).Accepted), "both endpoints alone miss " + (water ? "water" : "gap"));
        }

        byte[] ascending = Fixture();
        int cb = 16 + BinaryPrimitives.ReadInt32LittleEndian(ascending.AsSpan(12));
        int group = cb + BinaryPrimitives.ReadInt32LittleEndian(ascending.AsSpan(cb + 12));
        BinaryPrimitives.WriteUInt16LittleEndian(ascending.AsSpan(group), 0x8000);
        BinaryPrimitives.WriteUInt16LittleEndian(ascending.AsSpan(group + 2), 1);
        Require(NativeRouteSurfaceCertificate.Evaluate(NativeRouteSurfaceSnapshot.Capture(ascending, 0x1000), route, policy).Accepted, "preserved native nondescending group");
        byte[] implicitFlags = Fixture();
        BinaryPrimitives.WriteInt32LittleEndian(implicitFlags.AsSpan(cb + 4), 0);
        var implicitReport = NativeRouteSurfaceCertificate.Evaluate(NativeRouteSurfaceSnapshot.Capture(implicitFlags, 0x1000), route, policy);
        Require(implicitReport.Accepted && implicitReport.SupportBindings.All(b => b.Flag == 0xFF && b.FlagWadOffset == -1), "native beyond-count default FF");
        byte[] misaligned = Fixture();
        BinaryPrimitives.WriteInt32LittleEndian(misaligned.AsSpan(cb + 16), BinaryPrimitives.ReadInt32LittleEndian(misaligned.AsSpan(cb + 16)) + 1);
        RejectCapture(misaligned, "ownership");
        byte[] badGroup = Fixture();
        BinaryPrimitives.WriteUInt16LittleEndian(badGroup.AsSpan(group), 0x8002);
        RejectCapture(badGroup, "references");
        byte[] descriptorPointer = Fixture(descriptor: true);
        BinaryPrimitives.WriteInt32LittleEndian(descriptorPointer.AsSpan(20), 4);
        RejectCapture(descriptorPointer, "descriptor pointer");
        byte[] mutable = Fixture();
        var copied = NativeRouteSurfaceSnapshot.Capture(mutable, 0x1000);
        mutable[^1] ^= 1;
        Require(NativeRouteSurfaceCertificate.Evaluate(copied, route, policy).CertificateSha256 == dry.CertificateSha256, "detached source bytes");
        var routeCopy = route.ToArray();
        var frozen = NativeRouteSurfaceCertificate.Evaluate(source, routeCopy, policy);
        routeCopy[0] = new(0, 0, 0);
        Require(frozen.Route[0] == route[0] && frozen.CertificateSha256 == dry.CertificateSha256, "detached route report");
        Reject(() => NativeRouteSurfaceCertificate.Evaluate(source, route, new(0, 0, 1024)), "policy");
        Reject(() => NativeRouteSurfaceCertificate.Evaluate(source, [], policy), "route");
        Reject(() => NativeRouteSurfaceCertificate.Evaluate(source, [new(int.MaxValue, 0, 0)], policy), "coordinate");
        // Small hostile native buffers exercise limits before expanded arrays,
        // hex strings, or a combinatorial report are materialized.
        RejectCapture(DescriptorBudgetFixture(1, 4100), "descriptor byte budget");
        RejectCapture(DescriptorBudgetFixture(63, 2048), "descriptor byte budget");
        RejectCapture(IndexBudgetFixture(16, 16, 16, 512), "expanded-reference budget");
        RejectCapture(IndexBudgetFixture(256, 256, 2, 1), "logical-cell budget");
        RejectCapture(IndexBudgetFixture(9, 256, 0, 0, uniqueEmptySegments: true), "segment budget");
        RejectCapture(IndexBudgetFixture(256, 256, 0, 0), "segment-visit budget");
        var crowded = NativeRouteSurfaceSnapshot.Capture(Fixture(duplicates: 20), 0x1000);
        Require(!NativeRouteSurfaceCertificate.Evaluate(crowded, [route[0]], policy).Accepted, "one crowded segment fits bounded output");
        Reject(() => NativeRouteSurfaceCertificate.Evaluate(crowded, Enumerable.Repeat(route[0], 64).ToArray(), policy), "total issue output budget");
        var clips = new NativeRouteSurfaceWorkBudget();
        for (int i = 0; i < 100000; i++)
            clips.Clip(3);
        Reject(() => clips.Clip(3), "clipping budget");
        Reject(() => new NativeRouteSurfaceWorkBudget().Clip(65), "clipping budget");
        var pairs = new NativeRouteSurfaceWorkBudget();
        for (int i = 0; i < 20000; i++)
            pairs.Pair();
        Reject(pairs.Pair, "support-pair budget");
        var bindings = new NativeRouteSurfaceWorkBudget();
        for (int i = 0; i < 16384; i++)
            bindings.Binding();
        Reject(bindings.Binding, "binding output budget");
        var membership = new NativeRouteSurfaceWorkBudget();
        membership.Membership(2_000_000);
        Reject(() => membership.Membership(1), "membership lookup budget");
        byte[] byteType = Fixture(flag: 1, descriptor: true, surfaceType: 256);
        Require(NativeRouteSurfaceSnapshot.Capture(byteType, 0x1000).Triangles[1].Descriptor is { Type: 0, RawHex: "000100000000000000000000" }, "native descriptor consumer reads one byte");
        Console.WriteLine($"NATIVE_ROUTE_SURFACE_SMOKE_OK checks={_checks} continuous=true sourceFree=true physicsFootprint=false runtime=false");
        void RejectReport(byte[] bytes, string code)
        {
            var report = NativeRouteSurfaceCertificate.Evaluate(NativeRouteSurfaceSnapshot.Capture(bytes, 0x1000), route, policy);
            Require(!report.Accepted && report.Issues.Any(i => i.Code == code), code);
        }

        void RejectCapture(byte[] bytes, string text) => Reject(() => NativeRouteSurfaceSnapshot.Capture(bytes, 0x1000), text);
    }

    // Native model prefix: three empty components, special-surface component,
    // collision header, actual Z/Y/X tree, descending triangle group + sentinel,
    // packed native triangles, occlusion word and flag storage. No mock catalog.
    internal static byte[] Fixture(byte flag = 0x3F, bool descriptor = false, int surfaceType = 0, uint zFlags = 0, bool gap = false, bool overlap = false, bool missingReachability = false, bool obstacle = false, bool slope = false, bool middleWater = false, bool wall = false, int duplicates = 0)
    {
        var ts = new List<(int X, int Y, int Z, int Bx, int By, int Bz, int Cx, int Cy, int Cz, byte Flag, uint ZFlags)>
        {
            (0, 0, 0, 0, 248, 0, 248, 0, slope ? 16 : 0, 0x3F, 0),
            (248, 248, 0, 248, 0, 0, gap ? 16 : 0, 248, 0, flag, zFlags)
        };
        if (overlap)
            ts.Add(ts[0]);
        for (int i = 0; i < duplicates; i++)
            ts.Add(ts[0]);
        if (obstacle)
            ts.Add((120, 110, 16, 120, 145, 16, 140, 110, 16, 0x3F, 0));
        if (middleWater)
        {
            descriptor = true;
            ts.Add((120, 110, 0, 120, 145, 0, 140, 110, 0, 1, 0));
        }

        if (wall)
            ts.Add((128, 0, 0, 128, 248, 0, 128, 128, 64, 0x3F, 0));
        int specialLength = descriptor ? 40 : 8, co = 12 + specialLength, body = co + 4;
        int tree = body + 28, blocks = tree + 12, blockCount = (missingReachability ? ts.Count - 1 : ts.Count) + 1;
        int tri = (blocks + blockCount * 2 + 3) & ~3, occlusion = tri + ts.Count * 12, flags = occlusion + 4, total = (flags + ts.Count + 3) & ~3;
        byte[] b = new byte[total];
        U32(0, 4);
        U32(4, 4);
        U32(8, 4);
        U32(12, specialLength);
        U32(16, descriptor ? 2 : 0);
        if (descriptor)
        {
            U32(20, 12);
            U32(24, 24);
            U32(28, 0);
            U32(40, surfaceType);
        }

        U32(co, total - co);
        U32(body, ts.Count);
        U32(body + 4, ts.Count);
        U32(body + 8, tree - body);
        U32(body + 12, blocks - body);
        U32(body + 16, tri - body);
        U32(body + 20, occlusion - body);
        U32(body + 24, flags - body);
        foreach (var(o, v)in new[]
        {
            (0, 1),
            (2, 4),
            (4, 1),
            (6, 8),
            (8, 1),
            (10, 0)
        }

        )
            U16(tree + o, v);
        int n = missingReachability ? ts.Count - 1 : ts.Count;
        for (int i = 0; i < n; i++)
            U16(blocks + i * 2, (n - 1 - i) | (i == 0 ? 0x8000 : 0));
        U16(blocks + n * 2, 0x8000);
        for (int i = 0; i < ts.Count; i++)
        {
            var t = ts[i];
            uint x = (uint)t.X | ((uint)(t.Bx - t.X) & 511) << 14 | ((uint)(t.Cx - t.X) & 511) << 23;
            uint y = (uint)t.Y | ((uint)(t.By - t.Y) & 511) << 14 | ((uint)(t.Cy - t.Y) & 511) << 23;
            uint z = (uint)t.Z | t.ZFlags | (uint)(t.Bz - t.Z) << 16 | (uint)(t.Cz - t.Z) << 24;
            U32(tri + i * 12, unchecked((int)x));
            U32(tri + i * 12 + 4, unchecked((int)y));
            U32(tri + i * 12 + 8, unchecked((int)z));
            b[flags + i] = t.Flag;
        }

        return b;
        void U32(int o, int v) => BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(o), v);
        void U16(int o, int v) => BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(o), (ushort)v);
    }

    private static byte[] DescriptorBudgetFixture(int count, int rawLength)
    {
        byte[] source = Fixture();
        int specialLength = 8 + count * 4 + rawLength;
        byte[] bytes = new byte[source.Length + specialLength - 8];
        source.AsSpan(0, 12).CopyTo(bytes);
        source.AsSpan(20).CopyTo(bytes.AsSpan(12 + specialLength));
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(12), specialLength);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16), count);
        for (int i = 0; i < count; i++)
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(20 + i * 4), 4 + count * 4);
        return bytes;
    }

    private static byte[] IndexBudgetFixture(int zCount, int yCount, int xCount, int groupSize, bool uniqueEmptySegments = false)
    {
        byte[] source = Fixture();
        const int body = 24;
        const int tree = body + 28;
        int yStart = (zCount + 1) * 2;
        int yTables = uniqueEmptySegments ? zCount : 1;
        int xStart = yStart + yTables * (yCount + 1) * 2;
        int treeLength = xStart + (uniqueEmptySegments ? zCount * yCount * 2 : (xCount + 1) * 2);
        int blocks = tree + treeLength;
        int triangles = (blocks + (groupSize + 1) * 2 + 3) & ~3;
        int occlusion = triangles + 24;
        int flags = occlusion + 4;
        byte[] bytes = new byte[flags + 4];
        source.AsSpan(0, tree).CopyTo(bytes);
        source.AsSpan(72, 24).CopyTo(bytes.AsSpan(triangles));
        Write32(20, bytes.Length - 20);
        Write32(body + 12, blocks - body);
        Write32(body + 16, triangles - body);
        Write32(body + 20, occlusion - body);
        Write32(body + 24, flags - body);
        Write16(tree, zCount);
        for (int z = 0; z < zCount; z++)
            Write16(tree + (z + 1) * 2, yStart + (uniqueEmptySegments ? z * (yCount + 1) * 2 : 0));
        for (int z = 0; z < yTables; z++)
        {
            int yOffset = yStart + z * (yCount + 1) * 2;
            Write16(tree + yOffset, yCount);
            for (int y = 0; y < yCount; y++)
                Write16(tree + yOffset + (y + 1) * 2, xStart + (uniqueEmptySegments ? (z * yCount + y) * 2 : 0));
        }

        if (!uniqueEmptySegments)
            Write16(tree + xStart, xCount);
        Write16(blocks, 0x8000);
        Write16(blocks + groupSize * 2, 0x8000);
        bytes[flags] = bytes[flags + 1] = 0x3F;
        return bytes;
        void Write32(int offset, int value) => BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset), value);
        void Write16(int offset, int value) => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), checked((ushort)value));
    }

    private static void Reject(Action action, string message)
    {
        try
        {
            action();
        }
        catch (InvalidDataException e)when (e.Message.Contains(message, StringComparison.OrdinalIgnoreCase))
        {
            _checks++;
            return;
        }

        throw new Exception("Expected rejection: " + message);
    }

    private static void Require(bool value, string message)
    {
        if (!value)
            throw new Exception("Route surface regression: " + message);
        _checks++;
    }
}
