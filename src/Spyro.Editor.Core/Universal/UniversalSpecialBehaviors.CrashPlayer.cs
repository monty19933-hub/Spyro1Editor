namespace Spyro.Editor.Core.Universal;

public static partial class UniversalSpecialBehaviors
{
    // Playable Crash (see UniversalCrashPlayer). The level update runs before the player update each frame
    // (0x80033AA4, then 0x8004A200 at 0x80033AD8), so the controller sees this frame's buttons first: it takes
    // Square, Circle and (in the air) X away from Spyro and does Crash's moves with them. Engine facts:
    // - 0x80075814 non-zero skips Spyro's drawing in the gameplay renderer (0x800196E8); level loads clear it.
    // - Buttons: pressed this frame 0x80077378, held 0x80077380 (Square 0x80, Cross 0x40, Circle 0x20, d-pad 0xF000).
    // - Spyro: movement state 0x80078AD0 (0 standing, 1/21 starting to run, 2 running, 3 stopping, 5 jumping, 6 falling
    //   without a jump, 15 gliding), vertical speed 0x80078B48 (+0xC, +0x24 mirror it; it keeps its last value after
    //   landing), ground speed 0x80078B70, facing 0x80078A66 (256 per turn, the world direction he moves in, the same
    //   sense as an object's yaw); his position is about 352 units above the ground he stands on and is updated after the level update.
    // - Moby +8 is its collision block (0 for objects that cannot be hit; u16 +0 its top, u16 +6 its radius); +0x18
    //   collects hits: flame 0x10000, charge 0x20000 (only cleared at spawn). Spyro's own enemies react to Crash's
    //   hits as to a charge (Dark Hollow's 0x81 and 0x19C, emulator).
    private const uint HideSpyro = 0x80075814, PressedButtons = 0x80077378, MobyArray = 0x80075828, SpyroFacing = 0x80078A66,
        GroundSpeed = 0x80078B70, GameState = 0x800757D8;
    private const int ButtonSquare = 0x80, ButtonCross = 0x40, ButtonCircle = 0x20, DPad = 0xF000;
    private static readonly int[] AirStates = [5, 6, 15, 23];
    public const int CrashPlayerFeet = 352;
    public const int CrashSpinTicks = 14, CrashSpinCooldown = 6, CrashSpinReach = 900;
    public const int CrashDoubleJumpSpeed = 5000, CrashHighJumpSpeed = 4600, CrashSlamSpeed = -5600, CrashSlamReach = 1100,
        CrashGetUpTicks = 16;
    public const int CrashSlideTicks = 12, CrashSlideMinSpeed = 2500, CrashSlideSpeed = 5200, CrashSlideReach = 700;
    public const int CrashBounceReach = 300, CrashBounceAbove = 300, CrashIntroTicks = 14;
    public const int CrashRunSpeed = 1200, PlaceStepLimit = 1000;
    /// <summary>Crash's hits: the spin, slide and bounce hit like Spyro's charge; the belly flop like his flame, so
    /// enemies only flame can beat still fall to it.</summary>
    public const int CrashHitFlags = 0x20000, CrashSlamFlags = 0x10000;
    private const int RescueGameState = 8, GlideMoveState = 15, HurtState = 14, ChargeMoveState = 11, DeathGameState = 4;
    /// <summary>Spyro losing a life: sinking in water or lava (29), struck down (30, 31).</summary>
    private static readonly int[] DyingStates = [29, 30, 31];
    /// <summary>Package sound slots of the body (see UniversalCrashPlayer.Sounds).</summary>
    public const int CrashSoundSpin = 0, CrashSoundBounce = 1, CrashSoundSlide = 2, CrashSoundLand = 3, CrashSoundJump = 4;
    // Body property bytes: the controller's state. Legs property words: Spyro's position last frame.
    private const int PSpin = 0, PDoubleJump = 1, PSlam = 2, PGetUp = 3, PCooldown = 4, PSlide = 5, PCrouch = 6, PIntro = 7,
        PIntroTicks = 8, PHighJump = 9, PDash = 10, PXReleased = 11, PSpinAngle = 12;
    /// <summary>Crash 3 turns Crash in code while he spins (its spin animation keeps him facing one way): his objects
    /// turn this much per tick (256 a turn), about two turns over a spin.</summary>
    public const int CrashSpinTurn = 36;
    /// <summary>Each animation's own speed, as its package header stores it (CrashSpyroModel.Package).</summary>
    private static readonly int[] AnimationSpeeds = [.. UniversalCrashPlayer.Animations.Select(a => (a.Interpolated ? 32 : 64) / a.Step)];

