# Collapsed-cyclorama focused runtime observations — 2026-09-04

Status: **focused observations passed; complete runtime acceptance still pending**.

## Exact artifact and environment

Candidate directory: `/Users/john/Documents/Spyro Editor/Projects/default-project/_research/unusedlevel65blank/v1/materialized-collapsed-35-part-cyclorama-probe-schema7-candidates/remote-pad-grid.x384-y352-schema7-collapsed-35-part-cyclorama-probe-21a3fa24829040a5a9a4ee9f66e12aa442b34d207c2a98238deb2d67c9bf6d03`

Opened the CUE named `unusedlevel65blank-authoring-collapsed-35-part-cyclorama-probe-schema7-candidate-v1.cue` explicitly through DuckStation's file picker. BIN SHA-256 was independently verified before boot:

`65d70f5e91ed049a7a48d117a2e30223b77f37853a9a961120acb660c8cbc232`

DuckStation 0.1-11826 on macOS, Vulkan. Cold boot; no resume state loaded, memory cards disabled, game cheats disabled. Entered the established ID65 warp sequence from a fresh Artisans game. No ROM/project edits were made by the runtime test.

## Direct observations

- The ID65 pad loaded with Spyro, Sparx, the brick/grass terrain and grass Moby.
- The inspected starting, side and movement views had a uniform blue-gray background; the previously observed Town Square silhouettes were not visible in those views.
- Movement worked. Two falls reached the death/respawn transition and returned Spyro to the same pad. The life counter changed from 4 to 3 to 2.
- The final idle observation remained responsive at 30 game FPS / 60 video FPS, with Spyro and Sparx visible.
- No crash or visible leftover Town Square geometry occurred during these observations.

These are not proof of all camera positions, every visibility group, collision isolation, arbitrary assets, save compatibility, or long-duration stability.

## Input limitation and outstanding gate

This build exposes eight controller macros. A supported toggle macro was necessary for reliable frame-length inputs; ordinary synthesized tap bindings were not sufficient. Macro 8 was used for Start. Changing that macro to Cross through the live settings UI did not change its observed in-game behavior in this session. **Jump was not verified.** Therefore this report does not authorize promotion of the complete runtime gate or the one-part schema-8 successor. A reliable cold-start control configuration and remaining jump/camera checks are required first.

## Evidence files

Screenshots are native DuckStation captures under:
`/Users/john/Library/Application Support/DuckStation/screenshots`

| File | Observation | SHA-256 |
| --- | --- | --- |
| Spyro the Dragon 2026-09-04-19-19-29.png | Entry view | e1e689c9d8c4e64345d94ae4f8f456a133a99cb67e5d146c58bdc180a17686ae |
| Spyro the Dragon 2026-09-04-19-19-34.png | Side view | 2108266652daa9af6112106ffc59c896e194a7273cd5b55344a50d1bfbb1f42f |
| Spyro the Dragon 2026-09-04-19-20-13.png | First respawn | d9120bae442aa975907591be599de9a5527f74d5daea7cd551852a40c950ad47 |
| Spyro the Dragon 2026-09-04-19-20-30.png | Three lives | a706b6de289832e06d320db01710f1255b1f2ccc263df176b78f1d72620ed1d0 |
| Spyro the Dragon 2026-09-04-19-20-43.png | Movement view | 8f7ebf387eaa4732cdd4cc0d57c0d6466448dba8860ad03e84d7de214356d536 |
| Spyro the Dragon 2026-09-04-19-21-28.png | Two lives | 1bb938f64a92285c91f2b0ae33dafb42961dbd0a68e684cad9b0af2d33f10624 |
| Spyro the Dragon 2026-09-04-19-24-50.png | Final idle view | 47fbf396d892c454f6e8f16131c771017b114197af07a07524aeb77638009eee |

## Restoration

Quit DuckStation with Save State For Resume unchecked. No DuckStation process remained. Restored the original global and game configuration bytes; hashes matched the pretest originals:

- `settings.ini`: `cd3bfb693556cc08c81b69f56199b61068ac7b483f3d5dae22e2a3931ca55721`
- `gamesettings/SCUS-94228.ini`: `629d9bd3eb9890eedb2bfd9ae2458f7f214272f951a9081f687abe8e5f11d1f3`

No immutable candidate receipt or capability flag was changed.
