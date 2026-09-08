// PRIVATE future test helpers; no Core/source/result factory is introduced.
// The production interface lives in the new Window partial, not the frozen worker.
// Every fixture MUST release in finally and drain the actual UI task before Close.
using Spyro.Editor.App.Views;

internal sealed class FailingMaterialInspectionWork : INativeFreshMaterialInspectionUiWork
{
    internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource Released { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal IOException Primary { get; } = new("Neutral UI worker failure; no native source or preview issued.");
    internal NativeFreshMaterialInspectionRequest? Request { get; private set; }
    internal CancellationToken Token { get; private set; }
    internal int Calls { get; private set; }

    public async Task<NativeFreshMaterialInspectionWork> CaptureAsync(
        NativeFreshMaterialInspectionRequest request, CancellationToken token)
    {
        Calls++; Request = request; Token = token; Entered.TrySetResult();
        await Released.Task; // Intentionally ignores token: cancellation is not drain.
        throw Primary; // Cannot construct any successful issued object.
    }
}

internal sealed class HoldingActualMaterialInspectionWork : INativeFreshMaterialInspectionUiWork
{
    internal TaskCompletionSource Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource Released { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal NativeFreshMaterialInspectionWork? Actual { get; private set; }
    internal int Calls { get; private set; }

    public async Task<NativeFreshMaterialInspectionWork> CaptureAsync(
        NativeFreshMaterialInspectionRequest request, CancellationToken token)
    {
        Calls++;
        try
        {
            Actual = await NativeFreshMaterialInspectionWork.CaptureAsync(request, token);
            Ready.TrySetResult();
        }
        catch (Exception error)
        {
            Ready.TrySetException(error); // A real source failure cannot become a hung Ready gate.
            throw;
        }
        await Released.Task; // Delayed delivery of actual issued result; no source owner remains live here.
        return Actual;       // Deliberately leaves stale/cancelled UI acceptance to the Window's fence.
    }
}

// Returns only a real worker result, but deliberately for a different actual request.
internal sealed class ForeignActualMaterialInspectionWork : INativeFreshMaterialInspectionUiWork
{
    internal NativeFreshMaterialInspectionRequest? Requested { get; private set; }
    internal NativeFreshMaterialInspectionRequest? Reissued { get; private set; }
    internal NativeFreshMaterialInspectionWork? Actual { get; private set; }

    public async Task<NativeFreshMaterialInspectionWork> CaptureAsync(
        NativeFreshMaterialInspectionRequest request, CancellationToken token)
    {
        Requested = request;
        Reissued = NativeFreshMaterialInspectionRequest.Create(
            request.Document, request.Materials, request.OriginalImagePath);
        var actual = await NativeFreshMaterialInspectionWork.CaptureAsync(Reissued, token);
        Actual = actual;
        return actual;
    }
}
