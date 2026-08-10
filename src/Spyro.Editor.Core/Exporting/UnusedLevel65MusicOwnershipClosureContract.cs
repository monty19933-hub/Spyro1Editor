using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Spyro.Editor.Core.Music;

namespace Spyro.Editor.Core.Exporting;

internal sealed record UnusedLevel65MusicClosureFile(
    string Name,
    int Lba,
    int ByteLength);

internal sealed record UnusedLevel65MusicClosureRegion(
    string Name,
    int FileOffset,
    long ImageOffset,
    uint RuntimeAddress,
    int ByteLength,
    string Hex,
    string Sha256);

internal sealed record UnusedLevel65MusicClosureReference(
    string Name,
    int FileOffset,
    uint RuntimeAddress,
    string InstructionHex,
    uint ReferencedRuntimeAddress,
    string Semantics);

internal sealed record UnusedLevel65MusicClosureCallsite(
    string Consumer,
    int FileOffset,
    uint RuntimeAddress,
    uint TargetRuntimeAddress,
    string InstructionAndDelayHex,
    string Sha256,
    bool DirectJal);

internal sealed record UnusedLevel65MusicClosurePatch(
    string Name,
    int FileOffset,
    long ImageOffset,
    uint RuntimeAddress,
    int ByteLength,
    int RawSectorLba,
    string BeforeHex,
    string AfterHex,
    string BeforeSha256,
    string AfterSha256,
    int ChangedByteCount,
    string Semantics);

internal sealed record UnusedLevel65MusicClosureTrackProof(
    int TrackId,
    string DisplayName,
    bool Selectable,
    int InitialResolvedTrackId,
    IReadOnlyList<int> LongPlayResolvedTrackIds,
    bool InitialAndLongPlayMatch);

internal sealed record UnusedLevel65MusicClosureMode2Impact(
    int SectorSize,
    int UserOffset,
    int LogicalPatchWindowBytes,
    int ChangedLogicalBytes,
    int MinimumRawSectorCount,
    IReadOnlyList<int> AffectedRawSectorLbas,
    IReadOnlyList<long> AffectedRawSectorImageOffsets,
    bool AllAffectedSectorsAreMode2Form1,
    bool EdcEccRebuildRequired,
    bool FileExtentUnchanged,
    bool IsoDirectoryUnchanged,
    bool XaAudioSectorsUnchanged);

internal sealed record UnusedLevel65MusicOwnershipClosureContract(
    string ProfileId,
    string LockedDisplayNameImageSha256,
    string RemoteBlankImageSha256,
    string ExecutableSha256,
    string OutputExecutableSha256,
    string RemoteBlankOutputDataSha256,
    UnusedLevel65MusicClosureFile Executable,
    IReadOnlyList<UnusedLevel65MusicClosureFile> PeteXaFiles,
    int ContinuousLevelIndex,
    int WitnessTrackId,
    string WitnessTrackName,
    UnusedLevel65MusicClosureRegion MappingTable,
    UnusedLevel65MusicClosureRegion InitialSlot,
    UnusedLevel65MusicClosureRegion InitialConsumer,
    UnusedLevel65MusicClosureRegion LateAlternateTable,
    int LateAlternateRowCount,
    int LateAlternateValuesPerRow,
    UnusedLevel65MusicClosureRegion ReusedRow34,
    UnusedLevel65MusicClosureRegion PeteXaLbaTable,
    UnusedLevel65MusicClosureRegion LateConsumer,
    UnusedLevel65MusicClosureRegion RandomCallee,
    IReadOnlyList<UnusedLevel65MusicClosureReference> References,
    IReadOnlyList<UnusedLevel65MusicClosureCallsite> InitialConsumerCallsites,
    IReadOnlyList<UnusedLevel65MusicClosureCallsite> LateConsumerCallsites,
    IReadOnlyList<UnusedLevel65MusicClosurePatch> WitnessPatches,
    IReadOnlyList<UnusedLevel65MusicClosureTrackProof> SelectableTrackProofs,
    IReadOnlyList<int> ReservedTrackIds,
    string RetailOutcomeMatrixSha256,
    string Id65WitnessOutcomeMatrixSha256,
    string SupportedTrackIdSetSha256,
    UnusedLevel65MusicClosureMode2Impact Mode2Impact,
    bool LockedAndRemoteExecutablesIdentical,
    bool RemoteBlankOutputMatchesStaticPlan,
    bool RemoteBlankConstructionExcludesExecutable,
    bool InitialSlotIndependent,
    bool NoLateTableExtension,
    bool PeteXaLbaTablePreserved,
    bool PeteXaDirectoryPreserved,
    bool RandomCalleePreservesT0AndT1,
    bool RetailInitialAndLongPlayOutcomesPreserved,
    bool Id65InitialAndLongPlayResolveThroughSlot35,
    bool AllSelectableBuiltInTracksStaticallyClosed,
    bool MinimumTwoSectorTransactionAchieved,
    bool StaticTransactionClosed,
    bool GenericLevelMusicExporterStillRejectsId65,
    bool WritesBin,
    bool WritesCue,
    bool WriterAuthorized,
    bool RuntimeProofComplete,
    bool AppEnabled,
    bool NormalCreateBinEnabled,
    bool PromotionAuthorized,
    IReadOnlyList<string> RuntimeGates,
    IReadOnlyList<string> Notes);

