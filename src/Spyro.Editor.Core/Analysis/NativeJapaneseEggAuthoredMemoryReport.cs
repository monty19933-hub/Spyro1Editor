using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using Spyro.Editor.Core.Exporting;

[assembly: InternalsVisibleTo("Spyro.Editor.NativeJapaneseEggAuthoredMemorySmoke")]

namespace Spyro.Editor.Core.Analysis;

/// <summary>
/// Actual two-authored-class normal-load and synchronous visual stack footprint.
/// The unmodified ancestor's gap is NOT approval for the larger final resource.
/// No interrupt, alternate-mode, full renderer peak, collector or save authority.
/// </summary>
internal sealed class NativeJapaneseEggAuthoredMemoryReport
{
    private NativeJapaneseEggAuthoredMemoryReport(NativeJapaneseEggAuthoredClassesPlan plan,
        NativeJapaneseEggMemoryReport ancestor, NativeJapaneseEggMemoryReport.Metrics values)
    {
        PlanSha256 = plan.PlanSha256;
        FinalDataSha256 = plan.FinalDataSha256;
        FinalOverlaySha256 = plan.FinalOverlaySha256;
        Ancestor = ancestor;
        Values = values;
    }

    internal string PlanSha256 { get; }
    internal string FinalDataSha256 { get; }
    internal string FinalOverlaySha256 { get; }
    internal NativeJapaneseEggMemoryReport Ancestor { get; }
    internal NativeJapaneseEggMemoryReport.Metrics Values { get; }
    internal bool ModeledLoadExtentsFitProven => Values.ModeledExtentsFit;
    internal bool SynchronousVisualStackFitProven => Values.StackMargin >= 0;
    internal bool FullRuntimePeakProven => false;
    internal bool PublicationApproved => false;

    internal static NativeJapaneseEggAuthoredMemoryReport Capture(
        NativeRegionalSourceSession usa, NativeJapaneseEggAuthoredClassesPlan plan)
    {
        ArgumentNullException.ThrowIfNull(usa);
        ArgumentNullException.ThrowIfNull(plan);
        if (usa.Revision != NativeDiscRevision.UsaScus94228 || !plan.SourceCertified)
            throw new InvalidDataException("Authored memory requires exact-USA source-issued composition.");
        try
        {
            var integration = plan.Integration;
            var ancestor = NativeJapaneseEggMemoryReport.Capture(usa, integration);
            byte[] final = plan.CopyLevelData(), before = integration.CopyLevelData();
            byte[] overlay = plan.CopyOverlay(), oldOverlay = integration.CopyOverlay();
            byte[] exe = plan.CopyExecutable();
            if (!plan.RevertDataExact(final).AsSpan().SequenceEqual(before) ||
                !plan.RevertOverlayExact(overlay).AsSpan().SequenceEqual(oldOverlay) ||
                !exe.AsSpan().SequenceEqual(integration.CopyExecutable()) ||
                !final.AsSpan(0,16).SequenceEqual(before.AsSpan(0,16)) ||
                Word(final, 0x14) != 0x50000 || Word(final, 0x1C) != 0x14800 ||
                plan.Projection.GrowthBytes != 0x1800 || final.Length != before.Length + 0x1800 ||
                integration.FinalCopyBufferAddress != 0x80089238 ||
                !plan.Bindings.Select(b => b.AuthoredDestinationClass).SequenceEqual(new ushort[] {0x1E9,0x1F2}) ||
                plan.Bindings.Any(b => b.ByteLength != 0x808) ||
                plan.OverlayLayout.CopyAnchor != integration.FinalCopyBufferAddress)
                throw new InvalidDataException("Authored memory lost its exact larger final resource ancestry.");
            // All adapter/helper bytes remain exact. The extra class guard
            // comparisons do not allocate a stack frame; executed smoke checks
            // retain the actual dispatch caller SP and both class routes.
            int adapterOffset = checked((int)(integration.VisualAncestor.EntryPc - 0x8007AA38));
            if (Word(overlay, adapterOffset) != 0x27BDFD50 ||
                !overlay.AsSpan(adapterOffset, checked((int)(plan.GuardPc - integration.VisualAncestor.EntryPc)))
                    .SequenceEqual(oldOverlay.AsSpan(adapterOffset, checked((int)(plan.GuardPc - integration.VisualAncestor.EntryPc)))))
                throw new InvalidDataException("Authored visual adapter/helper stack owners changed.");
            var values = NativeJapaneseEggMemoryReport.Measure(integration.FinalCopyBufferAddress,
                checked((uint)integration.RuntimeMemory.UsedLevelDataBytes), Word(final,12),
                Word(final,20),Word(final,28), checked((uint)usa.Entries[8].ByteLength),
                Word(integration.CopyPeteHeader(),0),Word(exe,0x65DA8),Word(exe,0x65DA4),0x2B0);
            if (!values.ModeledExtentsFit ||
                values.SceneEnd != plan.Projection.ProjectedSceneReadEnd ||
                values.NormalGap != plan.Projection.ProjectedNormalGap ||
                values.SceneEnd != ancestor.Values.SceneEnd + 0x1800 ||
                values.NormalGap != ancestor.Values.NormalGap - 0x1800 ||
                values.VisualTickStackLow != ancestor.Values.VisualTickStackLow ||
                values.StackMargin != ancestor.Values.StackMargin)
                throw new InvalidDataException("Final authored load/stack endpoints disagree or exceed their bounds.");
            return new(plan, ancestor, values);
        }
        finally { usa.VerifyUnchanged(); }
    }

    private static uint Word(byte[] bytes,int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset));
}
