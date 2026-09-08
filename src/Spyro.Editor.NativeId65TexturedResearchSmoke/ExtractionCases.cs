using System.Reflection;
using Spyro.Editor.Core.Exporting;
using static Inspection;

internal static class ExtractionCases
{
    private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
    internal static void Free()
    {
        var binding = Type("NativeId65FreshResearchPublicationBinding");
        var role = binding.GetNestedType("Role", BindingFlags.NonPublic);
        Check(role is { IsNestedPrivate: true, IsAbstract: true }, "Missing closed private binding Role extraction.");
        var fields = binding.GetFields(PrivateInstance);
        Check(fields.Length == 1 && fields[0].IsInitOnly && fields[0].FieldType == role,
            "Facade retains only one actual private readonly Role, not caller optional states.");
        var constructors = binding.GetConstructors(PrivateInstance);
        Check(constructors.Length == 1 && constructors[0].IsPrivate &&
            constructors[0].GetParameters().Select(p => p.ParameterType).SequenceEqual([role!]), "Only private closed Role construction.");
        var adapters = binding.GetNestedTypes(BindingFlags.NonPublic).Where(t => t.BaseType == role).ToArray();
        Check(adapters.Length == 4 && adapters.All(t => t.IsNestedPrivate && t.IsSealed), "Exactly four old private sealed roles; no fifth role.");
        var expected = new[] { typeof(NativeId65FreshDiscResearchCandidatePlan), typeof(NativeId65FreshEditedDocumentResearchPlan),
            typeof(NativeId65FreshSavedDocumentExportContext), typeof(NativeId65FreshWizardResearchCandidatePlan) };
        var owners = adapters.Select(t => t.GetFields(PrivateInstance | BindingFlags.DeclaredOnly)).ToArray();
        Check(owners.All(f => f.Length == 1 && f[0].IsInitOnly) &&
            owners.Select(f => f[0].FieldType.FullName).Order().SequenceEqual(expected.Select(t => t.FullName).Order()),
            "One readonly actual issued owner per role; no delegate/profile authority.");
        var helper = Type("NativeId65FreshDiscStreaming");
        Check(!helper.IsVisible && helper.IsAbstract && helper.IsSealed, "Streaming helper is internal static, not a public writer API.");
        var measure = helper.GetMethod("Measure", BindingFlags.NonPublic | BindingFlags.Static);
        Check(measure is { IsAssembly: true } && measure.ReturnType == typeof(NativeId65FreshDiscVerification) &&
            measure.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] {
                typeof(NativeId65FreshDiscPlan), typeof(Stream), typeof(CancellationToken), typeof(Stream), typeof(Action) }),
            "Exact actual-Disc streaming signature, not caller sector generation.");
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Refuse<OperationCanceledException>(() => Call(null, helper, "Measure", null, null, cancelled.Token, null, null), "Moved helper cancellation precedence.");
        Refuse<ArgumentNullException>(() => Call(null, helper, "Measure", null, null, CancellationToken.None, null, null), "Moved helper null source precedence.");
    }

    internal static void ActualOwner(object binding, object owner)
    {
        object role = binding.GetType().GetFields(PrivateInstance).Single().GetValue(binding)!;
        var field = role.GetType().GetFields(PrivateInstance | BindingFlags.DeclaredOnly).Single();
        Check(ReferenceEquals(field.GetValue(role), owner), "Extracted role retains this exact publicly issued owner.");
    }

    internal static void Source(string bin, NativeId65FreshDiscPlan disc, string expected)
    {
        using var source = File.OpenRead(bin);
        var actual = (NativeId65FreshDiscVerification)Call(null, Type("NativeId65FreshDiscStreaming"), "Measure",
            disc, source, CancellationToken.None, null, null)!;
        Check(actual.OutputSha256 == expected && actual.ChangedSectors == 1253,
            "Direct extracted helper matches independent full edited image; no physical output.");
    }
}
