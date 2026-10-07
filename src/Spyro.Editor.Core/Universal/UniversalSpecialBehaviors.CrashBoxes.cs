namespace Spyro.Editor.Core.Universal;

public static partial class UniversalSpecialBehaviors
{
    // Engine facts used by the Crash boxes (USA SCUS_942.28):
    // - Spyro's flame marks objects it touches with 0x10000 in the object's hit word (+0x18), his charge with
    //   0x20000; 0x80000 also breaks a basket (0xC2 breaks on 0xB0000). The word is only cleared at spawn.
    // - 0x8004E2E8 (position*, radius, mask): sphere test against Spyro (his radius 0x164 is added), ORs the mask
    //   into his damage word 0x80078A84 and returns 1 on contact. Mask 1 costs one Sparx level; at Sparx 0
    //   (0x80078BBC) the next hit is fatal.
    // - 0x8003851C (object, sound slot, 0) plays the model's sound; 0x8003ABC0 (object, 3, 0, 0) drops the
    //   object's reward (+0x53: gem class, otherwise a butterfly); 0x8003B7C0 (object) keeps it destroyed.
    // - Spyro's movement state 0x80078AD0 (5 = rising jump); writing 5 with an upward speed in 0x80078B48,
    //   0x80078B54 and 0x80078B6C makes a normal jump arc.
    // - Particles 2 (flash and smoke) and 70 (sparks) are in every ground level's particle table.
    private const uint DamageHelper = 0x8004E2E8, DamageFlags = 0x80078A84, SparxHealth = 0x80078BBC;
    private const uint PlayModelSound = 0x8003851C, DropReward = 0x8003ABC0, KeepDestroyed = 0x8003B7C0;
    private const uint MovementState = 0x80078AD0, VerticalSpeed = 0x80078B48;
    private const int RisingJump = 5, BounceSpeed = 4400;
    private const int HitMask = 0x000B0000, ChainHit = 0x00080000;
    public const int CrashBoxCentre = 368, CrashOnTopHalf = 0x1C0, CrashOnTopLow = 0x380, CrashOnTopHigh = 0x4C0;
    public const int CrashTntBlast = 0x600, CrashNitroBlast = 0x200, CrashNitroTouch = 0x1E0, CrashChainReach = 0x700;
    public const int CrashTntStepFrames = 30;
    private const int T7 = 15, S2 = 18, S3 = 19, S4 = 20;

