using System.Buffers.Binary;

namespace Spyro.Editor.Core.Universal;

internal enum UniversalFixKind { None, Branch, BranchLabel, Jump, JumpLabel, JalLabel, AddrHi, AddrLo, TableHi, TableLo, LabelAddrHi, LabelAddrLo }

internal readonly record struct UniversalFix(UniversalFixKind Kind, uint Orig = 0, string? Label = null);

internal sealed record UniversalAsmItem(uint? Word, UniversalFix Fix, uint? Orig, string? Label, uint? SourcePc = null, bool ClassReadOwned = false);

internal sealed class UniversalAsmPart
{
    public List<UniversalAsmItem> Items { get; } = [];
    public Dictionary<string, IReadOnlyList<uint>> Tables { get; } = [];
    public bool LenientTables { get; set; }
    public void Emit(uint w, UniversalFix fix = default, uint? orig = null, uint? sourcePc = null) => Items.Add(new(w, fix, orig, null, sourcePc));
    public void Label(string name) => Items.Add(new(null, default, null, name));
}

/// <summary>Per-source-level behavior extracted for a destination.</summary>
public sealed record UniversalBehaviorSourceReport(string SourceLevelKey, IReadOnlyList<int> Classes,
    int UpdateBytes, int InitBytes, int JumpTables)
{
    public IReadOnlyList<uint> ParentClassReadSites { get; init; } = [];
    public UniversalMagicPairShadowReadback? PrivateMagicPairShadow { get; init; }
    public UniversalMagicDruidChainReadback? PrivateMagicDruidChain {get;init;}
    public uint PlayerDamageMask { get; init; }
    public IReadOnlyList<UniversalPlayerDamageWrite> PlayerDamageWrites { get; init; } = [];
    public IReadOnlyList<UniversalNativePlayerDamageCall> NativePlayerDamageCalls { get; init; } = [];
    public IReadOnlyList<uint> AllocatedClassReadSites { get; init; } = [];
    public IReadOnlyList<uint> LinkedClassReadSites { get; init; } = [];
    public IReadOnlyList<uint> TransitionClassStoreSites { get; init; } = [];
    public IReadOnlyList<uint> GlobalSoundReadSites { get; init; } = [];
    public IReadOnlyList<uint> GemCollectionCallSites { get; init; } = [];
    public int GemCollectionAdapterBytes { get; init; }
}

public sealed record UniversalBehaviorBundle(uint Base, byte[] Code, uint UpdateWrapper, uint InitWrapper,
    IReadOnlyList<UniversalBehaviorSourceReport> Sources)
{
    public IReadOnlyList<UniversalDestinationGemCall> DestinationGemCollectionCalls { get; init; } = [];
    public int DestinationGemCollectionExtraBytes { get; init; }
    public IReadOnlyList<UniversalDestinationGemColourLoad> DestinationGemColourLoads { get; init; } = [];
    public int DestinationGemColourExtraBytes { get; init; }
    public IReadOnlyList<UniversalNativeParticleNoOpProof> NativeParticleNoOpProofs { get; init; } = [];
    public uint? OwnedParticleUpdateWrapper {get;init;}
    public UniversalCollectionStarContinuation? CollectionStarContinuation {get;init;}
}

/// <summary>
/// Builds the relocatable behavior bundle appended to a destination overlay: a pruned copy of each
/// source level's update function (class switch replaced by a chain for the imported classes), a
/// copy of each source init function, and wrappers that chain onto the destination's own callbacks.
/// </summary>
public static class UniversalBehaviorBundleBuilder
{
    // Engine routines called at the top of every update function: build the active list and advance
    // animations. A chained source update must not run them a second time in the same frame.
    private static readonly HashSet<uint> PerFrameOnceRoutines = [0x80051FEC, 0x800522C0];

