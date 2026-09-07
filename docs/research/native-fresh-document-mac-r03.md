# Private Mac fresh-document test R03

The separate R03 app was built from clean checkpoint
`6eae8d417b6c746f3c4f980e41f7aa32151cd2d6`. This private package predates the
later detached P1/N32 scene work. It is not V6, a notarized release, or an
in-place replacement of the preserved editors.

App:
`_local/research/fresh-document-mac-test-r03/Spyro Fresh Document Test r03.app`.
Build scripts, native-launcher tests and complete handoff:
`_local/research/fresh-document-real-ui-r01.DsKiPF/mac-test-driver-r03/`.

## Verified packaging boundary

The native arm64 main retains fixed-path, argument, owner and no-link guards.
It executes the sealed helper with user shell startup disabled. A normal
launch selects only R03's private workspace, settings and diagnostics paths;
the launcher does not grant filesystem permissions.

Actual tests included 18 native guard cases, three source checks and four
driver-binding checks, with the earlier missing/extra-owner-newline failures
retained. One reviewed self-contained Release publish and native compile
succeeded. Strict deep signatures passed. The independent read-only validator
matched all 288 bundle entries, 4,387 seed entries, 19 native binary owners,
12 source/earlier-package pins and eight complete protected trees, including
the entire R02 tree. Protected before/after inventories matched exactly.

Root separately executed the actual native print-contract route and packaged
PNG compression round trip; both exited 0. Neither route starts the GUI.
The private project manifest was still absent afterward.

The captured outer build transcript was truncated by the tool. It is not
represented as complete: the actual driver-owned publish, compiler, dependency,
signature and inventory logs are retained in full under R03 `evidence/`.
Read `R03-PACKAGE-HANDOFF.md` for exact measured hashes and owner counts.

## Actual GUI boundary: main and saved document now visible

The earlier pending state below is superseded by a later read-only refresh.
Root found the same PID16530 running the actual R03 editor binary and its
private session log recording bootstrap/main initialization20:59:42–43 and
Stone Hill loading20:59:44. Normal CUA inventory/attachment then showed the
actual main editor with Fresh Level… and Browse Native Library… controls.
Root opened Fresh Level… and observed the saved2048-floor document, heading96,
colors#488478/#2C4264 and Track02. No document field, save, candidate or setting
was changed. The window was left open for John's hands-on testing.

Evidence: `evidence/ROOT-GUI-OPEN-20260906.md`. This proves actual main/Fresh
window opening, not edit/undo/save/close/reopen, publication or full post-startup
preservation. Do not automatically close or restore the now user-facing document.

### Retained initial permission history

Normal CUA/LaunchServices launch reached the native main and guarded helper,
but had not started the editor's project bootstrap. The initial UI request
timed out while the helper waited for macOS permission. Unlike R02's earlier
read denial, the narrow native TCC log explicitly records the R03 responsible
identity and `AUTHREQ_PROMPTING` for Documents-folder access at
2026-09-06 20:53:57 local time.

The system prompt is a protected UI surface that computer-use tooling refused
to operate. John was asked to click Allow himself if he approves. No alternate
automation route, Full Disk Access, privacy-database change, permission reset
or alternate-parent GUI launch was used.

Read `evidence/ROOT-GUI-GATE-PENDING.md` and the actual
`root-documents-consent-r01.log`. A running helper or a permission prompt is
not editor acceptance. The later main/Fresh window observation above does not
identify who handled the permission prompt. Edit/save/reopen, clean close and
the complete post-bootstrap protection inventory remain pending checks.
`POST-GUI-PRESERVATION-DESIGN.md` specifies the expected real bootstrap owners
and explicitly rejects missing private diagnostics or unexplained changes.

The preserved editor apps, original game image, prior test candidates and R02
were not replaced. No DuckStation session was started for this package gate.
