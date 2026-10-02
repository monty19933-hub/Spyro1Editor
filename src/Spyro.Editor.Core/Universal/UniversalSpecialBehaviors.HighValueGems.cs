namespace Spyro.Editor.Core.Universal;

public static partial class UniversalSpecialBehaviors
{
    /// <summary>Placed loose gems: spin, proximity pickup, exact native treasure/save
    /// award, bounded idle/collection effects, and retirement. Native MarkCollected owns the popup.</summary>
    internal static UniversalAsmPart HighValueGemUpdate(IReadOnlyCollection<int> gemClasses, string tag, bool remapSaveRows = false)
    {
        ValidateHighGems(gemClasses);
        const int S2 = 18, S3 = 19;
        var a = new Asm(); a.L(tag);
        a.I(9, Sp, Sp, -0x30); a.I(43, Sp, Ra, 0x20); a.I(43, Sp, S0, 0x10);
        a.I(43, Sp, S1, 0x14); a.I(43, Sp, S2, 0x18); a.I(43, Sp, S3, 0x1C);
        a.I(43, Sp, Zero, 0x24); // Per-scan idle budget; shared effect count lives at+28.
        a.I(15, Zero, T0, 0x8007); a.I(35, T0, S0, 0x5828); a.I(35, T0, S2, 0x5890); a.Nop();
        a.L(tag + "_loop");
        a.R(S0, S2, V0, 0, 0x2B); a.Br(4, V0, Zero, tag + "_done"); a.R(S0, Zero, S1, 0, 0x21);
        a.I(9, S0, S0, 0x58);
        a.I(37, S1, T0, 0x36); a.Nop();
        foreach (int cls in gemClasses.Order())
        {
            a.I(9, Zero, T1, cls); a.Br(4, T0, T1, tag + "_gem"); a.Nop();
        }
        a.J(tag + "_loop"); a.Nop(); a.L(tag + "_gem");
        a.I(35, S1, S3, 0); a.Nop(); a.Br(4, S3, Zero, tag + "_loop"); a.Nop();
        a.I(36, S1, V0, 0x48); a.Nop(); a.I(11, V0, V0, 0x80);
        a.Br(4, V0, Zero, tag + "_loop"); a.Nop();
        // Suppress already saved placed gems independently of the retail loader's
        // class/kind policy. This same logical bit is written by native MarkCollected.
        a.I(15, Zero, T0, 0x8007); a.I(35, T0, T1, 0x5828); a.Nop();
        a.R(S1, T1, T3, 0, 0x23); a.I(9, Zero, T4, 0x58); a.R(T3, T4, Zero, 0, 0x1B);
        a.R(Zero, Zero, T3, 0, 0x12); a.R(Zero, Zero, T4, 0, 0x10);
        a.Br(5, T4, Zero, tag + "_loop"); a.Nop();
        if (remapSaveRows)
        {
            a.R(T3, Zero, A2, 0, 0x21);
            a.Jal(Spyro.Editor.Core.Exporting.NativeMobySaveMap.MapRowAddress); a.Nop();
            a.R(A2, Zero, T3, 0, 0x21);
        }
        a.I(11, T3, T4, 256); a.Br(4, T4, Zero, tag + "_loop"); a.Nop();
        a.I(15, Zero, T0, 0x8007); a.I(35, T0, T1, 0x5964); a.Nop();
        a.I(11, T1, T2, 36); a.Br(4, T2, Zero, tag + "_loop"); a.R(Zero, T1, T1, 5, 0);
        a.R(Zero, T3, T2, 5, 2); a.R(Zero, T2, T2, 2, 0); a.R(T1, T2, T1, 0, 0x21);
        a.La(T0, CollectedMasks); a.R(T0, T1, T0, 0, 0x21); a.I(35, T0, T1, 0);
        a.I(12, T3, T3, 31); a.R(T3, T1, T1, 0, 6); a.I(12, T1, T1, 1);
        a.Br(4, T1, Zero, tag + "_uncollected"); a.Nop();
        a.Jal(FreeMoby); a.R(S1, Zero, A0, 0, 0x21);
        a.J(tag + "_loop"); a.Nop(); a.L(tag + "_uncollected");
        a.I(36, S1, V0, 0x46); a.Nop(); a.I(9, V0, V0, 3); a.I(40, S1, V0, 0x46);
        a.La(T0, SpyroPosition);
        a.I(35, T0, T1, 0); a.I(35, T0, T2, 4); a.I(35, T0, T3, 8);
        a.I(35, S1, T4, 0xC); a.I(35, S1, T5, 0x10); a.I(35, S1, T6, 0x14); a.Nop();
        foreach (var (d, s, suffix) in new[] { (T4, T1, "_x"), (T5, T2, "_y"), (T6, T3, "_z") })
        {
            a.R(d, s, d, 0, 0x23); a.Br(1, d, 1, tag + suffix); a.Nop(); a.R(Zero, d, d, 0, 0x23);
            a.L(tag + suffix);
        }
        foreach (var (d, radius) in new[] { (T4, PickupRadius), (T5, PickupRadius), (T6, PickupHeight) })
        { a.I(11, d, V0, radius); a.Br(4, V0, Zero, tag + "_idle"); a.Nop(); }
        a.I(37, S1, T0, 0x36); a.Nop(); a.I(9, Zero, T1, UniversalHighValueGems.TealClass);
        a.Br(5, T0, T1, tag + "_rainbow"); a.Nop();
        a.I(9, Zero, A0, 50); a.J(tag + "_award"); a.Nop();
        a.L(tag + "_rainbow"); a.I(9, Zero, A0, 100);
        a.L(tag + "_award"); a.Jal(MarkCollected); a.R(S1, Zero, A1, 0, 0x21);
        a.I(9, Zero, V0, 6); a.I(43, Sp, V0, 0x28);
        a.I(9, Zero, V0, UniversalHighValueGems.IdleSparkleLimit); a.I(43, Sp, V0, 0x24);
        // Type12/kind2 copies the actor coordinates synchronously. Each RGB channel
        // is a multiple of eight, so the original subtract-eight lifetime terminates.
        a.L(tag + "_particle_colour");
        a.I(37, S1, T0, 0x36); a.Nop(); a.I(9, Zero, T1, UniversalHighValueGems.TealClass);
        a.La(A3, UniversalHighValueGems.TealParticleColour); a.Br(4, T0, T1, tag + "_effect"); a.Nop();
        a.La(A3, UniversalHighValueGems.RainbowParticleColour); a.L(tag + "_effect");
        a.La(V0, ParticleSpawnGlobal); a.I(35, V0, V0, 0); a.I(35, Sp, A0, 0x28);
        a.I(9, Zero, A1, 12); a.R(S1, Zero, A2, 0, 0x21); a.R(V0, Zero, Ra, 0, 9); a.Nop();
        a.I(35, Sp, V0, 0x28); a.I(9, Zero, T0, 1);
        a.Br(4, V0, T0, tag + "_loop"); a.Nop();
        a.Jal(FreeMoby); a.R(S1, Zero, A0, 0, 0x21);
        a.J(tag + "_loop"); a.Nop();
        a.L(tag + "_idle");
        // The OR test is an exact nearby cube because the bound is a power of two.
        // Do not consume particles for distant or modal/transition-hidden gems.
        a.R(T4, T5, V0, 0, 0x25); a.R(V0, T6, V0, 0, 0x25);
        a.I(11, V0, V0, UniversalHighValueGems.IdleSparkleRadius);
        a.Br(4, V0, Zero, tag + "_loop"); a.Nop();
        a.I(15, Zero, T0, 0x8007); a.I(35, T0, V0, 0x5690); a.Nop();
        a.Br(5, V0, Zero, tag + "_loop"); a.Nop();
        a.I(35, T0, V0, 0x57D8); a.Nop(); a.Br(5, V0, Zero, tag + "_loop"); a.Nop();
        // Idle cosmetics must not trigger the native allocator's random replacement
        // fallback at capacity. Its certified next-free pointer includes freed slots.
        a.I(35, T0, T1, 0x5824); a.I(35, T0, T2, 0x5738); a.I(9, T1, T1, 0x2000);
        a.Br(4, T1, T2, tag + "_loop"); a.Nop();
        // Word4 is a transient active-only sparkle cooldown; native glyphs own their properties.
        a.I(35, S3, V0, 4); a.Nop(); a.I(9, V0, V0, -1);
        a.Br(7, V0, Zero, tag + "_loop"); a.I(43, S3, V0, 4);
        // Eight attempts per scan and a thirty-two-update cooldown prevent scan
        // starvation for all 256 admitted placed rows. At native lifetime16 this
        // occupies at most128 of the existing256 particle records, without a heap.
        a.I(35, Sp, V0, 0x24); a.Nop(); a.I(11, V0, T0, UniversalHighValueGems.IdleSparkleLimit);
        a.Br(4, T0, Zero, tag + "_loop"); a.Nop();
        a.I(9, V0, V0, 1); a.I(43, Sp, V0, 0x24);
        a.I(9, Zero, V0, UniversalHighValueGems.IdleSparkleFrames); a.I(43, S3, V0, 4);
        a.I(9, Zero, V0, 1); a.I(43, Sp, V0, 0x28);
        a.J(tag + "_particle_colour"); a.Nop();
        a.L(tag + "_done");
        a.I(35, Sp, Ra, 0x20); a.I(35, Sp, S0, 0x10); a.I(35, Sp, S1, 0x14);
        a.I(35, Sp, S2, 0x18); a.I(35, Sp, S3, 0x1C); a.Nop();
        a.Part.Emit(UniversalMips.JrRa); a.I(9, Sp, Sp, 0x30);
        return a.Part;
    }

    private static void ValidateHighGems(IReadOnlyCollection<int> classes)
    {
        if (classes.Count == 0 || classes.Distinct().Count() != classes.Count || classes.Any(c => !UniversalHighValueGems.IsClass(c)))
            throw new InvalidDataException("High-value gem behavior requires its unique certified gem classes.");
    }
}
