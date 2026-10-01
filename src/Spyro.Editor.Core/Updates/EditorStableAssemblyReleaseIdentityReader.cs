using System.Globalization;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Spyro.Editor.Core.Updates;

public sealed record EditorStableAssemblyReleaseIdentity(string AssemblyName,EditorStableReleaseVersion PublicVersion,int ReleaseManifestSchema,string InternalVersion);

public static class EditorStableAssemblyReleaseIdentityReader
{
    public const string ExpectedAssemblyName = "Spyro.Editor.App";
    public const string ChannelMetadataKey = "SpyroEditorReleaseChannel";
    public const string PublicReleaseMetadataKey = "SpyroEditorStableReleaseVersion";
    public const string ReleaseManifestSchemaMetadataKey = "SpyroEditorStableManifestSchema";

    private const string AssemblyMetadataAttributeName = "System.Reflection.AssemblyMetadataAttribute";
    private const string AssemblyInformationalVersionAttributeName = "System.Reflection.AssemblyInformationalVersionAttribute";

    public static EditorStableAssemblyReleaseIdentity Read(Stream assemblyStream, string? description = null)
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
            List<string> channelValues = [];
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
                    if (string.Equals(values[0], ChannelMetadataKey, StringComparison.Ordinal))
                        channelValues.Add(values[1]);
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

            if(channelValues.Count!=1||channelValues[0]!="stable")throw new InvalidDataException($"The {source} is not explicitly a stable-channel assembly.");
            if(publicVersionValues.Count!=1||!EditorStableReleaseVersion.TryParse(publicVersionValues[0],out var version))throw new InvalidDataException($"The {source} has invalid stable release metadata.");
            if(releaseManifestSchemaValues.Count!=1||releaseManifestSchemaValues[0]!="3")throw new InvalidDataException($"The {source} must declare stable manifest schema3.");
            if (informationalVersions.Count != 1 || string.IsNullOrWhiteSpace(informationalVersions[0]))
            {
                throw new InvalidDataException(
                    $"The {source} must contain exactly one non-empty AssemblyInformationalVersion value; found {informationalVersions.Count}.");
            }

            if(!EditorSemanticVersion.TryParse(informationalVersions[0],out var build)||build.IsPrerelease||build.Major!=version.Major||build.Minor!=version.Minor||build.Patch!=version.Patch)throw new InvalidDataException("Stable internal build identity must match the stable version without a prerelease identifier.");
            return new EditorStableAssemblyReleaseIdentity(assemblyName,version,3,informationalVersions[0]);
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
