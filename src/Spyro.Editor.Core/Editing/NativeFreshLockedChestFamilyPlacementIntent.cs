using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.Core.Editing;

/// <summary>
/// Neutral reference to one versioned family definition. This does not resolve
/// source bytes, native rows, dependency ownership or export/runtime approval.
/// </summary>
public sealed class NativeFreshLockedChestFamilyReference
{
    public const string SupportedDefinitionId="peace-keepers-locked-chest-v1";
    private NativeFreshLockedChestFamilyReference(string sourceVariant,string definitionId)
    {SourceVariant=sourceVariant;DefinitionId=definitionId;}

    public string SourceVariant{get;}
    public string DefinitionId{get;}

    public static NativeFreshLockedChestFamilyReference Create(string sourceVariant,string definitionId)
    {
        ArgumentNullException.ThrowIfNull(sourceVariant);ArgumentNullException.ThrowIfNull(definitionId);
        if(sourceVariant!=NativeFreshRetailTerrainMaterialIntent.SupportedSourceVariant||definitionId!=SupportedDefinitionId)
            throw new InvalidDataException("Locked-chest family intent requires the exact supported retail variant and peace-keepers-locked-chest-v1 definition; no source availability is implied.");
        return new(sourceVariant,definitionId);
    }
}

/// <summary>
/// One neutral authored family with independent WORLD key/chest anchors and
/// optional fixed WORLD reward destinations. Moving anchors never translates
/// explicit destinations. Native rows, parent relationships, yaw, behavior and
/// singleton/resource admission belong to a later source-owned compiler.
/// </summary>
public sealed class NativeFreshLockedChestFamilyPlacementIntent:NativeFreshPlacementIntent
{
    private NativeFreshLockedChestFamilyPlacementIntent(string editorId,NativeFreshLockedChestFamilyReference source,
        int keyX,int keyY,int keyZ,int chestX,int chestY,int chestZ,NativeFreshChestRewardDestinationOverrides? rewardDestinations):base(editorId)
    {
        Source=source;KeyX=keyX;KeyY=keyY;KeyZ=keyZ;ChestX=chestX;ChestY=chestY;ChestZ=chestZ;
        RewardDestinations=rewardDestinations;
    }

    public override string Tag=>"locked-chest-family";
    public NativeFreshLockedChestFamilyReference Source{get;}
    public int KeyX{get;}
    public int KeyY{get;}
    public int KeyZ{get;}
    public int ChestX{get;}
    public int ChestY{get;}
    public int ChestZ{get;}
    public NativeFreshChestRewardDestinationOverrides? RewardDestinations{get;}

    public static NativeFreshLockedChestFamilyPlacementIntent Create(string editorId,NativeFreshLockedChestFamilyReference source,
        int keyX,int keyY,int keyZ,int chestX,int chestY,int chestZ,NativeFreshChestRewardDestinationOverrides? rewardDestinations=null)
    {
        ArgumentNullException.ThrowIfNull(source);
        if(new[]{keyX,keyY,keyZ,chestX,chestY,chestZ}.Any(v=>v is <0 or >262128))
            throw new InvalidDataException("Locked-chest family anchors require WORLD XYZ in0..262128 without snapping, clamping or wrapping.");
        return new(editorId,source,keyX,keyY,keyZ,chestX,chestY,chestZ,rewardDestinations);
    }

    public NativeFreshLockedChestFamilyPlacementIntent WithAnchors(int keyX,int keyY,int keyZ,int chestX,int chestY,int chestZ)=>
        Create(EditorId,Source,keyX,keyY,keyZ,chestX,chestY,chestZ,RewardDestinations);

    public NativeFreshLockedChestFamilyPlacementIntent WithRewardDestinations(NativeFreshChestRewardDestinationOverrides? rewardDestinations)=>
        Create(EditorId,Source,KeyX,KeyY,KeyZ,ChestX,ChestY,ChestZ,rewardDestinations);
}
