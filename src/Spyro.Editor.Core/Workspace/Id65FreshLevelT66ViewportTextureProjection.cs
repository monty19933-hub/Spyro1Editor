using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.Core.Workspace;

/// <summary>
/// One immutable RGBA frame decoded from the exact witnessed T66 native
/// texture state. Pixels are copied at the API boundary so the in-memory
/// projection cannot be used as a mutable native afterimage.
/// </summary>
public sealed class Id65FreshLevelT66ViewportFrame
{
    private readonly byte[] _rgbaBytes;

    internal Id65FreshLevelT66ViewportFrame(
        NativeTerrainHqMaterialTier tier,
        int width,
        int height,
        byte[] rgbaBytes)
    {
        Tier = tier;
        Width = width;
        Height = height;
        _rgbaBytes = rgbaBytes.ToArray();
        RgbaSha256 = Hash(_rgbaBytes);
    }

    public NativeTerrainHqMaterialTier Tier { get; }
    public int Width { get; }
    public int Height { get; }
    public int PixelCount => checked(Width * Height);
    public int ByteLength => _rgbaBytes.Length;
    public string RgbaSha256 { get; }
    public byte[] CopyRgbaBytes() => _rgbaBytes.ToArray();

    private static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}

/// <summary>
/// Exact, source-bound, in-memory presentation of the pinned private T66
/// allocation. It contains decoded viewport material only; it has no writer,
/// BIN/CUE, runtime-candidate, or promotion authority.
/// </summary>
public sealed class Id65FreshLevelT66ViewportTextureProjection
{
    internal Id65FreshLevelT66ViewportTextureProjection(
        NativeTerrainLqTextureSet lowDetailTextures,
        NativeTerrainHqMaterialSet highDetailMaterials,
        Id65FreshLevelT66ViewportFrame normal,
        Id65FreshLevelT66ViewportFrame close)
    {
        LowDetailTextures = lowDetailTextures;
        HighDetailMaterials = highDetailMaterials;
        Normal = normal;
        Close = close;
    }

    public string ProfileId => Id65FreshLevelT66ViewportTextureProjector.ProfileId;
    public int SchemaVersion => Id65FreshLevelT66ViewportTextureProjector.SchemaVersion;
    public int TextureId => Id65V2NativeTextureCompositionCompiler.PrivateTextureId;
    public int DonorWadEntry => UnusedLevel65NativeTextureAuthoringContract.DonorWadEntry;
    public int DonorTextureId => UnusedLevel65NativeTextureAuthoringContract.DonorTextureId;
    public string ManifestSha256 => Id65V2NativeTextureCompositionCompiler.ExpectedManifestSha256;
    public string WitnessSha256 => Id65V2NativeTextureCompositionCompiler.ExpectedWitnessSha256;
    public string TexturePagesSha256 => Id65V2NativeTextureCompositionCompiler.ExpectedOutputTexturePagesSha256;
    public string TextureComponentSha256 => Id65V2NativeTextureCompositionCompiler.ExpectedOutputTextureComponentSha256;
    public string PrivateLowRowSha256 => Id65V2NativeTextureCompositionCompiler.ExpectedPrivateLowRowSha256;
    public string PrivateHighRowSha256 => Id65V2NativeTextureCompositionCompiler.ExpectedPrivateHighRowSha256;
    public NativeTerrainLqTextureSet LowDetailTextures { get; }
    public NativeTerrainHqMaterialSet HighDetailMaterials { get; }
    public Id65FreshLevelT66ViewportFrame Normal { get; }
    public Id65FreshLevelT66ViewportFrame Close { get; }
    public bool IsPresentationOnly => true;
    public bool WritesWorkspace => false;
    public bool WritesLockedBase => false;
    public bool WritesDiscImage => false;
    public bool AuthorizesCreateBin => false;
    public bool AuthorizesRuntimeCandidate => false;
    public bool AuthorizesReleasePromotion => false;
}

