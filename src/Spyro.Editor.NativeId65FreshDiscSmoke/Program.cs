using Spyro.Editor.Core.Exporting;

if (args.Length > 1)
    throw new ArgumentException("Usage: NativeId65FreshDiscSmoke [original.bin]; no output/publication mode exists.");

int checks = 0;
FreshDiscCases.SourceFree(Check, Reject);
NativeFreshDiscScalarCases.Run(Check, Reject);
if (args.Length == 1)
{
    var surface = await NativeId65AuthoredSurfaceBundle.CaptureAsync(args[0],
        new(8448, 8448, 8704, 1024, 1024, 0x708050, 0x406080));
    var companion = await NativeId65CompanionAssetPlan.CaptureAsync(args[0]);
    NativeFreshDiscScalarCases.RequireOriginalExecutable(companion.EngineProfile.CopyExecutable(), Check);
    var resources = await NativeId65NormalResourceBundle.CaptureAsync(args[0], surface, companion);
    var lists = await NativeId65ZeroSceneAnimationLists.CaptureAsync(args[0]);
    var chain = NativeId65NormalResourceChain.Compile(resources, lists);
    var firstScene = await NativeId65FreshSceneBundle.CaptureAsync(args[0], chain, new(8960, 8960, 9216, 64), 16);
    var secondScene = await NativeId65FreshSceneBundle.CaptureAsync(args[0], chain, new(8976, 8960, 9216, 128), 16);
    var shortScene = await NativeId65FreshSceneBundle.CaptureAsync(args[0], chain, new(8960, 8960, 9216, 64), 1);
    var firstStage = await NativeId65NormalUploadStaging.CaptureAsync(args[0], firstScene);
    var secondStage = await NativeId65NormalUploadStaging.CaptureAsync(args[0], secondScene);
    var shortStage = await NativeId65NormalUploadStaging.CaptureAsync(args[0], shortScene);
    var firstBody = await NativeId65NormalBodyPlan.CaptureAsync(args[0], firstScene, firstStage);
    var secondBody = await NativeId65NormalBodyPlan.CaptureAsync(args[0], secondScene, secondStage);
    var shortBody = await NativeId65NormalBodyPlan.CaptureAsync(args[0], shortScene, shortStage);
    var first = await FreshDiscCases.Source(args[0], firstBody, secondBody, shortBody, Check, Reject);
    await CaptureBoundaryCases.Source(args[0], first, Check);
    Console.WriteLine($"NATIVE_ID65_FRESH_DISC_SOURCE_OK checks={checks}");
}
Console.WriteLine($"NATIVE_ID65_FRESH_DISC_SMOKE_OK checks={checks}");

void Check(bool condition, string label)
{
    if (!condition)
        throw new InvalidDataException(label);
    checks++;
}

void Reject(Action action, string label)
{
    try
    {
        action();
    }
    catch (Exception error) when (error is InvalidDataException or ArgumentException or InvalidOperationException)
    {
        checks++;
        return;
    }
    throw new InvalidDataException("Unexpected acceptance: " + label);
}
