using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

// Future explicit opt-in only; default/source must never call this method.
// Nine actual PublishNew sets (~5.95 GB) are retained. No artifact is repaired,
// reused, or removed afterward. A separate root-approved absent path is required.
internal static class PostcommitCases
{
    private const long ImageLength=661547040,CorruptionOffset=37L*2352+24;
    private const string OriginalHash="fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37";
    private const string ImageHash="c4a70f826b1a9a3d4ad8e36512be354b6205d27ea2c7a314776a51067f35b5a6";
    private enum Scenario{LateCancel,CleanupCancel,RollbackCallback,StageOwner,StageFile,CorruptImage}

    internal static async Task Run(NativeId65FreshWizardResearchCandidatePlan plan,string workspace)
    {
        ArgumentNullException.ThrowIfNull(plan);ArgumentException.ThrowIfNullOrWhiteSpace(workspace);
        if(!Path.IsPathFullyQualified(workspace))throw new InvalidDataException("Postcommit workspace must be an explicitly approved absolute path.");
        string root=Path.TrimEndingDirectorySeparator(Path.GetFullPath(workspace));
        PersistencePathSafety.EnsureTreesDoNotOverlap(plan.SourceImagePath,root,"Postcommit workspace overlaps original BIN.");
        PersistencePathSafety.EnsureTreesDoNotOverlap(plan.SourceCuePath,root,"Postcommit workspace overlaps original CUE.");
        RequireAbsent(root);RequireUnlinkedAncestors(root);
        if(Directory.GetParent(root) is not {Exists:true})throw new InvalidDataException("Postcommit workspace parent must already exist.");
        int checks=0;var reports=new List<object>();
        string[] inputs=ReadInputs();await plan.RequireFreshAsync(CancellationToken.None);
        byte[] expectedCue=plan.CopyCueBytes(),expectedReceipt=plan.BuildReceipt(new(ImageHash,1290));
        string canonicalHash=Hash(plan.CopyCanonicalDocument());var actualDisc=plan.DiscAncestor;
        var binding=NativeId65FreshResearchPublicationBinding.FromWizard(plan);
        Check(ReferenceEquals(binding.WizardPlan,plan)&&binding.SavedContext==null&&binding.TracksCommittedFailure,
            "Only this actual issued Wizard binding owns the test; no saved or published object is fabricated.");
        (string Name,Scenario Kind,string? Owner)[] cases=
        [
            ("01-late-cancel-success",Scenario.LateCancel,null),
            ("02-cleanup-oce",Scenario.CleanupCancel,null),
            ("03-aftercommit-rollback-exception",Scenario.RollbackCallback,null),
            ("04-unknown-stage-owner",Scenario.StageOwner,"unknown-postcommit-owner.txt"),
            ("05-reused-image-basename",Scenario.StageOwner,NativeId65FreshWizardResearchCandidatePlan.ImageName),
            ("06-reused-cue-basename",Scenario.StageOwner,NativeId65FreshWizardResearchCandidatePlan.CueName),
            ("07-reused-receipt-basename",Scenario.StageOwner,NativeId65FreshWizardResearchCandidatePlan.ReceiptName),
            ("08-stage-replaced-by-file",Scenario.StageFile,null),
            ("09-corrupted-committed-image",Scenario.CorruptImage,null)
        ];
        // Guard again immediately before the first write. Trusted ancestors and
        // cooperating writers are required; this is not an adversarial FS lock.
        RequireAbsent(root);Directory.CreateDirectory(root);
        try
        {
            Check(!Directory.EnumerateFileSystemEntries(root).Any(),"New postcommit workspace starts empty.");
            foreach(var entry in cases)await RunCase(entry.Name,entry.Kind,entry.Owner);
            RequireEntries(root,cases.Select(c=>Path.Combine(root,c.Name)).ToArray());
            Check(ReferenceEquals(plan.DiscAncestor,actualDisc)&&Hash(plan.CopyCanonicalDocument())==canonicalHash&&
                plan.CopyCueBytes().SequenceEqual(expectedCue)&&plan.BuildReceipt(new(ImageHash,1290)).SequenceEqual(expectedReceipt),
                "Every real publication retained the same actual ancestry and immutable companion/canonical bytes.");
        }
        finally
        {
            await plan.RequireFreshAsync(CancellationToken.None);
            Check(ReadInputs().SequenceEqual(inputs),"Full original BIN/CUE SHA, length, position and EOF preserved in the outer finally.");
        }
        Console.WriteLine("FRESH_WIZARD_POSTCOMMIT_EXERCISED "+JsonSerializer.Serialize(new
        {
            checks,workspace=root,cases=reports,actualPublications=cases.Length,originalUnchanged=true,
            retainedAllCandidateSets=true,physicalOutputsRepaired=false,leaseReplacementExercised=false,runtime=false,normalExport=false,
            globalAudioCompatibility=false,peakCapacity=false,cardHistoryCompatibility=false
        }));

        async Task RunCase(string name,Scenario kind,string? ownerName)
        {
            string output=Path.Combine(root,name);RequireAbsent(output);RequireUnlinkedAncestors(output);
            string image=Path.Combine(output,binding.ImageName),lease=Path.Combine(output,binding.LeaseName),stage="";
            int afterCommit=0,beforeCleanup=0;bool effectApplied=false;byte? originalByte=null;
            byte[] foreignOwner=Encoding.UTF8.GetBytes("Retain exact actual postcommit owner: "+name+"\n");
            Exception? witness=kind switch
            {
                Scenario.CleanupCancel=>new OperationCanceledException("Actual Wizard cleanup callback cancellation witness."),
                // This is the callback exception under test, not a fabricated
                // transaction outcome. Actual PublishNew must have returned first.
                Scenario.RollbackCallback=>new ExportArtifactRollbackException("Actual postcommit callback witness, not transaction rollback.","",new IOException("callback witness")),
                _=>null
            };
            NativeId65FreshDiscResearchPublication? completed=null;
            NativeId65FreshWizardResearchCommittedException? committedFailure=null;
            using var cancel=new CancellationTokenSource();
            try
            {
                try
                {
                    completed=await NativeId65FreshWizardResearchCandidatePublisher.PublishPostcommitForSmokeAsync(plan,output,(point,path)=>
                    {
                        if(point==NativeId65FreshWizardPostcommitPoint.AfterCommit)
                        {
                            afterCommit++;Check(afterCommit==1&&path==image,"Callback is reached only once at the actual final image path.");
                            foreach(string artifact in binding.OutputNames)RequireRegular(Path.Combine(output,artifact));
                            Check(new FileInfo(image).Length==ImageLength,"Actual transaction placed the complete image before postcommit injection.");
                            RequireCompanion(Path.Combine(output,binding.CueName),expectedCue);
                            RequireCompanion(Path.Combine(output,binding.ReceiptName),expectedReceipt);
                            string[] stages=Directory.EnumerateDirectories(output).Where(p=>Path.GetFileName(p).StartsWith(binding.StagePrefix,StringComparison.Ordinal)).ToArray();
                            Check(stages.Length==1,"Exactly one actual publisher-owned stage exists at known commit.");
                            stage=stages[0];RequireEmptyStage();RequireRegular(lease);
                            Check(new FileInfo(lease).Length==32,"Actual Wizard output lease has its 32-byte ownership marker while held.");
                            RequireOutputEntries();
                            if(kind==Scenario.LateCancel){effectApplied=true;cancel.Cancel();}
                            else if(kind==Scenario.RollbackCallback){effectApplied=true;throw witness!;}
                            else if(kind==Scenario.CorruptImage)
                            {
                                // Only this just-committed child's exact BIN is
                                // opened writable; neither original path is used.
                                PersistencePathSafety.EnsureWritePathWithinRoot(output,image);
                                using var file=new FileStream(image,FileMode.Open,FileAccess.ReadWrite,FileShare.None);
                                Check(file.Length==ImageLength,"Corruption fixture keeps the complete new candidate length.");
                                file.Position=CorruptionOffset;int value=file.ReadByte();Check(value>=0,"Actual committed byte exists.");
                                originalByte=(byte)value;file.Position=CorruptionOffset;file.WriteByte((byte)(value^1));file.Flush(true);
                                file.Position=CorruptionOffset;Check(file.ReadByte()==(value^1)&&file.Length==ImageLength,"Exactly the intended new candidate byte was changed.");
                                effectApplied=true;
                            }
                            return;
                        }
                        Check(point==NativeId65FreshWizardPostcommitPoint.BeforeStageCleanup,"Only the two closed Wizard postcommit points are accepted.");
                        beforeCleanup++;Check(beforeCleanup==1&&path==stage,"Actual empty stage path is retained from known commit.");
                        RequireEmptyStage();RequireOutputEntries();
                        if(kind==Scenario.CleanupCancel){effectApplied=true;throw witness!;}
                        if(kind==Scenario.StageOwner)
                        {WriteTinyNew(Path.Combine(stage,ownerName!),foreignOwner);effectApplied=true;}
                        if(kind==Scenario.StageFile)
                        {
                            // Deliberately replace only this verified empty stage,
                            // never recursively delete a directory or final owner.
                            Directory.Delete(stage,recursive:false);WriteTinyNew(stage,foreignOwner);effectApplied=true;
                        }
                    },cancel.Token);
                }
                catch(NativeId65FreshWizardResearchCommittedException error){committedFailure=error;}
                Check(afterCommit==1&&effectApplied,"Each case actually committed before its requested postcommit effect.");
                if(kind==Scenario.LateCancel)
                {
                    Check(committedFailure==null&&completed!=null&&beforeCleanup==1&&cancel.IsCancellationRequested,
                        "AfterCommit token cancellation still completes noncancellable verification and cleanup.");
                    var result=completed!;
                    Check(result.OutputDirectory==output&&result.ImagePath==image&&result.PlanSha256==plan.PlanSha256&&
                        result.ImageSha256==ImageHash&&result.ChangedRawSectors==1290&&result.RetainedRecoveryDirectory==""&&
                        result.WholeImageReadback&&result.ResearchOnly&&!result.RuntimeVerified&&!result.NormalCreateBinApproved&&!result.CompleteNativeDependencyClosureProven,
                        "Real successful result has exact owners/readback and no runtime approval.");
                    RequireEntries(output,binding.OutputNames.Select(n=>Path.Combine(output,n)).ToArray());
                    string[] hashes=ReadArtifacts(false);
                    var reopened=await NativeId65FreshWizardResearchCandidatePublisher.VerifyAsync(plan,output,CancellationToken.None);
                    Check(reopened==result,"Actual clean-set public Verify independently reopens late-cancel success.");
                    await Refuse<OperationCanceledException>(()=>NativeId65FreshWizardResearchCandidatePublisher.VerifyAsync(plan,output,cancel.Token));
                    Check(ReadArtifacts(false).SequenceEqual(hashes),"Normal Verify cancellation preserves all successful artifacts.");
                    reports.Add(new{name,verifiedSuccess=true,afterCommit,beforeCleanup,imageSha256=hashes[0],cueSha256=hashes[1],receiptSha256=hashes[2]});
                }
                else
                {
                    Check(completed==null&&committedFailure!=null&&beforeCleanup==(kind is Scenario.RollbackCallback or Scenario.CorruptImage?0:1),
                        "Actual postcommit error yields only the typed committed failure, never canceled/partial/success result.");
                    var failure=committedFailure!;
                    Check(ReviewedPublicationCases.ContainsCommittedFailure(failure)&&
                        ReviewedPublicationCases.ContainsCommittedFailure(new AggregateException(new IOException("separate final-check witness"),new IOException("outer witness",failure)))&&
                        !ReviewedPublicationCases.ContainsCommittedFailure(new AggregateException(new IOException("uncommitted witness"))),
                        "CLI discovers only the actual committed failure, including when later verification errors preserve it in an exception tree.");
                    Check(failure.OutputDirectory==output&&failure.ImagePath==image&&failure.CuePath==Path.Combine(output,binding.CueName)&&
                        failure.ReceiptPath==Path.Combine(output,binding.ReceiptName)&&failure.PlanSha256==plan.PlanSha256&&
                        failure.RetainedStageDirectory==stage&&failure.RetainedOutputLeasePath==lease&&failure.RetainedRecoveryDirectory=="",
                        "Typed failure reports actual retained stage/file and lease; no fictional rollback or saved-project owner.");
                    if(witness!=null)Check(ReferenceEquals(failure.InnerException,witness),"Typed committed failure retains the exact callback OCE/rollback exception.");
                    else if(kind==Scenario.CorruptImage)Check(failure.InnerException is InvalidDataException&&originalByte.HasValue,"Actual final artifact readback failed after the intended byte change.");
                    else Check(failure.InnerException is IOException&&failure.InnerException.Message.Contains("preserve it.",StringComparison.Ordinal),
                        "Actual unknown/reused/file stage owner reached the preserving cleanup guard.");
                    RequireOutputEntries();
                    if(kind==Scenario.StageFile)RequireCompanion(stage,foreignOwner);
                    else if(kind==Scenario.StageOwner)
                    {RequireEntries(stage,[Path.Combine(stage,ownerName!)]);RequireCompanion(Path.Combine(stage,ownerName!),foreignOwner);}
                    else RequireEmptyStage();
                    byte[] marker=ReadSmall(lease,32);string markerHash=Hash(marker);
                    string[] hashes=ReadArtifacts(kind==Scenario.CorruptImage);
                    if(kind==Scenario.CorruptImage)await Refuse<InvalidDataException>(()=>{_=plan.VerifyImage(image,CancellationToken.None);return Task.CompletedTask;});
                    else
                    {
                        var actual=plan.VerifyImage(image,CancellationToken.None);
                        Check(actual.OutputSha256==ImageHash&&actual.ChangedSectors==1290,"Actual retained image reopens through complete original-authenticated 1290-sector readback.");
                    }
                    await Refuse<IOException>(()=>NativeId65FreshWizardResearchCandidatePublisher.VerifyAsync(plan,output,CancellationToken.None));
                    RequireOutputEntries();RequireCompanion(lease,marker);
                    Check(ReadArtifacts(kind==Scenario.CorruptImage).SequenceEqual(hashes),"Failed clean-set Verify never deletes or repairs retained image/CUE/receipt/evidence.");
                    if(kind==Scenario.StageFile)RequireCompanion(stage,foreignOwner);
                    if(kind==Scenario.StageOwner)
                    {RequireEntries(stage,[Path.Combine(stage,ownerName!)]);RequireCompanion(Path.Combine(stage,ownerName!),foreignOwner);}
                    reports.Add(new{name,verifiedSuccess=false,knownCommittedFailure=true,afterCommit,beforeCleanup,
                        innerType=failure.InnerException!.GetType().Name,failure.RetainedStageDirectory,failure.RetainedOutputLeasePath,
                        failure.RetainedRecoveryDirectory,outputLeaseSha256=markerHash,imageSha256=hashes[0],cueSha256=hashes[1],receiptSha256=hashes[2],
                        deliberatelyCorrupted=kind==Scenario.CorruptImage,corruptionOffset=kind==Scenario.CorruptImage?CorruptionOffset:(long?)null});
                }
            }
            finally
            {
                await plan.RequireFreshAsync(CancellationToken.None);
                Check(ReadInputs().SequenceEqual(inputs),"Per-case noncancellable finally preserves complete original BIN/CUE identities and actual EOF.");
            }

            void RequireEmptyStage()
            {Check(stage.Length!=0&&Directory.Exists(stage),"Actual stage directory exists.");RequireUnlinkedAncestors(stage);RequireEntries(stage,[]);}
            void RequireOutputEntries()=>RequireEntries(output,[..binding.OutputNames.Select(n=>Path.Combine(output,n)),lease,stage]);
            string[] ReadArtifacts(bool corrupt)
            {
                foreach(string artifact in binding.OutputNames)RequireRegular(Path.Combine(output,artifact));
                string imageHash=corrupt?HashCorruptImage(image,originalByte!.Value):HashFile(image,ImageLength);
                if(!corrupt)Check(imageHash==ImageHash,"Complete image SHA is the independent primary physical golden.");
                RequireCompanion(Path.Combine(output,binding.CueName),expectedCue);RequireCompanion(Path.Combine(output,binding.ReceiptName),expectedReceipt);
                Check(DiscCueSheet.ReferencesDataImageSnapshot(expectedCue,Path.Combine(output,binding.CueName),image),"Exact output CUE snapshot resolves this actual child BIN.");
                return [imageHash,HashFile(Path.Combine(output,binding.CueName),expectedCue.Length),HashFile(Path.Combine(output,binding.ReceiptName),expectedReceipt.Length)];
            }
        }

        string[] ReadInputs()
        {
            string image=HashFile(plan.SourceImagePath,ImageLength),cue=HashFile(plan.SourceCuePath);
            Check(image==OriginalHash&&cue==plan.SourceCueSha256,"Actual original full BIN and captured CUE hashes match their source-issued identities.");
            return [image,cue];
        }
        string HashFile(string path,long? exact=null)
        {
            using var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);long length=file.Length;
            Check(exact.HasValue?length==exact.Value:length is >=1 and <=65536,"Hash owner has exact/bounded length.");
            string sha=Convert.ToHexStringLower(SHA256.HashData(file));Eof(file,length);return sha;
        }
        string HashCorruptImage(string path,byte originalByte)
        {
            using var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
            Check(file.Length==ImageLength,"Deliberately corrupted child retains full image length.");
            using var actual=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);using var restored=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            byte[] buffer=new byte[65536];long at=0;int restorations=0;
            while(at<ImageLength)
            {
                int n=(int)Math.Min(buffer.Length,ImageLength-at);file.ReadExactly(buffer.AsSpan(0,n));actual.AppendData(buffer.AsSpan(0,n));
                if(at<=CorruptionOffset&&CorruptionOffset<at+n)
                {int index=(int)(CorruptionOffset-at);Check(buffer[index]==(byte)(originalByte^1),"Retained candidate still has precisely the injected byte value.");buffer[index]=originalByte;restorations++;}
                restored.AppendData(buffer.AsSpan(0,n));at+=n;
                Check(file.Length==ImageLength&&file.Position==at,"Read-only corrupt-image audit keeps exact block position/length.");
            }
            Eof(file,ImageLength);string sha=Convert.ToHexStringLower(actual.GetHashAndReset());
            Check(restorations==1&&sha!=ImageHash&&Convert.ToHexStringLower(restored.GetHashAndReset())==ImageHash,
                "Virtual restoration only proves the full retained candidate differs by exactly the intended byte; no on-disk repair.");
            return sha;
        }
        byte[] ReadSmall(string path,int length)
        {RequireRegular(path);using var file=File.OpenRead(path);Check(file.Length==length,"Exact retained small owner length.");byte[] bytes=new byte[length];file.ReadExactly(bytes);Eof(file,length);return bytes;}
        void RequireCompanion(string path,byte[] expected)
        {RequireRegular(path);using var file=File.OpenRead(path);NativeId65FreshResearchPublicationEngine.RequireCompanion(file,expected,CancellationToken.None);checks++;}
        void Eof(Stream file,long length)=>Check(file.Position==length&&file.Length==length&&file.ReadByte()==-1&&file.Position==length&&file.Length==length,
            "Complete read has actual EOF and final length/position checks.");
        void RequireEntries(string directory,string[] expected)
        {RequireUnlinkedAncestors(directory);Check(Directory.EnumerateFileSystemEntries(directory).Order(StringComparer.Ordinal).SequenceEqual(expected.Order(StringComparer.Ordinal)),"Exact declared child/stage/root owners, no extra entry accepted.");}
        void RequireRegular(string path)=>Check(File.Exists(path)&&!Directory.Exists(path)&&new FileInfo(path).LinkTarget==null,"Actual owner is a non-linked regular file.");
        void WriteTinyNew(string path,byte[] bytes)
        {
            PersistencePathSafety.EnsureWritePathWithinRoot(root,path);RequireAbsent(path);RequireUnlinkedAncestors(path);
            using var file=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);file.Write(bytes);file.Flush(true);
        }
        async Task Refuse<T>(Func<Task> action)where T:Exception
        {try{await action();}catch(T){checks++;return;}throw new InvalidDataException("Expected actual postcommit refusal "+typeof(T).Name);}
        void Check(bool ok,string why){if(!ok)throw new InvalidDataException(why);checks++;}
    }

    private static void RequireAbsent(string path)
    {if(File.Exists(path)||Directory.Exists(path)||new FileInfo(path).LinkTarget!=null||new DirectoryInfo(path).LinkTarget!=null)throw new IOException("Postcommit workspace/child/fixture must be new, absent and non-linked: "+path);}
    private static void RequireUnlinkedAncestors(string path)
    {
        for(string? current=path;current!=null;current=Directory.GetParent(current)?.FullName)
            if(new FileInfo(current).LinkTarget!=null||new DirectoryInfo(current).LinkTarget!=null)
                throw new InvalidDataException("Postcommit fixture requires unlinked exact ancestors: "+current);
    }
    private static string Hash(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
}
