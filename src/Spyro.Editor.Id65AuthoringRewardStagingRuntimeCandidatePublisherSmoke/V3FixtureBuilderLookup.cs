using System.Reflection;
using Spyro.Editor.Core.Exporting;

// Test-harness binding only. Resolving or testing the binding never invokes the
// fixture factory, its entrypoint, a native import, or artifact/profile I/O.
// Assembly.Load reads the compiled dependency; it is not fixture execution.
internal static class V3FixtureBuilderLookup
{
    private const string AssemblyName = "Spyro.Editor.Id65V3RuntimeEvidenceContractSmoke";
    private const string MemberName = "BuildCompleteFixture";
    private const BindingFlags Flags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
    private sealed record ParameterShape(string Name, Type Type, bool Optional, bool HasDefault, object? Default);

    private static readonly ParameterShape[] Expected =
    [
        new("sourceRepositoryRoot", typeof(string), false, false, null),
        new("artifactRepositoryRoot", typeof(string), false, false, null),
        new("pendingPackage", typeof(Id65V3RuntimeEvidencePackage), false, false, null),
        new("observedProfile", typeof(Id65V3RuntimeEvidenceProfile), false, false, null),
        new("reuseFirstProcessFingerprint", typeof(bool), true, true, false),
        new("primaryRecordingStartMilliseconds", typeof(int), true, true, 3500),
        new("exerciseWithinSessionUtcTies", typeof(bool), true, true, false),
        new("runOriginUtcOffsetMilliseconds", typeof(int), true, true, 0),
        new("runOriginMonotonicOffsetMicroseconds", typeof(long), true, true, 0L),
        new("profileBeforeCapturedMilliseconds", typeof(int), true, true, -1000),
        new("processStartedMilliseconds", typeof(int), true, true, 100),
        new("exerciseUnicodeChangedPaths", typeof(bool), true, true, false)
    ];

    internal static MethodInfo Resolve(Assembly assembly)
    {
        Type owner = GetOwner(assembly);
        return Select(owner, owner.GetMethods(Flags));
    }

    private static Type GetOwner(Assembly assembly)
    {
        Require(assembly is not null && assembly.GetName().Name == AssemblyName,
            "The V3 fixture builder must belong to the exact smoke assembly.");
        Type? owner = assembly!.GetType("Program", throwOnError: false, ignoreCase: false);
        Require(owner is not null && owner.IsNotPublic && !owner.IsNested && !owner.ContainsGenericParameters,
            "The exact global V3 Program fixture owner is missing or changed.");
        return owner!;
    }

    private static MethodInfo Select(Type owner, IEnumerable<MethodInfo> methods)
    {
        Require(owner == GetOwner(owner.Assembly) && methods is not null, "Invalid V3 fixture lookup owner or method input.");
        MethodInfo[] snapshot = methods!.Take(257).ToArray();
        Require(snapshot.Length <= 256 && snapshot.All(method => method is not null && method.DeclaringType == owner),
            "V3 fixture lookup exceeds its bound or includes a foreign method owner.");
        MethodInfo[] matches = snapshot.Where(method => method.IsStatic && !method.IsPublic &&
            string.Equals(method.Name, MemberName, StringComparison.Ordinal)).ToArray();
        Require(matches.Length == 1, $"Expected one exact Program.{MemberName} member; found {matches.Length}.");
        MethodInfo builder = matches[0];
        Require(builder.IsPrivate && !builder.ContainsGenericParameters && !builder.IsAbstract &&
            builder.ReturnType.Assembly == owner.Assembly && builder.ReturnType.FullName == "CompleteFixture" &&
            builder.ReturnType.IsNotPublic && builder.ReturnType.IsSealed && !builder.ReturnType.IsNested,
            "The V3 fixture builder visibility, generic shape or global return owner changed.");
        ValidateParameters(Snapshot(builder));
        return builder;
    }

    private static ParameterShape[] Snapshot(MethodInfo method) => method.GetParameters().Select(parameter =>
        new ParameterShape(parameter.Name ?? "", parameter.ParameterType, parameter.IsOptional, parameter.HasDefaultValue,
            parameter.HasDefaultValue ? parameter.DefaultValue : null)).ToArray();

