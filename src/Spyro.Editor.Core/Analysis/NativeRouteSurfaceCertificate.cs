using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Analysis;

internal sealed record NativeRoutePoint(int X, int Y, int Z);
internal sealed record NativeRouteSurfacePolicy(int GroundZ, int AuthoredHalfWidth, int ClearHeight);
internal sealed record NativeRouteSurfaceDescriptor(int Index, int Type, int? Param1, int? Param2, long WadOffset, string RawHex)
{
    public string Meaning => Type >= 0 && Param1 is { } p1 && Param2 is { } p2
        ? new NativeTerrainSurfaceSignature(Type, p1, p2).Label
        : "Unsupported or incomplete native descriptor encoding";
}

internal sealed record NativeRouteSurfaceTriangle(int Index,
    NativeRoutePoint A, NativeRoutePoint B, NativeRoutePoint C, byte Flag, uint ZFlags,
    long WadOffset, long FlagWadOffset, NativeRouteSurfaceDescriptor? Descriptor);

/// <summary>Detached native bytes, not a source-ROM or caller-ownership certificate.</summary>
internal sealed class NativeRouteSurfaceSnapshot
{
    private NativeRouteSurfaceSnapshot(string sha, long wad, int length,
        long collision, string collisionSha, NativeCollisionIndexSnapshot index,
        long tree, long blocks, NativeRouteSurfaceTriangle[] triangles)
    {
        ModelSha256 = sha;
        ModelWadOffset = wad;
        ModelByteLength = length;
        CollisionWadOffset = collision;
        CollisionSha256 = collisionSha;
        Index = index;
        TreeWadOffset = tree;
        BlocksWadOffset = blocks;
        Triangles = Array.AsReadOnly(triangles);
    }

    public string ModelSha256 { get; }
    public long ModelWadOffset { get; }
    public int ModelByteLength { get; }
    public long CollisionWadOffset { get; }
    public string CollisionSha256 { get; }
    public IReadOnlyList<NativeRouteSurfaceTriangle> Triangles { get; }
    internal NativeCollisionIndexSnapshot Index { get; }
    internal long TreeWadOffset { get; }
    internal long BlocksWadOffset { get; }

    public static NativeRouteSurfaceSnapshot Capture(byte[] model, long modelWadOffset)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (modelWadOffset < 0 || modelWadOffset > long.MaxValue - model.Length || model.Length > 16 * 1024 * 1024)
            throw new InvalidDataException("Native route model address/length is outside bounded source storage.");
        byte[] b = model.ToArray();
        int offset = 0;
        for (int i = 0; i < 3; i++)
            offset += Length(offset, 4);
        int special = offset;
        int specialLength = Length(special, 8);
        int body = special + 4;
        int count = I32(body);
        if (count is < 0 or > 63 || 8L + count * 4L > specialLength)
            Fail("special descriptor count");
        int[] pointers = new int[count];
        for (int i = 0; i < count; i++)
        {
            int p = I32(body + 4 + i * 4);
            if (p < 4 + count * 4 || (p & 3) != 0 || p > (long)specialLength - 8)
                Fail("special descriptor pointer");
            pointers[i] = body + p;
        }

        var descriptors = new NativeRouteSurfaceDescriptor[count];
        int descriptorBytes = 0;
        for (int i = 0; i < count; i++)
        {
            int p = pointers[i];
            int end = pointers.Where(v => v > p).DefaultIfEmpty(special + specialLength).Min();
            descriptorBytes += end - p;
            if (end - p > NativeRouteSurfaceBudget.MaximumDescriptorBytes ||
                descriptorBytes > NativeRouteSurfaceBudget.MaximumAggregateDescriptorBytes)
                Fail("descriptor byte budget");
            // Retail EXE 0x800573BC uses lbu at descriptor+0. Upper bytes are
            // retained as evidence, never folded into the native type value.
            descriptors[i] = new(i, b[p], end - p >= 8 ? I32(p + 4) : null,
                end - p >= 12 ? I32(p + 8) : null, modelWadOffset + p,
                Convert.ToHexString(b.AsSpan(p, end - p)));
        }