    public static UniversalBehaviorBundle Build(UniversalOverlay destination,
        IReadOnlyList<(string SourceKey, UniversalOverlay Source, IReadOnlyList<int> Classes)> sources, uint baseAddress,
        IReadOnlyList<int>? dragonflyEggClasses = null, IReadOnlyList<int>? signClasses = null,
        IReadOnlyDictionary<string, IReadOnlyDictionary<int, int>>? classMaps = null, bool remapSaveRows = false, byte[]? nativeExecutable = null,
        UniversalOverlay? nativeDestinationOwner = null, IReadOnlyList<int>? highValueGemClasses = null,
        IReadOnlyDictionary<string, UniversalWhirlwindSoundBinding>? whirlwindSounds = null,
        IReadOnlyDictionary<string, UniversalDryCanyonBirdCertificate>? dryCanyonBirdBranches = null,
        IReadOnlyDictionary<string,UniversalFlightChestCertificate>? flightChests = null, IReadOnlyDictionary<string,UniversalFlightGateCertificate>? flightGates = null,IReadOnlyList<UniversalOwnedParticleCertificate>? ownedParticles = null, UniversalMagicPairCertificate? magicPair = null,IReadOnlyDictionary<string,UniversalDarkPassageLanternContext>? darkLanternContexts = null,IReadOnlyDictionary<string,UniversalDarkPassageProjectileParticleCertificate>? darkProjectileParticles = null, UniversalMagicDruidChainCertificate? magicChain = null,IReadOnlyDictionary<string,UniversalHighCavesSpellParticleCertificate>? highCavesSpellParticles = null)
    {
        if (baseAddress % 4 != 0) throw new ArgumentException("Bundle base must be word aligned.");
        int Mapped(string key, int c) => classMaps != null && classMaps.TryGetValue(key, out var map) && map.TryGetValue(c, out int mapped) ? mapped : c;
        bool Remaps(string key) => classMaps != null && classMaps.TryGetValue(key, out var cm) && cm.Any(k => k.Key != k.Value);
        string InitTag(string key, int c) => Remaps(key) ? $"i_{key}_{c:X3}" : "i_" + key;
        bool hasHighValueGems = highValueGemClasses is { Count: > 0 };
        List<UniversalAsmPart> parts = [UniversalSpecialBehaviors.SignHookPreamble(signClasses is { Count: > 0 })];
        var destinationGems = (classMaps?.Values.SelectMany(m => UniversalGemCollectionAdapter.GemMappings(m)) ?? [])
            .GroupBy(p => p.Value).Select(g => g.Select(p => p.Key).Distinct().Count() == 1 ? g.First() :
                throw new InvalidDataException("Destination gem collection has an ambiguous canonical class owner."))
            .OrderBy(p => p.Value).ToArray();
        UniversalOverlay destinationOwner = nativeDestinationOwner ?? destination;
        IReadOnlyList<UniversalDestinationGemCollectionAdapter.OwnedCall> destinationGemCalls = [];
        if (destinationGems.Length != 0 && Enumerable.Range(0, (int)(destinationOwner.FunctionEnd(destinationOwner.Update) - destinationOwner.Update) / 4)
            .Any(n => destinationOwner.Word(destinationOwner.Update + (uint)n * 4) == UniversalMips.EncodeJ(3, UniversalGemCollectionAdapter.CollectItem)))
        {
            UniversalGemCollectionAdapter.RequireNativeOwner(nativeExecutable);
            destinationGemCalls = UniversalDestinationGemCollectionAdapter.Analyze(destinationOwner);
            UniversalDestinationGemCollectionAdapter.RequireCurrentOwner(destination, destinationOwner, destinationGemCalls);
        }
        IReadOnlyList<UniversalDestinationGemColourAdapter.OwnedLoad> destinationColourLoads = [];
        if (destinationGems.Length != 0)
        {
            destinationColourLoads = UniversalDestinationGemColourAdapter.Analyze(destinationOwner);
            if (destinationColourLoads.Count != 0)
            {
                UniversalGemCollectionAdapter.RequireNativeOwner(nativeExecutable);
                UniversalDestinationGemCollectionAdapter.RequireCurrentOwner(destination, destinationOwner, destinationGemCalls);
            }
        }
        string? destinationGemLabel = null;
        UniversalAsmPart w = new();
        w.Label("wrap_upd");
        w.Emit(UniversalMips.EncodeI(9, 29, 29, -0x18));
        w.Emit(UniversalMips.EncodeI(43, 29, 31, 0x10));
        w.Emit(UniversalMips.EncodeJ(3, destination.Update));
        w.Emit(UniversalMips.Nop);
        foreach (var s in sources)
        {
            w.Emit(UniversalMips.EncodeJ(3, 0), new(UniversalFixKind.JalLabel, Label: "u_" + s.SourceKey));
            w.Emit(UniversalMips.Nop);
        }
        if (dragonflyEggClasses is { Count: > 0 })
        {
            w.Emit(UniversalMips.EncodeJ(3, 0), new(UniversalFixKind.JalLabel, Label: "egg_upd"));
            w.Emit(UniversalMips.Nop);
        }
        if (hasHighValueGems)
        {
            w.Emit(UniversalMips.EncodeJ(3, 0), new(UniversalFixKind.JalLabel, Label: "high_gem_upd"));
            w.Emit(UniversalMips.Nop);
        }
        w.Emit(UniversalMips.EncodeI(35, 29, 31, 0x10));
        w.Emit(UniversalMips.Nop); // load delay before jr ra
        w.Emit(UniversalMips.JrRa);
        w.Emit(UniversalMips.EncodeI(9, 29, 29, 0x18));
        w.Label("wrap_init");
        foreach (var s in sources)
            foreach (int c in s.Classes)
            {
                w.Emit(UniversalMips.EncodeI(9, 0, 1, Mapped(s.SourceKey, c)));
                w.Emit(UniversalMips.EncodeI(4, 4, 1, 0), new(UniversalFixKind.BranchLabel, Label: Remaps(s.SourceKey) ? $"it_{s.SourceKey}_{c:X3}" : InitTag(s.SourceKey, c)));
                w.Emit(UniversalMips.Nop);
            }
        w.Emit(UniversalMips.EncodeJ(2, destination.Init));
        w.Emit(UniversalMips.Nop);
        foreach (var s in sources.Where(s => Remaps(s.SourceKey)))
            foreach (int c in s.Classes)
            {
                w.Label($"it_{s.SourceKey}_{c:X3}");
                w.Emit(UniversalMips.EncodeJ(2, 0), new(UniversalFixKind.JumpLabel, Label: InitTag(s.SourceKey, c)));
                w.Emit(UniversalMips.EncodeI(9, 0, 4, c));
            }
        parts.Add(w);

        foreach(var source in sources.Where(s=>s.SourceKey=="hauntedtowers" && s.Classes.Contains(0xCC)))
            if(!UniversalHauntedWizardParticles.HasNativeOwner(destination) &&
                !(ownedParticles?.Any(c=>c.SourceLevelKey==source.SourceKey && c.TypeIds.SequenceEqual(new[]{29}))??false))
                throw new InvalidDataException("This wizard needs its native resurrection effect before it can be placed in this level.");
        foreach(var source in sources)UniversalDarkPassageProjectileParticles.RequireBundle(source.SourceKey,source.Source,source.Classes,destination,nativeExecutable??[],darkProjectileParticles,ownedParticles);
        foreach(var source in sources)UniversalHighCavesSpellParticles.RequireBundle(source.SourceKey,source.Source,source.Classes,destination,nativeExecutable??[],highCavesSpellParticles,ownedParticles);
        Dictionary<string,UniversalOwnedParticleParts> ownedParticleParts=new(StringComparer.OrdinalIgnoreCase);
        if(ownedParticles is {Count:>0})
        {
            var native=ownedParticles[0].NativeUpdate;
            foreach(var certificate in ownedParticles)
            {
                certificate.RequireDestination(destination);
                if(certificate.NativeUpdate!=native || !sources.Any(s=>string.Equals(s.SourceKey,certificate.SourceLevelKey,StringComparison.OrdinalIgnoreCase) && s.Source.Bytes.SequenceEqual(certificate.Profile.Source.Bytes) && s.Source.Bindings.Count==certificate.Profile.Source.Bindings.Count && s.Source.Bindings.All(b=>certificate.Profile.Source.Bindings.TryGetValue(b.Key,out var captured) && b.Value==captured)))
                    throw new InvalidDataException("Owned particle dispatcher has no exact copied source or common destination manager.");
                var body=UniversalOwnedParticleDispatch.Build(certificate.Profile,"particle_"+certificate.SourceLevelKey);
                ownedParticleParts.Add(certificate.SourceLevelKey,body);parts.Add(body.Spawn);parts.Add(body.Update);
            }
            parts.Add(UniversalOwnedParticleDispatch.ChainUpdates(ownedParticleParts.Values.Select(p=>p.UpdateLabel).ToArray(),native,"wrap_owned_particles"));
        }
        List<UniversalBehaviorSourceReport> reports = [];
        List<UniversalNativeParticleNoOpProof> particleNoOpProofs = [];
        foreach (var s in sources)
        {
            UniversalFlightGateCertificate? flightGate = null;
            if(flightGates!=null)flightGates.TryGetValue(s.SourceKey,out flightGate);
            UniversalFlightChestCertificate? flightChest = null;
            if(flightChests!=null)flightChests.TryGetValue(s.SourceKey,out flightChest);
            if(flightChest!=null)flightChest.RequireBundle(s.Source,destination,s.Classes,nativeExecutable);
            else if(flightGate!=null)flightGate.RequireBundle(s.Source,destination,s.Classes,nativeExecutable);
            else UniversalFlightCopiedRootOwnership.RequireCopiedSourceDependencyOwnership(s.SourceKey, s.Source, s.Classes);
            var map = s.Classes.ToDictionary(c => c, c => Mapped(s.SourceKey, c));
            if (classMaps != null && classMaps.TryGetValue(s.SourceKey, out var fullMap)) map = fullMap.ToDictionary();
            UniversalDryCanyonBirdCertificate? birdProof = null;
            if (s.SourceKey == "drycanyon" && s.Classes.Contains(0xB8) &&
                (dryCanyonBirdBranches == null || !dryCanyonBirdBranches.TryGetValue(s.SourceKey, out birdProof)))
                throw new InvalidDataException("Copied Dry Canyon B8 behavior requires its issued state/frame branch proof.");
            var (upd, updBytes, jt1) = BuildPrunedUpdate(s.Source, s.Classes, "u_" + s.SourceKey, map, birdProof, s.SourceKey=="magiccrafters"?magicPair:null,s.SourceKey=="magiccrafters"?magicChain:null);
            UniversalHighCavesTornadoPlacement.RequireCopied(upd,s.SourceKey,s.Source,s.Classes,map,nativeExecutable??[]);
            if(s.SourceKey=="highcaves"&&s.Classes.Contains(0x127))UniversalHighCavesSpellParticles.RequireCopiedCalls(upd,s.Source);
            if(s.SourceKey=="twilightharbor" || UniversalTwilightTrooperOwnership.SourceFamily(s.Source)&&s.Classes.Any(c=>c is 0x93 or 0x94))
            {UniversalTwilightTrooperPlacement.RequireSourceLabel(s.SourceKey);UniversalTwilightTrooperOwnership.RequireCopiedInputs(upd,s.Source,s.Classes,map,nativeExecutable??[]);UniversalTwilightTrooperParticles.RequireBundle(s.Source,destination,s.Classes,ownedParticles,nativeExecutable??[]);}
            IReadOnlyList<uint> twilightClassSites=[];
            if(string.Equals(s.SourceKey,"darkpassage",StringComparison.OrdinalIgnoreCase)&&s.Classes.Contains(0x7A)&&darkLanternContexts!=null&&darkLanternContexts.TryGetValue(s.SourceKey,out var lanternDestinationContext))lanternDestinationContext.RequireDestination(destination,nativeExecutable??[]);
            UniversalDarkPassageLanternGroups.RequireCopied(upd,s.SourceKey,s.Source,s.Classes,map,darkLanternContexts);
            if(string.Equals(s.SourceKey,"darkpassage",StringComparison.OrdinalIgnoreCase)&&s.Classes.Contains(0x9B)&&darkProjectileParticles?.ContainsKey(s.SourceKey)==true)UniversalDarkPassageProjectileParticles.RequireCopiedCalls(upd,s.Source);
            var darkDogTransitionSites = UniversalDarkPassageShapeshifterOwnership.Patch(upd,s.SourceKey,s.Source,s.Classes,map,nativeExecutable??[]);
            UniversalBeastMistyBoarPlacement.RequireCopiedInputs(upd,s.SourceKey,s.Source,s.Classes,map,nativeExecutable??[]);
            var boarClassSites = map.Any(k=>k.Key!=k.Value)
                ? UniversalBeastMistyBoarClassOwnership.Translate(upd,s.SourceKey,s.Source,s.Classes,map,nativeExecutable??[])
                : (IReadOnlyList<uint>)[];
            if(s.SourceKey=="terracevillage" && s.Classes.Contains(0x20) && destination.Bindings[UniversalExeBindings.ParticleSpawnGlobal].Value==0x800844A0)
            {
                UniversalTerraceLineParticle77.Capture(s.SourceKey,"darkhollow",s.Source,destination,nativeExecutable??[]);
                UniversalTerraceLineParticle77.RequireCopiedCall(upd,s.Source);
            }
            if(string.Equals(s.SourceKey,"hauntedtowers",StringComparison.OrdinalIgnoreCase))
                UniversalHauntedWizardPlacement.RequireCopiedPlacementInputs(upd,s.Source,s.Classes);
            if(ownedParticleParts.TryGetValue(s.SourceKey,out var ownedParticleBody))
                ownedParticles!.Single(c=>string.Equals(c.SourceLevelKey,s.SourceKey,StringComparison.OrdinalIgnoreCase)).Patch(upd,s.Source,ownedParticleBody.SpawnLabel);
            flightChest?.Patch(upd,s.Source,map);
            flightGate?.Patch(upd,s.Source,map);
            if (map.Any(k => k.Key != k.Value))
            {
                if(s.SourceKey=="hauntedtowers")UniversalHauntedWizardClassOwnership.MarkArmorDebrisModelRead(upd,s.Source,s.Classes);
                UniversalNativeModelTailOwnership.MarkOwned(upd, s.Source, nativeExecutable);
                if (s.SourceKey == "gnorccove") UniversalGnorcCurrentModelRead.MarkOwned(upd, s.Source);
                particleNoOpProofs.AddRange(UniversalNativeParticleNoOpOwnership.MarkOwned(upd, s.Source, destination, nativeExecutable ?? []));
                if(s.SourceKey=="twilightharbor")twilightClassSites=UniversalTwilightTrooperOwnership.TranslateLinkedRead(upd,s.Source,s.Classes,map);
                UniversalClassRemapping.PatchOwnedClassReads(upd, s.Source, map, s.Classes);
                UniversalClassRemapping.PatchConstantSpawns(upd, s.Source, map,flightChest,flightGate);
            }
            string gemLabel = "gem_collect_" + s.SourceKey;
            List<uint> gemCalls = [.. UniversalGemCollectionAdapter.PatchCalls(upd, s.Source, map, gemLabel, nativeExecutable)];
            parts.Add(upd);
            List<UniversalPlayerDamageWrite> playerDamageWrites = [.. UniversalPlayerDamageWrites.AnalyzeOverlay(s.Source,
                new SortedSet<uint>(upd.Items.Where(i => i.Word != null && (i.SourcePc ?? i.Orig) != null).Select(i => (i.SourcePc ?? i.Orig)!.Value))).Writes];
            int initTotal = 0, tablesTotal = jt1; List<uint> parentClassSites = [], allocatedClassSites = [];
            Dictionary<int, UniversalAsmPart> constructors = [];
            foreach (int c in Remaps(s.SourceKey) ? s.Classes : s.Classes.Take(1))
            {
                UniversalAsmPart init; int initBytes, jt2;
                string initTag = InitTag(s.SourceKey, c);
                try { (init, initBytes, jt2) = BuildSpecializedInit(s.Source, Remaps(s.SourceKey) ? [c] : s.Classes, initTag); }
                catch (InvalidDataException) when (Mapped(s.SourceKey, c) == c && !map.Any(k => k.Key != k.Value))
                { (init, initBytes, jt2) = BuildWholeFunction(s.Source, s.Source.Init, initTag); }
                if(string.Equals(s.SourceKey,"highcaves",StringComparison.OrdinalIgnoreCase))UniversalHighCavesTornadoPlacement.RequireInitializer(init,s.Source,Remaps(s.SourceKey)?[c]:s.Classes,nativeExecutable??[]);
                if(s.SourceKey=="twilightharbor")UniversalTwilightTrooperOwnership.RequireInitializer(init,s.Source,c,nativeExecutable??[],Remaps(s.SourceKey)?[c]:s.Classes);
                if(string.Equals(s.SourceKey,"darkpassage",StringComparison.OrdinalIgnoreCase)&&s.Classes.Contains(0x7A)&&darkLanternContexts!=null&&darkLanternContexts.TryGetValue(s.SourceKey,out var lanternInitContext))lanternInitContext.RequireCopiedInit(init,s.Source,Remaps(s.SourceKey)?[c]:s.Classes);
                UniversalClassRemapping.PatchInitClassStore(init, s.Source, c, Mapped(s.SourceKey, c));
                if (Remaps(s.SourceKey))
                {
                    allocatedClassSites.AddRange(UniversalClassRemapping.PatchInitAllocatedClassReads(init, s.Source, map, c));
                    parentClassSites.AddRange(UniversalClassRemapping.PatchInitParentClassReads(init, s.Source, map));
                    if(s.SourceKey=="hauntedtowers")parentClassSites.AddRange(UniversalHauntedWizardClassOwnership.PatchProjectileParent(init,s.Source,map,c));
                    UniversalClassRemapping.RequireNoUnownedClassMemory(init, true);
                }
                UniversalClassRemapping.PatchConstantSpawns(init, s.Source, map);
                playerDamageWrites.AddRange(UniversalPlayerDamageWrites.AnalyzeOverlay(s.Source,
                    new SortedSet<uint>(init.Items.Where(i => i.Word != null && (i.SourcePc ?? i.Orig) != null).Select(i => (i.SourcePc ?? i.Orig)!.Value))).Writes);
                gemCalls.AddRange(UniversalGemCollectionAdapter.PatchCalls(init, s.Source, map, gemLabel, nativeExecutable));
                parts.Add(init); initTotal += init.Items.Count(i => i.Word != null) * 4; tablesTotal += jt2;
                constructors[c] = init;
            }
            var linkedClassSites = UniversalNativeLinkedActorOwnership.Patch(upd, s.Source, map, constructors)
                .Concat(Remaps(s.SourceKey) ? UniversalWhirlwindIndexedRows.TranslateOwnedReads(upd, s.Source, map) : []).Concat(boarClassSites).Concat(twilightClassSites).ToArray();
            IReadOnlyList<uint> soundReadSites = [];
            if (s.Classes.Contains(0x12C))
            {
                if (whirlwindSounds == null || !whirlwindSounds.TryGetValue(s.SourceKey, out var soundBinding))
                    throw new InvalidDataException("Imported whirlwind behavior requires its native looping sound dependency.");
                soundReadSites = UniversalWhirlwindSoundDependency.Patch(upd, s.Source, nativeExecutable ?? [], soundBinding);
            }
            if (Remaps(s.SourceKey)) UniversalClassRemapping.RequireNoUnownedClassMemory(upd, false, s.Source);
            var nativeDamage = UniversalNativePlayerDamageCalls.AnalyzeSource(s.SourceKey, s.Source, upd, nativeExecutable);
            if(magicPair!=null&&s.SourceKey=="magiccrafters")parts.Add(UniversalMagicPairCollisionShadow.Apply(upd,s.Source,nativeExecutable??[],magicPair));
            if(magicChain!=null&&s.SourceKey=="magiccrafters")parts.Add(magicChain.Apply(upd,s.Source,nativeExecutable??[]));
            int gemBytes = 0;
            if (gemCalls.Count != 0)
            {
                var adapter = UniversalGemCollectionAdapter.Build(map, gemLabel);
                parts.Add(adapter); gemBytes = adapter.Items.Count(i => i.Word != null) * 4;
                if (UniversalGemCollectionAdapter.GemMappings(map).OrderBy(p => p.Value).SequenceEqual(destinationGems))
                    destinationGemLabel = gemLabel;
            }
            reports.Add(new(s.SourceKey, s.Classes.ToArray(), upd.Items.Count(i => i.Word != null) * 4, initTotal, tablesTotal) { GlobalSoundReadSites = soundReadSites, LinkedClassReadSites = linkedClassSites, TransitionClassStoreSites = darkDogTransitionSites, GemCollectionCallSites = gemCalls.Distinct().Order().ToArray(), GemCollectionAdapterBytes = gemBytes, ParentClassReadSites = parentClassSites.Distinct().Order().ToArray(), AllocatedClassReadSites = allocatedClassSites.Distinct().Order().ToArray(), PlayerDamageMask = playerDamageWrites.Aggregate(nativeDamage.DamageMask, (mask, w) => mask | w.DamageMask), NativePlayerDamageCalls = nativeDamage.Calls, PlayerDamageWrites = playerDamageWrites.Distinct().OrderBy(w => w.StorePc).ToArray() });
        }
        int destinationGemBytes = 0;
        if (destinationGemCalls.Count != 0 && destinationGemLabel == null)
        {
            destinationGemLabel = "gem_collect_destination";
            var adapter = UniversalGemCollectionAdapter.BuildMappings(destinationGems, destinationGemLabel);
            parts.Add(adapter); destinationGemBytes = adapter.Items.Count(i => i.Word != null) * 4;
        }
        int destinationColourBytes = 0;
        foreach (var site in destinationColourLoads)
        {
            var adapter = UniversalDestinationGemColourAdapter.Build(destinationGems, site, $"gem_colour_destination_{site.Pc:X8}");
            parts.Add(adapter); destinationColourBytes += adapter.Items.Count(i => i.Word != null) * 4;
        }
        if (dragonflyEggClasses is { Count: > 0 })
            parts.Add(UniversalSpecialBehaviors.DragonflyEggUpdate(dragonflyEggClasses, "egg_upd", remapSaveRows));
        if (hasHighValueGems)
            parts.Add(UniversalSpecialBehaviors.HighValueGemUpdate(highValueGemClasses!, "high_gem_upd", remapSaveRows));
        if (signClasses is { Count: > 0 })
            parts.Add(UniversalSpecialBehaviors.SignDraw(signClasses, "sign_draw"));
        var (code, labels) = Assemble(parts, baseAddress);
        if(magicChain!=null){var proof=magicChain.RequireCompiled(code,baseAddress,parts);reports=reports.Select(r=>r.SourceLevelKey=="magiccrafters"?r with{PrivateMagicDruidChain=proof}:r).ToList();}
        if(magicPair!=null){magicPair.RequireCompiled(code,baseAddress,parts);var proof=UniversalMagicPairCollisionShadow.RequireWritten(code,labels,baseAddress,magicPair.PreservedCacheBytes);reports=reports.Select(r=>r.SourceLevelKey=="magiccrafters"?r with{PrivateMagicPairShadow=proof}:r).ToList();}
        return new(baseAddress, code, labels["wrap_upd"], labels["wrap_init"], reports)
        {
            DestinationGemCollectionCalls = destinationGemCalls.Select(c => new UniversalDestinationGemCall(c.Pc, c.Delay, c.Role, labels[destinationGemLabel!])).ToArray(),
            DestinationGemCollectionExtraBytes = destinationGemBytes,
            DestinationGemColourLoads = destinationColourLoads.Select(c => new UniversalDestinationGemColourLoad(c.Pc, c.PatchWord, c.Delay, c.AddWord, c.LoadWord, c.LoadPc, c.ClassRegister, c.Role, labels[$"gem_colour_destination_{c.Pc:X8}"])).ToArray(),
            DestinationGemColourExtraBytes = destinationColourBytes,
            NativeParticleNoOpProofs = particleNoOpProofs,
            OwnedParticleUpdateWrapper=ownedParticleParts.Count==0?null:labels["wrap_owned_particles"],
            CollectionStarContinuation=UniversalCollectionStarParticles.CaptureContinuation(destination,baseAddress,code,labels,ownedParticles,parts)
        };
    }

