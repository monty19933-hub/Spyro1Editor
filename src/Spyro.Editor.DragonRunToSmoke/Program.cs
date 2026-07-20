using System.Buffers.Binary;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Workspace;

const int ExpectedDragonLevels = 28;
const int ExpectedDragonScenes = 79;

string? workspaceArgument = args.FirstOrDefault(argument => !argument.StartsWith("--", StringComparison.Ordinal));
EditorWorkspace workspace = EditorWorkspace.Find(workspaceArgument);
LevelCatalog catalog = LevelCatalog.Load(workspace.RootPath);
string sourceImage = DiscImageLocator.FindImage(workspace);
if (!File.Exists(sourceImage))
    throw new FileNotFoundException("Dragon run-to smoke needs the configured clean Spyro BIN.", sourceImage);

int dragonLevels = 0;
int dragonScenes = 0;
int signedAngles = 0;
int minimumRadius = int.MaxValue;
int maximumRadius = int.MinValue;
double maximumRoundTripError = 0;
List<string> auxiliaryOutliers = [];
HashSet<long> packedPropertiesOffsets = [];
DragonRescueCameraData? focusedScene = null;
LevelDefinition? focusedLevel = null;
byte[]? focusedProperties = null;

foreach (LevelDefinition level in catalog.Levels.OrderBy(level => level.LevelId))
{
    IReadOnlyDictionary<int, DragonRescueCameraData> scenes = DragonRescueCameraLocator.Locate(sourceImage, level);
    if (scenes.Count == 0)
        continue;

    dragonLevels++;
    dragonScenes += scenes.Count;
    foreach (DragonRescueCameraData scene in scenes.Values.OrderBy(scene => scene.DragonTrueIndex))
    {
        if (!packedPropertiesOffsets.Add(scene.CameraDataWadOffset))
            throw new InvalidOperationException($"{level.Key} T{scene.DragonTrueIndex} shares packed properties offset 0x{scene.CameraDataWadOffset:X}.");

        if (scene.RunToAngle < 0)
            signedAngles++;
        minimumRadius = Math.Min(minimumRadius, scene.RunToRadius);
        maximumRadius = Math.Max(maximumRadius, scene.RunToRadius);
        if (DragonRescueRunTo.NormalizeAngle(scene.RunToAuxiliary) != DragonRescueRunTo.NormalizeAngle(scene.RunToAngle))
            auxiliaryOutliers.Add($"{level.Key}:T{scene.DragonTrueIndex}");

        DragonRunToEndpoint decoded = DragonRescueRunTo.DecodeEndpoint(
            scene.DragonRawX,
            scene.DragonRawY,
            scene.RunToAngle,
            scene.RunToRadius);
        if (decoded != scene.RunToEndpoint)
            throw new InvalidOperationException($"{level.Key} T{scene.DragonTrueIndex} locator endpoint does not match the retail Q12 decode.");

        DragonRunToEncodingResult encoded = DragonRescueRunTo.EncodeEndpoint(
            scene.DragonRawX,
            scene.DragonRawY,
            scene.RunToRawX,
            scene.RunToRawY,
            new DragonRunToEncoding(scene.RunToAngle, scene.RunToRadius));
        maximumRoundTripError = Math.Max(maximumRoundTripError, encoded.QuantizationErrorRaw);
        if (encoded.Validation.Status != MobyBuildSafetyStatus.Stable ||
            encoded.EncodedEndpoint != scene.RunToEndpoint ||
            DragonRescueRunTo.NormalizeAngle(encoded.Encoding.Angle) != DragonRescueRunTo.NormalizeAngle(scene.RunToAngle) ||
            encoded.Encoding.Radius != scene.RunToRadius)
        {
            throw new InvalidOperationException(
                $"{level.Key} T{scene.DragonTrueIndex} native run-to ({scene.RunToAngle}, {scene.RunToRadius}) round-tripped as " +
                $"({encoded.Encoding.Angle}, {encoded.Encoding.Radius}) with {encoded.QuantizationErrorRaw:0.###} raw error.");
        }

        byte[] packedProperties = DragonRescueRunTo.ReadPackedProperties(sourceImage, scene);
        DragonRunToPatchPlan noOpPlan = DragonRescueRunTo.BuildPatchPlan(
            scene,
            scene.DragonRawX,
            scene.DragonRawY,
            scene.RunToRawX,
            scene.RunToRawY,
            packedProperties);
        if (!noOpPlan.CanPatch || noOpPlan.PatchCount != 0)
            throw new InvalidOperationException($"{level.Key} T{scene.DragonTrueIndex} native endpoint did not plan as a no-op.");

        if (focusedScene == null && string.Equals(level.Key, "artisans", StringComparison.OrdinalIgnoreCase))
        {
            focusedScene = scene;
            focusedLevel = level;
            focusedProperties = packedProperties;
        }
    }
}

