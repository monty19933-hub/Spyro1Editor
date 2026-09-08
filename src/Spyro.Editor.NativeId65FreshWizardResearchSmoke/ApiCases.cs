using System.Reflection;
using Spyro.Editor.Core.Exporting;

internal static class ApiCases
{
    internal static async Task Run()
    {
        Type p=typeof(NativeId65FreshWizardResearchCandidatePlan),e=typeof(NativeId65FreshWizardResearchCommittedException);
        Require(p.IsSealed&&p.GetConstructors().Length==0&&p.GetProperties().All(x=>x.SetMethod?.IsPublic!=true),"Closed immutable source issuer.");
        var factory=p.GetMethods(BindingFlags.Public|BindingFlags.Static|BindingFlags.DeclaredOnly);
        Require(factory.Length==2&&factory.Single(x=>x.Name=="CaptureAsync").GetParameters().Select(x=>x.ParameterType).SequenceEqual(new[]{typeof(string),typeof(string),typeof(NativeId65FreshWizardResearchDiscPlan),typeof(CancellationToken)})&&
            factory.Single(x=>x.Name=="CaptureSeparatedAsync").GetParameters().Select(x=>x.ParameterType).SequenceEqual(new[]{typeof(string),typeof(string),typeof(NativeId65FreshWizardResearchDiscPlan),typeof(NativeFreshInitialSpawnClearancePlan),typeof(CancellationToken)}),
            "Exactly old actual Disc factory and separate actual Disc/clearance factory; no caller hash/profile/approval.");
        Require(e.IsSealed&&e.GetConstructors().Length==0&&typeof(IOException).IsAssignableFrom(e)&&e.GetProperties(BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly).All(x=>x.SetMethod?.IsPublic!=true),"Known-commit failure cannot be publicly manufactured or edited.");
        using var cancel=new CancellationTokenSource();cancel.Cancel();
        await Refuse<OperationCanceledException>(()=>NativeId65FreshWizardResearchCandidatePlan.CaptureAsync("must-not-open.bin","must-not-open.cue",null!,cancel.Token));
        await Refuse<ArgumentNullException>(()=>NativeId65FreshWizardResearchCandidatePlan.CaptureAsync("must-not-open.bin","must-not-open.cue",null!));
        foreach(var token in new[]{CancellationToken.None,cancel.Token})
        {
            await NullTask(()=>NativeId65FreshWizardResearchCandidatePublisher.PublishAsync(null!,"must-not-create",token));
            await NullTask(()=>NativeId65FreshWizardResearchCandidatePublisher.VerifyAsync(null!,"must-not-create",token));
        }
        Console.WriteLine("FRESH_WIZARD_RESEARCH_API_OK checks=9 outputsWritten=0");
    }
    private static async Task NullTask(Func<Task<NativeId65FreshDiscResearchPublication>> invoke)
    {
        Task<NativeId65FreshDiscResearchPublication> task;
        try{task=invoke();}catch(Exception error){throw new Exception("Public publisher must return a faulted Task for null plan.",error);}
        Require(task.IsFaulted&&!task.IsCanceled,"Null plan faulted Task, not cancellation/success.");
        try{await task;}catch(ArgumentNullException error)when(error.ParamName=="plan"){return;}
        throw new Exception("Expected plan ArgumentNullException.");
    }
    private static async Task Refuse<T>(Func<Task> action)where T:Exception{try{await action();}catch(T){return;}throw new Exception("Expected "+typeof(T).Name);}
    private static void Require(bool ok,string why){if(!ok)throw new InvalidDataException(why);}
}
