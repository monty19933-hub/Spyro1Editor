using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.Core.Exporting;

public sealed record NativeClearedRetailExecutableChange(int FileOffset, int ByteLength, string BeforeSha256, string AfterSha256);
public sealed record NativeClearedRetailBindingProfile(int LevelId, uint InitializerAddress, int InitializerByteLength,
    bool NineSettersSupported, int ContinuousIndex, bool HasRetailEggGoal);
public sealed record NativeClearedRetailBindingSummary(string ProfileId, NativeClearedRetailBindingProfile Target,
    string ExecutableSha256, IReadOnlyList<NativeClearedRetailExecutableChange> Changes);

/// <summary>Rebinds only a retail slot's privately owned full nine-setter initializer
/// to the exact Town engine profile. This does not issue a level body or certify entry,
/// exits, persistence, title demos, minimum retained assets or native gameplay.</summary>
public static class NativeClearedRetailBinding
{
    public const string ProfileId = "native-cleared-retail-owned-nine-setter-town-engine-usa-v1";
    private const int JumpTableOffset = 0x1BA4;
    private const int TownInitializerOffset = 0x4B098, FullLength = 0x98, ShortLength = 0x50;
    private const int CommonTailOffset = 0x4BEE0;
    private static readonly uint[] Globals = [0x800785D8,0x800758CC,0x80075734,0x800758E4,0x800756BC,
        0x800757A0,0x800758A8,0x8007574C,0x800758D8];

    public static IReadOnlyList<NativeClearedRetailBindingProfile> Census(byte[] originalExe)
    {
        NativeId65FreshExecutablePlan.RequireOriginal(originalExe);
        return Enumerable.Range(10,55).Where(NativeFreeFlightCompositionPlan.IsNormalLevel)
            .Select(id => Inspect(originalExe,id)).ToArray();
    }

    public static byte[] Prepare(byte[] originalExe, byte[] stagedExe, LevelDefinition target)
    {
        ArgumentNullException.ThrowIfNull(target);
        RequirePair(originalExe,stagedExe);
        if (!NativeFreeFlightCompositionPlan.IsNormalLevel(target.LevelId))
            throw new InvalidDataException("Start Blank requires a supported normal retail level slot.");
        int index = (target.LevelId / 10 - 1) * 6 + target.LevelId % 10;
        if (target.SourceWadEntry != 10 + 2 * index)
            throw new InvalidDataException("Start Blank retail identity differs from its source WAD slot.");
        var profile = Inspect(originalExe,target.LevelId);
        if (!profile.NineSettersSupported) return PrepareRelocated(originalExe,stagedExe,target,profile,index);
        // These shared owners are never borrowed for a target-specific rewrite.
        RequireUnchanged(originalExe,stagedExe,JumpTableOffset+target.LevelId*4,4,"target initializer entry");
        RequireUnchanged(originalExe,stagedExe,CommonTailOffset - 8,0x20,"normal initializer tail");
        RequireGoalConsumers(originalExe,stagedExe);
        int offset = checked((int)(profile.InitializerAddress - UniversalExeBindings.ExeBase));
        ReadOnlySpan<byte> current = stagedExe.AsSpan(offset,FullLength);
        ReadOnlySpan<byte> neutral = originalExe.AsSpan(TownInitializerOffset,FullLength);
        if (!current.SequenceEqual(originalExe.AsSpan(offset,FullLength)) && !current.SequenceEqual(neutral))
            throw new InvalidDataException("Start Blank conflicts with an already edited retail initializer; rebuild its own source stage first.");
        for(int id=0;id<66;id++)
        {
            if(id==target.LevelId)continue;
            uint entry=Word(stagedExe,JumpTableOffset+id*4);
            if(entry>=profile.InitializerAddress && entry<profile.InitializerAddress+FullLength &&
                (entry!=profile.InitializerAddress || !current.SequenceEqual(neutral)))
                throw new InvalidDataException("Start Blank would change an initializer also entered by another staged level.");
        }
        byte[] result = stagedExe.ToArray();
        neutral.CopyTo(result.AsSpan(offset,FullLength));
        Reset(0x5FC14 + index,1,"dragon");
        Reset(0x5FC38 + index * 2,2,"treasure");
        // Retail egg inventory is bounded to worlds1..3; later indexes overlap
        // unrelated strings. Missing later goal slots mean no goal, never permission
        // to extend the table or clear those strings.
        if (index < 18) Reset(0x5FC80 + index,1,"retail egg");
        return result;

        void Reset(int at,int length,string goal)
        {
            var value=stagedExe.AsSpan(at,length);
            if (!value.SequenceEqual(originalExe.AsSpan(at,length)) && value.ContainsAnyExcept((byte)0))
                throw new InvalidDataException($"Start Blank conflicts with the staged {goal} goal; authored/imported totals cannot be discarded silently.");
            result.AsSpan(at,length).Clear();
        }
    }