/// <summary>
/// Reconstructs the already-pinned T66 page/row proof from the exact locked
/// ID65 source and decodes it for the viewport without writing any afterimage.
/// </summary>
public static class Id65FreshLevelT66ViewportTextureProjector
{
    public const string ProfileId = "id65-fresh-level-t66-viewport-projection-v1";
    public const int SchemaVersion = 1;
    public const int ExpectedNormalWidth = 64;
    public const int ExpectedNormalHeight = 64;
    public const int ExpectedCloseWidth = 64;
    public const int ExpectedCloseHeight = 64;
    public const string ExpectedNormalRgbaSha256 =
        "1cc181ffaaeeeb9038f37410489a7a085dad113b6caf588f7b0c5b3f6001a305";
    public const string ExpectedCloseRgbaSha256 =
        "1cc181ffaaeeeb9038f37410489a7a085dad113b6caf588f7b0c5b3f6001a305";

    public static Id65FreshLevelT66ViewportTextureProjection ProjectExact(
        string lockedBaseImagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lockedBaseImagePath);
        string sourcePath = Path.GetFullPath(lockedBaseImagePath);
        ValidatePinnedT66Manifest();

        Id65V2NativeTextureWitness witness =
            Id65V2NativeTextureCompositionCompiler
                .DerivePinnedT66WitnessFromLockedBase(sourcePath);
        ValidateWitnessIdentity(witness);

        (byte[] sourcePages, byte[] sourceTextureComponent) =
            ReadExactLockedSource(sourcePath);
        byte[] outputPages = ApplyWitnessedPagePatches(sourcePages, witness);
        byte[] outputTextureComponent = BuildWitnessedTextureComponent(
            sourceTextureComponent,
            witness);

        NativeTerrainTextureInitialStateResult exactAuthoredState = new(
            Complete: true,
            TextureCount: Id65V2NativeTextureCompositionCompiler.OutputTextureCount,
            TextureListSize: Id65V2NativeTextureCompositionCompiler.OutputTextureComponentBytes,
            LowDetailTableOffset: 8,
            HighDetailTableOffset: checked(
                8 +
                (Id65V2NativeTextureCompositionCompiler.OutputTextureCount * 16)),
            OriginalTextureData: outputTextureComponent.ToArray(),
            InitializedTextureData: outputTextureComponent.ToArray(),
            Mutations: Array.Empty<NativeTerrainTextureInitialStateMutation>(),
            SafetyBlockers: Array.Empty<string>());
        int[] textureIds = Enumerable.Range(
            0,
            Id65V2NativeTextureCompositionCompiler.OutputTextureCount).ToArray();

        NativeTerrainLqTextureDecodeResult low =
            NativeTerrainLqTextureCacheCodec.DecodeNativeLoadState(
                exactAuthoredState,
                outputPages,
                textureIds);
        if (!low.Complete || low.TextureSet == null ||
            low.DecodedTextureCount != textureIds.Length ||
            !low.TextureSet.RetailAliasComplete ||
            !low.TextureSet.TryGetTexture(
                Id65V2NativeTextureCompositionCompiler.PrivateTextureId,
                out _))
        {
            throw new InvalidDataException(
                low.SafetyBlockers.FirstOrDefault() ??
                "The exact T66 viewport TexLq projection was incomplete.");
        }

        NativeTerrainHqMaterialDecodeResult high =
            NativeTerrainHqMaterialCacheCodec.DecodeNativeLoadState(
                exactAuthoredState,
                outputPages,
                textureIds);
        if (!high.Complete || high.MaterialSet == null ||
            high.DecodedTextureCount != textureIds.Length ||
            !high.MaterialSet.TryGetTexture(
                Id65V2NativeTextureCompositionCompiler.PrivateTextureId,
                out NativeTerrainHqMaterialRecordPayload? privateMaterial))
        {
            throw new InvalidDataException(
                high.SafetyBlockers.FirstOrDefault() ??
                "The exact T66 viewport HQ material projection was incomplete.");
        }

