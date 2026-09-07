using System.Reflection;
using Spyro.Editor.Core.Exporting;

internal static class ApiCases
{
    internal static async Task Run()
    {
        var t=typeof(NativeId65FreshWizardResearchExecutablePlan);
        Check(t.IsSealed&&t.GetConstructors().Length==0&&t.GetProperties().All(p=>p.SetMethod is null));
        var methods=t.GetMethods(BindingFlags.Public|BindingFlags.Static|BindingFlags.DeclaredOnly);
        Check(methods.Length==1&&methods[0].Name=="CaptureAsync"&&methods[0].GetParameters().Select(p=>p.ParameterType).SequenceEqual(new[]{
            typeof(string),typeof(NativeId65FreshWizardResearchBodyPlan),typeof(CancellationToken)}));
        Check(!t.GetMethods().Any(m=>new[]{"Publish","Write","CaptureFrom","Compile"}.Any(p=>m.Name.StartsWith(p,StringComparison.Ordinal))));
        using(var cancel=new CancellationTokenSource())
        {cancel.Cancel();await Reject<OperationCanceledException>(()=>NativeId65FreshWizardResearchExecutablePlan.CaptureAsync(null!,null!,cancel.Token));}
        await Reject<ArgumentNullException>(()=>NativeId65FreshWizardResearchExecutablePlan.CaptureAsync("must-not-open.bin",null!));
        Console.WriteLine("FRESH_WIZARD_EXECUTABLE_API_OK checks=5 outputsWritten=0");
    }
    private static void Check(bool ok){if(!ok)throw new InvalidDataException("Closed executable API changed.");}
    private static async Task Reject<T>(Func<Task> action)where T:Exception
    {try{await action();}catch(T){return;}throw new InvalidDataException("Expected "+typeof(T).Name);}
}
