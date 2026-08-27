using System.Collections;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

Case[] cases =
[
    new(
        "terrain-t88-t107", true, true, true,
        "8f27bfc3f97238402c0b7dd59a081335ae19df1acfaefa9575d9ca7110dce808",
        "9d853ccfcf18627a46dc730844efc9eeeda28ee9a0d53c70ae3b7144652b4c3b"),
    new(
        "terrain-t88", true, true, false,
        "2af4d72a73ced5d133f77c48174c410a843276efd5de1c5b36b4eac3be01a690",
        "2f10969d64daad7344b78956585b41e5099a823426ecf446e14e6ab346708914"),
    new(
        "terrain-t107", true, false, true,
        "28d024e184ac4a0befc769c6ed77cb40fd12512bd4d6f75d0fac9b2b895d86cd",
        "09d538be3ce5936093370423d75310dbde04c3b8d81db13fb1da9e582e722c53"),
    new(
        "no-terrain-t88-t107", false, true, true,
        "82f5551bafa2fbc554ec0634cb6daf4878a19e851691c1b3c5841f09beaba531",
        "023b02c8a0623d05f40c95651bec421b616c92f4d470ac39bf523b2c816a685c"),
    new(
        "no-terrain-t88", false, true, false,
        "4224c2ef96dc8ce1e7d8e629fd51072afc1b78f70f0f7de4f3dcd84e345e8db7",
        "7c5db1f5bd2f186d4847d1ceb22e68168e35715c5c3312a55027f3e0c17c676a"),
    new(
        "no-terrain-t107", false, false, true,
        "30fa4c31c997bd41b93d0d8001bad62eea4c33882a8282504420f409a5b91642",
        "625c4349569dda2a24388fee7da6de2bb3bab7ab7a96e997b75c99b37701009e"),
    new("t25-terrain-t88-t107", true, true, true, "ef66ec698c9acccb57c31754985f7f8f42553dfd8cbca6fe3a4d94c11a5d6ea1", "2249e27f53f2f3d26e028ab7a94f47ab3f8aecf1251f6d070fd52b9789df77b2", 25),
    new("t25-terrain-t88", true, true, false, "592f3ab132ed88d33a91419d20664cd1e59004073e6d05ebaf98029d7df04a8a", "b46246d76093727d36e373fac72ffdb5332e5c7310a24a3e4e95dd731f47c764", 25),
    new("t25-terrain-t107", true, false, true, "6016ea37052ca2722f5236b79eceec1e28b0d8f788cf83ceafe5dfb0b9384adc", "98e02f353719003b4491d24e509ff90c7c1e7442597b25c670e9706e5646d14e", 25),
    new("t25-no-terrain-t88-t107", false, true, true, "3e382861087cdca0d105e25fac5fe10a899bf844eac51ad43eee69f7f29af8d5", "cfd90b4aa81485e9503cf0785d7966040755b1227dddde649777f6859e4d19b5", 25),
    new("t25-no-terrain-t88", false, true, false, "d8fcf0cf9f994b094b24631504ed433e8728bb93512c5690ccbd626369849bf9", "35c700e8a4390c027a53c502169bf68e97178f5597407e4dce61800420a29f4b", 25),
    new("t25-no-terrain-t107", false, false, true, "d30c63a0f188d5d6fa233813d423f4d9059f77c3d73a2dca3be22f3aa6e4f0f0", "15735dfdd2dda935c963e4f32a59320e3ffcb3a87d3a55a3a3a4d93b887a084f", 25)
];

Dictionary<string, string> observed = new(StringComparer.Ordinal);
Id65AuthoringEditorPrivateTextureSwap? invariantTexture = null;
Id65AuthoringEditorBaselineHandles? invariantBaseline = null;
int validCompilations = 0;
int rejectionCount = 0;

