# Independent native egg verification, 2026-09-29

The retail USA save already stores the complete object bitmap. The bounded scalar tests execute actual source instructions; they do not prove a BIOS card write or a gameplay transition.

## Reproduce

Build `src/Spyro.Editor.UniversalIdentitySmoke` in Release, then run:

```sh
python3 tools/Test-UniversalEggProgress.py --dotnet /opt/homebrew/bin/dotnet
python3 tools/Test-UniversalEggUpdate.py --dotnet /opt/homebrew/bin/dotnet
python3 tools/Verify-SpyroNativeEggSave.py '/path/to/Spyro the Dragon (USA).bin'
```

The first two commands need no game image. The last authenticates the exact retail source and only reads it. No ROM, card, firmware, or save state is distributed with these tools.

## Source and limits

USA image SHA256: `fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37`. EXE SHA256: `a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998`.

Original serializer `0x80059864` and deserializer `0x80059594` copy all `0x480` bitmap bytes at `[0x80077908,0x80077D88)` to/from ordinary `0x600` save-slot offsets `[0x10C,0x58C)`. This represents 36 levels and 256 native row bits per level. The checksum at `0x58C` covers that bitmap. The full routines and original helper execute in the strict scalar CPU: 73 assertions, zero card I/O. Stable placement row identity is therefore necessary; reordering rows would attach saved bits to different objects.

Native MarkCollected `0x8003B854` derives a placed ordinal from `(moby-g80075828)/0x58` within `[g80075828,g80075890)`. Its durable address is `0x80077908 + (g80075964 << 5) + ((row >> 5) << 2)` and its bit is `1 << (row & 31)`. The current level index uses `(levelId/10-1)*6 + levelId%10`.

The JP egg row uses kind `0x7F`; the USA reload path only unlinks collected kind `0x7D`. Original bitmap retention alone therefore cannot suppress these custom actors. The generated egg update now checks the exact durable bit before animation or pickup. Independent emitted-code checks cover all seven classes (including white and pink), all 512 possible class IDs, multiple native row/level boundaries, reconstructed actors, invalid owner ranges, and blink-byte isolation: 2800 assertions. FreeMoby, particles, and MarkCollected are explicit narrow helper doubles in this test; their native behavior is not claimed by those assertions.

The source-bound live percentage routine at `0x8002BB20` has one direct caller in the bounded EXE/card/retail-overlay census, at `0x8001B654` in global inventory. The implemented base percentage is `floor((50*min(g,12000)+6000*d+10000*(e+K))/(12000+100*N))`, with g=treasure, d=dragons, e=ordinary eggs, K=collected custom eggs and N=active custom eggs. After every required category is complete, bonus treasure uses `100+floor((g-12000)/100)`. Independent scalar execution of emitted counter and percentage bytes covers N=1,2,6,7,32,116 and 1188 scenarios / 2383 assertions. The counter is 104 bytes and percentage body 188 bytes, within their 128/192-byte owners. This changes the global live percentage; it does not establish per-level X/Y, the save-menu percentage, or bonus-level admission.

The scalar CPU handles actual branch/load delay slots, signed/unsigned multiply, unsigned divide, and HI/LO. It does not model cycle timing, COP2/GTE, devices, BIOS, or card scheduling.

## Runtime boundary

A dedicated portable DuckStation profile uses copied firmware and a copied card. The unsupported host-scripted card callback trial remained pending; it is a failed harness attempt, not a native-save success or evidence of game failure. The final candidate (BIN SHA256 `74674d9a2f5c969764a123bbe5cd6e01cba9aeaf2fdaae3822add21cbe14be43`) booted and normally loaded the copied card into Stone Hill (58 gems, 2 dragons); the native pause menu opened. A debugger-assisted cursor-only write at `0x80075720` selected Inventory, but automated Cross input did not enter it. A later RSP interrupt timed out before any further mutation. No PC rewrite, host-called native save, or controller-memory injection is counted as gameplay. Custom placements are in Toasty; their final appearance, collection, sign content, egg save/cold reload, death and transition acceptance remain open. Original card and settings hashes remain unchanged. The isolated portable emulator was left open after the harness stopped; no forced shutdown or save was issued. The parent outputs/Verification/native-runtime.json records exact firmware, controller, export and preservation identities.

Effective selected model/effort is not exposed by verification tooling and remains unverified.