if (dragonLevels != ExpectedDragonLevels || dragonScenes != ExpectedDragonScenes)
{
    throw new InvalidOperationException(
        $"Dragon run-to audit found {dragonScenes} scene(s) across {dragonLevels} level(s); expected {ExpectedDragonScenes} across {ExpectedDragonLevels}.");
}
if (minimumRadius != DragonRescueRunTo.NativeMinimumRadiusRaw || maximumRadius != DragonRescueRunTo.NativeMaximumRadiusRaw)
{
    throw new InvalidOperationException(
        $"Dragon run-to radius audit found {minimumRadius}-{maximumRadius}; expected " +
        $"{DragonRescueRunTo.NativeMinimumRadiusRaw}-{DragonRescueRunTo.NativeMaximumRadiusRaw}.");
}
if (signedAngles != 2)
    throw new InvalidOperationException($"Dragon run-to audit found {signedAngles} signed native angle(s); expected 2.");
if (auxiliaryOutliers.Count != 1 || !auxiliaryOutliers[0].StartsWith("icecavern:", StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException($"Dragon run-to +0x30 audit found [{string.Join(", ", auxiliaryOutliers)}]; expected only Ice Cavern.");

DragonRescueCameraData focus = focusedScene ?? throw new InvalidOperationException("Artisans has no focused dragon run-to scene.");
LevelDefinition focusLevel = focusedLevel!;
byte[] focusBefore = focusedProperties!;

int editedAngle = DragonRescueRunTo.NormalizeAngle(focus.RunToAngle + 0x123);
int editedRadius = focus.RunToRadius + 17;
DragonRunToEndpoint editedEndpoint = DragonRescueRunTo.DecodeEndpoint(
    focus.DragonRawX,
    focus.DragonRawY,
    editedAngle,
    editedRadius);
DragonRunToPatchPlan endpointOnlyPlan = DragonRescueRunTo.BuildPatchPlan(
    focus,
    focus.DragonRawX,
    focus.DragonRawY,
    editedEndpoint.RawX,
    editedEndpoint.RawY,
    focusBefore);
if (!endpointOnlyPlan.CanPatch ||
    endpointOnlyPlan.PatchCount != 2 ||
    endpointOnlyPlan.Result.EncodedEndpoint != editedEndpoint ||
    endpointOnlyPlan.Result.Encoding.Angle != editedAngle ||
    endpointOnlyPlan.Result.Encoding.Radius != editedRadius ||
    !endpointOnlyPlan.FieldPatches.Select(patch => patch.FieldOffset).ToHashSet().SetEquals(
        [DragonRescueRunTo.AngleFieldOffset, DragonRescueRunTo.RadiusFieldOffset]))
{
    throw new InvalidOperationException("Focused Artisans endpoint edit did not produce exact +0x28/+0x2C angle/radius patches.");
}

byte[] focusAfter = (byte[])focusBefore.Clone();
foreach (DragonRunToFieldPatch patch in endpointOnlyPlan.FieldPatches)
    BinaryPrimitives.WriteInt32LittleEndian(focusAfter.AsSpan(patch.FieldOffset, sizeof(int)), patch.AfterValue);
for (int offset = 0; offset < focusBefore.Length; offset++)
{
    bool editableByte = offset is >= DragonRescueRunTo.AngleFieldOffset and < DragonRescueRunTo.AngleFieldOffset + sizeof(int) ||
        offset is >= DragonRescueRunTo.RadiusFieldOffset and < DragonRescueRunTo.RadiusFieldOffset + sizeof(int);
    if (!editableByte && focusBefore[offset] != focusAfter[offset])
        throw new InvalidOperationException($"Endpoint-only patch changed forbidden packed-properties byte +0x{offset:X}.");
}
if (BinaryPrimitives.ReadInt32LittleEndian(focusAfter.AsSpan(DragonRescueRunTo.AuxiliaryFieldOffset, sizeof(int))) != focus.RunToAuxiliary)
    throw new InvalidOperationException("Endpoint-only patch changed the +0x30 auxiliary value.");

IReadOnlyList<MobySourcePatch> sourcePatches = endpointOnlyPlan.ToMobySourcePatches(focusLevel, $"Dragon T{focus.DragonTrueIndex}");
if (sourcePatches.Count != 2 || sourcePatches.Any(patch =>
    patch.TrueIndex != focus.DragonTrueIndex ||
    !patch.WadRelativeOffset.Equals($"0x{focus.CameraDataWadOffset + (patch.Kind.EndsWith("angle", StringComparison.Ordinal) ? DragonRescueRunTo.AngleFieldOffset : DragonRescueRunTo.RadiusFieldOffset):X}", StringComparison.OrdinalIgnoreCase)))
{
    throw new InvalidOperationException("Focused endpoint plan did not convert to exact native Moby source patches.");
}

int dragonDeltaX = 1_376;
int dragonDeltaY = -912;
DragonRunToPatchPlan translatedTogetherPlan = DragonRescueRunTo.BuildPatchPlan(
    focus,
    checked(focus.DragonRawX + dragonDeltaX),
    checked(focus.DragonRawY + dragonDeltaY),
    checked(focus.RunToRawX + dragonDeltaX),
    checked(focus.RunToRawY + dragonDeltaY),
    focusBefore);
if (translatedTogetherPlan.PatchCount != 0)
    throw new InvalidOperationException("Moving a dragon and its run-to endpoint together changed their native relative angle/radius.");

int endpointDeltaX = 173;
int endpointDeltaY = -211;
int finalDragonRawX = checked(focus.DragonRawX + dragonDeltaX);
int finalDragonRawY = checked(focus.DragonRawY + dragonDeltaY);
int finalEndpointRawX = checked(focus.RunToRawX + dragonDeltaX + endpointDeltaX);
int finalEndpointRawY = checked(focus.RunToRawY + dragonDeltaY + endpointDeltaY);
DragonRunToEncodingResult combinedExpected = DragonRescueRunTo.EncodeEndpoint(
    finalDragonRawX,
    finalDragonRawY,
    finalEndpointRawX,
    finalEndpointRawY);
DragonRunToPatchPlan combinedPlan = DragonRescueRunTo.BuildPatchPlan(
    focus,
    finalDragonRawX,
    finalDragonRawY,
    finalEndpointRawX,
    finalEndpointRawY,
    focusBefore);
if (!combinedPlan.CanPatch ||
    combinedPlan.Result.Encoding != combinedExpected.Encoding ||
    combinedPlan.Result.EncodedEndpoint != combinedExpected.EncodedEndpoint ||
    combinedPlan.FieldPatches.Any(patch => patch.FieldOffset is not (DragonRescueRunTo.AngleFieldOffset or DragonRescueRunTo.RadiusFieldOffset)))
{
    throw new InvalidOperationException("Combined dragon/endpoint coordinate math did not encode relative to the final dragon position.");
}

byte[] tamperedAuxiliary = (byte[])focusBefore.Clone();
BinaryPrimitives.WriteInt32LittleEndian(
    tamperedAuxiliary.AsSpan(DragonRescueRunTo.AuxiliaryFieldOffset, sizeof(int)),
    focus.RunToAuxiliary ^ 1);
bool rejectedTamperedAuxiliary = false;
try
{
    DragonRescueRunTo.BuildPatchPlan(
        focus,
        focus.DragonRawX,
        focus.DragonRawY,
        editedEndpoint.RawX,
        editedEndpoint.RawY,
        tamperedAuxiliary);
}
catch (InvalidOperationException exception) when (
    exception.Message.StartsWith("Dragon run-to auxiliary +0x30 preimage", StringComparison.OrdinalIgnoreCase))
{
    rejectedTamperedAuxiliary = true;
}
if (!rejectedTamperedAuxiliary)
    throw new InvalidOperationException("Tampered +0x30 preimage was accepted.");

DragonRunToValidation review = DragonRescueRunTo.ValidateEndpoint(
    focus.DragonRawX,
    focus.DragonRawY,
    focus.DragonRawX + 100,
    focus.DragonRawY);
DragonRunToValidation zeroRadius = DragonRescueRunTo.ValidateEndpoint(
    focus.DragonRawX,
    focus.DragonRawY,
    focus.DragonRawX,
    focus.DragonRawY);
DragonRunToValidation overflow = DragonRescueRunTo.ValidateEndpoint(
    focus.DragonRawX,
    focus.DragonRawY,
    checked(focus.DragonRawX + DragonRescueRunTo.MaximumSafeRadiusRaw + 1),
    focus.DragonRawY);
if (review.Status != MobyBuildSafetyStatus.Review ||
    zeroRadius.Status != MobyBuildSafetyStatus.Blocked ||
    overflow.Status != MobyBuildSafetyStatus.Blocked ||
    DragonRescueRunTo.TryFromEditorUnits(double.NaN, 0, out _, out _))
{
    throw new InvalidOperationException("Dragon run-to Stable/Review/Blocked validation boundaries did not hold.");
}

Console.WriteLine(
    $"Dragon run-to smoke passed: {dragonScenes} scenes/{dragonLevels} levels, radius {minimumRadius}-{maximumRadius}, " +
    $"{signedAngles} signed angles, +0x30 preserved including {auxiliaryOutliers.Single()}, max round-trip error {maximumRoundTripError:0.###} raw; " +
    $"focused endpoint-only bytes +0x28/+0x2C and combined-coordinate math passed.");