/// <summary>
/// Read-only ownership closure for ID65 music. The exact relocated USA SCUS
/// has an independent initial mapping slot at continuous index 35, but its
/// late-alternate table ends at row 34 immediately before the six PETEXA LBA
/// words. This contract proves an in-place, branchless late-selector special
/// case which never extends that table:
///
/// - retail indices 0..34 continue to address their exact original row;
/// - ID65 index 35 safely reuses row 34, whose three values are all 34;
/// - only ID65 increments that loaded source index to 35 before the unchanged
///   48-entry mapping lookup, so initial and long-play playback both resolve
///   through the independently authored slot 35.
///
/// The proof constructs the proposed transaction only in memory. It does not
/// write a BIN/CUE, expose an editor control, authorize a writer, or enable
/// normal Create BIN/release promotion.
/// </summary>
internal static class UnusedLevel65MusicOwnershipClosure
{
    public const string ProfileId =
        "unused-level-65-music-slot35-branchless-long-play-static-clean-usa-v1";
    public const string LockedDisplayNameImageSha256 =
        "9e42b43bd1341b40915748432d1b2dc760e22a81c0a320ec09ae6a71ca2efcd8";
    public const string RemoteBlankImageSha256 =
        "8020947d4ab5e7e4b0eac4bc6409ff3d6f11212b0e118bbf007d870812014a8e";
    public const string ExecutableSha256 =
        "fa5fc7981188b78fa7d7b78facca64c1f79dadb107515e9146ad178ade39d442";
    public const string WitnessOutputExecutableSha256 =
        "30d7721b6b46b9753827ddee249bc2591dc9fc1b5a76db71a4ebffe146cc9f8c";

    private const int RawSectorBytes = 2352;
    private const int UserOffset = 24;
    private const int UserBytes = 2048;
    private const int ExecutableLba = 55_382;
    private const int ExecutableByteLength = 0x66000;
    private const int PsxExeHeaderByteLength = 0x800;
    private const uint PsxExeLoadAddress = 0x80010000;
    private const int PsxExePayloadByteLength = 0x65800;
    private const uint RuntimeBias = PsxExeLoadAddress - PsxExeHeaderByteLength;
    private const int ContinuousLevelIndex = 35;
    private const int WitnessTrackId = 26;

    private const int MappingTableFileOffset = 0x5F79C;
    private const int MappingEntryCount = 48;
    private const int InitialSlotFileOffset = MappingTableFileOffset + ContinuousLevelIndex * sizeof(int);
    private const int InitialConsumerFileOffset = 0x6504;
    private const int InitialConsumerByteLength = 0x24;
    private const uint InitialConsumerFunctionRuntimeAddress = 0x80015370;

    private const int LateTableFileOffset = 0x5F85C;
    private const int LateTableRowCount = 35;
    private const int LateValuesPerRow = 3;
    private const int LateTableByteLength = LateTableRowCount * LateValuesPerRow * sizeof(int);
    private const int LateRow34FileOffset = LateTableFileOffset + 34 * LateValuesPerRow * sizeof(int);
    private const int PeteXaLbaTableFileOffset = 0x5FA00;
    private const int PeteXaLbaTableByteLength = 6 * sizeof(int);
    private const uint LateConsumerFunctionRuntimeAddress = 0x8002BBE0;
    private const int LateConsumerFileOffset = 0x1C57C;
    private const int LateConsumerByteLength = 0x8C;
    private const uint RandomCalleeRuntimeAddress = 0x8006272C;
    private const int RandomCalleeFileOffset = 0x52F2C;
    private const int RandomCalleeByteLength = 0x30;

    private const int WadLba = UnusedLevel65RemoteBlankIsolationConstruction.WadLba;
    private const long DataWadOffset = UnusedLevel65RemoteBlankIsolationConstruction.DataWadOffset;
    private const int DataByteLength = UnusedLevel65RemoteBlankIsolationConstruction.DataByteLength;

    private static readonly (string Name, int Lba, int Size)[] ExpectedPeteXaFiles =
    [
        ("PETEXA0.STR", 60_000, 50_348_032),
        ("PETEXA1.STR", 84_584, 59_113_472),
        ("PETEXA2.STR", 113_448, 69_091_328),
        ("PETEXA3.STR", 147_184, 77_053_952),
        ("PETEXA4.STR", 184_808, 85_147_648),
        ("PETEXA5.STR", 226_384, 76_038_144)
    ];

    private static readonly int[] ExpectedInitialConsumerCallerOffsets =
    [
        0x1E7E8, 0x1FB04, 0x1FB80, 0x22D3C,
        0x23534, 0x23904, 0x23960, 0x23CBC
    ];

    private static readonly int[] ExpectedLateConsumerCallerOffsets =
    [
        0x6D94, 0x1EB40, 0x2350C, 0x2353C,
        0x238B0, 0x23A88, 0x23EA0, 0x2407C
    ];

    private static readonly (string Name, int Offset, string Before, string After, string Semantics)[]
        WitnessCodePatchSpecs =
    [
        (
            "preload-scus-high-in-condition-branch-delay",
            0x1C57C,
            "00000000",
            "0780083C",
            "The existing condition-branch delay slot loads t0=0x80070000; t0 is caller-saved and otherwise dead in this function."),
        (
            "preload-continuous-index-in-prng-jal-delay",
            0x1C584,
            "00000000",
            "6459098D",
            "The existing PRNG JAL delay slot loads t1=[0x80075964]; the exact PRNG callee does not write t0 or t1."),
        (
            "select-bounded-row-and-retain-id65-flag",
            0x1C594,
            "0780033C6459638C0780043C5CF08424402803002128A300802805002128A400",
            "2300262D2118260150F00425402803002128A300802805002128A4000100C638",
            "For retail, (index + 1) against base row -1 equals the original row; ID65 reads row 34, then retains an ID65-only +1 flag."),
        (
            "promote-row34-source-to-slot35-and-reload-timer",
            0x1C5D8,
            "0780033CC858638C",
            "21104600C858038D",
            "Only ID65 adds one to row 34's source value 34, then the unchanged lookup resolves mapping slot 35; the timer reload uses pinned t0.")
    ];