    /// <summary>Per-frame controller for the playable Crash's two objects (body and legs).</summary>
    internal static UniversalAsmPart CrashPlayerUpdate(IReadOnlyList<int> classes, string tag)
    {
        if (classes.Count != 2 || !classes.Contains(UniversalCrashPlayer.BodyClass) || !classes.Contains(UniversalCrashPlayer.LegsClass))
            throw new InvalidDataException("Playable Crash needs both his body and his legs.");
        const int T7 = 15, S2 = 18, S3 = 19, S4 = 20, S5 = 21, S6 = 22, S7 = 23, Fp = 30;
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
        void And(int rd, int rs, int rt) => a.R(rs, rt, rd, 0, 0x24);
        void Or(int rd, int rs, int rt) => a.R(rs, rt, rd, 0, 0x25);
        void Lw(int rt, int b, int off) { a.I(35, b, rt, off); a.Nop(); }
        void Lbu(int rt, int b, int off) { a.I(36, b, rt, off); a.Nop(); }
        void Lhu(int rt, int b, int off) { a.I(37, b, rt, off); a.Nop(); }
        void Sw(int rt, int b, int off) => a.I(43, b, rt, off);
        void Sb(int rt, int b, int off) => a.I(40, b, rt, off);
        void Beq(int rs, int rt, string l) { a.Br(4, rs, rt, l); a.Nop(); }
        void Bne(int rs, int rt, string l) { a.Br(5, rs, rt, l); a.Nop(); }
        void Blez(int r, string l) { a.Br(6, r, 0, l); a.Nop(); }
        void Bgtz(int r, string l) { a.Br(7, r, 0, l); a.Nop(); }
        void Bgez(int r, string l) { a.Br(1, r, 1, l); a.Nop(); }
        void Jmp(string l) { a.J(l); a.Nop(); }
        void Call(string l) { a.Part.Emit(UniversalMips.EncodeJ(3, 0), new(UniversalFixKind.JalLabel, Label: l)); a.Nop(); }
        void Abs(int reg) { string done = L("abs"); Bgez(reg, done); Subu(reg, Zero, reg); a.L(done); }
        void SetVertical(int speed)
        {
            a.La(T0, VerticalSpeed); Li(T1, speed);
            Sw(T1, T0, 0); Sw(T1, T0, 0xC); Sw(T1, T0, 0x24); Move(S6, T1);
        }
        void Hit(int reach, int flags = CrashHitFlags) { Li(A0, reach); Li(A1, flags); Call(tag + "_hit"); }
        void Sound(int slot)
        {
            Li(V0, 0x20); Sb(V0, S0, 0x55);
            Move(A0, S0); Li(A1, slot); a.Jal(PlayModelSound); Li(A2, 0);
        }
        void Particle(int count, int type, int extra)
        {
            a.La(V0, ParticleSpawnGlobal); a.I(35, V0, V0, 0);
            Li(A0, count); Li(A1, type); a.La(A2, SpyroPosition);
            a.R(V0, 0, Ra, 0, 0x09); Li(A3, extra);
        }

        a.L(tag);
        a.I(9, Sp, Sp, -0x38); Sw(Ra, Sp, 0x34);
        Sw(S0, Sp, 0x10); Sw(S1, Sp, 0x14); Sw(S2, Sp, 0x18); Sw(S3, Sp, 0x1C); Sw(S4, Sp, 0x20); Sw(S5, Sp, 0x24); Sw(S6, Sp, 0x28);
        Sw(S7, Sp, 0x2C); Sw(Fp, Sp, 0x30);
        // Find the body (s0) and the legs (s1) among the level's objects.
        Move(S0, Zero); Move(S1, Zero);
        a.La(T0, MobyArray); Lw(T0, T0, 0);
        a.L(tag + "_find");
        Lbu(T1, T0, 0x48); Li(T2, 0xFF); Beq(T1, T2, tag + "_found");
        Lhu(T1, T0, 0x36);
        {
            string notBody = L("nb"), notLegs = L("nl");
            Li(T2, UniversalCrashPlayer.BodyClass); Bne(T1, T2, notBody); Move(S0, T0); a.L(notBody);
            Li(T2, UniversalCrashPlayer.LegsClass); Bne(T1, T2, notLegs); Move(S1, T0); a.L(notLegs);
        }
        a.I(9, T0, T0, UniversalLevelData.RowStride); Jmp(tag + "_find");
        a.L(tag + "_found");
        Beq(S0, Zero, tag + "_done"); Beq(S1, Zero, tag + "_done");
        Lw(S2, S0, 0); Beq(S2, Zero, tag + "_done");
        Lw(Fp, S1, 0); Beq(Fp, Zero, tag + "_done");
        a.La(T0, GameState); Lw(T0, T0, 0);
        {
            // While Spyro dies (4) or a dragon is rescued (8) Crash stands in for him: flattened, or standing. (The level
            // update only runs in a rescue's first phase; later phases draw Crash from the exe hook, UniversalCrashPlayer.)
            string play = L("play"), notDeath = L("nd");
            Beq(T0, Zero, play);
            Lbu(T1, S2, PIntro); Beq(T1, Zero, tag + "_done");
            Li(T1, DeathGameState); Bne(T0, T1, notDeath);
            Sb(Zero, S2, PSpinAngle); Call(tag + "_place"); Li(A0, UniversalCrashPlayer.Death); Jmp(tag + "_pose");
            a.L(notDeath);
            Li(T1, RescueGameState); Bne(T0, T1, tag + "_done");
            Sb(Zero, S2, PSpinAngle); Call(tag + "_place"); Li(A0, UniversalCrashPlayer.Idle); Jmp(tag + "_pose");
            a.L(play);
        }

        a.La(T0, PressedButtons); Lw(S3, T0, 0); Lw(S4, T0, 8);
        a.La(T0, MovementState); Lw(S5, T0, 0);
        a.La(T0, VerticalSpeed); Lw(S6, T0, 0);
        // s7 = in the air (the vertical speed keeps its last value after landing, so it says nothing about the ground).
        Move(S7, Zero);
        foreach (int air in AirStates)
        {
            string n = L("a"); Li(T0, air); Bne(S5, T0, n); Li(S7, 1); a.L(n);
        }

        // Arrival: Spyro comes into the level as himself; once he stands on the ground a puff of stars turns him into
        // Crash, who appears mid-spin. Until then Crash waits out of sight below the level.
        {
            string ready = L("ready");
            Lbu(T0, S2, PIntro); Bne(T0, Zero, ready);
            Bne(S7, Zero, tag + "_away");
            Li(T0, 1); Sb(T0, S2, PIntro); Li(T0, CrashIntroTicks); Sb(T0, S2, PIntroTicks);
            Particle(24, 70, 0x20);
            Sound(CrashSoundSpin);
            a.L(ready);
        }
        a.La(T0, HideSpyro); Li(T1, 1); Sw(T1, T0, 0);

        // On the ground: the double jump is available again; a belly flop lands with a shock wave.
        {
            string air = L("air"), noLand = L("noland");
            Bne(S7, Zero, air);
            Sb(Zero, S2, PDoubleJump); Sb(Zero, S2, PXReleased);
            Lbu(T0, S2, PSlam); Li(T1, 1); Bne(T0, T1, noLand);
            Li(T1, 2); Sb(T1, S2, PSlam); Li(T1, CrashGetUpTicks); Sb(T1, S2, PGetUp);
            Hit(CrashSlamReach, CrashSlamFlags);
            Particle(12, 2, 0);
            Sound(CrashSoundLand);
            a.L(noLand);
            a.L(air);
        }
        // Getting up after a belly flop (Crash stays put meanwhile).
        {
            string no = L("getup");
            Lbu(T0, S2, PSlam); Li(T1, 2); Bne(T0, T1, no);
            Li(T0, ~DPad); And(S3, S3, T0); And(S4, S4, T0);
            Lbu(T0, S2, PGetUp); a.I(9, T0, T0, -1); Sb(T0, S2, PGetUp);
            Bgtz(T0, no); Sb(Zero, S2, PSlam);
            a.L(no);
        }
        // Square: the spin.
        {
            string noStart = L("ns"), noSpin = L("nospin"), cooled = L("cool");
            a.I(12, S3, T0, ButtonSquare); Beq(T0, Zero, noStart);
            Lbu(T1, S2, PSpin); Bne(T1, Zero, noStart);
            Lbu(T1, S2, PCooldown); Bne(T1, Zero, noStart);
            Lbu(T1, S2, PSlam); Bne(T1, Zero, noStart);
            Li(T1, CrashSpinTicks); Sb(T1, S2, PSpin);
            Sound(CrashSoundSpin);
            a.L(noStart);
            Lbu(T1, S2, PSpin); Beq(T1, Zero, noSpin);
            a.I(9, T1, T1, -1); Sb(T1, S2, PSpin);
            string still = L("still"); Bne(T1, Zero, still); Li(T2, CrashSpinCooldown); Sb(T2, S2, PCooldown); a.L(still);
            Hit(CrashSpinReach);
            Jmp(cooled);
            a.L(noSpin);
            Lbu(T1, S2, PCooldown); Beq(T1, Zero, cooled); a.I(9, T1, T1, -1); Sb(T1, S2, PCooldown);
            a.L(cooled);
        }
        // Circle: a belly flop in the air, a slide when running, a crouch when standing (held).
        {
            string notPressed = L("np"), ground = L("gr"), crouch = L("crouch"), done = L("circle");
            Sb(Zero, S2, PCrouch);
            a.I(12, S3, T0, ButtonCircle); Beq(T0, Zero, notPressed);
            Beq(S7, Zero, ground);
            Lbu(T1, S2, PSlam); Bne(T1, Zero, done);
            Li(T1, 1); Sb(T1, S2, PSlam); Sb(Zero, S2, PSpin);
            SetVertical(CrashSlamSpeed);
            Jmp(done);
            a.L(ground);
            Lbu(T1, S2, PSlam); Bne(T1, Zero, done);
            a.La(T0, GroundSpeed); Lw(T0, T0, 0); Li(T1, CrashSlideMinSpeed); Slt(T1, T0, T1); Bne(T1, Zero, crouch);
            Lbu(T1, S2, PSlide); Bne(T1, Zero, done);
            Li(T1, CrashSlideTicks); Sb(T1, S2, PSlide);
            Sound(CrashSoundSlide);
            Jmp(done);
            a.L(notPressed);
            // Held while standing still: crouch (no walking; X from here is the high jump).
            Bne(S7, Zero, done);
            a.I(12, S4, T0, ButtonCircle); Beq(T0, Zero, done);
            Lbu(T1, S2, PSlide); Bne(T1, Zero, done);
            a.L(crouch);
            Li(T1, 1); Sb(T1, S2, PCrouch);
            Li(T0, ~DPad); And(S3, S3, T0); And(S4, S4, T0);
            a.I(12, S3, T0, ButtonCross); Beq(T0, Zero, done);
            Li(T1, 1); Sb(T1, S2, PHighJump);
            a.L(done);
        }
        // The slide: full speed ahead for a moment, knocking over what it runs into.
        {
            string no = L("slide");
            Lbu(T1, S2, PSlide); Beq(T1, Zero, no);
            a.I(9, T1, T1, -1); Sb(T1, S2, PSlide);
            a.La(T0, GroundSpeed); Li(T1, CrashSlideSpeed); Sw(T1, T0, 0);
            Hit(CrashSlideReach);
            a.L(no);
        }
        // The high jump takes off with more speed than a jump (once Spyro's own jump has started).
        {
            string no = L("hj");
            Lbu(T1, S2, PHighJump); Beq(T1, Zero, no);
            Beq(S7, Zero, no);
            Sb(Zero, S2, PHighJump);
            SetVertical(CrashHighJumpSpeed);
            a.L(no);
        }
        // X in the air: the double jump (once per jump), on a new press once the jump's own X has been let go (the
        // press that started the jump still reads as pressed for its first frame in the air).
        {
            string no = L("dj"), held = L("held");
            Beq(S7, Zero, no);
            a.I(12, S4, T0, ButtonCross); Bne(T0, Zero, held);
            Li(T1, 1); Sb(T1, S2, PXReleased);
            a.L(held);
            a.I(12, S3, T0, ButtonCross); Beq(T0, Zero, no);
            Lbu(T1, S2, PXReleased); Beq(T1, Zero, no);
            Lbu(T1, S2, PDoubleJump); Bne(T1, Zero, no);
            Lbu(T1, S2, PSlam); Bne(T1, Zero, no);
            Li(T1, 1); Sb(T1, S2, PDoubleJump);
            a.La(T0, MovementState); Li(T1, JumpState); Sw(T1, T0, 0); Move(S5, T1);
            SetVertical(CrashDoubleJumpSpeed);
            Sound(CrashSoundJump);
            a.L(no);
        }
        // Falling onto something that can be hit: it takes the hit and Crash bounces off it.
        {
            string no = L("bounce");
            Beq(S7, Zero, no); Bgez(S6, no);
            Call(tag + "_stomp"); Beq(V0, Zero, no);
            a.La(T0, MovementState); Li(T1, JumpState); Sw(T1, T0, 0); Move(S5, T1);
            SetVertical(BounceSpeed);
            Sb(Zero, S2, PDoubleJump); Sb(Zero, S2, PSlam);
            Sound(CrashSoundBounce);
            a.L(no);
        }
        // Spyro never sees Circle (no flame), nor a new X press in the air (no glide of his own); X held keeps working,
        // so a held jump goes as high as Spyro's. Square is the spin; held on after a spin on the ground it is Crash's
        // dash (Spyro's charge, which also climbs supercharge ramps).
        {
            Move(T5, S3); Move(T6, S4);                                 // the buttons as pressed
            Li(T0, ~(ButtonSquare | ButtonCircle)); And(S3, S3, T0); And(S4, S4, T0);
            string ground = L("g"); Beq(S7, Zero, ground);
            Li(T0, ~ButtonCross); And(S3, S3, T0);
            a.L(ground);
            string noDash = L("nd"), dashing = L("dash"), written = L("w");
            a.I(12, T6, T0, ButtonSquare); Beq(T0, Zero, noDash);
            Bne(S7, Zero, written);
            Lbu(T0, S2, PSpin); Bne(T0, Zero, written);
            Lbu(T0, S2, PSlam); Bne(T0, Zero, written);
            Lbu(T0, S2, PDash); Bne(T0, Zero, dashing);
            Li(T0, 1); Sb(T0, S2, PDash); a.I(13, S3, S3, ButtonSquare);
            a.L(dashing);
            a.I(13, S4, S4, ButtonSquare);
            Jmp(written);
            a.L(noDash);
            Sb(Zero, S2, PDash);
            a.L(written);
            // The tornado spin: a Square press in the air starts Spyro's glide.
            string noTornado = L("nt");
            Beq(S7, Zero, noTornado);
            a.I(12, T5, T0, ButtonSquare); Beq(T0, Zero, noTornado);
            Li(T0, GlideMoveState); Beq(S5, T0, noTornado);
            a.I(13, S3, S3, ButtonCross);
            a.L(noTornado);
            a.La(T0, PressedButtons); Sw(S3, T0, 0); Sw(S4, T0, 8);
        }
        {
            string turning = L("turning"), set = L("setangle");
            Move(T1, Zero);
            Lbu(T0, S2, PSpin); Bne(T0, Zero, turning);
            Lbu(T0, S2, PIntroTicks); Bne(T0, Zero, turning);
            Li(T0, GlideMoveState); Bne(S5, T0, set);
            a.L(turning);
            Lbu(T1, S2, PSpinAngle); a.I(9, T1, T1, CrashSpinTurn);
            a.L(set);
            Sb(T1, S2, PSpinAngle);
        }
        Call(tag + "_place");
        // Animation.
        {
            string pick = L("pick");
            Lbu(T0, S2, PIntroTicks); string noIntro = L("ni"); Beq(T0, Zero, noIntro);
            a.I(9, T0, T0, -1); Sb(T0, S2, PIntroTicks);
            Li(A0, UniversalCrashPlayer.Spin); Jmp(pick);
            a.L(noIntro);
            foreach (int dying in DyingStates) { Li(A0, UniversalCrashPlayer.Death); Li(T0, dying); Beq(S5, T0, pick); }
            Li(A0, UniversalCrashPlayer.Hurt); Li(T0, HurtState); Beq(S5, T0, pick);
            Li(A0, UniversalCrashPlayer.Spin); Lbu(T0, S2, PSpin); Bne(T0, Zero, pick);
            Li(T0, GlideMoveState); Beq(S5, T0, pick);
            Lbu(T0, S2, PSlam);
            Li(A0, UniversalCrashPlayer.SlamDive); Li(T1, 1); Beq(T0, T1, pick);
            Li(A0, UniversalCrashPlayer.GetUp); Li(T1, 2); Beq(T0, T1, pick);
            Li(A0, UniversalCrashPlayer.Slide); Lbu(T0, S2, PSlide); Bne(T0, Zero, pick);
            string ground = L("ground"); Beq(S7, Zero, ground);
            Li(A0, UniversalCrashPlayer.JumpUp); Blez(S6, pick);
            Lbu(T0, S2, PDoubleJump); Beq(T0, Zero, pick);
            Li(A0, UniversalCrashPlayer.Flip); Jmp(pick);
            a.L(ground);
            Li(A0, UniversalCrashPlayer.Crouch); Lbu(T0, S2, PCrouch); Bne(T0, Zero, pick);
            Li(A0, UniversalCrashPlayer.Run); Li(T0, ChargeMoveState); Beq(S5, T0, pick);
            a.La(T0, GroundSpeed); Lw(T0, T0, 0); Li(T1, CrashRunSpeed); Slt(T1, T0, T1);
            Li(A0, UniversalCrashPlayer.Run); Beq(T1, Zero, pick);
            Li(A0, UniversalCrashPlayer.Idle);
            a.L(pick);
            a.L(tag + "_pose");
            // A new animation starts at once at its own speed (+0x3C/+0x3D animation, +0x3E/+0x3F key frames, +0x40
            // progress, +0x41 speed): blending into it waited for the current key frame, up to 16 ticks of the slow
            // idle, by which time a spin was over.
            {
                string known = L("speed");
                Li(T2, AnimationSpeeds[0]);
                for (int k = 1; k < AnimationSpeeds.Length; k++) { Li(T2, AnimationSpeeds[k]); Li(T1, k); Beq(A0, T1, known); }
                Li(T2, AnimationSpeeds[0]);
                a.L(known);
            }
            foreach (int r in new[] { S0, S1 })
            {
                string same = L("same");
                Lbu(T0, r, 0x3D); Beq(T0, A0, same);
                Sb(A0, r, 0x3C); Sb(A0, r, 0x3D); Sb(Zero, r, 0x3E); Li(T1, 1); Sb(T1, r, 0x3F); Sb(Zero, r, 0x40); Sb(T2, r, 0x41);
                a.L(same);
            }
            // Flattened stays flattened: at the last frame (+0x42 bit 1) the speed goes to zero; the next animation
            // brings its own speed.
            {
                string notDeath = L("hold");
                Li(T1, UniversalCrashPlayer.Death); Bne(A0, T1, notDeath);
                Lbu(T0, S0, 0x3C); Bne(T0, A0, notDeath);
                Lbu(T0, S0, 0x42); a.I(12, T0, T0, 2); Beq(T0, Zero, notDeath);
                Sb(Zero, S0, 0x41); Sb(Zero, S1, 0x41);
                a.L(notDeath);
            }
        }
        Jmp(tag + "_done");

        // Before the arrival: Crash waits below the level, at Spyro's side.
        a.L(tag + "_away");
        a.La(T0, SpyroPosition);
        foreach (int axis in new[] { 0, 4, 8 })
        {
            Lw(T1, T0, axis); Sw(T1, Fp, axis);
            if (axis == 8) Li(T1, -0x400000);
            Sw(T1, S0, 0xC + axis); Sw(T1, S1, 0xC + axis);
        }

        a.L(tag + "_done");
        Lw(Ra, Sp, 0x34);
        Lw(S0, Sp, 0x10); Lw(S1, Sp, 0x14); Lw(S2, Sp, 0x18); Lw(S3, Sp, 0x1C); Lw(S4, Sp, 0x20); Lw(S5, Sp, 0x24); Lw(S6, Sp, 0x28);
        Lw(S7, Sp, 0x2C); Lw(Fp, Sp, 0x30);
        a.Part.Emit(UniversalMips.JrRa); a.I(9, Sp, Sp, 0x38);

        // _place: both objects stand where Spyro will be after his update this frame (his position plus last frame's
        // step; a jump of more than PlaceStepLimit on any axis, a respawn or a cutscene placing him, is not repeated)
        // and face his way. Spyro is hidden.
        a.L(tag + "_place");
        a.La(T0, HideSpyro); Li(T1, 1); Sw(T1, T0, 0);
        a.La(T0, SpyroPosition);
        foreach (int axis in new[] { 0, 4, 8 })
        {
            Lw(T1, T0, axis); Lw(T2, Fp, axis); Sw(T1, Fp, axis);
            Subu(T2, T1, T2);
            string near = L("near"); Move(T3, T2); Abs(T3); a.I(10, T3, T3, PlaceStepLimit); Bne(T3, Zero, near); Move(T2, Zero); a.L(near);
            Addu(T1, T1, T2);
            if (axis == 8) a.I(9, T1, T1, -CrashPlayerFeet);
            Sw(T1, S0, 0xC + axis); Sw(T1, S1, 0xC + axis);
        }
        a.La(T0, SpyroFacing); Lbu(T4, T0, 0);
        Lbu(T5, S2, PSpinAngle); Addu(T4, T4, T5);
        foreach (int r in new[] { S0, S1 }) { Sb(Zero, r, 0x44); Sb(Zero, r, 0x45); Sb(T4, r, 0x46); }
        a.Part.Emit(UniversalMips.JrRa); a.Nop();

        // _hit: every hittable object within a0 of Spyro (each axis) collects Crash's hit; Crash's own objects are skipped.
        a.L(tag + "_hit");
        a.La(T5, ActiveList);
        a.La(T6, SpyroPosition);
        a.L(tag + "_hit_loop");
        Lw(T7, T5, 0); Beq(T7, Zero, tag + "_hit_done");
        a.I(9, T5, T5, 4);
        Beq(T7, S0, tag + "_hit_loop"); Beq(T7, S1, tag + "_hit_loop");
        Lw(T0, T7, 8); a.R(0, T0, T0, 24, 0x02); Li(T1, 0x80); Bne(T0, T1, tag + "_hit_loop");
        Lbu(T0, T7, 0x48); a.I(11, T0, T0, 0x80); Beq(T0, Zero, tag + "_hit_loop");
        foreach (int axis in new[] { 0, 4, 8 })
        {
            Lw(T0, T6, axis); Lw(T1, T7, 0xC + axis); Subu(T0, T0, T1); Abs(T0);
            Slt(T1, T0, A0); Beq(T1, Zero, tag + "_hit_loop");
        }
        Lw(T0, T7, 0x18); Or(T0, T0, A1); Sw(T0, T7, 0x18);
        Jmp(tag + "_hit_loop");
        a.L(tag + "_hit_done");
        a.Part.Emit(UniversalMips.JrRa); a.Nop();

        // _stomp: v0 = 1 when Crash's feet are on top of a hittable object (within its radius and a little above its
        // top); that object collects the hit.
        a.L(tag + "_stomp");
        Move(V0, Zero);
        a.La(T5, ActiveList);
        a.La(T6, SpyroPosition);
        a.L(tag + "_stomp_loop");
        Lw(T7, T5, 0); Beq(T7, Zero, tag + "_stomp_done");
        a.I(9, T5, T5, 4);
        Beq(T7, S0, tag + "_stomp_loop"); Beq(T7, S1, tag + "_stomp_loop");
        Lw(T4, T7, 8); a.R(0, T4, T0, 24, 0x02); Li(T1, 0x80); Bne(T0, T1, tag + "_stomp_loop");
        Lbu(T0, T7, 0x48); a.I(11, T0, T0, 0x80); Beq(T0, Zero, tag + "_stomp_loop");
        // Crash boxes see a landing for themselves (a TNT starts its countdown, a crate breaks, the switch is pressed)
        // and bounce Crash; a stomp would count as a charge and set a TNT off at once.
        Lhu(T0, T7, 0x36);
        foreach (int c in UniversalCrashBoxes.AllClasses) { Li(T1, c); Beq(T0, T1, tag + "_stomp_loop"); }
        Lhu(T3, T4, 6); a.I(9, T3, T3, CrashBounceReach);              // reach: its radius and a little more
        foreach (int axis in new[] { 0, 4 })
        {
            Lw(T0, T6, axis); Lw(T1, T7, 0xC + axis); Subu(T0, T0, T1); Abs(T0);
            Slt(T1, T0, T3); Beq(T1, Zero, tag + "_stomp_loop");
        }
        // Feet height above its base: between half its top and a little above the top.
        Lw(T0, T6, 8); a.I(9, T0, T0, -CrashPlayerFeet); Lw(T1, T7, 0x14); Subu(T0, T0, T1);
        Lhu(T2, T4, 0); a.R(0, T2, T1, 1, 0x02);                       // srl t1, t2, 1
        Slt(T1, T0, T1); Bne(T1, Zero, tag + "_stomp_loop");
        a.I(9, T2, T2, CrashBounceAbove); Slt(T1, T2, T0); Bne(T1, Zero, tag + "_stomp_loop");
        Lw(T0, T7, 0x18); Li(T1, CrashHitFlags); Or(T0, T0, T1); Sw(T0, T7, 0x18);
        Li(V0, 1);
        a.L(tag + "_stomp_done");
        a.Part.Emit(UniversalMips.JrRa); a.Nop();
        return a.Part;
    }
}