    private static void ValidateParameters(IReadOnlyList<ParameterShape> parameters)
    {
        Require(parameters.Count == Expected.Length, "The V3 fixture builder must retain exactly twelve inputs.");
        for (int index = 0; index < Expected.Length; index++)
            Require(parameters[index] == Expected[index], $"V3 fixture parameter {index + 1} changed name, type, flags or typed default.");
    }

    internal static void RunSourceFree()
    {
        Assembly assembly = Assembly.Load(AssemblyName);
        Type owner = GetOwner(assembly);
        MethodInfo[] methods = owner.GetMethods(Flags);
        MethodInfo builder = Resolve(assembly);
        int positive = 0, refused = 0;
        void Check(bool condition, string message) { Require(condition, message); positive++; }
        void Reject(Action action)
        {
            try { action(); }
            catch (InvalidDataException) { refused++; return; }
            throw new InvalidOperationException("Fixture lookup negative case did not refuse.");
        }

        Check(builder.Name == MemberName && builder.DeclaringType == owner, "Stable exact fixture member identity.");
        Check(Select(owner, methods.Reverse()) == builder, "Lookup is independent of metadata enumeration order.");
        MethodInfo[] ambiguous = methods.Where(method => method.Name.Contains(MemberName, StringComparison.Ordinal)).ToArray();
        Check(ambiguous.Length > 1 && ambiguous.Contains(builder), "The real compiler-generated helpers reproduce the old ambiguous substring set.");
        try { _ = ambiguous.Single(); }
        catch (InvalidOperationException) { refused++; }
        Check(Snapshot(builder).SequenceEqual(Expected), "Actual compiled builder retains every input/default.");

        Reject(() => Select(owner, []));
        Reject(() => Select(owner, [builder, builder]));
        Reject(() => Select(owner, ambiguous.Where(method => method != builder)));
        Reject(() => Select(owner, Enumerable.Repeat(builder, 257)));
        Reject(() => Resolve(typeof(V3FixtureBuilderLookup).Assembly));
        MethodInfo foreign = typeof(string).GetMethods(BindingFlags.Public | BindingFlags.Static).First();
        Reject(() => Select(owner, [builder, foreign]));
        Reject(() => Select(owner, null!));
        Reject(() => ValidateParameters(Expected[..^1]));
        Reject(() => ValidateParameters(Expected.Concat([Expected[0]]).ToArray()));
        for (int index = 0; index < Expected.Length; index++)
        {
            ParameterShape[] wrongName = Expected.ToArray();
            wrongName[index] = wrongName[index] with { Name = "wrong" };
            Reject(() => ValidateParameters(wrongName));
            ParameterShape[] wrongType = Expected.ToArray();
            wrongType[index] = wrongType[index] with { Type = typeof(object) };
            Reject(() => ValidateParameters(wrongType));
            ParameterShape[] wrongFlags = Expected.ToArray();
            wrongFlags[index] = wrongFlags[index] with { Optional = !wrongFlags[index].Optional };
            Reject(() => ValidateParameters(wrongFlags));
            ParameterShape[] wrongDefaultFlag = Expected.ToArray();
            wrongDefaultFlag[index] = wrongDefaultFlag[index] with { HasDefault = !wrongDefaultFlag[index].HasDefault };
            Reject(() => ValidateParameters(wrongDefaultFlag));
        }
        for (int index = 4; index < Expected.Length; index++)
        {
            ParameterShape[] wrongDefault = Expected.ToArray();
            wrongDefault[index] = wrongDefault[index] with { Default = "different typed constant" };
            Reject(() => ValidateParameters(wrongDefault));
        }
        ParameterShape[] wrongLong = Expected.ToArray();
        wrongLong[8] = wrongLong[8] with { Default = 0 }; // Int32 zero is not Int64 zero.
        Reject(() => ValidateParameters(wrongLong));
        Check(Snapshot(builder).SequenceEqual(Expected), "Refusals leave the compiled fixture contract unchanged.");
        Require(positive == 5 && refused == 67, "Complete fixture lookup regression matrix was not executed.");
        Console.WriteLine($"V3_FIXTURE_LOOKUP_SOURCE_FREE_OK positive={positive} refused={refused} legacySubstringMatches={ambiguous.Length} targetInvocations=0");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
}
