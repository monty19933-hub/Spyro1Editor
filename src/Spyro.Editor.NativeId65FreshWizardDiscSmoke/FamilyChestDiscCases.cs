using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;

/// <summary>
/// Typed payloads retain their own evidence boundaries. Physical expectations
/// below are assembled independently from those exact inputs and fixed ISO
/// geometry; the printed whole-image hash is observational, not a new payload
/// golden. No candidate image or output file is created.
/// </summary>
internal static class FamilyChestDiscCases
{
    private const int SectorBytes=2352,SectorCount=281270,End=55169;
    private const long ImageBytes=661547040;
    private const string Original="fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37";
    internal static void Free(Action<bool,string> check)
    {
        var type=typeof(NativeFreshLockedChestResearchDiscPlan);
        check(type.IsSealed&&!type.IsPublic&&type.GetConstructors(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)
            .All(c=>c.IsPrivate)&&type.GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly).Length==0,
            "Chest physical issuer is internal/sealed/private with no public writer or byte-substitution factory.");
        var capture=type.GetMethods(BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.DeclaredOnly).Single(m=>m.Name=="CaptureAsync");
        check(capture.IsAssembly&&capture.GetParameters().Select(p=>p.ParameterType).SequenceEqual(new[]{
            typeof(string),typeof(NativeFreshLockedChestExperimentalExecutable),typeof(CancellationToken)}),
            "Only source path and actual experimental executable enter the new detached physical issuer.");
        using var cancel=new CancellationTokenSource();cancel.Cancel();
        Reject<OperationCanceledException>(()=>NativeFreshLockedChestResearchDiscPlan.CaptureAsync("\0",null!,cancel.Token).GetAwaiter().GetResult(),check);
        Reject<ArgumentNullException>(()=>NativeFreshLockedChestResearchDiscPlan.CaptureAsync("\0",null!).GetAwaiter().GetResult(),check);
        using var unread=new Unreadable();
        Reject<OperationCanceledException>(()=>NativeId65FreshDiscStreaming.Measure((NativeFreshLockedChestResearchDiscPlan)null!,unread,cancel.Token),check);
        Reject<ArgumentNullException>(()=>NativeId65FreshDiscStreaming.Measure((NativeFreshLockedChestResearchDiscPlan)null!,unread,CancellationToken.None),check);
        check(unread.Accesses==0,"Null/canceled chest streaming refuses before touching caller-owned streams.");
        Reject<InvalidDataException>(()=>NativeId65FreshWizardDiscGeometry.Compile(0x12000,0x20F000),check);
        Reject<ArgumentNullException>(()=>NativeId65FreshWizardDiscGeometry.CompileChest(null!),check);
        Reject<ArgumentNullException>(()=>NativeFreshResearchPhysicalAfterimages.ForChest(null!,null!),check);
        Reject<ArgumentNullException>(()=>Source("\0",null!,check).GetAwaiter().GetResult(),check);
        Reject<ArgumentNullException>(()=>SourceSameLayout("\0",null!,check).GetAwaiter().GetResult(),check);
        Reject<ArgumentNullException>(()=>SourceAuthoredDestinations("\0",null!,check).GetAwaiter().GetResult(),check);
        var helper=typeof(NativeFreshResearchPhysicalAfterimages);
        check(!helper.IsPublic&&helper.IsSealed&&helper.GetConstructors(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).All(c=>c.IsPrivate)&&
            helper.GetMethods(BindingFlags.Static|BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly).Length==0,
            "Shared physical afterimages expose no public byte/hash/delegate issuer.");
        Console.WriteLine("NATIVE_FRESH_CHEST_DISC_FREE_OK experimentalKeyAssumption=true completeCensus=false runtime=false");
    }

    internal static Task Source(string path,NativeFreshLockedChestExperimentalExecutable executable,Action<bool,string> check)
    {
        ArgumentNullException.ThrowIfNull(check);
        RequireOriginalPlacement(executable);
        Reject<InvalidDataException>(()=>RequireSameLayoutPlacement(executable),check);
        check(true,"Historical chest disc entry retains the exact independent placed-scene reference480; it does not accept the wider fixture.");
        return SourceCore(path,executable,check,"NATIVE_FRESH_CHEST_DISC_SOURCE_OK");
    }

    internal static Task SourceSameLayout(string path,NativeFreshLockedChestExperimentalExecutable executable,Action<bool,string> check)
    {
        ArgumentNullException.ThrowIfNull(check);
        RequireSameLayoutPlacement(executable);
        Reject<InvalidDataException>(()=>RequireOriginalPlacement(executable),check);
        Reject<InvalidDataException>(()=>RequireSameLayoutPlacement(executable,true),check);
        check(true,"Separate wide disc entry requires the actual retained SameLayoutMaterialAuthored producer,3072 square and exact new key/chest anchors.");
        return SourceCore(path,executable,check,"NATIVE_FRESH_CHEST_DISC_SAME_LAYOUT_SOURCE_OK");
    }

    internal static Task SourceAuthoredDestinations(string path,NativeFreshLockedChestExperimentalExecutable executable,Action<bool,string> check)
    {
        ArgumentNullException.ThrowIfNull(check);
        RequireSameLayoutPlacement(executable,true);
        Reject<InvalidDataException>(()=>RequireOriginalPlacement(executable),check);
        Reject<InvalidDataException>(()=>RequireSameLayoutPlacement(executable),check);
        check(true,"Separate R03 physical entry consumes actual authored Reward2 destination ownership without weakening frozen R01/R02 gates.");
        return SourceCore(path,executable,check,"NATIVE_FRESH_CHEST_DISC_AUTHORED_DESTINATIONS_SOURCE_OK");
    }

    private static void RequireOriginalPlacement(NativeFreshLockedChestExperimentalExecutable executable)
    {
        ArgumentNullException.ThrowIfNull(executable);
        executable.RequireAncestry(executable.PlacementAncestor,executable.HostAncestor);
        Require(executable.PlacementAncestor.RewardDestinationsAncestor is null&&
            Hash(executable.PlacementAncestor.CopyScene())=="075c84cfd5ec13dc245dfa8b888f349903056b07676fc1990c494875e90f20d3",
            "Historical chest disc source requires the exact independent original-only placed-scene reference480.");
    }

    private static void RequireSameLayoutPlacement(NativeFreshLockedChestExperimentalExecutable executable,bool authored=false)
    {
        ArgumentNullException.ThrowIfNull(executable);
        executable.RequireAncestry(executable.PlacementAncestor,executable.HostAncestor);
        var placement=executable.PlacementAncestor;
        var basis=placement.BodyAncestor.ResourceAncestor.BaseBodyAncestor;
        var mixed=basis.MixedResourceAncestor??throw new InvalidDataException("Wide chest disc requires an actual material-authored base Body.");
        var scene=basis.HostAncestor.SceneAncestor;
        var floor=scene.CompilationAncestor.Intent;
        basis.RequireMixedAncestry(mixed);
        scene.RequireAncestry(scene.CompilationAncestor,scene.FamilyAncestor,scene.ResourceAncestor);
        mixed.RequireAncestry(basis.HostAncestor,mixed.DocumentAncestor,mixed.MaterialSources[0],mixed.MaterialSources[1]);
        Require(mixed.IsSameLayoutMaterialAuthored&&scene.Producer==NativeFreshWizardSceneProducer.SameLayoutMaterialAuthored&&
            ReferenceEquals(mixed.SceneAncestor,scene)&&ReferenceEquals(mixed.DocumentAncestor,scene.AuthoringDocumentAncestor)&&
            ReferenceEquals(mixed.DocumentAncestor.Base,floor)&&
            floor.FloorX==8448&&floor.FloorY==8448&&floor.FloorZ==8704&&floor.FloorWidth==3072&&floor.FloorDepth==3072&&
            floor.SpawnX==8960&&floor.SpawnY==8960&&floor.SpawnZ==9216&&
            placement.Intent==new NativeFreshLockedChestPlacementIntent(9728,9120,8704,10864,9888,8704),
            "Wide chest disc requires the actual same-layout material source/intent and exact3072 placement fixture, not just compatible physical lengths.");
        placement.RequireAncestry(executable.BodyAncestor,placement.Intent);
        if(authored)
        {
            var destinations=placement.RewardDestinationsAncestor??throw new InvalidDataException("R03 requires an actual explicit destination owner.");
            placement.RequireRewardDestinationsAncestry(destinations);
            Require(destinations.Values.Count==1&&destinations.Values[0]==
                new NativeFreshChestRewardDestinationOverride(NativeFreshChestRewardRole.Reward2,10454,10503,9216),
                "R03 requires exactly the independently tested world destination for Reward2, not another authored pattern.");
        }
        else Require(placement.RewardDestinationsAncestor is null,"Historical R02 requires the inherited source-relative reward pattern.");
        // WideChestPlacementCases independently reconstructs every coordinate
        // word and full scene/Body complement upstream. Do not turn this actual
        // retained scene into a new producer-derived golden or caller hash.
        placement.RequireExactScene(placement.CopyScene());
    }

    private static async Task SourceCore(string path,NativeFreshLockedChestExperimentalExecutable executable,
        Action<bool,string> check,string successMarker)
    {
        using(var original=File.OpenRead(path))await NativeId65FreshDiscSource.RequireWholeOriginalAsync(original,CancellationToken.None);
        try
        {
            var plan=await NativeFreshLockedChestResearchDiscPlan.CaptureAsync(path,executable);
            plan.RequireAncestry(executable);
            check(ReferenceEquals(plan.ExecutableAncestor,executable)&&ReferenceEquals(plan.PlacementAncestor,executable.PlacementAncestor)&&
                plan.ExecutablePlanSha256==executable.PlanSha256&&plan.PlacementPlanSha256==executable.PlacementAncestor.PlanSha256&&
                plan.BodyPlanSha256==executable.BodyAncestor.PlanSha256&&plan.HostPlanSha256==executable.HostAncestor.PlanSha256&&
                plan.ResourcePlanSha256==executable.BodyAncestor.ResourceAncestor.PlanSha256,
                "Disc retains complete actual placement/experimental executable/Body/Host/resource lineage.");
            check(plan.SourceCertified&&plan.ExperimentalKeyZeroAssumption&&plan.ExperimentalTreasureTarget==20&&
                !plan.CompleteStaticCensusProven&&!plan.CompleteNativeDependencyClosureProven&&!plan.RuntimeVerified&&
                !plan.PublicationApproved&&!plan.NormalCreateBinEnabled&&plan.PendingOwners.Count==4,
                "Exact physical bytes do not silently certify key contribution, full census, gameplay or normal Create BIN.");
            check(plan.BodyLba==53911&&plan.FinalExecutableLba==54965&&plan.FinalExecutableEndExclusive==End&&
                plan.FinalWadByteLength==0x6B48000&&plan.ChangedSectorOwnerCount==1297&&plan.SourceImageSha256==Original,
                "Actual12000 Host plus20F000 Body derives221000 growth and1297 changed physical owners.");
            var expected=ExpectedPayloads.Capture(path,executable);
            byte[][] copies=[plan.CopyBody(),plan.CopyOverlay(),plan.CopyExecutable(),plan.CopyWadDirectory(),plan.CopyIsoRootDirectory()];
            byte[][] required=[expected.Body,expected.Overlay,expected.Executable,expected.Wad,expected.Root];
            check(copies.Zip(required).All(p=>p.First.SequenceEqual(p.Second))&&
                new[]{plan.BodySha256,plan.OverlaySha256,plan.ExecutableSha256,plan.WadDirectorySha256,plan.IsoRootSha256}
                    .SequenceEqual(required.Select(b=>Hash(b))),
                "All complete typed payloads and independently patched both-endian root/reserved WAD owners match.");
            check(plan.CopyBody().AsSpan(0x102000,0x3800).SequenceEqual(executable.PlacementAncestor.CopyScene()),
                "Disc's complete scene is exactly its actual typed placement ancestor; entry-specific fixture authority remains separate.");
            string identity=plan.PlanSha256;
            foreach(byte[] bytes in copies){bytes[0]^=1;bytes[^1]^=1;}
            check(new[]{plan.CopyBody(),plan.CopyOverlay(),plan.CopyExecutable(),plan.CopyWadDirectory(),plan.CopyIsoRootDirectory()}
                .Zip(required).All(p=>p.First.SequenceEqual(p.Second))&&plan.PlanSha256==identity,
                "Returned physical payloads and metadata are detached copies.");
            var equal=NativeFreshLockedChestExperimentalExecutable.Compose(executable.PlacementAncestor,executable.HostAncestor);
            check(!ReferenceEquals(equal,executable)&&equal.PlanSha256==executable.PlanSha256&&
                equal.CopyExecutable().SequenceEqual(executable.CopyExecutable()),"Equal-hash distinct actual experimental executable control.");
            Reject<InvalidDataException>(()=>plan.RequireAncestry(equal),check);
            Reject<ArgumentNullException>(()=>plan.RequireAncestry(null!),check);
            var moved=NativeFreshLockedChestPlacementPlan.Compose(executable.BodyAncestor,
                executable.PlacementAncestor.Intent with{KeyX=executable.PlacementAncestor.Intent.KeyX+16});
            var movedExe=NativeFreshLockedChestExperimentalExecutable.Compose(moved,executable.HostAncestor);
            check(movedExe.CopyExecutable().SequenceEqual(executable.CopyExecutable())&&moved.LevelDataSha256!=plan.BodySha256,
                "Equal executable bytes cannot substitute a different actual placed Body.");
            Reject<InvalidDataException>(()=>plan.RequireAncestry(movedExe),check);
            using(var cancel=new CancellationTokenSource())
            {
                cancel.Cancel();await RejectAsync<OperationCanceledException>(()=>NativeFreshLockedChestResearchDiscPlan.CaptureAsync("\0",executable,cancel.Token),check);
            }
            await RejectAsync<ArgumentException>(()=>NativeFreshLockedChestResearchDiscPlan.CaptureAsync("\0",executable),check);
            await RejectAsync<ArgumentException>(()=>NativeFreshLockedChestResearchDiscPlan.CaptureAsync("",executable),check);
            var repeat=await NativeFreshLockedChestResearchDiscPlan.CaptureAsync(path,executable);
            check(repeat.PlanSha256==plan.PlanSha256&&repeat.CopyBody().SequenceEqual(expected.Body)&&
                repeat.CopyIsoRootDirectory().SequenceEqual(expected.Root),"Repeated complete capture is deterministic.");

            NativeId65FreshDiscVerification measured;
            string inverseHash,expectedHash;
            using(var source=File.OpenRead(path))
            using(var sinkSource=File.OpenRead(path))
            using(var sink=new InverseSink(sinkSource,plan,expected,check))
            {
                int first=0;
                measured=NativeId65FreshDiscStreaming.Measure(plan,source,CancellationToken.None,sink,()=>first++);
                (inverseHash,expectedHash)=sink.Complete();
                check(first==1&&measured.ChangedSectors==1297&&measured.OutputSha256==expectedHash&&
                    inverseHash==Original&&source.CanRead&&sinkSource.CanRead,
                    "Shared streaming equals every independently assembled raw sector and restores the complete Original through the actual inverse.");
            }
            using(var source=File.OpenRead(path))
            using(var virtualSource=File.OpenRead(path))
            using(var output=new ExpectedImage(virtualSource,expected))
            {
                source.Position=23;output.Position=47;
                var verified=plan.VerifyImage(source,output);
                check(verified==measured&&source.Position==ImageBytes&&output.Position==ImageBytes&&output.EofReads>0&&
                    source.CanRead&&virtualSource.CanRead,"Delegated full VerifyImage compares independent physical afterimages and consumes actual EOF without disposing inputs.");
            }
            using(var source=File.OpenRead(path))
            using(var output=new MemoryStream())
            using(var cancel=new CancellationTokenSource())
            {
                Reject<OperationCanceledException>(()=>NativeId65FreshDiscStreaming.Measure(plan,source,cancel.Token,output,cancel.Cancel),check);
                check(source.Position==SectorBytes&&output.Length==SectorBytes&&source.CanRead&&output.CanWrite,
                    "Cancellation after first measured sector returns no success and retains caller stream ownership.");
            }
            using(var source=File.OpenRead(path))
            {
                source.Position=1;
                Reject<InvalidDataException>(()=>NativeId65FreshDiscStreaming.Measure(plan,source,CancellationToken.None),check);
                check(source.Position==1,"Nonzero original position refuses before any expected-sector read.");
            }
            using(var source=File.OpenRead(path))
            using(var output=new MemoryStream(new byte[1]))
                Reject<InvalidDataException>(()=>NativeId65FreshDiscStreaming.Measure(plan,source,CancellationToken.None,output),check);
            using(var source=File.OpenRead(path))
            using(var invalid=new MemoryStream())
                Reject<InvalidDataException>(()=>plan.VerifyImage(source,invalid),check);
            byte[] raw=new byte[SectorBytes];
            using(var source=File.OpenRead(path))source.ReadExactly(raw);
            foreach(int lba in new[]{-1,SectorCount,int.MaxValue})
            {
                Reject<InvalidDataException>(()=>plan.CreateExpectedRawSector(lba,raw),check);
                Reject<InvalidDataException>(()=>plan.CreateOriginalRawSector(lba,raw),check);
            }
            foreach(byte[] bad in new byte[][]{[],raw[..^1],[..raw,(byte)0]})
            {
                Reject<InvalidDataException>(()=>plan.CreateExpectedRawSector(0,bad),check);
                Reject<InvalidDataException>(()=>plan.CreateOriginalRawSector(0,bad),check);
            }
            plan.RequireAncestry(executable);
            check(plan.PlanSha256==identity&&plan.CopyBody().SequenceEqual(expected.Body)&&
                executable.CopyExecutable().SequenceEqual(expected.Executable),"All refusals leave actual immutable ancestors and physical snapshots intact.");
            Console.WriteLine($"{successMarker} observedImage={measured.OutputSha256} changed={measured.ChangedSectors} inverse={inverseHash} plan={identity} independentPayloadGolden=false outputsWritten=0 completeCensus=false runtime=false");
        }
        finally
        {
            using var original=File.OpenRead(path);
            await NativeId65FreshDiscSource.RequireWholeOriginalAsync(original,CancellationToken.None);
        }
    }

    private sealed record ExpectedPayloads(byte[] Body,byte[] Overlay,byte[] Executable,byte[] Wad,byte[] Root,byte WadFlags,byte ExeFlags)
    {
        internal static ExpectedPayloads Capture(string path,NativeFreshLockedChestExperimentalExecutable executable)
        {
            byte[] body=executable.PlacementAncestor.CopyLevelData(),overlay=executable.HostAncestor.CopyOverlay(),exe=executable.CopyExecutable();
            Require(body.Length==0x20F000&&overlay.Length==0x12000&&exe.Length==0x66000,"Exact typed payload allocations");
            using var source=File.OpenRead(path);var layout=DiscImage.DetectLayout(source);
            byte[] root=DiscImage.ReadFileBytes(source,layout,22,0,2048),wad=DiscImage.ReadFileBytes(source,layout,37,0,2048);
            byte[] beforeRoot=root.ToArray(),beforeWad=wad.ToArray();
            Require(Word(root,562)==53875&&Big(root,566)==53875&&Word(root,690)==0x6927000&&Big(root,694)==0x6927000,
                "Exact original ISO both-endian metadata premises");
            Require(wad.AsSpan(79*8,16).SequenceEqual(new byte[16])&&wad.AsSpan(81*8,8).SequenceEqual(Convert.FromHexString("0078C90500200000")),
                "Exact original empty pair and complete adjacent occupied row");
            Put(root,562,54965);PutBig(root,566,54965);Put(root,690,0x6B48000);PutBig(root,694,0x6B48000);
            Put(wad,79*8,0x6927000);Put(wad,79*8+4,0x12000);Put(wad,80*8,0x6939000);Put(wad,80*8+4,0x20F000);
            Require(Enumerable.Range(0,root.Length).Where(i=>root[i]!=beforeRoot[i]).All(i=>i is >=562 and <570 or >=690 and <698)&&
                wad.AsSpan(0,79*8).SequenceEqual(beforeWad.AsSpan(0,79*8))&&wad.AsSpan(81*8).SequenceEqual(beforeWad.AsSpan(81*8)),
                "Independent complete metadata mutation complement");
            byte[] raw=new byte[SectorBytes];source.Position=53874L*SectorBytes;source.ReadExactly(raw);byte wadFlags=(byte)(raw[18]&0x81);
            source.Position=54078L*SectorBytes;source.ReadExactly(raw);byte exeFlags=(byte)(raw[18]&0x81);
            return new(body,overlay,exe,wad,root,wadFlags,exeFlags);
        }
        internal byte[] Sector(int lba,ReadOnlySpan<byte> before)
        {
            // Do not call either production disc's afterimage method here.
            byte[] result=before.ToArray();if(!Changed(lba))return result;
            var layout=new DiscLayout(2352,24,22,2048);
            if(lba>=54079)result=RawMode2Form1SectorIntegrity.PrepareOwnedZeroPaddingSector(result,layout,lba);
            RawMode2Form1SectorIntegrity.RequireLogicalWriteSector(result,layout,lba);
            ReadOnlySpan<byte> payload=lba switch
            {
                22=>Root,37=>Wad,
                >=53875 and <53911=>Overlay.AsSpan((lba-53875)*2048,2048),
                >=53911 and <54965=>Body.AsSpan((lba-53911)*2048,2048),
                >=54965 and <End=>Executable.AsSpan((lba-54965)*2048,2048),
                _=>before.Slice(24,2048)
            };
            payload.CopyTo(result.AsSpan(24,2048));
            int flags=lba==22?before[18]&0x81:lba==54964?WadFlags:lba==55168?ExeFlags:0;
            result[18]=result[22]=(byte)((result[18]&~0x81)|flags);
            RawMode2Form1SectorIntegrity.RebuildLogicalWriteSector(result,layout,lba);return result;
        }
    }

    private sealed class InverseSink(Stream original,NativeFreshLockedChestResearchDiscPlan plan,ExpectedPayloads expected,Action<bool,string> check):Stream
    {
        private readonly IncrementalHash _inverse=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        private readonly IncrementalHash _expected=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        private long _position;private int _changed,_newTail;
        public override bool CanRead=>false;public override bool CanSeek=>true;public override bool CanWrite=>true;
        public override long Length=>_position;
        public override long Position{get=>_position;set{if(value!=_position)throw new NotSupportedException();}}
        public override void Write(byte[] b,int at,int n)=>Write(b.AsSpan(at,n));
        public override void Write(ReadOnlySpan<byte> supplied)
        {
            Require(supplied.Length==SectorBytes&&_position<ImageBytes,"One complete sequential measured sector");
            int lba=(int)(_position/SectorBytes);byte[] before=new byte[SectorBytes];original.ReadExactly(before);
            byte[] after=expected.Sector(lba,before),restored=plan.CreateOriginalRawSector(lba,supplied);
            Require(supplied.SequenceEqual(after)&&restored.SequenceEqual(before),"Complete independent raw afterimage and exact inverse at LBA "+lba);
            bool changed=!after.AsSpan().SequenceEqual(before);
            Require(changed==Changed(lba),"Exact physical changed-owner partition at LBA "+lba);
            if(changed){_changed++;if(lba>=55125)_newTail++;}
            _expected.AppendData(after);_inverse.AppendData(restored);_position+=SectorBytes;
            if(lba is 0 or 22 or 37 or 53874 or 53875 or 53910 or 53911 or 54078 or 54079 or 54964 or 54965 or 55124 or 55125 or 55168 or 55169 or 59999 or 60000 or 281269)
            {
                foreach(int at in new[]{0,12,15,16,18,20,22,24,2072,2076,2351})
                {
                    byte[] bad=after.ToArray();bad[at]^=1;
                    Reject<InvalidDataException>(()=>plan.CreateOriginalRawSector(lba,bad),check);
                }
                byte[] wrongSource=before.ToArray();wrongSource[24]^=1;
                Reject<InvalidDataException>(()=>plan.CreateExpectedRawSector(lba,wrongSource),check);
                Reject<InvalidDataException>(()=>plan.CreateExpectedRawSector(lba==281269?0:lba+1,before),check);
                check(after.AsSpan(0,16).SequenceEqual(before.AsSpan(0,16))&&
                    (!Changed(lba)||after.AsSpan(16,4).SequenceEqual(after.AsSpan(20,4))),
                    "Actual sector address/header ownership and mirrored subheaders remain exact.");
                int[] metadata=lba==22?[562,566,690,694]:lba==37?[79*8,79*8+4,80*8,80*8+4,81*8]:[];
                foreach(int at in metadata)
                {
                    byte[] bad=after.ToArray();bad[24+at]^=1;
                    RawMode2Form1SectorIntegrity.RebuildLogicalWriteSector(bad,new(2352,24,22,2048),lba);
                    Reject<InvalidDataException>(()=>plan.CreateOriginalRawSector(lba,bad),check);
                }
            }
        }
        internal (string Inverse,string Expected) Complete()
        {
            Require(_position==ImageBytes&&original.Position==ImageBytes&&original.Length==ImageBytes&&original.ReadByte()==-1,
                "Complete sequential inverse sink and actual source EOF");
            check(_changed==1297&&_newTail==44,"Every1297 changed owner and44 extended-tail owners were independently compared and inverted.");
            return(Convert.ToHexStringLower(_inverse.GetHashAndReset()),Convert.ToHexStringLower(_expected.GetHashAndReset()));
        }
        protected override void Dispose(bool disposing){if(disposing){_inverse.Dispose();_expected.Dispose();}base.Dispose(disposing);}
        public override void Flush(){}
        public override int Read(byte[] b,int at,int n)=>throw new NotSupportedException();
        public override long Seek(long n,SeekOrigin origin)=>throw new NotSupportedException();
        public override void SetLength(long n)=>throw new NotSupportedException();
    }
    private sealed class ExpectedImage(Stream original,ExpectedPayloads expected):Stream
    {
        private long _position;internal int EofReads;
        public override bool CanRead=>true;public override bool CanSeek=>true;public override bool CanWrite=>false;
        public override long Length=>ImageBytes;
        public override long Position{get=>_position;set{if(value<0||value>Length)throw new ArgumentOutOfRangeException(nameof(value));_position=value;}}
        public override int Read(byte[] b,int at,int n)=>Read(b.AsSpan(at,n));
        public override int Read(Span<byte> destination)
        {
            int count=(int)Math.Min(destination.Length,Length-_position),copied=0;
            if(count==0&&!destination.IsEmpty)EofReads++;
            while(copied<count)
            {
                int lba=(int)(_position/SectorBytes),offset=(int)(_position%SectorBytes),part=Math.Min(SectorBytes-offset,count-copied);
                byte[] before=new byte[SectorBytes];original.Position=lba*(long)SectorBytes;original.ReadExactly(before);
                expected.Sector(lba,before).AsSpan(offset,part).CopyTo(destination[copied..]);copied+=part;_position+=part;
            }
            return count;
        }
        public override long Seek(long n,SeekOrigin origin){Position=checked((origin switch{SeekOrigin.Begin=>0,SeekOrigin.Current=>Position,SeekOrigin.End=>Length,_=>throw new ArgumentOutOfRangeException(nameof(origin))})+n);return Position;}
        public override void Flush()=>throw new NotSupportedException();
        public override void SetLength(long n)=>throw new NotSupportedException();
        public override void Write(byte[] b,int at,int n)=>throw new NotSupportedException();
    }
    private sealed class Unreadable:Stream
    {
        internal int Accesses;
        private T Touch<T>(){Accesses++;throw new InvalidOperationException("Source-free test touched a stream.");}
        public override bool CanRead=>Touch<bool>();public override bool CanSeek=>Touch<bool>();public override bool CanWrite=>Touch<bool>();
        public override long Length=>Touch<long>();public override long Position{get=>Touch<long>();set=>Touch<int>();}
        public override int Read(byte[] b,int at,int n)=>Touch<int>();public override long Seek(long n,SeekOrigin origin)=>Touch<long>();
        public override void Flush()=>Touch<int>();public override void SetLength(long n)=>Touch<int>();public override void Write(byte[] b,int at,int n)=>Touch<int>();
    }
    private static bool Changed(int lba)=>lba is 22 or 37||lba>=53874&&lba<End;
    private static int Word(byte[] b,int at)=>BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(at));
    private static int Big(byte[] b,int at)=>BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(at));
    private static void Put(byte[] b,int at,int value)=>BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(at),value);
    private static void PutBig(byte[] b,int at,int value)=>BinaryPrimitives.WriteInt32BigEndian(b.AsSpan(at),value);
    private static string Hash(ReadOnlySpan<byte> b)=>Convert.ToHexStringLower(SHA256.HashData(b));
    private static void Require(bool ok,string label){if(!ok)throw new InvalidDataException(label);}
    private static void Reject<T>(Action action,Action<bool,string> check)where T:Exception
    {try{action();}catch(T){check(true,"Chest physical refusal "+typeof(T).Name);return;}throw new InvalidDataException("Expected "+typeof(T).Name);}
    private static async Task RejectAsync<T>(Func<Task> action,Action<bool,string> check)where T:Exception
    {try{await action();}catch(T){check(true,"Chest physical refusal "+typeof(T).Name);return;}throw new InvalidDataException("Expected "+typeof(T).Name);}
}
