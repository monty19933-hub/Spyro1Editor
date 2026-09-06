using System.Reflection;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;

int checks = 0, refusals = 0;
void Check(bool condition, string label)
{
    if (!condition) throw new InvalidOperationException(label);
    checks++;
}
void Reject(Action action, string label)
{
    try { action(); }
    catch (InvalidDataException) { checks++; refusals++; return; }
    throw new InvalidOperationException("Expected native Wizard append refusal: " + label);
}

foreach (Type type in new[] { typeof(NativeDarkHollowWizardAppendSource), typeof(NativeDarkHollowWizardAppendPlan) })
    Check(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance).Length == 0,
        "Caller metadata cannot construct a source issuer or native append plan: " + type.Name);
Check(typeof(NativeDarkHollowWizardAppendInstance).GetProperties().Select(p => p.Name).SequenceEqual(new[] { "FamilyId" }),
    "The initial append intent is stable identity only, not caller native rows, coordinates, ordinals or certificates.");
string tiny = Path.Combine(Path.GetTempPath(), "spyro-wizard-append-invalid-" + Guid.NewGuid().ToString("N") + ".bin");
try
{
    File.WriteAllBytes(tiny, [0]);
    Reject(() => NativeDarkHollowWizardAppendSource.Capture(tiny, new LevelCatalog([])),
        "Wrong physical source and caller-empty catalog cannot issue an append source.");
}
finally { if (File.Exists(tiny)) File.Delete(tiny); }
Console.WriteLine($"NATIVE_WIZARD_APPEND_SOURCE_FREE_OK checks={checks} refusals={refusals} runtime=false");
if (args.Length != 0)
{
    if (args.Length != 3 || args[0] != "--source")
        throw new ArgumentException("Usage: [--source <original.bin> <workspace-root>]");
    WizardAppendSourceCases.Run(args[1], args[2], Check, Reject);
    Console.WriteLine($"NATIVE_WIZARD_APPEND_SOURCE_OK checks={checks} refusals={refusals} placement=false runtime=false publication=false");
}
