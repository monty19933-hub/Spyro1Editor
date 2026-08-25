using System.Text.Json;
using Spyro.Editor.Core.Exporting;

try
{
    if (!OperatingSystem.IsMacOS())
        throw new PlatformNotSupportedException(
            "Aggregate V1 runtime finalization requires macOS stable-handle boundaries.");
    string root = FindRepositoryRoot();
    if (args is ["self-test"])
    {
        Id65AuthoringAggregateV1RuntimeEvidenceFinalizerSmokeReport report =
            Id65AuthoringAggregateV1RuntimeEvidenceFinalizer.RunOfflineFixtureSmoke(root);
        Console.WriteLine("ID65_AUTHORING_AGGREGATE_V1_RUNTIME_FINALIZER_SELF_TEST_OK");
        Console.WriteLine(
            $"positive={report.PositiveCases} refusals={report.RefusalCases} " +
            $"checks={report.CheckCount} evidence={report.FixtureEvidenceSha256}");
        Console.WriteLine(
            $"production-inspect-refused={Lower(report.ProductionInspectRefused)} " +
            $"production-finalize-refused={Lower(report.ProductionFinalizeRefused)} " +
            $"production-verify-refused={Lower(report.ProductionVerifyRefused)}");
        Console.WriteLine(
            $"existing-identical={Lower(report.ExistingIdenticalObserved)} " +
            $"quarantined={Lower(report.FsyncFailureQuarantined)} " +
            $"cleaned={Lower(report.TemporaryFilesCleaned)}");
        Console.WriteLine($"capture-coordinator-sha256={report.CaptureCoordinatorSha256}");
        Console.WriteLine($"capture-source-closure-sha256={report.CaptureSourceClosureSha256}");
        Console.WriteLine($"aggregate-checklist-sha256={report.AggregateChecklistSha256}");
        Console.WriteLine($"capture-runtime-profile-sha256={report.CaptureRuntimeProfileSha256}");
        return 0;
    }
    if (args is ["status"])
    {
        Id65AuthoringAggregateV1RuntimeEvidenceFinalizerStatus status =
            Id65AuthoringAggregateV1RuntimeEvidenceFinalizer.GetStatus(root);
        Console.WriteLine(JsonSerializer.Serialize(status, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        }));
        return 0;
    }
    if (args is ["inspect", "--run-id", string runId])
    {
        _ = Id65AuthoringAggregateV1RuntimeEvidenceFinalizer.Inspect(root, runId);
        throw new InvalidOperationException(
            "Production aggregate inspection unexpectedly returned while fail-closed.");
    }
    if (args is
        [
            "finalize", "--run-id", string finalizeRunId,
            "--attestation", string attestationPath,
            "--attestation-sha256", string attestationSha256
        ])
    {
        _ = Id65AuthoringAggregateV1RuntimeEvidenceFinalizer.FinalizeFocusedPass(
            root, finalizeRunId, attestationPath, attestationSha256);
        throw new InvalidOperationException(
            "Production aggregate finalization unexpectedly returned while fail-closed.");
    }
    if (args is ["verify"])
    {
        _ = Id65AuthoringAggregateV1RuntimeEvidenceFinalizer.VerifyExisting(root);
        throw new InvalidOperationException(
            "Production aggregate verification unexpectedly returned while fail-closed.");
    }
    Console.Error.WriteLine(
        "Usage:\n" +
        "  self-test\n" +
        "  status\n" +
        "  inspect --run-id <id>\n" +
        "  finalize --run-id <id> --attestation <absolute-file> " +
        "--attestation-sha256 <lowercase-hex64>\n" +
        "  verify");
    return 2;
}
catch (Exception exception) when (exception is InvalidDataException or IOException or
                                  UnauthorizedAccessException or ArgumentException or
                                  DirectoryNotFoundException or PlatformNotSupportedException or
                                  InvalidOperationException or JsonException or OverflowException)
{
    Console.Error.WriteLine("REFUSED: " + exception.Message);
    Console.Error.WriteLine(
        "No runtime evidence was published and Create BIN/release/publish authority remains false.");
    return 3;
}

static string Lower(bool value) => value.ToString().ToLowerInvariant();

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
    throw new DirectoryNotFoundException(
        "Could not locate the authoritative Spyro Editor repository root.");
}
