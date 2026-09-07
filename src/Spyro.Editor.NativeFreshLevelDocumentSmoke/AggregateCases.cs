using System.Collections;
using System.Reflection;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Music;
using static Cases;

internal static class AggregateCases
{
    internal static void Run()
    {
        Check(typeof(NativeFreshLevelDocumentIntent).IsSealed && typeof(NativeFreshWizardRouteIntent).IsSealed,
            "aggregate and sole concrete variant are sealed");
        foreach (Type type in new[] { typeof(NativeFreshLevelDocumentIntent), typeof(NativeFreshPlacementIntent), typeof(NativeFreshWizardRouteIntent) })
        {
            Check(type.GetConstructors().Length == 0, "no public unchecked constructors");
            Check(type.GetProperties().All(p => p.SetMethod == null), "all public properties readonly");
            Check(type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .All(m => m.Name == "Create"), "neutral factory only, no source/file/compiler API");
        }
        Check(typeof(NativeFreshPlacementIntent).IsAbstract &&
            typeof(NativeFreshPlacementIntent).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
                .All(c => !c.IsFamily && !c.IsFamilyOrAssembly), "union cannot be externally subclassed");
        Check(typeof(NativeFreshLevelDocumentIntent).GetProperties().Select(p => p.Name).Order()
            .SequenceEqual(new[] { "Base", "Placements" }), "aggregate owns only neutral base and ordered placements");
        Check(typeof(NativeFreshWizardRouteIntent).GetProperties().Select(p => p.Name).Order()
            .SequenceEqual(new[] { "EditorId", "EndX", "EndY", "OriginX", "OriginY", "OriginZ", "Tag" }),
            "variant contains no native/save/pool/payload/approval fields");
        Check(typeof(NativeFreshLevelDocumentCodec).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Select(m => m.Name).Order().SequenceEqual(new[] { "Parse", "SerializeCanonical" }), "codec has no IO or source authority");

        var floor = Base(); var wizard = Wizard();
        NativeFreshPlacementIntent[] array = [wizard];
        var document = NativeFreshLevelDocumentIntent.Create(floor, array);
        Check(ReferenceEquals(floor, document.Base) && ReferenceEquals(wizard, document.Placements[0]), "actual immutable input objects retained");
        array[0] = Wizard(Id2);
        Check(Serialize(document) == Canonical, "caller array does not alias collection");
        var mutable = new List<NativeFreshPlacementIntent> { wizard };
        var copied = NativeFreshLevelDocumentIntent.Create(floor, mutable); mutable.Clear();
        Check(Serialize(copied) == Canonical, "caller list edits do not mutate aggregate");
        Check(document.Placements is not NativeFreshPlacementIntent[], "no exposed backing array");
        if (document.Placements is IList<NativeFreshPlacementIntent> list)
        {
            Check(list.IsReadOnly, "collection explicitly read-only");
            bool refused = false; try { list[0] = Wizard(Id2); } catch (NotSupportedException) { refused = true; }
            Check(refused && Serialize(document) == Canonical, "cast cannot mutate retained collection");
        }
        var once = new OnePass([Wizard(), Wizard(Id2)]);
        Check(NativeFreshLevelDocumentIntent.Create(floor, once).Placements.Count == 2 && once.Starts == 1,
            "caller enumeration consumed exactly once");
        var tooMany = new BoundedOverflow();
        Reject(() => NativeFreshLevelDocumentIntent.Create(floor, tooMany), "129th item refusal");
        Check(tooMany.Moves == 129 && tooMany.Disposed, "no unbounded ToArray or consumption beyond129th; enumerator disposed");
        Reject(() => NativeFreshLevelDocumentIntent.Create(null!, []), "null base");
        Reject(() => NativeFreshLevelDocumentIntent.Create(floor, null!), "null placements");
        Reject(() => NativeFreshLevelDocumentIntent.Create(floor, [null!]), "null placement");

        Check(Document().Placements.Count == 0, "zero-placement aggregate valid but not legacy export authority");
        var maximum = Enumerable.Range(1, 128).Select(i => Wizard(i.ToString("x32"))).ToArray();
        Check(Document(maximum).Placements.Count == 128, "128 authoring rows, no native-capacity assertion");
        Check(Parse(Serialize(Document(maximum))).Placements.Select(p => p.EditorId).SequenceEqual(maximum.Select(p => p.EditorId)),
            "maximum count retains exact authored IDs and order");
        Reject(() => Document(Wizard(), Wizard(Id2), Wizard()), "duplicate ID separated by other row");
        Reject(() => Document(Wizard(Id2), Wizard(), Wizard(Id2)), "duplicate still refused after reorder");
        var ordered = Document(Wizard(), Wizard(Id2)); var reversed = Document(Wizard(Id2), Wizard());
        Check(Serialize(ordered) != Serialize(reversed) && Parse(Serialize(reversed)).Placements.Select(p => p.EditorId)
            .SequenceEqual(new[] { Id2, Id }), "order neither sorted nor regenerated by parse/serialize");
        var changedId = Document(Wizard(Id2));
        Check(Serialize(changedId) == Canonical.Replace(Id, Id2), "ID-only edit changes only canonical ID");
        Check(NativeId65FreshDocumentCodec.SerializeCanonical(changedId.Base).SequenceEqual(NativeId65FreshDocumentCodec.SerializeCanonical(floor)),
            "ID/list changes preserve entire old base canonical bytes");
        var alternateBase = NativeId65FreshDocumentIntent.Create(8192, 8192, 4096, 2048, 2048, 9000, 9001, 13,
            new(1, 2, 3), new(250, 251, 252), 47);
        var alternate = NativeFreshLevelDocumentIntent.Create(alternateBase, [wizard]);
        Check(ReferenceEquals(alternateBase, alternate.Base) &&
            NativeId65FreshDocumentCodec.SerializeCanonical(Parse(Serialize(alternate)).Base)
                .SequenceEqual(NativeId65FreshDocumentCodec.SerializeCanonical(alternateBase)),
            "every non-default base geometry/spawn/color/music field survives, no fixed recipe substitution");
        var narrowerBase = NativeId65FreshDocumentIntent.Create(8704, 8704, 8704, 768, 768, 8960, 8960, 64,
            new(80, 128, 112), new(128, 96, 64), 19);
        Reject(() => NativeFreshLevelDocumentIntent.Create(narrowerBase, [wizard]), "containment uses actual changed base, not fixed A");
        foreach (string? id in new[] { null, "", " ", Id[..31], Id + "0", Id.ToUpperInvariant(), new string('0', 32), new string('g', 32),
                     "45db7a08-fe70-4970-9a4d-0b9dc59aca51", " " + Id, Id + "\n", Id[..31] + "é", Id[..31] + "\0" })
            Reject(() => Wizard(id!), "exact lower32hex nonzero ID");
        Check(Wizard(new string('f', 32)).EditorId == new string('f', 32), "maximum editor ID not converted to native integer");

        foreach (int z in new[] { 0, 1, 8704, 8909, 9216, 262128 })
            Check(((NativeFreshWizardRouteIntent)Parse(Serialize(Document(Wizard(z: z)))).Placements[0]).OriginZ == z,
                "neutral integer Z survives without grid or fixed-A height policy");
        foreach (Action invalid in new Action[] { () => Wizard(x: -16), () => Wizard(y: -16), () => Wizard(z: -1),
            () => Wizard(ex: -16), () => Wizard(ey: -16), () => Wizard(x: 262144), () => Wizard(y: 262144), () => Wizard(z: 262129),
            () => Wizard(ex: 262144), () => Wizard(ey: 262144), () => Wizard(x: int.MinValue), () => Wizard(z: int.MaxValue),
            () => Wizard(x: 8705), () => Wizard(y: 8961), () => Wizard(ex: 9217), () => Wizard(ey: 8961),
            () => Wizard(ex: 8704), () => Wizard(x: 0, y: 0, ex: 8208, ey: 0), () => Wizard(x: 0, y: 0, ex: 8192, ey: 16),
            () => Wizard(x: 0, y: 0, ex: 262128, ey: 262128) }) Reject(invalid, "bounded scalar/grid/distinct/squared length");
        Check(Wizard(x: 0, y: 0, ex: 8192, ey: 0).EndX == 8192, "exact8192 route accepted by neutral variant factory");
        Check(Wizard(x: 262128, y: 262128, ex: 262112, ey: 262128).OriginX == 262128, "world endpoint scalar valid");
        foreach (var boundary in new[] { Wizard(x: 8448), Wizard(x: 9472), Wizard(y: 8448), Wizard(y: 9472),
                     Wizard(ex: 8448), Wizard(ex: 9472), Wizard(ey: 8448), Wizard(ey: 9472),
                     Wizard(x: 8432), Wizard(ex: 9488) })
            Reject(() => Document(boundary), "both endpoints strictly inside current base");
        Check(Document(Wizard(x: 8464, y: 8464, ex: 9456, ey: 9456)).Placements.Count == 1, "nearest legal horizontal interior edges");
        foreach (var track in MusicTrackCatalog.SelectableTracks)
            Check(Parse(Serialize(NativeFreshLevelDocumentIntent.Create(Base(track.TrackId), [Wizard()]))).Base.MusicTrackId == track.TrackId,
                "every valid old base music retained independently of fixed source profile");
    }

    private sealed class OnePass(IEnumerable<NativeFreshPlacementIntent> values) : IEnumerable<NativeFreshPlacementIntent>
    {
        internal int Starts;
        public IEnumerator<NativeFreshPlacementIntent> GetEnumerator()
        { if (++Starts != 1) throw new InvalidOperationException("Unexpected re-enumeration"); return values.GetEnumerator(); }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
    private sealed class BoundedOverflow : IEnumerable<NativeFreshPlacementIntent>, IEnumerator<NativeFreshPlacementIntent>
    {
        internal int Moves; internal bool Disposed;
        public NativeFreshPlacementIntent Current => Wizard(Moves.ToString("x32"));
        object IEnumerator.Current => Current;
        public bool MoveNext() { if (++Moves > 129) throw new InvalidOperationException("Read beyond authoring cap"); return true; }
        public void Reset() => throw new NotSupportedException();
        public void Dispose() => Disposed = true;
        public IEnumerator<NativeFreshPlacementIntent> GetEnumerator() => this;
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