        Id65FreshLevelT66ViewportFrame normal = BuildFrame(privateMaterial.Normal);
        Id65FreshLevelT66ViewportFrame close = BuildFrame(privateMaterial.Close);
        ValidateDecodedProjection(low.TextureSet, high.MaterialSet, normal, close);
        RequireHash(HashFile(sourcePath), Id65RewardCensusContract.LockedSourceImageSha256,
            "locked ID65 display-name BIN after T66 viewport projection");
        return new Id65FreshLevelT66ViewportTextureProjection(
            low.TextureSet,
            high.MaterialSet,
            normal,
            close);
    }

    private static void ValidatePinnedT66Manifest()
    {
        Id65AuthoringManifest manifest =
            Id65V2NativeTextureCompositionCompiler.CreateMinimalV3T66Manifest();
        Id65RenderFaceIntent face = manifest.RenderFaces.Single();
        Id65TextureIntent authored = manifest.Textures.Single(texture =>
            texture.Id == Id65V2NativeTextureCompositionCompiler.PrivateTextureIntentId);
        if (manifest.ProfileId != Id65V2NativeTextureCompositionCompiler.ManifestProfileId ||
            !HashEquals(
                manifest.CanonicalSha256,
                Id65V2NativeTextureCompositionCompiler.ExpectedManifestSha256) ||
            face.Id != Id65FreshLevelSceneProjector.RenderFaceStableId ||
            face.TextureIntentId != Id65V2NativeTextureCompositionCompiler.PrivateTextureIntentId ||
            authored.Disposition != Id65AuthoringIntentDisposition.AuthorExact ||
            authored.DestinationRecordIndex != Id65V2NativeTextureCompositionCompiler.PrivateTextureId ||
            authored.SourceHandle != Id65V2NativeTextureCompositionCompiler.PrivateTextureSourceHandle)
        {
            throw new InvalidDataException(
                "The fresh-level viewport projection no longer matches the pinned T66 authoring manifest.");
        }
    }

    private static void ValidateWitnessIdentity(Id65V2NativeTextureWitness witness)
    {
        Id65V2NativeTexturePackedRow synthetic = witness.CopyPackedRows()
            .Single(row => row.Synthetic);
        if (witness.ProfileId != Id65V2NativeTextureCompositionCompiler.WitnessProfileId ||
            !HashEquals(
                witness.CanonicalSha256,
                Id65V2NativeTextureCompositionCompiler.ExpectedWitnessSha256) ||
            witness.SourceTextureCount != Id65V2NativeTextureCompositionCompiler.SourceTextureCount ||
            witness.OutputTextureCount != Id65V2NativeTextureCompositionCompiler.OutputTextureCount ||
            witness.PrivateTextureId != Id65V2NativeTextureCompositionCompiler.PrivateTextureId ||
            witness.DonorWadEntry != UnusedLevel65NativeTextureAuthoringContract.DonorWadEntry ||
            witness.DonorTextureId != UnusedLevel65NativeTextureAuthoringContract.DonorTextureId ||
            witness.MaterialTemplateTextureId != Id65V2NativeTextureCompositionCompiler.MaterialTemplateTextureId ||
            synthetic.TextureId != Id65V2NativeTextureCompositionCompiler.PrivateTextureId ||
            !HashEquals(
                synthetic.LowSha256,
                Id65V2NativeTextureCompositionCompiler.ExpectedPrivateLowRowSha256) ||
            !HashEquals(
                synthetic.HighSha256,
                Id65V2NativeTextureCompositionCompiler.ExpectedPrivateHighRowSha256) ||
            witness.ContainsFoundationModelBytes ||
            witness.ContainsFoundationFaceBinding ||
            witness.ContainsAppendAfterimage ||
            witness.ContainsPublisherState ||
            witness.CrossSliceCompositionAuthorized ||
            witness.AfterimageStackingAuthorized)
        {
            throw new InvalidDataException(
                "The fresh-level viewport projection lost its exact T66 witness identity or safety boundary.");
        }
    }

    private static (byte[] SourcePages, byte[] SourceTextureComponent)
        ReadExactLockedSource(string sourcePath)
    {
        DiscLayout layout = DiscImage.DetectLayout(sourcePath);
        if (layout.SectorSize != 2352 || layout.UserOffset != 24)
            throw new InvalidDataException("The exact T66 viewport projection requires MODE2/2352 source data.");
        using FileStream source = File.Open(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);
        DiscFileRecord wad = DiscImage.FindRootFileRecord(
            source,
            layout,
            name => string.Equals(name, "WAD.WAD", StringComparison.OrdinalIgnoreCase));
        if (wad.Lba != 37)
            throw new InvalidDataException("The exact T66 viewport projection lost the pinned WAD extent.");

        byte[] pages = DiscImage.ReadFileBytes(
            source,
            layout,
            wad.Lba,
            Id65V2NativeTextureCompositionCompiler.TexturePagesWadOffset,
            Id65V2NativeTextureCompositionCompiler.TexturePagesByteLength);
        byte[] component = DiscImage.ReadFileBytes(
            source,
            layout,
            wad.Lba,
            Id65V2NativeTextureCompositionCompiler.ModelWadOffset,
            Id65V2NativeTextureCompositionCompiler.SourceTextureComponentBytes);
        RequireHash(
            Hash(pages),
            Id65V2NativeTextureCompositionCompiler.ExpectedSourceTexturePagesSha256,
            "locked ID65 T66 source texture pages");
        RequireHash(
            Hash(component),
            Id65V2NativeTextureCompositionCompiler.ExpectedSourceTextureComponentSha256,
            "locked ID65 T66 source texture component");
        return (pages, component);
    }

    private static byte[] ApplyWitnessedPagePatches(
        byte[] sourcePages,
        Id65V2NativeTextureWitness witness)
    {
        byte[] output = sourcePages.ToArray();
        foreach (Id65V2NativeTexturePagePatch patch in witness.CopyPagePatches()
                     .OrderBy(patch => patch.RelativeOffset))
        {
            byte[] before = patch.CopyBefore();
            byte[] after = patch.CopyAfter();
            if (patch.RelativeOffset < 0 ||
                patch.RelativeOffset + (long)patch.ByteLength > output.Length ||
                before.Length != patch.ByteLength || after.Length != patch.ByteLength ||
                !HashEquals(Hash(before), patch.BeforeSha256) ||
                !HashEquals(Hash(after), patch.AfterSha256) ||
                !output.AsSpan(patch.RelativeOffset, patch.ByteLength).SequenceEqual(before))
            {
                throw new InvalidDataException(
                    "A pinned T66 viewport page patch lost its exact range or preimage.");
            }
            after.CopyTo(output, patch.RelativeOffset);
        }
        RequireHash(
            Hash(output),
            Id65V2NativeTextureCompositionCompiler.ExpectedOutputTexturePagesSha256,
            "witnessed T66 viewport texture pages");
        return output;
    }

    private static byte[] BuildWitnessedTextureComponent(
        byte[] sourceComponent,
        Id65V2NativeTextureWitness witness)
    {
        if (sourceComponent.Length != Id65V2NativeTextureCompositionCompiler.SourceTextureComponentBytes ||
            BinaryPrimitives.ReadInt32LittleEndian(sourceComponent) != sourceComponent.Length ||
            BinaryPrimitives.ReadInt32LittleEndian(sourceComponent.AsSpan(4)) !=
                Id65V2NativeTextureCompositionCompiler.SourceTextureCount)
        {
            throw new InvalidDataException("The pinned T66 source texture component header changed.");
        }

        IReadOnlyList<Id65V2NativeTexturePackedRow> rows = witness.CopyPackedRows();
        Dictionary<int, Id65V2NativeTexturePackedRow> packed = rows
            .ToDictionary(row => row.TextureId);
        byte[] output = new byte[
            Id65V2NativeTextureCompositionCompiler.OutputTextureComponentBytes];
        BinaryPrimitives.WriteInt32LittleEndian(output, output.Length);
        BinaryPrimitives.WriteInt32LittleEndian(
            output.AsSpan(4),
            Id65V2NativeTextureCompositionCompiler.OutputTextureCount);
        int sourceHighStart = checked(
            8 + (Id65V2NativeTextureCompositionCompiler.SourceTextureCount * 16));
        int outputHighStart = checked(
            8 + (Id65V2NativeTextureCompositionCompiler.OutputTextureCount * 16));
        for (int textureId = 0;
             textureId < Id65V2NativeTextureCompositionCompiler.SourceTextureCount;
             textureId++)
        {
            if (packed.TryGetValue(textureId, out Id65V2NativeTexturePackedRow? replacement))
            {
                replacement.CopyLow().CopyTo(output, 8 + (textureId * 16));
                replacement.CopyHigh().CopyTo(output, outputHighStart + (textureId * 168));
            }
            else
            {
                sourceComponent.AsSpan(8 + (textureId * 16), 16)
                    .CopyTo(output.AsSpan(8 + (textureId * 16), 16));
                sourceComponent.AsSpan(sourceHighStart + (textureId * 168), 168)
                    .CopyTo(output.AsSpan(outputHighStart + (textureId * 168), 168));
            }
        }

        Id65V2NativeTexturePackedRow synthetic = rows.Single(row => row.Synthetic);
        synthetic.CopyLow().CopyTo(
            output,
            8 + (Id65V2NativeTextureCompositionCompiler.PrivateTextureId * 16));
        synthetic.CopyHigh().CopyTo(
            output,
            outputHighStart +
            (Id65V2NativeTextureCompositionCompiler.PrivateTextureId * 168));
        RequireHash(
            Hash(output.AsSpan(
                8 + (Id65V2NativeTextureCompositionCompiler.PrivateTextureId * 16),
                16)),
            Id65V2NativeTextureCompositionCompiler.ExpectedPrivateLowRowSha256,
            "witnessed private T66 LQ row");
        RequireHash(
            Hash(output.AsSpan(
                outputHighStart +
                (Id65V2NativeTextureCompositionCompiler.PrivateTextureId * 168),
                168)),
            Id65V2NativeTextureCompositionCompiler.ExpectedPrivateHighRowSha256,
            "witnessed private T66 HQ row");
        RequireHash(
            Hash(output),
            Id65V2NativeTextureCompositionCompiler.ExpectedOutputTextureComponentSha256,
            "witnessed 67-row T66 texture component");
        return output;
    }

    private static Id65FreshLevelT66ViewportFrame BuildFrame(
        NativeTerrainHqMaterialTierPayload material)
    {
        ushort[] words = material.MaterializeCompositeRawWords();
        byte[] rgba = new byte[checked(words.Length * 4)];
        for (int index = 0; index < words.Length; index++)
        {
            ushort word = words[index];
            int offset = index * 4;
            rgba[offset] = ExpandPsx5(word & 0x1F);
            rgba[offset + 1] = ExpandPsx5((word >> 5) & 0x1F);
            rgba[offset + 2] = ExpandPsx5((word >> 10) & 0x1F);
            rgba[offset + 3] = word == 0 ? (byte)0 : (byte)255;
        }
        return new Id65FreshLevelT66ViewportFrame(
            material.Tier,
            material.CompositeSide,
            material.CompositeSide,
            rgba);
    }

    private static void ValidateDecodedProjection(
        NativeTerrainLqTextureSet low,
        NativeTerrainHqMaterialSet high,
        Id65FreshLevelT66ViewportFrame normal,
        Id65FreshLevelT66ViewportFrame close)
    {
        if (low.NativeTextureCount != Id65V2NativeTextureCompositionCompiler.OutputTextureCount ||
            low.TextureCount != Id65V2NativeTextureCompositionCompiler.OutputTextureCount ||
            high.NativeTextureCount != Id65V2NativeTextureCompositionCompiler.OutputTextureCount ||
            high.TextureCount != Id65V2NativeTextureCompositionCompiler.OutputTextureCount ||
            !HashEquals(
                low.TexturePagesSha256,
                Id65V2NativeTextureCompositionCompiler.ExpectedOutputTexturePagesSha256) ||
            !HashEquals(
                low.OriginalTextureComponentSha256,
                Id65V2NativeTextureCompositionCompiler.ExpectedOutputTextureComponentSha256) ||
            !HashEquals(
                high.TexturePagesSha256,
                Id65V2NativeTextureCompositionCompiler.ExpectedOutputTexturePagesSha256) ||
            !HashEquals(
                high.OriginalTextureComponentSha256,
                Id65V2NativeTextureCompositionCompiler.ExpectedOutputTextureComponentSha256) ||
            normal.Tier != NativeTerrainHqMaterialTier.Normal ||
            close.Tier != NativeTerrainHqMaterialTier.Close ||
            normal.Width != ExpectedNormalWidth || normal.Height != ExpectedNormalHeight ||
            close.Width != ExpectedCloseWidth || close.Height != ExpectedCloseHeight ||
            normal.ByteLength != checked(normal.PixelCount * 4) ||
            close.ByteLength != checked(close.PixelCount * 4) ||
            !HashEquals(normal.RgbaSha256, ExpectedNormalRgbaSha256) ||
            !HashEquals(close.RgbaSha256, ExpectedCloseRgbaSha256))
        {
            throw new InvalidDataException(
                "The exact T66 viewport projection lost its 67-row raw material or RGBA frame proof.");
        }
    }

    private static byte ExpandPsx5(int value) =>
        (byte)((value << 3) | (value >> 2));

    private static string HashFile(string path)
    {
        using FileStream stream = File.Open(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static bool HashEquals(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static void RequireHash(string actual, string expected, string label)
    {
        if (!HashEquals(actual, expected))
            throw new InvalidDataException($"The {label} SHA-256 changed: {actual}.");
    }
}
