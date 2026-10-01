# Native class consumers with authenticated owners

Universal imports retain actual remapped model classes. A class read cannot be
rewritten indiscriminately: some native consumers need the actual model-table
index, while others interpret the original class as a behavior value. The
following proofs admit only exact source-owned consumers; they do not bypass
the remaining ownership or capacity checks.

## Model lookup followed by native animation

`UniversalNativeModelTailOwnership` recognizes thirteen original-USA source
sites for class 0x186. Each computes the actual model pointer, reads its animation
argument and calls native helper 0x80055A78 with argument a2 equal to eight.
The helper's authenticated path calls 0x80056DC4, which kills v1 before using it.
Consequently the retained model-table address in v1 is not an unowned class
value. The actual remapped model lookup must remain intact.

The proof checks every reaching table-base definition, load-delay ordering,
control-flow entries, the complete retained source instruction witnesses and
exact executable helper bytes. Copied and pruned code may contain more than
one occurrence of a source address; every occurrence must remain unchanged.
A correct occurrence cannot authenticate an additional modified copy. No
instructions or resource allocations are added by this proof.

At commit `d732692`, Release and Debug each passed 68 source-free and 352 native
source checks. An independent scalar executor checked the original helper path.
All thirteen actual pruned handler parts were exercised. A focused survey of
829 prior ownership refusals admitted 564 compositions and retained 265 RAM
refusals. That focused source predates the final combined particle/colour work;
the final full survey records any subsequent resource changes. These are
composition results, not gameplay acceptance.

## Linked actor argument to a native particle no-op

`UniversalNativeParticleNoOpOwnership` authenticates 24 source packets that
pass a linked actor's class as argument a3 to particle type 64. All 35 original
destination particle callbacks dispatch this type to their native no-allocation
tail. The following authenticated native callee kills a3 before using it.
Changing the argument therefore does not change the observed native operation.

Admission requires the complete source packet, unchanged copied instruction
witnesses, the actual destination callback binding and full callback bytes,
the type-64 table target, and the following callee's kill prefix. The same
destination certificates are checked again after final executable/overlay
composition. Other particle types and changed bindings remain refused. This
proof emits no instructions and claims no general particle or enemy behavior.

At commit `ef77259`, Release and Debug each passed 78 source-free and 18,242
native-source checks. An independent R3000 delay-slot interpreter executed all
35 native callbacks for each possible class from zero through 511. The type-64
path made no engine calls or global writes; the only class-derived output was
the a3 argument subsequently killed by the next native callee. Mutated duplicate
source witnesses, changed final bindings/callbacks/table targets and changed
kill prefixes were rejected.

## Final integrated coverage

The full `3196a94` survey clears all 2,421 prior ownership refusals covered by
the two families. Of these, 1,601 compose and 820 reach measured RAM limits.
The simultaneously integrated gem-colour correction also makes 335 formerly
composing Dream Weavers variants exceed RAM capacity. Retail composition
therefore rises by a net 1,266 to 105,559 of 145,285 cells. The default fresh
scene gains 116 variants without regression, reaching 3,980 of 4,151. These
counts include dependency-unverified results and every matrix runtime field
remains not-tested.

An additional normal written-disc sample imports Alpine Ridge row 62 into
Stone Hill using the final Core. It passed 497 checks, including the final
native model/frame data, save mapping, target 210, 21,892 bytes RAM headroom
and all 254 unrelated WAD entries. Its BIN SHA-256 is
`98b7ecbdcdcabc68e8e96ff6ac7d928dc03ddd76aa6af0e6f427e124b22113e9`.
This is static composition/readback evidence, not an enemy interaction test.

Private source fixtures and reports stay under the task's
`work/universal-v1-continuation/model-tail-probe`, `class149-research` and
`verification/model-tail-review` directories. Public source contains only
proof logic, tests and digest certificates, not native assets.
