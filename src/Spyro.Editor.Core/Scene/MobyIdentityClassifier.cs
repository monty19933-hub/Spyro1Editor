using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.Scene;

public static class MobyIdentityClassifier
{
    public static int Apply(string levelKey, IList<Moby> mobys)
    {
        int applied = 0;
        foreach (Moby moby in mobys)
        {
            if (!TryClassify(levelKey, moby, out MobyIdentity identity))
                continue;

            bool replaceLabel = IsUserOverride(identity) ||
                IsDecompNativeClassIdentity(identity) ||
                HasWeakLabel(moby) ||
                HasReviewFamilyLabel(moby, identity) ||
                ShouldReplaceWithPassiveClassIdentity(moby, identity) ||
                (IsStrongIdentity(identity) && (HasPlaceholderControlLabel(moby) || HasCandidateLabel(moby) || HasValidationQualifierLabel(moby) || HasStaleConflictingLabel(moby, identity)));
            if (replaceLabel)
            {
                moby.Label = identity.Label;
                moby.OriginalLabel = identity.Label;
                if (IsStrongIdentity(identity) || IsPassiveClassIdentity(identity) || IsDecompNativeClassIdentity(identity))
                {
                    moby.CandidateKind = identity.Kind;
                    moby.Confidence = identity.Confidence;
                    moby.Evidence = identity.Evidence;
                }
                else
                {
                    moby.CandidateKind = FirstNonEmpty(moby.CandidateKind, identity.Kind);
                    moby.Confidence = FirstNonEmpty(moby.Confidence, identity.Confidence);
                    moby.Evidence = FirstNonEmpty(moby.Evidence, identity.Evidence);
                }
                applied++;
            }
            else
            {
                moby.CandidateKind = FirstNonEmpty(moby.CandidateKind, identity.Kind);
                moby.Confidence = FirstNonEmpty(moby.Confidence, identity.Confidence);
                moby.Evidence = FirstNonEmpty(moby.Evidence, identity.Evidence);
            }

            if (identity.Color != default && replaceLabel)
                moby.Color = identity.Color;
        }

        return applied;
    }

    public static bool TryClassify(string levelKey, Moby moby, out MobyIdentity identity)
    {
        if (TryClassifyGem(moby, out identity))
            return true;

        if (TryClassifyNativeBossContainedGem(levelKey, moby, out identity))
            return true;

        if (string.Equals(levelKey, "doctorshemp", StringComparison.OrdinalIgnoreCase) &&
            moby.TrueIndex is >= 72 and <= 75 &&
            ((moby.SourceByte37 << 8) | moby.SourceByte36) == 0x000D &&
            TryClassifyDecompNativeClass(levelKey, moby, out identity))
        {
            return true;
        }

        if (TryClassifyContainedGem(moby, out identity))
            return true;

        if (TryClassifyNativeArmoredChestLink(moby, out identity))
            return true;

        if (TryClassifyNativeDragonScene(moby, out identity))
            return true;

        if (TryClassifyDecompNativeClass(levelKey, moby, out identity))
            return true;

        if (TryClassifyFlightTarget(levelKey, moby, out identity))
            return true;

        if (TryClassifyEggThief(levelKey, moby, out identity))
            return true;

        if (string.Equals(levelKey, "doctorshemp", StringComparison.OrdinalIgnoreCase) &&
            TryClassifyDoctorShempTrueIndex(moby, out identity))
            return true;

        if (string.Equals(levelKey, "magiccrafters", StringComparison.OrdinalIgnoreCase) &&
            TryClassifyMagicCraftersTrueIndex(moby, out identity))
            return true;

        if (moby.Type == 0x20)
            return TryClassifyType20(levelKey, moby, out identity);

        if (moby.Type == 0x30)
            return TryClassifyType30(levelKey, moby, out identity);

        if (moby.Type == 0x00)
            return TryClassifyControl(levelKey, moby, out identity);

        if (moby.Type is 0x0A or 0x33 or 0x52)
        {
            identity = new MobyIdentity(
                "System/helper marker",
                "level helper, trigger, or engine control marker",
                "pattern-inferred",
                $"Type 0x{moby.Type:X2} is a recurring helper/system family, usually safer to treat as trigger or control data than as a visible placeable object.",
                ColorRgba.FromRgb(143, 166, 184));
            return true;
        }

        if (TryClassifyGenericSpecial(levelKey, moby, out identity))
            return true;

        identity = default;
        return false;
    }