    public static UnusedLevel65MusicOwnershipClosureContract Inspect(
        string workspaceRoot,
        string lockedDisplayNameImagePath,
        string remoteBlankImagePath)
    {
        string root = Path.GetFullPath(workspaceRoot);
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException("The Spyro Editor workspace root is missing.");
        string lockedPath = RequireFile(lockedDisplayNameImagePath, "locked ID65 display-name BIN");
        string remotePath = RequireFile(remoteBlankImagePath, "remote-blank runtime candidate BIN");
        RequireHash(HashFile(lockedPath), LockedDisplayNameImageSha256, "locked ID65 display-name BIN");
        RequireHash(HashFile(remotePath), RemoteBlankImageSha256, "remote-blank runtime candidate BIN");

        DiscLayout lockedLayout = DiscImage.DetectLayout(lockedPath);
        DiscLayout remoteLayout = DiscImage.DetectLayout(remotePath);
        RequireMode2Layout(lockedLayout, "locked display-name BIN");
        RequireMode2Layout(remoteLayout, "remote-blank BIN");

        byte[] lockedExecutable;
        byte[] remoteExecutable;
        DiscFileRecord lockedExeRecord;
        DiscFileRecord remoteExeRecord;
        List<UnusedLevel65MusicClosureFile> peteXaFiles = [];
        using (FileStream locked = File.OpenRead(lockedPath))
        using (FileStream remote = File.OpenRead(remotePath))
        {
            lockedExeRecord = RequireRootFile(locked, lockedLayout, "SCUS_942.28", ExecutableLba, ExecutableByteLength);
            remoteExeRecord = RequireRootFile(remote, remoteLayout, "SCUS_942.28", ExecutableLba, ExecutableByteLength);
            lockedExecutable = DiscImage.ReadFileBytes(
                locked,
                lockedLayout,
                lockedExeRecord.Lba,
                0,
                lockedExeRecord.Size);
            remoteExecutable = DiscImage.ReadFileBytes(
                remote,
                remoteLayout,
                remoteExeRecord.Lba,
                0,
                remoteExeRecord.Size);
            RequireHash(Hash(lockedExecutable), ExecutableSha256, "locked relocated SCUS");
            RequireHash(Hash(remoteExecutable), ExecutableSha256, "remote relocated SCUS");
            if (!lockedExecutable.SequenceEqual(remoteExecutable))
                throw new InvalidDataException("Remote-blank construction changed the relocated executable.");

            foreach ((string name, int lba, int size) in ExpectedPeteXaFiles)
            {
                DiscFileRecord lockedRecord = RequireRootFile(locked, lockedLayout, name, lba, size);
                DiscFileRecord remoteRecord = RequireRootFile(remote, remoteLayout, name, lba, size);
                if (lockedRecord != remoteRecord)
                    throw new InvalidDataException($"Remote-blank construction changed {name}'s ISO directory record.");
                peteXaFiles.Add(new(name, lba, size));
            }
        }

        RequirePsxExeHeader(lockedExecutable);
        UnusedLevel65RemoteBlankStaticPlan remotePlan =
            UnusedLevel65RemoteBlankIsolationConstruction.BuildStaticPlan(lockedPath);
        if (remotePlan.OutputDataSha256 != UnusedLevel65RemoteBlankIsolationConstruction.ExpectedOutputDataSha256 ||
            !remotePlan.ExecutableExcluded || !remotePlan.ProtectedSubfilesPreserved ||
            remotePlan.SourceImageMutationPossible || remotePlan.PromotionAuthorized || remotePlan.NormalCreateBinEnabled)
        {
            throw new InvalidDataException("The remote-blank static boundary changed.");
        }

        byte[] actualRemoteData;
        using (FileStream remote = File.OpenRead(remotePath))
        {
            DiscFileRecord wad = RequireRootFile(
                remote,
                remoteLayout,
                "WAD.WAD",
                WadLba,
                UnusedLevel65RemoteBlankIsolationConstruction.WadByteLength);
            actualRemoteData = DiscImage.ReadFileBytes(
                remote,
                remoteLayout,
                wad.Lba,
                DataWadOffset,
                DataByteLength);
        }
        RequireHash(
            Hash(actualRemoteData),
            UnusedLevel65RemoteBlankIsolationConstruction.ExpectedOutputDataSha256,
            "remote-blank row-80 data");
        if (!actualRemoteData.SequenceEqual(remotePlan.OutputData))
            throw new InvalidDataException("The frozen remote-blank BIN does not match its exact static plan.");

        UnusedLevel65MusicClosureRegion mapping = Region(
            lockedExecutable,
            lockedLayout,
            "48-entry initial music mapping table",
            MappingTableFileOffset,
            MappingEntryCount * sizeof(int),
            expectedSha256: "a4be62ad38b6c793831d9cdcc3d92d24278e18df4fab19ddc678ed29e191e5f5");
        UnusedLevel65MusicClosureRegion initialSlot = Region(
            lockedExecutable,
            lockedLayout,
            "ID65 initial mapping slot 35",
            InitialSlotFileOffset,
            sizeof(int),
            expectedHex: "13000000");
        UnusedLevel65MusicClosureRegion initialConsumer = Region(
            lockedExecutable,
            lockedLayout,
            "initial mapping consumer",
            InitialConsumerFileOffset,
            InitialConsumerByteLength,
            expectedSha256: "7698774b00c290b327fe980ea76a6aec22c36ada856351a3f85b32368529d106");
        UnusedLevel65MusicClosureRegion lateTable = Region(
            lockedExecutable,
            lockedLayout,
            "35-row late-alternate source table",
            LateTableFileOffset,
            LateTableByteLength,
            expectedSha256: "9ba86a52d7b508d9a376ec819bbcee27ba9f46553c50dae1ce3fd16432555a2c");
        UnusedLevel65MusicClosureRegion row34 = Region(
            lockedExecutable,
            lockedLayout,
            "late-alternate row 34",
            LateRow34FileOffset,
            LateValuesPerRow * sizeof(int),
            expectedHex: "220000002200000022000000");
        UnusedLevel65MusicClosureRegion peteXaLbaTable = Region(
            lockedExecutable,
            lockedLayout,
            "six PETEXA start LBAs",
            PeteXaLbaTableFileOffset,
            PeteXaLbaTableByteLength,
            expectedSha256: "9fe9ac8b1cc132bb1ca1e43f3fca869d527447209b6729cbd8590b3771091f01");
        UnusedLevel65MusicClosureRegion lateConsumer = Region(
            lockedExecutable,
            lockedLayout,
            "late-selector bounded patch span",
            LateConsumerFileOffset,
            LateConsumerByteLength,
            expectedSha256: "4ccb43171ed02fb55a71a013fde64c40370683821630be5fd2975115a26bfd33");
        UnusedLevel65MusicClosureRegion randomCallee = Region(
            lockedExecutable,
            lockedLayout,
            "late-selector PRNG callee",
            RandomCalleeFileOffset,
            RandomCalleeByteLength,
            expectedSha256: "8bcf455ef340cfd7846fd7937a88671fbf165aa56526545a4912547c8d6bcfd4");

        int[] mappingValues = ReadInt32Array(lockedExecutable, MappingTableFileOffset, MappingEntryCount);
        int[] lateValues = ReadInt32Array(
            lockedExecutable,
            LateTableFileOffset,
            LateTableRowCount * LateValuesPerRow);
        int[] peteXaLbas = ReadInt32Array(lockedExecutable, PeteXaLbaTableFileOffset, ExpectedPeteXaFiles.Length);
        if (!peteXaLbas.SequenceEqual(ExpectedPeteXaFiles.Select(item => item.Lba)))
            throw new InvalidDataException("The PETEXA LBA table no longer matches the ISO directory.");
        ValidateTables(mappingValues, lateValues);

        IReadOnlyList<UnusedLevel65MusicClosureReference> references = ValidateReferences(lockedExecutable);
        IReadOnlyList<UnusedLevel65MusicClosureCallsite> initialCallsites = ValidateCallsites(
            lockedExecutable,
            lockedLayout,
            "initial music consumer",
            InitialConsumerFunctionRuntimeAddress,
            ExpectedInitialConsumerCallerOffsets,
            "DC54000C01000424");
        IReadOnlyList<UnusedLevel65MusicClosureCallsite> lateCallsites = ValidateCallsites(
            lockedExecutable,
            lockedLayout,
            "late music consumer",
            LateConsumerFunctionRuntimeAddress,
            ExpectedLateConsumerCallerOffsets,
            "F8AE000C00000000");

        byte[] outputExecutable = lockedExecutable.ToArray();
        List<UnusedLevel65MusicClosurePatch> patches = [];
        foreach ((string name, int offset, string beforeHex, string afterHex, string semantics) in WitnessCodePatchSpecs)
        {
            patches.Add(ApplyInMemoryPatch(
                outputExecutable,
                lockedLayout,
                name,
                offset,
                beforeHex,
                afterHex,
                semantics));
        }

        MusicTrackEntry witnessTrack = MusicTrackCatalog.Find(WitnessTrackId)
            ?? throw new InvalidDataException("The Town Square witness track disappeared from the native catalog.");
        if (!witnessTrack.IsSelectable || witnessTrack.DisplayName != "Town Square")
            throw new InvalidDataException("The Town Square witness track identity changed.");
        if (MusicTrackCatalog.GetLevelIndex(UnusedLevel65BlankLevelLabProfileRegistry.Definition) != ContinuousLevelIndex ||
            MusicTrackCatalog.GetNativeTrackId(UnusedLevel65BlankLevelLabProfileRegistry.Definition) != 19)
        {
            throw new InvalidDataException("The ID65 generic music-catalog identity changed.");
        }
        bool genericExporterRejected = false;
        try
        {
            _ = LevelMusicPatchExporter.BuildBatchPlan(
                lockedPath,
                Path.Combine(root, "unused-id65-music-static-witness.bin"),
                Path.Combine(root, "unused-id65-music-static-witness.cue"),
                [new(UnusedLevel65BlankLevelLabProfileRegistry.Definition, WitnessTrackId)]);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains(
            "does not have an editable level-music slot",
            StringComparison.Ordinal))
        {
            genericExporterRejected = true;
        }
        if (!genericExporterRejected)
            throw new InvalidDataException("The generic LevelMusicPatchExporter no longer fails closed for ID65.");
        patches.Add(ApplyInMemoryPatch(
            outputExecutable,
            lockedLayout,
            "author-independent-slot35-witness-track",
            InitialSlotFileOffset,
            "13000000",
            ToHex(Int32Bytes(WitnessTrackId)),
            "The independent ID65 initial slot selects built-in Town Square track 26 for the static witness transaction."));
        RequireHash(Hash(outputExecutable), WitnessOutputExecutableSha256, "in-memory witness executable");
        RequireHash(
            Hash(outputExecutable.AsSpan(LateConsumerFileOffset, LateConsumerByteLength)),
            "e653e08a2456a222c79d373f4c1ace08816b57ef1b03bc4abd83aca8cb794f1c",
            "patched late-selector span");
        if (!outputExecutable.AsSpan(PeteXaLbaTableFileOffset, PeteXaLbaTableByteLength)
                .SequenceEqual(lockedExecutable.AsSpan(PeteXaLbaTableFileOffset, PeteXaLbaTableByteLength)))
        {
            throw new InvalidDataException("The in-memory witness transaction changed the PETEXA LBA table.");
        }

        int[] patchedMapping = ReadInt32Array(outputExecutable, MappingTableFileOffset, MappingEntryCount);
        IReadOnlyList<UnusedLevel65MusicClosureTrackProof> trackProofs = BuildTrackProofs(
            mappingValues,
            lateValues);
        int[] reservedTracks = MusicTrackCatalog.Tracks
            .Where(track => !track.IsSelectable)
            .Select(track => track.TrackId)
            .ToArray();
        if (!reservedTracks.SequenceEqual(new[] { 30, 35 }) || trackProofs.Count != 46)
            throw new InvalidDataException("The selectable built-in music-track boundary changed.");

        string retailOutcomeMatrix = BuildRetailOutcomeMatrix(mappingValues, lateValues);
        RequireHash(
            Hash(Encoding.ASCII.GetBytes(retailOutcomeMatrix)),
            "2eccb7780680d77c660ff3dc307d2c85d3e0857d5aca6a0d6a9b22e277b60750",
            "retail music outcome matrix");
        string id65WitnessMatrix = BuildId65OutcomeMatrix(patchedMapping, lateValues);
        RequireHash(
            Hash(Encoding.ASCII.GetBytes(id65WitnessMatrix)),
            "313a9e020668e3ac27ca6d19a1b16dcf4ee03aeae79eb23cfcf0a2d42ae642a5",
            "ID65 witness outcome matrix");
        string supportedTrackSet = string.Join(',', trackProofs.Select(proof => proof.TrackId.ToString("D2", CultureInfo.InvariantCulture)));
        RequireHash(
            Hash(Encoding.ASCII.GetBytes(supportedTrackSet)),
            "911f9c937d4119c3817f023a68774877fd56dd978b78600a14d36bc2e7294e0c",
            "supported built-in track set");

        AssertRetailEquivalence(mappingValues, lateValues);
        AssertId65Resolution(patchedMapping, lateValues, WitnessTrackId);

        int logicalPatchWindowBytes = patches.Sum(patch => patch.ByteLength);
        int changedLogicalBytes = patches.Sum(patch => patch.ChangedByteCount);
        int actualExecutableChangedBytes = lockedExecutable.Zip(outputExecutable).Count(pair => pair.First != pair.Second);
        if (logicalPatchWindowBytes != 52 || changedLogicalBytes != 40 || actualExecutableChangedBytes != 40)
            throw new InvalidDataException("The exact in-memory music transaction diff changed.");
        int[] affectedSectors = patches.Select(patch => patch.RawSectorLba).Distinct().Order().ToArray();
        if (!affectedSectors.SequenceEqual(new[] { 55_438, 55_573 }))
            throw new InvalidDataException("The music transaction no longer occupies exactly two SCUS sectors.");
        using (FileStream locked = File.OpenRead(lockedPath))
        using (FileStream remote = File.OpenRead(remotePath))
        {
            int lockedVerified = RawMode2Form1SectorIntegrity.VerifyAbsoluteSectors(
                locked,
                lockedLayout,
                affectedSectors.Select(lba => (lba, 1)));
            int remoteVerified = RawMode2Form1SectorIntegrity.VerifyAbsoluteSectors(
                remote,
                remoteLayout,
                affectedSectors.Select(lba => (lba, 1)));
            if (lockedVerified != 2 || remoteVerified != 2)
                throw new InvalidDataException("The exact affected MODE2 Form 1 sector set changed.");
        }

        UnusedLevel65MusicClosureMode2Impact mode2 = new(
            RawSectorBytes,
            UserOffset,
            logicalPatchWindowBytes,
            changedLogicalBytes,
            MinimumRawSectorCount: 2,
            affectedSectors,
            affectedSectors.Select(lba => (long)lba * RawSectorBytes).ToArray(),
            AllAffectedSectorsAreMode2Form1: true,
            EdcEccRebuildRequired: true,
            FileExtentUnchanged: true,
            IsoDirectoryUnchanged: true,
            XaAudioSectorsUnchanged: true);

        return new UnusedLevel65MusicOwnershipClosureContract(
            ProfileId,
            LockedDisplayNameImageSha256,
            RemoteBlankImageSha256,
            ExecutableSha256,
            WitnessOutputExecutableSha256,
            UnusedLevel65RemoteBlankIsolationConstruction.ExpectedOutputDataSha256,
            new(lockedExeRecord.Name, lockedExeRecord.Lba, lockedExeRecord.Size),
            peteXaFiles,
            ContinuousLevelIndex,
            WitnessTrackId,
            witnessTrack.DisplayName,
            mapping,
            initialSlot,
            initialConsumer,
            lateTable,
            LateTableRowCount,
            LateValuesPerRow,
            row34,
            peteXaLbaTable,
            lateConsumer,
            randomCallee,
            references,
            initialCallsites,
            lateCallsites,
            patches,
            trackProofs,
            reservedTracks,
            Hash(Encoding.ASCII.GetBytes(retailOutcomeMatrix)),
            Hash(Encoding.ASCII.GetBytes(id65WitnessMatrix)),
            Hash(Encoding.ASCII.GetBytes(supportedTrackSet)),
            mode2,
            LockedAndRemoteExecutablesIdentical: true,
            RemoteBlankOutputMatchesStaticPlan: true,
            RemoteBlankConstructionExcludesExecutable: true,
            InitialSlotIndependent: true,
            NoLateTableExtension: true,
            PeteXaLbaTablePreserved: true,
            PeteXaDirectoryPreserved: true,
            RandomCalleePreservesT0AndT1: true,
            RetailInitialAndLongPlayOutcomesPreserved: true,
            Id65InitialAndLongPlayResolveThroughSlot35: true,
            AllSelectableBuiltInTracksStaticallyClosed: true,
            MinimumTwoSectorTransactionAchieved: true,
            StaticTransactionClosed: true,
            GenericLevelMusicExporterStillRejectsId65: true,
            WritesBin: false,
            WritesCue: false,
            WriterAuthorized: false,
            RuntimeProofComplete: false,
            AppEnabled: false,
            NormalCreateBinEnabled: false,
            PromotionAuthorized: false,
            RuntimeGates:
            [
                "Cold boot ID65 and confirm the selected built-in track begins immediately.",
                "Remain in ID65 past all three native 8-12 minute late-alternate choices and confirm the selected track does not revert.",
                "Pause/unpause, die/respawn, reset, and re-enter ID65; confirm the selected track remains owned.",
                "Run retail music controls spanning ordinary, flight, boss, and hidden-alternate rows; outcomes must match the locked image.",
                "Verify both rebuilt SCUS MODE2 Form 1 sectors after a future transactional writer exists."
            ],
            Notes:
            [
                "The mapping table has 48 entries, while the late-alternate table has exactly 35 three-value rows and ends immediately before PETEXA0.",
                "The special case uses no code cave, no table relocation, no row extension, and no new loaded storage.",
                "Row 34 is exactly [34,34,34]. ID65 alone promotes those source indices to 35 before the unchanged mapping lookup.",
                "All retail indices 0..34 address their exact original late row and preserve their exact source-index and track outcomes.",
                "Track selection remains native-only: PETEXA0-5 directory records, LBA words, and XA audio sectors are not copied or modified.",
                "Two raw sectors are the minimum for a selectable ID65 transaction because the independent initial slot and late consumer occupy distinct SCUS sectors."
            ]);
    }

