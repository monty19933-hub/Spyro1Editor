using Spyro.Editor.Core.Exporting;

if (args.Length > 1)
    throw new ArgumentException("Usage: NativeId65NormalBodySmoke [original.bin]");

int checks = 0;
NativeBodyCases.Run(Check, Reject);
NativeBodySourceWindows.Run(Check, Reject);
if (args.Length == 1)
{
    var floor = new NativeId65AuthoredFloor(8448, 8448, 8704, 1024, 1024, 0x708050, 0x406080);
    var surface = await NativeId65AuthoredSurfaceBundle.CaptureAsync(args[0], floor);
    var companion = await NativeId65CompanionAssetPlan.CaptureAsync(args[0]);
    var resources = await NativeId65NormalResourceBundle.CaptureAsync(args[0], surface, companion);
    var lists = await NativeId65ZeroSceneAnimationLists.CaptureAsync(args[0]);
    var chain = NativeId65NormalResourceChain.Compile(resources, lists);
    var first = await NativeId65FreshSceneBundle.CaptureAsync(args[0], chain, new(8960, 8960, 9216, 64), 16);
    var second = await NativeId65FreshSceneBundle.CaptureAsync(args[0], chain, new(8976, 8960, 9216, 128), 16);
    await NativeBodyCases.Source(args[0], companion, first, second, Check, Reject);
    NativeBodySourceWindows.RequireOriginalExecutable(companion.EngineProfile.CopyExecutable(), Check);
    Console.WriteLine($"NATIVE_ID65_NORMAL_BODY_SOURCE_OK checks={checks}");
}
Console.WriteLine($"NATIVE_ID65_NORMAL_BODY_SMOKE_OK checks={checks}");

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