    private static void EmitReplay(UniversalAsmPart a, UniversalOverlay o, IReadOnlyList<UniversalReplayed> seq)
    {
        foreach (var r in seq)
        {
            if (r.IsConstant)
            {
                if (o.Contains(r.ConstantValue) && r.ConstantValue >= o.CodeStart)
                {
                    a.Emit(UniversalMips.EncodeI(15, 0, r.ConstantRegister, 0), new(UniversalFixKind.AddrHi, r.ConstantValue));
                    a.Emit(UniversalMips.EncodeI(9, r.ConstantRegister, r.ConstantRegister, 0), new(UniversalFixKind.AddrLo, r.ConstantValue));
                }
                else
                {
                    a.Emit(UniversalMips.EncodeI(15, 0, r.ConstantRegister, UniversalMips.Hi(r.ConstantValue)));
                    a.Emit(UniversalMips.EncodeI(9, r.ConstantRegister, r.ConstantRegister, UniversalMips.Lo(r.ConstantValue)));
                }
                continue;
            }
            a.Emit(r.Word, sourcePc: r.SourcePc);
            // A replayed load may now be adjacent to its consumer; preserve R3000 load-delay semantics.
            if (UniversalMips.IsLoad(r.Word)) a.Emit(UniversalMips.Nop);
        }
    }

