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
                HasWeakLabel(moby) ||
                HasReviewFamilyLabel(moby, identity) ||
                (IsStrongIdentity(identity) && (HasPlaceholderControlLabel(moby) || HasCandidateLabel(moby) || HasValidationQualifierLabel(moby) || HasStaleConflictingLabel(moby, identity)));
            if (replaceLabel)
            {
                moby.Label = identity.Label;
                moby.OriginalLabel = identity.Label;
                if (IsStrongIdentity(identity))
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

        if (TryClassifyContainedGem(moby, out identity))
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
                "Helper/system marker",
                "level helper or engine control marker",
                "pattern-inferred",
                $"Type 0x{moby.Type:X2} is a recurring helper/system family.",
                ColorRgba.FromRgb(143, 166, 184));
            return true;
        }

        if (TryClassifyGenericSpecial(levelKey, moby, out identity))
            return true;

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
            moby.Flag4B is 0x55 or 0x56)
        {
            identity = Observed(
                RewardLabel("Flame/charge chest", moby),
                "flame-or-charge breakable chest",
                "Peace Keepers and Dry Canyon user overrides identify this exact type 0x18/source byte 0xC2/flag 0x40 fingerprint as flame/charge chest records, with the reward byte stored in flag4B.",
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

        if (moby.Type == 0x18 && moby.SourceByte36 == 0xAE)
        {
            identity = new MobyIdentity(
                "Key Chest",
                "key-required chest shell",
                "byte-pattern",
                "Dark Hollow T58 uses type 0x18/source byte 0xAE as the key-required chest shell; its contained gem records hold the reward colors.",
                ColorRgba.FromRgb(230, 126, 34));
            return true;
        }

        if (moby.Type == 0x18 && moby.SourceByte36 == 0xAD)
        {
            identity = new MobyIdentity(
                "Key",
                "key collectible",
                "byte-pattern",
                "Type 0x18 with source byte 0xAD is the key collectible family; the value byte alone can look like a green gem.",
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
            case 0xE6 when moby.Flag4A == 0x10 && moby.Flag4B is 0x54 or 0x55 or 0x56 or 0x57:
                identity = Observed("Bird", "bird enemy", $"Dry Canyon user overrides repeatedly identify source byte 0xE6 reward actor records as birds{RewardEvidence(moby)}.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case 0xE6 when moby.Flag4A is 0x10 or 0x26 && moby.Flag4B == 0xFF:
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
            case ("loftycastle", 0x58):
                identity = RosterInferred("Caged Fairy", "Lofty Castle fairy rescue actor", "Lofty Castle has fifteen source byte 0x58 no-reward records at the same positions as the fifteen fairy cage/support records; the walkthrough repeatedly gates whirlwinds behind freeing caged fairies.", ColorRgba.FromRgb(118, 214, 206));
                return true;
            case ("loftycastle", 0x34):
                identity = RosterInferred(RewardLabel("Devil Cupid", moby), "devil cupid enemy", $"Lofty Castle source byte 0x34 is a twelve-record reward actor family, matching the level guide's twelve Devil Cupids{RewardEvidence(moby)}.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("loftycastle", 0x3E):
                identity = RosterInferred(RewardLabel("Fat Bat", moby), "fat bat enemy", $"Lofty Castle source byte 0x3E is a twelve-record reward actor family, matching the level guide's twelve Fat Bats{RewardEvidence(moby)}.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("darkpassage", 0x32):
                identity = RosterInferred(RewardLabel("Puppy / Devil Dog", moby), "puppy devil dog enemy", $"Dark Passage source byte 0x32 is the large transforming dog reward family, matching the Puppy/Devil Dog lane controlled by the Lamp Fools{RewardEvidence(moby)}.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("darkpassage", 0x34):
                identity = RosterInferred(RewardLabel("Devil Cupid", moby), "devil cupid enemy", $"Dark Passage source byte 0x34 is the reward-bearing archer family along the route, matching the level guide's Devil Cupid lane{RewardEvidence(moby)}.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("darkpassage", 0x35):
                identity = RosterInferred(RewardLabel("Turtle / Mutant Turtle", moby), "turtle mutant-turtle enemy", $"Dark Passage source byte 0x35 is the light-state turtle enemy reward family paired with Lamp Fool changes, matching the Turtle/Mutant Turtle lane{RewardEvidence(moby)}.", ColorRgba.FromRgb(230, 91, 67));
                return true;
            case ("darkpassage", 0x33):
            case ("darkpassage", 0x36):
                identity = ReviewInferred(RewardLabel("Lamp Fool transform control link", moby), "Lamp Fool transform control link", $"Release review keeps this smaller source family grouped beside Lamp Fools and Dark Passage light/dark enemy transformation records{RewardEvidence(moby)}; no independent visible host is proven, so the editor treats it as linked control data.", ColorRgba.FromRgb(216, 137, 42));
                return true;
            case ("hauntedtowers", 0xCB):
                identity = ReviewInferred(RewardLabel("Tin Soldier / Gnorc-Adier", moby), "Haunted Towers armored enemy family", $"Haunted Towers source byte 0xCB is the remaining large reward-bearing combat family after Summoning Wizards and chest families are separated; it aligns with the Tin Soldier/Gnorc-Adier roster lanes{RewardEvidence(moby)}.", ColorRgba.FromRgb(230, 91, 67));
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
            case ("icecavern", 0xD5) when moby.Flag4B == 0x10:
                identity = RosterInferred("Bat", "bat fodder", "Ice Cavern's sixteen-record no-reward source byte 0xD5 family matches the level's bat fodder roster.", ColorRgba.FromRgb(230, 91, 67));
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
                identity = RosterInferred("Lightning Wizard", "lightning wizard enemy", "Blowhard has one seven-record source byte 0x1B actor family matching the level guide's seven Lightning Wizards.", ColorRgba.FromRgb(230, 91, 67));
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
            case ("mistybog", 0x1C):
                identity = RosterInferred("Shielded Greenie", "shielded greenie enemy", "Misty Bog's remaining large reward-dropping source byte 0x1C actor family matches the level roster's Shielded Greenies, including the extra line-formation records noted by the enemy guide.", ColorRgba.FromRgb(230, 91, 67));
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
            case ("metalhead", 0x41):
                identity = RosterInferred("Power pylon", "Metalhead power pylon prop", "Metalhead's seventeen no-reward prop records align with the two boss-room pylon waves in the level roster.", ColorRgba.FromRgb(93, 173, 226));
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
                "Portal pad trigger marker",
                "home-world portal pad trigger/control marker",
                "byte-pattern",
                "Home-world caches pair source byte 0x8E records with portal destination markers around portal pads.",
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
                "Dragon rescue control marker",
                "dragon rescue control marker",
                "byte-pattern",
                "Full-cache sweep shows source byte 0x6E type 0x00 records sitting directly on dragon and pedestal rescue pairs across home worlds and realms.",
                ColorRgba.FromRgb(183, 140, 255));
            return true;
        }

        if (moby.SourceByte36 == 0x1E && moby.Flag4A == 0x10 && moby.Flag4B == 0xFF)
        {
            identity = new MobyIdentity(
                "Scene/route control marker",
                "level scene/route control marker",
                "byte-pattern",
                "Full-cache sweep shows source byte 0x1E type 0x00 records as non-rendered routing helpers beside portals, return-home pads, scene triggers, treasure, or encounter clusters.",
                ColorRgba.FromRgb(143, 166, 184));
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

        if (string.Equals(levelKey, "metalhead", StringComparison.OrdinalIgnoreCase) && moby.SourceByte36 == 0x66)
        {
            identity = ReviewInferred(
                "Metalhead pylon reward control",
                "Metalhead pylon/boss reward control marker",
                "Release review groups source byte 0x66 type 0x00 records with Metalhead pylon and boss reward clusters; source byte 0xAA is kept separate for the bird-linked control family until linked behavior is proven.",
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

        if (string.Equals(levelKey, "metalhead", StringComparison.OrdinalIgnoreCase) && moby.SourceByte36 == 0xAA)
        {
            identity = ReviewInferred(
                "Metalhead bird reward control",
                "Metalhead bird/reward control marker",
                "Release review groups source byte 0xAA type 0x00 records with Metalhead bird and reward-control clusters; source byte 0x66 is kept separate as the pylon/boss reward-control family.",
                ColorRgba.FromRgb(143, 166, 184));
            return true;
        }

        identity = new MobyIdentity(
            "Nonvisual control marker?",
            "nonvisual control or helper candidate",
            "pattern-inferred",
            $"Type 0x00 source byte 0x{moby.SourceByte36:X2}; visible object not confirmed.",
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

        if (string.Equals(levelKey, "metalhead", StringComparison.OrdinalIgnoreCase) && moby.Type == 0x40 && moby.SourceByte36 == 0x30 && moby.Flag4B is >= 0x53 and <= 0x57)
        {
            identity = ReviewInferred(
                RewardLabel("Metalhead boss reward control", moby),
                "Metalhead boss reward control marker",
                $"Release review groups this type 0x40/source 0x30 record with Metalhead boss reward clusters{RewardEvidence(moby)}.",
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

        if (string.Equals(levelKey, "metalhead", StringComparison.OrdinalIgnoreCase) && moby.Type is 0x1A or 0x1C && moby.SourceByte36 == 0x66 && moby.Flag4B is >= 0x53 and <= 0x57)
        {
            identity = ReviewInferred(
                RewardLabel("Metalhead pylon reward control", moby),
                "Metalhead pylon/boss reward control marker",
                $"Release review groups this Metalhead reward-bearing special family with nearby pylon and boss reward support records{RewardEvidence(moby)}.",
                ColorRgba.FromRgb(230, 91, 67));
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
            case ("metalhead", 0x2A, 0xAA) when moby.Flag4B is 0x54 or 0x55:
                identity = SourceObserved("Bird support", "bird enemy support", "Metalhead type 0x2A source byte 0xAA reward records share source-disc special signature f872cc6f8303f645 with 21 exact Metalhead Bird records; the green- and blue-reward variants share the same level-local model family.", ColorRgba.FromRgb(230, 91, 67));
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
            !identity.Confidence.Contains("source-signature-observed", StringComparison.OrdinalIgnoreCase))
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
        if (incoming.Contains("balloonist") && existing.Contains("baloonist"))
            return true;
        if (incoming.Contains("egg thief") && existing.Contains("egg theif"))
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
            identity.Confidence.Contains("user", StringComparison.OrdinalIgnoreCase) ||
            identity.Confidence.Contains("live", StringComparison.OrdinalIgnoreCase) ||
            identity.Confidence.Contains("observed", StringComparison.OrdinalIgnoreCase) ||
            identity.Confidence.Contains("validated", StringComparison.OrdinalIgnoreCase);
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
}

public readonly record struct MobyIdentity(string Label, string Kind, string Confidence, string Evidence, ColorRgba Color);