    private static IReadOnlyList<UnusedLevel65MusicClosureTrackProof> BuildTrackProofs(
        IReadOnlyList<int> originalMapping,
        IReadOnlyList<int> lateValues)
    {
        List<UnusedLevel65MusicClosureTrackProof> proofs = [];
        foreach (MusicTrackEntry track in MusicTrackCatalog.SelectableTracks.OrderBy(track => track.TrackId))
        {
            int[] mapping = originalMapping.ToArray();
            mapping[ContinuousLevelIndex] = track.TrackId;
            AssertRetailEquivalence(mapping, lateValues, compareTracksAgainst: originalMapping);
            int[] longPlay = Enumerable.Range(0, LateValuesPerRow)
                .Select(alternate => ResolvePatchedTrack(mapping, lateValues, ContinuousLevelIndex, alternate).TrackId)
                .ToArray();
            int initial = mapping[ContinuousLevelIndex];
            bool matches = initial == track.TrackId && longPlay.All(value => value == track.TrackId);
            if (!matches)
                throw new InvalidDataException($"Track {track.TrackId} does not close ID65 initial and long-play ownership.");
            proofs.Add(new(
                track.TrackId,
                track.DisplayName,
                track.IsSelectable,
                initial,
                longPlay,
                matches));
        }
        return proofs;
    }

