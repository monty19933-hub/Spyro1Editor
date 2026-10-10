namespace Spyro.Editor.Core.Universal;

public static partial class UniversalSpecialBehaviors
{
    // Crash enemies (see UniversalCrashEnemies). Engine facts, beyond the Crash boxes' ones:
    // - The level's update advances every active object's animation (0x800522C0 over the active list) before the
    //   bundle runs: writing +0x3D (next animation) and +0x3F (next frame) switches at the next frame step, and
    //   +0x42 bit 1 says the current frame is the animation's last. +0x3E is the current frame.
    // - +0x46 is the yaw (256 per turn); a model faces its +x at yaw 0. Spyro's movement state 0x80078AD0 is 11
    //   while he charges and 5 for a whole jump.
    // Each placed enemy has 16 bytes of properties (moby +0): home x, y, z (the knocked-flying velocity once it is
    // hit), then state (+0xC), hop step x and y (+0xD, +0xE, signed) and a timer (+0xF).
    public const int CrashEnemyFrog = 0, CrashEnemyKnight = 1, CrashEnemyWizard = 2;
    private const int EnemyInit = 0, EnemyIdle = 1, EnemyMove = 2, EnemyAttack = 3, EnemyRobeOff = 8, EnemyFlying = 9;
    private const int ChargeState = 11, JumpState = 5;
    private const int FlightTicks = 40, FlightSpeed = 110, FlightRise = 150, FlightFall = 9;
    /// <summary>Yaw that faces world angle a (256 per turn, from +x towards +y): +1 when the game turns models the
    /// same way, -1 when it turns them the other way.</summary>
    public const int CrashYawSense = 1;