    /// <summary>Per-frame Crash box update (see <see cref="UniversalCrashBoxes"/>): breaks, countdowns, explosions,
    /// chain reactions and the Nitro hop, over the engine's active object list.</summary>
    internal static UniversalAsmPart CrashBoxUpdate(IReadOnlyCollection<int> classes, string tag)
    {
        if (classes.Count == 0 || classes.Any(c => !UniversalCrashBoxes.IsClass(c)) || classes.Distinct().Count() != classes.Count)
            throw new InvalidDataException("Crash box behaviour needs distinct Crash box classes.");
        int min = UniversalCrashBoxes.AllClasses.Min(), max = UniversalCrashBoxes.AllClasses.Max();
        var a = new Asm();
        int labels = 0;
        string L(string name) => $"{tag}_{name}_{labels++}";
        void Li(int reg, int value)
        {
            if (value is >= short.MinValue and <= short.MaxValue) a.I(9, Zero, reg, value);
            else { a.I(15, 0, reg, (int)((uint)value >> 16)); a.I(13, reg, reg, value & 0xFFFF); }
        }
        void Move(int to, int from) => a.R(from, Zero, to, 0, 0x21);
        // |x| into the same register.
        void Abs(int reg)
        {
            string done = L("abs");
            a.Br(1, reg, 1, done); a.Nop();
            a.R(Zero, reg, reg, 0, 0x23);
            a.L(done);
        }
        // Falls through when the class in `reg` is a Crash box class, else branches to notBox (scratch: t3).
        void IsBox(int reg, string notBox)
        {
            string box = L("box");
            a.I(9, reg, T3, -min); a.I(11, T3, T3, max - min + 1); a.Br(4, T3, Zero, notBox); a.Nop();
            foreach (int c in UniversalCrashBoxes.AllClasses) { Li(T3, c); a.Br(4, reg, T3, box); a.Nop(); }
            a.J(notBox); a.Nop();
            a.L(box);
        }
        void Particle(int count, int type, int extra)
        {
            a.La(V0, ParticleSpawnGlobal); a.I(35, V0, V0, 0);
            Li(A0, count); Li(A1, type); a.I(9, S1, A2, 0xC);
            a.R(V0, 0, Ra, 0, 0x09); Li(A3, extra);
        }
        // Model sound slot 0: break or explosion; slot 1: the TNT beep or the Nitro rattle.
        void Sound(int slot = 0)
        {
            Li(V0, 0x20); a.I(40, S1, V0, 0x55);
            Move(A0, S1); Li(A1, slot); a.Jal(PlayModelSound); Li(A2, 0);
        }
        // The box centre (base + CrashBoxCentre up) into the scratch vector at sp+0x30; a0 points at it.
        void Centre()
        {
            a.I(35, S1, T0, 0xC); a.I(35, S1, T1, 0x10); a.I(35, S1, T2, 0x14); a.Nop();
            a.I(9, T2, T2, CrashBoxCentre);
            a.I(43, Sp, T0, 0x30); a.I(43, Sp, T1, 0x34); a.I(43, Sp, T2, 0x38);
            a.I(9, Sp, A0, 0x30);
        }
        // v0 = 1 while Spyro stands on (or drops onto) the box top and is not rising.
        void OnTop()
        {
            string end = L("ontop");
            a.La(T0, SpyroPosition);
            a.I(35, T0, T1, 0); a.I(35, S1, T4, 0xC); a.I(35, T0, T2, 4); a.I(35, S1, T5, 0x10);
            a.I(35, T0, T3, 8); a.I(35, S1, T6, 0x14); a.Nop();
            Li(V0, 0);
            a.R(T1, T4, T1, 0, 0x23); Abs(T1); a.I(11, T1, T7, CrashOnTopHalf); a.Br(4, T7, Zero, end); a.Nop();
            a.R(T2, T5, T2, 0, 0x23); Abs(T2); a.I(11, T2, T7, CrashOnTopHalf); a.Br(4, T7, Zero, end); a.Nop();
            a.R(T3, T6, T3, 0, 0x23); a.I(9, T3, T3, -CrashOnTopLow);
            a.I(11, T3, T7, CrashOnTopHigh - CrashOnTopLow); a.Br(4, T7, Zero, end); a.Nop();
            a.La(T0, MovementState); a.I(35, T0, T1, 0); Li(T2, RisingJump);
            a.Br(4, T1, T2, end); a.Nop();
            Li(V0, 1);
            a.L(end);
        }
        void Bounce()
        {
            a.La(T0, MovementState); Li(T1, RisingJump); a.I(43, T0, T1, 0);
            a.La(T0, VerticalSpeed); Li(T1, BounceSpeed);
            a.I(43, T0, T1, 0); a.I(43, T0, T1, 0xC); a.I(43, T0, T1, 0x24);
        }
        void Retire()
        {
            a.Jal(KeepDestroyed); Move(A0, S1);
            a.Jal(FreeMoby); Move(A0, S1);
            a.J(tag + "_loop"); a.Nop();
        }

        a.L(tag);
        a.I(9, Sp, Sp, -0x48); a.I(43, Sp, Ra, 0x40);
        a.I(43, Sp, S0, 0x10); a.I(43, Sp, S1, 0x14); a.I(43, Sp, S2, 0x18); a.I(43, Sp, S3, 0x1C); a.I(43, Sp, S4, 0x20);
        a.La(S0, ActiveList);
        a.L(tag + "_loop");
        a.I(35, S0, S1, 0); a.Nop();
        a.Br(4, S1, Zero, tag + "_done"); a.I(9, S0, S0, 4);
        a.I(37, S1, S2, 0x36); a.Nop();
        IsBox(S2, tag + "_loop");
        a.I(36, S1, V0, 0x48); a.Nop();
        a.I(11, V0, V0, 0x80); a.Br(4, V0, Zero, tag + "_loop"); a.Nop();
        a.I(35, S1, S3, 0x18); a.Nop();
        Li(T0, UniversalCrashBoxes.NitroClass); a.Br(4, S2, T0, tag + "_nitro"); a.Nop();
        foreach (int c in UniversalCrashBoxes.TntClasses) { Li(T0, c); a.Br(4, S2, T0, tag + "_tnt"); a.Nop(); }

        // Crate: flame, charge, a blast or a jump on top breaks it.
        Li(T0, HitMask); a.R(S3, T0, T0, 0, 0x24); a.Br(5, T0, Zero, tag + "_crate_break"); a.Nop();
        OnTop(); a.Br(4, V0, Zero, tag + "_loop"); a.Nop();
        Bounce();
        a.L(tag + "_crate_break");
        Sound();
        Particle(5, 2, 0);
        Particle(16, 70, 0x20);
        Move(A0, S1); Li(A1, 3); Li(A2, 0); a.Jal(DropReward); Li(A3, 0);
        Retire();

        // TNT: flame, charge or a blast sets it off at once; a jump on top bounces Spyro and starts 3-2-1.
        a.L(tag + "_tnt");
        Li(T0, HitMask); a.R(S3, T0, T0, 0, 0x24); a.Br(5, T0, Zero, tag + "_tnt_explode"); a.Nop();
        OnTop(); a.Br(4, V0, Zero, tag + "_tnt_count"); a.Nop();
        Bounce();
        Li(T0, UniversalCrashBoxes.TntClass); a.Br(5, S2, T0, tag + "_tnt_count"); a.Nop();
        Li(T0, UniversalCrashBoxes.Tnt3Class); a.I(41, S1, T0, 0x36);
        Li(T0, 1); a.I(40, S1, T0, 0x48);
        a.L(tag + "_tnt_beep");
        Sound(1);
        a.J(tag + "_loop"); a.Nop();
        a.L(tag + "_tnt_count");
        Li(T0, UniversalCrashBoxes.TntClass); a.Br(4, S2, T0, tag + "_loop"); a.Nop();
        a.I(36, S1, T1, 0x48); a.Nop(); a.I(9, T1, T1, 1); a.I(40, S1, T1, 0x48);
        Li(T0, CrashTntStepFrames + 1); a.Br(5, T1, T0, tag + "_tnt_c1"); a.Nop();
        Li(T0, UniversalCrashBoxes.Tnt2Class); a.J(tag + "_tnt_beep"); a.I(41, S1, T0, 0x36);
        a.L(tag + "_tnt_c1");
        Li(T0, 2 * CrashTntStepFrames + 1); a.Br(5, T1, T0, tag + "_tnt_c2"); a.Nop();
        Li(T0, UniversalCrashBoxes.Tnt1Class); a.J(tag + "_tnt_beep"); a.I(41, S1, T0, 0x36);
        a.L(tag + "_tnt_c2");
        a.I(11, T1, T0, 3 * CrashTntStepFrames + 1); a.Br(5, T0, Zero, tag + "_loop"); a.Nop();
        a.L(tag + "_tnt_explode");
        a.J(tag + "_explode"); Move(S4, Zero);

        // Nitro: anything that touches it sets it off; flame from a distance is the safe way.
        a.L(tag + "_nitro");
        Li(T0, HitMask); a.R(S3, T0, T0, 0, 0x24); a.Br(5, T0, Zero, tag + "_nitro_explode"); a.Nop();
        Centre(); Li(A1, CrashNitroTouch); a.Jal(DamageHelper); Li(A2, 0);
        a.Br(5, V0, Zero, tag + "_nitro_explode"); a.Nop();
        // Hop: props word 0 = resting height (word 1 marks it set), word 2 counts frames; a short arc every
        // 64 frames, offset per object so a group does not hop together.
        a.I(35, S1, T0, 0); a.Nop();
        a.Br(4, T0, Zero, tag + "_loop"); a.Nop();
        a.I(35, T0, T1, 4); a.Nop();
        a.Br(5, T1, Zero, tag + "_hop_ready"); a.Nop();
        a.I(35, S1, T2, 0x14); Li(T1, 1); a.I(43, T0, T2, 0); a.I(43, T0, T1, 4);
        a.L(tag + "_hop_ready");
        a.I(35, T0, T1, 8); a.Nop(); a.I(9, T1, T1, 1); a.I(43, T0, T1, 8);
        a.R(0, S1, T2, 4, 0x02); a.I(12, T2, T2, 31); a.R(0, T2, T3, 2, 0); a.R(T2, T3, T2, 0, 0x21);
        a.R(T1, T2, T1, 0, 0x21); a.I(12, T1, T1, 63);
        Li(T3, 0); a.I(11, T1, T2, 8); a.Br(4, T2, Zero, tag + "_hop_set"); a.Nop();
        Li(T3, 8); a.R(T3, T1, T3, 0, 0x23); a.R(T1, T3, 0, 0, 0x18); a.R(0, 0, T3, 0, 0x12);
        a.R(0, T3, T4, 3, 0); a.R(0, T3, T3, 2, 0); a.R(T3, T4, T3, 0, 0x21);
        a.L(tag + "_hop_set");
        a.I(35, T0, T2, 0); a.Nop(); a.R(T2, T3, T2, 0, 0x21); a.I(43, S1, T2, 0x14);
        a.Br(5, T1, Zero, tag + "_loop"); a.Nop();
        Sound(1);                                              // the rattle as each hop starts
        a.J(tag + "_loop"); a.Nop();
        a.L(tag + "_nitro_explode");
        Li(S4, 1);

        // Explosion: sound, flash, smoke and sparks; harm Spyro; mark nearby boxes and baskets so they go next.
        a.L(tag + "_explode");
        Sound();
        Particle(5, 2, 0);
        Particle(5, 2, 0);
        Particle(16, 70, 0x20);
        Particle(3, 70, 0x10);
        a.Br(4, S4, Zero, tag + "_tnt_blast"); a.Nop();
        Centre(); Li(A1, CrashNitroBlast); a.Jal(DamageHelper); Li(A2, 0);
        a.Br(4, V0, Zero, tag + "_chain"); a.Nop();
        a.La(T0, SparxHealth); a.I(43, T0, Zero, 0);
        a.La(T0, DamageFlags); a.I(35, T0, T1, 0); a.Nop(); a.I(13, T1, T1, 1); a.I(43, T0, T1, 0);
        a.J(tag + "_chain"); a.Nop();
        a.L(tag + "_tnt_blast");
        Centre(); Li(A1, CrashTntBlast); a.Jal(DamageHelper); Li(A2, 1);
        a.L(tag + "_chain");
        a.La(T0, ActiveList);
        a.L(tag + "_near");
        a.I(35, T0, T1, 0); a.Nop();
        a.Br(4, T1, Zero, tag + "_near_done"); a.I(9, T0, T0, 4);
        a.Br(4, T1, S1, tag + "_near"); a.Nop();
        a.I(37, T1, T2, 0x36); a.Nop();
        Li(T3, UniversalCrashBoxes.BasketClass); a.Br(4, T2, T3, tag + "_near_alive"); a.Nop();
        IsBox(T2, tag + "_near");
        a.L(tag + "_near_alive");
        a.I(36, T1, T3, 0x48); a.Nop(); a.I(11, T3, T3, 0x80); a.Br(4, T3, Zero, tag + "_near"); a.Nop();
        foreach (int axis in new[] { 0xC, 0x10, 0x14 })
        {
            a.I(35, T1, T3, axis); a.I(35, S1, T4, axis); a.Nop();
            a.R(T3, T4, T3, 0, 0x23); Abs(T3);
            a.I(11, T3, T4, CrashChainReach); a.Br(4, T4, Zero, tag + "_near"); a.Nop();
        }
        a.I(35, T1, T3, 0x18); Li(T4, ChainHit); a.R(T3, T4, T3, 0, 0x25); a.I(43, T1, T3, 0x18);
        a.J(tag + "_near"); a.Nop();
        a.L(tag + "_near_done");
        Retire();

        a.L(tag + "_done");
        a.I(35, Sp, Ra, 0x40);
        a.I(35, Sp, S0, 0x10); a.I(35, Sp, S1, 0x14); a.I(35, Sp, S2, 0x18); a.I(35, Sp, S3, 0x1C); a.I(35, Sp, S4, 0x20);
        a.Part.Emit(UniversalMips.JrRa); a.I(9, Sp, Sp, 0x48);
        return a.Part;
    }
}
