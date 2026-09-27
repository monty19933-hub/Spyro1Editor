using System.Buffers.Binary;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;
using F = AttemptFixtures;

internal static class StartupCompositionChecks
{
    internal static void SourceFree(Action<bool, string> check)
    {
        // There is intentionally no fixture issuer or arbitrary byte factory.
        check(typeof(NativeJapaneseEggLifecycleStartupPlan).GetConstructors().Length == 0,
            "Startup composition has no public constructor or fixture promotion path.");
        Refuse(() => NativeJapaneseEggLifecycleStartupPlan.Create(null!, null!, null!, null!), check, "Missing source ancestors");
    }

    internal static void Verify(NativeRegionalSourceSession usa, NativeJapaneseEggAuthoredClassesPlan authored,
        NativeJapaneseEggLifecycleOverlayPlan overlay, DetachedCollectibleManifest manifest, Action<bool, string> check)
    {
        uint W(byte[] bytes, int at) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at));
        int slot = overlay.Collector.OrdinarySlot;
        NativeCollectibleColdBootBootstrap Bootstrap(DetachedCollectibleManifest m, int s) =>
            NativeCollectibleColdBootBootstrap.Create(NativeCollectibleReservationMechanics.Create(NativeCollectibleSelectedSlotResidence.Capture(usa, m, s)));
        var bootstrap = Bootstrap(manifest, slot);
        byte[] original = authored.Integration.CopyOriginalExecutable(), lifecycle = overlay.CopyExecutable(),
            startup = bootstrap.CopyExecutable(), previousOverlay = overlay.CopyOverlay(), previousData = overlay.CopyLevelData();
        byte[] manifestBytes = manifest.SerializeCanonical();
        var plan = NativeJapaneseEggLifecycleStartupPlan.Create(authored, overlay, bootstrap, manifest);
        byte[] final = plan.CopyExecutable();
        check(plan.SourceCertified && ReferenceEquals(plan.OverlayAncestor, overlay) && ReferenceEquals(plan.BootstrapAncestor, bootstrap) &&
            plan.OrdinarySlot == slot && plan.ManifestSha256 == manifest.Sha256,
            "Startup composition is bound to exact source, lifecycle, bootstrap, manifest and ordinary slot ancestors.");
        check(!plan.NativeResidenceIssued && !plan.DispatchActivated && !plan.ReadyIssued && !plan.PersistentSaveChanges &&
            !plan.BiosLoadProven && !plan.InstructionCacheProven && !plan.InterruptSafetyProven && !plan.LifetimeProven &&
            !plan.FullRuntimePeakProven && !plan.CardIoApproved && !plan.PublicationApproved,
            "Detached startup composition does not promote loader, native residence, Ready, lifetime, card or publication authority.");
        Refuse(plan.RequireNativeInstallation, check, "Detached startup has no installation authority");
        check(final.Length == original.Length && plan.CopyOverlay().SequenceEqual(previousOverlay) && plan.CopyLevelData().SequenceEqual(previousData),
            "Startup composition preserves the entire lifecycle overlay and disk-form data.");
        bool StartupByte(int i) => i is >= 0x10 and < 0x14 or >= 0x65DA4 and < 0x65DA8 or >= 0x65E40 and < 0x66000;
        bool CopyByte(int i) => i is >= 0x4AF18 and < 0x4AF20;
        check(Enumerable.Range(0, final.Length).All(i => StartupByte(i) ? final[i] == startup[i] : final[i] == lifecycle[i]),
            "Every executable byte is from its exact startup owner or the complete lifecycle complement.");
        check(Enumerable.Range(0, final.Length).Where(i => !StartupByte(i) && !CopyByte(i)).All(i => final[i] == original[i]),
            "The common original complement is unchanged outside startup owners and the eight-byte final copy anchor.");
        check(W(final, 0x10) == bootstrap.EntryPc && W(final, 0x65DA4) == 0xA00 && W(final, 0x65DA8) == 0x200000 &&
            final.AsSpan(0x65E40, 0x1C0).SequenceEqual(startup.AsSpan(0x65E40, 0x1C0)) &&
            final.AsSpan(0x4AF18, 8).SequenceEqual(lifecycle.AsSpan(0x4AF18, 8)),
            "Final entry, A00 reserve, complete ephemeral tail and grown-overlay copy anchor retain their distinct owners.");
        check(plan.StartupOwners.Select(o => (o.Offset, o.ByteLength)).SequenceEqual(new (long, long)[] { (0x10, 4), (0x65DA4, 4), (0x65E40, 0x1C0) }) &&
            plan.CopyAnchorOwner.Offset == 0x4AF18 && plan.CopyAnchorOwner.ByteLength == 8 &&
            plan.StartupOwners.All(o => o.EndExclusive <= plan.CopyAnchorOwner.Offset || o.Offset >= plan.CopyAnchorOwner.EndExclusive),
            "Typed patch owners exactly cover entry4, reserve4, ephemeral-tail1C0 and disjoint anchor8.");
        check(plan.RevertToLifecycleExecutableExact(final).SequenceEqual(lifecycle) && plan.RevertToOriginalExecutableExact(final).SequenceEqual(original),
            "Both complete inverses recover the lifecycle executable and exact common original.");
        foreach (int at in new[] { 0, 0x10, 0x13, 0x14, 0x4AF18, 0x4AF1F, 0x4AF20, 0x65DA4, 0x65DA7, 0x65DA8, 0x65E3F, 0x65E40, 0x65E6C, 0x65FFF })
        {
            byte[] altered = final.ToArray(); altered[at] ^= 1;
            Refuse(() => plan.RevertToLifecycleExecutableExact(altered), check, "Changed startup/lifecycle/complement afterimage");
            Refuse(() => plan.RevertToOriginalExecutableExact(altered), check, "Changed complete original inverse input");
        }
        foreach (byte[] altered in new[] { final[..^1], final.Concat(new byte[] { 0 }).ToArray(), lifecycle, startup, original })
        {
            Refuse(() => plan.RevertToLifecycleExecutableExact(altered), check, "Unpaired or resized inverse input");
            Refuse(() => plan.RevertToOriginalExecutableExact(altered), check, "Unpaired or resized original inverse input");
        }
        foreach (var (copy, again) in new (byte[], Func<byte[]>)[] {
            (plan.CopyExecutable(), plan.CopyExecutable), (plan.CopyOverlay(), plan.CopyOverlay), (plan.CopyLevelData(), plan.CopyLevelData),
            (plan.RevertToLifecycleExecutableExact(final), () => plan.RevertToLifecycleExecutableExact(final)),
            (plan.RevertToOriginalExecutableExact(final), () => plan.RevertToOriginalExecutableExact(final)) })
        { byte before = copy[0]; copy[0] ^= 1; check(again()[0] == before, "Every returned executable/data/overlay/inverse array is a defensive copy."); }

        var metrics = plan.FinalMemory;
        const uint normalFloor = 0x801FF600 - 0x39C48 - 0x6008 - 2 * 0x1C000;
        uint sceneEnd = checked(overlay.Projection.CopyAnchor + (uint)authored.Integration.RuntimeMemory.UsedLevelDataBytes + W(previousData, 0x14) + W(previousData, 0x1C));
        uint peak = Math.Max(sceneEnd, Math.Max(overlay.Projection.CopyAnchor + W(previousData, 0xC), overlay.Projection.CopyAnchor + 0x80000));
        uint dispatch = 0x801FFFF8 - 0x20 - 0x20 - 0x1C0, worstLow = dispatch - 0x2B0 - 0x60;
        check(metrics.SceneEnd == sceneEnd && sceneEnd == overlay.Projection.SceneEnd && metrics.NormalReservedStart == normalFloor &&
            metrics.NormalGap == (long)normalFloor - sceneEnd && metrics.NormalGap == overlay.Projection.NormalGap - 0x200 &&
            plan.NormalFloorReduction == 0x200 && plan.OriginalReserveMemory.NormalReservedStart == normalFloor + 0x200 &&
            plan.OriginalReserveMemory.NormalGap == metrics.NormalGap + 0x200,
            "Independent final-anchor arithmetic charges the exact 0x200-byte (512-byte) reduction of normal floor and gap.");
        check(metrics.TransferPeakEnd == peak && metrics.PeteTemporaryStart == 0x801FF600 - 2 * 0x3A000 &&
            metrics.PeteResidentStart == 0x801FF600 - 0x39C48 && metrics.ModeledExtentsFit,
            "Normal transfer/PETE phase geometry is recomputed with the complete final data and A00 reserve.");
        check(plan.AdapterStackByteLength == 0x2B0 && plan.MaximumNestedStackBytes == 0x60 && metrics.DispatchStackPointer == dispatch &&
            metrics.VisualTickStackLow == dispatch - 0x2B0 - 0x28 && plan.WorstSynchronousStackLow == worstLow &&
            plan.WorstSynchronousStackMargin == (long)worstLow - 0x801FF600 && plan.LedgerStackMargin == (long)worstLow - 0x801FF800 &&
            plan.WorstSynchronousStackLow == metrics.VisualTickStackLow - 0x38 && plan.LedgerStackMargin >= 0,
            "Lifecycle nested60 and visual28 are sequential maxima below the actual unchanged2B0 adapter, with explicit ledger clearance.");
        check(plan.FinalExecutableSha256.Length == 64 && plan.PlanSha256.Length == 64 &&
            NativeJapaneseEggLifecycleStartupPlan.Create(authored, overlay, bootstrap, manifest).PlanSha256 == plan.PlanSha256,
            "Composition hashes bind deterministic complete bytes and source/manifest/slot geometry ancestry.");

        var next = manifest.Advance([F.Id(258)], []);
        Refuse(() => NativeJapaneseEggLifecycleStartupPlan.Create(authored, overlay, bootstrap, next), check, "Unpaired supplied manifest");
        var unpairedManifest = Bootstrap(next, slot);
        Refuse(() => NativeJapaneseEggLifecycleStartupPlan.Create(authored, overlay, unpairedManifest, manifest), check, "Bootstrap owns another manifest");
        var unpairedSlot = Bootstrap(manifest, (slot + 1) % DetachedCollectibleSave.OrdinarySlotCount);
        Refuse(() => NativeJapaneseEggLifecycleStartupPlan.Create(authored, overlay, unpairedSlot, manifest), check, "Bootstrap owns another ordinary slot");
        var fixture = F.Authored();
        NativeJapaneseEggCollectibleIdentity[] ids = overlay.Binding.Rows.Select(r => new NativeJapaneseEggCollectibleIdentity(r.RowId, r.InstanceId)).ToArray();
        var fixtureOverlay = NativeJapaneseEggLifecycleOverlayPlan.Fixture(fixture, manifest, slot, ids, F.Predicate());
        Refuse(() => NativeJapaneseEggLifecycleStartupPlan.Create(fixture, fixtureOverlay, bootstrap, manifest), check, "Fixture cannot become source-certified startup composition");
        Refuse(() => NativeJapaneseEggLifecycleStartupPlan.Create(authored, fixtureOverlay, bootstrap, manifest), check, "Mixed fixture/source ancestry");
        check(overlay.CopyExecutable().SequenceEqual(lifecycle) && overlay.CopyOverlay().SequenceEqual(previousOverlay) &&
            overlay.CopyLevelData().SequenceEqual(previousData) && bootstrap.CopyExecutable().SequenceEqual(startup) &&
            authored.Integration.CopyOriginalExecutable().SequenceEqual(original) && manifest.SerializeCanonical().SequenceEqual(manifestBytes) &&
            usa.ReadExecutableRange(0, usa.ExecutableByteLength).SequenceEqual(original), "Source and every supplied ancestor remain unchanged.");
        usa.VerifyUnchanged();
        StartupBootChecks.Verify(plan, check);
        Console.WriteLine($"JP_EGG_LIFECYCLE_STARTUP_COMPOSITION_SOURCE_OK anchor={overlay.Projection.CopyAnchor:X8} sceneEnd={sceneEnd:X8} normalFloor={normalFloor:X8} normalGap={metrics.NormalGap:X} stackLow={worstLow:X8} ledgerMargin={plan.LedgerStackMargin:X} nativeResidence=false ready=false publication=false");
    }

    private static void Refuse(Action action, Action<bool, string> check, string why)
    {
        try { action(); }
        catch (InvalidDataException) { check(true, why); return; }
        catch (ArgumentException) { check(true, why); return; }
        throw new Exception("Expected refusal: " + why);
    }
}
