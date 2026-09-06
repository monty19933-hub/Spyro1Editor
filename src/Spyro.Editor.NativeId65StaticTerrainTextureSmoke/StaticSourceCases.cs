using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;

internal static class StaticSourceCases
{
    // Complete source LQ16 + HQ168 records. These fixtures are not native source
    // authority: the neutral decoder must never set SourceCertified.
    internal const string DarkRecord = "a020256cbf201a80a020256cbf201a80a020256cbf201a80c0002000df008c80c0202000df208c80e0002000ff008c80e0202000ff208c80000020001f008e80002020001f208e80200020003f008e80202020003f208e80400060005f008e80402060005f208e80600060007f008e80602060007f208e80800060009f008e80802060009f208e80a0006000bf008e80a0206000bf208e80c0002000df008e80e0002000ff008e80c0202000df208e80e0202000ff208e80";
    internal const string GnastyRecord = "00a025401fa00f8000a025401fa00f8000a025401fa00f80000020041f008e80002020041f208e80200020043f008e80202020043f208e80000020040f008e00100020041f008e00002020040f208e00102020041f208e00001020040f108e00101020041f108e00003020040f308e00103020041f308e00200020042f008e00300020043f008e00202020042f208e00302020043f208e00201020042f108e00301020043f108e00203020042f308e00303020043f308e00";

    internal static void Free(Action<bool, string> check, Action<Action, string> reject)
    {
        check(typeof(NativeStaticTerrainTextureSource).IsSealed &&
              typeof(NativeStaticTerrainTextureSource).GetConstructors().Length == 0,
            "Native source authority has no public constructor for caller-authored certificates.");
        foreach (string fixture in new[] { DarkRecord, GnastyRecord })
        {
            byte[] record = Convert.FromHexString(fixture);
            var decoded = NativeStaticTerrainTextureLayout.Decode(record);
            check(!decoded.SourceCertified && decoded.Descriptors.Count == 23,
                "Neutral complete-record grammar is not original-source authority.");
            check(decoded.CopyRecord().SequenceEqual(record) && decoded.RecordSha256 == Program.Hash(record),
                "All184 captured record bytes survive neutral decode.");
            check(decoded.Descriptors.Select(d => d.RecordOffset).SequenceEqual(Enumerable.Range(0, 23).Select(i => i * 8)),
                "Two LQ + one leading + four normal + sixteen close descriptors own all23 native fields.");
            check(decoded.Descriptors.Take(3).All(d => d.BitsPerPixel == 4 && d.TileSide == 32 &&
                d.PixelRowByteLength == 16 && d.PixelRowCount == 32 && d.PaletteRowByteLength == 32 && d.PaletteRowCount == 16),
                "Every LQ alias owns all512 pixel bytes and sixteen distance CLUT rows.");
            check(decoded.Descriptors.Skip(3).Take(4).All(d => d.BitsPerPixel == 8 && d.TileSide == 32) &&
                  decoded.Descriptors.Skip(7).All(d => d.TileSide == (fixture == DarkRecord ? 32 : 16)),
                "Both source32/16px close grammars remain distinct from normal32px tiles.");
            byte[] pixels = Enumerable.Range(0, 0x80000).Select(i => (byte)((i * 17 + i / 1024) & 255)).ToArray();
            foreach (var descriptor in decoded.Descriptors)
            {
                check(descriptor.CopyPixels(pixels).SequenceEqual(Rectangle(pixels, descriptor.PixelByteOffset,
                    descriptor.PixelRowByteLength, descriptor.PixelRowCount)), "Strided indexed pixel extraction matches independent row slices.");
                check(descriptor.CopyPalette(pixels).SequenceEqual(Rectangle(pixels, descriptor.PaletteByteOffset,
                    descriptor.PaletteRowByteLength, descriptor.PaletteRowCount)), "Complete CLUT extraction matches independent row slices.");
                reject(() => descriptor.CopyPixels(pixels[..^1]), "truncated upload cannot be treated as a complete GPU domain");
                reject(() => descriptor.CopyPalette(new byte[0x80001]), "SPU/unowned tail cannot masquerade as VRAM");
            }
            byte[] copy = decoded.CopyRecord(); copy[0] ^= 1;
            check(decoded.CopyRecord().SequenceEqual(record), "Returned record bytes are detached.");
            for (int alias = 8; alias <= 16; alias += 8)
            {
                byte[] bad = record.ToArray(); bad[alias] ^= 1;
                reject(() => NativeStaticTerrainTextureLayout.Decode(bad), "LQ0/LQ1/leading alias is an exact native obligation");
            }
            foreach (var change in new[] { (24, (byte)255), (27, (byte)255), (31, (byte)0x90), (30, (byte)0), (3, (byte)127) })
            {
                byte[] bad = record.ToArray(); bad[change.Item1] = change.Item2;
                if (change.Item1 < 8) { bad.AsSpan(0, 8).CopyTo(bad.AsSpan(8)); bad.AsSpan(0, 8).CopyTo(bad.AsSpan(16)); }
                reject(() => NativeStaticTerrainTextureLayout.Decode(bad), "unsupported rotation/shape/palette/upload-domain refuses");
            }
        }
        foreach (int length in new[] { 0, 8, 183, 185, 0x10000 })
            reject(() => NativeStaticTerrainTextureLayout.Decode(new byte[length]), "exact record budget checked before parsing/copy");
        foreach (int invalid in new[] { -1, 128 })
        {
            bool expected = false;
            try { _ = NativeStaticTerrainTextureSource.Capture("unopened-missing-source.bin", 14, invalid); }
            catch (ArgumentOutOfRangeException) { expected = true; }
            check(expected, "Invalid record selection must refuse specifically before opening the missing source; IOException cannot satisfy this assertion.");
        }
    }

