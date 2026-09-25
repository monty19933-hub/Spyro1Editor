using System.Buffers.Binary;
using Spyro.Editor.Core.Analysis;

internal static class AppearanceCases
{
    public static void Run(Action<bool, string> check)
    {
        void Refuse(Action action)
        { try { action(); } catch (InvalidDataException) { check(true, "Appearance refusal"); return; } throw new Exception("Expected appearance refusal."); }
        byte[] table = new byte[128];
        BinaryPrimitives.WriteUInt32LittleEndian(table.AsSpan(4), 0x0A606060);
        BinaryPrimitives.WriteUInt32LittleEndian(table.AsSpan(8 * 4), 0x0A000060);
        BinaryPrimitives.WriteUInt32LittleEndian(table.AsSpan(9 * 4), 0x0A006000);
        var evidence = NativeJapaneseObjectAppearance.DecodeFixture(table);
        byte[] egg = new byte[0x58]; egg[0x36] = 0xF5; egg[0x4F] = 1; egg[0x4C] = 0x52;
        var original = evidence.DescribeRow(egg);
        check(original.PackedRowColorWord == 0x01000052 && original.ShadingProfile.Descriptor == 0x0A606060,
            "ASR22 shader lookup retains independent lower22bits.");
        check(original.ShadingProfile.Red == 96 && original.ShadingProfile.Green == 96 && original.ShadingProfile.Blue == 96 &&
            original.ShadingProfile.LightingControl == 10, "Descriptor components are not an invented final pixel.");
        byte[] red = evidence.WithProfile(egg, 8), green = evidence.WithProfile(egg, 9);
        check(red[0x4F] == 8 && green[0x4F] == 9 && egg[0x4F] == 1, "Two instances retain independent native appearance.");
        check(Enumerable.Range(0, 0x58).All(i => i == 0x4F || red[i] == egg[i] && green[i] == egg[i]),
            "Appearance changes only the requested selector byte.");
        check(!evidence.NativeImportApproved && !evidence.FinalRenderedColorVerified, "No runtime or import promotion.");
        table[4] = 0;
        check(evidence.Profiles[1].Descriptor == 0x0A606060, "Captured table is immutable.");
        check(evidence.FindExactProfileMatches(new(29, 0x0A606060)).SequenceEqual(new byte[] { 1 }), "Matching uses descriptor, not index.");
        check(evidence.FindExactProfileMatches(new(1, 0x11223344)).Count == 0, "Unknown regional descriptor has no fabricated mapping.");
        Refuse(() => NativeJapaneseObjectAppearance.DecodeFixture(new byte[127]));
        Refuse(() => evidence.WithProfile(egg, 32)); Refuse(() => evidence.WithProfile(egg, -1));
        foreach (byte b in new byte[] { 0x40, 0x80, 0xC0 })
        { byte[] bad = egg.ToArray(); bad[0x4E] = b; Refuse(() => evidence.DescribeRow(bad)); }
        foreach (byte b in new byte[] { 32, 127, 128, 255 })
        { byte[] bad = egg.ToArray(); bad[0x4F] = b; Refuse(() => evidence.DescribeRow(bad)); }
        byte[] unknown = egg.ToArray(); unknown[0x36] = 0x22; Refuse(() => evidence.DescribeRow(unknown));
        byte[] anim = egg.ToArray(); anim[0x3C] = 1; anim[0x3D] = 1; Refuse(() => evidence.DescribeRow(anim));
        Refuse(() => evidence.DescribeRow(new byte[87]));
    }

    public static void VerifySource(NativeRegionalSourceSession source, Action<bool, string> check)
    {
        var appearance = NativeJapaneseObjectAppearance.Capture(source);
        check(appearance.SourceImageSha256 == source.SourceImageSha256 && appearance.Profiles.Count == 32, "Source-bound shader prefix.");
        check(appearance.Profiles[1].Descriptor == 0x0A606060 && appearance.Profiles[9].Descriptor == 0x0A006000,
            "Native neutral and green descriptors remain distinct.");
        bool jp = source.Revision == NativeDiscRevision.JapanScps10083;
        check(appearance.Profiles[11].Descriptor == (jp ? 0x0A005860u : 0x0A006060u) &&
            appearance.Profiles[16].Descriptor == (jp ? 0xCA005860u : 0xCA006060u), "Regional descriptor differences retained.");
        if (jp) VerifyJapaneseInstances(source, appearance, check);
        source.VerifyUnchanged();
    }

    private static void VerifyJapaneseInstances(NativeRegionalSourceSession source,
        NativeJapaneseObjectAppearance appearance, Action<bool, string> check)
    {
        static int Word(ReadOnlySpan<byte> data, int offset)
        {
            if (offset < 0 || offset > data.Length - 4) throw new InvalidDataException("Source word escapes its owner.");
            int value = BinaryPrimitives.ReadInt32LittleEndian(data[offset..]);
            return value >= 0 ? value : throw new InvalidDataException("Negative source range.");
        }
        static int Component(ReadOnlySpan<byte> scene, int offset)
        {
            int length = Word(scene, offset);
            if (length < 8 || length % 4 != 0 || length > scene.Length - offset)
                throw new InvalidDataException("Source component escapes its owner.");
            return length;
        }
        int signs = 0, eggs = 0;
        for (int entry = 23; entry <= 91; entry += 2)
        {
            byte[] data = source.ReadWadEntry(entry);
            int sceneOffset = Word(data, 0x18), sceneLength = Word(data, 0x1C);
            if (sceneOffset > data.Length || sceneLength > data.Length - sceneOffset)
                throw new InvalidDataException("Source scene escapes its owner.");
            ReadOnlySpan<byte> scene = data.AsSpan(sceneOffset, sceneLength);
            int component = 0x88;
            for (int i = 0; i < 7; i++) component += Component(scene, component);
            int length = Component(scene, component), count = Word(scene, component + 4);
            if (8L + 0x58L * count > length) throw new InvalidDataException("Source rows escape their component.");
            for (int index = 0; index < count; index++)
            {
                ReadOnlySpan<byte> row = scene.Slice(component + 8 + index * 0x58, 0x58);
                ushort actor = BinaryPrimitives.ReadUInt16LittleEndian(row[0x36..]);
                if (actor is not (0x47 or 0xF5)) continue;
                var observed = appearance.DescribeRow(row);
                if (actor == 0x47) signs++;
                else
                {
                    eggs++;
                    check(observed.PackedRowColorWord == 0x01000000 && observed.ShadingProfile.Index == 1,
                        $"Original egg shading profile entry{entry} T{index}.");
                }
                byte[] first = appearance.WithProfile(row, 8), second = appearance.WithProfile(row, 9);
                check(first.AsSpan(0, 0x4F).SequenceEqual(row[..0x4F]) &&
                    first.AsSpan(0x50).SequenceEqual(row[0x50..]) &&
                    second.AsSpan(0, 0x4F).SequenceEqual(row[..0x4F]) &&
                    second.AsSpan(0x50).SequenceEqual(row[0x50..]) &&
                    first[0x4F] == 8 && second[0x4F] == 9 && row[0x4F] == observed.ShadingProfile.Index,
                    $"Independent detached appearance changes only row selector entry{entry} T{index}.");
            }
        }
        check(signs == 330 && eggs == 31, "Every identified Japanese sign and egg has an admitted native appearance row.");
    }
}
