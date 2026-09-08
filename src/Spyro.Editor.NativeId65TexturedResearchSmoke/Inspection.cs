using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;

// Test-only inspection of real publicly issued objects. No private constructor,
// field assignment, receipt deserialization or fabricated plan authority.
internal static class Inspection
{
    internal static int Checks { get; private set; }
    internal static readonly Assembly Core = typeof(NativeId65FreshDiscPlan).Assembly;
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    internal static Type Type(string name) => Core.GetType("Spyro.Editor.Core.Exporting." + name, throwOnError: true)!;
    internal static PropertyInfo Property(Type type, string name) => type.GetProperty(name, Flags)
        ?? throw new InvalidDataException("Missing lineage/API property: " + type.Name + "." + name);
    internal static T Get<T>(object owner, string name) => (T)Property(owner.GetType(), name).GetValue(owner)!;
    internal static object? Call(object? owner, Type type, string name, params object?[] args)
    {
        MethodInfo[] methods = type.GetMethods(Flags).Where(m => m.Name == name && m.GetParameters().Length == args.Length &&
            m.GetParameters().Select((p, i) => args[i] == null || p.ParameterType.IsInstanceOfType(args[i])).All(v => v)).ToArray();
        if (methods.Length != 1) throw new InvalidDataException("Missing/ambiguous exact inspected method: " + type.Name + "." + name);
        try { return methods[0].Invoke(owner, args); }
        catch (TargetInvocationException e) when (e.InnerException != null)
        { ExceptionDispatchInfo.Capture(e.InnerException).Throw(); throw; }
    }
    internal static object? Call(object owner, string name, params object?[] args) => Call(owner, owner.GetType(), name, args);
    internal static void Check(bool yes, string why)
    { if (!yes) throw new InvalidDataException(why); Checks++; }
    internal static T Refuse<T>(Action action, string why) where T : Exception
    {
        try { action(); }
        catch (T e) { Checks++; return e; }
        throw new InvalidDataException("Expected " + typeof(T).Name + ": " + why);
    }
    internal static async Task<T> RefuseAsync<T>(Func<Task> action, string why) where T : Exception
    {
        try { await action(); }
        catch (T e) { Checks++; return e; }
        throw new InvalidDataException("Expected " + typeof(T).Name + ": " + why);
    }
    internal static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    internal static void Finish(List<Exception> errors)
    {
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new IOException("Baseline primary and all final/close errors.", new AggregateException(errors));
    }
}
