using System.Buffers.Binary;

namespace Spyro.Editor.Core.Analysis;

public sealed record NativeId65CompanionParticleRequirement(
    int Selector, int AllocationKind, uint SpawnCaseAddress, uint TickCaseAddress,
    string SecondContextArgument, string ThirdContextArgument, string ObservedCalls);

/// <summary>
/// Exact Town companion spawn/tick schemas only. Allocation kinds are native
/// pool kinds, not model classes, texture descriptors or complete effect closures.
/// </summary>
public sealed class NativeId65CompanionParticleContract
{
    private NativeId65CompanionParticleContract(IEnumerable<NativeId65EngineEvidence> evidence)
    {
        Evidence = Array.AsReadOnly(evidence.ToArray());
    }

    public bool SourceCertified => true;
    public bool TransitiveClosureProven => false;
    public bool PublicationApproved => false;
    public IReadOnlyList<NativeId65EngineEvidence> Evidence { get; }
    public IReadOnlyList<NativeId65CompanionParticleRequirement> Requirements { get; } =
        Array.AsReadOnly(new[]
        {
            new NativeId65CompanionParticleRequirement(0x0C, 2, 0x800892C0, 0x800881E8,
                "a2=class0x78 Moby row; native branch reads rotation+0x20 and position+0x0C",
                "a3=packed color selected by native collection state; not a pointer",
                "800836A0 with count1; native caller and its register setup retained"),
            new NativeId65CompanionParticleRequirement(0x42, 0, 0x8008962C, 0x80088510,
                "a2=three-word world-position vector; spawn converts coordinates by signed >>2",
                "a3=0 at both companion calls; color also depends on g80078BBC and native RNG",
                "80083020 and8008306C each count2 when companion status is at least2")
        });
    public IReadOnlyList<string> PendingRequirements { get; } = Array.AsReadOnly(new[]
    {
        "Shared particle renderer, GPU primitive/texture consumers and transitive helper/state ownership.",
        "Native RNG, collection-color tables, companion auxiliary state and their initialization/reset order.",
        "Complete player-generated selector set and shared particle pool capacity/retirement across reloads.",
        "Separate native visual/audio/runtime verification; exact spawn/tick bytes alone do not close these obligations."
    });

    internal static NativeId65CompanionParticleContract Capture(
        ReadOnlySpan<byte> executable, ReadOnlySpan<byte> overlay)
    {
        if (executable.Length != 0x66000 || overlay.Length != 0xF800 ||
            Hash(executable) != "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998" ||
            Hash(overlay) != "9bc923cc8d27703537b81b01f51fde351e58aabee87d38a8afc630f1031878a5")
            throw new InvalidDataException("Companion particle schemas require complete original Town/EXE source owners.");

        var evidence = new List<NativeId65EngineEvidence>();
        foreach (var binding in new[]
        {
            (Selector: 0x0C, Spawn: 0x800892C0u, Tick: 0x800881E8u),
            (Selector: 0x42, Spawn: 0x8008962Cu, Tick: 0x80088510u)
        })
        {
            foreach (var (table, expected, name) in new[]
            {
                (0x8007ADC4u, binding.Spawn, "spawn"),
                (0x8007AC80u, binding.Tick, "tick")
            })
            {
                uint address = checked(table + (uint)binding.Selector * 4);
                int offset = checked((int)(address - 0x8007AA38));
                if (BinaryPrimitives.ReadUInt32LittleEndian(overlay[offset..]) != expected)
                    throw new InvalidDataException("Companion selector table does not resolve to its exact native consumer.");
                evidence.Add(new($"particle-{binding.Selector:X2}-{name}-table", "WAD",
                    0x118E800 + offset, 4, Hash(overlay.Slice(offset, 4))));
            }
        }
        foreach (var (name, address, length, hash) in new[]
        {
            ("particle-0C-spawn-case", 0x800892C0u, 0x148, "c0966d0467e8b8c34566ed92d9223cc21cd4e568237d2993762fd9ce7490fce4"),
            ("particle-42-spawn-case", 0x8008962Cu, 0x160, "1d0eae0bfccf3e8f5e77092ffc215bab2a1111360ab7b4a12ff10b3034693481"),
            ("particle-0C-tick-case", 0x800881E8u, 0x74, "5de94cabf94c9014ae69e2241e7703d001ccb063c87951e0a4d1011b49f8b5f2"),
            ("particle-42-tick-case", 0x80088510u, 0xCC, "bb95c0689ad0d3bed394478e42c0d18904f85b2fe143a70ec3f4fad5d63cacfe"),
            ("complete-companion-caller-context", 0x80082FB8u, 0xB94, "19c9ba78c6b34584e058e7f268c517422d453e0406906411583d01c585d3a11f"),
            ("complete-particle-spawn-callback", 0x80088F68u, 0xF64, "0f41d76490950c9ee0b2c3be847ff9b3f360b4852ce674e7acc04120c975c931"),
            ("complete-particle-tick-callback", 0x80087E20u, 0x1148, "1279e327bdbf92e03c991d619cf3190f3015bb63493f13855355588c0ba78cdc")
        })
        {
            int offset = checked((int)(address - 0x8007AA38));
            if (Hash(overlay.Slice(offset, length)) != hash)
                throw new InvalidDataException("Companion particle native case or containing callback changed.");
            evidence.Add(new(name, "WAD", 0x118E800 + offset, length, hash));
        }
        return new(evidence);
    }

    private static string Hash(ReadOnlySpan<byte> bytes) => NativeId65EngineProfileDecoder.Hash(bytes);
}
