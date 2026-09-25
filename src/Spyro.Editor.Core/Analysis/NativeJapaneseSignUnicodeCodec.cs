using System.Globalization;
using System.Text;

namespace Spyro.Editor.Core.Analysis;

/// <summary>
/// An editable Unicode view of SCPS-10083's native kana font, not Shift-JIS or
/// ASCII passthrough. Unmapped native glyphs retain explicit lossless escapes.
/// Formatting uses \c0..\c6 and unmapped glyphs use \gHH. These are text-editor
/// presentation tokens, never native pointer values or export approval.
/// </summary>
public static class NativeJapaneseSignUnicodeCodec
{
    private const string Hiragana = "あいうえおかきくけこさしすせそたちつてとなにぬねのはひふへほまみむめもやゆよらりるれろわをんぁぃぅぇぉゃゅょっ";
    private static readonly IReadOnlyDictionary<byte, char> Glyphs = BuildGlyphs();
    private static readonly IReadOnlyDictionary<char, byte> Bytes = Glyphs.ToDictionary(p => p.Value, p => p.Key);

    public static NativeJapaneseSignMessage FromEditableText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length > 4096)
            throw new InvalidDataException("Sign editing text exceeds the bounded input limit.");
        string normalized;
        try { normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Normalize(NormalizationForm.FormD); }
        catch (ArgumentException e) { throw new InvalidDataException("Sign text contains invalid Unicode.", e); }
        List<byte> bytes = [];
        for (int i = 0; i < normalized.Length; i++)
        {
            char ch = normalized[i];
            if (ch == '\\')
            {
                if (i + 2 < normalized.Length && normalized[i + 1] == 'c' && normalized[i + 2] is >= '0' and <= '6')
                {
                    bytes.Add((byte)(0x40 + normalized[i + 2] - '0')); i += 2;
                }
                else if (i + 3 < normalized.Length && normalized[i + 1] == 'g' &&
                         byte.TryParse(normalized.AsSpan(i + 2, 2), NumberStyles.AllowHexSpecifier,
                             CultureInfo.InvariantCulture, out byte glyph))
                {
                    // Do not smuggle control/NUL bytes through glyph notation.
                    var native = NativeJapaneseSignMessage.FromNativeBytes([glyph]);
                    if (native.Tokens[0].Kind != NativeJapaneseSignTokenKind.Glyph)
                        throw new InvalidDataException("A sign glyph escape cannot encode a text control.");
                    bytes.Add(glyph); i += 3;
                }
                else throw new InvalidDataException("Invalid sign text escape. Use a native glyph \\gHH or color \\c0 through \\c6.");
            }
            else if (ch == '\n') bytes.Add(0xFF);
            else if (ch == ' ') bytes.Add(0x20);
            else if (Bytes.TryGetValue(ch, out byte value)) bytes.Add(value);
            else throw new InvalidDataException($"Character U+{(int)ch:X4} is not represented by the verified native sign font. Latin letters, kanji and unsupported symbols are not silently substituted.");
            if (bytes.Count > NativeJapaneseSignMessage.MaximumAuthoredMessageBytes)
                throw new InvalidDataException("Sign text exceeds the native-message authoring policy; nothing was truncated.");
        }
        return NativeJapaneseSignMessage.FromNativeBytes(bytes.ToArray());
    }

    public static string ToEditableText(NativeJapaneseSignMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        StringBuilder text = new();
        foreach (NativeJapaneseSignToken token in message.Tokens)
        {
            switch (token.Kind)
            {
                case NativeJapaneseSignTokenKind.Space: text.Append(' '); break;
                case NativeJapaneseSignTokenKind.LineBreak: text.Append('\n'); break;
                case NativeJapaneseSignTokenKind.TextColor:
                    text.Append("\\c").Append((char)('0' + token.NativeValue - 0x40)); break;
                case NativeJapaneseSignTokenKind.Glyph:
                    if (Glyphs.TryGetValue(token.NativeValue, out char ch)) text.Append(ch);
                    else text.Append("\\g").Append(token.NativeValue.ToString("X2", CultureInfo.InvariantCulture));
                    break;
                default: throw new InvalidDataException("Unknown native sign token kind.");
            }
        }
        // NFC makes native base-kana + dakuten readable as が/ガ while the
        // inverse uses NFD to recover the original two native bytes exactly.
        return text.ToString().Normalize(NormalizationForm.FormC);
    }

    private static IReadOnlyDictionary<byte, char> BuildGlyphs()
    {
        Dictionary<byte, char> glyphs = [];
        for (int i = 0; i < Hiragana.Length; i++)
        {
            glyphs.Add((byte)(0x80 + i), Hiragana[i]);
            glyphs.Add((byte)(0xC0 + i), (char)(Hiragana[i] + 0x60));
        }
        for (byte digit = 0x30; digit <= 0x39; digit++) glyphs.Add(digit, (char)digit);
        foreach (byte ascii in new byte[] { 0x21, 0x25, 0x28, 0x29, 0x2B, 0x2D, 0x2F, 0x3D, 0x3F })
            glyphs.Add(ascii, (char)ascii);
        glyphs.Add(0xB7, 'ー'); glyphs.Add(0xB8, '「'); glyphs.Add(0xB9, '」');
        glyphs.Add(0xBA, '。'); glyphs.Add(0xBB, '、');
        // All 330 source signs use BC/BD for their 103 handakuten / 1708
        // dakuten marks. 01/02 are larger alternate atlas forms with different
        // cursor correction at 80022910..938; keep those as explicit escapes.
        glyphs.Add(0xBC, '\u309A'); glyphs.Add(0xBD, '\u3099'); glyphs.Add(0xBE, '・');
        // Literal spacing marks U+309B/C are deliberately unsupported: accepting
        // them as distinct glyphs would conflate two Unicode representations of
        // the same native bytes. Use composed kana, combining marks or \gBC/BD.
        // Controller icons and alternate atlas forms remain lossless glyph
        // tokens, not misleading ASCII letters or conflated duplicate kana.
        return glyphs;
    }
}
