using System.Reflection;
using Spyro.Editor.Core.Exporting;

internal static class ApiTimingCases
{
    internal static int Checks { get; private set; }

    internal static async Task Run()
    {
        var methods = typeof(NativeId65FreshDocumentResearchPublisher)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
        Check(methods.Select(m => m.Name).Order().SequenceEqual(new[] { "PublishAsync", "VerifyAsync" }),
            "only explicit generic publication and verification APIs");
        foreach (var method in methods)
            Check(method.ReturnType == typeof(Task<NativeId65FreshDiscResearchPublication>) &&
                method.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[]
                { typeof(NativeId65FreshSavedDocumentExportContext), typeof(string), typeof(CancellationToken) }),
                "actual saved context required; no bare Plan, delegate, hash or approval overload");

        Type committed = typeof(NativeId65FreshDocumentResearchCommittedException);
        Check(committed.IsSealed && committed.IsSubclassOf(typeof(IOException)) && committed.GetConstructors().Length == 0,
            "known commit failure cannot be publicly manufactured or inherited");
        var properties = committed.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Check(properties.All(p => p.PropertyType == typeof(string) && p.SetMethod == null) &&
            properties.Select(p => p.Name).Order().SequenceEqual(new[]
            {
                "CuePath", "ImagePath", "OutputDirectory", "PlanSha256", "ReceiptPath",
                "RetainedOutputLeasePath", "RetainedProjectLeasePath", "RetainedRecoveryDirectory", "RetainedStageDirectory"
            }), "failure has exact artifact/retained owner paths, not a successful readback result or caller flags");

        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        foreach (CancellationToken token in new[] { CancellationToken.None, cancel.Token })
        foreach (string? directory in new[] { null, "", " ", "\0", "missing-output" })
        {
            await FaultedAtAwait(() => NativeId65FreshDocumentResearchPublisher.PublishAsync(null!, directory!, token));
            await FaultedAtAwait(() => NativeId65FreshDocumentResearchPublisher.VerifyAsync(null!, directory!, token));
        }
        // Deliberate finite harness controls: a synchronous exception, successful
        // task, cancellation and wrong parameter are not valid null-context behavior.
        await RejectHarness(() => FaultedAtAwait(() => throw new ArgumentNullException("context")));
        await RejectHarness(() => FaultedAtAwait(() => Task.FromResult<NativeId65FreshDiscResearchPublication>(null!)));
        await RejectHarness(() => FaultedAtAwait(() => Task.FromCanceled<NativeId65FreshDiscResearchPublication>(cancel.Token)));
        await RejectHarness(() => FaultedAtAwait(() => Task.FromException<NativeId65FreshDiscResearchPublication>(new ArgumentNullException("plan"))));
        foreach (var token in new[] { CancellationToken.None, cancel.Token })
        {
            Task<NativeId65FreshDiscResearchPublication> task;
            try
            {
                task = NativeId65FreshResearchPublicationEngine.PublishDocumentForSmokeAsync(null!, "missing-output",
                    NativeId65FreshDocumentResearchFault.None, null, token);
            }
            catch (Exception error) { throw new InvalidDataException("engine None guard must fault its Task, not invocation", error); }
            Check(task.IsCompleted && task.IsFaulted, "engine forced path refuses None before any plan/token/IO authority");
            bool refused = false;
            try { await task; }
            catch (InvalidDataException error) when (error.Message.Contains("force a precommit fault", StringComparison.Ordinal)) { refused = true; }
            Check(refused, "internal forced path cannot silently enter normal publication with None");
        }
        Console.WriteLine($"NATIVE_ID65_FRESH_DOCUMENT_PUBLICATION_FREE_OK checks={Checks} outputWritten=false");
    }

    private static async Task FaultedAtAwait(Func<Task<NativeId65FreshDiscResearchPublication>> invoke)
    {
        Task<NativeId65FreshDiscResearchPublication> task;
        try { task = invoke(); }
        catch (Exception error) { throw new InvalidDataException("public invocation threw synchronously", error); }
        Check(task.IsCompleted && task.IsFaulted && !task.IsCanceled, "null context produces an immediately faulted Task");
        try { await task; }
        catch (ArgumentNullException error) when (error.ParamName == "context" && error.GetType() == typeof(ArgumentNullException))
        { Checks++; return; }
        catch (Exception error) { throw new InvalidDataException("wrong await exception or parameter", error); }
        throw new InvalidDataException("null context unexpectedly succeeded");
    }

    private static async Task RejectHarness(Func<Task> action)
    {
        try { await action(); }
        catch (InvalidDataException) { Checks++; return; }
        throw new InvalidDataException("finite broken harness fixture was accepted");
    }

    private static void Check(bool condition, string label)
    { if (!condition) throw new InvalidDataException(label); Checks++; }
}
