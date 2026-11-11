namespace Spyro.Editor.Core.Universal;

public static partial class UniversalSpecialBehaviors
{
    private const uint CollectItem = UniversalGemCollectionAdapter.CollectItem;
    private const uint PlacedRowsStart = 0x80075828, PlacedRowsEnd = 0x80075890;
    private const uint OrientMoby = 0x800529E4;       // (moby, 4): what the game's gem collection calls first
    private const uint SparxTarget = 0x80075758, SparxState = 0x80075830;
    /// <summary>Live objects the two scans walk at most: the placed rows end where the spawned ones begin, and those end
    /// at the allocator's terminator (state -1); the bound only guards a damaged list.</summary>
    public const int HighValueRewardScanLimit = 512;
    internal const string HighValueRewardPreLabel = "high_reward_pre", HighValueRewardUpdateLabel = "high_reward_upd",
        HighValueRewardCollectLabel = "high_reward_collect";

    /// <summary>Before the level's object update: every spawned gem shown as a high-value gem gets back the class of the
    /// original gem that stands in for it. A leaf that leaves the argument and saved registers alone.</summary>
    internal static UniversalAsmPart HighValueRewardPre(string tag = HighValueRewardPreLabel)
    {
        const int T9 = 25;
        var a = new Asm(); a.L(tag);
        a.I(15, Zero, T0, 0x8007); a.I(35, T0, T1, UniversalMips.Lo(PlacedRowsEnd));
        a.I(9, Zero, T9, HighValueRewardScanLimit);
        a.L(tag + "_loop");
        a.I(32, T1, T2, 0x48); a.I(9, Zero, T3, -1);
        a.Br(4, T2, T3, tag + "_done"); a.I(9, T9, T9, -1);
        a.Br(6, T9, Zero, tag + "_done"); a.Nop();
        a.Br(1, T2, 0, tag + "_next"); a.Nop();                  // bltz: a freed slot
        a.I(37, T1, T4, 0x36); a.I(9, Zero, T5, UniversalHighValueGems.TealClass);
        a.Br(4, T4, T5, tag + "_set"); a.I(9, Zero, T6, UniversalHighValueRewards.TealStandInClass);
        a.I(9, Zero, T5, UniversalHighValueGems.RainbowClass);
        a.Br(5, T4, T5, tag + "_next"); a.I(9, Zero, T6, UniversalHighValueRewards.RainbowStandInClass);
        a.L(tag + "_set");
        a.I(41, T1, T6, 0x36);
        a.L(tag + "_next");
        a.J(tag + "_loop"); a.I(9, T1, T1, 0x58);
        a.L(tag + "_done");
        a.Part.Emit(UniversalMips.JrRa); a.Nop();
        return a.Part;
    }