    private static void AssertRetailEquivalence(
        IReadOnlyList<int> mapping,
        IReadOnlyList<int> lateValues,
        IReadOnlyList<int>? compareTracksAgainst = null)
    {
        IReadOnlyList<int> originalMapping = compareTracksAgainst ?? mapping;
        for (int levelIndex = 0; levelIndex < LateTableRowCount; levelIndex++)
        {
            for (int alternate = 0; alternate < LateValuesPerRow; alternate++)
            {
                int originalSource = lateValues[levelIndex * LateValuesPerRow + alternate];
                int originalTrack = originalMapping[originalSource];
                (int rowIndex, int sourceIndex, int trackId) =
                    ResolvePatchedTrack(mapping, lateValues, levelIndex, alternate);
                if (rowIndex != levelIndex || sourceIndex != originalSource || trackId != originalTrack)
                {
                    throw new InvalidDataException(
                        $"The branchless special case changed retail level index {levelIndex}, alternate {alternate}.");
                }
            }
        }
    }

    private static void AssertId65Resolution(
        IReadOnlyList<int> mapping,
        IReadOnlyList<int> lateValues,
        int selectedTrackId)
    {
        if (mapping[ContinuousLevelIndex] != selectedTrackId)
            throw new InvalidDataException("The ID65 initial mapping slot does not contain the selected track.");
        for (int alternate = 0; alternate < LateValuesPerRow; alternate++)
        {
            (int rowIndex, int sourceIndex, int trackId) =
                ResolvePatchedTrack(mapping, lateValues, ContinuousLevelIndex, alternate);
            if (rowIndex != 34 || sourceIndex != ContinuousLevelIndex || trackId != selectedTrackId)
                throw new InvalidDataException("The ID65 long-play special case does not resolve through slot 35.");
        }
    }