    internal static (UniversalAsmPart Part, int Bytes, int Tables) BuildPrunedUpdate(UniversalOverlay o, IReadOnlyList<int> classes, string tag, IReadOnlyDictionary<int, int>? classMap = null, UniversalDryCanyonBirdCertificate? birdBranches = null, UniversalMagicPairCertificate? magicPair = null, UniversalMagicDruidChainCertificate? magicChain = null)
    {
        if (birdBranches != null && !classes.Contains(0xB8)) throw new InvalidDataException("Dry Canyon bird branch proof has no B8 owner in this copy.");
        var sw = UniversalDispatchAnalysis.FindUpdateSwitch(o);
        List<(int Class, UniversalTrace Trace)> entries = classes
            .Select(c => (c, UniversalDispatchAnalysis.Trace(o, sw.TreeStart, c, sw.Seeds, sw.ClassRegister))).ToList();
        var def = UniversalDispatchAnalysis.Trace(o, sw.TreeStart, UniversalDispatchAnalysis.SentinelClass, sw.Seeds, sw.ClassRegister);
        List<uint> starts = [sw.Function, def.Stop, .. entries.Select(e => e.Trace.Stop)];
        uint fEnd = o.FunctionEnd(sw.Function);
        // Materialized code addresses (jump-table targets) must stay addressable in the copy.
        foreach (var r in entries.SelectMany(e => e.Trace.Sequence).Concat(def.Sequence))
            if (r.IsConstant && r.ConstantValue >= sw.Function && r.ConstantValue < fEnd && r.ConstantValue % 4 == 0) starts.Add(r.ConstantValue);
        var (closure, tables) = UniversalDispatchAnalysis.Closure(o, sw.Function, starts, new HashSet<uint> { sw.TreeStart }, birdBranches, magicPair, magicChain);
        var part = Relayout(o, closure, tables, tag, sw.TreeStart, a =>
        {
            for (int i = 0; i < entries.Count; i++)
            {
                a.Emit(UniversalMips.EncodeI(9, 0, 1, classMap != null && classMap.TryGetValue(entries[i].Class, out int mapped) ? mapped : entries[i].Class));
                a.Emit(UniversalMips.EncodeI(5, sw.ClassRegister, 1, 0), new(UniversalFixKind.BranchLabel, Label: $"{tag}_n{i}"));
                a.Emit(UniversalMips.Nop);
                EmitReplay(a, o, entries[i].Trace.Sequence);
                a.Emit(UniversalMips.EncodeJ(2, 0), new(UniversalFixKind.Jump, entries[i].Trace.Stop));
                a.Emit(UniversalMips.Nop);
                a.Label($"{tag}_n{i}");
            }
            EmitReplay(a, o, def.Sequence);
            a.Emit(UniversalMips.EncodeJ(2, 0), new(UniversalFixKind.Jump, def.Stop));
            a.Emit(UniversalMips.Nop);
        }, nopJalsBefore: sw.TreeStart, decided: birdBranches?.Decisions);
        birdBranches?.RequireEmitted(part);
        return (part, part.Items.Count(i => i.Word != null) * 4, tables.Count);
    }

