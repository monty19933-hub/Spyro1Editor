using System.Buffers.Binary;

namespace Spyro.Editor.Core.Universal;

/// <summary>Adds two cartoon eyes (white hexagon + dark diamond pupil, as untextured Gouraud triangles) to the
/// Japanese dragonfly egg model. Layout of that model (single animation, two frames, encoding 0):
/// root 0x3C, animation header 0x3C..0x70 (+2 shading count, +8/+9 near/far vertex count, +0x14/+0x18
/// near faces/shading, +0x1C/+0x20 far faces/shading, +0x24 two frame words), model data from 0x70.
/// Frame vertices are a mixed stream: absolute words (x = w&gt;&gt;21, y = (w&lt;&lt;10)&gt;&gt;21, z = (w&lt;&lt;20)&gt;&gt;19, the
/// low bit selects a delta halfword for the next vertex). New vertices are appended as absolute words
/// right after the original absolute partition; the far (distant) faces are unchanged.</summary>
public static class UniversalEggEyes
{
    private const int Anim = 0x3C, ModelData = 0x70;

    private sealed record Frame(uint Packed, uint Aux, int Start, int AbsCount, int DeltaCount, int End,
        List<(int X, int Y, int Z)> Vertices, bool LastWasDelta, int LastItemOffset);

    /// <summary>Decoded vertices of each frame (diagnostics).</summary>
    public static List<List<(int X, int Y, int Z)>> DebugFrames(byte[] p)
    {
        uint W(int o) => BinaryPrimitives.ReadUInt32LittleEndian(p.AsSpan(o));
        ushort H(int o) => BinaryPrimitives.ReadUInt16LittleEndian(p.AsSpan(o));
        int vcount = p[Anim + 8], shift = p[Anim + 6] + 1, md = (int)W(0x34);
        var all = new List<List<(int, int, int)>>();
        for (int f = 0; f < H(Anim); f++)
        {
            uint packed = W(Anim + 0x24 + 8 * f), aux = W(Anim + 0x28 + 8 * f);
            int start = md + (int)(packed & 0x1FFFFF), abs = start, delta = start + 4 * (int)(aux >> 24);
            int x = 0, y = 0, z = 0, flag = 0; var verts = new List<(int, int, int)>();
            for (int v = 0; v < vcount; v++)
            {
                int word;
                if (flag == 0) { word = (int)W(abs); abs += 4; x = word >> 21; y = (word << 10) >> 21; z = (word << 20) >> 19; }
                else { word = (short)H(delta); delta += 2; x += (word >> 11) << shift; y -= ((word << 21) >> 27) << shift; z -= ((word << 26) >> 27) << shift; }
                verts.Add((x, y, z)); flag = word & 1;
            }
            all.Add(verts);
        }
        return all;
    }

