using System.Buffers.Binary;
using Spyro.Editor.Core.Analysis;

internal static class EditPlanCases
{
    private sealed record Fixture(byte[] Data, int Row1, int Row2, int Property1, int Property2, int Fixups);
    private static void Word(byte[] data, int offset, int value) => BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset), value);
    private static Fixture BuildFixture()
    {
        const int sceneBase = 0x200;
        byte[] data = new byte[0x800]; Word(data, 0x18, sceneBase); Word(data, 0x1C, 0x600);
        int cursor = sceneBase + 0x88;
        for (int i = 0; i < 7; i++) { Word(data, cursor, 8); cursor += 8; }
        Word(data, cursor, 8 + 2 * 0x58); Word(data, cursor + 4, 2);
        int row1 = cursor + 8, row2 = row1 + 0x58, props = row2 + 0x58;
        int p1 = props + 4 - sceneBase;
        var first = NativeJapaneseSignMessage.FromNativeBytes([0x80, 0x81, 0x82, 0x83, 0xFF]);
        byte[] firstBlock = first.BuildPrivateData(p1);
        int p2 = p1 + firstBlock.Length + 16; // Deliberately unclaimed space is NOT sign capacity.
        byte[] secondBlock = NativeJapaneseSignMessage.FromNativeBytes([0x84, 0x85, 0xFF]).BuildPrivateData(p2);
        Word(data, props, 4 + firstBlock.Length + 16 + secondBlock.Length);
        Word(data, row1, p1); Word(data, row2, p2);
        data[row1 + 0x36] = data[row2 + 0x36] = 0x47;
        firstBlock.CopyTo(data, sceneBase + p1); secondBlock.CopyTo(data, sceneBase + p2);
        data.AsSpan(sceneBase + p1 + firstBlock.Length, 16).Fill(0xCC);
        int afterProps = sceneBase + p2 + secondBlock.Length;
        Word(data, afterProps, 4); Word(data, afterProps + 4, 4);
        int fixups = afterProps + 8;
        int[] fields = [row1 - sceneBase, p1 + 12, p1 + 16, row2 - sceneBase, p2 + 12, p2 + 16];
        Word(data, fixups, fields.Length);
        for (int i = 0; i < fields.Length; i++) Word(data, fixups + 4 + i * 4, fields[i]);
        return new(data, row1, row2, p1, p2, fixups);
    }

    public static void Run(Action<bool, string> check)
    {
        void Refuse(Action action)
        { try { action(); } catch (InvalidDataException) { check(true, "Edit plan refusal"); return; } throw new Exception("Expected edit plan refusal."); }
        Fixture f = BuildFixture();
        var signs = NativeJapaneseSignSource.DecodeLevel(f.Data, 23, 0x800);
        var text = NativeJapaneseSignUnicodeCodec.FromEditableText("あ").AsCompleteDialog();
        NativeJapaneseSignEditRequest first = new(signs[0], text), second = new(signs[1], text);
        NativeJapaneseSignEditPlan Plan(byte[] data) => NativeJapaneseSignEditPlan.CompileFixture(data, 23, 0x800,
            NativeJapaneseSignSource.DecodeLevel(data, 23, 0x800).Select(s => new NativeJapaneseSignEditRequest(s, text)));
        var plan = NativeJapaneseSignEditPlan.CompileFixture(f.Data, 23, 0x800, [first, second]);
        check(plan.Writes.Count == 2 && !plan.NativeExportApproved && !plan.PublicationApproved && !plan.RuntimeVerified,
            "Detached fixture edits do not grant native publication or runtime approval.");
        var patch = plan.Writes[0];
        check(patch.ByteLength == 32 && patch.MaximumMessageBytes == 7 && patch.WadOffset == 0x800 + 0x200 + f.Property1,
            "Exact NUL-derived block and source WAD offset; unused adjacent space is excluded.");
        check(patch.RegisteredFieldSceneOffsets.SequenceEqual(new[] { f.Row1 - 0x200, f.Property1 + 12, f.Property1 + 16 }),
            "Three exact native relocation fields retained.");
        byte[] after = patch.CopyAfterBytes();
        check(after.AsSpan(0, 24).SequenceEqual(patch.CopyBeforeBytes().AsSpan(0, 24)) && after[24] == 0x80 &&
            after[25] == 0xFF && !after.AsSpan(26).ContainsAnyExcept((byte)0), "Short text ends its native line then zero-pads only the original block and preserves its header.");
        after[24] = 0x81;
        check(patch.CopyAfterBytes()[24] == 0x80, "Patch byte arrays are detached and immutable.");
        byte[] edited = plan.ApplyToCapturedLevel(23, f.Data);
        plan.VerifyLevelReadback(23, f.Data, edited);
        check(edited.AsSpan(f.Row1, 0x58).SequenceEqual(f.Data.AsSpan(f.Row1, 0x58)) &&
            edited.AsSpan(f.Fixups).SequenceEqual(f.Data.AsSpan(f.Fixups)), "Rows and complete fixup/tail read back unchanged.");
        check(edited.AsSpan(0x200 + f.Property1 + 32, 16).SequenceEqual(f.Data.AsSpan(0x200 + f.Property1 + 32, 16)),
            "Adjacent unclaimed gap is untouched.");
        var longest = NativeJapaneseSignMessage.FromNativeBytes(Enumerable.Repeat((byte)0x80, 6).ToArray()).AsCompleteDialog();
        check(NativeJapaneseSignEditPlan.CompileFixture(f.Data, 23, 0x800, [new(signs[0], longest)]).Writes[0].ByteLength == 32,
            "Existing aligned padding is the only permitted extra message capacity.");
        var tooLong = NativeJapaneseSignMessage.FromNativeBytes(Enumerable.Repeat((byte)0x80, 7).ToArray()).AsCompleteDialog();
        Refuse(() => NativeJapaneseSignEditPlan.CompileFixture(f.Data, 23, 0x800, [new(signs[0], tooLong)]));
        Refuse(() => NativeJapaneseSignEditPlan.CompileFixture(f.Data, 23, 0x800,
            [new(signs[0], NativeJapaneseSignUnicodeCodec.FromEditableText("あ"))]));
        Refuse(() => NativeJapaneseSignEditPlan.CompileFixture(f.Data, 23, 0x800, [first, first]));
        Refuse(() => NativeJapaneseSignEditPlan.CompileFixture(f.Data, 23, 0x800, []));
        Refuse(() => NativeJapaneseSignEditPlan.CompileFixture(f.Data, 23, 0x800, [new(null!, text)]));
        Refuse(() => NativeJapaneseSignEditPlan.CompileFixture(f.Data, 23, 0x800, [new(signs[0], null!)]));
        Refuse(() => NativeJapaneseSignEditPlan.CompileFixture(f.Data, 23, 0x804, [first]));
        Refuse(() => plan.ApplyToCapturedLevel(25, f.Data));
        byte[] stale = f.Data.ToArray(); stale[0x40] ^= 1;
        Refuse(() => NativeJapaneseSignEditPlan.CompileFixture(stale, 23, 0x800, [first]));
        Refuse(() => plan.ApplyToCapturedLevel(23, stale));
        foreach (int offset in new[] { f.Row1 + 4, 0x200 + f.Property1 + 12, f.Fixups + 4, 0x40 })
        { byte[] bad = edited.ToArray(); bad[offset] ^= 1; Refuse(() => plan.VerifyLevelReadback(23, f.Data, bad)); }
        foreach (var change in new (int Offset, int Value)[]
        {
            (f.Fixups, 0), (f.Fixups, int.MaxValue), (f.Fixups, -1),
            (f.Fixups + 4, f.Row1 - 0x200 + 4), // Missing row-base registration.
            (f.Fixups + 8, f.Row1 - 0x200), // Duplicate field.
            (f.Fixups + 4, f.Row1 - 0x200 + 1), // Unaligned field.
            (f.Fixups + 4, f.Fixups - 0x200), // Relocation envelope overlap.
            (f.Fixups + 4, int.MaxValue),
        })
        { byte[] bad = f.Data.ToArray(); Word(bad, change.Offset, change.Value); Refuse(() => Plan(bad)); }
        byte[] padding = f.Data.ToArray(); padding[0x200 + f.Property1 + 29] = 0x42;
        Refuse(() => Plan(padding));
        byte[] alias = f.Data.ToArray();
        Word(alias, f.Fixups, 7); Word(alias, f.Fixups + 28, f.Property1 + 32);
        Word(alias, 0x200 + f.Property1 + 32, f.Property1 + 24);
        Refuse(() => Plan(alias));
        byte[] internalField = f.Data.ToArray(); Word(internalField, f.Fixups, 7);
        Word(internalField, f.Fixups + 28, f.Property1);
        Refuse(() => Plan(internalField));
        const string jpHash = "1bcf4353d6a4d4d0db152d2ebd60879ef85516feaf170f364ff419c54f599ea7";
        NativeJapaneseSignEditPlan.RequireSourceIdentity(NativeDiscRevision.JapanScps10083, jpHash, 727179600);
        check(true, "Exact Japanese identity admitted without inventing fixture source authority.");
        Refuse(() => NativeJapaneseSignEditPlan.RequireSourceIdentity(NativeDiscRevision.UsaScus94228, jpHash, 727179600));
        Refuse(() => NativeJapaneseSignEditPlan.RequireSourceIdentity(NativeDiscRevision.JapanScps10083, new string('0', 64), 727179600));
        Refuse(() => NativeJapaneseSignEditPlan.RequireSourceIdentity(NativeDiscRevision.JapanScps10083, jpHash, 727179599));
    }

    public static void VerifySource(NativeRegionalSourceSession source, IReadOnlyList<NativeJapaneseSignSource> signs,
        Action<bool, string> check)
    {
        var replacement = NativeJapaneseSignUnicodeCodec.FromEditableText("あ").AsCompleteDialog();
        var plan = NativeJapaneseSignEditPlan.Compile(source, signs.Select(s => new NativeJapaneseSignEditRequest(s, replacement)));
        check(plan.Writes.Count == 330 && plan.SourceImageSha256 == source.SourceImageSha256 &&
            plan.SourceImageByteLength == source.SourceImageByteLength, "All 330 existing-sign edit targets bind the exact whole image.");
        check(!plan.NativeExportApproved && !plan.PublicationApproved && !plan.RuntimeVerified, "Source plan is not a runtime/export certificate.");
        foreach (var group in signs.GroupBy(s => s.DataWadEntry))
        {
            byte[] original = source.ReadWadEntry(group.Key);
            byte[] edited = plan.ApplyToCapturedLevel(group.Key, original);
            plan.VerifyLevelReadback(group.Key, original, edited);
            check(true, $"Exact complete level edit readback entry {group.Key}.");
            foreach (var sign in group)
            {
                var patch = plan.Writes.Single(w => w.DataWadEntry == group.Key && w.TrueIndex == sign.TrueIndex);
                check(patch.RegisteredFieldSceneOffsets.Count == 3 && patch.TargetRecordSha256 == sign.RecordSha256 &&
                    patch.CopyBeforeBytes().AsSpan().SequenceEqual(sign.Message.BuildPrivateData(sign.PropertiesSceneOffset)),
                    $"Exact original block and three registered fixups entry {group.Key} T{sign.TrueIndex}.");
                check(edited.AsSpan(sign.SceneDataOffset + sign.RowSceneOffset, 0x58).SequenceEqual(sign.CopyRecord()) &&
                    patch.CopyAfterBytes().AsSpan(0, 24).SequenceEqual(patch.CopyBeforeBytes().AsSpan(0, 24)),
                    $"Native row and both pointer words unchanged entry {group.Key} T{sign.TrueIndex}.");
                check(patch.CopyAfterBytes()[24] == 0x80 && patch.CopyAfterBytes()[25] == 0xFF &&
                    !patch.CopyAfterBytes().AsSpan(26).ContainsAnyExcept((byte)0),
                    $"Bounded replacement and zero padding entry {group.Key} T{sign.TrueIndex}.");
            }
        }
        source.VerifyUnchanged();
    }
}
