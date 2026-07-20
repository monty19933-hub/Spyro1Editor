using System.Globalization;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Spyro.Editor.Core.Updates;

public sealed record EditorAssemblyReleaseIdentity(
    string AssemblyName,
    int PublicBetaVersion,
    EditorBetaReleaseVersion PublicVersion,
    bool HasExplicitPublicVersion,
    int ReleaseManifestSchema,
    bool HasExplicitReleaseManifestSchema,
    string InternalVersion);

public static class EditorAssemblyReleaseIdentityReader
{
    public const string ExpectedAssemblyName = "Spyro.Editor.App";
    public const string BetaReleaseMetadataKey = "SpyroEditorBetaRelease";
    public const string PublicReleaseMetadataKey = "SpyroEditorPublicReleaseVersion";
    public const string ReleaseManifestSchemaMetadataKey = "SpyroEditorReleaseManifestSchema";

    private const string AssemblyMetadataAttributeName = "System.Reflection.AssemblyMetadataAttribute";
    private const string AssemblyInformationalVersionAttributeName = "System.Reflection.AssemblyInformationalVersionAttribute";

    public static EditorAssemblyReleaseIdentity Read(Stream assemblyStream, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(assemblyStream);
        string source = string.IsNullOrWhiteSpace(description) ? "application assembly" : description.Trim();
        if (!assemblyStream.CanRead || !assemblyStream.CanSeek)
            throw new InvalidDataException($"The {source} must be a readable, seekable .NET assembly stream.");

        try
        {
            using PEReader peReader = new(assemblyStream, PEStreamOptions.LeaveOpen);
            if (!peReader.HasMetadata)
                throw new InvalidDataException($"The {source} is not a managed .NET assembly.");

            MetadataReader metadata = peReader.GetMetadataReader();
            if (!metadata.IsAssembly)
                throw new InvalidDataException($"The {source} does not contain .NET assembly metadata.");

            AssemblyDefinition assembly = metadata.GetAssemblyDefinition();
            string assemblyName = metadata.GetString(assembly.Name);
            List<string> betaValues = [];
            List<string> publicVersionValues = [];
            List<string> releaseManifestSchemaValues = [];
            List<string> informationalVersions = [];
            foreach (CustomAttributeHandle handle in assembly.GetCustomAttributes())
            {
                CustomAttribute attribute = metadata.GetCustomAttribute(handle);
                string attributeName = GetAttributeTypeName(metadata, attribute.Constructor);
                if (string.Equals(attributeName, AssemblyMetadataAttributeName, StringComparison.Ordinal))
                {
                    string[] values = ReadFixedStringArguments(metadata, attribute, expectedCount: 2, source);
                    if (string.Equals(values[0], BetaReleaseMetadataKey, StringComparison.Ordinal))
                        betaValues.Add(values[1]);
                    else if (string.Equals(values[0], PublicReleaseMetadataKey, StringComparison.Ordinal))
                        publicVersionValues.Add(values[1]);
                    else if (string.Equals(values[0], ReleaseManifestSchemaMetadataKey, StringComparison.Ordinal))
                        releaseManifestSchemaValues.Add(values[1]);
                }
                else if (string.Equals(attributeName, AssemblyInformationalVersionAttributeName, StringComparison.Ordinal))
                {
                    string[] values = ReadFixedStringArguments(metadata, attribute, expectedCount: 1, source);
                    informationalVersions.Add(values[0]);
                }
            }

            if (betaValues.Count != 1)
            {
                throw new InvalidDataException(
                    $"The {source} must contain exactly one '{BetaReleaseMetadataKey}' assembly metadata value; found {betaValues.Count}.");
            }
            string betaText = betaValues[0];
            if (!int.TryParse(betaText, NumberStyles.None, CultureInfo.InvariantCulture, out int betaVersion) ||
                betaVersion <= 0 ||
                !string.Equals(betaText, betaVersion.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
            {
                throw new InvalidDataException($"The {source} contains an invalid public beta assembly metadata value: '{betaText}'.");
            }

            if (publicVersionValues.Count > 1)
            {
                throw new InvalidDataException(
                    $"The {source} must contain at most one '{PublicReleaseMetadataKey}' assembly metadata value; found {publicVersionValues.Count}.");
            }

            bool hasExplicitPublicVersion = publicVersionValues.Count == 1;
            EditorBetaReleaseVersion publicVersion;
            if (hasExplicitPublicVersion)
            {
                if (!EditorBetaReleaseVersion.TryParse(publicVersionValues[0], out publicVersion) ||
                    !string.Equals(publicVersionValues[0], publicVersion.CanonicalVersion, StringComparison.Ordinal) ||
                    publicVersion.Major != betaVersion)
                {
                    throw new InvalidDataException(
                        $"The {source} contains an invalid or legacy-major-mismatched public release metadata value: '{publicVersionValues[0]}'.");
                }
            }
            else
            {
                publicVersion = new EditorBetaReleaseVersion(betaVersion);
            }

            if (releaseManifestSchemaValues.Count > 1)
            {
                throw new InvalidDataException(
                    $"The {source} must contain at most one '{ReleaseManifestSchemaMetadataKey}' assembly metadata value; found {releaseManifestSchemaValues.Count}.");
            }

            bool hasExplicitReleaseManifestSchema = releaseManifestSchemaValues.Count == 1;
            int expectedManifestSchema = publicVersion.IsIncremental ? 2 : 1;
            int releaseManifestSchema = expectedManifestSchema;
            if (hasExplicitReleaseManifestSchema &&
                (!int.TryParse(releaseManifestSchemaValues[0], NumberStyles.None, CultureInfo.InvariantCulture, out releaseManifestSchema) ||
                 releaseManifestSchema != expectedManifestSchema ||
                 !string.Equals(releaseManifestSchemaValues[0], releaseManifestSchema.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)))
            {
                throw new InvalidDataException(
                    $"The {source} contains release-manifest schema metadata '{releaseManifestSchemaValues[0]}' that does not match public version {publicVersion.CanonicalVersion}.");
            }

            if (informationalVersions.Count != 1 || string.IsNullOrWhiteSpace(informationalVersions[0]))
            {
                throw new InvalidDataException(
                    $"The {source} must contain exactly one non-empty AssemblyInformationalVersion value; found {informationalVersions.Count}.");
            }

            return new EditorAssemblyReleaseIdentity(
                assemblyName,
                betaVersion,
                publicVersion,
                hasExplicitPublicVersion,
                releaseManifestSchema,
                hasExplicitReleaseManifestSchema,
                informationalVersions[0]);
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception exception) when (exception is BadImageFormatException or IOException or ArgumentOutOfRangeException)
        {
            throw new InvalidDataException($"The {source} does not contain readable .NET assembly metadata.", exception);
        }
    }

    private static string GetAttributeTypeName(MetadataReader metadata, EntityHandle constructor)
    {
        EntityHandle declaringType = constructor.Kind switch
        {
            HandleKind.MemberReference => metadata.GetMemberReference((MemberReferenceHandle)constructor).Parent,
            HandleKind.MethodDefinition => metadata.GetMethodDefinition((MethodDefinitionHandle)constructor).GetDeclaringType(),
            _ => default
        };

        return declaringType.Kind switch
        {
            HandleKind.TypeReference => FullName(metadata, metadata.GetTypeReference((TypeReferenceHandle)declaringType)),
            HandleKind.TypeDefinition => FullName(metadata, metadata.GetTypeDefinition((TypeDefinitionHandle)declaringType)),
            _ => ""
        };
    }

    private static string FullName(MetadataReader metadata, TypeReference type)
    {
        string name = metadata.GetString(type.Name);
        string ns = metadata.GetString(type.Namespace);
        return string.IsNullOrWhiteSpace(ns) ? name : $"{ns}.{name}";
    }

    private static string FullName(MetadataReader metadata, TypeDefinition type)
    {
        string name = metadata.GetString(type.Name);
        string ns = metadata.GetString(type.Namespace);
        return string.IsNullOrWhiteSpace(ns) ? name : $"{ns}.{name}";
    }

    private static string[] ReadFixedStringArguments(
        MetadataReader metadata,
        CustomAttribute attribute,
        int expectedCount,
        string source)
    {
        BlobReader value = metadata.GetBlobReader(attribute.Value);
        if (value.RemainingBytes < 2 || value.ReadUInt16() != 1)
            throw new InvalidDataException($"The {source} contains malformed release identity metadata.");

        string[] values = new string[expectedCount];
        for (int index = 0; index < expectedCount; index++)
        {
            values[index] = value.ReadSerializedString()
                ?? throw new InvalidDataException($"The {source} contains a null release identity metadata value.");
        }

        if (value.RemainingBytes < 2 || value.ReadUInt16() != 0 || value.RemainingBytes != 0)
            throw new InvalidDataException($"The {source} contains unsupported named release identity metadata values.");
        return values;
    }
}
