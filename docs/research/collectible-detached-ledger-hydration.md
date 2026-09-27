# Detached selected-slot ledger hydration

`DetachedCollectibleLedgerImage` prepares an immutable **host research image**
from an existing detached save envelope. It replaces the need to invent Ready
metadata by hand in new CPU-composition fixtures. It does not issue native Ready,
install a hook, read/write a card, allocate RAM, or copy anything into live RAM.
The startup experiment does not change those limits.

## One validated input boundary

```text
Hydrate(residence, exactManifest, rawEnvelope,
        controllerIndex, nativeFileMarker, sessionEpoch)
```

The ordinary slot comes from the existing selected-slot residence descriptor.
The supplied manifest must match that descriptor's entire canonical identity,
including revision, stable bit assignments and retirement state. A canonically
identical parsed manifest is accepted; merely matching content/revision/count
with different identities is not.

The factory itself calls the existing `DetachedCollectibleSaveCodec.Inspect`
and derives state only from that newly validated immutable snapshot. It does
not accept a caller-provided read-result State, digest, cached count or active
mask. Unknown/legacy, future-version, malformed/checksum-invalid, padding-invalid
and other-manifest envelopes refuse without modifying the input or synthesizing
empty state. It does not migrate a predecessor implicitly. Explicit new-state
creation and authorized migration remain separate operations outside this API.

The existing residence supplies the selected lifetime bitmap and active mask;
there is no second detached-save serializer. All three envelope slots are
validated even though only one bitmap is represented in the RAM-shaped image.

## Exact image and bounded metadata

| Existing ABI field | Hydrated value |
| --- | --- |
| Full reservation | Exactly `0x200` bytes; guard, alignment padding and unused tail zero. |
| Magic/version/phase | Existing ABI magic/version; Ready phase byte **only in the detached image**. |
| Slot/controller/marker | Residence slot 0..2, explicit controller index 0/1, uint marker checked before narrowing to u16. |
| Content/manifest | Canonical big-endian GUID bytes, exact manifest SHA256 and revision. |
| Envelope digest | SHA256 of the complete retained validated envelope, including all three slots and its checksum trailer. |
| Lifetime bitmap | Complete selected-slot bits, including earned retired identities; no truncation/remapping. |
| Active mask/counts | Descriptor mask; counts recomputed by popcount of mask and earned AND mask. |
| Dirty/status/generations/operation/failure | All zero: a clean host snapshot, not proof of card durability. |
| Session epoch | Explicit uint copied exactly; zero and `uint.MaxValue` are admitted opaque metadata, never incremented. |

The existing ABI assigns no special meaning to epoch 0, and the current collector
does not read that field. Scalar bounds do not authenticate a selected native
save, fresh session, callback generation, card or file. The 16-bit native marker
is specifically **not a unique identity**. The envelope digest establishes host
snapshot identity, not evidence that those bytes came from or remain on a card.

The unchanged residence admission is 1..1408 lifetime identities, even when most
are retired. The detached save format still supports 4096. All u16 field widths
are checked; the native profile does not silently reduce or discard state.
Returned image/envelope arrays are defensive copies. Source arrays, manifest,
save state and prior returned copies cannot mutate the retained artifact.

`NativeBindingValidated`, `ReadyAdmissionAuthorized`, `NativeResidenceIssued`,
`Installed`, `LifetimeProven`, `CardIoApproved`, `PersistentSaveChanges` and
`PublicationApproved` remain false. `RequireNativeInstallation()` always refuses.
In particular, a zero guard in this byte image gives no permission to overwrite
a native live particle sentinel or an existing ledger.

## Focused regression

Tests were written first. After correcting a test initializer syntax error,
the valid test project failed with 9 errors for the missing production type;
the implemented Release build then passed with zero warnings/errors.

```text
DETACHED_COLLECTIBLE_LEDGER_IMAGE_OK checks=896 sourceReads=0 imageWrites=0 cardWrites=0 nativeReady=false lifetime=false
```

Coverage includes every slot/controller, marker 0/FFFF and oversized marker
refusals, epoch 0/1/MaxValue, GUID/digest/field widths, dense 1408 earned counts,
capacity and byte-boundary cases, retirement/all-retired state, exact manifest
binding, immutable copies and refusal without legacy/corrupt-byte resets.

The unchanged strict `RenderProbeCpu` also executes the actual collection leaf
against a hydrated 257-bit image: retired 254 and earned 255 refuse without writes;
unearned 256 collects without low-byte aliasing. Success changes exactly the
existing eight bit/count/generation/dirty bytes, preserving every other byte,
including retired bits. Duplicate collection and wrong-slot binding refuse.
These isolated CPU copies do not prove a native RAM owner, runtime admission,
concurrency, lifetime, save/card behavior or installation.

```sh
dotnet build src/Spyro.Editor.DetachedCollectibleLedgerImageSmoke -c Release
dotnet src/Spyro.Editor.DetachedCollectibleLedgerImageSmoke/bin/Release/net10.0/Spyro.Editor.DetachedCollectibleLedgerImageSmoke.dll
```