    private static (int RowIndex, int SourceIndex, int TrackId) ResolvePatchedTrack(
        IReadOnlyList<int> mapping,
        IReadOnlyList<int> lateValues,
        int levelIndex,
        int alternate)
    {
        if (levelIndex is < 0 or > ContinuousLevelIndex)
            throw new ArgumentOutOfRangeException(nameof(levelIndex));
        if (alternate is < 0 or >= LateValuesPerRow)
            throw new ArgumentOutOfRangeException(nameof(alternate));
        int retailIndicator = levelIndex < ContinuousLevelIndex ? 1 : 0;
        int rowIndex = levelIndex + retailIndicator - 1;
        int id65Indicator = retailIndicator ^ 1;
        int sourceIndex = checked(lateValues[rowIndex * LateValuesPerRow + alternate] + id65Indicator);
        if (sourceIndex is < 0 or >= MappingEntryCount)
            throw new InvalidDataException("The bounded late lookup escaped the mapping table.");
        return (rowIndex, sourceIndex, mapping[sourceIndex]);
    }

    private static string BuildRetailOutcomeMatrix(
        IReadOnlyList<int> mapping,
        IReadOnlyList<int> lateValues)
    {
        List<string> rows = [];
        for (int levelIndex = 0; levelIndex < LateTableRowCount; levelIndex++)
        {
            for (int alternate = 0; alternate < LateValuesPerRow; alternate++)
            {
                int source = lateValues[levelIndex * LateValuesPerRow + alternate];
                rows.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{levelIndex:D2}:{alternate}:{levelIndex:D2}:{source:D2}:{mapping[source]:D2}"));
            }
        }
        return string.Join('\n', rows);
    }

