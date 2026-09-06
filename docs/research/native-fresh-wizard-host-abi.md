# Fresh Wizard: source-native Town host ABI boundary

September6,2026. New isolated source oracle:
`src/Spyro.Editor.NativeFreshWizardHostSmoke/source_fresh_wizard_host_abi_oracle.py`.
No Core changes, candidate output or gameplay acceptance in this batch.

The existing Dark Hollow adapter is not directly reusable in the fresh body's
retained Town host. Actual Town actor setup uses s3 for the Moby, fp for Spyro
and s7=10. Donor Wizard loads Spyro from s7. The unchanged Dark adapter leaves
Town's s7 unchanged, so its source slice reads address10. The retained RED
demonstrates an unsupported host combination, not a Dark Hollow regression.

The bounded Town recipe saves/restores30 nonzero/non-SP GPRs, SP and HI/LO in
an owned2B0 frame, copies old fp to donor s7 before setting fp1, and returns to
the actual native next-owner/epilogue. Tests compare all512 native class routes;
only exact classes26/11B receive new hooks. Native companion78 remains unchanged.

Particle boundaries also differ. Town tick uses s2=raw and s0=raw+1, rather
than Dark's s1 cursor. A private-frame adapter maps semantic owners without
reusing Dark-only operand patches. Town spawn selector/color/counter registers
need saved-value loads to avoid destructive register swaps. Actual source07/41
integer ticks and the pinned RNG helper execute with exact32-byte afterimages;
native deletion/allocator calls stop at their unowned helpers. The separately
seeded sprite suffix is not a simulated successful allocator return.

**Not an installable overlay:** the prototype table-complement witness places
the same synthetic ENTRY in four words, but uses different role bodies in
separate machines. Independent review demonstrated that one shared tick body
fails spawn inputs. Final materialization must allocate and bind distinct
main/bolt/tick/spawn and selector-specific entries and validate their combined
bytes. Individual recipes and unchanged-table complements do not certify that
combined placement, resource ownership or native execution.

Evidence directory:
`_local/research/dark-wizard-native-closure-20260906/fresh-wizard-host-abi-r01.ASvq7K/`.
Full README documents the split entries and controls. Final source1027 checks/
37 expected refusals; independent peer and root repeats exited0 and were byte-
identical to05-final-green.json, SHA
`2e8ac86f574bc8e4a174ec61f0eb750da0433432f10fa9e7386ca6e69c36ef99`.
Root output is12-root-source.json with empty stderr. Whole original BIN and
all pinned helper/source spans are authenticated before/after. Oracle source
SHA `e6032f4ff386cd060c056919cbed52629fadfe6efbd9257a72a77be6dae41800`.
Peer review11-zeno-review.md includes destructive-swap, shared-entry and
unretired-load negative controls; none modify tracked/source/candidate bytes.

Visibility helpers, whole Wizard/lightning handlers, allocator/vector/deletion
completion, transitive stack peak, final overlay/copy-buffer placement, model/
animation/texture/sound resources, scene owners and save/reward budgets remain
next work. This source recipe is not universal Moby portability or runtime proof.