    internal static (UniversalAsmPart Part, int Bytes, int Tables) BuildSpecializedInit(UniversalOverlay o, IReadOnlyList<int> classes, string tag)
    {
        var spec = UniversalSpecializer.Specialize(o, o.Init, 4, classes);
        var part = Relayout(o, spec.Instructions, spec.Tables, tag, null, null, null, spec.Branches);
        part.LenientTables = true;
        return (part, part.Items.Count(i => i.Word != null) * 4, spec.Tables.Count);
    }

    internal static (UniversalAsmPart Part, int Bytes, int Tables) BuildWholeFunction(UniversalOverlay o, uint f, string tag)
    {
        var (closure, tables) = UniversalDispatchAnalysis.Closure(o, f, [f], new HashSet<uint>());
        var part = Relayout(o, closure, tables, tag, null, null, null);
        return (part, part.Items.Count(i => i.Word != null) * 4, tables.Count);
    }

    private static UniversalAsmPart Relayout(UniversalOverlay o, SortedSet<uint> closure, List<UniversalJumpTable> tables,
        string tag, uint? chainAt, Action<UniversalAsmPart>? emitChain, uint? nopJalsBefore,
        IReadOnlyDictionary<uint, (bool Taken, bool NotTaken)>? decided = null)
    {
        Dictionary<uint, UniversalFix> tableFix = [];
        UniversalAsmPart a = new();
        foreach (var t in tables)
        {
            string name = $"{tag}_jt_{t.JumpRegisterPc:X8}";
            tableFix[t.LuiPc] = new(UniversalFixKind.TableHi, Label: name);
            tableFix[t.LoadPc] = new(UniversalFixKind.TableLo, Label: name);
            a.Tables[name] = t.Targets;
        }
        foreach (var (lui, use, target) in UniversalDispatchAnalysis.OverlayReferences(o, closure))
            if (!tableFix.ContainsKey(lui))
                throw new InvalidDataException($"{tag}: overlay-local data reference 0x{target:X8} at 0x{use:X8} is not relocatable yet.");
        a.Label(tag);
        IEnumerable<uint> keys = chainAt is uint c ? closure.Append(c).Order() : closure;
        foreach (uint addr in keys)
        {
            if (chainAt == addr) { a.Label(tag + "_chain"); emitChain!(a); continue; }
            uint w = o.Word(addr);
            if (nopJalsBefore is uint limit && addr < limit && UniversalMips.IsJal(w) &&
                PerFrameOnceRoutines.Contains(UniversalMips.BranchTarget(w, addr)))
            { a.Emit(UniversalMips.Nop, orig: addr); continue; }
            UniversalFix fix = default;
            if (decided != null && UniversalMips.IsPcRelative(w) && !UniversalMips.IsUnconditionalBranch(w) &&
                (UniversalMips.Op(w) != 1 || UniversalMips.Rt(w) is 0 or 1) && decided.TryGetValue(addr, out var outcome))
            {
                // Branch decided by the specialized class value: keep only the reachable side.
                if (outcome.Taken && !outcome.NotTaken) { a.Emit(UniversalMips.EncodeI(4, 0, 0, 0), new(UniversalFixKind.Branch, UniversalMips.BranchTarget(w, addr)), addr); continue; }
                if (!outcome.Taken && outcome.NotTaken) { a.Emit(UniversalMips.Nop, orig: addr); continue; }
            }
            if (tableFix.TryGetValue(addr, out var tf)) fix = tf;
            else if (UniversalMips.IsPcRelative(w)) fix = new(UniversalFixKind.Branch, UniversalMips.BranchTarget(w, addr));
            else if (UniversalMips.Op(w) == 2) fix = new(UniversalFixKind.Jump, UniversalMips.BranchTarget(w, addr));
            else if (UniversalMips.IsJal(w) && o.Contains(UniversalMips.BranchTarget(w, addr)))
                throw new InvalidDataException($"{tag}: call into an overlay helper at 0x{addr:X8} is not relocatable yet.");
            a.Emit(w, fix, addr);
        }
        return a;
    }

