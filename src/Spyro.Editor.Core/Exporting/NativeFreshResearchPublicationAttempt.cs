namespace Spyro.Editor.Core.Exporting;

internal enum NativeFreshResearchPublicationState { NotInvoked, RollbackIncomplete, Unconfirmed, Committed }

// Neutral mechanics, not source/recipe authority. State can change only around
// this one real unchanged transaction call; failure accounting cannot change it.
internal sealed class NativeFreshResearchPublicationAttempt
{
    private readonly List<Exception> _failures = [];
    internal NativeFreshResearchPublicationState State { get; private set; }
    internal Exception? Failure => _failures.Count switch
    {
        0 => null,
        1 => _failures[0],
        _ => new AggregateException("Fresh research publication failed with additional observation or cleanup failures.", _failures)
    };

    internal ExportArtifactPublicationResult Invoke(string root, IReadOnlyList<ExportArtifactReplacement> artifacts, CancellationToken token)
    {
        if (State != NativeFreshResearchPublicationState.NotInvoked)
            throw new InvalidOperationException("A publication attempt cannot be repeated.");
        token.ThrowIfCancellationRequested(); // No actual call has happened yet.
        State = NativeFreshResearchPublicationState.Unconfirmed;
        try
        {
            var result = ExportArtifactTransaction.PublishNew(root, artifacts, token);
            State = NativeFreshResearchPublicationState.Committed;
            return result;
        }
        catch (ExportArtifactRollbackException)
        {
            State = NativeFreshResearchPublicationState.RollbackIncomplete;
            throw;
        }
        // Every other exception, including OCE, escaped the actual invocation.
        // No file contents or later callback can replace the missing outcome.
    }

    internal void RecordFailure(Exception error)
    { ArgumentNullException.ThrowIfNull(error); _failures.Add(error); }

    internal bool Capture(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        try { action(); return true; } catch (Exception error) { RecordFailure(error); return false; }
    }

    internal async Task CaptureAsync(Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        try { await action(); } catch (Exception error) { RecordFailure(error); }
    }
}