    // Levels with the native four-setter short initializer (Dark Hollow, Toasty, ...) have no room for the nine
    // Town setters. Their copy goes into a slot of retail libgpu functions that nothing in the executable or any
    // WAD entry calls, jumps or points to (0x8005E8D8..0x8005EA94: three image-transfer wrappers), and the level's
    // jump-table entry moves there. The short initializer itself stays untouched. Each slot is either retail bytes
    // (free) or an exact neutral copy owned by the one level whose entry points at it.
    internal const uint RelocationAreaStart = 0x8005E8D8, RelocationAreaEnd = 0x8005EA94;
    internal const string RelocationAreaSha256 = "073d8dac8fa86d18fc623f021041d91455830f83893183fc0876c2efc663f171";
    public static IReadOnlyList<uint> RelocationSlots { get; } = [0x8005E8D8, 0x8005E970];

    private static byte[] PrepareRelocated(byte[] originalExe, byte[] stagedExe, LevelDefinition target,
        NativeClearedRetailBindingProfile profile, int index)
    {
        int area = checked((int)(RelocationAreaStart - UniversalExeBindings.ExeBase));
        if (Hash(originalExe.AsSpan(area, (int)(RelocationAreaEnd - RelocationAreaStart))) != RelocationAreaSha256)
            throw new InvalidDataException("Start Blank's initializer area differs from the original executable.");
        int entryAt = JumpTableOffset + target.LevelId * 4;
        uint original = Word(originalExe, entryAt), current = Word(stagedExe, entryAt);
        byte[] neutral = originalExe.AsSpan(TownInitializerOffset, FullLength).ToArray();
        RequireUnchanged(originalExe, stagedExe, CommonTailOffset - 8, 0x20, "normal initializer tail");
        RequireGoalConsumers(originalExe, stagedExe);
        // The short initializer is never rewritten, and no other level may have been moved onto it.
        int shortAt = checked((int)(profile.InitializerAddress - UniversalExeBindings.ExeBase));
        RequireUnchanged(originalExe, stagedExe, shortAt, profile.InitializerByteLength, "short initializer");
        uint slot;
        if (current == original)
        {
            uint? free = RelocationSlots.Cast<uint?>().FirstOrDefault(s => SlotIsRetail(s!.Value) && !Entered(s.Value));
            slot = free ?? throw new InvalidDataException("Every Start Blank initializer slot for levels like this one is already in use in this game.");
        }
        else if (RelocationSlots.Contains(current) && SlotIsNeutral(current)) slot = current;
        else throw new InvalidDataException("Start Blank conflicts with an already edited initializer entry for this level.");
        for (int id = 0; id < 66; id++)
            if (id != target.LevelId && Word(stagedExe, JumpTableOffset + id * 4) is uint other && other >= slot && other < slot + FullLength)
                throw new InvalidDataException("Start Blank would share an initializer slot with another level.");
        byte[] result = stagedExe.ToArray();
        neutral.CopyTo(result.AsSpan(checked((int)(slot - UniversalExeBindings.ExeBase)), FullLength));
        Put(result, entryAt, slot);
        Reset(0x5FC14 + index, 1, "dragon");
        Reset(0x5FC38 + index * 2, 2, "treasure");
        if (index < 18) Reset(0x5FC80 + index, 1, "retail egg");
        return result;

        bool SlotIsRetail(uint s) { int o = checked((int)(s - UniversalExeBindings.ExeBase)); return stagedExe.AsSpan(o, FullLength).SequenceEqual(originalExe.AsSpan(o, FullLength)); }
        bool SlotIsNeutral(uint s) => stagedExe.AsSpan(checked((int)(s - UniversalExeBindings.ExeBase)), FullLength).SequenceEqual(neutral);
        bool Entered(uint s) => Enumerable.Range(0, 66).Any(id => Word(stagedExe, JumpTableOffset + id * 4) is uint e && e >= s && e < s + FullLength);
        void Reset(int at, int length, string goal)
        {
            var value = stagedExe.AsSpan(at, length);
            if (!value.SequenceEqual(originalExe.AsSpan(at, length)) && value.ContainsAnyExcept((byte)0))
                throw new InvalidDataException($"Start Blank conflicts with the staged {goal} goal; authored/imported totals cannot be discarded silently.");
            result.AsSpan(at, length).Clear();
        }
    }

