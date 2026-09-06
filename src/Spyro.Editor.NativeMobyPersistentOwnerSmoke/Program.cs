using Spyro.Editor.Core.Scene;

// Tests-first project, with retained missing-API RED before its implementation.
// Full-runner registration is coordinated separately with the root gate owner.
int checks = 0, refusals = 0;
void Check(bool condition, string label)
{
    if (!condition) throw new InvalidOperationException(label);
    checks++;
}
void Reject(Action action, string label)
{
    try { action(); }
    catch (InvalidDataException) { refusals++; checks++; return; }
    throw new InvalidOperationException("Expected owner-ledger refusal: " + label);
}

OwnerLedgerCases.Run(Check, Reject);
Console.WriteLine($"NATIVE_MOBY_PERSISTENT_OWNER_SOURCE_FREE_OK checks={checks} refusals={refusals} runtime=false");
if (args.Length != 0)
{
    if (args.Length != 3 || args[0] != "--source")
        throw new ArgumentException("Usage: [--source <original.bin> <workspace-root>]");
    OwnerLedgerSourceCases.Run(args[1], args[2], Check, Reject);
    Console.WriteLine($"NATIVE_MOBY_PERSISTENT_OWNER_SOURCE_OK checks={checks} refusals={refusals} runtime=false nativeEmission=false");
}
