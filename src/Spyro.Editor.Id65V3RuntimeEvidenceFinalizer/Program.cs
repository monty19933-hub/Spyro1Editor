using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;

try
{
    string root = FindRepositoryRoot();
    if (args is ["inspect", "--run-id", string runId])
    {
        Id65V3RuntimeEvidenceFinalizationPreview preview =
            Id65V3RuntimeEvidenceFinalizer.Inspect(root, runId);
        Console.WriteLine("ID65_V3_RUNTIME_EVIDENCE_INSPECT_READY");
        Console.WriteLine($"run-id={preview.RunId}");
        Console.WriteLine($"run-origin-sha256={preview.RunOriginSha256}");
        Console.WriteLine($"final-terminal-event-sha256={preview.FinalTerminalEventSha256}");
        Console.WriteLine($"capture-closure-sha256={preview.CaptureClosureSha256}");
        Console.WriteLine($"catalog={preview.ArtifactCount} sessions={preview.SessionCount} checks={preview.CheckCount}");
        foreach (Id65V3RuntimeEvidenceReviewCheck check in preview.Checks)
        {
            string? requiredEdgeToken =
                Id65V3RuntimeEvidenceContract.RequiredEdgeAssignmentToken(check.CheckId);
            Console.WriteLine($"review-check={check.CheckId} session={check.SessionId} " +
                $"observed-at={check.ObservedAtUtc} binding={check.EvidenceBindingSha256}" +
                (requiredEdgeToken is null
                    ? string.Empty
                    : $" required-edge-assignment={requiredEdgeToken}"));
            foreach (Id65V3RuntimeEvidenceReviewReference reference in check.References)
            {
                string interval = reference.StartMilliseconds is long start &&
                    reference.EndMilliseconds is long end
                        ? $" slice-ms={start}..{end}"
                        : string.Empty;
                Console.WriteLine($"  artifact={reference.ArtifactId} sha256={reference.Sha256}" +
                    interval + $" path={reference.RepoRelativePath}");
            }
        }
        Console.WriteLine("canonical-attestation-template-begin");
        Console.WriteLine(Encoding.UTF8.GetString(preview.CanonicalAttestationTemplateUtf8));
        Console.WriteLine("canonical-attestation-template-end");
        Console.WriteLine(
            "PENDING: publisher/Create BIN authority remains closed until the canonical evidence hash is independently reviewed and pinned.");
        return 0;
    }
    if (args is
        [
            "finalize", "--run-id", string finalizeRunId,
            "--attestation", string attestationPath,
            "--attestation-sha256", string attestationSha256
        ])
    {
        Id65V3RuntimeEvidenceFinalizationResult result =
            Id65V3RuntimeEvidenceFinalizer.FinalizeFocusedPass(
                root,
                finalizeRunId,
                attestationPath,
                attestationSha256);
        PrintResult("ID65_V3_RUNTIME_EVIDENCE_FINALIZED", result);
        return 0;
    }
    if (args is ["verify"])
    {
        Id65V3RuntimeEvidenceFinalizationResult result =
            Id65V3RuntimeEvidenceFinalizer.VerifyExisting(root);
        PrintResult("ID65_V3_RUNTIME_EVIDENCE_VERIFIED", result);
        return 0;
    }
    Console.Error.WriteLine(
        "Usage:\n" +
        "  inspect --run-id <id65v3-...>\n" +
        "  finalize --run-id <id65v3-...> --attestation <absolute-file> " +
        "--attestation-sha256 <lowercase-hex64>\n" +
        "  verify");
    return 2;
}
catch (Exception exception) when (exception is InvalidDataException or IOException or
                                  UnauthorizedAccessException or ArgumentException or
                                  DirectoryNotFoundException or PlatformNotSupportedException or
                                  JsonException or OverflowException)
{
    Console.Error.WriteLine("REFUSED: " + exception.Message);
    return 3;
}

static void PrintResult(string marker, Id65V3RuntimeEvidenceFinalizationResult result)
{
    Console.WriteLine(marker);
    Console.WriteLine($"evidence-path={result.EvidenceRelativePath}");
    Console.WriteLine($"evidence-sha256={result.EvidenceSha256}");
    Console.WriteLine($"run-id={result.RunId}");
    Console.WriteLine($"run-origin-sha256={result.RunOriginSha256}");
    Console.WriteLine($"final-terminal-event-sha256={result.FinalTerminalEventSha256}");
    Console.WriteLine($"capture-closure-sha256={result.CaptureClosureSha256}");
    Console.WriteLine($"source-closure-sha256={result.SourceClosureSha256}");
    Console.WriteLine($"runtime-profile-sha256={result.RuntimeProfileSha256}");
    Console.WriteLine($"artifact-set-sha256={result.ArtifactSetSha256}");
    Console.WriteLine($"payload-sha256={result.PayloadSha256}");
    Console.WriteLine($"contract-sha256={result.ContractSha256}");
    Console.WriteLine($"catalog={result.ArtifactCount} sessions={result.SessionCount} checks={result.CheckCount}");
    Console.WriteLine($"existing-identical={result.ExistingIdentical.ToString().ToLowerInvariant()}");
    Console.WriteLine($"publisher-pin-boundary={result.PublisherPinBoundary}");
    Console.WriteLine(
        "PENDING: publisher/Create BIN authority remains closed; this command does not pin or publish the evidence hash.");
}

static string FindRepositoryRoot()
{
    DirectoryInfo? directory = new(AppContext.BaseDirectory);
    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "Directory.Build.props")) &&
            File.Exists(Path.Combine(
                directory.FullName,
                "src",
                "Spyro.Editor.Core",
                "Spyro.Editor.Core.csproj")))
        {
            return directory.FullName;
        }
        directory = directory.Parent;
    }
    throw new DirectoryNotFoundException("Could not locate the authoritative Spyro Editor repository root.");
}