    internal static (byte[] Code, Dictionary<string, uint> Labels) Assemble(IReadOnlyList<UniversalAsmPart> parts, uint baseAddress)
    {
        Dictionary<string, uint> labels = [];
        Dictionary<UniversalAsmPart, Dictionary<uint, uint>> orig = [];
        List<(UniversalAsmPart Part, uint Word, UniversalFix Fix, uint At)> seq = [];
        uint pc = baseAddress;
        foreach (var p in parts)
        {
            orig[p] = [];
            foreach (var it in p.Items)
            {
                if (it.Word == null) { labels[it.Label!] = pc; continue; }
                if (it.Orig is uint o) orig[p][o] = pc;
                seq.Add((p, it.Word.Value, it.Fix, pc)); pc += 4;
            }
        }
        Dictionary<string, uint> tableAddr = [];
        List<(UniversalAsmPart Part, IReadOnlyList<uint> Targets)> tableData = [];
        foreach (var p in parts)
            foreach (var (name, targets) in p.Tables) { tableAddr[name] = pc; tableData.Add((p, targets)); pc += (uint)(4 * targets.Count); }

        byte[] code = new byte[pc - baseAddress];
        int offset = 0;
        foreach (var (p, word, fix, at) in seq)
        {
            uint w = word;
            uint Orig(uint o) => orig[p].TryGetValue(o, out uint n) ? n
                : throw new InvalidDataException($"Relocated target 0x{o:X8} is outside the extracted behavior closure.");
            switch (fix.Kind)
            {
                case UniversalFixKind.Branch: w = UniversalMips.WithBranchTarget(w, at, Orig(fix.Orig)); break;
                case UniversalFixKind.BranchLabel: w = UniversalMips.WithBranchTarget(w, at, labels[fix.Label!]); break;
                case UniversalFixKind.Jump: w = UniversalMips.WithJumpTarget(w, Orig(fix.Orig)); break;
                case UniversalFixKind.JumpLabel: w = UniversalMips.WithJumpTarget(w, labels[fix.Label!]); break;
                case UniversalFixKind.JalLabel: w = UniversalMips.EncodeJ(3, labels[fix.Label!]); break;
                case UniversalFixKind.AddrHi: w = UniversalMips.WithImm(w, UniversalMips.Hi(Orig(fix.Orig))); break;
                case UniversalFixKind.AddrLo: w = UniversalMips.WithImm(w, UniversalMips.Lo(Orig(fix.Orig))); break;
                case UniversalFixKind.LabelAddrHi: w = UniversalMips.WithImm(w, UniversalMips.Hi(labels[fix.Label!])); break;
                case UniversalFixKind.LabelAddrLo: w = UniversalMips.WithImm(w, UniversalMips.Lo(labels[fix.Label!])); break;
                case UniversalFixKind.TableHi: w = UniversalMips.WithImm(w, UniversalMips.Hi(tableAddr[fix.Label!])); break;
                case UniversalFixKind.TableLo: w = UniversalMips.WithImm(w, UniversalMips.Lo(tableAddr[fix.Label!])); break;
            }
            BinaryPrimitives.WriteUInt32LittleEndian(code.AsSpan(offset), w); offset += 4;
        }
        foreach (var (p, targets) in tableData)
            foreach (uint t in targets)
            {
                uint n = orig[p].TryGetValue(t, out uint m) ? m
                    : p.LenientTables ? orig[p].Values.Min() // unreachable for the specialized classes
                    : throw new InvalidDataException($"Jump table target 0x{t:X8} is outside the extracted behavior closure.");
                BinaryPrimitives.WriteUInt32LittleEndian(code.AsSpan(offset), n); offset += 4;
            }
        return (code, labels);
    }
}