    /// <summary>Describes the actual source-stage delta; no body/runtime authority.</summary>
    public static NativeClearedRetailBindingSummary Summarize(byte[] originalExe,byte[] stagedExe,LevelDefinition target)
    {
        byte[] after=Prepare(originalExe,stagedExe,target);
        List<NativeClearedRetailExecutableChange> changes=[];
        for(int at=0;at<after.Length;)
        {
            if(stagedExe[at]==after[at]){at++;continue;}
            int start=at++;
            while(at<after.Length && stagedExe[at]!=after[at])at++;
            changes.Add(new(start,at-start,Hash(stagedExe.AsSpan(start,at-start)),Hash(after.AsSpan(start,at-start))));
        }
        return new(ProfileId,Inspect(originalExe,target.LevelId),Hash(after),changes.AsReadOnly());
    }

    /// <summary>Changes only the two original New Game level immediates. Existing
    /// owned normal/65 selections are allowed; native save loading remains unchanged.</summary>
    public static void ApplyNewGameStart(byte[] originalExe,byte[] stagedExe,int levelId)
    {
        RequirePair(originalExe,stagedExe);
        if (!NativeFreeFlightCompositionPlan.IsNormalLevel(levelId) && levelId!=65)
            throw new InvalidDataException("New Game start requires a normal retail slot or authored65.");
        Guard(0x2F7C,0x44,0x2F80);
        Guard(0x1DC40,0x114,0x1DCF0);
        Guard(0x23308,0x150,-1);
        Guard(0x49D94,0x74,-1);
        if(Word(stagedExe,0x2F80)!=Word(stagedExe,0x1DCF0))
            throw new InvalidDataException("New Game start owners disagree; a mixed selection cannot be discarded silently.");
        foreach(int at in new[]{0x2F80,0x1DCF0}) Put(stagedExe,at,0x24020000u | (uint)levelId);

        void Guard(int start,int length,int change)
        {
            for(int at=start;at<start+length;at+=4)
            {
                uint before=Word(originalExe,at),current=Word(stagedExe,at);
                if(current==before)continue;
                int prior=(ushort)current;
                if(at!=change || before!=0x2402000A || (current & 0xFFFF0000)!=0x24020000 ||
                    !NativeFreeFlightCompositionPlan.IsNormalLevel(prior) && prior!=65)
                    throw new InvalidDataException($"New Game start conflicts with its original instruction owner at 0x{at:X}.");
            }
        }
    }

