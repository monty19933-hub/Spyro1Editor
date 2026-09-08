// New fixed-a surface: tests precede production; defaults use no source/output.
using System.Reflection;
using Spyro.Editor.Core.Exporting;
using static Inspection;

internal static class CandidateApiCases
{
    internal static async Task Free()
    {
        Type candidate = typeof(NativeId65FreshTexturedResearchCandidatePlan);
        Check(candidate.IsSealed && candidate.GetConstructors().Length == 0,
            "New plan is sealed/private-issued, not neutral caller construction.");
        MethodInfo[] factories = candidate.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
        Check(factories.Length == 1 && factories[0].Name == "CaptureAsync" &&
            factories[0].ReturnType == typeof(Task<NativeId65FreshTexturedResearchCandidatePlan>) &&
            factories[0].GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] {
                typeof(string), typeof(string), typeof(NativeId65FreshDiscPlan), typeof(CancellationToken) }),
            "Exactly one actual-Disc factory; no caller profile/hash/output authority.");
        Check(candidate.GetProperties().All(p => p.SetMethod == null), "No mutable candidate properties.");
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        var nullDisc = NativeId65FreshTexturedResearchCandidatePlan.CaptureAsync("\0", "\0", null!, canceled.Token);
        Check(nullDisc.IsFaulted, "Capture returns a faulted Task, not a synchronous throw.");
        await RefuseAsync<ArgumentNullException>(() => nullDisc);
        foreach (var call in new Func<Task>[] {
            () => NativeId65FreshTexturedResearchCandidatePublisher.PublishAsync(null!, "\0", canceled.Token),
            () => NativeId65FreshTexturedResearchCandidatePublisher.VerifyAsync(null!, "\0", canceled.Token) })
        {
            Task task = call();
            Check(task.IsFaulted, "Thin wrapper null timing stays async and source-free.");
            await RefuseAsync<ArgumentNullException>(() => task);
        }
        Type publisher = typeof(NativeId65FreshTexturedResearchCandidatePublisher);
        var methods = publisher.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
        Check(methods.Select(m => m.Name).Order().SequenceEqual(new[] { "PublishAsync", "VerifyAsync" }) &&
            methods.All(m => m.ReturnType == typeof(Task<NativeId65FreshDiscResearchPublication>) &&
                m.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] {
                    candidate, typeof(string), typeof(CancellationToken) })), "Exactly two typed public wrappers.");
    }

    private static async Task RefuseAsync<T>(Func<Task> action) where T : Exception
    {
        try { await action(); }
        catch (T) { Check(true, "Actual expected " + typeof(T).Name); return; }
        throw new InvalidDataException("Expected " + typeof(T).Name);
    }
}
