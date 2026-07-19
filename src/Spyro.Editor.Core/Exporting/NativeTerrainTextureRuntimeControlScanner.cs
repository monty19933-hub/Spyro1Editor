using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Exporting;

public enum NativeTerrainTextureRuntimeControlKind
{
    FullRecordAnimation,
    ScrollingDescriptor
}

public sealed record NativeTerrainTextureRuntimeControl(
    int TextureId,
    NativeTerrainTextureRuntimeControlKind Kind,
    int PointerIndex,
    int SceneRelativeStructureOffset,
    string Description);

public sealed record NativeTerrainTextureAnimationFrame(
    int FrameIndex,
    byte DurationAndFlags,
    int DurationTicks,
    byte StateFlags,
    int ForwardNextFrame,
    int ReverseNextFrame,
    int SourceTextureId);

public sealed record NativeTerrainTextureAnimationControl(
    int ControlId,
    byte InitialStateFlags,
    int InitialCurrentFrame,
    int InitialTicksRemaining,
    int DestinationTextureId,
    int InitialSourceTextureId,
    int PointerIndex,
    int SceneRelativeStructureOffset,
    string RawBytesHex,
    IReadOnlyList<NativeTerrainTextureAnimationFrame> Frames);

public sealed record NativeTerrainScrollingTextureFrame(
    int FrameIndex,
    byte DurationAndFlags,
    int DurationTicks,
    byte StateFlags,
    int ForwardNextFrame,
    int ReverseNextFrame,
    int PhaseDelta);

public sealed record NativeTerrainScrollingTextureControl(
    int ControlId,
    byte InitialStateFlags,
    int InitialCurrentFrame,
    int InitialTicksRemaining,
    int DestinationTextureId,
    int InitialPhase,
    int PointerIndex,
    int SceneRelativeStructureOffset,
    string RawBytesHex,
    IReadOnlyList<NativeTerrainScrollingTextureFrame> Frames);

public sealed record NativeTerrainTextureInitialStateMutation(
    NativeTerrainTextureRuntimeControlKind Kind,
    int ControlId,
    int PointerIndex,
    int DestinationTextureId,
    int? InitialSourceTextureId,
    int? InitialPhase,
    int LqChangedByteCount,
    int HqChangedByteCount,
    string OriginalLqSha256,
    string InitializedLqSha256,
    string OriginalHqSha256,
    string InitializedHqSha256);

public sealed record NativeTerrainTextureInitialStateResult(
    bool Complete,
    int TextureCount,
    int TextureListSize,
    int LowDetailTableOffset,
    int HighDetailTableOffset,
    byte[] OriginalTextureData,
    byte[] InitializedTextureData,
    IReadOnlyList<NativeTerrainTextureInitialStateMutation> Mutations,
    IReadOnlyList<string> SafetyBlockers)
{
    public int ChangedLqByteCount => Mutations.Sum(mutation => mutation.LqChangedByteCount);
    public int ChangedHqByteCount => Mutations.Sum(mutation => mutation.HqChangedByteCount);
}

