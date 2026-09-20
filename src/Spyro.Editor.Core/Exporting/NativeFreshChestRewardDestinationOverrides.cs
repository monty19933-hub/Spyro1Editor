namespace Spyro.Editor.Core.Exporting;

internal enum NativeFreshChestRewardRole
{
    Reward1=0,
    Reward2=1,
    Reward3=2,
    Reward4=3,
    Reward5=4
}

internal sealed record NativeFreshChestRewardDestinationOverride(NativeFreshChestRewardRole Role,int X,int Y,int Z);

/// <summary>
/// Detached neutral authorship of one to five reward destinations in WORLD
/// coordinates, not chest-relative offsets or native trajectory evidence.
/// Only the source-owned placement producer can bind these values to D rows.
/// </summary>
internal sealed class NativeFreshChestRewardDestinationOverrides
{
    private NativeFreshChestRewardDestinationOverrides(NativeFreshChestRewardDestinationOverride[] values) =>
        Values=Array.AsReadOnly(values);

    internal IReadOnlyList<NativeFreshChestRewardDestinationOverride> Values{get;}

    internal static NativeFreshChestRewardDestinationOverrides Create(IReadOnlyList<NativeFreshChestRewardDestinationOverride> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        int count=values.Count;
        if(count is <1 or >5)
            throw new InvalidDataException("Reward destinations require one to five unique semantic reward roles.");
        var snapshot=new NativeFreshChestRewardDestinationOverride[count];
        bool[] roles=new bool[5];
        for(int i=0;i<count;i++)
        {
            var value=values[i]??throw new InvalidDataException("A reward destination cannot be null.");
            if(!Enum.IsDefined(value.Role)||roles[(int)value.Role])
                throw new InvalidDataException("Reward destinations require defined, unique Reward1 through Reward5 roles.");
            if(value.X is <0 or >262128||value.Y is <0 or >262128||value.Z is <0 or >262128)
                throw new InvalidDataException("Reward destinations must be finite native WORLD coordinates in0..262128.");
            roles[(int)value.Role]=true;
            snapshot[i]=new(value.Role,value.X,value.Y,value.Z);
        }
        if(values.Count!=count)
            throw new InvalidDataException("Reward destination count changed during bounded capture.");
        Array.Sort(snapshot,(left,right)=>left.Role.CompareTo(right.Role));
        return new(snapshot);
    }
}
