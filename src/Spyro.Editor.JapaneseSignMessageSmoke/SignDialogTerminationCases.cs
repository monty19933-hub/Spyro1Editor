using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;

internal static class SignDialogTerminationCases
{
    internal static void Run(Action<bool, string> check)
    {
        var raw = NativeJapaneseSignUnicodeCodec.FromEditableText("181 ガパ\n\\c2カメラ");
        check(raw.NativeBytesHex == "31383120c5bdd9bcff42c5e1e6" && !raw.HasFinalLineTerminator,
            "Lossless Unicode/raw codec preserves the diagnostic unterminated-tail preimage.");
        var complete = raw.AsCompleteDialog();
        check(complete.NativeBytesHex == "31383120c5bdd9bcff42c5e1e6ff" && complete.ByteLength == 14 &&
            complete.HasFinalLineTerminator && ReferenceEquals(complete, complete.AsCompleteDialog()),
            "Complete modal dialog adds exactly one final native FF, idempotently.");
        check(raw.ByteLength == 13 && !raw.HasFinalLineTerminator, "Completing a dialog does not mutate the raw authoring fragment.");
        Refuse(() => raw.BuildPrivateData(0x100));
        byte[] block = complete.BuildPrivateData(0x100);
        check(block.Length == 40 && block[37] == 0xFF && block[38] == 0 && block[39] == 0,
            "Serialized dialog ends FF/NUL inside its exact aligned block.");
        byte[][] hidden = DrawnLines(raw.CopyNativeBytes(), 1000);
        byte[][] shown = DrawnLines(complete.CopyNativeBytes(), 1000);
        check(hidden.Length == 1 && hidden[0].SequenceEqual(Convert.FromHexString("31383120c5bdd9bc")),
            "Native complete-state model drops an unterminated final line; NUL is not a flush.");
        check(shown.Length == 2 && shown[1].SequenceEqual(Convert.FromHexString("42c5e1e6")),
            "Final FF keeps the second colored line visible at completed reveal state.");
        check(DrawnLines(raw.CopyNativeBytes(), 12).Length == 2 && DrawnLines(raw.CopyNativeBytes(), 14).Length == 1,
            "Unterminated line can appear during reveal then disappear, matching the captured r01 failure.");
        check(DrawnLines(complete.CopyNativeBytes(), 12).Length == 2 && DrawnLines(complete.CopyNativeBytes(), 14).Length == 2,
            "Terminated final line survives reveal progression and completion.");
        var oneLine = NativeJapaneseSignMessage.FromNativeBytes([0x80]);
        check(DrawnLines(oneLine.CopyNativeBytes(), 1000).Length == 0 &&
            DrawnLines(oneLine.AsCompleteDialog().CopyNativeBytes(), 1000).Single().SequenceEqual(new byte[] { 0x80 }),
            "Single-line messages also require FF; this is not only a multi-line policy.");
        var largestLine = NativeJapaneseSignMessage.FromNativeBytes(Enumerable.Repeat((byte)0x80, 47).ToArray()).AsCompleteDialog();
        check(largestLine.ByteLength == 48 && DrawnLines(largestLine.CopyNativeBytes(), 1000).Single().Length == 47,
            "FF flush is outside the 47-byte native line-buffer payload.");
        byte[] policyMaximum = Enumerable.Range(0, 3).SelectMany(_ => Enumerable.Repeat((byte)0x80, 47).Append((byte)0xFF))
            .Concat(Enumerable.Repeat((byte)0x80, 9)).ToArray();
        check(policyMaximum.Length == 153, "Fixture reaches the exact authored-byte policy boundary.");
        Refuse(() => NativeJapaneseSignMessage.FromNativeBytes(policyMaximum).AsCompleteDialog());
        Refuse(() => NativeJapaneseSignMessage.FromNativeBytes(Enumerable.Repeat((byte)0xFF, 8).Append((byte)0x80).ToArray()).AsCompleteDialog());
        return;
        void Refuse(Action action)
        {
            try { action(); } catch (InvalidDataException) { check(true, "Incomplete or overflowing modal dialog refused."); return; }
            throw new Exception("Expected modal termination refusal.");
        }
    }

    internal static void VerifySource(NativeRegionalSourceSession source, Action<bool, string> check)
    {
        if (source.Revision != NativeDiscRevision.JapanScps10083) throw new InvalidDataException("Japanese modal consumer required.");
        byte[] consumer = source.ReadExecutableRange(0xBA1C, 0x170);
        check(Convert.ToHexStringLower(SHA256.HashData(consumer)) == "18c0271e566a18eeb514babaa6b0b80fddf5c6dd83866908d00bd90926c9a822",
            "Complete native line-count/copy/flush/NUL-exit consumer retains the exact source certificate.");
        var signs = NativeJapaneseSignSource.CaptureAll(source);
        check(signs.Count == 330 && signs.All(s => s.Message.HasFinalLineTerminator), "Every original Japanese sign ends its final line with FF before NUL.");
        foreach (var sign in signs)
        {
            byte[] bytes = sign.Message.CopyNativeBytes();
            byte[][] expected = bytes.SplitNativeLines();
            byte[][] observed = DrawnLines(bytes, 1000);
            check(observed.Length == expected.Length && observed.Zip(expected).All(p => p.First.SequenceEqual(p.Second)),
                $"Completed native display retains every source line entry{sign.DataWadEntry} T{sign.TrueIndex}.");
        }
        source.VerifyUnchanged();
    }

    // Independent bounded behavioral transcription of source8001B294..B348:
    // FF or reveal index flushes; NUL only exits. This is not a GPU emulator.
    private static byte[][] DrawnLines(byte[] message, int reveal)
    {
        byte[] terminated = message.Append((byte)0).ToArray(); List<byte[]> result = []; List<byte> line = [];
        for (int index = 0; index < terminated.Length; index++)
        {
            byte value = terminated[index];
            if (value == 0xFF || index == reveal)
            {
                if (line.Count != 0) result.Add(line.ToArray());
                line.Clear();
            }
            else line.Add(value);
            if (index == reveal || value == 0) return result.ToArray();
        }
        throw new Exception("Bounded modal fixture failed to reach its NUL.");
    }
    private static byte[][] SplitNativeLines(this byte[] message)
    {
        List<byte[]> result = []; int start = 0;
        for (int index = 0; index < message.Length; index++)
            if (message[index] == 0xFF)
            {
                if (index != start) result.Add(message[start..index]);
                start = index + 1;
            }
        return result.ToArray();
    }
}
