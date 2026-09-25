using Spyro.Editor.Core.Analysis;
using System.Text;

internal static class UnicodeCases
{
    internal static void Run(Action<bool, string> check)
    {
        void Reject(string text)
        {
            try { NativeJapaneseSignUnicodeCodec.FromEditableText(text); }
            catch (InvalidDataException) { check(true, "Unicode input refused without substitution."); return; }
            throw new InvalidOperationException("Expected unsupported/overflow text refusal.");
        }
        var kana = NativeJapaneseSignUnicodeCodec.FromEditableText("こんにちは スパイロ！".Replace('！', '!'));
        check(NativeJapaneseSignUnicodeCodec.ToEditableText(kana) == "こんにちは スパイロ!", "Readable kana round trip.");
        check(NativeJapaneseSignUnicodeCodec.FromEditableText("がぱヴ").CopyNativeBytes().SequenceEqual(new byte[] {0x85,0xBD,0x99,0xBC,0xC2,0xBD}),
            "Voiced kana use the marks observed in all source signs, not larger alternate forms.");
        check(NativeJapaneseSignUnicodeCodec.FromEditableText("か\u3099は\u309Aウ\u3099").NativeBytesHex == "85bd99bcc2bd",
            "Decomposed input preserves source-sign accent size and cursor placement.");
        check(NativeJapaneseSignUnicodeCodec.ToEditableText(NativeJapaneseSignMessage.FromNativeBytes([1, 2])) == "\\g01\\g02",
            "Alternate large accent forms retain their own native byte identities.");
        check(NativeJapaneseSignUnicodeCodec.FromEditableText("カメラ").CopyNativeBytes().SequenceEqual(new byte[] {0xC5,0xE1,0xE6}),
            "Source camera word independently identifies the native katakana sequence.");
        check(NativeJapaneseSignUnicodeCodec.FromEditableText("テクニック").CopyNativeBytes().SequenceEqual(new byte[] {0xD2,0xC7,0xD5,0xF6,0xC7}),
            "Source technique word retains the native bytes.");
        var formatted = NativeJapaneseSignUnicodeCodec.FromEditableText("\\c4あ\r\n\\gF7 123");
        check(formatted.CopyNativeBytes().SequenceEqual(new byte[] {0x44,0x80,0xFF,0xF7,0x20,0x31,0x32,0x33}),
            "Text formatting and native glyph preservation are not pointer editing.");
        List<byte> admitted = [];
        for (int value = 1; value <= 255; value++)
        {
            NativeJapaneseSignMessage native;
            try { native = NativeJapaneseSignMessage.FromNativeBytes([(byte)value]); }
            catch (InvalidDataException) { continue; }
            admitted.Add((byte)value);
            string text = NativeJapaneseSignUnicodeCodec.ToEditableText(native);
            check(NativeJapaneseSignUnicodeCodec.FromEditableText(text).CopyNativeBytes().SequenceEqual(native.CopyNativeBytes()),
                $"Every admitted byte retains exact round-trip identity: {value:X2}.");
        }
        foreach (string value in new[] {"A", "hello", "漢字", "🟢", "\t", "\r", "\0", "\\", "\\c7", "\\g00", "\\gFF", "\\g40", "\\g20", "\\gFD", "\\gXX", "\uD800", "\uDC00", "\uD800x", "\uD800\uD800", "゛", "゜", "か゛", "\\c", "\\c-1", "\\C0", "\\g", "\\g1", "\\g 1", "\\g+1", "\\g0x1", "\\G01", "\\n", "\\\\", "\uFEFF", "\u2028", "\uFF76", new string('あ',48), new string('あ',4097)})
            Reject(value);
        check(NativeJapaneseSignUnicodeCodec.FromEditableText(new string('あ',47)).ByteLength == 47, "47 native bytes fit with terminator.");
        Reject(new string('が',24)); // 24 visible glyphs become48 native bytes.
        void Roundtrip(byte[] bytes)
        {
            var native = NativeJapaneseSignMessage.FromNativeBytes(bytes);
            string editable = NativeJapaneseSignUnicodeCodec.ToEditableText(native);
            check(NativeJapaneseSignUnicodeCodec.FromEditableText(editable).CopyNativeBytes().SequenceEqual(bytes),
                $"NFC/NFD preserves adjacent native glyph, escape, control and mark bytes: {Convert.ToHexString(bytes)}.");
        }
        foreach (byte a in admitted) foreach (byte b in admitted) Roundtrip([a, b]);
        foreach (byte a in admitted) foreach (byte b in new byte[] {1, 2, 0xBC, 0xBD})
            foreach (byte c in new byte[] {1, 2, 0xBC, 0xBD}) Roundtrip([a, b, c]);
    }

    internal static void VerifySource(NativeJapaneseSignSource sign, Action<bool, string> check)
    {
        string editable = NativeJapaneseSignUnicodeCodec.ToEditableText(sign.Message);
        var roundtrip = NativeJapaneseSignUnicodeCodec.FromEditableText(editable);
        check(roundtrip.NativeBytesHex == sign.Message.NativeBytesHex,
            $"Exact Unicode/formatting readback entry{sign.DataWadEntry} T{sign.TrueIndex}.");
        byte[] bytes = sign.Message.CopyNativeBytes();
        string decomposed = editable.Normalize(NormalizationForm.FormD);
        check(!bytes.Any(b => b is 1 or 2), "Source signs do not use the alternate large accent forms.");
        check(decomposed.Count(c => c == '\u309A') == bytes.Count(b => b == 0xBC) &&
            decomposed.Count(c => c == '\u3099') == bytes.Count(b => b == 0xBD),
            "Unicode accent counts preserve each source sign's BC/BD identity.");
        if (sign.DataWadEntry == 23 && sign.TrueIndex == 180)
            check(editable.Contains("カメラ", StringComparison.Ordinal), "Original Artisans camera sign verifies katakana mapping.");
        if (sign.DataWadEntry == 25 && sign.TrueIndex == 201)
            check(editable.Contains("テクニック", StringComparison.Ordinal), "Original Stone Hill technique sign verifies katakana mapping.");
    }
}
