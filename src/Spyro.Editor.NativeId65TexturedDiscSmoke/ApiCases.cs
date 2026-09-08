using System.Reflection;
using Spyro.Editor.Core.Exporting;

internal static class ApiCases
{
    internal const string Binding = "native-id65-fresh-textured-normal-disc-direct-lq-source-only-usa-v1";
    internal static void Free(Action<bool, string> check)
    {
        var type = typeof(NativeId65FreshDiscPlan);
        check(type.IsSealed && type.GetConstructors().Length == 0, "Disc remains a sealed, privately issued capability.");
        var factories = type.GetMethods(BindingFlags.Public | BindingFlags.Static).OrderBy(m => m.Name).ToArray();
        check(factories.Select(m => m.Name).SequenceEqual(new[] { "CaptureAsync", "CaptureTexturedAsync" }),
            "Exactly the two named factories exist, without bytes/profile/hash overloads.");
        foreach (var factory in factories)
            check(factory.ReturnType == typeof(Task<NativeId65FreshDiscPlan>) && factory.GetParameters()
                .Select(p => p.ParameterType).SequenceEqual(new[] { typeof(string), typeof(NativeId65NormalBodyPlan), typeof(int), typeof(CancellationToken) }) &&
                factory.GetParameters()[3].HasDefaultValue, "Exact original/new factory signatures and optional token.");
        check(type.GetProperty(nameof(NativeId65FreshDiscPlan.BindingProfileId))!.PropertyType == typeof(string) &&
            type.GetProperty(nameof(NativeId65FreshDiscPlan.BindingProfileId))!.SetMethod == null,
            "Instance profile is issued read-only metadata, not caller authority.");
        check(NativeId65FreshDiscPlan.ProfileId == "native-id65-fresh-normal-disc-LAB-source-only-usa-v1" &&
            NativeId65FreshDiscPlan.AcceptedBodyResourceProfile == "fresh-untextured-G3-two-triangle-data-8C8-v1",
            "Old static identifiers remain byte-for-byte unchanged.");
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        foreach (bool textured in new[] { false, true })
        foreach (var token in new[] { CancellationToken.None, cancelled.Token })
        {
            Task<NativeId65FreshDiscPlan>? pending = null;
            try
            {
                pending = textured ? NativeId65FreshDiscPlan.CaptureTexturedAsync("must-not-open.bin", null!, 19, token)
                    : NativeId65FreshDiscPlan.CaptureAsync("must-not-open.bin", null!, 19, token);
            }
            catch (Exception e) { throw new InvalidDataException("Null input must fault the Task, not throw at invocation.", e); }
            check(pending != null, "Async call returns its task before validation is observed.");
            try { pending!.GetAwaiter().GetResult(); throw new InvalidDataException("Null body accepted."); }
            catch (ArgumentNullException e) { check(e.ParamName == "issuedBody", "Null body precedes cancellation and path I/O in both factories."); }
        }
    }
}