        int co = special + specialLength;
        int length = Length(co, 32);
        int cb = co + 4;
        int tc = I32(cb);
        int fc = I32(cb + 4);
        if (tc is < 1 or > 32768 || fc < 0 || fc > tc)
            Fail("collision triangle/flag count");
        int tree = Pointer(8);
        int blocks = Pointer(12);
        int tri = Pointer(16);
        int occlusion = Pointer(20);
        int flags = Pointer(24);
        if ((tree & 1) != 0 || (blocks & 1) != 0 || (tri & 3) != 0 ||
            (occlusion & 3) != 0 || (flags & 3) != 0 || tree < cb + 28 ||
            blocks <= tree || tri <= blocks || tri + 12L * tc > occlusion ||
            occlusion > flags || flags + (long)fc > co + length)
            Fail("collision table ownership/order");
        int used = tri - blocks;
        while (used >= 2 && BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(blocks + used - 2)) == 0)
            used -= 2;
        // Retail Magic Crafters contains nondescending groups. This read-only
        // audit preserves native ordering and proves membership, never rebuilds
        // or asserts the stricter ordering required by some editing profiles.
        NativeRouteSurfaceBudget.ValidateIndexExpansion(b.AsSpan(tree, blocks - tree),
            b.AsSpan(blocks, tri - blocks), used);
        NativeCollisionIndexSnapshot index = NativeCollisionIndexCodec.Decode(
            b.AsSpan(tree, blocks - tree), b.AsSpan(blocks, tri - blocks), used, tc,
            requireDescendingOrder: false);
        var triangles = new NativeRouteSurfaceTriangle[tc];
        for (int i = 0; i < tc; i++)
        {
            int o = tri + i * 12;
            uint x = U32(o);
            uint y = U32(o + 4);
            uint z = U32(o + 8);
            byte f = i < fc ? b[flags + i] : (byte)0xFF;
            int si = f & 0x3F;
            // The catalog parser's absent-descriptor => type -1 fallback is not
            // safe here: only native index 63 is ordinary, never a missing row.
            if (si != 63 && si >= descriptors.Length)
                Fail($"missing special descriptor {si} for triangle {i}");
            var a = new NativeRoutePoint((int)(x & 0x3FFF) * 16,
                (int)(y & 0x3FFF) * 16, (int)(z & 0x3FFF) * 16);
            var p = new NativeRoutePoint(a.X + Signed9(x >> 14) * 16,
                a.Y + Signed9(y >> 14) * 16, a.Z + (int)((z >> 16) & 255) * 16);
            var q = new NativeRoutePoint(a.X + Signed9(x >> 23) * 16,
                a.Y + Signed9(y >> 23) * 16, a.Z + (int)((z >> 24) & 255) * 16);
            triangles[i] = new(i, a, p, q, f, z & 0xC000, modelWadOffset + o,
                i < fc ? modelWadOffset + flags + i : -1, si == 63 ? null : descriptors[si]);
        }

        return new(Hash(b), modelWadOffset, b.Length, modelWadOffset + co,
            Hash(b.AsSpan(co, length)), index, modelWadOffset + tree,
            modelWadOffset + blocks, triangles);

        int Pointer(int field)
        {
            int p = I32(cb + field);
            if (p < 0 || p > length - 4)
                Fail("collision pointer");
            return cb + p;
        }

        int Length(int p, int minimum)
        {
            int n = I32(p);
            if (n < minimum || (n & 3) != 0 || p + (long)n > b.Length)
                Fail("component length");
            return n;
        }

        int I32(int p)
        {
            if (p < 0 || p > b.Length - 4)
                Fail("truncated component");
            return BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(p));
        }

        uint U32(int p) => unchecked((uint)I32(p));
        static void Fail(string detail) => throw new InvalidDataException("Native route source: " + detail + ".");
    }

    internal static string Hash(ReadOnlySpan<byte> b) => Convert.ToHexStringLower(SHA256.HashData(b));
    private static int Signed9(uint v) => (v & 256) != 0 ? (int)(v & 511) - 512 : (int)(v & 511);
}