foreach (Case item in cases)
{
    Id65AuthoringEditorControlState state = new(
        item.Terrain,
        item.T88,
        item.T107,
        item.TextureId);
    Id65AuthoringEditorControlStateCompilation first =
        Id65AuthoringEditorControlStateCompiler.Compile(state);
    Id65AuthoringEditorControlStateCompilation repeat =
        Id65AuthoringEditorControlStateCompiler.Compile(new(
            item.Terrain,
            item.T88,
            item.T107,
            item.TextureId));
    validCompilations += 2;
    Id65AuthoringEditorDocument document = first.Document;
    Id65AuthoringEditorDocument direct = Id65AuthoringEditorDocument.CreateExact(
        item.Terrain,
        PlacementIds(item),
        item.TextureId);
    Require(
        document.SerializeUtf8().SequenceEqual(direct.SerializeUtf8()) &&
        document.SerializeUtf8().SequenceEqual(repeat.Document.SerializeUtf8()) &&
        first.DocumentCanonicalSha256 == document.CanonicalSha256 &&
        first.DocumentContentSha256 == document.ContentSha256 &&
        first.DocumentCanonicalSha256 == repeat.DocumentCanonicalSha256 &&
        document.CanonicalSha256 == item.ExpectedCanonicalSha256 &&
        document.ContentSha256 == item.ExpectedContentSha256,
        $"The {item.Key} control state did not compile deterministically through the exact factory.");

    Id65AuthoringEditorDocument reparsed =
        Id65AuthoringEditorDocument.Parse(document.SerializeUtf8());
    Require(reparsed.SerializeUtf8().SequenceEqual(document.SerializeUtf8()),
        $"The {item.Key} document did not serialize/parse/serialize exactly.");
    Id65AuthoringEditorControlState recovered =
        Id65AuthoringEditorControlStateCompiler.RecoverExactState(reparsed);
    Require(Equivalent(state, recovered),
        $"The {item.Key} document did not recover its exact control state.");

    Id65AuthoringEditorDocumentEnvelopeIdentity identity = new(
        document,
        Hash($"envelope:{item.Key}"),
        Id65AuthoringEditorDocumentStore.ProjectionContractId,
        Id65AuthoringCompositeCompiler.ProfileId,
        Hash($"manifest:{item.Key}"),
        Hash($"row80:{item.Key}"),
        Hash($"plan:{item.Key}"));
    Id65AuthoringEditorDocumentReadModel readModel = new(identity);
    Id65AuthoringEditorControlState recoveredFromPersistedReadModel =
        Id65AuthoringEditorControlStateCompiler.RecoverControlState(readModel);
    Require(Equivalent(state, recoveredFromPersistedReadModel),
        $"The persisted {item.Key} read model did not recover its exact control state.");

    int[] expectedTargets =
        (item.T88, item.T107) switch
        {
            (true, true) => [88, 107],
            (true, false) => [88],
            (false, true) => [107],
            _ => throw new InvalidOperationException("The valid case matrix contains no Grass placement.")
        };
    Require(
        document.Terrain.Count == (item.Terrain ? 1 : 0) &&
        document.MobyPlacements.Select(placement => placement.TargetTrueIndex)
            .SequenceEqual(expectedTargets) &&
        first.TerrainIntentCount == document.Terrain.Count &&
        first.MobyTargetTrueIndices.SequenceEqual(expectedTargets) &&
        first.PrivateTextureDestinationRecordIndex == 66 &&
        document.MandatoryBaseFaceTextureId == item.TextureId,
        $"The {item.Key} receipt lost terrain, T66, or exact Grass placement semantics.");
    if (item.T88)
    {
        Id65AuthoringEditorMobyPlacement t88 = document.MobyPlacements.Single(placement =>
            placement.TargetTrueIndex == 88);
        Require(
            t88.Id == Id65AuthoringMobyDependencyBundleCompiler.T88PlacementId &&
            t88.RowPolicy == Id65AuthoringEditorDocument.ReplaceExactRowPolicy &&
            t88.CoordinatePolicy == Id65AuthoringEditorDocument.PreserveDestinationCoordinatePolicy &&
            t88.CoordinatesOrDelta == null && t88.YawByte == null && t88.SupportHandle == null,
            "The exact T88 Grass placement semantics changed.");
    }
    if (item.T107)
    {
        Id65AuthoringEditorMobyPlacement t107 = document.MobyPlacements.Single(placement =>
            placement.TargetTrueIndex == 107);
        Require(
            t107.Id == Id65AuthoringMobyDependencyBundleCompiler.T107PlacementId &&
            t107.RowPolicy == Id65AuthoringEditorDocument.AppendContiguousRowPolicy &&
            t107.CoordinatePolicy == Id65AuthoringEditorDocument.AuthorExactCoordinatePolicy &&
            t107.CoordinatesOrDelta ==
                new Id65AuthoringEditorCoordinate(124_384, 102_304, 8_192) &&
            t107.YawByte == 0x04 && t107.SupportHandle == "support.native-apron.t1353",
            "The exact T107 Grass placement semantics changed.");
    }

    invariantTexture ??= document.PrivateTextureSwap;
    invariantBaseline ??= document.BaselineHandles;
    Require(document.PrivateTextureSwap == invariantTexture &&
            document.BaselineHandles == invariantBaseline,
        "Private T66 or baseline ownership changed across control states.");

    byte[] leakedDocumentBytes = document.SerializeUtf8();
    leakedDocumentBytes[0] ^= 1;
    Require(document.SerializeUtf8()[0] != leakedDocumentBytes[0],
        "The exact document exposed mutable canonical bytes.");
    IList receiptTargets = (IList)first.MobyTargetTrueIndices;
    Require(receiptTargets.IsReadOnly,
        "The control-state receipt exposed a mutable target-index collection.");
    ExpectReject<NotSupportedException>(
        $"{item.Key} target-index mutation",
        () => receiptTargets[0] = 0);
    rejectionCount++;

    AssertFailClosed(first, item.Key);
    observed[item.Key] = $"{document.CanonicalSha256}|{document.ContentSha256}";
}

