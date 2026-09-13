using System.Buffers.Binary;
using Spyro.Editor.Core.Analysis;

namespace Spyro.Editor.Core.Exporting;

internal sealed record NativeFreshFamilyStaticRewardRow(int PlacedOrdinal,ushort ActorClassId,byte ActorKind,
    byte RewardClassId,int StaticValue,int SceneRowOffset,string SceneRowSha256,string FamilyAssetPlanSha256);

// Initial deterministic treasure only. FF is not a universal no-drop sentinel:
// the original emitter masks it to7F and takes its RNG path if actually called.
internal sealed class NativeFreshFamilyStaticRewardCensus
{
    private NativeFreshFamilyStaticRewardCensus(NativeFreshFamilyStaticRewardRow[] rows)
    {Rows=Array.AsReadOnly(rows);StaticTreasureTarget=checked(rows.Sum(row=>row.StaticValue));}
    public IReadOnlyList<NativeFreshFamilyStaticRewardRow> Rows{get;}
    public int PlacedCount=>Rows.Count;
    public int StaticTreasureTarget{get;}
    public int TargetSlot=>35;
    public int TargetFileOffset=>0x5FC7E;
    public bool RuntimeDropGuarantee=>false;
    internal static NativeId65EngineEvidence SelectorEvidence=>new("native-initial-selector-range-not-universal-no-drop","EXE",
        0x2B3C0,0x51C,"979e567dd4bebbdd9cadaf8f884e1110be16f293c6590ba6214941be5be7a4a8");
    internal static NativeFreshFamilyStaticRewardCensus Capture(NativeFreshMobyFamilyCompositionPlan family,
        ReadOnlySpan<byte> original,ReadOnlySpan<byte> scene,int wizardValue)
    {
        NativeId65FreshWizardResearchExecutableSource.RequireFamilyComposition(family);
        NativeId65FreshExecutablePlan.RequireOriginal(original);
        if(!scene.SequenceEqual(family.CopyScene())||wizardValue!=10)
            throw new InvalidDataException("Family census requires the complete actual composed scene and pinned native yellow value.");
        var evidence=SelectorEvidence;
        if(Hash(original.Slice((int)evidence.Offset,evidence.ByteLength))!=evidence.Sha256||
            Word(original,0x2B3F4)!=0x92620053||Word(original,0x2B3FC)!=0x3050007F||
            Word(original,0x2B400)!=0x2602FFFF||Word(original,0x2B404)!=0x2C42007E||
            Word(original,0x2B408)!=0x10400009||Word(original,0x2B40C)!=0x00E0B021)
            throw new InvalidDataException("Native selector load/mask/range/branch/delay owner changed.");
        var grass=family.Placements[1].PassiveAsset!;var proof=family.Placements[1].Dispatch!;
        grass.RequireRetainedOwners();proof.RequireAncestry(family.BaseBodyAncestor.HostAncestor,grass);
        byte[] donor=grass.CopyRow();var wizard=scene.Slice(0xD4,0x58);var passive=scene.Slice(0x12C,0x58);
        if(Word(scene,0xD0)!=2||Half(wizard,0x36)!=0x11B||wizard[0x3A]!=0x7F||wizard[0x53]!=0x56||
            Half(passive,0x36)!=0x1F5||passive[0x3A]!=donor[0x3A]||passive[0x53]!=0xFF||
            Word(passive,8)!=0||passive[0x4F]!=0||passive[0x50]!=0x20||passive[0x51]!=0||passive[0x52]!=0x10||
            !proof.SourceCertified||!proof.PassiveDispatchCompatible||proof.CallInstructionCount!=0||
            proof.PropertiesReadCount!=0||proof.RuntimeActorWriteCount!=0)
            throw new InvalidDataException("Family census lost its actual initial Wizard or exact passive Grass identity/dispatch.");
        var result=new NativeFreshFamilyStaticRewardCensus([
            new(0,0x11B,wizard[0x3A],wizard[0x53],wizardValue,0xD4,Hash(wizard),family.Placements[0].WizardAsset!.PlanSha256),
            new(1,0x1F5,passive[0x3A],passive[0x53],0,0x12C,Hash(passive),grass.PlanSha256)]);
        if(result.StaticTreasureTarget!=10||Half(original,result.TargetFileOffset)!=0)
            throw new InvalidDataException("Family static target escaped its signed native slot35 preimage and exact10 sum.");
        return result;
    }
    private static uint Word(ReadOnlySpan<byte> b,int at)=>BinaryPrimitives.ReadUInt32LittleEndian(b[at..]);
    private static ushort Half(ReadOnlySpan<byte> b,int at)=>BinaryPrimitives.ReadUInt16LittleEndian(b[at..]);
    private static string Hash(ReadOnlySpan<byte> b)=>NativeId65FreshExecutablePlan.Hash(b);
}
