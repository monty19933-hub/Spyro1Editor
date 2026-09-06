using System.Reflection;
using Spyro.Editor.Core.Exporting;

internal static class Program
{
    private static int _checks;
    internal static void Check(bool value,string why)
    { if(!value)throw new InvalidOperationException(why);_checks++; }
    internal static void Reject(Action action,string why)
    {
        try { action(); }
        catch(InvalidDataException) { _checks++;return; }
        catch(ArgumentException) { _checks++;return; }
        throw new InvalidOperationException("Expected refusal: "+why);
    }
    internal static void RejectData(Action action,string expected,string why)
    {
        try { action(); }
        catch(InvalidDataException e) when(e.Message.Contains(expected,StringComparison.Ordinal)) {_checks++;return;}
        throw new InvalidOperationException("Expected exact data refusal: "+why);
    }
    private static async Task Main(string[] args)
    {
        foreach(var type in new[]{typeof(NativeDarkHollowWizardResourcePlan),typeof(NativeDarkHollowWizardOverlayPlan),typeof(NativeDarkHollowWizardDeathRewardPlan)})
            Check(type.GetConstructors(BindingFlags.Public|BindingFlags.Instance).Length==0,"closed source-issued constructors");
        var method=typeof(NativeDarkHollowWizardResourcePlan).GetMethod("CompileWithAppendPlacementAsync")!;
        Check(method!=null,"named actual-append composition entry point");
        Check(method!.GetParameters().All(p=>p.ParameterType!=typeof(int)&&p.ParameterType!=typeof(string)||p.Name=="path"),
            "no caller count, identity hash or profile-label authority");
        Reject(()=>NativeDarkHollowWizardResourcePlan.CompileWithAppendPlacementAsync("missing-do-not-open.bin",null!,null!,null!,null!,null!,null!,null!).GetAwaiter().GetResult(),"null actual owners before source I/O");
        using(var canceled=new CancellationTokenSource())
        {
            canceled.Cancel();
            try { await NativeDarkHollowWizardResourcePlan.CompileWithAppendPlacementAsync("missing-do-not-open.bin",null!,null!,null!,null!,null!,null!,null!,canceled.Token);throw new InvalidOperationException("Missing pre-I/O cancellation"); }
            catch(OperationCanceledException){Check(true,"source-free cancellation before any source access");}
        }
        Console.WriteLine($"NATIVE_WIZARD_COMPOSITION_FREE_OK checks={_checks}");
        if(args.Length==0)return;
        if(args.Length!=3||args[0]!="--source")throw new ArgumentException("Usage: --source original.bin workspace");
        await SourceCases.Run(args[1],args[2]);
        Console.WriteLine($"NATIVE_WIZARD_COMPOSITION_SOURCE_OK checks={_checks} runtime=false publication=false");
    }
}