    /// <summary>After the level's object updates: tints the chests that hold a teal or rainbow gem, marks the gems
    /// such rows have just paid out and gives every marked gem the high-value gem's class and shine colour for
    /// drawing. A leaf on temporaries; its hue phase and tint words follow its code.</summary>
    internal static UniversalAsmPart HighValueRewardUpdate(IReadOnlyCollection<int> tintedClasses, string tag = HighValueRewardUpdateLabel)
    {
        const int T7 = 15, T8 = 24, T9 = 25;
        if (tintedClasses.Any(c => c is <= 0 or >= 0x200) || tintedClasses.Distinct().Count() != tintedClasses.Count)
            throw new InvalidDataException("Reward-tinted chest classes must be distinct object classes.");
        void LabelLoad(Asm a, int op, int reg, int baseReg, string label)
        {
            a.Part.Emit(UniversalMips.EncodeI(15, 0, baseReg, 0), new(UniversalFixKind.LabelAddrHi, Label: label));
            a.Part.Emit(UniversalMips.EncodeI(op, baseReg, reg, 0), new(UniversalFixKind.LabelAddrLo, Label: label));
        }
        var a = new Asm(); a.L(tag);
        // Hue phase, as the placed rainbow gem's wobble phase: two frame steps per update, six hues per turn.
        LabelLoad(a, 35, T7, T0, tag + "_phase");
        a.I(15, Zero, At, UniversalMips.Hi(UniversalHighValueGems.FrameStep));
        a.I(35, At, V0, UniversalMips.Lo(UniversalHighValueGems.FrameStep)); a.Nop();
        a.R(Zero, V0, V0, 1, 0); a.R(T7, V0, T7, 0, 0x21);
        a.I(43, T0, T7, 0); a.Part.Items[^1] = a.Part.Items[^1] with { Fix = new(UniversalFixKind.LabelAddrLo, Label: tag + "_phase") };
        a.I(12, T7, T7, 0xFF);
        a.R(Zero, T7, T2, 1, 0); a.R(Zero, T7, T3, 2, 0); a.R(T2, T3, T2, 0, 0x21);
        a.R(Zero, T2, T2, 8, 2); a.R(Zero, T2, T2, 2, 0);
        a.I(15, Zero, T3, (int)(UniversalHighValueGems.RainbowHueOrder >> 16)); a.I(13, T3, T3, (int)(UniversalHighValueGems.RainbowHueOrder & 0xFFFF));
        a.R(T2, T3, T3, 0, 6); a.I(12, T3, T8, 0xF);                         // t8 = the rainbow's shine entry now
        a.I(15, Zero, T0, 0x8007);
        if (tintedClasses.Count > 0)
        {
            a.I(35, T0, T1, UniversalMips.Lo(PlacedRowsStart)); a.I(35, T0, T9, UniversalMips.Lo(PlacedRowsEnd)); a.Nop();
            a.L(tag + "_tint");
            a.R(T1, T9, V0, 0, 0x2B); a.Br(4, V0, Zero, tag + "_tinted"); a.Nop();
            a.I(36, T1, T2, 0x53); a.I(32, T1, T3, 0x48);
            a.I(9, T2, T2, -UniversalHighValueRewards.TealReward);            // 0 teal, 1 rainbow
            a.Br(1, T3, 0, tag + "_tint_next"); a.I(11, T2, V0, 2);
            a.Br(4, V0, Zero, tag + "_tint_next"); a.Nop();
            a.I(37, T1, T4, 0x36); a.Nop();
            foreach (int c in tintedClasses.Order())
            {
                a.I(9, Zero, T5, c); a.Br(4, T4, T5, tag + "_tint_hit"); a.Nop();
            }
            a.J(tag + "_tint_next"); a.Nop();
            a.L(tag + "_tint_hit");
            a.Br(4, T2, Zero, tag + "_tint_teal"); a.R(Zero, Zero, T6, 0, 0x21);
            a.R(Zero, T8, T6, 2, 0);
            a.L(tag + "_tint_teal");
            a.Part.Emit(UniversalMips.EncodeI(15, 0, T5, 0), new(UniversalFixKind.LabelAddrHi, Label: tag + "_tints"));
            a.Part.Emit(UniversalMips.EncodeI(9, T5, T5, 0), new(UniversalFixKind.LabelAddrLo, Label: tag + "_tints"));
            a.R(T5, T6, T5, 0, 0x21); a.I(35, T5, T5, 0); a.Nop();
            a.I(43, T1, T5, 0x4C);
            a.L(tag + "_tint_next");
            a.J(tag + "_tint"); a.I(9, T1, T1, 0x58);
            a.L(tag + "_tinted");
        }
        // Spawned objects: from the end of the placed rows to the allocator's terminator.
        a.I(35, T0, T1, UniversalMips.Lo(PlacedRowsEnd)); a.I(35, T0, T9, UniversalMips.Lo(PlacedRowsStart));
        a.I(9, Zero, T7, HighValueRewardScanLimit);
        a.L(tag + "_loop");
        a.I(32, T1, T2, 0x48); a.I(9, Zero, T3, -1);
        a.Br(4, T2, T3, tag + "_done"); a.I(9, T7, T7, -1);
        a.Br(6, T7, Zero, tag + "_done"); a.Nop();
        a.Br(1, T2, 0, tag + "_next"); a.Nop();
        a.I(37, T1, T4, 0x36); a.Nop();
        a.I(9, T4, T5, -UniversalHighValueRewards.TealStandInClass); a.I(11, T5, V0, 2);
        a.Br(4, V0, Zero, tag + "_next"); a.Nop();
        a.I(36, T1, T6, 0x53); a.I(13, T4, T5, 0x80);                        // t5 = the byte that marks this gem
        a.Br(4, T6, T5, tag + "_show"); a.I(9, Zero, V0, 0xFF);
        a.Br(5, T6, V0, tag + "_next"); a.Nop();
        // Not marked yet: the placed row that paid it (+0x56) must carry the byte and its paid bit, be alive, and hold
        // this very gem (a spring chest keeps the gem it made in its first property word).
        a.I(36, T1, V0, 0x56); a.Nop();
        a.R(Zero, V0, V1, 6, 0); a.R(Zero, V0, At, 4, 0); a.R(V1, At, V1, 0, 0x21);
        a.R(Zero, V0, At, 3, 0); a.R(V1, At, V1, 0, 0x21); a.R(V1, T9, V1, 0, 0x21);
        a.I(36, V1, At, 0x53); a.I(36, V1, V0, 0x3A);
        a.Br(5, At, T5, tag + "_next"); a.I(12, V0, V0, 0x80);
        a.Br(4, V0, Zero, tag + "_next"); a.Nop();
        a.I(32, V1, V0, 0x48); a.I(35, V1, At, 0);
        a.Br(1, V0, 0, tag + "_next"); a.Nop();
        a.I(35, At, At, 0); a.Nop();
        a.Br(5, At, T1, tag + "_next"); a.Nop();
        a.I(40, T1, T5, 0x53);
        a.L(tag + "_show");
        a.I(9, Zero, V0, UniversalHighValueRewards.TealStandInClass);
        a.Br(5, T4, V0, tag + "_rainbow"); a.Nop();
        a.I(9, Zero, V0, UniversalHighValueGems.TealClass); a.I(41, T1, V0, 0x36);
        a.J(tag + "_next"); a.I(40, T1, Zero, 0x4F);
        a.L(tag + "_rainbow");
        a.I(9, Zero, V0, UniversalHighValueGems.RainbowClass); a.I(41, T1, V0, 0x36);
        a.I(40, T1, T8, 0x4F);
        a.L(tag + "_next");
        a.J(tag + "_loop"); a.I(9, T1, T1, 0x58);
        a.L(tag + "_done");
        a.Part.Emit(UniversalMips.JrRa); a.Nop();
        a.L(tag + "_phase"); a.Part.Emit(0);
        a.L(tag + "_tints");
        foreach (uint tint in UniversalHighValueRewards.Tints) a.Part.Emit(tint);
        return a.Part;
    }

