using Spyro.Editor.Core.Exporting;

// Source-free entry-boundary tests: no private plan construction, path lookup,
// output creation or checkpoint execution. These do not establish cancellation
// precedence after a valid issued plan, nor any postcommit outcome.
internal static class PublicationApiTimingCases
{
    private const string ForceFaultMessage = "A staging exercise must force a precommit fault.";

    internal static async Task Run()
    {
        int cases = 0;
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        string?[] outputs = [null, "", " \t", "\0", "publication-api-must-not-touch"];
        CancellationToken[] tokens = [CancellationToken.None, canceled.Token];
        int callbacks = 0;

        // Invocation and awaiting must be separate: a conventional await inside
        // one try/catch accepts a synchronous regression as the same exception.
        foreach (string? output in outputs)
        foreach (CancellationToken token in tokens)
        {
            await ReturnsNullPlanTask(() => NativeId65FreshEditedDocumentResearchPublisher.PublishAsync(null!, output!, token));
            cases++;
            await ReturnsNullPlanTask(() => NativeId65FreshEditedDocumentResearchPublisher.VerifyAsync(null!, output!, token));
            cases++;
            foreach (var fault in new[]
            {
                NativeId65FreshEditedDocumentResearchFault.AfterFirstSector,
                NativeId65FreshEditedDocumentResearchFault.AfterImageWrite,
                NativeId65FreshEditedDocumentResearchFault.BeforePublication,
                (NativeId65FreshEditedDocumentResearchFault)(-1),
                (NativeId65FreshEditedDocumentResearchFault)999
            })
            {
                await ReturnsNullPlanTask(() => NativeId65FreshEditedDocumentResearchPublisher.PublishForSmokeAsync(
                    null!, output!, fault, (_, _) => callbacks++, token));
                cases++;
            }
            ThrowsNoneSynchronously(() => NativeId65FreshEditedDocumentResearchPublisher.PublishForSmokeAsync(
                null!, output!, NativeId65FreshEditedDocumentResearchFault.None, (_, _) => callbacks++, token));
            cases++;
        }
        if (callbacks != 0) throw new InvalidDataException("A rejected entry invoked a writer checkpoint.");
        cases++;

        // Finite harness negatives, never substitutes for source-issued plans.
        // Each must be rejected by the exact expected timing/type discriminator.
        await HarnessRefuses(() => ReturnsNullPlanTask(
            () => throw new ArgumentNullException("plan")), "synchronous");
        await HarnessRefuses(() => ReturnsNullPlanTask(
            () => Task.FromCanceled<NativeId65FreshDiscResearchPublication>(canceled.Token)), "not-faulted-task");
        await HarnessRefuses(() => ReturnsNullPlanTask(
            () => Task.FromResult<NativeId65FreshDiscResearchPublication>(null!)), "not-faulted-task");
        await HarnessRefuses(() => ReturnsNullPlanTask(
            () => Task.FromException<NativeId65FreshDiscResearchPublication>(new InvalidDataException())), "wrong-async-type");
        await HarnessRefuses(() => ReturnsNullPlanTask(
            () => Task.FromException<NativeId65FreshDiscResearchPublication>(new ArgumentNullException("other"))), "wrong-parameter");
        await HarnessRefuses(() =>
        {
            ThrowsNoneSynchronously(() => Task.FromException<NativeId65FreshDiscResearchPublication>(
                new InvalidDataException(ForceFaultMessage)));
            return Task.CompletedTask;
        }, "returned-task");
        await HarnessRefuses(() =>
        {
            ThrowsNoneSynchronously(() => throw new ArgumentNullException("plan"));
            return Task.CompletedTask;
        }, "wrong-sync-type");
        await HarnessRefuses(() =>
        {
            ThrowsNoneSynchronously(() => throw new InvalidDataException("different failure"));
            return Task.CompletedTask;
        }, "wrong-sync-message");
        cases += 8;
        Console.WriteLine($"NATIVE_ID65_FRESH_EDITED_PUBLICATION_API_TIMING_OK cases={cases} negativeControls=8 sourceAccess=false outputCreated=false");
    }

    private static async Task ReturnsNullPlanTask(Func<Task<NativeId65FreshDiscResearchPublication>> invoke)
    {
        Task<NativeId65FreshDiscResearchPublication> task;
        try { task = invoke(); }
        catch (Exception error) { throw new TimingMismatch("synchronous", error); }
        if (task == null || !task.IsCompleted || !task.IsFaulted || task.IsCanceled)
            throw new TimingMismatch("not-faulted-task");
        Exception? failure = null;
        try { await task; }
        catch (Exception error) { failure = error; }
        if (failure?.GetType() != typeof(ArgumentNullException))
            throw new TimingMismatch("wrong-async-type", failure);
        if (((ArgumentNullException)failure).ParamName != "plan")
            throw new TimingMismatch("wrong-parameter", failure);
    }

    private static void ThrowsNoneSynchronously(Func<Task<NativeId65FreshDiscResearchPublication>> invoke)
    {
        Task<NativeId65FreshDiscResearchPublication>? returned = null;
        Exception? failure = null;
        try { returned = invoke(); }
        catch (Exception error) { failure = error; }
        if (failure == null)
        {
            // Observe the deliberate negative's faulted task; it is not awaited
            // because the contract under test requires failure before return.
            _ = returned?.Exception;
            throw new TimingMismatch("returned-task");
        }
        if (failure.GetType() != typeof(InvalidDataException))
            throw new TimingMismatch("wrong-sync-type", failure);
        if (failure.Message != ForceFaultMessage)
            throw new TimingMismatch("wrong-sync-message", failure);
    }

    private static async Task HarnessRefuses(Func<Task> action, string reason)
    {
        try { await action(); }
        catch (TimingMismatch error) when (error.Message == reason) { return; }
        throw new InvalidDataException("Timing harness accepted negative control: " + reason);
    }

    private sealed class TimingMismatch(string reason, Exception? inner = null) : Exception(reason, inner) { }
}