internal sealed record NativeRouteSurfaceIssue(string Code, int Segment, int Triangle,
    long TriangleWadOffset, string Detail, NativeRouteSurfaceDescriptor? Descriptor);
internal sealed record NativeRouteSupportBinding(int Segment, int Triangle,
    NativeCollisionCell Cell, long TriangleWadOffset, long FlagWadOffset, byte Flag,
    long TreeLeafWadOffset, long BlockGroupWadOffset);
internal sealed record NativeRouteSurfaceReport(string Profile, string CertificateSha256,
    string ModelSha256, string CollisionSha256, long ModelWadOffset,
    NativeRouteSurfacePolicy Policy, IReadOnlyList<NativeRoutePoint> Route, bool Accepted,
    IReadOnlyList<NativeRouteSurfaceIssue> Issues,
    IReadOnlyList<NativeRouteSupportBinding> SupportBindings, string Scope);

/// <summary>
/// Exact rational clipping of the continuous segment + authored square, not
/// endpoint sampling. Restricted single-flat-floor grammar; conservative on
/// stacked/special surfaces. No mover, rendered geometry or actor-volume claim.
/// </summary>
internal static class NativeRouteSurfaceCertificate
{
    internal const string Profile = "native-route-flat-dry-support-audit-v1";
    internal const string Scope = "Continuous authored square corridor and clear-column test; not a physics footprint, native mover trajectory, rendered-surface, resident-object, or runtime certificate. Nonflat/stacked support and special semantics fail closed. Route Z is bound as intent, never treated as the native mover's ground height.";

    public static int? FindUniqueFlatGround(NativeRouteSurfaceSnapshot source, NativeRoutePoint origin)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(origin);
        ValidatePoint(origin);
        var heights = new HashSet<int>();
        foreach (var t in source.Triangles)
        {
            var p = Points(t);
            if (Area(p) >= 0 || !Contains(p, new(origin.X, origin.Y, 0)))
                continue;
            if (t.A.Z != t.B.Z || t.A.Z != t.C.Z)
                return null;
            heights.Add(t.A.Z);
        }