    private static NativeClearedRetailBindingProfile Inspect(byte[] original,int id)
    {
        uint address=Word(original,JumpTableOffset+id*4);
        int offset=checked((int)(address-UniversalExeBindings.ExeBase));
        if(offset<0 || offset>original.Length-FullLength)
            throw new InvalidDataException("Retail initializer leaves the exact executable owner.");
        // Complete source preimage means every instruction is known. Require the
        // native grammar too, so this profile cannot acquire hidden side effects.
        bool full=Word(original,offset+0x90)==0x08016DB8 && Word(original,offset+0x94)==0;
        int length=full?FullLength:ShortLength;
        for(int id2=0;id2<66;id2++)
        {
            if(id2==id)continue;
            uint other=Word(original,JumpTableOffset+id2*4);
            if(other>=address && other<address+length)
                throw new InvalidDataException("Retail initializer is shared or overlaps another level entry.");
        }
        int setters=full?9:4;
        for(int i=0;i<setters;i++) RequireSetter(original,offset+i*16,Globals[i]);
        if(!full)
        {
            if(Word(original,offset+0x40)>>16!=0x3C02 || Word(original,offset+0x44)>>16!=0x2442 ||
                Word(original,offset+0x48)!=0x08016DB6 || Word(original,offset+0x4C)!=0)
                throw new InvalidDataException("Retail short initializer differs from the native four-setter/tick-tail grammar.");
        }
        ReadOnlySpan<uint> tail=[0x3C028008,0x8C4285D8,0x3C018008,0xAC2285DC,0x03E00008,0];
        for(int i=0;i<tail.Length;i++)if(Word(original,CommonTailOffset+i*4)!=tail[i])
            throw new InvalidDataException("Retail initializer common tail changed.");
        int index=(id/10-1)*6+id%10;
        return new(id,address,length,full,index,index<18);
    }

    private static void RequireSetter(byte[] original,int offset,uint global)
    {
        uint lui=Word(original,offset),addiu=Word(original,offset+4);
        if(lui>>16!=0x3C02 || addiu>>16!=0x2442 ||
            Word(original,offset+8)!=(0x3C010000u|((global+0x8000)>>16)) ||
            Word(original,offset+12)!=(0xAC220000u|(global&0xFFFF)))
            throw new InvalidDataException("Retail initializer setter contains unsupported instructions or global ownership.");
    }

    private static void RequireGoalConsumers(byte[] original,byte[] staged)
    {
        // Full native indexing/field load spans: unsigned byte dragon/egg goals,
        // signed halfword gem goals. Keep both egg readers and gem completion.
        foreach(var (offset,length) in new[]{(0xC910,0x40),(0xC8CC,0x34),(0xCB30,0x2C),(0xCC98,0x34),(0x45450,0x2C)})
            RequireUnchanged(original,staged,offset,length,"retail goal consumer");
        if(Word(original,0xC934)!=0x90420000 || Word(original,0xC8E8)!=0x8426F438 ||
            Word(original,0xCB4C)!=0x9022F480 || Word(original,0xCCBC)!=0x9022F480 || Word(original,0x45468)!=0x8423F438)
            throw new InvalidDataException("Retail goal field widths differ from their native consumers.");
    }

    private static void RequirePair(byte[] original,byte[] staged)
    {
        NativeId65FreshExecutablePlan.RequireOriginal(original);
        if(staged.Length!=original.Length)throw new InvalidDataException("Retail staged executable extent changed.");
    }
    private static void RequireUnchanged(byte[] original,byte[] staged,int offset,int length,string owner)
    {
        if(!original.AsSpan(offset,length).SequenceEqual(staged.AsSpan(offset,length)))
            throw new InvalidDataException($"Start Blank conflicts with its source-owned {owner} at 0x{offset:X}.");
    }
    private static uint Word(byte[] bytes,int offset)=>BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset));
    private static void Put(byte[] bytes,int offset,uint word)=>BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset),word);
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
}
