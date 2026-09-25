using Spyro.Editor.Core.Persistence;

namespace Spyro.Editor.Core.Exporting;

public sealed record NativeWizardResearchPublication(string OutputDirectory,string ImagePath,string CuePath,string ReceiptPath,
    string ImageSha256,string PlanSha256,int ChangedRawSectors,string RetainedRecoveryDirectory="")
{
    public bool WholeImageReadback=>true;
    public bool ResearchOnly=>true;
    public bool RuntimeVerified=>false;
    public bool NormalCreateBinApproved=>false;
}
internal enum NativeWizardResearchFault { None,AfterImageWrite,BeforePublication }

/// <summary>Legacy public entry points retain their exact one-Wizard source/profile contract.</summary>
public static class NativeDarkHollowWizardResearchCandidatePublisher
{
    public static async Task<NativeWizardResearchPublication> PublishAsync(NativeDarkHollowWizardResearchCandidatePlan plan,string directory,CancellationToken token=default)=>
        await NativeWizardResearchPublicationEngine.PublishAsync(NativeWizardResearchPublicationBinding.FromLegacy(plan),directory,token);
    public static async Task<NativeWizardResearchPublication> VerifyAsync(NativeDarkHollowWizardResearchCandidatePlan plan,string directory,CancellationToken token=default)=>
        await NativeWizardResearchPublicationEngine.VerifyAsync(NativeWizardResearchPublicationBinding.FromLegacy(plan),directory,token);
    internal static async Task<NativeWizardResearchPublication> PublishForSmokeAsync(NativeDarkHollowWizardResearchCandidatePlan plan,string directory,
        NativeWizardResearchFault fault,Action<NativeWizardResearchFault,string>? checkpoint=null,CancellationToken token=default)=>
        await NativeWizardResearchPublicationEngine.PublishForSmokeAsync(NativeWizardResearchPublicationBinding.FromLegacy(plan),directory,fault,checkpoint,token);
    internal static void RequireCompanion(Stream input,ReadOnlySpan<byte> expected)=>NativeWizardResearchPublicationEngine.RequireCompanion(input,expected);
}

/// <summary>
/// Closed actual-plan binding. Neither public callbacks nor names/hash metadata
/// can choose a publication profile or supply writer/readback/receipt behavior.
/// </summary>
internal sealed class NativeWizardResearchPublicationBinding
{
    private readonly NativeDarkHollowWizardResearchCandidatePlan? _legacy;
    private readonly NativeDarkHollowTwoWizardResearchCandidatePlan? _two;
    private readonly NativeJapaneseEggResearchCandidatePlan? _egg;
    private NativeWizardResearchPublicationBinding(NativeDarkHollowWizardResearchCandidatePlan plan)=>_legacy=plan;
    private NativeWizardResearchPublicationBinding(NativeDarkHollowTwoWizardResearchCandidatePlan plan)=>_two=plan;
    private NativeWizardResearchPublicationBinding(NativeJapaneseEggResearchCandidatePlan plan)=>_egg=plan;
    internal static NativeWizardResearchPublicationBinding FromJapaneseEgg(NativeJapaneseEggResearchCandidatePlan plan)
    {ArgumentNullException.ThrowIfNull(plan);return new(plan);}
    internal string? JapaneseSourceImagePath=>_egg?.JapaneseSourceImagePath;
    internal static NativeWizardResearchPublicationBinding FromLegacy(NativeDarkHollowWizardResearchCandidatePlan plan)
    {ArgumentNullException.ThrowIfNull(plan);return new(plan);}
    internal static NativeWizardResearchPublicationBinding FromTwo(NativeDarkHollowTwoWizardResearchCandidatePlan plan)
    {ArgumentNullException.ThrowIfNull(plan);return new(plan);}
    internal string SourceImagePath=>_egg?.SourceImagePath??_legacy?.SourceImagePath??_two!.SourceImagePath;
    internal string SourceCuePath=>_egg?.SourceCuePath??_legacy?.SourceCuePath??_two!.SourceCuePath;
    internal string PlanSha256=>_egg?.PlanSha256??_legacy?.PlanSha256??_two!.PlanSha256;
    internal string ImageName=>_egg!=null?NativeJapaneseEggResearchCandidatePlan.ImageName:_legacy!=null?NativeDarkHollowWizardResearchCandidatePlan.ImageName:NativeDarkHollowTwoWizardResearchCandidatePlan.ImageName;
    internal string CueName=>_egg!=null?NativeJapaneseEggResearchCandidatePlan.CueName:_legacy!=null?NativeDarkHollowWizardResearchCandidatePlan.CueName:NativeDarkHollowTwoWizardResearchCandidatePlan.CueName;
    internal string ReceiptName=>_egg!=null?NativeJapaneseEggResearchCandidatePlan.ReceiptName:_legacy!=null?NativeDarkHollowWizardResearchCandidatePlan.ReceiptName:NativeDarkHollowTwoWizardResearchCandidatePlan.ReceiptName;
    internal string[] OutputNames=>_egg!=null?NativeJapaneseEggResearchCandidatePlan.OutputNames:_legacy!=null?NativeDarkHollowWizardResearchCandidatePlan.OutputNames:NativeDarkHollowTwoWizardResearchCandidatePlan.OutputNames;
    internal string LeaseName=>_egg!=null?".native-japanese-eggs.writer.lock":_legacy!=null?".native-wizard.writer.lock":".native-two-wizards.writer.lock";
    internal string StagePrefix=>_egg!=null?".native-japanese-eggs-stage-":_legacy!=null?".native-wizard-stage-":".native-two-wizards-stage-";
    internal Task RequireFreshAsync(CancellationToken token=default)=>_egg?.RequireFreshAsync(token)??_legacy?.RequireFreshAsync(token)??_two!.RequireFreshAsync(token);
    internal void RequireStageReversion(){if(_egg!=null)_egg.RequireStageReversion();else if(_legacy!=null)_legacy.RequireStageReversion();else _two!.RequireStageReversion();}
    internal void WriteNewImage(string path,CancellationToken token){if(_egg!=null)_egg.WriteNewImage(path,token);else if(_legacy!=null)_legacy.WriteNewImage(path,token);else _two!.WriteNewImage(path,token);}
    internal (string Hash,int Changed) VerifyImage(string path)=>_egg?.VerifyImage(path)??_legacy?.VerifyImage(path)??_two!.VerifyImage(path);
    internal byte[] BuildReceipt((string Hash,int Changed) result)=>_egg?.BuildReceipt(result)??_legacy?.BuildReceipt(result)??_two!.BuildReceipt(result);
    internal byte[] CopyCueBytes()=>_egg?.CopyCueBytes()??_legacy?.CopyCueBytes()??_two!.CopyCueBytes();
}

