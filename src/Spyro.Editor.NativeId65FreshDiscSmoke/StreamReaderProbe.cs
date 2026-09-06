using System.Reflection;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;

if (args.Length > 1)
    throw new ArgumentException("Usage: FreshDiscProbe=StreamSeam [original.bin]");

Assembly core = typeof(NativeId65NormalBodyPlan).Assembly;
Type disc = core.GetType("Spyro.Editor.Core.Exporting.DiscImage", throwOnError: true)!;
Type layout = core.GetType("Spyro.Editor.Core.Exporting.DiscLayout", throwOnError: true)!;
Type growth = core.GetType("Spyro.Editor.Core.Exporting.NativeWadGrowthSectorPreparation", throwOnError: true)!;
Type geometry = core.GetType("Spyro.Editor.Core.Exporting.NativeSkyWadRelocationPlan", throwOnError: true)!;
int checks = 0;
MethodInfo detect = Require(disc, "DetectLayout", typeof(Stream));
MethodInfo read = Require(disc, "ReadFileBytes", typeof(Stream), layout, typeof(int), typeof(long), typeof(int));
_ = Require(disc, "FindRootFileRecord", typeof(Stream), layout, typeof(Predicate<string>));
_ = Require(growth, "Create", typeof(Stream), layout, geometry);
_ = Require(growth, "ReadFlatRootFiles", typeof(Stream), layout);

// Existing signatures remain present; their implementation is shared, not
// bypassed or changed to a source-authority-granting fake FileStream subclass.
_ = Require(disc, "DetectLayout", typeof(string));
MethodInfo oldRead = Require(disc, "ReadFileBytes", typeof(FileStream), layout, typeof(int), typeof(long), typeof(int));
_ = Require(growth, "Create", typeof(FileStream), layout, geometry);

if (args.Length == 1)
{
    using var original = File.OpenRead(args[0]);
    RequireOriginal(original);
    object detected = detect.Invoke(null, [original])!;
    foreach (var (lba, offset, length) in new[] { (16, 0L, 2048), (22, 0L, 2048), (37, 0L, 2048), (53875, 2000L, 96) })
    {
        byte[] a = (byte[])read.Invoke(null, [original, detected, lba, offset, length])!;
        byte[] b = (byte[])oldRead.Invoke(null, [original, detected, lba, offset, length])!;
        if (!a.SequenceEqual(b))
            throw new InvalidDataException("Read-only Stream/FileStream paths diverged.");
        checks++;
    }
    RequireOriginal(original);
    Console.WriteLine($"NATIVE_ID65_FRESH_DISC_STREAM_SOURCE_OK checks={checks}");
}
Console.WriteLine($"NATIVE_ID65_FRESH_DISC_STREAM_SMOKE_OK checks={checks}");

MethodInfo Require(Type owner, string name, params Type[] parameters)
{
    MethodInfo? method = owner.GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
        binder: null, types: parameters, modifiers: null);
    if (method == null || !method.GetParameters().Select(p => p.ParameterType).SequenceEqual(parameters))
        throw new InvalidDataException($"EXPECTED_READ_ONLY_STREAM_SEAM_MISSING: {owner.Name}.{name}({string.Join(",", parameters.Select(p => p.Name))})");
    checks++;
    return method;
}

void RequireOriginal(FileStream image)
{
    if (image.Length != 661547040)
        throw new InvalidDataException("Wrong original length.");
    image.Position = 0;
    if (Convert.ToHexStringLower(SHA256.HashData(image)) !=
        "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37" ||
        image.ReadByte() != -1 || image.Position != 661547040 || image.Length != 661547040)
        throw new InvalidDataException("Wrong original identity/EOF.");
    checks++;
}