/// <summary>
/// Exact, source-bound description of the native texture animation and scrolling
/// programs embedded in a retail level scene. The detailed programs are safe to
/// use only when <see cref="Complete"/> is true.
/// </summary>
public sealed record NativeTerrainTextureRuntimeControlAudit(
    int TargetWadEntry,
    int TextureCount,
    int SceneByteLength,
    string SceneSha256,
    bool Complete,
    IReadOnlyList<NativeTerrainTextureRuntimeControl> Controls,
    IReadOnlyList<NativeTerrainTextureAnimationControl> AnimationControls,
    IReadOnlyList<NativeTerrainScrollingTextureControl> ScrollingControls,
    IReadOnlyList<string> SafetyBlockers,
    IReadOnlyList<string> Notes)
{
    public IReadOnlyList<int> AnimatedTextureIds => Controls
        .Where(control => control.Kind == NativeTerrainTextureRuntimeControlKind.FullRecordAnimation)
        .Select(control => control.TextureId)
        .Distinct()
        .Order()
        .ToArray();

    public IReadOnlyList<int> ScrollingTextureIds => Controls
        .Where(control => control.Kind == NativeTerrainTextureRuntimeControlKind.ScrollingDescriptor)
        .Select(control => control.TextureId)
        .Distinct()
        .Order()
        .ToArray();

    public IReadOnlyList<int> ControlledTextureIds => Controls
        .Select(control => control.TextureId)
        .Distinct()
        .Order()
        .ToArray();

    public IReadOnlyList<int> AnimationSourceTextureIds => AnimationControls
        .SelectMany(control => control.Frames)
        .Select(frame => frame.SourceTextureId)
        .Distinct()
        .Order()
        .ToArray();

    public bool IsRuntimePersistentTarget(int textureId) =>
        Complete && textureId >= 0 && textureId < TextureCount &&
        !Controls.Any(control => control.TextureId == textureId);

    public string TargetReadinessNote(int textureId)
    {
        if (!Complete)
            return SafetyBlockers.FirstOrDefault() ?? "Runtime texture-control inspection is incomplete.";
        if (textureId < 0 || textureId >= TextureCount)
            return $"Texture {textureId} is outside the decoded {TextureCount}-record table.";

        NativeTerrainTextureRuntimeControl[] matches = Controls
            .Where(control => control.TextureId == textureId)
            .ToArray();
        if (matches.Length == 0)
            return "The level scene does not rewrite this target texture record at runtime.";

        string kinds = string.Join(" and ", matches
            .Select(control => control.Kind == NativeTerrainTextureRuntimeControlKind.FullRecordAnimation
                ? "full-record animation"
                : "scrolling-descriptor control")
            .Distinct(StringComparer.Ordinal));
        return $"Texture {textureId} is controlled by {kinds}; a relocated record could be overwritten after level load.";
    }
}

public static class NativeTerrainTextureRuntimeControlScanner
{
    private const int WadLba = 37;
    private const int LevelHeaderMinimumBytes = 0x20;
    private const int LevelDataOffsetField = 0x08;
    private const int LevelDataSizeField = 0x0C;
    private const int SceneOffsetField = 0x18;
    private const int SceneSizeField = 0x1C;
    private const int LowDetailRecordBytes = 16;
    private const int HighDetailRecordBytes = 168;

    // LevelSceneHeader is Vector3D (12), Vector3D8 (4), and fifteen
    // eight-byte Tiledefs: flame, shadow, unused, orb/egg[10], superflame,
    // and the specular/metal texture.
    private const int SceneControlComponentsOffset = 16 + (15 * 8);

    public static NativeTerrainTextureRuntimeControlAudit Inspect(
        string sourceImagePath,
        LevelDefinition level)
    {
        ArgumentNullException.ThrowIfNull(level);
        return Inspect(sourceImagePath, level.SourceWadEntry);
    }

    public static NativeTerrainTextureRuntimeControlAudit Inspect(
        string sourceImagePath,
        int targetWadEntry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceImagePath);
        if (!File.Exists(sourceImagePath))
            throw new FileNotFoundException("The retail source image is required for runtime texture-control inspection.", sourceImagePath);
        if (targetWadEntry < 0)
            throw new ArgumentOutOfRangeException(nameof(targetWadEntry));

        DiscLayout layout = DiscImage.DetectLayout(sourceImagePath);
        using FileStream image = File.OpenRead(sourceImagePath);
        List<string> blockers = [];
        List<string> notes =
        [
            "The load-state preview applies func_8002B4AC semantics: animation copies first in pointer order, then scrolling-coordinate rewrites in pointer order.",
            "The retail texture-animation update copies a complete 16-byte LQ plus 168-byte HQ record over each animation destination texture id.",
            "The retail scrolling-texture update mutates LQ and HQ descriptor coordinates for each scrolling destination texture id.",
            "This audit describes native load/default state only; level scripts can later pause, reverse, or select frames."
        ];

        byte[] wadHeader = DiscImage.ReadFileBytes(image, layout, WadLba, 0, 4096);
        int entryHeaderOffset = checked(targetWadEntry * 8);
        if (entryHeaderOffset + 8 > wadHeader.Length)
        {
            blockers.Add($"WAD entry {targetWadEntry} is outside the retail top-level header.");
            return Incomplete(targetWadEntry, blockers, notes);
        }