Task<Id65AuthoringEditorControlStateCompilation>[] parallel = Enumerable.Range(0, 12)
    .Select(_ => Task.Run(() => Id65AuthoringEditorControlStateCompiler.Compile(
        Id65AuthoringEditorControlState.CreateDefault())))
    .ToArray();
Id65AuthoringEditorControlStateCompilation[] parallelResults = await Task.WhenAll(parallel);
Require(parallelResults.All(result =>
        result.DocumentCanonicalSha256 ==
            "8f27bfc3f97238402c0b7dd59a081335ae19df1acfaefa9575d9ca7110dce808" &&
        result.DocumentContentSha256 ==
            "9d853ccfcf18627a46dc730844efc9eeeda28ee9a0d53c70ae3b7144652b4c3b"),
    "Parallel default control-state compilation changed the frozen full document.");

ExpectReject<ArgumentNullException>(
    "null control state",
    () => Id65AuthoringEditorControlStateCompiler.Compile(null!));
rejectionCount++;
ExpectReject<ArgumentOutOfRangeException>(
    "unsupported mandatory base face texture",
    () => _ = new Id65AuthoringEditorControlState(true, true, true, 24));
rejectionCount++;
ExpectReject<InvalidDataException>(
    "transient no-placement control state",
    () => Id65AuthoringEditorControlStateCompiler.Compile(new(
        includeStructuralTerrain: true,
        includeGrassAtT88: false,
        includeGrassAtT107: false)));
rejectionCount++;
ExpectReject<InvalidDataException>(
    "forged mismatched control-state receipt",
    () => _ = new Id65AuthoringEditorControlStateCompilation(
        new(
            includeStructuralTerrain: true,
            includeGrassAtT88: true,
            includeGrassAtT107: true),
        Id65AuthoringEditorDocument.CreateExact(
            includeOptionalTerrain: false,
            placementIds:
            [
                Id65AuthoringMobyDependencyBundleCompiler.T88PlacementId,
                Id65AuthoringMobyDependencyBundleCompiler.T107PlacementId
            ])));
rejectionCount++;

AssertPublicSurface();
VerifySourceTopology(FindRepositoryRoot(args.ElementAtOrDefault(0)));

Console.WriteLine(
    $"ID65_AUTHORING_EDITOR_CONTROL_STATE_COMPILER_SMOKE_HASHES {string.Join(' ', observed.Select(item => $"{item.Key}={item.Value}"))}");
Console.WriteLine(
    $"ID65_AUTHORING_EDITOR_CONTROL_STATE_COMPILER_SMOKE_OK states={cases.Length} " +
    $"compilations={validCompilations + parallelResults.Length} rejects={rejectionCount} " +
    "path-free exact document translation/recovery passed without native derivation, persistence, or App/UI/Create-BIN/runtime/release authority.");

static IEnumerable<string> PlacementIds(Case item)
{
    if (item.T107)
        yield return Id65AuthoringMobyDependencyBundleCompiler.T107PlacementId;
    if (item.T88)
        yield return Id65AuthoringMobyDependencyBundleCompiler.T88PlacementId;
}

static bool Equivalent(
    Id65AuthoringEditorControlState left,
    Id65AuthoringEditorControlState right) =>
    left.IncludeStructuralTerrain == right.IncludeStructuralTerrain &&
    left.IncludeGrassAtT88 == right.IncludeGrassAtT88 &&
    left.IncludeGrassAtT107 == right.IncludeGrassAtT107 &&
    left.MandatoryBaseFaceTextureId == right.MandatoryBaseFaceTextureId;