    /// <summary>Per-frame Crash enemy update over the engine's active object list.</summary>
    internal static UniversalAsmPart CrashEnemyUpdate(IReadOnlyCollection<int> classes, string tag)
    {
        if (classes.Count == 0 || classes.Any(c => !UniversalCrashEnemies.IsClass(c)) || classes.Distinct().Count() != classes.Count)
            throw new InvalidDataException("Crash enemy behaviour needs distinct Crash enemy classes.");
        (int Class, int Kind, UniversalCrashEnemies.Tuning T) Tuning(int c)
        {
            var e = UniversalCrashEnemies.ForClass(c)!;
            return (c, e.Kind >= 0 ? e.Kind : e == UniversalCrashEnemies.Frog ? CrashEnemyFrog : e == UniversalCrashEnemies.Knight ? CrashEnemyKnight : CrashEnemyWizard, e.Tuning);
        }
        var tunings = classes.Order().Select(Tuning).ToList();
        if (tunings.Any(t => t.Kind == CrashEnemyWizard) && !classes.Contains(UniversalCrashEnemies.Wizard.Companion!.Class))
            throw new InvalidDataException("The Crash wizard needs its robe-off model.");
        const int S2 = 18, S3 = 19, S4 = 20, S5 = 21, S6 = 22, S7 = 23, Fp = 30;
        // Scratch: sp+0x30..0x38 hold a direction across _atan, sp+0x40/0x44 one across _len, sp+0x48 the tuning row.
        var a = new Asm();
        int labels = 0;
        string L(string name) => $"{tag}_{name}_{labels++}";
        void Li(int reg, int value)
        {
            if (value is >= short.MinValue and <= short.MaxValue) a.I(9, Zero, reg, value);
            else { a.I(15, 0, reg, (int)((uint)value >> 16)); a.I(13, reg, reg, value & 0xFFFF); }
        }
        void Move(int to, int from) => a.R(from, Zero, to, 0, 0x21);
        void Addu(int rd, int rs, int rt) => a.R(rs, rt, rd, 0, 0x21);
        void Subu(int rd, int rs, int rt) => a.R(rs, rt, rd, 0, 0x23);
        void Slt(int rd, int rs, int rt) => a.R(rs, rt, rd, 0, 0x2A);
        void Sll(int rd, int rt, int sh) => a.R(0, rt, rd, sh, 0x00);
        void Sra(int rd, int rt, int sh) => a.R(0, rt, rd, sh, 0x03);
        void Lw(int rt, int b, int off) { a.I(35, b, rt, off); a.Nop(); }
        void Lbu(int rt, int b, int off) { a.I(36, b, rt, off); a.Nop(); }
        void Lb(int rt, int b, int off) { a.I(32, b, rt, off); a.Nop(); }
        void Sw(int rt, int b, int off) => a.I(43, b, rt, off);
        void Sb(int rt, int b, int off) => a.I(40, b, rt, off);
        void Beq(int rs, int rt, string l) { a.Br(4, rs, rt, l); a.Nop(); }
        void Bne(int rs, int rt, string l) { a.Br(5, rs, rt, l); a.Nop(); }
        void Bltz(int r, string l) { a.Br(1, r, 0, l); a.Nop(); }
        void Bgez(int r, string l) { a.Br(1, r, 1, l); a.Nop(); }
        void Blez(int r, string l) { a.Br(6, r, 0, l); a.Nop(); }
        void Bgtz(int r, string l) { a.Br(7, r, 0, l); a.Nop(); }
        void Jmp(string l) { a.J(l); a.Nop(); }
        void Call(string l) { a.Part.Emit(UniversalMips.EncodeJ(3, 0), new(UniversalFixKind.JalLabel, Label: l)); a.Nop(); }
        void Abs(int reg) { string done = L("abs"); a.Br(1, reg, 1, done); a.Nop(); Subu(reg, Zero, reg); a.L(done); }
        void Mflo(int rd) { a.R(0, 0, rd, 0, 0x12); a.Nop(); a.Nop(); }
        void Mult(int rd, int rs, int rt) { a.R(rs, rt, 0, 0, 0x18); Mflo(rd); }
        void Div(int rd, int rs, int rt) { a.R(rs, rt, 0, 0, 0x1A); Mflo(rd); }
        // Tuning table (data after the code): one row per class - class, kind, then the distances below. The row of
        // the object being updated is kept at sp+0x48.
        const int RowKind = 1, FTouch = 2, FTouchHigh = 3, FStompLow = 4, FStompHigh = 5, FSight = 6, FLeash = 7, FSpeed = 8,
            FStomp = 9, FAttack = 10, FAttackStop = 11, FReach = 12, RowWords = 13;
        void Tuned(int reg, int field) { Lw(reg, Sp, 0x48); Lw(reg, reg, field * 4); }
        void Particle(int count, int type, int extra, bool atSpyro = false)
        {
            a.La(V0, ParticleSpawnGlobal); a.I(35, V0, V0, 0);
            Li(A0, count); Li(A1, type);
            if (atSpyro) a.La(A2, SpyroPosition); else a.I(9, S1, A2, 0xC);
            a.R(V0, 0, Ra, 0, 0x09); Li(A3, extra);
        }
        void Sound(int slot)
        {
            Li(V0, 0x20); Sb(V0, S1, 0x55);
            Move(A0, S1); Li(A1, slot); a.Jal(PlayModelSound); Li(A2, 0);
        }
        // Switch to animation n unless it is already playing or queued.
        void Animate(int n)
        {
            string done = L("anim");
            Lbu(T0, S1, 0x3D); Li(T1, n); Beq(T0, T1, done);
            Sb(T1, S1, 0x3D); Sb(Zero, S1, 0x3F);
            a.L(done);
        }
        void SetState(int state) { Li(T0, state); Sb(T0, S3, 0xC); }
        // Turn the yaw towards world angle v0 by at most `step` (v0 is kept).
        void TurnTowards(int step)
        {
            string done = L("turn");
            if (CrashYawSense < 0) Subu(T0, Zero, V0); else Move(T0, V0);
            Lbu(T1, S1, 0x46);
            Subu(T0, T0, T1); Sll(T0, T0, 24); Sra(T0, T0, 24);   // signed difference
            Li(T2, step); Slt(T3, T2, T0); string notHigh = L("hi"); Beq(T3, Zero, notHigh); Move(T0, T2); a.L(notHigh);
            Li(T2, -step); Slt(T3, T0, T2); string notLow = L("lo"); Beq(T3, Zero, notLow); Move(T0, T2); a.L(notLow);
            Addu(T1, T1, T0); Sb(T1, S1, 0x46);
            a.L(done);
        }
        // t0 = |signed yaw difference| to world angle v0.
        void FacingError()
        {
            if (CrashYawSense < 0) Subu(T0, Zero, V0); else Move(T0, V0);
            Lbu(T1, S1, 0x46);
            Subu(T0, T0, T1); Sll(T0, T0, 24); Sra(T0, T0, 24); Abs(T0);
        }
        // Step the object `speed` units towards (dx, dy) in a0, a1 (approximate length in a2).
        void StepTowards(int speedReg)
        {
            string done = L("step");
            Blez(A2, done);
            Mult(T0, A0, speedReg); Div(T0, T0, A2);
            Mult(T1, A1, speedReg); Div(T1, T1, A2);
            Lw(T2, S1, 0xC); Addu(T2, T2, T0); Sw(T2, S1, 0xC);
            Lw(T2, S1, 0x10); Addu(T2, T2, T1); Sw(T2, S1, 0x10);
            a.L(done);
        }
        // a0, a1 = home - position; a2 = its approximate length.
        void ToHome()
        {
            Lw(A0, S3, 0); Lw(T0, S1, 0xC); Subu(A0, A0, T0);
            Lw(A1, S3, 4); Lw(T0, S1, 0x10); Subu(A1, A1, T0);
            Sw(A0, Sp, 0x40); Sw(A1, Sp, 0x44);
            Call(tag + "_len"); Move(A2, V0); Lw(A0, Sp, 0x40); Lw(A1, Sp, 0x44);
        }
        // The damage test from the enemy's position with a reach that always includes Spyro (the callers have
        // already decided he is touched), so the engine knocks him away from the enemy.
        void HurtSpyro()
        {
            Lw(T0, S1, 0xC); Lw(T1, S1, 0x10); Lw(T2, S1, 0x14);
            Sw(T0, Sp, 0x30); Sw(T1, Sp, 0x34); Sw(T2, Sp, 0x38);
            a.I(9, Sp, A0, 0x30); Li(A1, 0x2000); a.Jal(DamageHelper); Li(A2, 1);
        }

        a.L(tag);
        a.I(9, Sp, Sp, -0x58); Sw(Ra, Sp, 0x50); Sw(Fp, Sp, 0x4C);
        Sw(S0, Sp, 0x10); Sw(S1, Sp, 0x14); Sw(S2, Sp, 0x18); Sw(S3, Sp, 0x1C); Sw(S4, Sp, 0x20); Sw(S5, Sp, 0x24); Sw(S6, Sp, 0x28); Sw(S7, Sp, 0x2C);
        a.La(S0, ActiveList);
        a.L(tag + "_loop");
        a.I(35, S0, S1, 0); a.Nop();
        a.Br(4, S1, Zero, tag + "_done"); a.I(9, S0, S0, 4);
        a.I(37, S1, S4, 0x36); a.Nop();
        {
            string find = L("find"), found = L("found");
            a.Part.Emit(UniversalMips.EncodeI(15, 0, T0, 0), new(UniversalFixKind.LabelAddrHi, Label: tag + "_table"));
            a.Part.Emit(UniversalMips.EncodeI(9, T0, T0, 0), new(UniversalFixKind.LabelAddrLo, Label: tag + "_table"));
            a.L(find);
            Lw(T1, T0, 0); Beq(T1, Zero, tag + "_loop"); Beq(T1, S4, found);
            a.I(9, T0, T0, RowWords * 4); Jmp(find);
            a.L(found);
            Sw(T0, Sp, 0x48);
        }
        Lbu(V0, S1, 0x48); a.I(11, V0, V0, 0x80); Beq(V0, Zero, tag + "_loop");
        Lw(S3, S1, 0); Beq(S3, Zero, tag + "_loop");
        Lbu(T0, S3, 0xC);
        Li(T1, EnemyFlying); Beq(T0, T1, tag + "_flying");
        Li(T1, EnemyRobeOff); Beq(T0, T1, tag + "_robe");
        Bne(T0, Zero, tag + "_ready");
        // First update: remember home, start idle with a per-object phase.
        Lw(T1, S1, 0xC); Lw(T2, S1, 0x10); Lw(T3, S1, 0x14);
        Sw(T1, S3, 0); Sw(T2, S3, 4); Sw(T3, S3, 8);
        SetState(EnemyIdle);
        a.R(0, S1, T0, 4, 0x02); a.I(12, T0, T0, 31); a.I(9, T0, T0, 10); Sb(T0, S3, 0xF);
        a.L(tag + "_ready");
        // Spyro relative to the enemy: s5, s6, s7 = dx, dy, dz; fp = horizontal distance.
        a.La(T0, SpyroPosition);
        Lw(S5, T0, 0); Lw(T1, S1, 0xC); Subu(S5, S5, T1);
        Lw(S6, T0, 4); Lw(T1, S1, 0x10); Subu(S6, S6, T1);
        Lw(S7, T0, 8); Lw(T1, S1, 0x14); Subu(S7, S7, T1);
        Move(A0, S5); Call(tag + "_len_xy"); Move(Fp, V0);

        // Flame (0x10000) or charge (0x20000) knocks it flying.
        Lw(T0, S1, 0x18); a.I(15, 0, T1, 3); a.R(T0, T1, T0, 0, 0x24);
        Bne(T0, Zero, tag + "_knocked");
        // Jumped on: Spyro above its top, close, and not on his way up.
        {
            string notStomp = L("nostomp");
            Tuned(T0, FStomp); Slt(T1, Fp, T0); Beq(T1, Zero, notStomp);
            Tuned(T0, FStompLow); Slt(T1, S7, T0); Bne(T1, Zero, notStomp);
            Tuned(T0, FStompHigh); Slt(T1, S7, T0); Beq(T1, Zero, notStomp);
            a.La(T0, MovementState); Lw(T1, T0, 0); Li(T2, JumpState);
            string falling = L("falling"); Bne(T1, T2, falling);
            a.La(T0, VerticalSpeed); Lw(T1, T0, 0); Bgtz(T1, notStomp);
            a.L(falling);
            a.La(T0, MovementState); Li(T1, RisingJump); Sw(T1, T0, 0);
            a.La(T0, VerticalSpeed); Li(T1, BounceSpeed); Sw(T1, T0, 0); Sw(T1, T0, 0xC); Sw(T1, T0, 0x24);
            Sound(UniversalCrashEnemies.BounceSlot);
            Jmp(tag + "_knocked");
            a.L(notStomp);
        }
        // Touching it hurts (unless Spyro is charging into it).
        {
            string noTouch = L("notouch");
            a.La(T0, MovementState); Lw(T1, T0, 0); Li(T2, ChargeState); Beq(T1, T2, noTouch);
            Tuned(T0, FTouch); Slt(T1, Fp, T0); Beq(T1, Zero, noTouch);
            Li(T0, -300); Slt(T1, S7, T0); Bne(T1, Zero, noTouch);
            Tuned(T0, FTouchHigh); Slt(T1, S7, T0); Beq(T1, Zero, noTouch);
            HurtSpyro();
            a.L(noTouch);
        }
        // Can it see Spyro? s2 = 1 when he is within sight and not far above or below.
        {
            string no = L("nosight");
            Move(S2, Zero);
            Tuned(T0, FSight); Slt(T1, Fp, T0); Beq(T1, Zero, no);
            Move(T0, S7); Abs(T0); a.I(10, T0, T1, 2400); Beq(T1, Zero, no);
            Li(S2, 1);
            a.L(no);
        }
        Tuned(T0, RowKind);
        foreach (int kind in tunings.Select(t => t.Kind).Distinct())
        {
            Li(T1, kind);
            Beq(T0, T1, kind switch { CrashEnemyFrog => tag + "_frog", CrashEnemyKnight => tag + "_knight", _ => tag + "_wizard" });
        }
        Jmp(tag + "_loop");

        // ---- Frog: sits (throat pulsing), then hops towards Spyro when it sees him, or back home.
        a.L(tag + "_frog");
        Lbu(T0, S3, 0xC); Li(T1, EnemyMove); Beq(T0, T1, tag + "_frog_hop");
        Animate(2);
        Lbu(T0, S3, 0xF); a.I(9, T0, T0, -1); Sb(T0, S3, 0xF); Bgtz(T0, tag + "_loop");
        // Pick the hop: towards Spyro (within its leash), else home if it has strayed, else in place.
        {
            string home = L("home"), go = L("go"), still = L("still");
            Beq(S2, Zero, home);
            ToHome(); Tuned(T0, FLeash); Slt(T1, A2, T0); Beq(T1, Zero, home);
            Move(A0, S5); Move(A1, S6); Move(A2, Fp); Jmp(go);
            a.L(home);
            ToHome(); a.I(10, A2, T1, 700); Bne(T1, Zero, still);
            a.L(go);
            string moving = L("moving"); Bgtz(A2, moving); Jmp(still); a.L(moving);
            Sw(A0, Sp, 0x30); Sw(A1, Sp, 0x34); Sw(A2, Sp, 0x38);
            Call(tag + "_atan"); TurnTowards(128);
            Lw(A0, Sp, 0x30); Lw(A1, Sp, 0x34); Lw(A2, Sp, 0x38);
            Tuned(T3, FSpeed);
            Mult(T0, A0, T3); Div(T0, T0, A2); Sb(T0, S3, 0xD);
            Mult(T1, A1, T3); Div(T1, T1, A2); Sb(T1, S3, 0xE);
            string started = L("started"); Jmp(started);
            a.L(still);
            Sb(Zero, S3, 0xD); Sb(Zero, S3, 0xE);
            a.L(started);
            SetState(EnemyMove); Animate(0);
        }
        Jmp(tag + "_loop");
        a.L(tag + "_frog_hop");
        {
            // Airborne frames 7..13 carry it forward; the last frame lands it.
            string notAir = L("air");
            Lbu(T0, S1, 0x3C); Bne(T0, Zero, tag + "_loop");          // still finishing the sit animation
            Lbu(T0, S1, 0x3E); a.I(11, T0, T1, 7); Bne(T1, Zero, notAir); a.I(11, T0, T1, 14); Beq(T1, Zero, notAir);
            Lb(T0, S3, 0xD); Lw(T1, S1, 0xC); Addu(T1, T1, T0); Sw(T1, S1, 0xC);
            Lb(T0, S3, 0xE); Lw(T1, S1, 0x10); Addu(T1, T1, T0); Sw(T1, S1, 0x10);
            a.L(notAir);
            Lbu(T0, S1, 0x42); a.I(12, T0, T0, 2); Beq(T0, Zero, tag + "_loop");
            SetState(EnemyIdle); Animate(2);
            a.R(0, S1, T0, 3, 0x02); a.I(12, T0, T0, 15); a.I(9, T0, T0, 12); Sb(T0, S3, 0xF);
            Jmp(tag + "_loop");
        }

        // ---- Knight: guards, turns to face Spyro, marches up to him (within its leash) and swings its sword.
        a.L(tag + "_knight");
        Lbu(T0, S3, 0xC);
        Li(T1, EnemyAttack); Beq(T0, T1, tag + "_knight_swing");
        Li(T1, EnemyMove); Beq(T0, T1, tag + "_knight_march");
        // Guard.
        Animate(0);
        Lbu(T0, S3, 0xF); string cooled = L("cooled"); Beq(T0, Zero, cooled); a.I(9, T0, T0, -1); Sb(T0, S3, 0xF); a.L(cooled);
        {
            string unseen = L("unseen");
            Beq(S2, Zero, unseen);
            Move(A0, S5); Move(A1, S6); Call(tag + "_atan"); TurnTowards(10);
            Tuned(T0, FAttack); Slt(T1, Fp, T0); string far = L("far"); Beq(T1, Zero, far);
            Lbu(T0, S3, 0xF); Bne(T0, Zero, tag + "_loop");
            FacingError(); a.I(10, T0, T1, 20); Beq(T1, Zero, tag + "_loop");
            SetState(EnemyAttack); Animate(2); Jmp(tag + "_loop");
            a.L(far);
            ToHome(); Tuned(T0, FLeash); Slt(T1, A2, T0); Beq(T1, Zero, tag + "_loop");
            SetState(EnemyMove); Jmp(tag + "_loop");
            a.L(unseen);
            ToHome(); a.I(10, A2, T1, 300); Bne(T1, Zero, tag + "_loop");
            SetState(EnemyMove); Jmp(tag + "_loop");
        }
        a.L(tag + "_knight_march");
        Animate(1);
        {
            string home = L("home"), go = L("go");
            Beq(S2, Zero, home);
            ToHome(); Tuned(T0, FLeash); Slt(T1, A2, T0); Beq(T1, Zero, home);
            Tuned(T0, FAttackStop); Slt(T1, Fp, T0); string stop = L("stop"); Bne(T1, Zero, stop);
            Move(A0, S5); Move(A1, S6); Move(A2, Fp); Jmp(go);
            a.L(home);
            ToHome(); a.I(10, A2, T1, 200); Bne(T1, Zero, stop);
            a.L(go);
            Sw(A0, Sp, 0x30); Sw(A1, Sp, 0x34); Sw(A2, Sp, 0x38);
            Call(tag + "_atan"); TurnTowards(12);
            Lw(A0, Sp, 0x30); Lw(A1, Sp, 0x34); Lw(A2, Sp, 0x38);
            Tuned(T3, FSpeed); StepTowards(T3);
            Jmp(tag + "_loop");
            a.L(stop);
            SetState(EnemyIdle); Jmp(tag + "_loop");
        }
        a.L(tag + "_knight_swing");
        Animate(2);
        {
            string end = L("end");
            Lbu(T0, S1, 0x3C); Li(T1, 2); Bne(T0, T1, tag + "_loop");
            // The blade comes down on frames 6..9: Spyro within its reach in front of the knight is hit.
            Lbu(T0, S1, 0x3E); a.I(11, T0, T1, 6); Bne(T1, Zero, end); a.I(11, T0, T1, 10); Beq(T1, Zero, end);
            Tuned(T0, FReach); Slt(T1, Fp, T0); Beq(T1, Zero, end);
            Move(T0, S7); Abs(T0); a.I(10, T0, T1, 1600); Beq(T1, Zero, end);
            Move(A0, S5); Move(A1, S6); Call(tag + "_atan"); FacingError(); a.I(10, T0, T1, 40); Beq(T1, Zero, end);
            HurtSpyro();
            a.L(end);
            Lbu(T0, S1, 0x42); a.I(12, T0, T0, 2); Beq(T0, Zero, tag + "_loop");
            SetState(EnemyIdle); Li(T0, 45); Sb(T0, S3, 0xF); Animate(0);
            Jmp(tag + "_loop");
        }

        // ---- Wizard: stands his ground, turns to face Spyro and casts: the zap lands when the staff comes down if
        // Spyro is still close and in front of him (one hit per spell, then a pause).
        a.L(tag + "_wizard");
        Lbu(T0, S3, 0xC); Li(T1, EnemyAttack); Beq(T0, T1, tag + "_wizard_cast");
        Animate(0);
        Lbu(T0, S3, 0xF); string wcool = L("wcool"); Beq(T0, Zero, wcool); a.I(9, T0, T0, -1); Sb(T0, S3, 0xF); a.L(wcool);
        Beq(S2, Zero, tag + "_loop");
        Move(A0, S5); Move(A1, S6); Call(tag + "_atan"); TurnTowards(8);
        Tuned(T0, FAttack); Slt(T1, Fp, T0); Beq(T1, Zero, tag + "_loop");
        Lbu(T0, S3, 0xF); Bne(T0, Zero, tag + "_loop");
        FacingError(); a.I(10, T0, T1, 24); Beq(T1, Zero, tag + "_loop");
        SetState(EnemyAttack); Sb(Zero, S3, 0xD); Animate(2); Jmp(tag + "_loop");
        a.L(tag + "_wizard_cast");
        Animate(2);
        {
            string end = L("end");
            Lbu(T0, S1, 0x3C); Li(T1, 2); Bne(T0, T1, tag + "_loop");
            Lbu(T0, S3, 0xD); Bne(T0, Zero, end);
            Lbu(T0, S1, 0x3E); a.I(11, T0, T1, 12); Bne(T1, Zero, end);
            Li(T0, 1); Sb(T0, S3, 0xD);
            Tuned(T0, FReach); Slt(T1, Fp, T0); Beq(T1, Zero, end);
            Move(T0, S7); Abs(T0); a.I(10, T0, T1, 1800); Beq(T1, Zero, end);
            Move(A0, S5); Move(A1, S6); Call(tag + "_atan"); FacingError(); a.I(10, T0, T1, 48); Beq(T1, Zero, end);
            Particle(14, 70, 0x20, atSpyro: true);
            HurtSpyro();
            a.L(end);
            Lbu(T0, S1, 0x42); a.I(12, T0, T0, 2); Beq(T0, Zero, tag + "_loop");
            SetState(EnemyIdle); Li(T0, 50); Sb(T0, S3, 0xF); Animate(0);
            Jmp(tag + "_loop");
        }
        // Robe blown off: once the companion model's first animation has played, he goes flying.
        a.L(tag + "_robe");
        {
            Lbu(T0, S1, 0x3C); Bne(T0, Zero, tag + "_robe_go");
            Lbu(T0, S1, 0x42); a.I(12, T0, T0, 2); Beq(T0, Zero, tag + "_loop");
            a.L(tag + "_robe_go");
            Animate(1);
            a.La(T0, SpyroPosition);
            Lw(S5, T0, 0); Lw(T1, S1, 0xC); Subu(S5, S5, T1);
            Lw(S6, T0, 4); Lw(T1, S1, 0x10); Subu(S6, S6, T1);
            Move(A0, S5); Call(tag + "_len_xy"); Move(Fp, V0);
            Jmp(tag + "_launch");
        }

        // ---- Knocked flying (Crash's spin): sparks and its gem, then (a wizard first loses his robe) the whack
        // sound as it tumbles away from Spyro.
        a.L(tag + "_knocked");
        Particle(10, 70, 0x20);
        Particle(3, 2, 0);
        Move(A0, S1); Li(A1, 3); Li(A2, 0); a.Jal(DropReward); Li(A3, 0);
        a.Jal(KeepDestroyed); Move(A0, S1);
        if (tunings.Any(t => t.Kind == CrashEnemyWizard))
        {
            string notWizard = L("nw");
            Li(T0, UniversalCrashEnemies.Wizard.Class); Bne(S4, T0, notWizard);
            Li(T0, UniversalCrashEnemies.Wizard.Companion!.Class); a.I(41, S1, T0, 0x36);
            Sw(Zero, S1, 0x3C); Sb(Zero, S1, 0x40); Li(T0, 32); Sb(T0, S1, 0x41); Sb(Zero, S1, 0x42);
            SetState(EnemyRobeOff); Jmp(tag + "_loop");
            a.L(notWizard);
        }
        a.L(tag + "_launch");
        Sound(UniversalCrashEnemies.FlySlot);
        Subu(A0, Zero, S5); Subu(A1, Zero, S6); Move(A2, Fp);
        {
            string aimed = L("aimed");
            Bgtz(A2, aimed); Li(A0, 1); Li(A1, 0); Li(A2, 1);
            a.L(aimed);
        }
        Li(T3, FlightSpeed);
        Mult(T0, A0, T3); Div(T0, T0, A2); Sw(T0, S3, 0);
        Mult(T1, A1, T3); Div(T1, T1, A2); Sw(T1, S3, 4);
        Li(T0, FlightRise); Sw(T0, S3, 8);
        SetState(EnemyFlying); Sb(Zero, S3, 0xF);
        if (tunings.Any(t => t.Kind == CrashEnemyFrog))
        {
            string notFrog = L("nf"); Tuned(T0, RowKind); Li(T1, CrashEnemyFrog); Bne(T0, T1, notFrog); Animate(1); a.L(notFrog);
        }
        Jmp(tag + "_loop");
        a.L(tag + "_flying");
        Lw(T0, S3, 0); Lw(T1, S1, 0xC); Addu(T1, T1, T0); Sw(T1, S1, 0xC);
        Lw(T0, S3, 4); Lw(T1, S1, 0x10); Addu(T1, T1, T0); Sw(T1, S1, 0x10);
        Lw(T0, S3, 8); Lw(T1, S1, 0x14); Addu(T1, T1, T0); Sw(T1, S1, 0x14);
        a.I(9, T0, T0, -FlightFall); Sw(T0, S3, 8);
        Lbu(T0, S1, 0x46); a.I(9, T0, T0, 23); Sb(T0, S1, 0x46);
        Lbu(T0, S1, 0x44); a.I(9, T0, T0, 13); Sb(T0, S1, 0x44);
        Lbu(T0, S3, 0xF); a.I(9, T0, T0, 1); Sb(T0, S3, 0xF);
        a.I(11, T0, T0, FlightTicks); Bne(T0, Zero, tag + "_loop");
        a.Jal(FreeMoby); Move(A0, S1);
        Jmp(tag + "_loop");

        a.L(tag + "_done");
        Lw(Ra, Sp, 0x50); Lw(Fp, Sp, 0x4C);
        Lw(S0, Sp, 0x10); Lw(S1, Sp, 0x14); Lw(S2, Sp, 0x18); Lw(S3, Sp, 0x1C); Lw(S4, Sp, 0x20); Lw(S5, Sp, 0x24); Lw(S6, Sp, 0x28); Lw(S7, Sp, 0x2C);
        a.Part.Emit(UniversalMips.JrRa); a.I(9, Sp, Sp, 0x58);

        // _len_xy: v0 = approximate length of (a0, s6) - the caller passes dx in a0 and dy in s6.
        a.L(tag + "_len_xy");
        Move(A1, S6);
        // _len: v0 = max(|a0|, |a1|) + 3/8 min (within 7% of the true length).
        a.L(tag + "_len");
        Move(T0, A0); Abs(T0); Move(T1, A1); Abs(T1);
        Slt(T2, T0, T1);
        {
            string ordered = L("ord"); Beq(T2, Zero, ordered); Move(T2, T0); Move(T0, T1); Move(T1, T2); a.L(ordered);
        }
        Sll(T2, T1, 1); Addu(T2, T2, T1); Sra(T2, T2, 3); Addu(V0, T0, T2);
        a.Part.Emit(UniversalMips.JrRa); a.Nop();

        // _atan: v0 = world angle of (a0, a1), 256 per turn from +x towards +y (within 1.5 steps).
        a.L(tag + "_atan");
        {
            string steep = L("steep"), octant = L("oct"), xpos = L("xpos"), ypos = L("ypos"), zero = L("zero");
            Move(T0, A0); Abs(T0); Move(T1, A1); Abs(T1);
            Addu(T2, T0, T1); Beq(T2, Zero, zero);
            Slt(T2, T0, T1); Bne(T2, Zero, steep);
            Sll(T3, T1, 8); Div(T3, T3, T0); Li(T4, 0); Jmp(octant);
            a.L(steep);
            Sll(T3, T0, 8); Div(T3, T3, T1); Li(T4, 1);
            a.L(octant);
            // A(r) = (32 r 256 + 11 r (256 - r)) >> 16 for r = 256 tan in 0..256.
            Li(T2, 256); Subu(T2, T2, T3); Mult(T2, T2, T3); Li(T5, 11); Mult(T2, T2, T5);
            Sll(T5, T3, 13); Addu(T2, T2, T5); Sra(V0, T2, 16);
            string flat = L("flat"); Beq(T4, Zero, flat); Li(T2, 64); Subu(V0, T2, V0); a.L(flat);
            Bgez(A0, xpos); Li(T2, 128); Subu(V0, T2, V0); a.L(xpos);
            Bgez(A1, ypos); Subu(V0, Zero, V0); a.L(ypos);
            a.I(12, V0, V0, 255);
            a.Part.Emit(UniversalMips.JrRa); a.Nop();
            a.L(zero);
            Li(V0, 0);
            a.Part.Emit(UniversalMips.JrRa); a.Nop();
        }
        a.L(tag + "_table");
        foreach (var (c, kind, t) in tunings)
            foreach (int word in new[] { c, kind, t.Touch, t.Top + 100, t.Top - 100, t.Stand + 200, t.Sight, t.Leash, t.Speed, t.Stomp, t.Attack, t.Attack - 200, t.Reach })
                a.Part.Emit((uint)word);
        a.Part.Emit(0);
        return a.Part;
    }
}