        long entryOffset = BinaryPrimitives.ReadUInt32LittleEndian(wadHeader.AsSpan(entryHeaderOffset, 4));
        long entrySize = BinaryPrimitives.ReadUInt32LittleEndian(wadHeader.AsSpan(entryHeaderOffset + 4, 4));
        if (entryOffset <= 0 || entrySize < LevelHeaderMinimumBytes)
        {
            blockers.Add($"WAD entry {targetWadEntry} does not contain a readable level header.");
            return Incomplete(targetWadEntry, blockers, notes);
        }

        byte[] levelHeader = DiscImage.ReadFileBytes(image, layout, WadLba, entryOffset, LevelHeaderMinimumBytes);
        long levelDataOffset = ReadUInt32(levelHeader, LevelDataOffsetField);
        long levelDataSize = ReadUInt32(levelHeader, LevelDataSizeField);
        long sceneOffset = ReadUInt32(levelHeader, SceneOffsetField);
        long sceneSize = ReadUInt32(levelHeader, SceneSizeField);
        if (!IsEntryRange(levelDataOffset, levelDataSize, entrySize) || levelDataSize < 8)
            blockers.Add("The level-data range containing the terrain texture count is outside the level entry.");
        if (!IsEntryRange(sceneOffset, sceneSize, entrySize) || sceneSize < SceneControlComponentsOffset + 16)
            blockers.Add("The level-scene range containing texture runtime controls is outside the level entry.");
        if (blockers.Count > 0)
            return Incomplete(targetWadEntry, blockers, notes);

        byte[] levelDataLead = DiscImage.ReadFileBytes(image, layout, WadLba, entryOffset + levelDataOffset, 8);
        uint textureComponentSizeRaw = ReadUInt32(levelDataLead, 0);
        uint textureCountRaw = ReadUInt32(levelDataLead, 4);
        if (textureComponentSizeRaw > int.MaxValue || textureCountRaw > int.MaxValue)
        {
            blockers.Add("The terrain texture component size/count exceeds the supported integer range.");
            return Incomplete(targetWadEntry, blockers, notes);
        }

        int textureComponentSize = (int)textureComponentSizeRaw;
        int textureCount = (int)textureCountRaw;
        int expectedTextureComponentSize = textureCount is > 0 and <= 128
            ? checked(8 + (textureCount * (LowDetailRecordBytes + HighDetailRecordBytes)))
            : -1;
        if (textureComponentSize < 8 || textureComponentSize > levelDataSize ||
            textureCount <= 0 || textureCount > 128 || textureComponentSize != expectedTextureComponentSize)
        {
            blockers.Add($"The terrain texture component has invalid size/count values ({textureComponentSize} bytes, {textureCount} records).");
            return Incomplete(targetWadEntry, blockers, notes, textureCount);
        }

        byte[] scene = DiscImage.ReadFileBytes(image, layout, WadLba, entryOffset + sceneOffset, checked((int)sceneSize));
        List<NativeTerrainTextureRuntimeControl> controls = [];
        List<NativeTerrainTextureAnimationControl> animationControls = [];
        List<NativeTerrainScrollingTextureControl> scrollingControls = [];
        int nextComponent = ParseAnimationComponent(
            scene,
            SceneControlComponentsOffset,
            textureCount,
            controls,
            animationControls,
            blockers);
        if (nextComponent >= 0)
        {
            ParseScrollingComponent(
                scene,
                nextComponent,
                textureCount,
                controls,
                scrollingControls,
                blockers);
        }