static void AssertFailClosed(
    Id65AuthoringEditorControlStateCompilation result,
    string label)
{
    Require(
        result.ExactDocumentFactoryVerified &&
        result.CanonicalDocumentRoundTripVerified &&
        result.ControlStateRoundTripVerified &&
        !result.ContainsFilePaths && !result.ContainsGameBytes &&
        !result.ContainsNativePayloadBytes && !result.ContainsPhysicalAddresses &&
        !result.ContainsCompiledAfterimage && !result.NativeInputsDerived &&
        !result.CurrentProjectionRevalidated && !result.PersistencePerformed &&
        !result.AggregateMaterializationEnabled && !result.WritesFileSystem &&
        !result.WritesDiscImage && !result.WritesCue && !result.CreateBinEnabled &&
        !result.NormalCreateBinEnabled && !result.RuntimeCandidateAuthorized &&
        !result.RuntimeProven && !result.ReleaseIntegrated &&
        !result.PromotionAuthorized && !result.Publishable,
        $"The {label} compilation crossed a fail-closed boundary.");
}

static void AssertPublicSurface()
{
    Type state = typeof(Id65AuthoringEditorControlState);
    PropertyInfo[] properties = state.GetProperties(
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
    Require(
        state.IsPublic && state.IsSealed && properties.Length == 4 &&
        properties.Count(property => property.PropertyType == typeof(bool)) == 3 &&
        properties.Count(property => property.PropertyType == typeof(int)) == 1 &&
        properties.Select(property => property.Name).Order(StringComparer.Ordinal).SequenceEqual(
            new[]
            {
                "IncludeGrassAtT107",
                "IncludeGrassAtT88",
                "IncludeStructuralTerrain",
                "MandatoryBaseFaceTextureId"
            }.Order(StringComparer.Ordinal)) &&
        properties.All(property => !property.CanWrite),
        "The public control state is not exactly three immutable choices plus the exact texture selector.");
    ConstructorInfo[] constructors = state.GetConstructors(
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
    Require(
        constructors.Length == 1 &&
        constructors[0].GetParameters().Select(parameter => parameter.ParameterType)
            .SequenceEqual([typeof(bool), typeof(bool), typeof(bool), typeof(int)]),
        "The public control state constructor lost its exact four-choice contract.");

    MethodInfo[] compilerMethods = typeof(Id65AuthoringEditorControlStateCompiler).GetMethods(
            BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly)
        .OrderBy(method => method.Name, StringComparer.Ordinal)
        .ToArray();
    Require(
        compilerMethods.Length == 2 &&
        compilerMethods[0].Name == "Compile" &&
        compilerMethods[0].ReturnType == typeof(Id65AuthoringEditorControlStateCompilation) &&
        compilerMethods[0].GetParameters().Select(parameter => parameter.ParameterType)
            .SequenceEqual([typeof(Id65AuthoringEditorControlState)]) &&
        compilerMethods[1].Name == "RecoverControlState" &&
        compilerMethods[1].ReturnType == typeof(Id65AuthoringEditorControlState) &&
        compilerMethods[1].GetParameters().Select(parameter => parameter.ParameterType)
            .SequenceEqual([typeof(Id65AuthoringEditorDocumentReadModel)]),
        "The public control-state compiler surface gained a path/native/persistence-shaped entrypoint.");
}

static void VerifySourceTopology(string repositoryRoot)
{
    string relative = Path.Combine(
        "src",
        "Spyro.Editor.Core",
        "Persistence",
        "Id65AuthoringEditorControlStateCompiler.cs");
    string source = File.ReadAllText(Path.Combine(repositoryRoot, relative));
    string[] forbidden =
    [
        "Id65AuthoringEditorNativeInputProvider",
        "Id65AuthoringEditorDocumentProjector",
        "Id65AuthoringEditorDocumentStore",
        "FileStream",
        "CancellationToken"
    ];
    Regex fileApi = new(
        @"\b(?:File|Directory|Path|DiscImage)\s*\.",
        RegexOptions.CultureInvariant);
    Require(
        forbidden.All(item => !source.Contains(item, StringComparison.Ordinal)) &&
        !fileApi.IsMatch(source),
        "The control-state compiler acquired a filesystem, native-input, projector, or Store dependency.");
}

static void ExpectReject<TException>(string label, Action action)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }
    throw new InvalidOperationException($"The negative case '{label}' was accepted.");
}

static string Hash(string text) =>
    Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

static string FindRepositoryRoot(string? start)
{
    DirectoryInfo? current = new(Path.GetFullPath(start ?? Directory.GetCurrentDirectory()));
    while (current != null)
    {
        if (File.Exists(Path.Combine(current.FullName, "global.json")) &&
            Directory.Exists(Path.Combine(current.FullName, "src", "Spyro.Editor.Core")))
        {
            return current.FullName;
        }
        current = current.Parent;
    }
    throw new DirectoryNotFoundException("Could not locate the Spyro Editor repository root.");
}

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

internal sealed record Case(
    string Key,
    bool Terrain,
    bool T88,
    bool T107,
    string ExpectedCanonicalSha256,
    string ExpectedContentSha256,
    int TextureId = 66);
