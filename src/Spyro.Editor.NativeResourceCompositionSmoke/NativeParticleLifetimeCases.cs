using System.Buffers.Binary;
using Spyro.Editor.Core.Exporting;

internal static class NativeParticleLifetimeCases
{
    internal static void Run(Action<bool, string> check, Action<Action, string> reject)
    {
        byte[] window = Words(0x97EBFFF0, 0x8FE3FFEC, 0x000B6202, 0x00AC2822, 0x04A0FDC4,
            0x316C00FF, 0x000C60C0, 0x002C0820, 0x8C2B0004, 0x8C2C0008);
        var decoded = NativeParticleFrameWindowCodec.Decode(window);
        check(decoded == new NativeParticleFrameWindow(0x10, 8, 4, 12), "actual low frame byte and two descriptor words own exactly eight payload bytes");
        foreach (int offset in new[] { 0, 20, 24, 28, 32, 36 })
        { byte[] bad = window.ToArray(); bad[offset] ^= 1; reject(() => NativeParticleFrameWindowCodec.Decode(bad), "frame/stride/base/read offset mutation refused"); }
        reject(() => NativeParticleFrameWindowCodec.Decode(window[..^1]), "partial renderer window refused");
        reject(() => NativeParticleFrameWindowCodec.Decode([.. window, 0]), "unowned renderer suffix refused");
        byte[] record = Convert.FromHexString("07000800E039660EFF582800");
        NativeParticleFrameWindowCodec.RequireFrameOwner(record, 0);
        check(true, "single frame reads through byte11, not next sound component");
        foreach (int frame in new[] { -1, 1, 255, int.MaxValue })
            reject(() => NativeParticleFrameWindowCodec.RequireFrameOwner(record, frame), "nonzero or invalid frame cannot borrow following resource bytes");
        reject(() => NativeParticleFrameWindowCodec.RequireFrameOwner(record[..^1], 0), "truncated last descriptor refused");
        byte[] wrong = record.ToArray(); wrong[2] = 16;
        reject(() => NativeParticleFrameWindowCodec.RequireFrameOwner(wrong, 0), "unissued wider payload refused");
        check(typeof(NativeDarkHollowWizardParticleLifetimeContract).GetConstructors().Length == 0, "caller metadata cannot issue native lifetime proof");
    }

    internal static void RunSource(NativeDarkHollowWizardParticleBehaviorSource source,
        Action<bool, string> check, Action<Action, string> reject)
    {
        var proof = source.LifetimeContract;
        check(proof.SingleFrameOwnershipProven && proof.MaxFrameIndex == 0 && proof.MaxAgeUpdates == 25 &&
            proof.MaxAgeCheckCallsIncludingDeletion == 26, "age0 receives25 updates then deletion on26th check, not25 frames");
        check(proof.NormalPoolStart == 0x801C3BB8 && proof.NormalPoolEnd == 0x801C5BB8 &&
            !proof.GteStateProven && !proof.PeakTransientFitProven && !proof.RuntimeProven && !proof.PublicationApproved &&
            !proof.CompleteNativeClosureProven, "normal pool ownership does not imply GPU/transient/gameplay proof");
        foreach (var evidence in proof.Evidence)
        {
            byte[] bytes = proof.CopyEvidenceBytes(evidence.Id); proof.RequireEvidence(evidence.Id, bytes);
            check(bytes.Length == evidence.ByteLength, "every source certificate retains its complete immutable owner");
            bytes[bytes.Length / 2] ^= 1;
            reject(() => proof.RequireEvidence(evidence.Id, bytes), "individual lifetime/helper/pool/PETE source mutation refused");
            check(proof.CopyEvidenceBytes(evidence.Id).AsSpan().SequenceEqual(bytes) == false, "evidence output mutation does not alter source contract");
        }
        foreach (var (id, offset) in new[] { ("particle07-frame-zero-tail", 8), ("renderer-frame-window", 24),
            ("allocator", 0x24), ("random", 0), ("vector-pack", 4), ("negative-kind-prefetch", 0),
            ("pool-init-and-terminal", 0), ("normal-buffer-initializer", 4), ("PETE-header", 0), ("PETE-runtime-copy", 0x20) })
        {
            byte[] changed = proof.CopyEvidenceBytes(id); changed[offset] ^= 1;
            reject(() => proof.RequireEvidence(id, changed), "exact frame/helper/pool/PETE boundary source word independently protected");
        }
        byte[] tick = proof.CopyEvidenceBytes("particle07-tick");
        NativeParticleFrameWindowCodec.RequireAgeOperations(tick);
        foreach (int offset in new[] { 0, 8, 12, 0x5C, 0x7C, 0x80 })
        { byte[] bad = tick.ToArray(); bad[offset] ^= 1; reject(() => NativeParticleFrameWindowCodec.RequireAgeOperations(bad), "native age load/boundary/increment/store independently bound"); }
        reject(() => NativeParticleFrameWindowCodec.RequireAgeOperations(tick[..^1]), "partial age update owner refused");
        reject(() => proof.RequireEvidence("caller-invented", [0]), "caller cannot introduce lifetime evidence owner");
        reject(() => proof.RequireEvidence("PETE-header", proof.CopyEvidenceBytes("PETE-header")[..^1]), "partial resident-owner certificate refused");
        // Abstract arithmetic replay: this does not execute native helpers, GTE, particle motion or renderer.
        byte[] descriptor = Convert.FromHexString("07000800E039660EFF582800");
        uint frameStore = BinaryPrimitives.ReadUInt32LittleEndian(proof.CopyEvidenceBytes("particle07-frame-zero-tail").AsSpan(8));
        check(frameStore >> 26 == 0x28 && ((frameStore >> 21) & 31) == 18 && ((frameStore >> 16) & 31) == 0 &&
            (short)frameStore == 0x10, "decode exact source SB zero,raw-frame for abstract store application");
        bool matrixPassed = true; int samples = 0;
        for (int stale = 0; stale < 256; stale++)
        for (int initial = 0; initial < 16; initial++)
        {
            byte[] raw = new byte[32]; raw[0x10] = (byte)stale;
            raw[(short)frameStore] = 0; // Apply only the decoded source SB-zero effect, not a CPU instruction emulator.
            int frame = raw[0x10], age = initial, updates = 0, calls = 0;
            while (true)
            {
                calls++;
                if (age >= 25) break;
                NativeParticleFrameWindowCodec.RequireFrameOwner(descriptor, frame);
                age++; updates++;
            }
            matrixPassed &= frame == 0 && updates == 25 - initial && calls == updates + 1; samples++;
        }
        check(matrixPassed && samples == 4096, "complete abstract stale-frame/initial-age matrix");
        Console.WriteLine($"NATIVE_PARTICLE_LIFETIME_ABSTRACT_MATRIX samples={samples} nativeCpuEmulated=false nativeGpuEmulated=false");
    }
    private static byte[] Words(params uint[] values)
    { byte[] b = new byte[values.Length * 4]; for (int i = 0; i < values.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(i * 4), values[i]); return b; }
}