        return new NativeTerrainTextureRuntimeControlAudit(
            TargetWadEntry: targetWadEntry,
            TextureCount: textureCount,
            SceneByteLength: scene.Length,
            SceneSha256: Convert.ToHexString(SHA256.HashData(scene)),
            Complete: blockers.Count == 0,
            Controls: controls
                .OrderBy(control => control.TextureId)
                .ThenBy(control => control.Kind)
                .ThenBy(control => control.PointerIndex)
                .ToArray(),
            AnimationControls: animationControls.OrderBy(control => control.PointerIndex).ToArray(),
            ScrollingControls: scrollingControls.OrderBy(control => control.PointerIndex).ToArray(),
            SafetyBlockers: blockers.ToArray(),
            Notes: notes);
    }

    /// <summary>
    /// Clones a native terrain texture component/model subfile and applies the
    /// exact load-time texture initialization performed by func_8002B4AC.
    /// The returned data is usable only when Complete is true.
    /// </summary>
    public static NativeTerrainTextureInitialStateResult InitializeTextureRecords(
        NativeTerrainTextureRuntimeControlAudit audit,
        ReadOnlySpan<byte> textureData)
    {
        ArgumentNullException.ThrowIfNull(audit);
        byte[] original = textureData.ToArray();
        byte[] initialized = original.ToArray();
        List<string> blockers = [];
        List<NativeTerrainTextureInitialStateMutation> mutations = [];

        if (!audit.Complete)
            blockers.Add(audit.SafetyBlockers.FirstOrDefault() ?? "Runtime texture-control inspection is incomplete.");
        if (textureData.Length < 8)
            blockers.Add("The native terrain texture component is shorter than its eight-byte header.");

        int textureListSize = 0;
        int textureCount = 0;
        int highDetailTableOffset = 0;
        if (textureData.Length >= 8)
        {
            uint textureListSizeRaw = BinaryPrimitives.ReadUInt32LittleEndian(textureData[..4]);
            uint textureCountRaw = BinaryPrimitives.ReadUInt32LittleEndian(textureData.Slice(4, 4));
            if (textureListSizeRaw > int.MaxValue || textureCountRaw > int.MaxValue)
            {
                blockers.Add("The native terrain texture component header exceeds the supported integer range.");
            }
            else
            {
                textureListSize = (int)textureListSizeRaw;
                textureCount = (int)textureCountRaw;
                long expectedSize = 8L + (textureCount * (long)(LowDetailRecordBytes + HighDetailRecordBytes));
                if (textureCount <= 0 || textureCount > 128 || expectedSize > int.MaxValue ||
                    textureListSize != expectedSize || textureListSize > textureData.Length)
                {
                    blockers.Add($"The native terrain texture component has invalid size/count values ({textureListSize} bytes, {textureCount} records).");
                }
                else
                {
                    highDetailTableOffset = checked(8 + (textureCount * LowDetailRecordBytes));
                }
            }
        }

        if (textureCount != audit.TextureCount)
            blockers.Add($"The texture component contains {textureCount} records, but the control audit is bound to {audit.TextureCount} records.");
        if (blockers.Count > 0)
        {
            return new NativeTerrainTextureInitialStateResult(
                false,
                Math.Max(0, textureCount),
                Math.Max(0, textureListSize),
                8,
                Math.Max(0, highDetailTableOffset),
                original,
                initialized,
                mutations,
                blockers);
        }

        foreach (NativeTerrainTextureAnimationControl control in audit.AnimationControls.OrderBy(control => control.PointerIndex))
        {
            if (!IsValidTexture(control.DestinationTextureId, textureCount) ||
                !IsValidTexture(control.InitialSourceTextureId, textureCount))
            {
                blockers.Add($"Animation control {control.PointerIndex} has an invalid initial source or destination texture id.");
                continue;
            }

            int destinationLq = checked(8 + (control.DestinationTextureId * LowDetailRecordBytes));
            int sourceLq = checked(8 + (control.InitialSourceTextureId * LowDetailRecordBytes));
            int destinationHq = checked(highDetailTableOffset + (control.DestinationTextureId * HighDetailRecordBytes));
            int sourceHq = checked(highDetailTableOffset + (control.InitialSourceTextureId * HighDetailRecordBytes));
            byte[] beforeLq = initialized.AsSpan(destinationLq, LowDetailRecordBytes).ToArray();
            byte[] beforeHq = initialized.AsSpan(destinationHq, HighDetailRecordBytes).ToArray();
            initialized.AsSpan(sourceLq, LowDetailRecordBytes).CopyTo(initialized.AsSpan(destinationLq, LowDetailRecordBytes));
            initialized.AsSpan(sourceHq, HighDetailRecordBytes).CopyTo(initialized.AsSpan(destinationHq, HighDetailRecordBytes));
            mutations.Add(BuildMutation(
                NativeTerrainTextureRuntimeControlKind.FullRecordAnimation,
                control.ControlId,
                control.PointerIndex,
                control.DestinationTextureId,
                control.InitialSourceTextureId,
                null,
                beforeLq,
                initialized.AsSpan(destinationLq, LowDetailRecordBytes),
                beforeHq,
                initialized.AsSpan(destinationHq, HighDetailRecordBytes)));
        }

        foreach (NativeTerrainScrollingTextureControl control in audit.ScrollingControls.OrderBy(control => control.PointerIndex))
        {
            if (!IsValidTexture(control.DestinationTextureId, textureCount) || control.InitialPhase is < 0 or > 0x7F)
            {
                blockers.Add($"Scrolling control {control.PointerIndex} has an invalid initial phase or destination texture id.");
                continue;
            }

            int destinationLq = checked(8 + (control.DestinationTextureId * LowDetailRecordBytes));
            int destinationHq = checked(highDetailTableOffset + (control.DestinationTextureId * HighDetailRecordBytes));
            byte[] beforeLq = initialized.AsSpan(destinationLq, LowDetailRecordBytes).ToArray();
            byte[] beforeHq = initialized.AsSpan(destinationHq, HighDetailRecordBytes).ToArray();
            RewriteScrollingCoordinates(
                initialized.AsSpan(destinationLq, LowDetailRecordBytes),
                initialized.AsSpan(destinationHq, HighDetailRecordBytes),
                control.InitialPhase);
            mutations.Add(BuildMutation(
                NativeTerrainTextureRuntimeControlKind.ScrollingDescriptor,
                control.ControlId,
                control.PointerIndex,
                control.DestinationTextureId,
                null,
                control.InitialPhase,
                beforeLq,
                initialized.AsSpan(destinationLq, LowDetailRecordBytes),
                beforeHq,
                initialized.AsSpan(destinationHq, HighDetailRecordBytes)));
        }

        return new NativeTerrainTextureInitialStateResult(
            Complete: blockers.Count == 0 && mutations.Count == audit.AnimationControls.Count + audit.ScrollingControls.Count,
            TextureCount: textureCount,
            TextureListSize: textureListSize,
            LowDetailTableOffset: 8,
            HighDetailTableOffset: highDetailTableOffset,
            OriginalTextureData: original,
            InitializedTextureData: initialized,
            Mutations: mutations,
            SafetyBlockers: blockers);
    }

    private static int ParseAnimationComponent(
        byte[] scene,
        int componentOffset,
        int textureCount,
        List<NativeTerrainTextureRuntimeControl> controls,
        List<NativeTerrainTextureAnimationControl> detailedControls,
        List<string> blockers)
    {
        if (!TryReadComponent(scene, componentOffset, "texture-animation", out ComponentLayout component, blockers))
            return -1;

        foreach (ComponentStructure structure in component.Structures.OrderBy(structure => structure.PointerIndex))
        {
            int frameCount = ValidateStructureLength(structure, "texture-animation", blockers);
            if (frameCount <= 0)
                continue;

            int destinationTextureId = ReadInt32Checked(scene, structure.Offset + 4, "texture-animation destination", blockers);
            if (!IsValidTexture(destinationTextureId, textureCount))
            {
                blockers.Add($"The texture-animation pointer {structure.PointerIndex} targets texture {destinationTextureId}, outside the {textureCount}-record table.");
                continue;
            }

            int currentFrame = scene[structure.Offset + 2];
            if (currentFrame >= frameCount)
            {
                blockers.Add($"The texture-animation pointer {structure.PointerIndex} selects frame {currentFrame}, outside its {frameCount}-frame program.");
                continue;
            }

            List<NativeTerrainTextureAnimationFrame> frames = [];
            bool framesValid = true;
            for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                int frameOffset = structure.Offset + 8 + (frameIndex * 4);
                byte durationAndFlags = scene[frameOffset];
                int duration = durationAndFlags >> 2;
                byte stateFlags = (byte)(durationAndFlags & 3);
                int forward = scene[frameOffset + 1];
                int reverse = scene[frameOffset + 2];
                int sourceTextureId = scene[frameOffset + 3];
                if (forward >= frameCount || reverse >= frameCount)
                {
                    blockers.Add($"The texture-animation pointer {structure.PointerIndex} frame {frameIndex} has an out-of-range transition ({forward}/{reverse}).");
                    framesValid = false;
                }
                if (!IsValidTexture(sourceTextureId, textureCount))
                {
                    blockers.Add($"The texture-animation pointer {structure.PointerIndex} frame {frameIndex} references source texture {sourceTextureId}, outside the {textureCount}-record table.");
                    framesValid = false;
                }
                if (duration == 0 && (stateFlags & 2) == 0)
                {
                    blockers.Add($"The texture-animation pointer {structure.PointerIndex} frame {frameIndex} has a zero duration without the pause flag.");
                    framesValid = false;
                }

                frames.Add(new NativeTerrainTextureAnimationFrame(
                    frameIndex,
                    durationAndFlags,
                    duration,
                    stateFlags,
                    forward,
                    reverse,
                    sourceTextureId));
            }
            if (!framesValid)
                continue;

            NativeTerrainTextureAnimationControl detailed = new(
                ControlId: scene[structure.Offset],
                InitialStateFlags: scene[structure.Offset + 1],
                InitialCurrentFrame: currentFrame,
                InitialTicksRemaining: scene[structure.Offset + 3],
                DestinationTextureId: destinationTextureId,
                InitialSourceTextureId: frames[currentFrame].SourceTextureId,
                PointerIndex: structure.PointerIndex,
                SceneRelativeStructureOffset: structure.Offset,
                RawBytesHex: Convert.ToHexString(scene.AsSpan(structure.Offset, structure.Length)),
                Frames: frames);
            detailedControls.Add(detailed);
            controls.Add(new NativeTerrainTextureRuntimeControl(
                destinationTextureId,
                NativeTerrainTextureRuntimeControlKind.FullRecordAnimation,
                structure.PointerIndex,
                structure.Offset,
                $"At native load, texture {destinationTextureId} receives complete LQ/HQ records from source texture {detailed.InitialSourceTextureId}."));
        }

        return component.EndOffset;
    }

    private static int ParseScrollingComponent(
        byte[] scene,
        int componentOffset,
        int textureCount,
        List<NativeTerrainTextureRuntimeControl> controls,
        List<NativeTerrainScrollingTextureControl> detailedControls,
        List<string> blockers)
    {
        if (!TryReadComponent(scene, componentOffset, "scrolling-texture", out ComponentLayout component, blockers))
            return -1;

        foreach (ComponentStructure structure in component.Structures.OrderBy(structure => structure.PointerIndex))
        {
            int frameCount = ValidateStructureLength(structure, "scrolling-texture", blockers);
            if (frameCount <= 0)
                continue;

            int destinationTextureId = BinaryPrimitives.ReadUInt16LittleEndian(scene.AsSpan(structure.Offset + 4, 2));
            int initialPhase = BinaryPrimitives.ReadUInt16LittleEndian(scene.AsSpan(structure.Offset + 6, 2));
            if (!IsValidTexture(destinationTextureId, textureCount))
            {
                blockers.Add($"The scrolling-texture pointer {structure.PointerIndex} targets texture {destinationTextureId}, outside the {textureCount}-record table.");
                continue;
            }
            if (initialPhase > 0x7F)
            {
                blockers.Add($"The scrolling-texture pointer {structure.PointerIndex} has initial phase {initialPhase}, outside the native 0..127 phase ring.");
                continue;
            }

            int currentFrame = scene[structure.Offset + 2];
            if (currentFrame >= frameCount)
            {
                blockers.Add($"The scrolling-texture pointer {structure.PointerIndex} selects frame {currentFrame}, outside its {frameCount}-frame program.");
                continue;
            }

            List<NativeTerrainScrollingTextureFrame> frames = [];
            bool framesValid = true;
            for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                int frameOffset = structure.Offset + 8 + (frameIndex * 4);
                byte durationAndFlags = scene[frameOffset];
                int duration = durationAndFlags >> 2;
                byte stateFlags = (byte)(durationAndFlags & 3);
                int forward = scene[frameOffset + 1];
                int reverse = scene[frameOffset + 2];
                int phaseDelta = unchecked((sbyte)scene[frameOffset + 3]);
                if (forward >= frameCount || reverse >= frameCount)
                {
                    blockers.Add($"The scrolling-texture pointer {structure.PointerIndex} frame {frameIndex} has an out-of-range transition ({forward}/{reverse}).");
                    framesValid = false;
                }
                if (duration == 0 && (stateFlags & 2) == 0)
                {
                    blockers.Add($"The scrolling-texture pointer {structure.PointerIndex} frame {frameIndex} has a zero duration without the pause flag.");
                    framesValid = false;
                }

                frames.Add(new NativeTerrainScrollingTextureFrame(
                    frameIndex,
                    durationAndFlags,
                    duration,
                    stateFlags,
                    forward,
                    reverse,
                    phaseDelta));
            }
            if (!framesValid)
                continue;

            NativeTerrainScrollingTextureControl detailed = new(
                ControlId: scene[structure.Offset],
                InitialStateFlags: scene[structure.Offset + 1],
                InitialCurrentFrame: currentFrame,
                InitialTicksRemaining: scene[structure.Offset + 3],
                DestinationTextureId: destinationTextureId,
                InitialPhase: initialPhase,
                PointerIndex: structure.PointerIndex,
                SceneRelativeStructureOffset: structure.Offset,
                RawBytesHex: Convert.ToHexString(scene.AsSpan(structure.Offset, structure.Length)),
                Frames: frames);
            detailedControls.Add(detailed);
            controls.Add(new NativeTerrainTextureRuntimeControl(
                destinationTextureId,
                NativeTerrainTextureRuntimeControlKind.ScrollingDescriptor,
                structure.PointerIndex,
                structure.Offset,
                $"At native load, texture {destinationTextureId} receives scrolling descriptor coordinates for phase {initialPhase}."));
        }

        return component.EndOffset;
    }

    private static bool TryReadComponent(
        byte[] scene,
        int componentOffset,
        string label,
        out ComponentLayout component,
        List<string> blockers)
    {
        component = null!;
        if (componentOffset < 0 || componentOffset + 8 > scene.Length)
        {
            blockers.Add($"The {label} component header is outside the level scene.");
            return false;
        }

        uint componentSizeRaw = ReadUInt32(scene, componentOffset);
        uint countRaw = ReadUInt32(scene, componentOffset + 4);
        if (componentSizeRaw > int.MaxValue || countRaw > 1024)
        {
            blockers.Add($"The {label} component size/count exceeds the supported range.");
            return false;
        }

        int componentSize = (int)componentSizeRaw;
        int count = (int)countRaw;
        long componentEndLong = (long)componentOffset + componentSize;
        int pointerTableEnd = checked(componentOffset + 8 + (count * 4));
        if (componentSize < 8 || componentEndLong > scene.Length || pointerTableEnd > componentEndLong)
        {
            blockers.Add($"The {label} component has invalid size/count values ({componentSize} bytes, {count} controls).");
            return false;
        }

        int componentEnd = checked((int)componentEndLong);
        List<(int PointerIndex, int Offset)> resolved = [];
        for (int index = 0; index < count; index++)
        {
            int pointerOffset = checked(componentOffset + 8 + (index * 4));
            long structureOffsetLong = componentOffset + 4L + ReadUInt32(scene, pointerOffset);
            if (structureOffsetLong < pointerTableEnd || structureOffsetLong + 8 > componentEnd)
            {
                blockers.Add($"The {label} pointer {index} resolves outside its component data area.");
                continue;
            }
            resolved.Add((index, checked((int)structureOffsetLong)));
        }

        foreach (IGrouping<int, (int PointerIndex, int Offset)> duplicate in resolved.GroupBy(item => item.Offset).Where(group => group.Count() > 1))
            blockers.Add($"The {label} pointer table aliases structure offset 0x{duplicate.Key:X}.");

        int[] uniqueOffsets = resolved.Select(item => item.Offset).Distinct().Order().ToArray();
        List<ComponentStructure> structures = [];
        foreach ((int pointerIndex, int offset) in resolved)
        {
            int end = uniqueOffsets.FirstOrDefault(candidate => candidate > offset);
            if (end == 0)
                end = componentEnd;
            structures.Add(new ComponentStructure(pointerIndex, offset, checked(end - offset)));
        }

        component = new ComponentLayout(componentOffset, componentEnd, structures);
        return resolved.Count == count && blockers.Count == 0;
    }

    private static int ValidateStructureLength(ComponentStructure structure, string label, List<string> blockers)
    {
        if (structure.Length < 12 || (structure.Length - 8) % 4 != 0)
        {
            blockers.Add($"The {label} pointer {structure.PointerIndex} has invalid structure length {structure.Length}.");
            return -1;
        }
        return (structure.Length - 8) / 4;
    }

    private static int ReadInt32Checked(byte[] bytes, int offset, string label, List<string> blockers)
    {
        uint raw = ReadUInt32(bytes, offset);
        if (raw > int.MaxValue)
        {
            blockers.Add($"The {label} exceeds the supported integer range.");
            return -1;
        }
        return (int)raw;
    }

    private static void RewriteScrollingCoordinates(Span<byte> lqRecord, Span<byte> hqRecord, int phase)
    {
        byte quarter = (byte)(phase >> 2);
        foreach (int offset in new[] { 1, 5, 9, 13 })
            lqRecord[offset] = quarter;

        hqRecord[1] = quarter;
        hqRecord[5] = quarter;
        byte half = (byte)(phase >> 1);
        WriteOffsets(hqRecord, half, 9, 13, 17, 21);
        WriteOffsets(hqRecord, (byte)((half + 32) & 63), 25, 29, 33, 37);
        WriteOffsets(hqRecord, half, 41, 45, 49, 53, 57, 61, 65, 69);
        WriteOffsets(hqRecord, (byte)((half + 16) & 63), 73, 77, 81, 85, 89, 93, 97, 101);
        WriteOffsets(hqRecord, (byte)((half + 32) & 63), 105, 109, 113, 117, 121, 125, 129, 133);
        WriteOffsets(hqRecord, (byte)((half + 48) & 63), 137, 141, 145, 149, 153, 157, 161, 165);
    }

    private static void WriteOffsets(Span<byte> bytes, byte value, params int[] offsets)
    {
        foreach (int offset in offsets)
            bytes[offset] = value;
    }

    private static NativeTerrainTextureInitialStateMutation BuildMutation(
        NativeTerrainTextureRuntimeControlKind kind,
        int controlId,
        int pointerIndex,
        int destinationTextureId,
        int? initialSourceTextureId,
        int? initialPhase,
        ReadOnlySpan<byte> beforeLq,
        ReadOnlySpan<byte> afterLq,
        ReadOnlySpan<byte> beforeHq,
        ReadOnlySpan<byte> afterHq) =>
        new(
            kind,
            controlId,
            pointerIndex,
            destinationTextureId,
            initialSourceTextureId,
            initialPhase,
            CountChangedBytes(beforeLq, afterLq),
            CountChangedBytes(beforeHq, afterHq),
            Convert.ToHexString(SHA256.HashData(beforeLq)),
            Convert.ToHexString(SHA256.HashData(afterLq)),
            Convert.ToHexString(SHA256.HashData(beforeHq)),
            Convert.ToHexString(SHA256.HashData(afterHq)));

    private static int CountChangedBytes(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        int count = 0;
        for (int index = 0; index < Math.Min(left.Length, right.Length); index++)
        {
            if (left[index] != right[index])
                count++;
        }
        return count + Math.Abs(left.Length - right.Length);
    }

    private static NativeTerrainTextureRuntimeControlAudit Incomplete(
        int targetWadEntry,
        IReadOnlyList<string> blockers,
        IReadOnlyList<string> notes,
        int textureCount = 0) =>
        new(
            TargetWadEntry: targetWadEntry,
            TextureCount: Math.Max(0, textureCount),
            SceneByteLength: 0,
            SceneSha256: "",
            Complete: false,
            Controls: Array.Empty<NativeTerrainTextureRuntimeControl>(),
            AnimationControls: Array.Empty<NativeTerrainTextureAnimationControl>(),
            ScrollingControls: Array.Empty<NativeTerrainScrollingTextureControl>(),
            SafetyBlockers: blockers.ToArray(),
            Notes: notes);

    private static bool IsValidTexture(int textureId, int textureCount) => textureId >= 0 && textureId < textureCount;

    private static bool IsEntryRange(long offset, long length, long entrySize) =>
        offset >= 0 && length > 0 && offset <= entrySize && length <= entrySize - offset;

    private static uint ReadUInt32(byte[] bytes, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));

    private sealed record ComponentLayout(int StartOffset, int EndOffset, IReadOnlyList<ComponentStructure> Structures);
    private sealed record ComponentStructure(int PointerIndex, int Offset, int Length);
}