    /// <summary>Stands in for the game's gem collection routine (same argument: the gem). A spawned gem marked teal or
    /// rainbow is awarded 50 or 100 with the game's own award routine, after the same steps the game's routine takes
    /// (Sparx's fetch released, the gem turned, six collection stars); every other gem goes on to the game's routine.</summary>
    internal static UniversalAsmPart HighValueRewardCollect(string tag = HighValueRewardCollectLabel)
    {
        var a = new Asm(); a.L(tag);
        a.I(15, Zero, T2, 0x8007); a.I(35, T2, T2, UniversalMips.Lo(PlacedRowsEnd));
        a.I(36, A0, T0, 0x53);
        a.R(A0, T2, T3, 0, 0x2B); a.Br(5, T3, Zero, tag + "_native"); a.I(9, Zero, T1, UniversalHighValueRewards.TealReward);
        a.Br(4, T0, T1, tag + "_high"); a.I(9, Zero, V0, 50);
        a.I(9, Zero, T1, UniversalHighValueRewards.RainbowReward);
        a.Br(4, T0, T1, tag + "_high"); a.I(9, Zero, V0, 100);
        a.L(tag + "_native");
        a.Part.Emit(UniversalMips.EncodeJ(2, CollectItem)); a.Nop();
        a.L(tag + "_high");
        a.I(9, Sp, Sp, -0x20); a.I(43, Sp, Ra, 0x1C); a.I(43, Sp, S0, 0x14); a.I(43, Sp, S1, 0x18);
        a.R(A0, Zero, S0, 0, 0x21); a.R(V0, Zero, S1, 0, 0x21);
        a.I(15, Zero, T0, 0x8007); a.I(35, T0, V1, UniversalMips.Lo(SparxTarget)); a.Nop();
        a.Br(5, S0, V1, tag + "_turn"); a.Nop();
        a.I(9, Zero, V0, 3); a.I(43, T0, V0, UniversalMips.Lo(SparxState));
        a.L(tag + "_turn");
        a.R(S0, Zero, A0, 0, 0x21); a.Jal(OrientMoby); a.I(9, Zero, A1, 4);
        a.I(9, Zero, T0, 50);
        a.La(A3, UniversalHighValueGems.TealParticleColour); a.Br(4, S1, T0, tag + "_stars"); a.Nop();
        a.La(A3, UniversalHighValueGems.RainbowParticleColour);
        a.L(tag + "_stars");
        a.La(V0, ParticleSpawnGlobal); a.I(35, V0, V0, 0); a.I(9, Zero, A0, 6);
        a.I(9, Zero, A1, 12); a.R(V0, Zero, Ra, 0, 9); a.R(S0, Zero, A2, 0, 0x21);
        a.R(S1, Zero, A0, 0, 0x21); a.Jal(MarkCollected); a.R(S0, Zero, A1, 0, 0x21);
        a.I(35, Sp, Ra, 0x1C); a.I(35, Sp, S0, 0x14); a.I(35, Sp, S1, 0x18); a.Nop();
        a.Part.Emit(UniversalMips.JrRa); a.I(9, Sp, Sp, 0x20);
        return a.Part;
    }

    /// <summary>Sends every call to the game's gem collection routine in the level's added code through
    /// <see cref="HighValueRewardCollect"/> (copied object code and the adapters for renumbered gems alike).</summary>
    internal static int RedirectGemCollection(IEnumerable<UniversalAsmPart> parts, string label = HighValueRewardCollectLabel)
    {
        uint call = UniversalMips.EncodeJ(3, CollectItem);
        int redirected = 0;
        foreach (var part in parts)
            for (int i = 0; i < part.Items.Count; i++)
                if (part.Items[i].Word == call && part.Items[i].Fix.Kind == UniversalFixKind.None)
                {
                    part.Items[i] = part.Items[i] with { Fix = new(UniversalFixKind.JalLabel, Label: label) };
                    redirected++;
                }
        return redirected;
    }
}
