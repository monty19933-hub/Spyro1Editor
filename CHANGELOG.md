# Spyro Editor 1.0.4

- Add placeable teal gems worth 50 and rainbow gems worth 100 under
  Collectibles → Gems, using the existing USA source disc.
- Include both values in editor totals, native collected treasure and level
  targets, with ordinary memory-card collected-state persistence.
- Support both gems in retail and fresh level composition with exact capacity
  checks, normal save/reopen, duplication, removal and distinct viewport markers.
- Keep the original five gem values and enemy/chest reward selectors unchanged.
- Add native source-owner, save identity, numeric popup and particle lifecycle
  checks. Level capacity still applies; see the supported-scope guide.

---

# Spyro Editor 1.0.3

## Changes since Spyro Editor 1.0.2

- Add **Game Settings → Portals & Return Home**: choose a doorway destination and each level's return doorway, save immediately, undo a route change, and include routes in normal Create BIN.
- Preserve the moved doorway's trigger, lettering and fly-out path when returning. Duplicate destinations use the return doorway you select. The stone arch remains separately editable terrain.
- Support route composition with imported Return Home objects, saved replacement levels and fresh slot65, with native ownership and memory checks before export.
- Automatically rebuild missing or damaged original terrain textures when loading a level. Textures and all editable terrain remain available together; offline geometry loads explain missing materials.
- Give new and cleared levels a wider removable starter floor, correct fresh-level portal arrival height, and check landing space before export. Existing saved terrain is preserved.
- Continue universal-object dependency proofs. Full universal Moby gameplay coverage remains unfinished; native resource and behavior limits still apply.
