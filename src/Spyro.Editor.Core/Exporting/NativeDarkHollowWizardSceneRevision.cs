using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Closed ancestry adapter for the existing scene producer and the bounded
/// persistent one/two-instance producer. Never issued by count, hash or receipt.
/// This is source composition, not accepted project history or native runtime.
/// </summary>
internal sealed class NativeDarkHollowWizardSceneRevision
{
    private readonly NativeDarkHollowWizardScenePlan? _legacy;
    private readonly NativeDarkHollowWizardAppendPlan? _append;
    private readonly NativeDarkHollowWizardPlacementPlan? _legacyPlacement;
    private readonly NativeDarkHollowWizardAppendPlacementPlan? _placement;
    private readonly byte[] _original, _final;

    private NativeDarkHollowWizardSceneRevision(NativeDarkHollowWizardScenePlan scene,
        NativeDarkHollowWizardPlacementPlan? placement)
    {
        _legacy=scene;_legacyPlacement=placement;
        placement?.RequireSceneStage(scene);
        _original=scene.CopyOriginalScene();_final=placement?.CopyScene()??scene.CopyScene();
        RewardOwnerCount=1;PlacedCount=scene.PlacedCountAfter;DynamicCapacity=scene.DynamicCapacityAfter;
        RequireOriginal(_original);RequireScene(_final);
    }
    private NativeDarkHollowWizardSceneRevision(NativeDarkHollowWizardAppendPlan append,
        NativeDarkHollowWizardAppendPlacementPlan placement)
    {
        // Bound source-issued collections before copying any scene. Public
        // receipts are cross-checks only; the actual private ancestors authorize.
        int count=append.ProposedOwners.Entries.Count;
        if(count is <1 or >2)throw new InvalidDataException("Native composed Wizard profile supports exactly one or two source-issued owners.");
        if(!ReferenceEquals(placement.Appended,append)||!ReferenceEquals(placement.ProposedOwners,append.ProposedOwners))
            throw new InvalidDataException("Native composed placement must retain this actual append and ledger ancestor.");
        if(append.Families.Count!=count||placement.FamilyCount!=count||append.PlacedCountBefore!=128||
            append.PlacedCountAfter!=128+count||append.PlacedCountAfter>256||append.DynamicCapacityBefore!=91||
            append.FixupCountAfter!=147+2*count||append.DynamicCapacityAfter!=placement.DynamicCapacityAfter||
            append.PlacedCountAfter!=placement.PlacedCountAfter||append.FixupCountAfter!=placement.FixupCountAfter)
            throw new InvalidDataException("Native composed scene/ledger capacity or fixup binding changed.");
        var owners=append.CopyPlacementOwners();
        if(owners.Count!=count)throw new InvalidDataException("Native composed private role set differs from its ledger.");
        for(int i=0;i<count;i++)
        {
            var e=append.ProposedOwners.Entries[i];var f=append.Families[i];var p=placement.Families[i];
            var o=owners.Single(x=>x.FamilyId==e.FamilyId&&x.RoleId==e.RoleId);
            if(e.Retired||e.NativeIndex!=128+i||e.RoleId!="wizard"||e.NativeClass!=0x11B||
                e.Role.DonorLevelId!=33||e.Role.DonorTrueIndex!=6||f.NativeIndex!=e.NativeIndex||
                f.FamilyId!=e.FamilyId||f.SourceVariantId!=e.SourceVariantId||p.NativeIndex!=e.NativeIndex||p.FamilyId!=e.FamilyId||
                o.NativeIndex!=e.NativeIndex||o.Row.Space!=o.Properties.Space||o.Row.Offset!=0x8C38+e.NativeIndex*0x58||
                o.Row.ByteLength!=0x58||o.Properties.Offset!=0xEED4+i*0x50||o.Properties.ByteLength!=0x50||
                o.Row.Space.Revision!=NativeAddressRevision.Final||o.Row.Space.Kind!=NativeAddressKind.Scene||o.Row.Space.ByteLength!=0x11800)
                throw new InvalidDataException("Native composed reward role does not own its exact stable row/private extent.");
            if(i>0&&o.Row.Space!=owners[0].Row.Space)
                throw new InvalidDataException("Native composed rows do not share the allocator's canonical scene domain.");
        }
        _append=append;_placement=placement;
        _original=append.CopyOriginalScene();_final=placement.CopyScene();
        RewardOwnerCount=count;PlacedCount=append.PlacedCountAfter;DynamicCapacity=append.DynamicCapacityAfter;
        RequireOriginal(_original);RequireScene(_final);
    }
    internal static NativeDarkHollowWizardSceneRevision FromLegacy(NativeDarkHollowWizardScenePlan scene,
        NativeDarkHollowWizardPlacementPlan? placement)=>new(scene,placement);
    internal static NativeDarkHollowWizardSceneRevision FromAppend(NativeDarkHollowWizardAppendPlan append,
        NativeDarkHollowWizardAppendPlacementPlan placement)
    {ArgumentNullException.ThrowIfNull(append);ArgumentNullException.ThrowIfNull(placement);return new(append,placement);}
    internal bool PersistentOwners=>_append!=null;
    internal int RewardOwnerCount { get; }
    internal int PlacedCount { get; }
    internal int DynamicCapacity { get; }
    internal string? PlacementPlanSha256=>_placement?.PlanSha256??_legacyPlacement?.PlanSha256;
    internal byte[] CopyScene()=>_final.ToArray();
    internal NativeSkyRelocationPayload CopyOriginalAddressPayload(int index)=>
        (_append?.CopyOriginalAddressPayload(index)??_legacy!.CopyOriginalAddressPayload(index)) with{Bytes=CopyScene()};
    internal void RequireOriginal(ReadOnlySpan<byte> scene)
    {
        if(scene.Length!=0x11800||Convert.ToHexStringLower(SHA256.HashData(scene))!="c48c344d3809f1ea5761e968ce6f18b02d5c7bbb82fcec4a7fd453343dbb84f8"||!scene.SequenceEqual(_original))
            throw new InvalidDataException("Native composed scene is not the exact original Dark owner.");
        if(_append!=null)_append.VerifyOriginalAndFinal(scene,_append.CopyScene());
        else _legacy!.VerifyOriginalAndFinal(scene,_legacy.CopyScene());
    }
    internal void RequireInstalled(ReadOnlySpan<byte> data)
    {
        if(data.Length<0x800)throw new InvalidDataException("Native composed scene header is missing.");
        uint at=Word(data,0x18),length=Word(data,0x1C);
        if(length!=0x11800||at>data.Length||length>data.Length-at)throw new InvalidDataException("Native composed scene descriptor is outside its actual data owner.");
        RequireScene(data.Slice((int)at,(int)length));
    }
    private void RequireScene(ReadOnlySpan<byte> scene)
    {
        if(!scene.SequenceEqual(_final)||Word(scene,0x8C34)!=PlacedCount||
            !scene.Slice(0x8C38,128*0x58).SequenceEqual(_original.AsSpan(0x8C38,128*0x58)))
            throw new InvalidDataException("Native composed scene count, original rows or complete placement bytes changed.");
        if(_placement!=null)_placement.RequireOriginalAndFinal(_append!.CopyScene(),scene);
        else if(_legacyPlacement!=null)_legacyPlacement.RequireOriginalAndFinal(_legacy!.CopyScene(),scene);
        for(int i=0;i<PlacedCount;i++)
        {
            var row=scene.Slice(0x8C38+i*0x58,0x58);
            bool wizard=BinaryPrimitives.ReadUInt16LittleEndian(row[0x36..])==0x11B;
            if(wizard!=(i>=128)||wizard&&(row[0x53]!=0x56||row[0x3A]!=0x7F||row[0x48]!=0||row[0x49]!=0))
                throw new InvalidDataException("Native composed Wizard class/initial reward/state differs from its source-owned role.");
        }
    }
    private static uint Word(ReadOnlySpan<byte> bytes,int offset)=>BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
}
