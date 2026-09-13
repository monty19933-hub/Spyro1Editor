namespace Spyro.Editor.Core.Editing;

/// <summary>
/// Neutral reference to an intended original retail row, not a captured asset.
/// The 4095 index limit bounds document ingestion; it is not an inventory or
/// native capacity claim. Actual family/dependency resolution may refuse it.
/// </summary>
public sealed class NativeFreshRetailMobyReference
{
    private NativeFreshRetailMobyReference(string sourceVariant,int sourceWadEntry,int sourceTrueIndex,int expectedNativeClass)
    {SourceVariant=sourceVariant;SourceWadEntry=sourceWadEntry;SourceTrueIndex=sourceTrueIndex;ExpectedNativeClass=expectedNativeClass;}

    public string SourceVariant{get;}
    public int SourceWadEntry{get;}
    public int SourceTrueIndex{get;}
    public int ExpectedNativeClass{get;}

    public static NativeFreshRetailMobyReference Create(string sourceVariant,int sourceWadEntry,int sourceTrueIndex,int expectedNativeClass)
    {
        ArgumentNullException.ThrowIfNull(sourceVariant);
        if(sourceVariant!=NativeFreshRetailTerrainMaterialIntent.SupportedSourceVariant||sourceWadEntry is <10 or >78||
            (sourceWadEntry&1)!=0||sourceTrueIndex is <0 or >4095||expectedNativeClass is <0 or >65535)
            throw new InvalidDataException("Native Moby reference requires the known retail source variant, an even data entry10..78, row index0..4095 and expected class0..65535; availability is not implied.");
        return new(sourceVariant,sourceWadEntry,sourceTrueIndex,expectedNativeClass);
    }
}

/// <summary>
/// Generic neutral native-Moby placement. XYZ are raw native coordinate integers,
/// not display units. XY containment belongs to the actual aggregate; Z0..FFFF
/// and native yaw0..255 are authoring bounds, not collision or import approval.
/// Native yaw is distinct from player spawn heading; no pitch/roll is authored.
/// </summary>
public sealed class NativeFreshMobyPlacementIntent:NativeFreshPlacementIntent
{
    private NativeFreshMobyPlacementIntent(string editorId,NativeFreshRetailMobyReference source,int x,int y,int z,int nativeYawByte):base(editorId)
    {Source=source;X=x;Y=y;Z=z;NativeYawByte=nativeYawByte;}

    public override string Tag=>"native-moby";
    public NativeFreshRetailMobyReference Source{get;}
    public int X{get;}
    public int Y{get;}
    public int Z{get;}
    public int NativeYawByte{get;}

    public static NativeFreshMobyPlacementIntent Create(string editorId,NativeFreshRetailMobyReference source,int x,int y,int z,int nativeYawByte)
    {
        ArgumentNullException.ThrowIfNull(source);
        if(z is <0 or >65535||nativeYawByte is <0 or >255)
            throw new InvalidDataException("Native Moby intent requires raw authored Z0..65535 and native yaw byte0..255 without snapping, clamping or wrapping.");
        return new(editorId,source,x,y,z,nativeYawByte);
    }
}