    private static bool TryClassifyDecompNativeClass(string levelKey, Moby moby, out MobyIdentity identity)
    {
        int nativeClass = (moby.SourceByte37 << 8) | moby.SourceByte36;
        string key = levelKey.ToLowerInvariant();

        if (nativeClass == 0x011E && moby.Flag4A == 0x10 && moby.Flag4B == 0xFF)
        {
            identity = NativeClassObserved(
                "Ambient sound emitter",
                "positional ambient sound emitter / moving sound path",
                "The native 0x011E handler copied across all 35 level overlays sets the Moby's 3D sound distance, optionally advances its position along 16-byte path points, selects a sound-table entry, and calls PlaySound with this non-rendered Moby as the positional source.",
                ColorRgba.FromRgb(116, 185, 255));
            return true;
        }

        if (nativeClass == 0x0022 && moby.Type == 0x20 && moby.Flag4A == 0xFF && moby.Flag4B == 0xFF)
        {
            identity = NativeClassObserved(
                "Dragon Egg",
                "visible Egg Thief-carried dragon egg collectible actor",
                "Native class 0x0022 is the engine's Dragon Egg model. These loader-created rows sit immediately after their levels' source tables and beside active class-0x0021 Egg Thieves, whose drop/child class byte is also 0x22; they are the visible carried eggs, not flags, scenery, or a second thief.",
                ColorRgba.FromRgb(91, 184, 226));
            return true;
        }

        if (nativeClass == 0x0148 && key is "magiccrafters" or "alpineridge" or "highcaves" or "blowhard")
        {
            bool dormant = key == "alpineridge" && moby.TrueIndex is 135 or 136;
            identity = NativeClassObserved(
                dormant ? "Dormant Green Druid spell target" : "Green Druid spell target",
                dormant
                    ? "orphaned nonvisual green druid collision-triangle target control marker"
                    : "nonvisual green druid collision-triangle target control marker",
                dormant
                    ? "Native class 0x0148 acquires a floor collision triangle and snaps this marker to its centroid for Green Druid terrain spells. Alpine Ridge T135 and T136 have the same four-byte props and handler but no retail owner reference, making them dormant orphan targets."
                    : "Native class 0x0148 acquires a floor collision triangle and snaps this marker to its centroid. Green Druid props reference the row by true index as the destination for their terrain-moving spell; the class has no model and is nonvisual.",
                ColorRgba.FromRgb(116, 185, 255));
            return true;
        }

        if (nativeClass == 0x015F &&
            key is "toasty" or "doctorshemp" or "blowhard" &&
            GemValue.TryFromIdByte(moby.Flag4B, out GemValue phaseReward))
        {
            string owner = key switch
            {
                "toasty" => "Toasty",
                "doctorshemp" => "Doctor Shemp",
                _ => "Blowhard"
            };
            string chainEvidence = key switch
            {
                "toasty" => "Toasty's owner links the single Blue marker T53.",
                "doctorshemp" => "Doctor Shemp's owner links T71 (Blue) to T70 (Yellow) and then terminates the chain.",
                _ => "Blowhard's owner links the two Yellow markers T73 to T74 and then terminates the chain."
            };
            identity = NativeClassObserved(
                $"{owner} phase-reward {phaseReward.Name} marker",
                "nonvisual boss phase-reward gem spawn marker",
                $"Native class 0x015F is a linked phase-reward marker, not scenery or a visible gem. Its 16-byte payload stores the next true index and original XYZ; the boss handler advances that chain, copies the owner's current position into the marker, spawns the gem class selected by drop byte 0x{moby.Flag4B:X2} ({phaseReward.Name}, value {phaseReward.Value}), and removes the marker. {chainEvidence}",
                ColorRgba.FromRgb(143, 166, 184));
            return true;
        }

        string? returnHomeLetter = nativeClass switch
        {
            0x01AE => "E",
            0x01B1 => "H",
            0x01B6 => "M",
            0x01B7 => "N",
            0x01B8 => "O",
            0x01BB => "R",
            0x01BD => "T",
            0x01BE => "U",
            _ => null
        };
        if (key == "drycanyon" && returnHomeLetter is not null)
        {
            identity = NativeClassObserved(
                $"Return Home letter {returnHomeLetter}",
                "visible 3D Return Home portal-text glyph",
                $"Dry Canyon native class 0x{nativeClass:X4} is the shared 3D letter-{returnHomeLetter} model. The ten runtime-spawned rows spell RETURN HOME above their common portal parent: native letter classes are contiguous from 0x01AA=A, their props preserve the source string indices while skipping its space, and all use the visible metal-shaded glyph family.",
                ColorRgba.FromRgb(186, 196, 207));
            return true;
        }

        switch (key, nativeClass)
        {
            case ("toasty", 0x013A) when moby.TrueIndex == 11:
                identity = NativeClassObserved(
                    "Toasty disguise boss actor",
                    "visible Toasty scarecrow-disguise boss actor",
                    "Toasty native class 0x013A T11 is the active scarecrow-disguise boss root. Its source payload owns the encounter path, links T51 at payload +0x20, and links the Blue phase-reward marker T53 at +0x28. When the disguise sequence reaches native phase 16, the handler copies T11's live position and palette to T51, activates T51 in animation 7, marks T11 defeated, and removes this first form.",
                    ColorRgba.FromRgb(145, 57, 45));
                return true;
            case ("toasty", 0x013A) when moby.TrueIndex == 51:
                identity = NativeClassObserved(
                    "Toasty sheep-on-stilts phase actor",
                    "dormant revealed sheep-on-stilts boss phase actor",
                    "Toasty native class 0x013A T51 is the dormant second boss form linked reciprocally to active root T11. T11's phase-16 branch moves T51 to the live boss position, enables its render/update state, and starts animation 7; the decoded animation-7 model is Toasty's revealed sheep standing on wooden stilts. T51 also owns the two contained Blue reward rows T54/T55.",
                    ColorRgba.FromRgb(191, 194, 194));
                return true;
            case ("peacekeepers", 0x01A1):
                identity = NativeClassObserved(
                    "Cannon-breakable target rock",
                    "nonvisual cannon auto-target and breakable-rock environment controller",
                    "Peace Keepers native class 0x01A1 is special-cased by the class-0x00E1 Cannon handler as an auto-aim target even though it has no Moby model. Its own handler waits for a cannon hit, switches environment animation 0, plays the destruction sound, marks the source row killed, and emits four sets of three rock-debris classes 0x01C6-0x01C8. The two source rows are the target-rock control points, not general scene markers.",
                    ColorRgba.FromRgb(143, 166, 184));
                return true;
            case ("mistybog", 0x01E7):
                identity = NativeClassObserved(
                    "Chicken cage",
                    "breakable wooden chicken cage scenery prop",
                    "Misty Bog native class 0x01E7 is the exact two-cage family T87/T88. Each row is placed beside one Shielded Greenie and one spotted chicken, matching the two chicken-trapping encounters. The five-animation 108-vertex wooden-bar cage model progresses from intact to flattened boards, while its dedicated damage handler drives animations 1/2/4 through the break sequence and removes the cage.",
                    ColorRgba.FromRgb(145, 111, 68));
                return true;
            case ("mistybog", 0x01E4):
                identity = NativeClassObserved(
                    "Arrow-sign Fairy",
                    "static Misty Bog route-arrow sign-bearing Fairy actor",
                    "Misty Bog native class 0x01E4 is the sole T211 sign-bearing Fairy. Its decoded five-frame model is a winged Fairy holding the large yellow route arrow, and the overlay gives the class no independent update handler. It is the visible tree-stump route cue, not a flight-course class-0x01E5 sign or a nonvisual route marker.",
                    ColorRgba.FromRgb(242, 156, 208));
                return true;
            case ("twilightharbor", 0x00AB):
                identity = NativeClassObserved(
                    "Drawbridge lever",
                    "flame-operated Twilight Harbor drawbridge lever prop",
                    "Twilight Harbor native class 0x00AB is the two-lever family T21/T22, one handle on each side of the drawbridge. Its two-animation lever model changes state when the native handler receives flame damage; that handler starts environment animation 0 and a timed transition, raising the drawbridge into the Supercharge ramp. These are interactive levers, not generic machinery or scenery anchors.",
                    ColorRgba.FromRgb(154, 164, 174));
                return true;
            case ("artisans", 0x000B):
            case ("magiccrafters", 0x000B):
            case ("alpineridge", 0x000B):
                identity = NativeClassObserved(
                    "Water bubble emitter",
                    "nonvisual ambient water-surface bubble particle emitter",
                    "Native class 0x000B is a non-rendered four-byte timer Moby. Its Artisans, Magic Crafters, and Alpine Ridge handlers tick that timer and, when it expires, spawn particle 0x16 at the marker before resetting the timer. Particle 0x16 jitters horizontally, rises at +8 Z, rotates, shrinks, fades, and uses the same semitransparent 32x32 four-bit bubble sprite in all three levels; all 22 rows form water-surface clusters and have no Moby model.",
                    ColorRgba.FromRgb(143, 166, 184));
                return true;
            case ("artisans", 0x0012):
                identity = NativeClassObserved(
                    "Sunny Flight stepping stone",
                    "Sunny Flight pond stepping-stone platform scenery prop",
                    "Artisans native class 0x0012 is the five-piece pond set that unlocks Sunny Flight. The five source rows occupy the known stepping-stone arc at one shared height, and the WAD model is a matching low 25-vertex, 40-triangle stone platform with two lit/unlit palette animations.",
                    ColorRgba.FromRgb(224, 190, 77));
                return true;
            case ("artisans", 0x015E):
                identity = NativeClassObserved(
                    "Sunny Flight stepping-stone unlock controller",
                    "nonvisual Sunny Flight stepping-stone environmental-animation controller",
                    "Artisans native class 0x015E is the sole controller linked by all five class-0x0012 stepping stones. Each stone owns one bit in T138's shared activation byte; the 0x015E handler watches that state, drives environment animation 0 and its opening sound, and restores the open frame immediately when Sunny Flight's visited flag is set.",
                    ColorRgba.FromRgb(116, 185, 255));
                return true;
            case ("artisans", 0x0031):
                identity = NativeClassObserved(
                    "Toasty portal gate controller",
                    "nonvisual Toasty dragon-mouth portal gate environmental-animation controller",
                    "Artisans native class 0x0031 occurs only at T160 beside the Toasty portal. Its props select environment animation 2 and link the neighboring dragon scene; the native handler checks Toasty's visited flag, the Artisans level-completion flags, and the linked dragon state before opening the gate and playing its sound.",
                    ColorRgba.FromRgb(116, 185, 255));
                return true;
            case ("artisans", 0x0187):
                identity = NativeClassObserved(
                    "Toasty portal particle emitter",
                    "paired nonvisual Toasty portal side-particle emitter",
                    "Artisans native class 0x0187 is the exact T43/T44 pair flanking the Toasty portal. Its handler projects each marker to a nearby collision triangle, derives a mirrored direction from its yaw, and repeatedly spawns particle type 0x0E from the two portal sides; the class has no model and is not scenery.",
                    ColorRgba.FromRgb(116, 185, 255));
                return true;
            case ("highcaves", 0x006D):
                identity = NativeClassObserved(
                    "Rescue Fairy",
                    "rescue fairy actor / High Caves Fairy Trio member",
                    "High Caves native class 0x006D is the three-member Fairy Trio that rescues Spyro after a fall. T9-T11 use the same three-animation winged Fairy model, select trio slots 2/0/1 in their props, and each own five embedded flight routes; the dedicated handler steers them relative to Spyro and enters the carrying/rescue state.",
                    ColorRgba.FromRgb(118, 214, 206));
                return true;
            case ("highcaves", 0x00E3):
                identity = NativeClassObserved(
                    "Temporary Superflame Fairy",
                    "temporary Superflame Fairy power-up actor",
                    "High Caves native class 0x00E3 is the standalone Fairy actor at T24. Its model topology and palette match Haunted Towers' temporary Superflame Fairy, and the shared Fairy-kiss handler grants the finite Superflame timer; the alternate permanent branch explicitly checks class 0x00F1 instead.",
                    ColorRgba.FromRgb(242, 156, 208));
                return true;
            case ("doctorshemp", 0x000D):
                identity = NativeClassObserved(
                    RewardLabel("Gem spawner", moby),
                    "nonvisual route-driven gem spawner control",
                    $"The native engine enum names class 0x000D MOBYCLASS_GEM_SPAWNER. Doctor Shemp T72-T75 each carry a 40-byte path payload and blue-gem class byte 0x55; the dedicated handler initializes the linked spawned Moby, moves it along the configured path, coordinates sibling spawners, and preserves the configured gem/reward class{RewardEvidence(moby)}.",
                    ColorRgba.FromRgb(82, 155, 225));
                return true;
            case ("wizardpeak", 0x0042):
                identity = NativeClassObserved(
                    RewardLabel("Ice Gnorc", moby),
                    "Ice Gnorc enemy actor / Elder Wizard-conjured Ice Gnorc",
                    $"Wizard Peak native class 0x0042 dispatches to a full ten-state enemy handler and uses a ten-animation, 139-vertex blue club-carrying Gnorc model. The ten source rows exactly form the Ice Gnorc family: T4 starts active, while the other nine are dormant actors linked through props to Elder Wizards and have their render radius raised to 0x20 when summoned{RewardEvidence(moby)}.",
                    ColorRgba.FromRgb(92, 156, 220));
                return true;
            case ("sunnyflight", 0x0166):
            case ("nightflight", 0x0166):
            case ("crystalflight", 0x0166):
            case ("wildflight", 0x0166):
            case ("icyflight", 0x0166):
                identity = NativeClassObserved(
                    "Flight challenge controller",
                    "nonvisual flight challenge manager/controller",
                    "All five flight overlays dispatch native class 0x0166 to the challenge manager. Its handler initializes and tracks the four objective counters and timer, creates the HUD digits, punctuation, and target icons, and handles completion, results, and records; the class has no WAD model and is not a route marker.",
                    ColorRgba.FromRgb(116, 185, 255));
                return true;
            case ("sunnyflight", 0x0105):
                identity = NativeClassObserved(
                    "Flight HUD timer digit",
                    "flight HUD timer digit display control marker",
                    "Sunny Flight native class 0x0105 is T52 in the hidden timer-display row beside class 0x0106, 0x0147, and 0x0109, which together form the changing flight timer. The engine's native Moby roster assigns sequential classes 0x0104-0x010D to NUMBER_0 through NUMBER_9, making this stable role a HUD timer digit rather than a course marker.",
                    ColorRgba.FromRgb(236, 240, 241));
                return true;
            case ("sunnyflight", 0x01EA):
                identity = NativeClassObserved(
                    "Flight barrel HUD icon",
                    "off-world flight barrel HUD icon display control marker",
                    "Sunny Flight native class 0x01EA is the eight-row hidden HUD icon pool at the canonical off-world display coordinate. Its decoded 440-byte simple model is a five-band barrel icon, the native spawn table groups it with the other flight-objective icons, and the eight rows match the eight train-barrel targets one-for-one.",
                    ColorRgba.FromRgb(214, 148, 82));
                return true;
            case ("crystalflight", 0x01E5):
            case ("nightflight", 0x01E5):
                identity = NativeClassObserved(
                    "Flight direction arrow sign",
                    "static flight-course directional arrow sign scenery",
                    "Crystal Flight and Night Flight native class 0x01E5 use the same two-animation sign-bearer model holding a large yellow directional arrow. The rows receive no actor update beyond the overlay default, so they are static course-direction scenery rather than route controls or HUD records.",
                    ColorRgba.FromRgb(245, 203, 66));
                return true;
            case ("artisans", 0x00AC):
            case ("stonehill", 0x00AC):
                identity = NativeClassObserved(
                    "Tower flag",
                    "animated tower-flag scenery prop",
                    "Native class 0x00AC is the same 15-frame, 13-vertex instance-colored flag model in Artisans and Stone Hill. Its decoded geometry is byte-for-byte identical after normalization across both WADs, and seven Artisans plus ten Stone Hill source rows account for the complete 17-flag family, including the previously live-observed tower flags.",
                    ColorRgba.FromRgb(235, 196, 76));
                return true;
            case ("dreamweavers", 0x007E):
            case ("jacques", 0x007E):
                identity = NativeClassObserved(
                    "Clock Fool",
                    "timed-platform Clock Fool enemy actor",
                    "Dream Weavers and Jacques native class 0x007E use the same canonical 13-animation actor model and near-identical 13-state route handlers. Each record owns route-point props and drives linked timed platforms, identifying all fifteen rows as visible Clock Fools rather than scenery.",
                    ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("dreamweavers", 0x0040):
                identity = NativeClassObserved(
                    "Wall lantern",
                    "animated hanging wall-lantern scenery prop",
                    "Dream Weavers native class 0x0040 is a passive 15-frame hanging light fixture: a textured gold/gray upper wall mount and cage, colored lower lantern body and finial, and a separate red/orange/yellow flame mesh. Its model is shared with Dark Passage, neither level gives the class a dedicated actor handler, and T149/T150 form a same-height pair on the portal facade; it is distinct from the freestanding class-0x0081 Lamp post.",
                    ColorRgba.FromRgb(217, 166, 79));
                return true;
            case ("magiccrafters", 0x010E):
            case ("alpineridge", 0x010E):
            case ("highcaves", 0x012E):
            case ("blowhard", 0x01F1):
                identity = NativeClassObserved(
                    RewardLabel("Green Druid", moby),
                    "green druid terrain-spell enemy actor",
                    "These four native Green Druid actor classes own the true-index links to class-0x0148 terrain-spell targets. Their level handlers drive the linked collision-triangle anchor during the moving-platform spell; Alpine Ridge's larger-radius and alternate update-distance rows are actor variants of the same family, not control markers.",
                    ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("jacques", 0x0096):
                identity = NativeClassObserved(
                    "Jacques (boss)",
                    "Jacques boss enemy actor",
                    "Jacques native class 0x0096 has one source row and a unique seven-animation, 224-vertex actor model. Its dedicated overlay handler implements the three-hit boss state machine, Spyro/camera locking, sounds, and related-object spawning.",
                    ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("loftycastle", 0x0128):
                identity = NativeClassObserved(
                    "Balloognorc balloon",
                    "linked Balloognorc enemy balloon component",
                    "Lofty Castle native class 0x0128 is a small two-animation red/yellow balloon model. Its nine rows pair one-for-one at identical coordinates with nine class-0x012A Balloognorc bodies; the balloon handler follows and bobs above its indexed owner and coordinates hit/animation state with that body.",
                    ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("beastmakers", 0x00BE):
            case ("dreamweavers", 0x00BF):
            case ("gnastysworld", 0x00C0):
                identity = NativeClassObserved(
                    "Balloonist",
                    "home-world balloonist actor",
                    "Native classes 0x00BE, 0x00BF, and 0x00C0 are the successive Beast Makers, Dream Weavers, and Gnasty's World Balloonist actors. Each home world has one source row with the matching 181-vertex, 294-triangle Balloonist model family; the Beast Makers row is also placed beside its transport balloon.",
                    ColorRgba.FromRgb(248, 196, 113));
                return true;
            case ("gnastygnorc", 0x009F) when
                (moby.Type is 0x00 or 0x20) &&
                moby.Flag4A == 0x50 &&
                moby.Flag4B == 0xFF &&
                moby.SourceByte4F == 0x00:
                identity = NativeClassObserved(
                    "Key thief",
                    "visible key-carrying final-boss chase thief actor",
                    "Gnasty Gnorc native class 0x009F is the two-actor key-thief family at T1/T2. Its dedicated chase/path handler reacts to Spyro, drives movement and animation, and follows each thief's private link to a class-0x00B5 carried key. The decoded three-animation actor model and reciprocal key-owner links identify both render-radius variants as visible thieves, not route or boss-control markers.",
                    ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("gnastygnorc", 0x00B5) when
                (moby.Type is 0x00 or 0x20) &&
                moby.Flag4A == 0x10 &&
                moby.Flag4B == 0xFF &&
                moby.SourceByte4F == 0x02:
                identity = NativeClassObserved(
                    "Key-thief key",
                    "visible key collectible carried by a final-boss key thief",
                    "Gnasty Gnorc native class 0x00B5 is the reflective carried-key model at T100/T101. Each key's props link back to its class-0x009F thief owner and forward to a reciprocal class-0x00B9 keyhole insertion target; the native handler exposes and collects the key, sets the global key flag, uses the target's position and yaw for insertion, and starts the selected gate animation.",
                    ColorRgba.FromRgb(248, 196, 113));
                return true;
            case ("gnastygnorc", 0x00B9) when
                moby.Type == 0x00 &&
                moby.Flag4A == 0x10 &&
                moby.Flag4B == 0xFF &&
                moby.SourceByte4F == 0x00:
                identity = NativeClassObserved(
                    moby.TrueIndex switch
                    {
                        102 => "First keyhole insertion target",
                        103 => "Second keyhole insertion target",
                        _ => "Keyhole insertion target"
                    },
                    "nonvisual key-lock insertion and camera orientation anchor",
                    "Gnasty Gnorc native class 0x00B9 has no visible model or dedicated actor handler. T102 and T103 each store one class-0x00B5 key index, while those keys point reciprocally back to their target. The key handler reads the target's XYZ and yaw to face the lock, position the key, and drive the insertion camera before opening the linked environment animation; these rows are keyhole anchors, not thief route waypoints or door-opening controllers.",
                    ColorRgba.FromRgb(143, 166, 184));
                return true;
            case ("gnastysloot", 0x00B5) when
                moby.Type == 0x20 &&
                moby.Flag4A == 0x10 &&
                moby.Flag4B == 0xFF &&
                moby.SourceByte4F == 0x02:
                identity = NativeClassObserved(
                    "Thief key",
                    "visible carried key collectible and key-gate unlock actor",
                    "Gnasty's Loot native class 0x00B5 is the complete four-key family at T1/T2/T3/T9. The native handler reads each key's carrying ground-thief or aircraft owner, exposes and collects the key when that holder is gone, sets the global key flag, uses the reciprocal class-0x00B9 unlock-pose marker, starts the selected environment gate animation, and clears the key flag.",
                    ColorRgba.FromRgb(248, 196, 113));
                return true;
            case ("gnastysloot", 0x00B9) when
                moby.Type == 0x00 &&
                moby.Flag4A == 0x10 &&
                moby.Flag4B == 0xFF &&
                moby.SourceByte4F == 0x00:
                identity = NativeClassObserved(
                    "Key-gate unlock pose marker",
                    "nonvisual key-gate unlock position and yaw target",
                    "Gnasty's Loot native class 0x00B9 is the four-marker family at T7/T8/T10/T11. Each one-word payload points to a class-0x00B5 key, and each key points reciprocally back to its marker. The key handler uses that marker's position and yaw to require Spyro's facing and animate the unlock sequence before starting the selected environment animation; B9 has no model or handler and is not a thief or aircraft route waypoint.",
                    ColorRgba.FromRgb(143, 166, 184));
                return true;
            case ("gnastysloot", 0x00D9) when
                moby.Type == 0x00 &&
                moby.Flag4A == 0xFF &&
                moby.Flag4B == 0xFF &&
                moby.SourceByte4F == 0x00:
                identity = NativeClassObserved(
                    "Gnasty's Loot 100% ending controller",
                    "nonvisual 14,000-gem ending and cutscene controller",
                    "Gnasty's Loot native class 0x00D9 is the sole T128 ending controller. Its native handler requires the 14,000-gem total and Spyro's completion state, fades the environment, marks the level exit, selects cutscene 3, and switches to the ending game state; it is not a return-home or thief-route marker.",
                    ColorRgba.FromRgb(116, 185, 255));
                return true;
            case ("gnastysworld", 0x00D0) when
                moby.Type == 0x00 &&
                moby.Flag4A == 0xFF &&
                moby.Flag4B == 0xFF &&
                moby.SourceByte4F == 0x00:
                identity = NativeClassObserved(
                    "Gnorc Gnexus progression-gate controller",
                    "nonvisual final-homeworld portal-gate environment-animation controller",
                    "Gnasty's World native class 0x00D0 is the sole T1 progression controller. Its native handler checks the visited flags for the three final levels and advances their gate environment animations, then checks 12,000 gems, 80 dragons, and 12 eggs to drive the bonus-level gate animation; it is not a visible portal, travel marker, or level-name sign.",
                    ColorRgba.FromRgb(116, 185, 255));
                return true;
            case ("icecavern", 0x00CD):
                identity = NativeClassObserved(
                    "Chargeable lamppost",
                    "chargeable gem lamppost interactive scenery prop",
                    "Ice Cavern native class 0x00CD dispatches to the dedicated charge/damage-aware lamppost handler. Charging switches its animation/state and raises or releases its linked gem; six of the ten source rows link directly to red, green, or yellow gems while the other four are unlinked posts.",
                    ColorRgba.FromRgb(121, 178, 226));
                return true;
            case ("icecavern", 0x00F0):
                identity = NativeClassObserved(
                    "Ice stalactite",
                    "static hanging stalactite scenery prop",
                    "Ice Cavern native class 0x00F0 is a three-piece static stalactite family. Its one-frame, 52-vertex non-colliding model has a broad ceiling attachment tapering 1,738 model units to a point; all three rows hang above gameplay clusters and use the overlay's default no-op dispatch with no reward or behavioral link.",
                    ColorRgba.FromRgb(154, 190, 211));
                return true;
            case ("icecavern", 0x01A6):
                identity = NativeClassObserved(
                    "Extra Life Chest butterfly",
                    "visible butterfly actor presented by an Extra Life Chest",
                    "Ice Cavern T229/T230 are loader-created class-0x01A6 children at the exact coordinates of the two class-0x01A5 Extra Life Chests T186/T187. The native Extra Life Chest handler spawns class 0x01A6 as its linked child during the opening cycle, and the decoded one-animation model is a 29-frame pair of flapping wings; these are visible chest butterflies, not level-control markers.",
                    ColorRgba.FromRgb(244, 205, 69));
                return true;
            case ("gnorccove", 0x00CF):
                identity = NativeClassObserved(
                    "Steel barrel shell",
                    "linked visible steel-barrel component prop",
                    "Gnorc Cove native class 0x00CF decodes as a complete capped steel barrel mesh with four eight-point rings and two cap centers. All eleven rows pair with class-0x00C7 Engineer or class-0x00C9 TNT Wrangler hosts; sibling class 0x00CA has the same geometry with a red TNT-barrel palette, distinguishing these eleven as steel shells rather than generic chest links.",
                    ColorRgba.FromRgb(177, 126, 70));
                return true;
            case ("gnorccove", 0x009A):
                identity = NativeClassObserved(
                    "Barrel supply hatch",
                    "interactive barrel-supply hatch prop",
                    "Gnorc Cove native class 0x009A is a six-animation, 58-vertex barrel dispenser. Its rest pose is a thin circular floor hatch; each 14-frame cycle raises the barrel mechanism roughly 2,800 model units and fires three frame sounds. Nine rows sit directly in front of the nine Dockworkers and thirteen are standalone lane dispensers.",
                    ColorRgba.FromRgb(116, 145, 157));
                return true;
            case ("hauntedtowers", 0x0088):
                identity = NativeClassObserved(
                    "Metal door",
                    "Superflame-destructible metal door scenery prop",
                    "Haunted Towers native class 0x0088 is a large, thin doorway-sized metal slab with separate intact/destroyed model states. All twelve source rows occupy castle doorways in the level whose metal doors require Superflame, so these are visible doors rather than nonvisual special controls.",
                    ColorRgba.FromRgb(113, 119, 132));
                return true;
            case ("hauntedtowers", 0x008E):
                identity = NativeClassObserved(
                    "Temporary Superflame Fairy",
                    "temporary Superflame Fairy power-up actor",
                    "Haunted Towers native class 0x008E uses the 247-vertex winged Fairy model and the shared Fairy-kiss handler. Its normal branch grants the timed Superflame duration; four source rows use this class.",
                    ColorRgba.FromRgb(242, 156, 208));
                return true;
            case ("hauntedtowers", 0x00F1):
                identity = NativeClassObserved(
                    "Permanent Superflame Fairy",
                    "permanent Superflame Fairy power-up actor",
                    "Haunted Towers native class 0x00F1 is the unique alternate Fairy model checked explicitly by the Fairy-kiss handler. That branch sets the persistent level-long Superflame state, matching the one hidden permanent-power Fairy source row.",
                    ColorRgba.FromRgb(244, 179, 78));
                return true;
            case ("clifftown", 0x0192):
            case ("terracevillage", 0x0192):
            case ("darkpassage", 0x0192):
            case ("gnastysloot", 0x0192):
                identity = NativeClassObserved(
                    "Lantern post",
                    "decorative caged lantern-post scenery prop",
                    "Native class 0x0192 uses the same normalized 43-vertex, 76-triangle caged lantern-on-a-pole model in Cliff Town, Terrace Village, Dark Passage, and Gnasty's Loot. All nine rows are visible lantern posts with level-local palette data, not generic route scenery.",
                    ColorRgba.FromRgb(217, 166, 79));
                return true;
            case ("beastmakers", 0x00AE):
            case ("darkhollow", 0x00AE):
            case ("doctorshemp", 0x00AE):
            case ("drycanyon", 0x00AE):
            case ("gnorccove", 0x00AE):
            case ("icecavern", 0x00AE):
            case ("jacques", 0x00AE):
            case ("loftycastle", 0x00AE):
            case ("magiccrafters", 0x00AE):
            case ("metalhead", 0x00AE):
            case ("peacekeepers", 0x00AE):
            case ("stonehill", 0x00AE):
            case ("treetops", 0x00AE):
                identity = NativeClassObserved(
                    "Key Chest",
                    "key-required locked chest shell",
                    "Native class 0x00AE has the same 103-vertex, 146-triangle chest-and-padlock model across all thirteen source levels. The reward/drop byte carries the contained gem class; later-level update-distance 0xFF rows are still the same locked Key Chest family and are not trees or passive scenery.",
                    ColorRgba.FromRgb(230, 126, 34));
                return true;
            case ("loftycastle", 0x0058):
                identity = NativeClassObserved(
                    "Caged Fairy",
                    "Lofty Castle caged fairy rescue actor",
                    "Lofty Castle native class 0x0058 dispatches to a dedicated five-state moving rescue-actor handler. Its fifteen rows pair one-for-one at identical X/Y with fifteen separate class 0x003D cage props, whose damage handler activates a linked Moby.",
                    ColorRgba.FromRgb(118, 214, 206));
                return true;
            case ("darkpassage", 0x0032):
            case ("darkpassage", 0x0033):
                identity = NativeClassRoster(
                    RewardLabel("Puppy / Devil Dog", moby),
                    "transforming puppy / devil dog enemy",
                    $"Dark Passage has thirty class 0x0032 source actors. The level-51 combat handler changes the same Moby between native model classes 0x0032 and 0x0033, while no 0x0033 source roots exist; these are transforming enemies, not control links{RewardEvidence(moby)}.",
                    ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("darkpassage", 0x0035):
            case ("darkpassage", 0x0036):
                identity = NativeClassRoster(
                    RewardLabel("Turtle / Mutant Turtle", moby),
                    "transforming turtle / mutant turtle enemy",
                    $"Dark Passage uses native classes 0x0035 and 0x0036 as the runtime light/dark model forms of the same Turtle/Mutant Turtle enemy family; the alternate class is not a standalone control record{RewardEvidence(moby)}.",
                    ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("hauntedtowers", 0x00CB):
                identity = NativeClassObserved(
                    RewardLabel("Tin Soldier", moby),
                    "tin soldier enemy",
                    $"Haunted Towers native class 0x00CB dispatches to a dedicated armored actor handler that applies flame heat and spawns six class-0x00D7 model fragments on its 0x00080000 damage path. All twenty-five source rows use this class; class 0x00CC has a separate lightning-spawning Summoning Wizard handler{RewardEvidence(moby)}.",
                    ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("icecavern", 0x00D5):
                identity = NativeClassObserved(
                    "Bat",
                    "bat fodder",
                    "Ice Cavern native class 0x00D5 dispatches to a dedicated damage-aware five-state flying actor handler. It activates within 0x800, moves toward a target, and uses Sin/Cos motion to circle Spyro; exactly sixteen source rows T96-T111 use this class.",
                    ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("mistybog", 0x001C):
                identity = NativeClassRoster(
                    RewardLabel("Shielded Greenie", moby),
                    "shielded greenie enemy",
                    $"Misty Bog has twenty-six native class 0x001C source actors dispatched to one complete flame, damage, attack, movement, animation, sound, and death handler with no class transition. Shared or zero-filled formation payloads do not change their actor identity{RewardEvidence(moby)}.",
                    ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("metalhead", 0x0030):
                identity = NativeClassObserved(
                    RewardLabel("Metalhead boss", moby),
                    "Metalhead boss enemy actor",
                    $"Metalhead native class 0x0030 is the visible boss actor. Its sole source row carries a blue-gem drop; byte 0x50 is its render radius and does not make the row a reward-control marker{RewardEvidence(moby)}.",
                    ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("metalhead", 0x0041):
                identity = NativeClassObserved(
                    "Power pole",
                    "destructible Metalhead power pylon",
                    "Metalhead's seventeen native class 0x0041 rows dispatch to the boss-room power-pole handler, which applies flame damage, spawns destruction effects, decrements the boss counter, and frees linked children. They form the two visible destructible waves, not nonvisual controls.",
                    ColorRgba.FromRgb(93, 173, 226));
                return true;
            case ("metalhead", 0x0066):
                identity = NativeClassObserved(
                    RewardLabel("Armored Banana Boy", moby),
                    "armored banana boy enemy",
                    $"Metalhead native class 0x0066 dispatches to a handler with the same 87-call sequence as Tree Tops class 0x0064 Banana Boys, while the Metalhead roster distinguishes the armored variant. All twenty-one source rows are reward-dropping actors; byte 0x50 only varies render radius and does not make radius-zero rows into controls{RewardEvidence(moby)}.",
                    ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("metalhead", 0x00AA):
                identity = NativeClassObserved(
                    RewardLabel("Strongarm", moby),
                    "strongarm enemy",
                    $"Metalhead native class 0x00AA uses the same handler sequence as Tree Tops class 0x0065 Strongarms. All eleven source rows are reward-dropping actors; byte 0x50 is their render radius, not an object type or helper marker{RewardEvidence(moby)}.",
                    ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("beastmakers", 0x0005):
            case ("mistybog", 0x0005):
            case ("terracevillage", 0x0005):
                identity = NativeClassObserved(
                    "Swamp grass clump",
                    "tall swamp-grass/reed scenery clump",
                    "Native class 0x0005 uses the same byte-identical static model in Beast Makers, Misty Bog, and Terrace Village: nine long independent triangular blades radiate from a tight base, with a dark swamp palette and no collision, sound, or update handler. All 44 rows are decorative grass/reed clumps.",
                    ColorRgba.FromRgb(102, 137, 76));
                return true;
            case ("beastmakers", 0x0006):
            case ("mistybog", 0x0006):
                identity = NativeClassObserved(
                    "Beast Makers banner",
                    "decorative Beast Makers three-tail banner/pennant scenery prop",
                    "Native class 0x0006 is a static planar cloth model: a textured green-and-brown panel with a gold center emblem and three stacked pointed tails. Beast Makers and Misty Bog share identical indexed art and palette; the four rows are decorative banners, not waterfalls.",
                    ColorRgba.FromRgb(171, 139, 67));
                return true;
            case ("beastmakers", 0x01DB):
                identity = NativeClassObserved(
                    "Crocodile",
                    "stationary flame/charge-reactive crocodile enemy actor",
                    "Beast Makers native class 0x01DB uses a unique five-animation, 58-vertex green crocodilian model with a long snout, tail, four limbs, and collision. Its dedicated five-state handler selects animation 1 for charge damage, animation 2 for flame damage, and completion states through animations 3/4; all four rows are stationary reactive Crocodiles, distinct from class-0x0089 Giant Boars.",
                    ColorRgba.FromRgb(95, 153, 83));
                return true;
            case ("alpineridge", 0x01F3):
                identity = NativeClassObserved(
                    "Broad alpine tree",
                    "broad conical alpine-tree scenery prop",
                    "Alpine Ridge native class 0x01F3 is a passive 41-vertex, 40-face conical tree model with the same six-ring tree topology as the large alpine-tree family. Its broader canopy geometry and 15-color warm-olive palette distinguish all eight rows from the slender class 0x01F8 variant; the class has no actor update dispatch and is not the unrelated class 0x00F3 Red flag.",
                    ColorRgba.FromRgb(106, 124, 71));
                return true;
            case ("alpineridge", 0x01F4):
            case ("blowhard", 0x01F4):
                identity = NativeClassObserved(
                    "Small swaying alpine tree",
                    "small animated alpine-tree scenery prop",
                    "Native class 0x01F4 is a 17-frame, 13-vertex tapered tree-sheet model shared by Alpine Ridge and Blowhard. Its animation visibly bends the tree while both level dispatchers leave the class without an actor update handler, identifying all eight rows as small swaying alpine-tree scenery.",
                    ColorRgba.FromRgb(96, 151, 115));
                return true;
            case ("blowhard", 0x01F7):
            case ("magiccrafters", 0x01F7):
                identity = NativeClassObserved(
                    "Large alpine tree",
                    "large conical alpine-tree scenery prop",
                    "Native class 0x01F7 uses the same canonical 41-vertex, 40-face conical tree geometry in Blowhard and Magic Crafters. Six connected canopy/root rings form the large tree, and neither level dispatches this class to an actor update handler.",
                    ColorRgba.FromRgb(83, 137, 108));
                return true;
            case ("blowhard", 0x01F8):
            case ("magiccrafters", 0x01F8):
                identity = NativeClassObserved(
                    "Slender alpine tree",
                    "slender conical alpine-tree scenery prop",
                    "Native class 0x01F8 is the narrower sibling of class 0x01F7: the shared 41-vertex, 40-face tall-tree topology is about five to six percent slimmer and uses a purple-gray final palette color. Neither level gives it an actor update handler.",
                    ColorRgba.FromRgb(117, 119, 143));
                return true;
            case ("beastmakers", 0x01E6):
            case ("mistybog", 0x01E6):
            case ("terracevillage", 0x01E6):
            case ("treetops", 0x01E6):
            case ("metalhead", 0x01E6):
                identity = NativeClassObserved(
                    "Torch",
                    "animated torch scenery prop",
                    "Native class 0x01E6 has no actor update handler and uses a 15-frame, 45-vertex animated scenery model shared nearly byte-for-byte across all five Beast Makers-region levels. Its palette contains the red/orange/yellow flame ramp, identifying all 124 source rows as torches rather than class 0x00E6 Birds.",
                    ColorRgba.FromRgb(230, 126, 34));
                return true;
            default:
                identity = default;
                return false;
        }
    }

    private static bool TryClassifyNativeDragonScene(Moby moby, out MobyIdentity identity)
    {
        bool commonTail = moby.SourceByte4F == 0x00 && moby.Flag4A == 0x10 && moby.Flag4B == 0xFF;
        if (commonTail &&
            moby.Type is 0x20 or 0x3C &&
            moby.SourceByte36 == 0xFA &&
            moby.SourceByte37 == 0x00)
        {
            identity = new MobyIdentity(
                "Dragon",
                "native dragon actor/model",
                "native-scene-family",
                "All 79 native dragon actors use the 0xFA/0x00/0x00/0x10/0xFF scene signature and pair one-to-one with a pedestal, a scene-link control, an approach camera, and a later cinematic camera track.",
                ColorRgba.FromRgb(183, 140, 255));
            return true;
        }

        if (commonTail &&
            moby.Type == 0x20 &&
            moby.SourceByte36 is 0x4B or 0x4C or 0x4D &&
            moby.SourceByte37 == 0x01)
        {
            identity = new MobyIdentity(
                "Dragon pedestal",
                "native dragon pedestal / linked rescue prop",
                "native-scene-family",
                "All 79 native dragon pedestals use the 0x4B-0x4D/0x01/0x00/0x10/0xFF scene family and pair one-to-one with a dragon and scene-link control.",
                ColorRgba.FromRgb(183, 140, 255));
            return true;
        }

        if (commonTail &&
            moby.Type == 0x00 &&
            moby.SourceByte36 == 0x6E &&
            moby.SourceByte37 == 0x00)
        {
            identity = new MobyIdentity(
                "Dragon scene link control",
                "dragon/pedestal scene link control",
                "native-scene-family",
                "All 79 native 0x6E controls pair one-to-one with a dragon and pedestal. The approach camera and later cinematic camera track live in separate data linked through the dragon.",
                ColorRgba.FromRgb(183, 140, 255));
            return true;
        }

        identity = default;
        return false;
    }

    private static bool TryClassifyEggThief(string levelKey, Moby moby, out MobyIdentity identity)
    {
        bool isClassicEggThief = moby.Flag4A == 0x30;
        bool isAlpineRidgeEggThief = string.Equals(levelKey, "alpineridge", StringComparison.OrdinalIgnoreCase) && moby.Flag4A == 0x20;
        if (moby.SourceByte36 == 0x21
            && (isClassicEggThief || isAlpineRidgeEggThief)
            && moby.Flag4B == 0x22
            && moby.Type is 0x20 or 0x40)
        {
            string evidence = isAlpineRidgeEggThief
                ? "Alpine Ridge T43 has the same 0x21/0x22 egg-thief signature as the other Magic Crafters-region thief records, with a local 0x20 behavior byte variant."
                : moby.Type == 0x40 && string.Equals(levelKey, "stonehill", StringComparison.OrdinalIgnoreCase)
                ? "Stone Hill T166 uses the same 0x21/0x30/0x22 egg-thief signature as Town Square and Peace Keepers egg thieves; Stone Hill stores that thief as a type 0x40 interactive actor record."
                : "Town Square and Peace Keepers user overrides identify this exact actor fingerprint as egg thieves; the odd behavior bytes separate it from chest records.";
            identity = Observed("Egg thief", "egg thief enemy", evidence, ColorRgba.FromRgb(230, 91, 67));
            return true;
        }

        identity = default;
        return false;
    }

    private static bool TryClassifyDoctorShempTrueIndex(Moby moby, out MobyIdentity identity)
    {
        switch (moby.TrueIndex)
        {
            case >= 3 and <= 14:
                identity = UserObserved(
                    "Torch",
                    "torch scenery prop",
                    $"Doctor Shemp user review identifies source record T{moby.TrueIndex} as scenery/torch, not Fat Momma/Cauldron.",
                    ColorRgba.FromRgb(241, 196, 15));
                return true;

            case 15:
            case 16:
            case 23:
            case 25:
            case 27:
            case 28:
            case 29:
                identity = UserObserved(
                    "Fat Momma/Cauldron",
                    "fat momma cauldron enemy",
                    $"Doctor Shemp user review identifies source record T{moby.TrueIndex} as Fat Momma/Cauldron.",
                    ColorRgba.FromRgb(230, 126, 34));
                return true;

            case 0:
            case 17:
            case 18:
            case 19:
            case 20:
            case 21:
            case 22:
            case 24:
            case 26:
                identity = UserObserved(
                    "Kamikaze Gnorc",
                    "kamikaze gnorc enemy",
                    $"Doctor Shemp user review identifies source record T{moby.TrueIndex} as a Kamikaze Gnorc.",
                    ColorRgba.FromRgb(230, 91, 67));
                return true;

            default:
                identity = default;
                return false;
        }
    }

    private static bool TryClassifyMagicCraftersTrueIndex(Moby moby, out MobyIdentity identity)
    {
        if (moby.TrueIndex == 39)
        {
            identity = UserObserved(
                "Balloonist",
                "home-world balloonist",
                "Magic Crafters user review identifies source record T39 as the Balloonist standing beside the transport balloon.",
                ColorRgba.FromRgb(248, 196, 113));
            return true;
        }

        identity = default;
        return false;
    }

    private static bool TryClassifyGem(Moby moby, out MobyIdentity identity)
    {
        if (moby.Type == 0x18 &&
            moby.SourceByte36 == 0xC2 &&
            moby.Flag4A == 0x40 &&
            moby.Flag4B is >= 0x53 and <= 0x57)
        {
            identity = new MobyIdentity(
                RewardLabel("Flame/charge chest", moby),
                "flame-or-charge breakable chest",
                "native-class-byte-pattern",
                "Native class 0x00C2 is the wooden chest family; the loader's type 0x18/flag 0x40 runtime form preserves that class while flag4B selects the reward color.",
                ColorRgba.FromRgb(230, 126, 34));
            return true;
        }

        if (moby.Type == 0x18 &&
            moby.SourceByte36 == 0xC3 &&
            moby.Flag4A == 0x40 &&
            moby.Flag4B is >= 0x53 and <= 0x57)
        {
            identity = Observed(
                RewardLabel("Charge chest", moby),
                "charge chest",
                $"Cliff Town release review groups this type 0x18/source byte 0xC3 reward-bearing family with the existing source byte 0xC3 charge chest records{RewardEvidence(moby)}.",
                ColorRgba.FromRgb(211, 84, 0));
            return true;
        }

        if (moby.Type == 0x18 &&
            moby.SourceByte36 == 0xA5 &&
            moby.SourceByte37 == 0x01 &&
            moby.Flag4A == 0x40 &&
            moby.Flag4B == 0x0E)
        {
            identity = new MobyIdentity(
                "Life chest",
                "life chest",
                "native-class-byte-pattern",
                "Native class 0x01A5/behavior byte 0x0E is the life-chest family; these type 0x18/flag 0x40 rows are its loader-created runtime form.",
                ColorRgba.FromRgb(230, 126, 34));
            return true;
        }

        if (moby.Type == 0x18 && moby.SourceByte36 == 0xAE)
        {
            identity = NativeClassObserved(
                "Key Chest",
                "key-required chest shell",
                "Native class 0x00AE has the same 103-vertex, 146-triangle chest-and-padlock model across all thirteen source levels. Dark Hollow T58 is the loader-transformed type-0x18 form of that same Key Chest family; its contained gem records hold the reward colors.",
                ColorRgba.FromRgb(230, 126, 34));
            return true;
        }

        if (moby.HasNativeKeyFingerprint)
        {
            identity = new MobyIdentity(
                "Key",
                "key collectible",
                "byte-pattern",
                "The 0xAD/0x02 key family is type 0x00/flag 0x00 in the source table and becomes type 0x18/flag 0x40 after the level loader transforms it.",
                ColorRgba.FromRgb(245, 214, 92));
            return true;
        }

        GemValue gem = moby.Gem;
        if (moby.Type == 0x18 && gem != GemValue.Unknown)
        {
            identity = new MobyIdentity(
                gem.DisplayName,
                $"{gem.Name.ToLowerInvariant()} collectible",
                "byte-pattern",
                $"Type 0x18 with gem byte 0x{moby.SourceByte36:X2}/value byte 0x{moby.SourceByte4F:X2}.",
                gem.Color);
            return true;
        }

        identity = default;
        return false;
    }

    private static bool TryClassifyNativeBossContainedGem(string levelKey, Moby moby, out MobyIdentity identity)
    {
        if (string.Equals(levelKey, "toasty", StringComparison.OrdinalIgnoreCase) &&
            moby.TrueIndex is 54 or 55 &&
            moby.Type == 0x00 &&
            moby.SourceByte36 == 0x0D &&
            moby.SourceByte37 == 0x00 &&
            moby.SourceByte4F == 0x00 &&
            moby.Flag4A == 0xFF &&
            GemValue.TryFromIdByte(moby.Flag4B, out GemValue gem))
        {
            identity = NativeClassObserved(
                $"Toasty sheep-form contained {gem.DisplayName}",
                "nonvisual Toasty sheep-form contained boss reward",
                $"Toasty class 0x000D T{moby.TrueIndex}'s native payload begins with owner true index 51, the dormant sheep-on-stilts phase actor. Both linked records preserve the contained-gem encoding, so the editor identifies the boss reward without treating it as locked-chest content.",
                gem.Color);
            return true;
        }

        identity = default;
        return false;
    }

    private static bool TryClassifyContainedGem(Moby moby, out MobyIdentity identity)
    {
        if (moby.Type == 0x00 && moby.Flag4A == 0xFF && GemValue.TryFromIdByte(moby.Flag4B, out GemValue gem))
        {
            identity = new MobyIdentity(
                $"Contained {gem.DisplayName}",
                "locked chest contained reward marker",
                "byte-pattern",
                $"Type 0x00 with flag4A 0xFF and contained gem byte 0x{moby.Flag4B:X2}.",
                gem.Color);
            return true;
        }

        identity = default;
        return false;
    }

    private static bool TryClassifyNativeArmoredChestLink(Moby moby, out MobyIdentity identity)
    {
        bool isNativeArmoredChestClass = moby.SourceByte36 == 0x91 && moby.SourceByte37 == 0x01;
        bool isLinkedVariant = moby.Type != 0x20 || moby.Flag4A != 0x10;
        if (isNativeArmoredChestClass && isLinkedVariant)
        {
            identity = new MobyIdentity(
                RewardLabel("Armored / super flame chest linked record", moby),
                "native armored/super flame chest linked control record",
                "native-class-linked",
                $"The full native class 0x0191 is MOBYCLASS_ARMORED_CHEST in the decompilation and 21 same-class source rows use the validated Super flame chest family; this outlier is retained as a linked/control record rather than mislabeled from its low byte{RewardEvidence(moby)}.",
                ColorRgba.FromRgb(230, 126, 34));
            return true;
        }

        identity = default;
        return false;
    }

    private static bool TryClassifyType20(string levelKey, Moby moby, out MobyIdentity identity)
    {
        if (string.Equals(levelKey, "artisans", StringComparison.OrdinalIgnoreCase) && moby.SourceByte36 == 0x12 && moby.Flag4B == 0xFF)
        {
            identity = PositionInferred("Artisans platform scenery", "Artisans platform scenery prop", "Five no-reward Artisans records share one source/package and sit around the dragon pedestal and nearby portal-pad cluster, so the editor treats them as local platform scenery rather than unidentified actors.", ColorRgba.FromRgb(93, 173, 226));
            return true;
        }

        if (string.Equals(levelKey, "nightflight", StringComparison.OrdinalIgnoreCase) && moby.SourceByte36 == 0xE5 && moby.Flag4B == 0xFF)
        {
            identity = ReviewInferred("Night Flight course marker", "Night Flight course prop/control marker", "Release review groups this no-reward Night Flight source family with the flight-course route/target setup; exact role still needs a focused course test.", ColorRgba.FromRgb(143, 166, 184));
            return true;
        }

        if (IsFlightLevel(levelKey) && moby.SourceByte36 == 0xE5 && moby.Flag4B == 0xFF)
        {
            identity = ReviewInferred("Flight course marker", "flight course prop/control marker", "Release review groups this no-reward flight-level source family with route and target setup records; exact role still needs a focused course test.", ColorRgba.FromRgb(143, 166, 184));
            return true;
        }

        if (TryClassifyLevelRosterFamily(levelKey, moby, out identity))
            return true;

        if (IsBeastMakersRegionalProp(levelKey, moby))
        {
            identity = PositionInferred("Beast Makers scenery prop", "Beast Makers regional scenery prop", $"Source byte 0x{moby.SourceByte36:X2} no-reward records repeat across Beast Makers-region scenery and treasure clusters, so the editor treats them as local scenery instead of unidentified actors.", ColorRgba.FromRgb(93, 173, 226));
            return true;
        }

        if (string.Equals(levelKey, "icecavern", StringComparison.OrdinalIgnoreCase) && moby.SourceByte36 is 0xCD or 0xF0 && moby.Flag4B == 0xFF)
        {
            identity = PositionInferred("Ice Cavern scenery prop", "Ice Cavern scenery prop", "These no-reward Ice Cavern records share repeated scenery packages and sit beside gems, chests, flags, and level route points, so the editor treats them as scenery rather than actors.", ColorRgba.FromRgb(93, 173, 226));
            return true;
        }

        if (IsMagicCraftersRegionalProp(levelKey, moby))
        {
            identity = PositionInferred("Magic Crafters scenery prop", "Magic Crafters regional scenery prop", $"Source byte 0x{moby.SourceByte36:X2} no-reward records repeat around Magic Crafters-region route, chest, and portal clusters, so the editor treats them as scenery rather than actors.", ColorRgba.FromRgb(93, 173, 226));
            return true;
        }

        if (string.Equals(levelKey, "dreamweavers", StringComparison.OrdinalIgnoreCase) && moby.SourceByte36 == 0x7E && moby.Flag4B == 0xFF)
        {
            identity = PositionInferred("Dream Weavers scenery prop", "Dream Weavers/Jacques scenery prop", "This no-reward Dream Weavers/Jacques source family repeats as local scenery around treasure, portal, and route clusters, so the editor treats it as scenery rather than an unidentified actor.", ColorRgba.FromRgb(93, 173, 226));
            return true;
        }

        if (moby.SourceByte36 == 0x92 && moby.Flag4B == 0xFF)
        {
            identity = PositionInferred("Shared route scenery prop", "shared route scenery prop", "This no-reward source family appears in route-heavy areas across Cliff Town, Terrace Village, Dark Passage, and Gnasty's Loot, so the editor treats it as route scenery rather than a free actor.", ColorRgba.FromRgb(93, 173, 226));
            return true;
        }

        switch (moby.SourceByte36)
        {
            case 0x09:
                identity = new MobyIdentity("Return Home platform", "return-home platform", "byte-pattern", "Stone Hill validation mapped source byte 0x09 to the return-home platform family.", ColorRgba.FromRgb(73, 192, 211));
                return true;
            case 0x0A when moby.Flag4A == 0x10 && moby.Flag4B == 0x10:
                identity = Observed("Sheep", "sheep fodder", "Artisans user overrides repeatedly identify this exact 0x0A/0x10 fodder fingerprint as sheep.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case 0x17 when moby.Flag4A == 0x10:
                identity = Observed("Bull", "bull enemy", "Town Square user overrides repeatedly identify source byte 0x17 actor records as bulls.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case 0x38:
                identity = new MobyIdentity(RewardLabel("Firework chest", moby), "firework chest", "byte-pattern", $"User validation mapped source byte 0x38 to firework chest objects{RewardEvidence(moby)}.", ColorRgba.FromRgb(230, 126, 34));
                return true;
            case 0x49:
                identity = new MobyIdentity(RewardLabel("Spring chest", moby), "spring chest", "byte-pattern", $"User validation mapped source byte 0x49 to spring chest objects{RewardEvidence(moby)}.", ColorRgba.FromRgb(230, 126, 34));
                return true;
            case 0x4A when moby.Flag4B == 0xFF:
                identity = Observed("Torch", "torch scenery prop", "Dry Canyon and Peace Keepers user overrides repeatedly identify this source byte 0x4A scenery fingerprint as torches.", ColorRgba.FromRgb(241, 196, 15));
                return true;
            case 0x4C when moby.Flag4B == 0xFF:
                identity = Observed("Dragon pedestal", "dragon pedestal / linked rescue prop", "Dry Canyon and Peace Keepers user overrides repeatedly identify this source byte 0x4C scenery fingerprint as dragon pedestals.", ColorRgba.FromRgb(183, 140, 255));
                return true;
            case 0x4D when moby.Flag4B == 0xFF:
                identity = SourceObserved("Dragon pedestal", "dragon pedestal / linked rescue prop", "Magic Crafters-family source byte 0x4D no-reward records sit directly beside confirmed dragons in Alpine Ridge, Blowhard, High Caves, Magic Crafters, and Wizard Peak.", ColorRgba.FromRgb(183, 140, 255));
                return true;
            case 0x4F when moby.Flag4A == 0x10:
                identity = Observed("Dog", "dog enemy", $"Toasty user overrides repeatedly identify source byte 0x4F actor records as dogs{RewardEvidence(moby)}.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case 0x53 when moby.Flag4A == 0x10 && moby.Flag4B == 0x53:
                identity = Observed("Treasure Gnorc", "three-hit treasure gnorc enemy", "Artisans live validation repeatedly identifies the exact source byte 0x53 red-reward fingerprint as Treasure Gnorc records.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case 0x5D when moby.Flag4A == 0x10:
                identity = Observed("Shepherd", "shepherd enemy", $"Toasty user overrides repeatedly identify source byte 0x5D actor records as shepherds{RewardEvidence(moby)}.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case 0x71 when string.Equals(levelKey, "stonehill", StringComparison.OrdinalIgnoreCase):
                identity = Observed("Shepherd", "shepherd enemy", "Stone Hill validation repeatedly maps source byte 0x71 records to the Shepherd enemy family.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case 0x79 when string.Equals(levelKey, "stonehill", StringComparison.OrdinalIgnoreCase):
                identity = Observed("Ram", "ram enemy", "Stone Hill validation repeatedly maps source byte 0x79 records to the Ram enemy family.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case 0x0A:
            case 0x71:
            case 0x79:
                identity = new MobyIdentity(InferActorLabel(levelKey, moby), "enemy/fodder actor candidate", "pattern-inferred", $"Type 0x20 actor-like source byte 0x{moby.SourceByte36:X2}.", ColorRgba.FromRgb(216, 137, 42));
                return true;
            case 0x72 when (moby.Flag4A is 0x10 or 0x60) && (moby.Flag4B is 0x54 or 0x55):
                identity = Observed("Regular Gnorc", "regular gnorc enemy", $"Artisans user overrides repeatedly identify source byte 0x72 actor records as regular gnorcs; this variant preserves the same actor byte with reward byte 0x{moby.Flag4B:X2}.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case 0x73 when moby.Flag4A == 0x10:
                identity = Observed("Large Gnorc", "large gnorc enemy", "Dark Hollow user overrides repeatedly identify source byte 0x73 actor records as large gnorcs.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case 0xC2:
                identity = new MobyIdentity(RewardLabel("Flame/charge chest", moby), "flame-or-charge breakable chest", "byte-pattern", $"Validation repeatedly mapped source byte 0xC2 to flame/charge breakable chest objects{RewardEvidence(moby)}.", ColorRgba.FromRgb(230, 126, 34));
                return true;
            case 0xC3:
                identity = new MobyIdentity(RewardLabel("Charge chest", moby), "charge chest", "byte-pattern", $"Validation repeatedly mapped source byte 0xC3 to charge chest objects{RewardEvidence(moby)}.", ColorRgba.FromRgb(211, 84, 0));
                return true;
            case 0x80:
                identity = Observed("Wall lamp", "wall lamp scenery prop", "Dark Hollow user observation identifies source byte 0x80 as a wall lamp; Stone Hill validation also mapped this byte into the lamp/scenery family.", ColorRgba.FromRgb(93, 173, 226));
                return true;
            case 0x81 when moby.Flag4B == 0xFF:
                identity = Observed("Lamp post", "lamp post scenery prop", "Dark Hollow user overrides repeatedly identify source byte 0x81 scenery records as lamp posts.", ColorRgba.FromRgb(93, 173, 226));
                return true;
            case 0x50:
                identity = new MobyIdentity("Tulip flower", "single flower scenery prop", "byte-pattern", "Artisans user validation mapped source byte 0x50 to single tulip flower props.", ColorRgba.FromRgb(88, 214, 141));
                return true;
            case 0x56:
                identity = new MobyIdentity("Tulip flowers", "flower cluster scenery prop", "byte-pattern", "Artisans user validation mapped source byte 0x56 to tulip flower cluster props.", ColorRgba.FromRgb(88, 214, 141));
                return true;
            case 0x86 when moby.Flag4A is 0x10 or 0xFF && moby.Flag4B is 0x53 or 0x54 or 0x55 or 0x56 or 0x57:
                identity = Observed(RewardLabel("3x flame chest", moby), "3x flame chest", $"Dry Canyon and Town Square user overrides identify source byte 0x86 reward fingerprints as 3x flame chest records; Haunted Towers and Gnasty's Loot reuse the same source/package family for the red/purple source variants{RewardEvidence(moby)}.", ColorRgba.FromRgb(230, 126, 34));
                return true;
            case 0x91 when moby.Flag4A == 0x10 && moby.Flag4B is 0x53 or 0x54 or 0x55 or 0x56 or 0x57:
                identity = Observed(RewardLabel("Super flame chest", moby), "super flame chest", $"Peace Keepers user overrides identify this source byte 0x91 reward fingerprint as super flame chest records; Terrace Village and Haunted Towers reuse the same source/package family for the blue and purple reward variants{RewardEvidence(moby)}.", ColorRgba.FromRgb(230, 126, 34));
                return true;
            case 0x9C when moby.Flag4A == 0x10 && moby.Flag4B == 0x10:
                identity = Observed("Fodder", "fodder enemy/health animal", "Dark Hollow user overrides repeatedly identify this source byte 0x9C actor fingerprint as fodder.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case 0x9D when moby.Flag4A == 0x10 && moby.Flag4B == 0x10:
                identity = Observed("Chicken", "chicken fodder", "Town Square user overrides repeatedly identify this source byte 0x9D actor fingerprint as chickens.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case 0x9D when moby.Flag4A == 0x10 && moby.Flag4B == 0xFF:
                identity = Observed("Campfire", "campfire scenery prop", "Dark Hollow user overrides repeatedly identify source byte 0x9D no-reward records as campfires; the 0xFF variant is separated from the 0x10 chicken fodder fingerprint.", ColorRgba.FromRgb(230, 126, 34));
                return true;
            case 0xA6 when moby.Flag4A == 0x10 && moby.Flag4B == 0x55:
                identity = Observed("Big Armor Gnorc", "big armor gnorc enemy", $"Dark Hollow user overrides repeatedly identify source byte 0xA6 blue-reward actor records as Big Armor Gnorcs{RewardEvidence(moby)}.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case 0xAC:
                identity = Observed("Flag/scenery prop", "flag scenery prop", "Artisans and Stone Hill validation repeatedly map source byte 0xAC to visible flag/scenery records; two noisy observations are treated as stale test labels.", ColorRgba.FromRgb(88, 214, 141));
                return true;
            case 0x22:
                identity = PositionInferred("Flag/scenery prop", "flag scenery prop", $"Stone Hill validation mapped source byte 0x{moby.SourceByte36:X2} to flag/scenery objects, and the remaining Magic Crafters record sits on the egg-thief route beside loose gems.", ColorRgba.FromRgb(88, 214, 141));
                return true;
            case 0xAE when moby.Flag4A == 0x10 && moby.Flag4B != 0xFF:
                identity = Observed("Key Chest", "key-required chest shell", "Peace Keepers, Stone Hill, and Dark Hollow evidence identify source byte 0xAE as key-required chest records; contained gem records hold the reward colors.", ColorRgba.FromRgb(230, 126, 34));
                return true;
            case 0xB8 when moby.Flag4A == 0x10:
                identity = Observed("Big Gnorc with bird", "big gnorc with bird enemy", "Dry Canyon user overrides identify source byte 0xB8 actor records as big gnorcs with birds.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case 0xBA when moby.Flag4A == 0x10:
                identity = Observed("Gnorc with gun", "gun gnorc enemy", "Dry Canyon user overrides repeatedly identify source byte 0xBA actor records as gnorcs with guns.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case 0xBB when moby.Flag4A == 0x10 && moby.Flag4B == 0xFF:
                identity = Observed("Balloonist", "home-world balloonist", "Artisans live validation identifies the exact source byte 0xBB home-world actor fingerprint as the Balloonist.", ColorRgba.FromRgb(248, 196, 113));
                return true;
            case 0xBC when moby.Flag4A == 0x10 && moby.Flag4B == 0xFF:
                identity = Observed("Balloonist", "home-world balloonist", "Peace Keepers user evidence identifies the exact source byte 0xBC home-world actor fingerprint as the Balloonist.", ColorRgba.FromRgb(248, 196, 113));
                return true;
            case 0xBD when string.Equals(levelKey, "magiccrafters", StringComparison.OrdinalIgnoreCase) && moby.Flag4A == 0x10 && moby.Flag4B == 0xFF:
                identity = Observed("Balloonist", "home-world balloonist", "Magic Crafters user evidence identifies the exact source byte 0xBD home-world actor fingerprint as the Balloonist, matching source record T39 in the current cache.", ColorRgba.FromRgb(248, 196, 113));
                return true;
            case 0xC1 when moby.Flag4A == 0x10 && moby.Flag4B == 0x10:
                identity = Observed("Rabbit", "rabbit fodder", "Dry Canyon and Peace Keepers user overrides repeatedly identify this source byte 0xC1 actor fingerprint as rabbits.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case 0xCE:
                identity = new MobyIdentity("Short cactus", "short cactus scenery prop", "byte-pattern", "Dry Canyon user validation mapped source byte 0xCE to short cactus props.", ColorRgba.FromRgb(92, 151, 81));
                return true;
            case 0xD6 when moby.Flag4A == 0x10:
                identity = Observed("Cannon Gnorc", "cannon gnorc enemy", "Peace Keepers user overrides identify source byte 0xD6 actor records as cannon gnorcs.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case 0xD8 when moby.Flag4A == 0x10:
                identity = Observed("Spear Gnorc", "spear gnorc enemy", "Peace Keepers user overrides repeatedly identify source byte 0xD8 actor records as spear gnorcs.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case 0xDE:
            case 0xDF:
            case 0xE0:
            case 0xAE:
                identity = PositionInferred("Tree/scenery prop", "tree scenery prop", $"Tree/scenery source byte 0x{moby.SourceByte36:X2}; the remaining no-reward Dry Canyon records are a local repeated scenery cluster beside return-home and Gnorc route objects.", ColorRgba.FromRgb(63, 176, 117));
                return true;
            case 0xE6 when moby.SourceByte37 == 0x00 && moby.Flag4A == 0x10 && moby.Flag4B is 0x54 or 0x55 or 0x56 or 0x57:
                identity = Observed("Bird", "bird enemy", $"Dry Canyon user overrides repeatedly identify source byte 0xE6 reward actor records as birds{RewardEvidence(moby)}.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case 0xE6 when moby.SourceByte37 == 0x00 && moby.Flag4A is 0x10 or 0x26 && moby.Flag4B == 0xFF:
                identity = Observed("Bird", "bird enemy", "Dry Canyon user overrides identify source byte 0xE6 reward variants as birds; the no-reward 0xFF variant repeats heavily in the Beast Makers family with the same actor byte.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case 0xEF:
                identity = new MobyIdentity("Tall cactus", "tall cactus scenery prop", "byte-pattern", "Dry Canyon user validation mapped source byte 0xEF to tall cactus props.", ColorRgba.FromRgb(92, 151, 81));
                return true;
            case 0xF2:
                identity = new MobyIdentity("Stubby cactus", "stubby cactus scenery prop", "byte-pattern", "Dry Canyon user validation mapped source byte 0xF2 to stubby cactus props.", ColorRgba.FromRgb(92, 151, 81));
                return true;
            case 0xF3 when moby.Flag4B == 0xFF:
                identity = Observed("Red flag", "red flag scenery prop", "Dry Canyon user overrides repeatedly identify this source byte 0xF3 scenery fingerprint as red flags.", ColorRgba.FromRgb(220, 78, 65));
                return true;
            case 0xF5 when moby.Flag4A == 0x10 && moby.Flag4B == 0xFF:
                identity = Observed("Grass", "grass scenery prop", "Artisans user override identifies this exact source byte 0xF5 scenery fingerprint as grass.", ColorRgba.FromRgb(88, 214, 141));
                return true;
            case 0x4B:
                identity = new MobyIdentity("Dragon pedestal", "dragon pedestal / linked rescue prop", "byte-pattern", "Artisans and Stone Hill validation map source byte 0x4B to dragon pedestal records.", ColorRgba.FromRgb(183, 140, 255));
                return true;
            case 0xFA:
                identity = new MobyIdentity("Dragon", "dragon actor/model", "byte-pattern", "Artisans validation maps source byte 0xFA to the visible dragon actor/model family.", ColorRgba.FromRgb(183, 140, 255));
                return true;
            case 0xA5:
                if (moby.Flag4B == 0x0E)
                {
                    identity = new MobyIdentity("Life chest", "life chest", "byte-pattern", "Stone Hill T174 and matching source byte 0xA5 behavior byte 0x0E records are life chests, not dragon actor records.", ColorRgba.FromRgb(230, 126, 34));
                    return true;
                }

                if (moby.Flag4A == 0x10 && moby.Flag4B is 0x53 or 0x54 or 0x55 or 0x56 or 0x57)
                {
                    identity = Observed("Small Gnorc", "small gnorc enemy", $"Dark Hollow user overrides repeatedly identify source byte 0xA5 reward actor records as small gnorcs{RewardEvidence(moby)}.", ColorRgba.FromRgb(230, 91, 67));
                    return true;
                }

                identity = new MobyIdentity("Dragon scene helper", "dragon actor/pedestal/control candidate", "pattern-inferred", $"Dragon-scene source byte 0x{moby.SourceByte36:X2}; needs per-level confirmation for actor vs pedestal.", ColorRgba.FromRgb(183, 140, 255));
                return true;
        }

        if (moby.Flag4B == 0xFF)
        {
            identity = new MobyIdentity(
                "Scenery/prop object?",
                "scenery prop candidate",
                "pattern-inferred",
                $"Type 0x20 with source byte 0x{moby.SourceByte36:X2} and non-reward flag 0xFF behaves like placed scenery/prop data until live-tested.",
                ColorRgba.FromRgb(93, 173, 226));
            return true;
        }

        identity = new MobyIdentity(
            "Actor/container object?",
            "actor, container, or interactive object candidate",
            "pattern-inferred",
            $"Type 0x20 source byte 0x{moby.SourceByte36:X2}, flag4B 0x{moby.Flag4B:X2}; needs live validation for the exact model.",
            ColorRgba.FromRgb(216, 137, 42));
        return true;
    }

    private static bool IsBeastMakersRegionalProp(string levelKey, Moby moby)
    {
        if (moby.Type != 0x20 || moby.Flag4B != 0xFF)
            return false;

        string key = levelKey.ToLowerInvariant();
        return key is "beastmakers" or "terracevillage" or "mistybog" &&
            moby.SourceByte36 is 0x05 or 0x06 or 0x57 or 0xDB;
    }

    private static bool IsMagicCraftersRegionalProp(string levelKey, Moby moby)
    {
        if (moby.Type != 0x20 || moby.Flag4B != 0xFF)
            return false;

        string key = levelKey.ToLowerInvariant();
        return key is "magiccrafters" or "highcaves" or "blowhard" &&
            moby.SourceByte36 is 0x6D or 0xF4 or 0xF7 or 0xF8;
    }

    private static bool TryClassifyLevelRosterFamily(string levelKey, Moby moby, out MobyIdentity identity)
    {
        if (moby.Type != 0x20)
        {
            identity = default;
            return false;
        }

        string key = levelKey.ToLowerInvariant();
        switch (key, moby.SourceByte36)
        {
            case ("gnorccove", 0x9A) when moby.Flag4A == 0x20 && moby.Flag4B == 0xFF:
                identity = PositionInferred("Gnorc Cove structural scenery", "Gnorc Cove structural scenery prop", "This large no-reward Gnorc Cove family repeats as shared scenery around route and treasure clusters, with no reward bytes or actor behavior, so the editor treats it as structural scenery.", ColorRgba.FromRgb(93, 173, 226));
                return true;
            case ("gnorccove", 0xCA) when moby.Flag4B == 0xFF:
                identity = PositionInferred("Gnorc Cove treasure-route prop", "Gnorc Cove treasure-route prop", "This no-reward Gnorc Cove package repeatedly sits beside spring chests, charge chests, loose gems, and route treasure clusters, so the editor treats it as treasure-route scenery.", ColorRgba.FromRgb(93, 173, 226));
                return true;
            case ("gnorccove", 0xCF) when moby.Flag4B == 0xFF:
                identity = PositionInferred("Gnorc Cove chest link", "Gnorc Cove chest-linked record", "This no-reward shared-data family sits directly on or beside many known Gnorc Cove spring, charge, and super-flame chest records, so the editor treats it as chest-linked data instead of independent scenery.", ColorRgba.FromRgb(230, 126, 34));
                return true;
            case ("gnorccove", 0xC9):
                identity = PositionInferred(RewardLabel("Barrel Engineer", moby), "barrel engineer enemy", $"Gnorc Cove source byte 0xC9 records share a barrel-rider actor signature and line up with nearby 0xCA barrel support records, matching the Engineer/gnorc-on-barrel enemy lane{RewardEvidence(moby)}.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("gnorccove", 0xC8):
                identity = PositionInferred(RewardLabel("Dockworker / TNT Wrangler", moby), "dockworker or TNT Wrangler enemy", $"Gnorc Cove source byte 0xC8 covers the remaining reward-bearing barrel-lane enemies after barrel engineers, rats, and chest records are separated; the cache groups these as the Dockworker/TNT Wrangler lane{RewardEvidence(moby)}.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("loftycastle", 0x3D) when moby.Flag4B == 0xFF:
                identity = PositionInferred("Fairy cage prop", "Lofty Castle caged-fairy cage prop", "Lofty Castle source byte 0x3D has fifteen no-reward records paired one-for-one with the fifteen source byte 0x58 fairy rescue records, matching the level's repeated caged-fairy/whirlwind progression.", ColorRgba.FromRgb(93, 173, 226));
                return true;
            case ("loftycastle", 0x28) when moby.Flag4B == 0xFF:
                identity = PositionInferred("Lofty Castle route scenery", "Lofty Castle route scenery prop", "This no-reward Lofty Castle package repeats around route, chest, key, and whirlwind clusters, so the editor treats it as route scenery.", ColorRgba.FromRgb(93, 173, 226));
                return true;
            case ("loftycastle", 0x34):
                identity = RosterInferred(RewardLabel("Devil Cupid", moby), "devil cupid enemy", $"Lofty Castle source byte 0x34 is a twelve-record reward actor family, matching the level guide's twelve Devil Cupids{RewardEvidence(moby)}.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("loftycastle", 0x3E):
                identity = RosterInferred(RewardLabel("Fat Bat", moby), "fat bat enemy", $"Lofty Castle source byte 0x3E is a twelve-record reward actor family, matching the level guide's twelve Fat Bats{RewardEvidence(moby)}.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("darkpassage", 0x34):
                identity = RosterInferred(RewardLabel("Devil Cupid", moby), "devil cupid enemy", $"Dark Passage source byte 0x34 is the reward-bearing archer family along the route, matching the level guide's Devil Cupid lane{RewardEvidence(moby)}.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("hauntedtowers", 0x8E) when moby.Flag4B == 0xFF:
                identity = PositionInferred("Haunted Towers scenery prop", "Haunted Towers scenery prop", "This no-reward Haunted Towers package repeats around chest, gem, and tower-route scenery clusters, so the editor treats it as scenery rather than an actor.", ColorRgba.FromRgb(93, 173, 226));
                return true;
            case ("jacques", 0x2D):
                identity = RosterInferred(RewardLabel("Metal Claw Monster", moby), "metal claw monster enemy", $"Jacques source byte 0x2D has seven reward actors, matching the level guide's seven Metal Claw Monsters{RewardEvidence(moby)}.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("jacques", 0x2E):
                identity = RosterInferred(RewardLabel("Fat Claw Monster", moby), "fat claw monster enemy", $"Jacques source byte 0x2E has eleven reward actors, matching the level guide's eleven Fat Claw Monsters{RewardEvidence(moby)}.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("jacques", 0x7E) when moby.Flag4B == 0xFF:
                identity = PositionInferred("Jacques scenery prop", "Jacques scenery prop", "This no-reward Jacques shared-data family repeats along gem, chest, whirlwind, and boss-route areas, so the editor treats it as scenery instead of an unidentified actor.", ColorRgba.FromRgb(93, 173, 226));
                return true;
            case ("mistybog", 0x05) when moby.Flag4B == 0xFF:
                identity = PositionInferred("Misty Bog scenery prop", "Misty Bog scenery prop", "This large no-reward Misty Bog family repeats around birds, dragons, chests, and route clusters, so the editor treats it as local scenery rather than an unidentified actor.", ColorRgba.FromRgb(93, 173, 226));
                return true;
            case ("blowhard", 0xF7) when moby.Flag4B == 0xFF:
                identity = PositionInferred("Blowhard route scenery", "Blowhard route scenery prop", "This no-reward Blowhard package repeats around cave route and chest clusters, so the editor treats it as route scenery.", ColorRgba.FromRgb(93, 173, 226));
                return true;
            case ("doctorshemp", 0xCD):
                identity = RosterInferred("Fat Momma/Cauldron", "fat momma cauldron encounter record", "Doctor Shemp has twelve same-package 0xCD records in six close pairs; the raw package points at the 0x74 Kamikaze package and matches the Enemy/Boss FAQ's six Fat Ladies in the first section.", ColorRgba.FromRgb(230, 126, 34));
                return true;
            case ("gnastysloot", 0x92) when moby.Flag4B == 0xFF:
                identity = PositionInferred("Loot route scenery prop", "Gnasty's Loot route scenery prop", "This no-reward Gnasty's Loot source family sits in the thief/key chase lane, so the editor treats it as route scenery rather than unrelated actor data.", ColorRgba.FromRgb(248, 196, 113));
                return true;
            case ("gnastysworld", 0xB8) when moby.Flag4B == 0xFF:
            case ("gnastysworld", 0xBD) when moby.Flag4B == 0xFF:
            case ("gnastysworld", 0xBC) when moby.Flag4B == 0xFF:
            case ("gnastysworld", 0xAA) when moby.Flag4B == 0xFF:
            case ("gnastysworld", 0xB0) when moby.Flag4B == 0xFF:
            case ("gnastysworld", 0xB5) when moby.Flag4B == 0xFF:
            case ("gnastysworld", 0xB7) when moby.Flag4B == 0xFF:
            case ("gnastysworld", 0xC0) when moby.Flag4B == 0xFF:
                identity = PositionInferred("Gnasty's World scenery prop", "Gnasty's World scenery prop", "These no-reward Gnasty's World packages sit in local scenery, portal, dragon, and treasure clusters, so the editor treats them as scenery rather than actor records.", ColorRgba.FromRgb(93, 173, 226));
                return true;
            case ("drycanyon", 0xB7) when moby.Flag4B == 0xFF:
            case ("drycanyon", 0xB8) when moby.Flag4B == 0xFF:
            case ("drycanyon", 0xBB) when moby.Flag4B == 0xFF:
            case ("drycanyon", 0xBD) when moby.Flag4B == 0xFF:
            case ("drycanyon", 0xB1) when moby.Flag4B == 0xFF:
            case ("drycanyon", 0xB6) when moby.Flag4B == 0xFF:
            case ("drycanyon", 0xBE) when moby.Flag4B == 0xFF:
                identity = PositionInferred("Dry Canyon scenery prop", "Dry Canyon scenery prop", "These no-reward Dry Canyon packages sit around local scenery, route, return-home, and treasure clusters, so the editor treats them as scenery rather than actors.", ColorRgba.FromRgb(93, 173, 226));
                return true;
            case ("alpineridge", 0x46):
                identity = RosterInferred(RewardLabel("Elder Wizard", moby), "elder wizard enemy", $"Alpine Ridge's compact source byte 0x46 family has four same-package reward actors, matching the level roster's Elder Wizard lane{RewardEvidence(moby)}.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("alpineridge", 0x0E):
                identity = RosterInferred(RewardLabel("Green Druid", moby), "green druid enemy", $"Alpine Ridge source byte 0x0E shares the Magic Crafters-region green druid actor family and sits in the level's platform/route druid encounters{RewardEvidence(moby)}.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("alpineridge", 0x21):
            case ("alpineridge", 0xFD):
                identity = ReviewInferred(RewardLabel("Alpine Ridge reward link", moby), "Alpine Ridge reward/control link", $"Release review groups this shared-data family with Alpine Ridge reward and route records{RewardEvidence(moby)}; exact linked host still needs a focused move test.", ColorRgba.FromRgb(216, 137, 42));
                return true;
            case ("alpineridge", 0xF4) when moby.Flag4B == 0xFF:
                identity = PositionInferred("Alpine Ridge scenery prop", "Alpine Ridge high-route scenery prop", "This no-reward Alpine Ridge package repeats around high-route scenery and treasure clusters, so the editor treats it as scenery rather than an unidentified actor.", ColorRgba.FromRgb(93, 173, 226));
                return true;
            case ("beastmakers", 0xBE) when moby.Flag4B == 0xFF:
                identity = PositionInferred("Beast Makers scenery prop", "Beast Makers scenery prop", "This no-reward Beast Makers package sits with local scenery and treasure clusters, so the editor treats it as scenery rather than an actor.", ColorRgba.FromRgb(93, 173, 226));
                return true;
            case ("blowhard", 0xF4) when moby.Flag4B == 0xFF:
            case ("blowhard", 0xF8) when moby.Flag4B == 0xFF:
                identity = PositionInferred("Blowhard scenery prop", "Blowhard scenery prop", "These no-reward Blowhard packages sit with route, chest, and return-home clusters, so the editor treats them as scenery rather than actors.", ColorRgba.FromRgb(93, 173, 226));
                return true;
            case ("dreamweavers", 0x40) when moby.Flag4B == 0xFF:
            case ("dreamweavers", 0xBF) when moby.Flag4B == 0xFF:
                identity = PositionInferred("Dream Weavers scenery prop", "Dream Weavers scenery prop", "These no-reward Dream Weavers packages sit with portal, dragon, gem, and route clusters, so the editor treats them as scenery rather than actors.", ColorRgba.FromRgb(93, 173, 226));
                return true;
            case ("dreamweavers", 0x5C):
                identity = RosterInferred(RewardLabel("Winged Fool", moby), "winged fool enemy", $"Dream Weavers source byte 0x5C is a ten-record reward family, matching the homeworld guide's ten Winged Guys/Fools{RewardEvidence(moby)}.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("dreamweavers", 0x82):
                identity = RosterInferred(RewardLabel("Armored Fool", moby), "armored fool enemy", $"Dream Weavers source byte 0x82 is an eight-record reward family, matching the homeworld guide's eight Armored Guys/Fools{RewardEvidence(moby)}.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("gnorccove", 0x9A) when moby.Flag4B == 0xFF:
                identity = PositionInferred("Gnorc Cove structural scenery", "Gnorc Cove structural scenery prop", "This no-reward Gnorc Cove family repeats as shared scenery around route and treasure clusters, with no reward bytes or actor behavior, so the editor treats it as structural scenery.", ColorRgba.FromRgb(93, 173, 226));
                return true;
            case ("hauntedtowers", 0xF1) when moby.Flag4B == 0xFF:
                identity = PositionInferred("Haunted Towers scenery prop", "Haunted Towers scenery prop", "This no-reward Haunted Towers package sits in local route and treasure scenery clusters, so the editor treats it as scenery rather than an actor.", ColorRgba.FromRgb(93, 173, 226));
                return true;
            case ("highcaves", 0xE3) when moby.Flag4B == 0xFF:
                identity = PositionInferred("High Caves route scenery", "High Caves route scenery prop", "This no-reward High Caves package sits with wizard, spider, fairy-helper, and route clusters, so the editor treats it as route scenery.", ColorRgba.FromRgb(93, 173, 226));
                return true;
            case ("jacques", 0x96) when moby.Flag4B == 0xFF:
                identity = PositionInferred("Jacques route scenery", "Jacques route scenery prop", "This no-reward Jacques package sits with route and treasure clusters, so the editor treats it as route scenery rather than an actor.", ColorRgba.FromRgb(93, 173, 226));
                return true;
            case ("magiccrafters", 0xF8) when moby.Flag4B == 0xFF:
                identity = PositionInferred("Magic Crafters scenery prop", "Magic Crafters scenery prop", "This no-reward Magic Crafters package sits with portal, chest, key, and route clusters, so the editor treats it as scenery rather than an actor.", ColorRgba.FromRgb(93, 173, 226));
                return true;
            case ("magiccrafters", 0xBD) when moby.Flag4B == 0xFF:
                identity = PositionInferred("Magic Crafters scenery prop", "Magic Crafters scenery prop", "This no-reward Magic Crafters package sits with local route and treasure clusters, so the editor treats it as scenery rather than an actor.", ColorRgba.FromRgb(93, 173, 226));
                return true;
            case ("mistybog", 0xE4) when moby.Flag4B == 0xFF:
            case ("mistybog", 0xE7):
                identity = PositionInferred("Misty Bog route scenery", "Misty Bog route scenery prop", "These Misty Bog records sit with route and reward clusters and do not carry independent reward behavior, so the editor treats them as route scenery.", ColorRgba.FromRgb(93, 173, 226));
                return true;
            case ("terracevillage", 0xD2) when moby.Type != 0x20 || moby.Flag4B != 0x10:
                identity = ReviewInferred("Terrace Village linked family", "Terrace Village interactive linked family", "Release review groups this special-data family with Terrace Village floor/laser encounter records; exact role still needs a focused move test.", ColorRgba.FromRgb(216, 137, 42));
                return true;
            case ("twilightharbor", 0xAB) when moby.Flag4B == 0xFF:
                identity = PositionInferred("Twilight Harbor scenery prop", "Twilight Harbor scenery prop", "This no-reward Twilight Harbor package sits with route and level scenery clusters, so the editor treats it as scenery rather than an actor.", ColorRgba.FromRgb(93, 173, 226));
                return true;
            case ("twilightharbor", 0x91):
                identity = ReviewInferred(RewardLabel("Auto-gunner Gnorc link", moby), "auto-gunner gnorc linked record", $"Release review groups this source byte 0x91 linked record with Twilight Harbor's Auto-gunner Gnorc clusters{RewardEvidence(moby)}; exact role still needs a focused move test.", ColorRgba.FromRgb(216, 137, 42));
                return true;
            case ("toasty", 0x3A):
                identity = ReviewInferred(RewardLabel("Toasty encounter linked family", moby), "Toasty encounter linked family", $"Release review groups this shared-data family with Toasty's dog, shepherd, and boss encounter cluster{RewardEvidence(moby)}; exact role still needs a focused move test.", ColorRgba.FromRgb(216, 137, 42));
                return true;
            case ("wizardpeak", 0x42):
                identity = ReviewInferred(RewardLabel("Wizard Peak actor/reward family", moby), "Wizard Peak actor/reward family", $"Release review groups this shared-data family with Wizard Peak reward and route clusters{RewardEvidence(moby)}; exact role still needs a focused move test.", ColorRgba.FromRgb(216, 137, 42));
                return true;
            case ("gnastygnorc", 0x9F):
            case ("gnastygnorc", 0xB5):
                identity = ReviewInferred("Boss route control link", "final boss route control marker", "Release review groups this source family with Gnasty Gnorc boss, thief, and guard route clusters; exact role still needs a focused move test.", ColorRgba.FromRgb(216, 137, 42));
                return true;
            case ("townsquare", 0x8B) when moby.Flag4A == 0x10:
                identity = Observed("Torro Gnorc", "torro gnorc enemy", $"Town Square user overrides identify source byte 0x8B actor records as Torro Gnorcs{RewardEvidence(moby)}.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("clifftown", 0xEE) when moby.Flag4B == 0x10:
                identity = RosterInferred("Frilled lizard", "frilled lizard fodder", "Cliff Town's no-reward source byte 0xEE family matches the Peace Keepers fodder roster entry for Cliff Town.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("clifftown", 0xE2):
                identity = RosterInferred("Wavy-shield Gnorc", "shielded gnorc enemy", "Cliff Town has one ten-record reward actor family at source byte 0xE2, matching the guide count for the level's ten wavy/silver-shield gnorcs.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("clifftown", 0xDA):
                identity = RosterInferred("Fat Momma/Cauldron", "fat momma cauldron support record", "Cliff Town has seven 0xDA package records sharing raw special-data signature f622c76b11983457; that package points at the 0xE2 Pueblo family and pairs spatially with the seven Fat Lady/cauldron encounters listed by the Enemy/Boss FAQ.", ColorRgba.FromRgb(230, 126, 34));
                return true;
            case ("clifftown", 0xE5):
                identity = RosterInferred("Fat Momma/Cauldron", "fat momma cauldron reward record", "Cliff Town has seven 0xE5 reward/package records sharing raw special-data signature 6da6f89aef7aebc7; they pair with the 0xDA support package and match the seven Fat Lady/cauldron encounters listed by the Enemy/Boss FAQ.", ColorRgba.FromRgb(230, 126, 34));
                return true;
            case ("magiccrafters", 0x24) when moby.Flag4B == 0x10:
            case ("alpineridge", 0x24) when moby.Flag4B == 0x10:
            case ("highcaves", 0x24) when moby.Flag4B == 0x10:
            case ("wizardpeak", 0x24) when moby.Flag4B == 0x10:
                identity = RosterInferred("Goat sheep", "Magic Crafters fodder", "The no-reward source byte 0x24 family appears across Magic Crafters and its realms, matching the regional goat-sheep fodder roster.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("magiccrafters", 0x0F):
                identity = RosterInferred("Armored Druid", "armored druid enemy", "Magic Crafters has one fourteen-record source byte 0x0F actor family matching the enemy guide's fourteen shielded/armored druids.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("magiccrafters", 0x0E):
                identity = RosterInferred("Green Druid", "green druid enemy", "Magic Crafters has one six-record source byte 0x0E actor family matching the enemy guide's six chanting green druids.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("magiccrafters", 0x1B):
                identity = RosterInferred("Green Wizard", "green wizard enemy", "Magic Crafters has one four-record source byte 0x1B actor family matching the enemy guide's four green wizards.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("alpineridge", 0x0F):
                identity = RosterInferred("Beast", "Alpine Ridge beast enemy", "Alpine Ridge has one ten-record source byte 0x0F actor family matching the enemy guide's ten Beasts.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("highcaves", 0x2D):
                identity = RosterInferred("Elder Wizard", "elder wizard enemy", "High Caves has one four-record source byte 0x2D actor family matching the level roster's four elder wizards.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("highcaves", 0x2F):
                identity = RosterInferred("Tornado Wizard", "tornado wizard enemy", "High Caves has one three-record source byte 0x2F actor family matching the level roster's three tornado wizards.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("highcaves", 0x2E):
                identity = RosterInferred("Green Druid", "green druid enemy", "High Caves has one four-record source byte 0x2E actor family matching the level roster's green druid enemy lane.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("highcaves", 0x31):
                identity = RosterInferred("Metalback Spider", "metalback spider enemy", "High Caves has one five-record source byte 0x31 actor family matching the level roster's five metalback spiders.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("wizardpeak", 0x1B):
                identity = RosterInferred("Green Wizard", "green wizard enemy", "Wizard Peak has one six-record source byte 0x1B actor family matching the enemy guide's six green wizards.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("wizardpeak", 0x1C):
                identity = RosterInferred("Armored Druid", "armored druid enemy", "Wizard Peak has one six-record source byte 0x1C actor family matching the enemy guide's six shielded/armored druids.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("wizardpeak", 0x1D):
                identity = RosterInferred("Elder Wizard", "elder wizard enemy", "Wizard Peak has one ten-record source byte 0x1D actor family matching the enemy guide's ten elder wizards.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("blowhard", 0xF1):
                identity = RosterInferred("Green Druid", "green druid enemy", "Blowhard has one four-record source byte 0xF1 actor family matching the enemy guide's four platform-moving green druids inside the cave.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("blowhard", 0x1B):
                identity = RosterInferred("Green Wizard", "green wizard enemy", "Blowhard has seven native actor 0x011B Green Wizard rows (T0-T6). This native family is the preferred same-level donor for replacement and per-instance properties/route work in Blowhard.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("beastmakers", 0xD2) when moby.Flag4B == 0x10:
            case ("terracevillage", 0xD2) when moby.Flag4B == 0x10:
            case ("mistybog", 0xD2) when moby.Flag4B == 0x10:
            case ("treetops", 0xD2) when moby.Flag4B == 0x10:
            case ("metalhead", 0xD2) when moby.Flag4B == 0x10:
            case ("treetops", 0xC5) when moby.Flag4B == 0x10:
                identity = RosterInferred("Spotted chicken", "Beast Makers fodder", "This no-reward Beast Makers actor family matches the regional spotted-chicken fodder roster.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("beastmakers", 0xCD):
                identity = RosterInferred("Electro Gnorc", "electro gnorc enemy", "Beast Makers has one five-record source byte 0xCD actor family matching the enemy guide's five home-world Electro Gnorcs.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("beastmakers", 0x89):
                identity = RosterInferred("Giant Boar", "giant boar enemy", "Beast Makers has one four-record source byte 0x89 actor family matching the enemy guide's four giant boars.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("icecavern", 0xC6):
                identity = RosterInferred("Snow Gnorc", "snow gnorc enemy", "Ice Cavern has one ten-record source byte 0xC6 actor family matching the enemy guide's ten large purple Snow Gnorcs.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("icecavern", 0xE7):
                identity = RosterInferred("Armored Gnorc", "armored gnorc enemy", "Ice Cavern has one four-record source byte 0xE7 actor family matching the enemy guide's four large shielded Armored Gnorcs.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("icecavern", 0xD3):
                identity = RosterInferred("Snowballer", "snowballer gnorc enemy", "Ice Cavern has one six-record source byte 0xD3 actor family matching the level guide's six Snowballers.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("icecavern", 0xD4):
                identity = RosterInferred("Agile Snowballer", "agile snowballer gnorc enemy", "Ice Cavern has one three-record source byte 0xD4 actor family matching the level guide's three Agile Snowballers.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("dreamweavers", 0xA0) when moby.Flag4B == 0x10:
            case ("darkpassage", 0xA0) when moby.Flag4B == 0x10:
            case ("loftycastle", 0xA0) when moby.Flag4B == 0x10:
            case ("hauntedtowers", 0xA0) when moby.Flag4B == 0x10:
            case ("jacques", 0xA0) when moby.Flag4B == 0x10:
                identity = RosterInferred("Mushroom", "Dream Weavers mushroom fodder", "The no-reward source byte 0xA0 family appears throughout Dream Weavers and its realms, matching the regional mushroom fodder roster.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("gnastysworld", 0xEC) when moby.Flag4B == 0x10:
            case ("gnorccove", 0xEC) when moby.Flag4B == 0x10:
            case ("twilightharbor", 0xEC) when moby.Flag4B == 0x10:
                identity = RosterInferred("Rat", "Gnorc Gnexus rat fodder", "The no-reward source byte 0xEC family matches the Gnorc Gnexus/Gnorc Cove/Twilight Harbor rat fodder roster.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("gnastysloot", 0xB3):
                identity = RosterInferred("Plane thief", "key-carrying plane thief", "Gnasty's Loot source table has two source byte 0xB3 thief-behavior records at the key chase starts; walkthrough evidence describes keys held by thieves in this level.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("gnastysloot", 0xB5):
                identity = RosterInferred("Plane thief key-route record", "plane thief key-route record", "Gnasty's Loot has four source byte 0xB5 records in the thief/key route cluster; they are kept separate from generic scenery because this level's progression is built around thief-held keys.", ColorRgba.FromRgb(248, 196, 113));
                return true;
            case ("gnastysloot", 0xEC) when moby.Flag4B == 0x10:
                identity = RosterInferred("Plane thief", "key-carrying plane thief", "Gnasty's Loot reuses the Gnorc Gnexus source byte 0xEC actor family for three key/treasure-route thieves; unlike the Gnexus rat uses, these records sit in the bonus level's thief chase lanes.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("mistybog", 0x89):
                identity = RosterInferred("Boar", "boar enemy", "Misty Bog has one compact five-record actor family matching the level's boar roster entry.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("mistybog", 0x93):
                identity = RosterInferred("Attack frog", "attack frog enemy", "Misty Bog's 0x93 actor family is a repeated reward-dropping family that matches the level's attack frog roster lane.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("mistybog", 0xDC):
                identity = RosterInferred("Dragon-eating plant", "dragon-eating plant enemy", "Misty Bog has a nine-record actor family at source byte 0xDC, matching the level's repeated dragon-eating plant encounters.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("mistybog", 0xD2):
                identity = RosterInferred("Chicken", "chicken fodder", "Misty Bog's only small no-reward type 0x20 actor family is the level's chicken fodder lane.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("doctorshemp", 0x74):
                identity = RosterInferred("Kamikaze tribesman", "blind runner / kamikaze tribesman enemy", "Doctor Shemp has one seven-record source byte 0x74 actor family matching the Enemy/Boss FAQ's seven Kamikaze warriors in this boss level.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("terracevillage", 0xCD):
                identity = RosterInferred("Floor Shocker", "floor shocker enemy", "Terrace Village has seven visible type 0x20 source byte 0xCD actors plus two same-pointer special siblings, matching the level guide's nine Floor Shockers.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("terracevillage", 0xD3):
                identity = RosterInferred("Laser Gnorc", "laser gnorc enemy", "Terrace Village has eleven visible type 0x20 source byte 0xD3 actors plus two same-pointer special siblings, matching the level guide's thirteen Laser Gnorcs.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("terracevillage", 0x23):
                identity = RosterInferred("Volt Shooter", "volt shooter enemy", "Terrace Village has one sixteen-record source byte 0x23 actor family matching the level guide's sixteen Volt Shooters.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("darkpassage", 0x40) when moby.Flag4B == 0xFF:
                identity = SourceObserved("Lamp post", "lamp post scenery prop", "Dark Passage source byte 0x40 no-reward records share source-disc special signature 1f719458baca40f7 with the exact T82 Lamp post record.", ColorRgba.FromRgb(93, 173, 226));
                return true;
            case ("gnorccove", 0xC7) when moby.Flag4A == 0x10 && moby.Flag4B == 0x53:
                identity = SourceObserved(RewardLabel("Spring chest", moby), "spring chest", "Gnorc Cove source byte 0xC7 red-reward records share source-disc special signature de47c9b27eb8d300 with exact spring chest records T181/T182.", ColorRgba.FromRgb(230, 126, 34));
                return true;
            case ("treetops", 0x64):
                identity = RosterInferred("Banana boy", "banana boy enemy", "Tree Tops has an eleven-record reward actor family at source byte 0x64, matching the level's banana boy roster count.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("treetops", 0x65):
                identity = RosterInferred("Strongarm", "strongarm enemy", "Tree Tops has a thirteen-record reward actor family at source byte 0x65, matching the level's strongarm patrol family including state variants.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("treetops", 0x2A) when moby.Flag4B == 0xFF:
                identity = SourceObserved("Bird", "bird enemy", "Tree Tops source byte 0x2A no-reward records share source-disc special signature 7f3c53122e06f2b2 with 48 exact Tree Tops Bird records.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("darkpassage", 0x7A) when moby.Flag4B == 0xFF:
                identity = RosterInferred("Lamp Fool", "lamp fool control enemy", "Dark Passage has one nine-record no-reward source byte 0x7A family matching the level guide's nine Fools that control the light/dark enemy state.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("loftycastle", 0x2A):
                identity = RosterInferred("Balloognorc", "balloon gnorc enemy", "Lofty Castle has eight visible type 0x20 source byte 0x2A actors plus one same-pointer special sibling, matching the level guide's nine Balloognorcs.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("hauntedtowers", 0xCC):
                identity = RosterInferred("Summoning Wizard", "summoning wizard enemy", "Haunted Towers has one six-record source byte 0xCC actor family matching the level guide's six Summoning Wizards.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            default:
                identity = default;
                return false;
        }
    }

    private static bool TryClassifyType30(string levelKey, Moby moby, out MobyIdentity identity)
    {
        if (string.Equals(levelKey, "alpineridge", StringComparison.OrdinalIgnoreCase) && moby.SourceByte36 == 0x46)
        {
            identity = RosterInferred(
                RewardLabel("Elder Wizard", moby),
                "elder wizard enemy",
                $"Alpine Ridge type 0x30/source 0x46 is the fourth member of the level's same-package Elder Wizard family{RewardEvidence(moby)}.",
                ColorRgba.FromRgb(230, 91, 67));
            return true;
        }

        if (string.Equals(levelKey, "alpineridge", StringComparison.OrdinalIgnoreCase) && moby.SourceByte36 == 0x0E)
        {
            identity = PositionInferred(
                RewardLabel("Moving platform marker", moby),
                "Alpine Ridge moving platform/control marker",
                $"This type 0x30/source 0x0E marker shares the Alpine Ridge green-druid package and sits on the same route-control family{RewardEvidence(moby)}.",
                ColorRgba.FromRgb(143, 166, 184));
            return true;
        }

        if (string.Equals(levelKey, "darkpassage", StringComparison.OrdinalIgnoreCase) && moby.SourceByte36 == 0x91)
        {
            identity = ReviewInferred(
                RewardLabel("Dark Passage transform control link", moby),
                "Dark Passage transform enemy control marker",
                $"Release review groups this type 0x30/source 0x91 record with Dark Passage actor/control clusters{RewardEvidence(moby)}; exact role still needs a focused move test.",
                ColorRgba.FromRgb(216, 137, 42));
            return true;
        }

        if (string.Equals(levelKey, "doctorshemp", StringComparison.OrdinalIgnoreCase) && moby.SourceByte36 == 0x73)
        {
            identity = ReviewInferred(
                RewardLabel("Doctor Shemp encounter control", moby),
                "Doctor Shemp encounter control marker",
                $"Release review groups this type 0x30/source 0x73 record with Doctor Shemp boss-level control clusters{RewardEvidence(moby)}; exact role still needs a focused move test.",
                ColorRgba.FromRgb(216, 137, 42));
            return true;
        }

        if (string.Equals(levelKey, "treetops", StringComparison.OrdinalIgnoreCase) && moby.SourceByte36 == 0x25)
        {
            identity = ReviewInferred(
                RewardLabel("Tree Tops large route family", moby),
                "Tree Tops large route/reward family",
                $"Release review groups this type 0x30/source 0x25 record with Tree Tops route and reward clusters{RewardEvidence(moby)}; exact role still needs a focused move test.",
                ColorRgba.FromRgb(216, 137, 42));
            return true;
        }

        if (string.Equals(levelKey, "twilightharbor", StringComparison.OrdinalIgnoreCase) && moby.SourceByte36 == 0x91)
        {
            identity = RosterInferred("Auto-gunner Gnorc", "auto-gunner gnorc enemy", "Twilight Harbor has one thirteen-record type 0x30 source byte 0x91 family, matching the level guide's thirteen Auto-gunner Gnorcs.", ColorRgba.FromRgb(230, 91, 67));
            return true;
        }

        if (string.Equals(levelKey, "twilightharbor", StringComparison.OrdinalIgnoreCase) && moby.SourceByte36 == 0x93)
        {
            identity = RosterInferred("Gnorc Commando", "gnorc commando enemy", "Twilight Harbor has nine type 0x30 source byte 0x93 records plus one same-pointer type 0x10 sibling, matching the level guide's ten Gnorc Commandos.", ColorRgba.FromRgb(230, 91, 67));
            return true;
        }

        if (string.Equals(levelKey, "twilightharbor", StringComparison.OrdinalIgnoreCase) && moby.SourceByte36 == 0x94)
        {
            identity = RosterInferred("Super Grenadier", "super grenadier enemy", "Twilight Harbor has three type 0x30 source byte 0x94 records plus one same-pointer type 0x10 sibling, matching the level guide's four Super Grenadiers.", ColorRgba.FromRgb(230, 91, 67));
            return true;
        }

        if (moby.SourceByte36 == 0x62)
        {
            identity = new MobyIdentity(
                "Skinny tree",
                "skinny tree scenery prop",
                "byte-pattern",
                "Artisans live validation mapped type 0x30 source byte 0x62 to skinny tree props.",
                ColorRgba.FromRgb(63, 176, 117));
            return true;
        }

        if (moby.SourceByte36 == 0x63)
        {
            identity = new MobyIdentity(
                "Tall 2-ball tree",
                "tall two-ball tree scenery prop",
                "byte-pattern",
                "Artisans live validation mapped type 0x30 source byte 0x63 to tall two-ball tree props.",
                ColorRgba.FromRgb(63, 176, 117));
            return true;
        }

        if (moby.SourceByte36 == 0x64)
        {
            identity = new MobyIdentity(
                "Wider tree",
                "wider tree scenery prop",
                "byte-pattern",
                "Artisans live validation mapped type 0x30 source byte 0x64 to wider tree props.",
                ColorRgba.FromRgb(63, 176, 117));
            return true;
        }

        if (moby.SourceByte36 is 0xDE or 0xDF or 0xE0 or 0xAE)
        {
            identity = new MobyIdentity(
                "Tree/scenery prop",
                "tree scenery prop candidate",
                "pattern-inferred",
                $"Type 0x30 scenery source byte 0x{moby.SourceByte36:X2}.",
                ColorRgba.FromRgb(63, 176, 117));
            return true;
        }

        identity = new MobyIdentity(
            "Scenery/large object?",
            "large scenery or special object candidate",
            "pattern-inferred",
            $"Type 0x30 with source byte 0x{moby.SourceByte36:X2}; known to include trees and some special objects.",
            ColorRgba.FromRgb(93, 173, 226));
        return true;
    }

    private static bool TryClassifyControl(string levelKey, Moby moby, out MobyIdentity identity)
    {
        if (string.Equals(levelKey, "gnastysloot", StringComparison.OrdinalIgnoreCase) &&
            moby.SourceByte36 == 0xB9 &&
            moby.Flag4A == 0x10 &&
            moby.Flag4B == 0xFF)
        {
            identity = new MobyIdentity(
                "Plane thief route marker",
                "plane thief route/control marker",
                "roster-inferred",
                "Gnasty's Loot source table places source byte 0xB9 control records along the same progression route as the key-carrying plane thieves.",
                ColorRgba.FromRgb(143, 166, 184));
            return true;
        }

        if (string.Equals(levelKey, "artisans", StringComparison.OrdinalIgnoreCase) &&
            moby.SourceByte36 == 0x01 &&
            moby.Flag4A == 0x10 &&
            moby.Flag4B == 0xFF)
        {
            identity = new MobyIdentity(
                "Portal level name sign",
                "home-world portal level-name sign",
                "byte-pattern",
                "Artisans T144 is user-observed as the Stone Hill level-name sign; T145-T148 use the same type 0x00/source byte 0x01 home-world portal sign family.",
                ColorRgba.FromRgb(244, 208, 63));
            return true;
        }

        if (moby.SourceByte36 == 0x01 && moby.Flag4A == 0x10 && moby.Flag4B == 0xFF)
        {
            identity = new MobyIdentity(
                "Portal destination marker",
                "home-world portal label/destination marker",
                "byte-pattern",
                "Home-world caches place source byte 0x01 records at portal entrances; Artisans user validation identified T144 as the Stone Hill level-name marker.",
                ColorRgba.FromRgb(73, 192, 211));
            return true;
        }

        if (moby.SourceByte36 == 0x8E && moby.Flag4A == 0x10 && moby.Flag4B == 0xFF)
        {
            identity = new MobyIdentity(
                "Portal travel path marker",
                "home-world portal travel-path marker",
                "source-proven",
                "Native homeworld portal records point to source byte 0x8E mobys as the travel path used when Spyro enters a portal; the actual walk-in trigger is a separate type-6 collision surface.",
                ColorRgba.FromRgb(73, 192, 211));
            return true;
        }

        if (moby.SourceByte36 == 0x2C && moby.Flag4A == 0x10 && moby.Flag4B == 0xFF)
        {
            identity = new MobyIdentity(
                "Whirlwind",
                "whirlwind transport marker",
                "observed-fingerprint",
                "Artisans user evidence identifies the shared type 0x00/source 0x2C/0x10/0xFF fingerprint as a whirlwind; Stone Hill uses the same fingerprint at its whirlwind location.",
                ColorRgba.FromRgb(72, 201, 176));
            return true;
        }

        if (moby.SourceByte36 == 0x2C)
        {
            identity = new MobyIdentity(
                "Whirlwind",
                "whirlwind transport marker",
                "byte-pattern",
                "Every cached type 0x00 source byte 0x2C record belongs to the whirlwind family; flag variants are kept visible as whirlwinds rather than generic nonvisual controls.",
                ColorRgba.FromRgb(72, 201, 176));
            return true;
        }

        if (moby.SourceByte36 == 0x6E && moby.Flag4A == 0x10 && moby.Flag4B == 0xFF)
        {
            identity = new MobyIdentity(
                "Dragon scene link control",
                "dragon/pedestal scene link control",
                "native-scene-family",
                "All 79 source byte 0x6E type 0x00 records pair one-to-one with native dragon actors and pedestals. Separate linked data stores the approach camera and later cinematic camera track.",
                ColorRgba.FromRgb(183, 140, 255));
            return true;
        }

        if (moby.SourceByte36 == 0x43 && moby.Flag4A == 0xFF && moby.Flag4B == 0xFF)
        {
            identity = new MobyIdentity(
                "Sparx/player anchor companion",
                "Sparx/player anchor companion control",
                "byte-pattern",
                "Full-cache sweep shows source byte 0x43 type 0x00 records as single non-rendered anchor rows with no reward bytes across many levels; when present with source byte 0x78, the two rows usually sit as a tight pair.",
                ColorRgba.FromRgb(143, 166, 184));
            return true;
        }

        if (moby.SourceByte36 == 0x0D && moby.Flag4A == 0x20 && GemValue.TryFromIdByte(moby.Flag4B, out _))
        {
            identity = new MobyIdentity(
                RewardLabel("Encounter reward control marker", moby),
                "encounter reward control marker",
                "byte-pattern",
                "Full-cache sweep separates source byte 0x0D flag 0x20 records from contained gems; these non-rendered rows sit with boss, route, bird, torch, or reward clusters and carry reward-value bytes.",
                ColorRgba.FromRgb(143, 166, 184));
            return true;
        }

        if (moby.SourceByte36 is 0x0D)
        {
            identity = new MobyIdentity("Nonvisual control marker", "nonvisual control or placeholder", "pattern-inferred", $"Nonvisual/control source byte 0x{moby.SourceByte36:X2}.", ColorRgba.FromRgb(143, 166, 184));
            return true;
        }

        if (IsFlightCourseControl(levelKey, moby))
        {
            identity = ReviewInferred(
                "Flight course control marker",
                "flight course route/control marker",
                $"Release review groups this level's repeated source byte 0x{moby.SourceByte36:X2} type 0x00 records as flight-course control markers; no visible model is expected.",
                ColorRgba.FromRgb(143, 166, 184));
            return true;
        }

        if (IsFlightLevel(levelKey) && moby.SourceByte36 == 0x05)
        {
            identity = ReviewInferred(
                "Flight support control marker",
                "flight course route/control marker",
                "Release review groups this flight-level type 0x00/source 0x05 record with the course route/control setup; no visible model is expected.",
                ColorRgba.FromRgb(143, 166, 184));
            return true;
        }

        if (string.Equals(levelKey, "peacekeepers", StringComparison.OrdinalIgnoreCase) && moby.SourceByte36 == 0xA1)
        {
            identity = ReviewInferred(
                "Peace Keepers scene control marker",
                "Peace Keepers scene/control marker",
                "Release review groups source byte 0xA1 control rows with nearby spear-gnorc, chest, and portal clusters; no visible model is expected until a linked behavior test proves otherwise.",
                ColorRgba.FromRgb(143, 166, 184));
            return true;
        }

        if (string.Equals(levelKey, "toasty", StringComparison.OrdinalIgnoreCase) && moby.SourceByte36 is 0x3A or 0x5F)
        {
            identity = ReviewInferred(
                "Toasty encounter control marker",
                "Toasty encounter/control marker",
                $"Release review groups source byte 0x{moby.SourceByte36:X2} control rows with the Toasty dog, shepherd, and boss encounter cluster; no visible model is expected until a linked behavior test proves otherwise.",
                ColorRgba.FromRgb(143, 166, 184));
            return true;
        }

        if (string.Equals(levelKey, "artisans", StringComparison.OrdinalIgnoreCase) && moby.SourceByte36 is 0x0B or 0x31 or 0x5E or 0x5F or 0x87)
        {
            identity = ReviewInferred(
                "Artisans scene control marker",
                "Artisans portal/scene control marker",
                $"Release review groups source byte 0x{moby.SourceByte36:X2} type 0x00 records with nearby portal, dragon, tree, gem, or encounter clusters; move linked groups together before naming individual behavior.",
                ColorRgba.FromRgb(143, 166, 184));
            return true;
        }

        if (string.Equals(levelKey, "alpineridge", StringComparison.OrdinalIgnoreCase) && moby.SourceByte36 is 0x0B or 0x48)
        {
            identity = ReviewInferred(
                "Alpine Ridge scene control marker",
                "Alpine Ridge scene/route control marker",
                $"Release review groups source byte 0x{moby.SourceByte36:X2} type 0x00 records with nearby route, reward, and encounter clusters; no visible model is expected until a linked behavior test proves otherwise.",
                ColorRgba.FromRgb(143, 166, 184));
            return true;
        }

        if (string.Equals(levelKey, "doctorshemp", StringComparison.OrdinalIgnoreCase) && moby.SourceByte36 == 0x5F)
        {
            identity = ReviewInferred(
                "Doctor Shemp scene control marker",
                "Doctor Shemp encounter/control marker",
                "Release review groups the two source byte 0x5F control records with the dragon, pedestal, and yellow chest cluster; no visible model is expected until a linked behavior test proves otherwise.",
                ColorRgba.FromRgb(143, 166, 184));
            return true;
        }

        if (string.Equals(levelKey, "gnastygnorc", StringComparison.OrdinalIgnoreCase) && moby.SourceByte36 is 0x9F or 0xB5 or 0xB9)
        {
            identity = ReviewInferred(
                "Boss route control marker",
                "final boss route control marker",
                $"Release review groups source byte 0x{moby.SourceByte36:X2} control records with the boss and thief/guard route clusters; no visible model is expected until a linked behavior test proves otherwise.",
                ColorRgba.FromRgb(143, 166, 184));
            return true;
        }

        if (string.Equals(levelKey, "gnastysloot", StringComparison.OrdinalIgnoreCase) && moby.SourceByte36 == 0xD9)
        {
            identity = ReviewInferred(
                "Loot route control marker",
                "Gnasty's Loot route control marker",
                "Release review groups this control record with the Gnasty's Loot thief-route prop cluster; no visible model is expected until a linked behavior test proves otherwise.",
                ColorRgba.FromRgb(143, 166, 184));
            return true;
        }

        if (string.Equals(levelKey, "gnastysworld", StringComparison.OrdinalIgnoreCase) && moby.SourceByte36 == 0xD0)
        {
            identity = ReviewInferred(
                "Gnasty's World scene control marker",
                "Gnasty's World scene/portal control marker",
                "Release review groups this control record near the Gnasty's World transport, dragon-pedestal, and chest-support cluster; no visible model is expected until a linked behavior test proves otherwise.",
                ColorRgba.FromRgb(143, 166, 184));
            return true;
        }

        if (IsMagicCraftersRegionalControl(levelKey, moby))
        {
            identity = ReviewInferred(
                "Magic Crafters control marker",
                "Magic Crafters regional scene/route control marker",
                $"Release review groups source byte 0x{moby.SourceByte36:X2} type 0x00 records with Magic Crafters-family route, reward, and encounter clusters; no visible model is expected until a linked behavior test proves otherwise.",
                ColorRgba.FromRgb(143, 166, 184));
            return true;
        }

        if (string.Equals(levelKey, "wizardpeak", StringComparison.OrdinalIgnoreCase) && moby.SourceByte36 == 0x42)
        {
            identity = ReviewInferred(
                "Wizard Peak control marker",
                "Wizard Peak reward/route control marker",
                "Release review groups source byte 0x42 type 0x00 records with Wizard Peak route/reward clusters; move with nearby records before naming exact behavior.",
                ColorRgba.FromRgb(143, 166, 184));
            return true;
        }

        if (IsFlightLevelGeneralControl(levelKey, moby))
        {
            identity = ReviewInferred(
                "Flight course control marker",
                "flight level course control marker",
                $"Release review groups source byte 0x{moby.SourceByte36:X2} type 0x00 records with the flight-level route/target setup; no visible model is expected.",
                ColorRgba.FromRgb(143, 166, 184));
            return true;
        }

        identity = new MobyIdentity(
            "System/trigger marker?",
            "nonvisual trigger, camera, reward, or helper/control candidate",
            "pattern-inferred",
            $"Type 0x00 source byte 0x{moby.SourceByte36:X2}; visible object not confirmed. Treat as system data until a focused move test proves whether it controls camera, cutscene, reward, route, or encounter behavior.",
            ColorRgba.FromRgb(143, 166, 184));
        return true;
    }

    private static bool IsFlightCourseControl(string levelKey, Moby moby)
    {
        return moby.Type == 0x00 &&
            moby.Flag4A == 0xFF &&
            moby.Flag4B == 0xFF &&
            levelKey.ToLowerInvariant() switch
            {
                "sunnyflight" => moby.SourceByte36 == 0xEA,
                "nightflight" => moby.SourceByte36 == 0xE5,
                "crystalflight" => moby.SourceByte36 == 0x83,
                "wildflight" => moby.SourceByte36 == 0x8C,
                "icyflight" => moby.SourceByte36 == 0xEB,
                _ => false
            };
    }

    private static bool IsFlightLevelGeneralControl(string levelKey, Moby moby)
    {
        return moby.Type == 0x00 &&
            levelKey.ToLowerInvariant() is "sunnyflight" or "nightflight" or "crystalflight" or "wildflight" or "icyflight" &&
            moby.SourceByte36 == 0x66;
    }

    private static bool IsMagicCraftersRegionalControl(string levelKey, Moby moby)
    {
        if (moby.Type != 0x00)
            return false;

        string key = levelKey.ToLowerInvariant();
        return key is "magiccrafters" or "highcaves" or "blowhard" &&
            moby.SourceByte36 is 0x0B or 0x48 or 0x5F;
    }

    private static bool TryClassifyGenericSpecial(string levelKey, Moby moby, out MobyIdentity identity)
    {
        if (TryClassifyFlightTarget(levelKey, moby, out identity))
            return true;

        if (TryClassifyLevelSpecialSibling(levelKey, moby, out identity))
            return true;

        if (string.Equals(levelKey, "alpineridge", StringComparison.OrdinalIgnoreCase) && moby.Type == 0x10 && moby.SourceByte36 == 0x3B)
        {
            identity = ReviewInferred(
                "Alpine Ridge route marker",
                "Alpine Ridge route/control marker",
                "Release review groups this type 0x10/source 0x3B record as a route or reward marker; no visible model is expected until a linked behavior test proves otherwise.",
                ColorRgba.FromRgb(143, 166, 184));
            return true;
        }

        if (string.Equals(levelKey, "alpineridge", StringComparison.OrdinalIgnoreCase) && moby.Type == 0x40 && moby.SourceByte36 == 0x38 && moby.Flag4B is >= 0x53 and <= 0x57)
        {
            identity = PositionInferred(
                RewardLabel("Firework chest trigger", moby),
                "firework chest trigger record",
                $"This type 0x40/source 0x38 family is colocated with Alpine Ridge's source 0x38 firework chest package and carries the same reward bytes{RewardEvidence(moby)}.",
                ColorRgba.FromRgb(230, 126, 34));
            return true;
        }

        if (string.Equals(levelKey, "alpineridge", StringComparison.OrdinalIgnoreCase) && moby.Type == 0x12 && moby.SourceByte36 == 0xFD && moby.Flag4B is >= 0x53 and <= 0x57)
        {
            identity = ReviewInferred(
                RewardLabel("Alpine Ridge reward link", moby),
                "Alpine Ridge reward/control link",
                $"Release review groups this type 0x12/source 0xFD family with Alpine Ridge reward records{RewardEvidence(moby)}; it is likely linked data rather than a visible standalone object.",
                ColorRgba.FromRgb(230, 126, 34));
            return true;
        }

        if (string.Equals(levelKey, "loftycastle", StringComparison.OrdinalIgnoreCase) && moby.Type == 0x28 && moby.SourceByte36 == 0x28)
        {
            identity = ReviewInferred(
                "Lofty Castle linked prop",
                "Lofty Castle linked prop",
                "Release review groups this type 0x28/source 0x28 record with the same Lofty Castle prop special-data family as nearby no-reward source 0x28 props; exact role still needs a focused move test.",
                ColorRgba.FromRgb(118, 150, 178));
            return true;
        }

        if (moby.Type == 0x10 && moby.SourceByte36 == 0xA6 && levelKey.ToLowerInvariant() is "loftycastle" or "metalhead")
        {
            identity = ReviewInferred(
                "Life chest support link",
                "life chest support/control link",
                $"Release review groups this {levelKey} type 0x10/source 0xA6 record as a nonvisual support row. In Lofty Castle and Metalhead it is exactly co-located with a confirmed Life Chest, so the editor treats it as that chest's support/control link.",
                ColorRgba.FromRgb(143, 166, 184));
            return true;
        }

        if (string.Equals(levelKey, "dreamweavers", StringComparison.OrdinalIgnoreCase) && moby.Type == 0x50 && moby.SourceByte36 == 0x5B && moby.Flag4B == 0xFF)
        {
            identity = PositionInferred(
                "Resize cannon",
                "Dream Weavers resize cannon object",
                "This type 0x50/source 0x5B record sits inside the Dream Weavers homeworld enemy-resize cluster; the editor treats it as the resize cannon until a focused move test proves a narrower trigger role.",
                ColorRgba.FromRgb(143, 166, 184));
            return true;
        }

        if (string.Equals(levelKey, "dreamweavers", StringComparison.OrdinalIgnoreCase) && moby.Type == 0x60 && moby.SourceByte36 == 0x69 && moby.Flag4B == 0xFF)
        {
            identity = PositionInferred(
                "Enemy resize effect marker",
                "Dream Weavers enemy-resize effect/control marker",
                "This type 0x60/source 0x69 record sits inside the Dream Weavers homeworld enemy-resize cluster; the editor treats it as a nonvisual resize/effect support marker until focused testing proves the exact trigger role.",
                ColorRgba.FromRgb(143, 166, 184));
            return true;
        }

        if (string.Equals(levelKey, "dreamweavers", StringComparison.OrdinalIgnoreCase) && moby.Type == 0x40 && moby.SourceByte36 == 0x5C && moby.Flag4B is >= 0x53 and <= 0x57)
        {
            identity = RosterInferred(
                RewardLabel("Winged Fool", moby),
                "winged fool enemy",
                $"Dream Weavers type 0x40/source 0x5C is a ten-record reward family, matching the homeworld guide's ten Winged Guys/Fools{RewardEvidence(moby)}.",
                ColorRgba.FromRgb(230, 91, 67));
            return true;
        }

        if (string.Equals(levelKey, "dreamweavers", StringComparison.OrdinalIgnoreCase) && moby.Type == 0x40 && moby.SourceByte36 == 0x82 && moby.Flag4B is >= 0x53 and <= 0x57)
        {
            identity = RosterInferred(
                RewardLabel("Armored Fool", moby),
                "armored fool enemy",
                $"Dream Weavers type 0x40/source 0x82 is an eight-record reward family, matching the homeworld guide's eight Armored Guys/Fools{RewardEvidence(moby)}.",
                ColorRgba.FromRgb(230, 91, 67));
            return true;
        }

        if (string.Equals(levelKey, "gnastygnorc", StringComparison.OrdinalIgnoreCase) && moby.Type == 0x50 && moby.SourceByte36 == 0xA1 && moby.Flag4B is >= 0x53 and <= 0x57)
        {
            identity = ReviewInferred(
                RewardLabel("Gnasty Gnorc reward link", moby),
                "Gnasty Gnorc reward-linked object",
                $"Release review groups this type 0x50/source 0xA1 record with the Gnasty Gnorc boss reward cluster{RewardEvidence(moby)}.",
                ColorRgba.FromRgb(230, 126, 34));
            return true;
        }

        if (string.Equals(levelKey, "terracevillage", StringComparison.OrdinalIgnoreCase) && moby.Type == 0x28 && moby.SourceByte36 == 0xD2)
        {
            identity = ReviewInferred(
                "Spotted chicken linked record",
                "spotted chicken fodder linked record",
                "Terrace Village type 0x28/source 0xD2 shares the same special-data family as the local Spotted Chicken fodder records; exact support behavior still needs a focused move test.",
                ColorRgba.FromRgb(230, 91, 67));
            return true;
        }

        if (string.Equals(levelKey, "treetops", StringComparison.OrdinalIgnoreCase) && moby.Type == 0x50 && moby.SourceByte36 == 0x26 && moby.Flag4B is >= 0x53 and <= 0x57)
        {
            identity = ReviewInferred(
                RewardLabel("Tree Tops reward link", moby),
                "Tree Tops reward-linked object",
                $"Release review groups this type 0x50/source 0x26 record with Tree Tops route and reward-support clusters{RewardEvidence(moby)}.",
                ColorRgba.FromRgb(230, 126, 34));
            return true;
        }

        if (string.Equals(levelKey, "dreamweavers", StringComparison.OrdinalIgnoreCase) && moby.Type == 0x40 && moby.Flag4B is 0x53 or 0x54 or 0x55 or 0x56 or 0x57)
        {
            identity = ReviewInferred(
                RewardLabel("Dream Weavers reward family", moby),
                "Dream Weavers interactive reward object family",
                $"Release review groups Dream Weavers type 0x40 reward-bearing records as unresolved interactive/reward object families{RewardEvidence(moby)}.",
                ColorRgba.FromRgb(216, 137, 42));
            return true;
        }

        if (string.Equals(levelKey, "hauntedtowers", StringComparison.OrdinalIgnoreCase) && moby.Type == 0x28 && moby.SourceByte36 == 0x88)
        {
            identity = ReviewInferred(
                "Haunted Towers special control",
                "Haunted Towers special control marker",
                "Release review groups this type 0x28 source family as a Haunted Towers special-control cluster; move with nearby records before naming exact behavior.",
                ColorRgba.FromRgb(118, 150, 178));
            return true;
        }

        if (string.Equals(levelKey, "hauntedtowers", StringComparison.OrdinalIgnoreCase) && moby.Type == 0x10 && moby.SourceByte36 == 0x84)
        {
            identity = ReviewInferred(
                "Haunted Towers control link",
                "Haunted Towers special control marker",
                "Release review groups this type 0x10 source family as a Haunted Towers control cluster; no visible model is expected until linked behavior is proven.",
                ColorRgba.FromRgb(143, 166, 184));
            return true;
        }

        if (string.Equals(levelKey, "alpineridge", StringComparison.OrdinalIgnoreCase) && moby.Type == 0x38 && moby.SourceByte36 == 0xFD && moby.Flag4B is >= 0x53 and <= 0x57)
        {
            identity = ReviewInferred(
                RewardLabel("Alpine Ridge reward link", moby),
                "Alpine Ridge reward/control link",
                $"Release review groups this Alpine Ridge reward-bearing special family with chest/reward records{RewardEvidence(moby)}; it is likely linked data rather than a visible standalone object.",
                ColorRgba.FromRgb(230, 91, 67));
            return true;
        }

        if (string.Equals(levelKey, "alpineridge", StringComparison.OrdinalIgnoreCase) && moby.Type == 0x28 && moby.SourceByte36 == 0x0E && moby.Flag4B is >= 0x53 and <= 0x57)
        {
            identity = PositionInferred(
                RewardLabel("Moving platform marker", moby),
                "Alpine Ridge moving platform/control marker",
                $"This type 0x28/source 0x0E family shares the Alpine Ridge green-druid package and sits in the same route-control cluster{RewardEvidence(moby)}.",
                ColorRgba.FromRgb(143, 166, 184));
            return true;
        }

        if (string.Equals(levelKey, "icecavern", StringComparison.OrdinalIgnoreCase) && moby.Type == 0x10 && moby.SourceByte36 == 0xA6)
        {
            identity = ReviewInferred(
                "Ice Cavern special control marker",
                "Ice Cavern special control marker",
                "Release review groups this Ice Cavern type 0x10 family as a level control marker cluster; no visible model is expected until linked behavior is proven.",
                ColorRgba.FromRgb(143, 166, 184));
            return true;
        }

        if (string.Equals(levelKey, "treetops", StringComparison.OrdinalIgnoreCase) && moby.Type == 0x3C && moby.SourceByte36 == 0xFA)
        {
            identity = ReviewInferred(
                "Tree Tops special control marker",
                "Tree Tops special control marker",
                "Release review groups this Tree Tops type 0x3C family as a level control marker cluster; move with nearby records before naming the exact behavior.",
                ColorRgba.FromRgb(143, 166, 184));
            return true;
        }

        if (IsFlightSpecialSupport(levelKey, moby))
        {
            identity = ReviewInferred(
                "Flight course special record",
                "flight course special record",
                $"Release review groups this type 0x{moby.Type:X2}/source 0x{moby.SourceByte36:X2} record with the flight-course setup; exact role still needs a focused course test.",
                ColorRgba.FromRgb(143, 166, 184));
            return true;
        }

        if (moby.Type == 0x38 && moby.SourceByte36 == 0x92 && moby.Flag4B == 0xFF)
        {
            identity = Observed(
                "Tent",
                "tent scenery prop",
                "Peace Keepers user overrides repeatedly identify type 0x38 source byte 0x92 records as tents.",
                ColorRgba.FromRgb(93, 173, 226));
            return true;
        }

        if (moby.Type == 0x38 && moby.SourceByte36 == 0xE1 && moby.Flag4B == 0xFF)
        {
            identity = Observed(
                "Cannon",
                "cannon scenery/control object",
                "Peace Keepers user overrides repeatedly identify type 0x38 source byte 0xE1 records as cannon objects.",
                ColorRgba.FromRgb(118, 150, 178));
            return true;
        }

        if (moby.Type == 0x1A && moby.SourceByte36 == 0x88 && moby.Flag4A == 0xFF && moby.Flag4B == 0xFF)
        {
            identity = new MobyIdentity(
                "3x flame chest fan",
                "3x flame chest fan record",
                "byte-pattern",
                "Dry Canyon and Peace Keepers user validation mapped type 0x1A source byte 0x88 to the fan record used by 3x flame chests.",
                ColorRgba.FromRgb(230, 126, 34));
            return true;
        }

        if (moby.Type == 0x3C && moby.SourceByte36 == 0xA0 && moby.Flag4A == 0x10 && moby.Flag4B == 0xFF)
        {
            identity = Observed(
                "Balloon",
                "transport balloon scenery prop",
                "Artisans live validation identified the exact type 0x3C source byte 0xA0 fingerprint as the visible transport balloon object.",
                ColorRgba.FromRgb(248, 196, 113));
            return true;
        }

        if (moby.Type == 0x10 && moby.SourceByte36 == 0x78)
        {
            identity = new MobyIdentity(
                "Sparx behavior control",
                "Sparx behavior control record",
                "live-observed behavior",
                "Artisans live validation showed one type 0x10 source byte 0x78 record affects Sparx behavior: after moving it, Sparx flew to the moved position and then returned to Spyro. This pattern repeats once per many levels.",
                ColorRgba.FromRgb(143, 166, 184));
            return true;
        }

        if (moby.Type == 0x40 && moby.SourceByte36 is 0x59 or 0x5A or 0x5B && moby.Flag4A == 0xFF && moby.Flag4B == 0xFF)
        {
            identity = Observed(
                "Flight timer number",
                "flight timer number display",
                "Sunny Flight user overrides identify the type 0x40/0x5B fingerprint as timer number records; the 0x59 and 0x5A variants share the same type/flag/value shape and appear only in flight-level timer display rows.",
                ColorRgba.FromRgb(248, 196, 113));
            return true;
        }

        if (moby.Type == 0x50 && moby.Flag4B == 0xFF)
        {
            identity = moby.SourceByte36 switch
            {
                0x2B => Observed("Flight chest", "flight chest target", "Sunny Flight user overrides repeatedly identify type 0x50 source byte 0x2B records as flight chest targets.", ColorRgba.FromRgb(230, 126, 34)),
                0x34 => Observed("Airplane", "flight airplane target", "Sunny Flight user overrides repeatedly identify type 0x50 source byte 0x34 records as airplane targets.", ColorRgba.FromRgb(93, 173, 226)),
                0x4E => Observed("Train TNT cluster", "flight train TNT target", "Sunny Flight user overrides repeatedly identify type 0x50 source byte 0x4E records as train TNT target pieces.", ColorRgba.FromRgb(230, 91, 67)),
                0x61 => Observed("Flight arch", "flight arch target", "Sunny Flight user overrides repeatedly identify type 0x50 source byte 0x61 records as flight arches.", ColorRgba.FromRgb(93, 173, 226)),
                0x97 => Observed("Train piece", "flight train target", "Sunny Flight user overrides identify type 0x50 source byte 0x97 records as train pieces.", ColorRgba.FromRgb(93, 173, 226)),
                0x98 => Observed("Train TNT car", "flight train TNT target", "Sunny Flight user overrides repeatedly identify type 0x50 source byte 0x98 records as train TNT cars.", ColorRgba.FromRgb(230, 91, 67)),
                _ => default
            };
            if (identity != default)
                return true;
        }

        if (moby.Flag4B is >= 0x53 and <= 0x57)
        {
            identity = new MobyIdentity(
                $"Interactive reward object 0x{moby.Type:X2}?",
                "interactive actor/object with reward byte candidate",
                "pattern-inferred",
                $"Type 0x{moby.Type:X2} source byte 0x{moby.SourceByte36:X2}, reward/variant byte 0x{moby.Flag4B:X2}; needs live validation for the exact model.",
                ColorRgba.FromRgb(216, 137, 42));
            return true;
        }

        if (moby.Flag4B is > 0 and < 0xFF)
        {
            identity = new MobyIdentity(
                $"Interactive object 0x{moby.Type:X2}?",
                "interactive actor/object candidate",
                "pattern-inferred",
                $"Type 0x{moby.Type:X2} source byte 0x{moby.SourceByte36:X2}, behavior byte 0x{moby.Flag4B:X2}; needs live validation for the exact model.",
                ColorRgba.FromRgb(216, 137, 42));
            return true;
        }

        if (moby.Flag4A == 0xFF || moby.Flag4B == 0xFF)
        {
            identity = new MobyIdentity(
                $"Special object 0x{moby.Type:X2}?",
                "special object, prop, or helper candidate",
                "pattern-inferred",
                $"Type 0x{moby.Type:X2} source byte 0x{moby.SourceByte36:X2}; needs live validation for the exact model.",
                ColorRgba.FromRgb(118, 150, 178));
            return true;
        }

        identity = default;
        return false;
    }

    private static bool IsFlightSpecialSupport(string levelKey, Moby moby)
    {
        if (moby.Flag4A != 0xFF || moby.Flag4B != 0xFF)
            return false;

        return levelKey.ToLowerInvariant() switch
        {
            "crystalflight" => moby.Type == 0xFF && moby.SourceByte36 is 0x05 or 0x06 or 0x07 or 0x47,
            "sunnyflight" => moby.Type == 0xFF && moby.SourceByte36 is 0x05 or 0x06 or 0x09 or 0x47,
            "wildflight" => moby.Type == 0xFF && moby.SourceByte36 is 0x05 or 0x08 or 0x47,
            "icyflight" => moby.Type == 0xFF && moby.SourceByte36 is 0x04 or 0x05 or 0x0A or 0x47 or 0x5A or 0xEB,
            "treetops" => moby.Type == 0x3C && moby.SourceByte36 == 0xFA,
            _ => false
        };
    }

    private static bool TryClassifyLevelSpecialSibling(string levelKey, Moby moby, out MobyIdentity identity)
    {
        string key = levelKey.ToLowerInvariant();
        switch (key, moby.Type, moby.SourceByte36)
        {
            case ("clifftown", 0x2C, 0xDA):
                identity = RosterInferred("Fat Momma/Cauldron", "fat momma cauldron support record", "This Cliff Town special record shares the raw 0xDA package with the seven Fat Lady/cauldron support records; the package points at the 0xE2 Pueblo family.", ColorRgba.FromRgb(230, 126, 34));
                return true;
            case ("clifftown", 0x2C, 0xE5):
                identity = RosterInferred("Fat Momma/Cauldron", "fat momma cauldron reward record", "This Cliff Town special record shares the raw 0xE5 reward package with the seven Fat Lady/cauldron encounters listed by the Enemy/Boss FAQ.", ColorRgba.FromRgb(230, 126, 34));
                return true;
            case ("terracevillage", 0x2A or 0x38, 0xCD):
                identity = RosterInferred("Floor Shocker linked record", "floor shocker enemy linked record", "This Terrace Village special record shares the 0xCD Floor Shocker model pointer; the seven visible actors plus two linked siblings match the guide's nine Floor Shockers.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("terracevillage", 0x2A or 0x38, 0xD3):
                identity = RosterInferred("Laser Gnorc linked record", "laser gnorc linked record", "This Terrace Village special record shares the 0xD3 Laser Gnorc model pointer; the eleven visible actors plus two linked siblings match the guide's thirteen Laser Gnorcs.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("twilightharbor", 0x10, 0x93):
                identity = RosterInferred("Gnorc Commando linked record", "gnorc commando linked record", "This Twilight Harbor type 0x10 record shares the 0x93 Gnorc Commando pointer; nine visible type 0x30 records plus this sibling match the guide's ten Gnorc Commandos.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("twilightharbor", 0x10, 0x94):
                identity = RosterInferred("Super Grenadier linked record", "super grenadier linked record", "This Twilight Harbor type 0x10 record shares the 0x94 Super Grenadier pointer; three visible type 0x30 records plus this sibling match the guide's four Super Grenadiers.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("loftycastle", 0x28, 0x2A):
                identity = RosterInferred("Balloognorc linked record", "balloon gnorc linked record", "This Lofty Castle type 0x28 record shares the 0x2A Balloognorc pointer; eight visible actors plus this linked sibling match the guide's nine Balloognorcs.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("alpineridge", 0x10, 0xA5) when moby.Flag4B == 0x0E:
                identity = SourceObserved("Red flag", "red flag scenery prop", "Alpine Ridge type 0x10 source byte 0xA5 shares source-disc special signature f054ec7055d178d0 with eight exact Alpine Ridge Red flag records.", ColorRgba.FromRgb(220, 78, 65));
                return true;
            case ("gnastysloot", 0x50, 0xB1):
                identity = RosterInferred("Plane thief aircraft", "plane thief aircraft/route actor", "Gnasty's Loot source table has two type 0x50 source byte 0xB1 records paired with the key-carrying plane thief route.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            default:
                identity = default;
                return false;
        }
    }

    private static bool TryClassifyFlightTarget(string levelKey, Moby moby, out MobyIdentity identity)
    {
        if (!IsFlightLevel(levelKey) || moby.Flag4B != 0xFF)
        {
            identity = default;
            return false;
        }

        if (moby.SourceByte36 == 0x2B && moby.Type is 0x30 or 0x40 or 0x50)
        {
            identity = Observed(
                "Flight chest target",
                "flight chest target",
                "Sunny Flight user overrides identify source byte 0x2B as flight chest targets; Wild Flight and Crystal Flight reuse the same target source byte under level-local runtime types.",
                ColorRgba.FromRgb(230, 126, 34));
            return true;
        }

        if (moby.SourceByte36 == 0x34 && moby.Type is 0x30 or 0x40 or 0x50)
        {
            identity = Observed(
                "Airplane",
                "flight airplane target",
                "Sunny Flight user overrides identify source byte 0x34 as airplane targets; Wild Flight and Crystal Flight reuse the same target source byte under level-local runtime types.",
                ColorRgba.FromRgb(93, 173, 226));
            return true;
        }

        if (moby.SourceByte36 == 0x61 && moby.Type is 0x3C or 0x50 or 0x7F)
        {
            identity = Observed(
                "Flight arch",
                "flight arch target",
                "Sunny Flight user overrides identify source byte 0x61 as flight arches; Wild Flight and Crystal Flight reuse the same target source byte under level-local runtime types.",
                ColorRgba.FromRgb(93, 173, 226));
            return true;
        }

        if (moby.SourceByte36 == 0x54 && moby.Type is 0x50 or 0x7F && moby.Flag4A == 0x50)
        {
            identity = new MobyIdentity(
                "Flight ring",
                "flight ring target",
                "flight-target-inferred",
                "Night Flight and Crystal Flight place eight source byte 0x54 records in the target rows left after chests, planes, arches, and lighthouses are separated.",
                ColorRgba.FromRgb(248, 196, 113));
            return true;
        }

        if (moby.SourceByte36 is 0x8D or 0x8F && moby.Type == 0x60)
        {
            identity = new MobyIdentity(
                "Flight lighthouse",
                "flight lighthouse target",
                "flight-target-inferred",
                "Night Flight and Icy Flight share source byte 0x8D/0x8F target rows; these match the lighthouse target family after chest, ring, and arch families are separated.",
                ColorRgba.FromRgb(248, 196, 113));
            return true;
        }

        if (string.Equals(levelKey, "wildflight", StringComparison.OrdinalIgnoreCase) &&
            moby.Type == 0x30 &&
            moby.SourceByte36 == 0xA2)
        {
            identity = new MobyIdentity(
                "Flight boat target",
                "flight boat target",
                "flight-target-inferred",
                "Wild Flight source byte 0xA2 is the remaining eight-target row after chest, airplane, and arch source bytes are identified.",
                ColorRgba.FromRgb(93, 173, 226));
            return true;
        }

        if (string.Equals(levelKey, "icyflight", StringComparison.OrdinalIgnoreCase) &&
            moby.Type == 0x50 &&
            moby.SourceByte36 == 0xA3 &&
            moby.Flag4A == 0x10)
        {
            identity = new MobyIdentity(
                "Copter Gnorc",
                "flight copter target",
                "flight-target-inferred",
                "Icy Flight has eight source byte 0xA3 flight-target records; the level target roster has eight copters after lighthouses, barrels, and chests are separated.",
                ColorRgba.FromRgb(93, 173, 226));
            return true;
        }

        identity = default;
        return false;
    }

    private static bool IsFlightLevel(string levelKey)
    {
        return string.Equals(levelKey, "sunnyflight", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(levelKey, "nightflight", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(levelKey, "crystalflight", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(levelKey, "wildflight", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(levelKey, "icyflight", StringComparison.OrdinalIgnoreCase);
    }

    private static string InferActorLabel(string levelKey, Moby moby)
    {
        if (string.Equals(levelKey, "stonehill", StringComparison.OrdinalIgnoreCase))
        {
            if (moby.SourceByte36 == 0x0A)
                return "Sheep/fodder or small actor?";
            if (moby.Flag4B == 0x55)
                return "Ram enemy?";
            if (moby.Flag4B == 0x54)
                return "Shepherd/ram enemy?";
        }

        return "Enemy/fodder actor?";
    }

    private static bool HasWeakLabel(Moby moby)
    {
        if (string.IsNullOrWhiteSpace(moby.Label))
            return true;

        if (moby.Label.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return true;

        if (moby.Label.Contains("?", StringComparison.Ordinal))
            return true;

        if (moby.Label.Contains("unknown", StringComparison.OrdinalIgnoreCase))
            return true;

        return string.Equals(moby.Label, Moby.FallbackLabel(moby.Type), StringComparison.Ordinal);
    }

    private static bool HasPlaceholderControlLabel(Moby moby)
    {
        string text = $"{moby.Label} {moby.CandidateKind}".ToLowerInvariant();
        return text.Contains("placeholder") ||
            (text.Contains("nonvisual") && text.Contains("control"));
    }

    private static bool ShouldReplaceWithPassiveClassIdentity(Moby moby, MobyIdentity identity)
    {
        if (!IsPassiveClassIdentity(identity))
            return false;

        string text = $"{moby.Label} {moby.CandidateKind}".ToLowerInvariant();
        return HasPlaceholderControlLabel(moby) ||
            text.Contains("class 0x1e passive control") ||
            text.Contains("unresolved passive native control") ||
            text.Contains("linked helper") ||
            text.Contains("rescue control/helper") ||
            text.Contains("scene/route control") ||
            text.Contains("0x1e scene/route");
    }

    private static bool IsPassiveClassIdentity(MobyIdentity identity)
    {
        return identity.Confidence.Contains("native-passive-class", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasCandidateLabel(Moby moby)
    {
        string text = $"{moby.Label} {moby.CandidateKind} {moby.Confidence}".ToLowerInvariant();
        return text.Contains("candidate");
    }

    private static bool HasValidationQualifierLabel(Moby moby)
    {
        string text = $"{moby.Label} {moby.CandidateKind} {moby.Confidence}".ToLowerInvariant();
        return text.Contains("safe-ground observed") ||
            text.Contains("isolate observed") ||
            text.Contains("wide-row observed") ||
            text.Contains("raised-row observed");
    }

    private static bool HasReviewFamilyLabel(Moby moby, MobyIdentity identity)
    {
        if (!identity.Confidence.Contains("position-cluster-inferred", StringComparison.OrdinalIgnoreCase) &&
            !identity.Confidence.Contains("guide-roster-count", StringComparison.OrdinalIgnoreCase) &&
            !identity.Confidence.Contains("source-signature-observed", StringComparison.OrdinalIgnoreCase) &&
            !identity.Confidence.Contains("native-class-linked", StringComparison.OrdinalIgnoreCase))
            return false;

        string existing = $"{moby.Label} {moby.CandidateKind} {moby.Confidence} {moby.Evidence}".ToLowerInvariant();
        return existing.Contains("release-review-family") ||
            existing.Contains("prop family") ||
            existing.Contains("scenery family") ||
            existing.Contains("support family") ||
            existing.Contains("exact model");
    }

    private static bool HasStaleConflictingLabel(Moby moby, MobyIdentity identity)
    {
        string existing = $"{moby.Label} {moby.CandidateKind}".ToLowerInvariant();
        string incoming = $"{identity.Label} {identity.Kind}".ToLowerInvariant();
        if (incoming.Contains("life chest") && existing.Contains("dragon"))
            return true;
        if (incoming.Contains("dragon actor") && existing.Contains("pedestal"))
            return true;
        if (incoming.Contains("dragon pedestal") && existing.Contains("dragon") && !existing.Contains("pedestal"))
            return true;
        if (incoming.Contains("balloonist") && existing.Contains("baloonist"))
            return true;
        if (incoming.Contains("egg thief") && existing.Contains("egg theif"))
            return true;
        if (incoming.Contains("ambient sound emitter") && existing.Contains("return home helper"))
            return true;

        return false;
    }

    private static string FirstNonEmpty(string existing, string inferred)
    {
        return string.IsNullOrWhiteSpace(existing) ? inferred : existing;
    }

    private static bool IsStrongIdentity(MobyIdentity identity)
    {
        return identity.Confidence.Contains("byte-pattern", StringComparison.OrdinalIgnoreCase) ||
            identity.Confidence.Contains("native-scene", StringComparison.OrdinalIgnoreCase) ||
            identity.Confidence.Contains("native-class", StringComparison.OrdinalIgnoreCase) ||
            identity.Confidence.Contains("user", StringComparison.OrdinalIgnoreCase) ||
            identity.Confidence.Contains("live", StringComparison.OrdinalIgnoreCase) ||
            identity.Confidence.Contains("observed", StringComparison.OrdinalIgnoreCase) ||
            identity.Confidence.Contains("validated", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDecompNativeClassIdentity(MobyIdentity identity)
    {
        return identity.Confidence.Contains("native-class-observed", StringComparison.OrdinalIgnoreCase) ||
            identity.Confidence.Contains("native-handler", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUserOverride(MobyIdentity identity)
    {
        return identity.Confidence.Contains("user-override", StringComparison.OrdinalIgnoreCase);
    }

    private static string RewardLabel(string baseLabel, Moby moby)
    {
        return GemValue.TryFromIdByte(moby.Flag4B, out GemValue gem)
            ? $"{baseLabel} ({gem.Name.Replace(" gem", "", StringComparison.OrdinalIgnoreCase)} reward)"
            : baseLabel;
    }

    private static string RewardEvidence(Moby moby)
    {
        return GemValue.TryFromIdByte(moby.Flag4B, out GemValue gem)
            ? $", with reward byte 0x{moby.Flag4B:X2} for {gem.Name}"
            : "";
    }

    private static MobyIdentity Observed(string label, string kind, string evidence, ColorRgba color)
    {
        return new MobyIdentity(label, kind, "observed-fingerprint", evidence, color);
    }

    private static MobyIdentity UserObserved(string label, string kind, string evidence, ColorRgba color)
    {
        return new MobyIdentity(label, kind, "user-override", evidence, color);
    }

    private static MobyIdentity RosterInferred(string label, string kind, string evidence, ColorRgba color)
    {
        return new MobyIdentity(label, kind, "guide-roster-count", evidence, color);
    }

    private static MobyIdentity ReviewInferred(string label, string kind, string evidence, ColorRgba color)
    {
        return new MobyIdentity(label, kind, "release-review-family", evidence, color);
    }

    private static MobyIdentity PositionInferred(string label, string kind, string evidence, ColorRgba color)
    {
        return new MobyIdentity(label, kind, "position-cluster-inferred", evidence, color);
    }

    private static MobyIdentity SourceObserved(string label, string kind, string evidence, ColorRgba color)
    {
        return new MobyIdentity(label, kind, "source-signature-observed", evidence, color);
    }

    private static MobyIdentity NativeClassObserved(string label, string kind, string evidence, ColorRgba color)
    {
        return new MobyIdentity(label, kind, "native-class-observed", evidence, color);
    }

    private static MobyIdentity NativeClassRoster(string label, string kind, string evidence, ColorRgba color)
    {
        return new MobyIdentity(label, kind, "guide-roster-count-native-handler", evidence, color);
    }
}

public readonly record struct MobyIdentity(string Label, string Kind, string Confidence, string Evidence, ColorRgba Color);
