using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

// Generic diagnostic orchestration only. These pure simulations do not call a
// publisher, create a publication result, or manufacture a committed exception.
internal static class PublicationFailureCases
{
    internal static async Task<T> RunWithFinalCheck<T>(Func<Task<T>> action, Func<Task> finalCheck)
    {
        // Invalid delegates refuse before either action can execute.
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(finalCheck);
        T? value = default;
        ExceptionDispatchInfo? primaryFailure = null, finalFailure = null;
        try { value = await action().ConfigureAwait(false); }
        catch (Exception error) { primaryFailure = ExceptionDispatchInfo.Capture(error); }

        // No caller cancellation token is inherited or checked here. A canceled
        // action still runs finalCheck; a failure in finalCheck is also retained.
        try { await finalCheck().ConfigureAwait(false); }
        catch (Exception error) { finalFailure = ExceptionDispatchInfo.Capture(error); }

        if (primaryFailure != null && finalFailure != null)
            throw new AggregateException("Operation and final verification both failed.",
                primaryFailure.SourceException, finalFailure.SourceException);
        primaryFailure?.Throw();
        finalFailure?.Throw();
        return value!;
    }

    internal static async Task Run()
    {
        int checks = 0;
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        // Primary: success, synchronous failure, asynchronous failure, actual
        // pre-canceled Task, asynchronous cancellation. Final: success, sync or
        // async failure, async cancellation. Every combination is pure/no-I/O.
        for (int primaryKind = 0; primaryKind < 5; primaryKind++)
        for (int finalKind = 0; finalKind < 4; finalKind++)
        {
            List<string> events = [];
            Exception? primary = primaryKind switch
            {
                0 or 3 => null,
                4 => new OperationCanceledException("simulated asynchronous primary cancellation", cancelled.Token),
                _ => new IOException("simulated primary failure")
            };
            Exception? final = finalKind switch
            {
                0 => null,
                3 => new OperationCanceledException("simulated asynchronous final cancellation", cancelled.Token),
                _ => new InvalidDataException("simulated final failure")
            };
            int value = -1;
            Exception? observed = null;
            Task<int> operation = RunWithFinalCheck(Primary, Final);
            try { value = await operation; }
            catch (Exception error) { observed = error; }

            Check(events.SequenceEqual(new[] { "primary", "final" }), "Final check runs exactly once after the primary, including cancellation.");
            if (primaryKind == 3)
                Check(primary is OperationCanceledException canceled && canceled.CancellationToken == cancelled.Token,
                    "Pre-canceled Task supplies its real observed cancellation object and token.");

            if (primary == null && final == null)
                Check(observed == null && value == 42 && operation.IsCompletedSuccessfully, "Successful action value is returned only after successful final check.");
            else if (primary != null && final != null)
            {
                Check(observed is AggregateException aggregate && aggregate.InnerExceptions.Count == 2 &&
                    ReferenceEquals(aggregate.InnerExceptions[0], primary) && ReferenceEquals(aggregate.InnerExceptions[1], final),
                    "Dual failure retains exactly the same primary/final objects in order, without flattening or substitution.");
                Check(operation.IsFaulted && !operation.IsCanceled && value == -1, "Combined failure is a fault, never canceled or successful return.");
            }
            else
            {
                Exception expected = primary ?? final!;
                Check(ReferenceEquals(observed, expected) && value == -1, "A single failure is rethrown as the same original object, without a wrapper/value.");
                Check(expected is OperationCanceledException ? operation.IsCanceled : operation.IsFaulted,
                    "Single cancellation/fault retains its natural Task state after final verification.");
            }
            if (primaryKind == 1)
                Check(primary!.StackTrace?.Contains(nameof(ThrowPrimarySynchronously), StringComparison.Ordinal) == true,
                    "Primary synchronous original throw-site survives exception dispatch preservation.");
            if (primaryKind is 2 or 4)
                Check(primary!.StackTrace?.Contains(nameof(ThrowPrimaryAsynchronously), StringComparison.Ordinal) == true,
                    "Primary asynchronous original throw-site survives exception dispatch preservation.");
            if (finalKind == 1)
                Check(final!.StackTrace?.Contains(nameof(ThrowFinalSynchronously), StringComparison.Ordinal) == true,
                    "Final synchronous original throw-site survives exception dispatch preservation.");
            if (finalKind is 2 or 3)
                Check(final!.StackTrace?.Contains(nameof(ThrowFinalAsynchronously), StringComparison.Ordinal) == true,
                    "Final asynchronous original throw-site survives exception dispatch preservation.");

            Task<int> Primary()
            {
                events.Add("primary");
                return primaryKind switch
                {
                    0 => Task.FromResult(42),
                    1 => ThrowPrimarySynchronously(primary!),
                    3 => ObservePreCanceledTask(),
                    _ => ThrowPrimaryAsynchronously(primary!)
                };
            }
            Task Final()
            {
                events.Add("final");
                return finalKind switch
                {
                    0 => Task.CompletedTask,
                    1 => ThrowFinalSynchronously(final!),
                    _ => ThrowFinalAsynchronously(final!)
                };
            }
            async Task<int> ObservePreCanceledTask()
            {
                try { return await Task.FromCanceled<int>(cancelled.Token); }
                catch (Exception error) { primary = error; throw; }
            }
        }

        int actionCalls = 0, finalCalls = 0;
        await NullGuard(() => RunWithFinalCheck<int>(null!, () => { finalCalls++; return Task.CompletedTask; }), "action");
        await NullGuard(() => RunWithFinalCheck(() => { actionCalls++; return Task.FromResult(1); }, null!), "finalCheck");
        await NullGuard(() => RunWithFinalCheck<int>(null!, null!), "action");
        Check(actionCalls == 0 && finalCalls == 0, "Both delegates are validated before any action/final invocation.");

        // This nests the same helper used for independent BIN and CUE checks.
        // Labels describe simulated checks only; there are no filesystem reads.
        var primaryError = new IOException("simulated operation failure");
        var binError = new InvalidDataException("simulated BIN identity failure");
        var cueError = new IOException("simulated CUE identity failure");
        List<string> nestedEvents = [];
        Exception? nested = null;
        try
        {
            await RunWithFinalCheck<int>(
                () => { nestedEvents.Add("operation"); return ThrowPrimarySynchronously(primaryError); },
                async () =>
                {
                    _ = await RunWithFinalCheck<int>(
                        () => { nestedEvents.Add("BIN"); return ThrowPrimarySynchronously(binError); },
                        () => { nestedEvents.Add("CUE"); return ThrowFinalSynchronously(cueError); });
                });
        }
        catch (Exception error) { nested = error; }
        Check(nestedEvents.SequenceEqual(new[] { "operation", "BIN", "CUE" }), "Both independent final checks are attempted after primary failure; no boolean short-circuit.");
        Check(nested is AggregateException outer && outer.InnerExceptions.Count == 2 &&
            ReferenceEquals(outer.InnerExceptions[0], primaryError) && outer.InnerExceptions[1] is AggregateException inner &&
            inner.InnerExceptions.Count == 2 && ReferenceEquals(inner.InnerExceptions[0], binError) && ReferenceEquals(inner.InnerExceptions[1], cueError),
            "Nested failures retain every original object and ownership order, rather than replacing primary or BIN evidence.");

        Console.WriteLine($"FRESH_WIZARD_PUBLICATION_FAILURE_HELPER_OK checks={checks} simulationsOnly=true sourceOrOutputIo=0");
        void Check(bool value, string message)
        {
            if (!value) throw new InvalidDataException(message);
            checks++;
        }
        async Task NullGuard(Func<Task<int>> invoke, string parameter)
        {
            try { _ = await invoke(); }
            catch (ArgumentNullException error) when (error.ParamName == parameter) { checks++; return; }
            throw new InvalidDataException("Expected null delegate refusal before actions: " + parameter);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Task<int> ThrowPrimarySynchronously(Exception error) => throw error;

    private static async Task<int> ThrowPrimaryAsynchronously(Exception error)
    {
        await Task.Yield();
        throw error;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Task ThrowFinalSynchronously(Exception error) => throw error;

    private static async Task ThrowFinalAsynchronously(Exception error)
    {
        await Task.Yield();
        throw error;
    }
}