    internal static void Source(string image, Action<bool, string> check, Action<Action, string> reject)
    {
        var dark = NativeStaticTerrainTextureSource.Capture(image, 14, 0);
        var gnasty = NativeStaticTerrainTextureSource.Capture(image, 70, 0);
        check(dark.SourceCertified && gnasty.SourceCertified && dark.SourceTextureId == 0 && gnasty.SourceTextureId == 0 &&
              dark.SourceWadEntry == 14 && gnasty.SourceWadEntry == 70 && dark.Handle != gnasty.Handle,
            "Same localID under distinct actual native levels cannot alias a source identity.");
        check(dark.Layout.CopyRecord().SequenceEqual(Convert.FromHexString(DarkRecord)) &&
              gnasty.Layout.CopyRecord().SequenceEqual(Convert.FromHexString(GnastyRecord)),
            "Independent raw source record fixtures match actual private issuer reads.");
        check(dark.Layout.RecordSha256 == "7b074611db43208653a931fb12c98ee64dbc5f8f41ef4edd7b1e5e796a4e0954" &&
              gnasty.Layout.RecordSha256 == "2ffdf4bc69645c31a1cc75862288bf3620955567ef65e35b2f8f65862b7dda72",
            "Complete raw native184-byte source goldens.");
        check(dark.SourceDataSha256 == "a937aecb3626ffd2311ba136684a34c9f36877054b52557b4524fcacee0e2d97" &&
              gnasty.SourceDataSha256 == "b2a25147c4d81e7fd366a830546b92e6a482fe9a56f8a429a30fcb4857091a02",
            "Whole original donor payload, not only selected descriptors, binds source interpretation.");
        foreach (var source in new[] { dark, gnasty })
        {
            check(!source.RuntimeVerified && !source.PublicationApproved && !source.DestinationAllocationProven &&
                  source.StaticRecordProven && source.NativeAnimationControlCount == 0 && source.NativeScrollingControlCount == 0,
                "Source-static records alone cannot authorize final allocation, runtime or publication.");
            byte[] upload = source.CopySourceVram();
            check(upload.Length == 0x80000, "Full source GPU domain excludes following SPU samples.");
            check(Program.Hash(source.Layout.Descriptors[0].CopyPixels(upload)) == (source.SourceWadEntry == 14
                    ? "c7af73a5a5898363725bab63df7046a6015aab0e2f84e956e00b6f0d526f48ae"
                    : "2d55c1c244b13f906e7788f5e8f4f50662210096515264da224f3dd34ba294af"),
                "Actual strided native indexed art differs independently of palette/record identity.");
            upload[0] ^= 1;
            check(source.CopySourceVram()[0] != upload[0], "Source upload copy cannot mutate issued ownership.");
            source.RequireExactSourceRecord(source.Layout.CopyRecord());
            byte[] corrupt = source.Layout.CopyRecord(); corrupt[24] ^= 1;
            reject(() => source.RequireExactSourceRecord(corrupt), "a neutral changed record cannot replace source-issued bytes");
            var reopened = NativeStaticTerrainTextureSource.Capture(image, source.SourceWadEntry, 0);
            check(reopened.Handle == source.Handle, "Opaque source identity remains stable across genuine re-capture.");
        }
        reject(() => NativeStaticTerrainTextureSource.Capture(image, 16, 1), "actual native Town scrolling destination refuses");
        reject(() => NativeStaticTerrainTextureSource.Capture(image, 40, 70), "actual native full-record animation destination refuses");
        reject(() => NativeStaticTerrainTextureSource.Capture(image, 70, 12), "rotated GnastyT12 is outside first supported subset");
        reject(() => NativeStaticTerrainTextureSource.Capture(image, 13, 0), "overlay entry is not a native donor data owner");
        NativeScalarCases.VerifyOriginal(image, check);
    }

    private static byte[] Rectangle(byte[] bytes, int offset, int width, int rows) =>
        Enumerable.Range(0, rows).SelectMany(row => bytes.AsSpan(offset + row * 1024, width).ToArray()).ToArray();
}