    public static byte[] AddEyes(byte[] package)
    {
        byte[] p = package;
        uint W(int o) => BinaryPrimitives.ReadUInt32LittleEndian(p.AsSpan(o));
        ushort H(int o) => BinaryPrimitives.ReadUInt16LittleEndian(p.AsSpan(o));
        if (W(0) != 1 || W(0x34) != ModelData || W(0x38) != Anim || H(Anim) != 2 || p[Anim + 4] != 0)
            throw new InvalidDataException("Unexpected dragonfly egg model layout.");
        int shading = H(Anim + 2), vcount = p[Anim + 8], shift = p[Anim + 6] + 1;
        if (p[Anim + 9] != vcount) throw new InvalidDataException("Egg near/far vertex counts differ.");
        int nearFace = ModelData + (int)W(Anim + 0x14), nearShade = ModelData + (int)W(Anim + 0x18);
        int farFace = ModelData + (int)W(Anim + 0x1C), farShade = ModelData + (int)W(Anim + 0x20);

        var frames = new List<Frame>();
        for (int f = 0; f < 2; f++)
        {
            uint packed = W(Anim + 0x24 + 8 * f), aux = W(Anim + 0x28 + 8 * f);
            int start = ModelData + (int)(packed & 0x1FFFFF), absCount = (int)(aux >> 24);
            int abs = start, deltaStart = start + 4 * absCount, delta = deltaStart;
            int x = 0, y = 0, z = 0, flag = 0, last = 0; bool lastDelta = false;
            List<(int, int, int)> verts = [];
            for (int v = 0; v < vcount; v++)
            {
                int word;
                if (flag == 0) { last = abs; lastDelta = false; word = (int)W(abs); abs += 4; x = word >> 21; y = (word << 10) >> 21; z = (word << 20) >> 19; }
                else
                {
                    last = delta; lastDelta = true; word = (short)H(delta); delta += 2;
                    x += (word >> 11) << shift; y -= ((word << 21) >> 27) << shift; z -= ((word << 26) >> 27) << shift;
                }
                verts.Add((x, y, z)); flag = word & 1;
            }
            if (abs != deltaStart) throw new InvalidDataException("Egg frame absolute partition mismatch.");
            frames.Add(new(packed, aux, start, absCount, (delta - deltaStart) / 2, Math.Max(abs, delta), verts, lastDelta, last));
        }

        // Outward winding convention of the existing near triangles.
        var f0 = frames[0].Vertices;
        (double cx, double cy, double cz) = (f0.Average(v => v.X), f0.Average(v => v.Y), f0.Average(v => v.Z));
        int nearLen = (int)W(nearFace), outward = 0;
        uint triWord = 0, triColour = 0; bool haveTri = false;
        for (int q = nearFace + 4; q < nearFace + 4 + nearLen;)
        {
            uint a = W(q), c = W(q + 4);
            bool quad = (a & 0x80000000) != 0;
            if (!quad)
            {
                if (!haveTri) { triWord = a; triColour = c; haveTri = true; }
                var v0 = f0[(int)(a >> 22 & 511)]; var v1 = f0[(int)(a >> 13 & 511)]; var v2 = f0[(int)(a >> 4 & 511)];
                var n = Cross(Sub(v1, v0), Sub(v2, v0));
                double d = n.X * ((v0.X + v1.X + v2.X) / 3.0 - cx) + n.Y * ((v0.Y + v1.Y + v2.Y) / 3.0 - cy) + n.Z * ((v0.Z + v1.Z + v2.Z) / 3.0 - cz);
                outward += d > 0 ? 1 : d < 0 ? -1 : 0;
            }
            q += quad ? 12 : 8;
        }
        if (!haveTri) throw new InvalidDataException("Egg model has no triangle to copy face flags from.");
        int windingSign = outward >= 0 ? 1 : -1;
        var surfaceTris = new List<(int, int, int)>();
        for (int q = nearFace + 4; q < nearFace + 4 + nearLen;)
        {
            uint a = W(q); bool quad = (a & 0x80000000) != 0;
            int i0 = (int)(a >> 22 & 511), i1 = (int)(a >> 13 & 511), i2 = (int)(a >> 4 & 511);
            surfaceTris.Add((i0, i1, i2));
            if (quad)
            {
                int i3 = (int)(W(q + 8) >> 2 & 511);
                surfaceTris.Add((i0, i2, i3)); surfaceTris.Add((i1, i2, i3)); surfaceTris.Add((i0, i1, i3));
            }
            q += quad ? 12 : 8;
        }

        // Eye geometry per frame (same topology, positions follow each frame's shape).
        const int whiteIdx = 0, pupilIdx = 1; // appended shading entries: shading, shading+1
        var eyeTris = new List<(int A, int B, int C, int S)>();
        var newVerts = new List<List<(int X, int Y, int Z)>>();
        // The body is round in X/Z (model up is -Z: the egg rests at z=0); the wings stick out along Y. Eyes sit high on the +X face, between the
        // wings. Each frame gets an open-eye set and a closed-eye set (lid line) for blinking.
        var closedVerts = new List<List<(int X, int Y, int Z)>>();
        foreach (var fr in frames)
        {
            var vs = fr.Vertices;
            double mx = (vs.Min(v => v.X) + vs.Max(v => v.X)) / 2.0, my = (vs.Min(v => v.Y) + vs.Max(v => v.Y)) / 2.0, mz = (vs.Min(v => v.Z) + vs.Max(v => v.Z)) / 2.0;
            double ax = (vs.Max(v => v.X) - vs.Min(v => v.X)) / 2.0, az = (vs.Max(v => v.Z) - vs.Min(v => v.Z)) / 2.0;
            double ew = 0.30 * az, eh = 0.40 * az;
            var open = new List<(int, int, int)>(); var closed = new List<(int, int, int)>();
            foreach (double side in new[] { -1.0, 1.0 })
            {
                var d0 = Norm((0.70, 0.30 * side, -0.65));                         // model up is -Z
                var dir = Norm((ax * d0.X, ax * d0.Y, az * d0.Z));
                var (P, n) = CastToSurface((mx, my, mz), dir, vs, surfaceTris)
                    ?? ((mx + ax * d0.X, my + ax * d0.Y, mz + az * d0.Z), Norm((d0.X / ax, d0.Y / ax, d0.Z / az)));
                var sv = Norm(CrossD(n, (0, 0, 1)));
                if (sv.Y * side < 0) sv = (-sv.X, -sv.Y, -sv.Z);
                var up = Norm(CrossD(sv, n));
                if (up.Z > 0) up = (-up.X, -up.Y, -up.Z);
                double curve = Math.Min(ax, az), sag = (ew / 2) * (ew / 2) / (2 * curve) + (eh / 2) * (eh / 2) / (2 * curve);
                (double X, double Y, double Z) Pt(double a, double b, double outBy) =>
                    (P.X + sv.X * a + up.X * b + n.X * outBy, P.Y + sv.Y * a + up.Y * b + n.Y * outBy, P.Z + sv.Z * a + up.Z * b + n.Z * outBy);
                double o1 = sag + 0.04 * curve, o2 = o1 + 0.06 * curve;
                (double, double, double)[] Hex(double hh) => [Pt(0, hh / 2, o1), Pt(ew / 2, hh / 4, o1), Pt(ew / 2, -hh / 4, o1), Pt(0, -hh / 2, o1), Pt(-ew / 2, -hh / 4, o1), Pt(-ew / 2, hh / 4, o1)];
                double look = -side * 0.10 * ew;
                (double, double, double)[] Dia(double pw, double ph, double cy) => [Pt(look, cy + ph, o2), Pt(look + pw, cy, o2), Pt(look, cy - ph, o2), Pt(look - pw, cy, o2)];
                foreach (var v in Hex(eh).Concat(Dia(ew * 0.26, eh * 0.26, 0.06 * eh))) open.Add(R3(v));
                // closed: white squashed to a thin band, the dark pupil stretched over it as the lid line
                foreach (var v in Hex(eh * 0.14).Concat(Dia(ew * 0.46, eh * 0.05, 0))) closed.Add(R3(v));
            }
            newVerts.Add(open); closedVerts.Add(closed);
        }
        for (int e = 0; e < 2; e++)
        {
            int b = vcount + e * 10;
            foreach (var (i, j, k) in new[] { (0, 1, 2), (0, 2, 3), (0, 3, 4), (0, 4, 5) }) eyeTris.Add((b + i, b + j, b + k, whiteIdx));
            foreach (var (i, j, k) in new[] { (6, 7, 8), (6, 8, 9) }) eyeTris.Add((b + i, b + j, b + k, pupilIdx));
        }
        int newCount = vcount + 20;
        if (newCount > 255) throw new InvalidDataException("Egg model would exceed 255 vertices.");

        // Near face table with eye triangles (winding matches the model's outward convention).
        using var nearOut = new MemoryStream();
        nearOut.Write(p, nearFace + 4, nearLen);
        var fr0 = newVerts[0];
        (int X, int Y, int Z) V(int idx) => idx < vcount ? f0[idx] : fr0[idx - vcount];
        Span<byte> t = stackalloc byte[8];
        foreach (var (a0, b0, c0, s) in eyeTris)
        {
            int a = a0, b = b0, c = c0;
            var n = Cross(Sub(V(b), V(a)), Sub(V(c), V(a)));
            var ctr = ((V(a).X + V(b).X + V(c).X) / 3.0 - cx, (V(a).Y + V(b).Y + V(c).Y) / 3.0 - cy, (V(a).Z + V(b).Z + V(c).Z) / 3.0 - cz);
            double d = n.X * ctr.Item1 + n.Y * ctr.Item2 + n.Z * ctr.Item3;
            if (Math.Sign(d) != windingSign) (b, c) = (c, b);
            uint word = triWord & ~((511u << 22) | (511u << 13) | (511u << 4)) | ((uint)a << 22) | ((uint)b << 13) | ((uint)c << 4);
            int si = shading + s;
            uint col = triColour & ~((511u << 19) | (511u << 10) | (511u << 1)) | ((uint)si << 19) | ((uint)si << 10) | ((uint)si << 1);
            BinaryPrimitives.WriteUInt32LittleEndian(t, word); BinaryPrimitives.WriteUInt32LittleEndian(t[4..], col);
            nearOut.Write(t);
        }
        byte[] nearBody = nearOut.ToArray();

        uint Template(int shadeTable) => W(shadeTable);
        byte[] Shade(int table)
        {
            byte[] s = new byte[(shading + 2) * 4];
            p.AsSpan(table, shading * 4).CopyTo(s);
            uint baseWord = Template(table) & 0xFF000000;
            BinaryPrimitives.WriteUInt32LittleEndian(s.AsSpan(shading * 4), baseWord | 0xF4F4F4);     // white (R,G,B bytes)
            BinaryPrimitives.WriteUInt32LittleEndian(s.AsSpan(shading * 4 + 4), baseWord | 0x201814);   // near-black pupil
            return s;
        }

        // Rebuild model data: near faces, far faces, near shading, far shading, frames, look-ahead pad.
        // Four frames (0,1 eyes open; 2,3 the same poses with eyes closed): the animation header grows by
        // 16 bytes, so model data starts at 0x80 instead of 0x70.
        const int NewModelData = 0x80;
        using var md = new MemoryStream();
        int Here() => NewModelData + (int)md.Length;
        void Align4() { while (md.Length % 4 != 0) md.WriteByte(0); }
        int nNear = Here(); WriteU32(md, (uint)nearBody.Length); md.Write(nearBody); md.Write(new byte[8]); Align4();
        int farLen = (int)W(farFace);
        int nFar = Here(); md.Write(p, farFace, 4 + farLen); md.Write(new byte[8]); Align4();
        int nNearShade = Here(); md.Write(Shade(nearShade)); md.Write(new byte[4]);
        int nFarShade = Here(); md.Write(Shade(farShade)); md.Write(new byte[4]);
        var frameStarts = new List<int>();
        for (int f4 = 0; f4 < 4; f4++)
        {
            int f = f4 & 1;
            var fr = frames[f];
            var eyeSet = f4 < 2 ? newVerts[f] : closedVerts[f];
            byte[] absPart = p.AsSpan(fr.Start, 4 * fr.AbsCount).ToArray();
            byte[] deltaPart = p.AsSpan(fr.Start + 4 * fr.AbsCount, 2 * fr.DeltaCount).ToArray();
            // The next vertex after the original ones must come from the absolute stream: clear the last flag.
            int rel = fr.LastItemOffset - fr.Start;
            if (fr.LastWasDelta) deltaPart[rel - 4 * fr.AbsCount] &= 0xFE; else absPart[rel] &= 0xFE;
            frameStarts.Add(Here());
            md.Write(absPart);
            foreach (var v in eyeSet) WriteU32(md, EncodeAbsolute(v.X, v.Y, v.Z));
            md.Write(deltaPart); Align4();
            md.Write(new byte[8]);
        }
        byte[] result = new byte[NewModelData + (int)md.Length];
        p.AsSpan(0, ModelData).CopyTo(result);
        md.ToArray().CopyTo(result, NewModelData);
        void Put(int o, uint v) => BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(o), v);
        Put(0x34, NewModelData);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(Anim), 4);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(Anim + 2), (ushort)(shading + 2));
        result[Anim + 8] = (byte)newCount; result[Anim + 9] = (byte)newCount;
        Put(Anim + 0x14, (uint)(nNear - NewModelData)); Put(Anim + 0x18, (uint)(nNearShade - NewModelData));
        Put(Anim + 0x1C, (uint)(nFar - NewModelData)); Put(Anim + 0x20, (uint)(nFarShade - NewModelData));
        for (int f4 = 0; f4 < 4; f4++)
        {
            var fr = frames[f4 & 1];
            Put(Anim + 0x24 + 8 * f4, (fr.Packed & ~0x1FFFFFu) | (uint)(frameStarts[f4] - NewModelData));
            Put(Anim + 0x28 + 8 * f4, (fr.Aux & 0x00FFFFFF) | ((uint)(fr.AbsCount + 20) << 24));
        }
        return result;
    }

    /// <summary>Absolute vertex word as the native decoder reads it. x (bits 21-31), y (bits 11-21) and z
    /// (bits 0-11, times two) share their boundary bits, so y's parity follows z's sign and x's parity follows
    /// y's sign; the low bit (next-is-delta flag) stays clear. Coordinates move by at most a few units.</summary>
    public static uint EncodeAbsolute(int x, int y, int z)
    {
        int zz = (z >> 1) & ~1;                                  // z = sext12(zz) * 2, flag bit clear
        zz = Math.Clamp(zz, -2048, 2046);
        int zSign = zz < 0 ? 1 : 0;
        if ((y & 1) != zSign) y += y >= 0 ? 1 : -1;               // bit 11 = y bit 0 = z sign
        if ((y & 1) != zSign) y -= 2;
        y = Math.Clamp(y, -1024, 1023);
        int ySign = y < 0 ? 1 : 0;
        if ((x & 1) != ySign) x += x >= 0 ? 1 : -1;               // bit 21 = x bit 0 = y sign
        x = Math.Clamp(x, -1024, 1023);
        uint w = ((uint)(x & 0x7FF) << 21) | ((uint)(y & 0x7FF) << 11) | (uint)(zz & 0xFFF);
        int dx = (int)w >> 21, dy = ((int)w << 10) >> 21, dz = ((int)w << 20) >> 19;
        if (dx != x || dy != y || (w & 1) != 0) throw new InvalidDataException("Egg eye vertex does not encode exactly.");
        return w;
    }

    private static ((double X, double Y, double Z) P, (double X, double Y, double Z) N)? CastToSurface(
        (double X, double Y, double Z) o, (double X, double Y, double Z) dir, List<(int X, int Y, int Z)> vs, List<(int, int, int)> tris)
    {
        double best = -1; (double, double, double) bestN = default;
        foreach (var (i, j, k) in tris)
        {
            var a = (X: (double)vs[i].X, Y: (double)vs[i].Y, Z: (double)vs[i].Z);
            var e1 = (X: vs[j].X - a.X, Y: vs[j].Y - a.Y, Z: vs[j].Z - a.Z);
            var e2 = (X: vs[k].X - a.X, Y: vs[k].Y - a.Y, Z: vs[k].Z - a.Z);
            var pv = CrossD(dir, e2); double det = e1.X * pv.X + e1.Y * pv.Y + e1.Z * pv.Z;
            if (Math.Abs(det) < 1e-9) continue;
            var tv = (X: o.X - a.X, Y: o.Y - a.Y, Z: o.Z - a.Z);
            double u = (tv.X * pv.X + tv.Y * pv.Y + tv.Z * pv.Z) / det; if (u < 0 || u > 1) continue;
            var qv = CrossD(tv, e1); double v = (dir.X * qv.X + dir.Y * qv.Y + dir.Z * qv.Z) / det; if (v < 0 || u + v > 1) continue;
            double t = (e2.X * qv.X + e2.Y * qv.Y + e2.Z * qv.Z) / det;
            if (t > best) { best = t; bestN = Norm(CrossD(e1, e2)); }
        }
        if (best <= 0) return null;
        var n = bestN; if (n.Item1 * dir.X + n.Item2 * dir.Y + n.Item3 * dir.Z < 0) n = (-n.Item1, -n.Item2, -n.Item3);
        return ((o.X + dir.X * best, o.Y + dir.Y * best, o.Z + dir.Z * best), n);
    }

    private static (double X, double Y, double Z) Norm((double X, double Y, double Z) v)
    { double l = Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z); return (v.X / l, v.Y / l, v.Z / l); }
    private static (double X, double Y, double Z) CrossD((double X, double Y, double Z) a, (double X, double Y, double Z) b) =>
        (a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

    private static (int, int, int) R3((double X, double Y, double Z) v) => ((int)Math.Round(v.X), (int)Math.Round(v.Y), (int)Math.Round(v.Z));

    private static void WriteU32(Stream s, uint v) { Span<byte> b = stackalloc byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(b, v); s.Write(b); }
    private static (double X, double Y, double Z) Sub((int X, int Y, int Z) a, (int X, int Y, int Z) b) => (a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    private static (double X, double Y, double Z) Cross((double X, double Y, double Z) a, (double X, double Y, double Z) b) =>
        (a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
}
