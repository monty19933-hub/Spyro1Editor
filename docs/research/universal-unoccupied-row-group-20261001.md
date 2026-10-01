# Unoccupied native row collision-chain sentinel

The ordinary Artisans export `2d2a7e7f467a2b5a429026061b905e5170b452e20cabceb7cb48cde4d07a9f02` froze while deleting native row30. The frozen RAM SHA256 is `275b1660f514712bdc592b7d55b16f212674e45663cb68045fe2d3e93cd8e98f`. Row30 at `80173BA4` had class1FE, no pointers and group0 at offset34. Global group-list base `80075778` was valid `8017A9A4`; the row had never been inserted into that group. Native `80052588` searches until it finds the actor and has no missing-membership exit.

Original USA executable SHA256 `a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998` establishes the required contract:

- `80052568` loads the signed group halfword at actor+34; `80052574` branches to `800525A8` when negative. Its delay instruction still reads the global group-list pointer, but it does not read a group entry.
- Animated constructor `80052700..80052708` writes -1 before optional collider insertion; only a successful insertion writes the actual group at `80052740` and links the actor at `8005274C..80052754`.
- Simple constructor `800529CC..800529E0` also writes -1 at actor+34.
- The installed save-map constructor pass skips unoccupied physical rows. They therefore need this sentinel in their serialized cleared row rather than relying on a constructor to supply it.

`UniversalRowRemoval.RemoveOwnedPropertiesAndClearRows` now writes group -1 after clearing removed rows. Existing class1FE/rewardFF sentinels, property pruning, physical allocation, save identity, all survivor group fields and native executable routines stay unchanged. Rows later reused by an import receive the complete source row before normal initialization.

Both Release and Debug isolated builds passed with zero warnings/errors. Each source-backed writer test passed 201 checks against the exact nine removals `[29,30,32,33,94,109,113,121,124]`. It verifies every tombstone, survivor group fields, all non-scene subfiles, source immutability, hole reuse and tail trimming. Each strict scalar test passed 63 checks using the actual original KillMoby and constructor bytes. Every emitted tombstone returns with only its own state+48=FD write and no group-entry read; the former group0 encoding fails at the unowned group head. Live linked groups0/1/63 still unlink the exact next pointer. These tests are source/static execution evidence, not native gameplay acceptance.

Reproduce from the source root (no discs or cards are written):

```sh
dotnet run --project src/Spyro.Editor.UniversalRowRemovalSmoke -c Release
dotnet run --project src/Spyro.Editor.UniversalRowRemovalSmoke -c Release -- \
  "$USA_BIN" "$SOURCE_ROOT" "$FIXTURE_JSON"
python3 tools/Test-UniversalRowRemoval.py "$FIXTURE_JSON" "$ORIGINAL_EXE_BIN" --report "$REPORT_JSON"
```

The source-aware emitter authenticates the original image. The scalar test authenticates the original executable and all emitted native windows. Logs, source/module bindings and read/write receipts are under `work/universal-v1-continuation/dependencies/row-removal-*`. The frozen failure image/RAM are retained under `work/universal-v1-continuation/runtime`. Native cold-load retesting is owned by the parent and must be reported separately.
