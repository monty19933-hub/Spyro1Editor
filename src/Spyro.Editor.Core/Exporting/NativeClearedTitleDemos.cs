using System.Buffers.Binary;

namespace Spyro.Editor.Core.Exporting;

/// <summary>Redirects only complete original title-demo records whose retail
/// levels were cleared. The untouched Icy Flight record supplies level, frame
/// count and pose together; the native four-slot sequencer is unchanged.</summary>
public static class NativeClearedTitleDemos
{
    public const string ProfileId="native-cleared-title-demo-original-icy-flight-record-usa-v1";
    private const int LevelOffset=0x5F67C,FrameOffset=0x5F68C,PoseOffset=0x5F69C;
    private const int ReplacementIndex=2;
    private static readonly int[] OriginalLevels=[11,24,55,33];
    private static readonly (int Offset,int Length)[] Consumers=
        [(0x48A0,0x84),(0x233E0,0x4C),(0x23D0C,0x38),(0x23EDC,0x30),(0x24024,0x20)];

    public static byte[] Prepare(byte[] originalExe,byte[] stagedExe,IReadOnlySet<int> clearedLevelIds)
    {
        ArgumentNullException.ThrowIfNull(clearedLevelIds);
        NativeId65FreshExecutablePlan.RequireOriginal(originalExe);
        if(stagedExe.Length!=originalExe.Length || clearedLevelIds.Any(id=>!NativeFreeFlightCompositionPlan.IsNormalLevel(id)))
            throw new InvalidDataException("Title-demo retirement requires normal cleared retail slots and the exact executable extent.");
        // The same index g800757B8 selects level and duration with index*4,
        // and all four pose words with index*16. Its only native sequencer use
        // increments and masks to3. These complete source consumer spans remain
        // exact; no code, timing policy or per-index branch is rewritten here.
        foreach(var (offset,length) in Consumers)
            if(!originalExe.AsSpan(offset,length).SequenceEqual(stagedExe.AsSpan(offset,length)))
                throw new InvalidDataException($"Title-demo consumer changed at executable file0x{offset:X}.");
        for(int i=0;i<4;i++)
        {
            if(Word(originalExe,LevelOffset+i*4)!=OriginalLevels[i])
                throw new InvalidDataException("Original title-demo parallel level table changed.");
            if(!SameRecord(stagedExe,i,originalExe,i) && !SameRecord(stagedExe,i,originalExe,ReplacementIndex))
                throw new InvalidDataException($"Title-demo slot{i} is neither its complete source record nor the owned Icy Flight replacement.");
        }
        // IcyFlight55 is excluded from StartBlank and its record remains a
        // source-owned fallback. A staged mutation of this donor is a conflict.
        if(!SameRecord(stagedExe,ReplacementIndex,originalExe,ReplacementIndex))
            throw new InvalidDataException("The untouched native Icy Flight title-demo donor changed.");
        byte[] result=stagedExe.ToArray();
        for(int i=0;i<4;i++)
        {
            int donor=clearedLevelIds.Contains(OriginalLevels[i])?ReplacementIndex:i;
            originalExe.AsSpan(LevelOffset+donor*4,4).CopyTo(result.AsSpan(LevelOffset+i*4));
            originalExe.AsSpan(FrameOffset+donor*4,4).CopyTo(result.AsSpan(FrameOffset+i*4));
            originalExe.AsSpan(PoseOffset+donor*16,16).CopyTo(result.AsSpan(PoseOffset+i*16));
        }
        return result;
    }

    private static bool SameRecord(byte[] current,int slot,byte[] source,int donor)=>
        current.AsSpan(LevelOffset+slot*4,4).SequenceEqual(source.AsSpan(LevelOffset+donor*4,4)) &&
        current.AsSpan(FrameOffset+slot*4,4).SequenceEqual(source.AsSpan(FrameOffset+donor*4,4)) &&
        current.AsSpan(PoseOffset+slot*16,16).SequenceEqual(source.AsSpan(PoseOffset+donor*16,16));
    private static uint Word(byte[] bytes,int offset)=>BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset));
}