    private static string BuildId65OutcomeMatrix(
        IReadOnlyList<int> mapping,
        IReadOnlyList<int> lateValues)
    {
        List<string> rows = [];
        for (int alternate = 0; alternate < LateValuesPerRow; alternate++)
        {
            (int row, int source, int track) =
                ResolvePatchedTrack(mapping, lateValues, ContinuousLevelIndex, alternate);
            rows.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"{ContinuousLevelIndex:D2}:{alternate}:{row:D2}:{source:D2}:{track:D2}"));
        }
        return string.Join('\n', rows);
    }

    private static void ValidateTables(IReadOnlyList<int> mapping, IReadOnlyList<int> lateValues)
    {
        if (mapping.Count != MappingEntryCount || lateValues.Count != LateTableRowCount * LateValuesPerRow)
            throw new InvalidDataException("The exact music table dimensions changed.");
        for (int index = 0; index < MappingEntryCount; index++)
        {
            if (mapping[index] is < 0 or >= MappingEntryCount)
                throw new InvalidDataException($"Mapping entry {index} is outside the 48 native XA track IDs.");
        }
        for (int index = 0; index < lateValues.Count; index++)
        {
            if (lateValues[index] is < 0 or >= MappingEntryCount)
                throw new InvalidDataException($"Late-alternate source {index} is outside the mapping table.");
        }
        if (!lateValues.Skip(34 * LateValuesPerRow).Take(LateValuesPerRow).SequenceEqual(new[] { 34, 34, 34 }))
            throw new InvalidDataException("Late-alternate row 34 is no longer the exact safe ID65 bridge row.");
        if (LateTableFileOffset + LateTableByteLength != PeteXaLbaTableFileOffset)
            throw new InvalidDataException("The late-table/PETEXA adjacency changed.");
    }

    private static IReadOnlyList<UnusedLevel65MusicClosureReference> ValidateReferences(byte[] executable)
    {
        int[] mappingLoads = FindInstructions(executable, word =>
            (word >> 26) == 0x23 && (word & 0xFFFF) == 0xEF9C);
        if (!mappingLoads.SequenceEqual(new[] { 0x651C, 0x1C5EC }))
            throw new InvalidDataException("The exact two mapping-table consumers changed.");
        int[] lateBases = FindInstructions(executable, word =>
            (word >> 26) == 0x09 && (word & 0xFFFF) == 0xF05C);
        if (!lateBases.SequenceEqual(new[] { 0x1C5A0 }))
            throw new InvalidDataException("The exact late-table base reference changed.");
        int[] peteXaBases = FindInstructions(executable, word =>
            (word >> 26) == 0x09 && (word & 0xFFFF) == 0xF200);
        if (!peteXaBases.SequenceEqual(new[] { 0x2D80 }))
            throw new InvalidDataException("The exact PETEXA LBA-table consumer changed.");

        return
        [
            Reference(executable, "initial mapping lookup", 0x651C, Runtime(MappingTableFileOffset),
                "current continuous index selects one of 48 initial track IDs"),
            Reference(executable, "late mapping lookup", 0x1C5EC, Runtime(MappingTableFileOffset),
                "late source index selects one of the same 48 track IDs"),
            Reference(executable, "late source-row base", 0x1C5A0, Runtime(LateTableFileOffset),
                "current continuous index and random modulo 3 select a late source index"),
            Reference(executable, "PETEXA start-LBA base", 0x2D80, Runtime(PeteXaLbaTableFileOffset),
                "six stream LBAs initialize the 48 native XA track extents")
        ];
    }

    private static UnusedLevel65MusicClosureReference Reference(
        byte[] executable,
        string name,
        int offset,
        uint target,
        string semantics) =>
        new(
            name,
            offset,
            Runtime(offset),
            ToHex(executable.AsSpan(offset, sizeof(uint))),
            target,
            semantics);

    private static IReadOnlyList<UnusedLevel65MusicClosureCallsite> ValidateCallsites(
        byte[] executable,
        DiscLayout layout,
        string consumer,
        uint targetRuntimeAddress,
        IReadOnlyList<int> expectedOffsets,
        string expectedInstructionAndDelayHex)
    {
        int[] offsets = FindDirectJalCallers(executable, targetRuntimeAddress);
        if (!offsets.SequenceEqual(expectedOffsets))
            throw new InvalidDataException($"The exact direct-call graph for {consumer} changed.");
        List<UnusedLevel65MusicClosureCallsite> result = [];
        foreach (int offset in offsets)
        {
            byte[] bytes = executable.AsSpan(offset, 8).ToArray();
            RequireHex(bytes, expectedInstructionAndDelayHex, $"{consumer} callsite @ 0x{offset:X}");
            result.Add(new(
                consumer,
                offset,
                Runtime(offset),
                targetRuntimeAddress,
                ToHex(bytes),
                Hash(bytes),
                DirectJal: true));
        }
        return result;
    }

    private static int[] FindDirectJalCallers(byte[] executable, uint targetRuntimeAddress)
    {
        List<int> offsets = [];
        for (int offset = PsxExeHeaderByteLength; offset <= executable.Length - sizeof(uint); offset += sizeof(uint))
        {
            uint word = BinaryPrimitives.ReadUInt32LittleEndian(executable.AsSpan(offset, sizeof(uint)));
            if ((word >> 26) != 0x03)
                continue;
            uint pc = Runtime(offset);
            uint target = ((pc + 4) & 0xF0000000u) | ((word & 0x03FFFFFFu) << 2);
            if (target == targetRuntimeAddress)
                offsets.Add(offset);
        }
        return offsets.ToArray();
    }

    private static int[] FindInstructions(byte[] executable, Predicate<uint> predicate)
    {
        List<int> offsets = [];
        for (int offset = PsxExeHeaderByteLength; offset <= executable.Length - sizeof(uint); offset += sizeof(uint))
        {
            uint word = BinaryPrimitives.ReadUInt32LittleEndian(executable.AsSpan(offset, sizeof(uint)));
            if (predicate(word))
                offsets.Add(offset);
        }
        return offsets.ToArray();
    }

    private static UnusedLevel65MusicClosurePatch ApplyInMemoryPatch(
        byte[] executable,
        DiscLayout layout,
        string name,
        int offset,
        string beforeHex,
        string afterHex,
        string semantics)
    {
        byte[] before = Convert.FromHexString(beforeHex);
        byte[] after = Convert.FromHexString(afterHex);
        if (before.Length != after.Length || before.Length == 0)
            throw new InvalidOperationException($"Patch {name} has an invalid fixed-size transaction.");
        if (!executable.AsSpan(offset, before.Length).SequenceEqual(before))
            throw new InvalidDataException($"Patch {name} failed its exact SCUS preimage guard.");
        int changed = before.Zip(after).Count(pair => pair.First != pair.Second);
        after.CopyTo(executable, offset);
        return new(
            name,
            offset,
            DiscImage.ConvertFileOffsetToImageOffset(layout, ExecutableLba, offset),
            Runtime(offset),
            before.Length,
            ExecutableLba + offset / UserBytes,
            ToHex(before),
            ToHex(after),
            Hash(before),
            Hash(after),
            changed,
            semantics);
    }

    private static UnusedLevel65MusicClosureRegion Region(
        byte[] executable,
        DiscLayout layout,
        string name,
        int offset,
        int length,
        string? expectedHex = null,
        string? expectedSha256 = null)
    {
        byte[] bytes = executable.AsSpan(offset, length).ToArray();
        if (expectedHex != null)
            RequireHex(bytes, expectedHex, name);
        string sha = Hash(bytes);
        if (expectedSha256 != null)
            RequireHash(sha, expectedSha256, name);
        return new(
            name,
            offset,
            DiscImage.ConvertFileOffsetToImageOffset(layout, ExecutableLba, offset),
            Runtime(offset),
            length,
            ToHex(bytes),
            sha);
    }

    private static void RequirePsxExeHeader(byte[] executable)
    {
        RequireHex(executable.AsSpan(0, 8), "50532D5820455845", "PS-X EXE signature");
        uint loadAddress = BinaryPrimitives.ReadUInt32LittleEndian(executable.AsSpan(0x18, sizeof(uint)));
        int payloadLength = BinaryPrimitives.ReadInt32LittleEndian(executable.AsSpan(0x1C, sizeof(int)));
        if (loadAddress != PsxExeLoadAddress || payloadLength != PsxExePayloadByteLength ||
            PsxExeHeaderByteLength + payloadLength != executable.Length)
        {
            throw new InvalidDataException("The exact relocated PS-X EXE load range changed.");
        }
    }

    private static DiscFileRecord RequireRootFile(
        FileStream image,
        DiscLayout layout,
        string name,
        int expectedLba,
        int expectedSize)
    {
        DiscFileRecord record = DiscImage.FindRootFileRecord(
            image,
            layout,
            candidate => candidate.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (record.Lba != expectedLba || record.Size != expectedSize)
            throw new InvalidDataException($"The exact {name} ISO extent changed.");
        return record;
    }

    private static int[] ReadInt32Array(byte[] bytes, int offset, int count) =>
        Enumerable.Range(0, count)
            .Select(index => BinaryPrimitives.ReadInt32LittleEndian(
                bytes.AsSpan(offset + index * sizeof(int), sizeof(int))))
            .ToArray();

    private static byte[] Int32Bytes(int value)
    {
        byte[] bytes = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        return bytes;
    }

    private static uint Runtime(int fileOffset) => checked(RuntimeBias + (uint)fileOffset);

    private static void RequireMode2Layout(DiscLayout layout, string name)
    {
        if (layout.SectorSize != RawSectorBytes || layout.UserOffset != UserOffset)
            throw new InvalidDataException($"The {name} is not exact MODE2/2352 Form 1 data layout.");
    }

    private static string RequireFile(string path, string name)
    {
        string full = Path.GetFullPath(path);
        if (!File.Exists(full))
            throw new FileNotFoundException($"The exact {name} is missing.", full);
        return full;
    }

    private static void RequireHex(ReadOnlySpan<byte> actual, string expectedHex, string name)
    {
        byte[] expected = Convert.FromHexString(expectedHex);
        if (!actual.SequenceEqual(expected))
            throw new InvalidDataException($"The exact {name} bytes changed.");
    }

    private static void RequireHash(string actual, string expected, string name)
    {
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"The exact {name} SHA-256 changed: {actual}.");
    }

    private static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static string HashFile(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string ToHex(ReadOnlySpan<byte> bytes) => Convert.ToHexString(bytes);
}
