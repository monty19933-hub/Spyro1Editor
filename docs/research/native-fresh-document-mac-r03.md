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

## Actual GUI boundary: user consent pending

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
not editor acceptance. Fresh Level open/edit/save/reopen, clean close, and the
post-bootstrap protection inventory remain pending that normal user action.
`POST-GUI-PRESERVATION-DESIGN.md` specifies the expected real bootstrap owners
and explicitly rejects missing private diagnostics or unexplained changes.

The preserved editor apps, original game image, prior test candidates and R02
were not replaced. No DuckStation session was started for this package gate.