internal static class NativeWizardResearchPublicationEngine
{
    public static Task<NativeWizardResearchPublication> PublishAsync(NativeWizardResearchPublicationBinding plan,string directory,CancellationToken token=default)=>
        PublishCoreAsync(plan,directory,NativeWizardResearchFault.None,null,token);
    internal static Task<NativeWizardResearchPublication> PublishForSmokeAsync(NativeWizardResearchPublicationBinding plan,string directory,
        NativeWizardResearchFault fault,Action<NativeWizardResearchFault,string>? checkpoint=null,CancellationToken token=default)=>
        PublishCoreAsync(plan,directory,fault,checkpoint,token);
    public static async Task<NativeWizardResearchPublication> VerifyAsync(NativeWizardResearchPublicationBinding plan,string directory,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(plan);string root=Resolve(plan,directory);
        if(!CompleteSet(plan,root))throw new InvalidDataException("Research candidate set is missing or incomplete.");
        await plan.RequireFreshAsync(token);plan.RequireStageReversion();var result=VerifySet(plan,root);
        await plan.RequireFreshAsync(token);return Result(plan,root,result);
    }
    private static async Task<NativeWizardResearchPublication> PublishCoreAsync(NativeWizardResearchPublicationBinding plan,string directory,
        NativeWizardResearchFault fault,Action<NativeWizardResearchFault,string>? checkpoint,CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(plan);if(!Enum.IsDefined(fault))throw new InvalidDataException("Unknown research fault.");
        string root=Resolve(plan,directory);
        if(CompleteSet(plan,root))return await VerifyAsync(plan,root,token);
        await plan.RequireFreshAsync(token);plan.RequireStageReversion();Directory.CreateDirectory(root);RequireNoLink(root);
        string leasePath=Path.Combine(root,plan.LeaseName);RequireNoLink(leasePath);
        using FileStream lease=new(leasePath,FileMode.CreateNew,FileAccess.Write,FileShare.None);
        string stage=Path.Combine(root,plan.StagePrefix+Guid.NewGuid().ToString("N"));bool preserve=false,published=false;
        try
        {
            if(Directory.EnumerateFileSystemEntries(root).Any(p=>p!=leasePath))throw new IOException("Research output became occupied while acquiring its lease.");
            Directory.CreateDirectory(stage);string image=Path.Combine(stage,plan.ImageName);
            plan.WriteNewImage(image,token);Checkpoint(NativeWizardResearchFault.AfterImageWrite,image);
            var verified=plan.VerifyImage(image);
            WriteNew(Path.Combine(stage,plan.ReceiptName),plan.BuildReceipt(verified));
            WriteNew(Path.Combine(stage,plan.CueName),plan.CopyCueBytes());
            if(VerifySet(plan,stage)!=verified)throw new InvalidDataException("Research staging readback changed.");
            Checkpoint(NativeWizardResearchFault.BeforePublication,image);
            await plan.RequireFreshAsync(token);plan.RequireStageReversion();
            if(VerifySet(plan,stage)!=verified)throw new InvalidDataException("Research staging changed before publication.");
            var publication=ExportArtifactTransaction.PublishNew(root,plan.OutputNames.Select(name=>
                new ExportArtifactReplacement(Path.Combine(root,name),Path.Combine(stage,name))).ToArray(),token);
            published=true;preserve=!string.IsNullOrEmpty(publication.RetainedRecoveryDirectory);
            var final=VerifySet(plan,root);if(final!=verified)throw new InvalidDataException("Research final artifacts differ from verified staging.");
            await plan.RequireFreshAsync(token);return Result(plan,root,final,publication.RetainedRecoveryDirectory);
        }
        catch(ExportArtifactRollbackException){preserve=true;throw;}
        catch(Exception error)when(published)
        {preserve=true;throw new IOException("Research artifacts were published but final verification failed; preserve "+root,error);}
        finally
        {
            // Only this invocation's explicitly named files. Cleanup is best
            // effort and cannot mask a successful publication or original error.
            if(!preserve)
            {
                try
                {
                    foreach(string name in plan.OutputNames)
                    {string path=Path.Combine(stage,name);if(File.Exists(path))File.Delete(path);}
                    if(Directory.Exists(stage)&&!Directory.EnumerateFileSystemEntries(stage).Any())Directory.Delete(stage);
                }
                catch(IOException){preserve=true;}
                catch(UnauthorizedAccessException){preserve=true;}
            }
            lease.Dispose();
            if(!preserve)try{if(File.Exists(leasePath))File.Delete(leasePath);}catch(IOException){}catch(UnauthorizedAccessException){}
        }
        void Checkpoint(NativeWizardResearchFault point,string image)
        {checkpoint?.Invoke(point,image);token.ThrowIfCancellationRequested();if(fault==point)throw new IOException("Deliberate research publication fault: "+point);}
    }
    private static (string Hash,int Changed) VerifySet(NativeWizardResearchPublicationBinding plan,string directory)
    {
        foreach(string name in plan.OutputNames)RequireNoLink(Path.Combine(directory,name));
        var result=plan.VerifyImage(Path.Combine(directory,plan.ImageName));
        RequireCompanionFile(Path.Combine(directory,plan.CueName),plan.CopyCueBytes());
        RequireCompanionFile(Path.Combine(directory,plan.ReceiptName),plan.BuildReceipt(result));
        if(!DiscCueSheet.ReferencesDataImage(Path.Combine(directory,plan.CueName),
            Path.Combine(directory,plan.ImageName)))throw new InvalidDataException("Research output CUE binding changed.");
        return result;
    }
    private static void RequireCompanionFile(string path,byte[] expected)
    {
        using FileStream input=new(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        RequireCompanion(input,expected);
    }
    internal static void RequireCompanion(Stream input,ReadOnlySpan<byte> expected)
    {
        ArgumentNullException.ThrowIfNull(input);
        if(!input.CanRead||!input.CanSeek||input.Length!=expected.Length)
            throw new InvalidDataException("Research companion length or readable owner changed before allocation.");
        input.Position=0;byte[] actual=new byte[expected.Length];input.ReadExactly(actual);
        if(input.Length!=expected.Length||input.Position!=expected.Length||!actual.AsSpan().SequenceEqual(expected))
            throw new InvalidDataException("Research companion changed exact bytes or length during readback.");
    }
    private static NativeWizardResearchPublication Result(NativeWizardResearchPublicationBinding plan,string root,(string Hash,int Changed) verified,string recovery="")=>
        new(root,Path.Combine(root,plan.ImageName),Path.Combine(root,plan.CueName),
            Path.Combine(root,plan.ReceiptName),verified.Hash,plan.PlanSha256,verified.Changed,recovery);
    private static string Resolve(NativeWizardResearchPublicationBinding plan,string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);string root=Path.GetFullPath(directory);
        PersistencePathSafety.EnsureTreesDoNotOverlap(plan.SourceImagePath,root,"Research output overlaps source BIN.");
        PersistencePathSafety.EnsureTreesDoNotOverlap(plan.SourceCuePath,root,"Research output overlaps source CUE.");
        if(plan.JapaneseSourceImagePath is string japanese)
            PersistencePathSafety.EnsureTreesDoNotOverlap(japanese,root,"Research output overlaps Japanese donor BIN.");
        RequireNoLink(root);if(File.Exists(root))throw new InvalidDataException("Research output directory is a file.");return root;
    }
    private static bool CompleteSet(NativeWizardResearchPublicationBinding plan,string root)
    {
        if(!Directory.Exists(root))return false;
        var names=Directory.EnumerateFileSystemEntries(root).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray();
        if(names.Length==0)return false;
        if(!names.SequenceEqual(plan.OutputNames.Order(StringComparer.Ordinal)))
            throw new IOException("Research output is partial, occupied, or retains recovery evidence; it cannot be replaced.");
        foreach(string name in plan.OutputNames)
        {string path=Path.Combine(root,name);RequireNoLink(path);if(!File.Exists(path))throw new InvalidDataException("Research artifact is not a file.");}
        return true;
    }
    private static void RequireNoLink(string path)
    {if(new FileInfo(path).LinkTarget!=null||new DirectoryInfo(path).LinkTarget!=null)throw new InvalidDataException("Research output may not be a symbolic link; trusted ancestors are required.");}
    private static void WriteNew(string path,byte[] bytes)
    {using FileStream output=new(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);output.Write(bytes);output.Flush(true);}
}