        return heights.Count == 1 ? heights.Single() : null;
    }

    public static NativeRouteSurfaceReport Evaluate(NativeRouteSurfaceSnapshot source,
        IReadOnlyList<NativeRoutePoint> route, NativeRouteSurfacePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(policy);
        if (route.Count is < 1 or > 64)
            throw new InvalidDataException("Native route requires 1..64 bounded points.");
        if (policy.AuthoredHalfWidth is < 1 or > 4096 ||
            policy.ClearHeight is < 1 or > 16384 || Math.Abs((long)policy.GroundZ) > 1_000_000)
            throw new InvalidDataException("Native route policy is outside bounded authored test dimensions.");
        var points = route.ToArray();
        foreach (var p in points)
        {
            ArgumentNullException.ThrowIfNull(p);
            ValidatePoint(p);
        }

        List<NativeRouteSurfaceIssue> issues = [];
        List<NativeRouteSupportBinding> bindings = [];
        var budget = new NativeRouteSurfaceWorkBudget();
        for (int s = 0; s < Math.Max(1, points.Length - 1); s++)
        {
            NativeRoutePoint a = points[s];
            NativeRoutePoint b = points[Math.Min(s + 1, points.Length - 1)];
            List<P> corridor = Corridor(a, b, policy.AuthoredHalfWidth);
            int loX = Math.Min(a.X, b.X) - policy.AuthoredHalfWidth;
            int hiX = Math.Max(a.X, b.X) + policy.AuthoredHalfWidth;
            int loY = Math.Min(a.Y, b.Y) - policy.AuthoredHalfWidth;
            int hiY = Math.Max(a.Y, b.Y) + policy.AuthoredHalfWidth;
            List<(NativeRouteSurfaceTriangle T, List<P> Polygon)> support = [];
            foreach (var t in source.Triangles)
            {
                if (Math.Max(t.A.X, Math.Max(t.B.X, t.C.X)) < loX ||
                    Math.Min(t.A.X, Math.Min(t.B.X, t.C.X)) > hiX ||
                    Math.Max(t.A.Y, Math.Max(t.B.Y, t.C.Y)) < loY ||
                    Math.Min(t.A.Y, Math.Min(t.B.Y, t.C.Y)) > hiY)
                    continue;
                var original = Points(t);
                var clipped = Intersect(original, corridor, budget);
                if (clipped.Count == 0)
                    continue;
                R area = Abs(Area(clipped));
                bool upward = Area(original) < 0;
                if (t.ZFlags != 0)
                    Issue("UnsupportedZFlags", t, $"Raw packed Z flags 0x{t.ZFlags:X4} are unsupported.");
                if (t.Descriptor != null)
                    Issue("SpecialSurface", t, $"Native surface index {t.Flag & 63}, type {t.Descriptor.Type}: {t.Descriptor.Meaning}; ordinary land is required.");
                else if (t.Flag is not (0x3F or 0xFF))
                    Issue("UnsupportedFlagBits", t, $"Native flag byte 0x{t.Flag:X2} is not a supported ordinary encoding.");
                bool flat = upward && t.A.Z == policy.GroundZ && t.B.Z == policy.GroundZ && t.C.Z == policy.GroundZ;
                if (flat && area > 0)
                {
                    support.Add((t, clipped));
                    if (support.Count > 2048)
                        throw new InvalidDataException("Native route exact geometry budget exceeded; no certificate issued.");
                    int tileLoX = Math.Max(loX, Math.Min(t.A.X, Math.Min(t.B.X, t.C.X))) >> 12;
                    int tileHiX = Math.Min(hiX, Math.Max(t.A.X, Math.Max(t.B.X, t.C.X))) >> 12;
                    int tileLoY = Math.Max(loY, Math.Min(t.A.Y, Math.Min(t.B.Y, t.C.Y))) >> 12;
                    int tileHiY = Math.Min(hiY, Math.Max(t.A.Y, Math.Max(t.B.Y, t.C.Y))) >> 12;
                    for (int y = tileLoY; y <= tileHiY; y++)
                        for (int x = tileLoX; x <= tileHiX; x++)
                        {
                            var cellPolygon = Box(clipped, x * 4096, (x + 1) * 4096, y * 4096, (y + 1) * 4096, budget);
                            if (Abs(Area(cellPolygon)) == 0)
                                continue;
                            NativeCollisionCell cell = new(x, y, policy.GroundZ >> 12);
                            source.Index.Cells.TryGetValue(cell, out var binding);
                            budget.Membership(binding?.OrderedTriangleIndexes.Count ?? 0);
                            if (binding == null || !binding.OrderedTriangleIndexes.Contains(t.Index))
                                Issue("UnreachableSupport", t, $"Support is absent from native collision cell {cell}.");
                            else
                            {
                                budget.Binding();
                                bindings.Add(new(s, t.Index, cell, t.WadOffset, t.FlagWadOffset, t.Flag,
                                    source.TreeWadOffset + binding.TreePointerByteOffset,
                                    source.BlocksWadOffset + binding.SourceBlockWordOffset * 2L));
                            }
                        }
                }
                else
                {
                    if (upward && area > 0)
                        Issue("UnsupportedSupportHeight", t, "Nonflat or stacked support differs from the single certified ground plane.");
                    // Even zero-XY-area walls/edges survive clipping and block.
                    var aboveFloor = Clip(clipped, p => p.Z - policy.GroundZ - 1, budget);
                    if (Clip(aboveFloor, p => policy.GroundZ + policy.ClearHeight - p.Z, budget).Count > 0)
                        Issue("BlockedColumn", t, "Nonfloor terrain intersects the ground+1 through clear-height prism.");
                }
            }

            for (int i = 0; i < support.Count; i++)
                for (int j = i + 1; j < support.Count; j++)
                {
                    budget.Pair();
                    if (Abs(Area(Intersect(support[i].Polygon, support[j].Polygon, budget))) > 0)
                        Issue("OverlappingSupport", support[j].T, $"Positive-area overlap with support triangle {support[i].T.Index}.");
                }

            R covered = 0;
            foreach (var f in support)
                covered += Abs(Area(f.Polygon));
            R required = Abs(Area(corridor));
            if (covered != required)
            {
                budget.Issue();
                issues.Add(new("IncompleteSupport", s, -1, -1, $"Exact area {covered} differs from corridor {required}.", null));
            }

            void Issue(string code, NativeRouteSurfaceTriangle t, string detail)
            {
                budget.Issue();
                issues.Add(new(code, s, t.Index, t.WadOffset, detail, t.Descriptor));
            }
        }

        var ordered = issues.Distinct().OrderBy(i => i.Segment).ThenBy(i => i.Triangle)
            .ThenBy(i => i.Code, StringComparer.Ordinal).ToArray();
        var bound = bindings.Distinct().OrderBy(i => i.Segment).ThenBy(i => i.Triangle)
            .ThenBy(i => i.Cell.Z).ThenBy(i => i.Cell.Y).ThenBy(i => i.Cell.X).ToArray();
        string hash = NativeRouteSurfaceSnapshot.Hash(JsonSerializer.SerializeToUtf8Bytes(new
        {
            Profile, source.ModelSha256, source.CollisionSha256, source.ModelWadOffset,
            source.ModelByteLength, policy, route = points, issues = ordered, bindings = bound, Scope
        }));
        return new(Profile, hash, source.ModelSha256, source.CollisionSha256,
            source.ModelWadOffset, policy, Array.AsReadOnly(points), ordered.Length == 0,
            Array.AsReadOnly(ordered), Array.AsReadOnly(bound), Scope);
    }

    private static void ValidatePoint(NativeRoutePoint p)
    {
        if (Math.Abs((long)p.X) > 1_000_000 || Math.Abs((long)p.Y) > 1_000_000 || Math.Abs((long)p.Z) > 1_000_000)
            throw new InvalidDataException("Native route coordinate is outside bounded exact geometry.");
    }

    private static List<P> Points(NativeRouteSurfaceTriangle t) =>
        [new(t.A.X, t.A.Y, t.A.Z), new(t.B.X, t.B.Y, t.B.Z), new(t.C.X, t.C.Y, t.C.Z)];

    private static List<P> Corridor(NativeRoutePoint a, NativeRoutePoint b, int width)
    {
        var corners = new[] { a, b }.SelectMany(p => new[]
        {
            new NativeRoutePoint(p.X - width, p.Y - width, 0),
            new(p.X - width, p.Y + width, 0),
            new(p.X + width, p.Y - width, 0),
            new(p.X + width, p.Y + width, 0)
        }).Distinct().OrderBy(p => p.X).ThenBy(p => p.Y).ToArray();
        List<NativeRoutePoint> hull = [];
        foreach (var p in corners)
        {
            while (hull.Count >= 2 && Cross(hull[^2], hull[^1], p) <= 0)
                hull.RemoveAt(hull.Count - 1);
            hull.Add(p);
        }

        int lower = hull.Count;
        foreach (var p in corners.Reverse().Skip(1))
        {
            while (hull.Count > lower && Cross(hull[^2], hull[^1], p) <= 0)
                hull.RemoveAt(hull.Count - 1);
            hull.Add(p);
        }

        hull.RemoveAt(hull.Count - 1);
        return hull.Select(p => new P(p.X, p.Y, 0)).ToList();

        static long Cross(NativeRoutePoint a, NativeRoutePoint b, NativeRoutePoint c) =>
            (b.X - (long)a.X) * (c.Y - (long)a.Y) - (b.Y - (long)a.Y) * (c.X - (long)a.X);
    }

    private static bool Contains(IReadOnlyList<P> polygon, P p)
    {
        int sign = Area(polygon).Sign;
        for (int i = 0; i < polygon.Count; i++)
        {
            var a = polygon[i];
            var b = polygon[(i + 1) % polygon.Count];
            if (sign * ((b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X)) < 0)
                return false;
        }

        return true;
    }

    private static List<P> Intersect(IReadOnlyList<P> polygon, IReadOnlyList<P> bound, NativeRouteSurfaceWorkBudget budget)
    {
        var result = polygon.ToList();
        int sign = Area(bound).Sign;
        for (int i = 0; i < bound.Count && result.Count > 0; i++)
        {
            var a = bound[i];
            var b = bound[(i + 1) % bound.Count];
            result = Clip(result, p => sign * ((b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X)), budget);
        }

        return result;
    }

    private static List<P> Box(IReadOnlyList<P> p, int loX, int hiX, int loY, int hiY,
        NativeRouteSurfaceWorkBudget budget)
    {
        var result = Clip(p, q => q.X - loX, budget);
        result = Clip(result, q => hiX - q.X, budget);
        result = Clip(result, q => q.Y - loY, budget);
        return Clip(result, q => hiY - q.Y, budget);
    }

    private static List<P> Clip(IReadOnlyList<P> polygon, Func<P, R> side, NativeRouteSurfaceWorkBudget budget)
    {
        budget.Clip(polygon.Count);
        List<P> result = [];
        for (int i = 0; i < polygon.Count; i++)
        {
            var a = polygon[i];
            var b = polygon[(i + 1) % polygon.Count];
            R av = side(a);
            R bv = side(b);
            bool ai = av >= 0;
            bool bi = bv >= 0;
            if (ai)
                result.Add(a);
            if (ai != bi)
            {
                R t = av / (av - bv);
                result.Add(new(a.X + t * (b.X - a.X), a.Y + t * (b.Y - a.Y), a.Z + t * (b.Z - a.Z)));
            }
        }

        return result;
    }

    private static R Area(IReadOnlyList<P> p)
    {
        R sum = 0;
        for (int i = 0; i < p.Count; i++)
        {
            var a = p[i];
            var b = p[(i + 1) % p.Count];
            sum += a.X * b.Y - a.Y * b.X;
        }

        return sum / 2;
    }

    private static R Abs(R value) => value < 0 ? -value : value;
    private sealed record P(R X, R Y, R Z);
    // Native integer inputs and exact rational clipping avoid epsilon-sized
    // gaps/overlaps being silently certified as complete surface coverage.
    private readonly record struct R
    {
        private readonly BigInteger n, d;
        private R(BigInteger numerator, BigInteger denominator)
        {
            if (denominator.IsZero)
                throw new DivideByZeroException();
            if (denominator.Sign < 0)
            {
                numerator = -numerator;
                denominator = -denominator;
            }

            var gcd = BigInteger.GreatestCommonDivisor(BigInteger.Abs(numerator), denominator);
            n = numerator / gcd;
            d = denominator / gcd;
        }

        public int Sign => n.Sign;

        public static implicit operator R(int v) => new(v, 1);
        public static R operator +(R a, R b) => new(a.n * b.d + b.n * a.d, a.d * b.d);
        public static R operator -(R a, R b) => new(a.n * b.d - b.n * a.d, a.d * b.d);
        public static R operator -(R a) => new(-a.n, a.d);
        public static R operator *(R a, R b) => new(a.n * b.n, a.d * b.d);
        public static R operator /(R a, R b) => new(a.n * b.d, a.d * b.n);
        public static bool operator <(R a, R b) => a.n * b.d < b.n * a.d;
        public static bool operator >(R a, R b) => a.n * b.d > b.n * a.d;
        public static bool operator <=(R a, R b) => !(a > b);
        public static bool operator >=(R a, R b) => !(a < b);
        public override string ToString() => d.IsOne ? n.ToString(System.Globalization.CultureInfo.InvariantCulture) : $"{n}/{d}";
    }
}
