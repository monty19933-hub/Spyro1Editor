using System.Reflection;
using Spyro.Editor.Core.Exporting;

internal static class ApiCases
{
    internal static async Task Run()
    {
        Type t=typeof(NativeId65FreshWizardResearchDiscPlan);
        Require(t.IsSealed&&t.GetConstructors().Length==0,"Only the closed actual issuer constructs plans.");
        var factories=t.GetMethods(BindingFlags.Public|BindingFlags.Static|BindingFlags.DeclaredOnly);
        Require(factories.Length==1&&factories[0].Name=="CaptureAsync"&&factories[0].GetParameters().Select(p=>p.ParameterType)
            .SequenceEqual(new[]{typeof(string),typeof(NativeId65FreshWizardResearchExecutablePlan),typeof(CancellationToken)}),"Only original path + actual EXE + token; no bytes/hash/track/output policy.");
        Require(!t.GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly)
            .Any(m=>m.Name.Contains("Write",StringComparison.Ordinal)||m.Name.Contains("Publish",StringComparison.Ordinal)&&!m.Name.StartsWith("get_",StringComparison.Ordinal)),"No writer or publication method.");
        Require(t.GetProperties().All(p=>p.SetMethod?.IsPublic!=true),"No mutable public authority.");
        using var cancelled=new CancellationTokenSource();cancelled.Cancel();
        try{await NativeId65FreshWizardResearchDiscPlan.CaptureAsync("must-not-open.bin",null!,cancelled.Token);throw new Exception("Missing cancellation refusal.");}
        catch(OperationCanceledException){}
        try{await NativeId65FreshWizardResearchDiscPlan.CaptureAsync("must-not-open.bin",null!);throw new Exception("Missing null refusal.");}
        catch(ArgumentNullException){}
        Console.WriteLine("FRESH_WIZARD_DISC_API_OK checks=6 outputsWritten=0");
    }
    private static void Require(bool ok,string why){if(!ok)throw new InvalidDataException(why);}
}
