# Main level picker: safe read-only automation

## Failure and scope

The preserved beta47/R10 macOS app exited 134 when an accessibility client called
`IValueProvider.SetValue("Toasty")` on the main level picker. The pinned
[Avalonia 12.0.3 ComboBox peer](https://github.com/AvaloniaUI/Avalonia/blob/12.0.3/src/Avalonia.Controls/Automation/Peers/ComboBoxAutomationPeer.cs)
already reports `IsReadOnly = true`, but its setter throws `NotSupportedException`.
The native caller reached that setter despite the read-only value contract.

Only `_levelJumpBox` now uses `LevelPickerComboBox`. Its app-owned peer retains
the ComboBox theme, role, selected-item name, selection provider, and inherited
expand/collapse provider. It reimplements the value interface: read-only remains
true and every setter call is a no-op. Matching, ambiguous, unknown, empty, null,
or numeric strings never select a level. There is no exception catcher, global
handler, dependency upgrade, selection/export policy change, or replacement of
other ComboBoxes.

## Regression evidence

The actual MainWindow picker test was added before the production change. Against
the stock peer it reproduced `ComboBoxAutomationPeer.IValueProvider.SetValue`,
`NotSupportedException`, and exit 134.

Release checks after the change:

```text
dotnet run --project src/Spyro.Editor.UiSmoke -c Release -- --level-picker-automation-only
LEVEL_PICKER_AUTOMATION_OK checks=26 sourceOpened=false nativeWindowOpened=false nativeOutputWritten=false

dotnet run --project src/Spyro.Editor.UiSmoke -c Release --no-build -- --free-flight-ui-only
FREE_FLIGHT_SELECTION_UI_OK checks=9 actualPickerAndLoader=true sourceOpened=false nativeOutputWritten=false
FREE_FLIGHT_UI_OK checks=55 sourceOpened=false nativeOutputWritten=false
```

The focused test covers provider discovery/interface dispatch, selected label,
role/theme, unchanged selected-item identity and load-request state for rejected
values, inherited selection reporting, ordinary selection changes, disabled and
empty states. A test-only inline ItemsPresenter realizes the actual picker's
Toasty container without opening a popup; its inherited
`ISelectionItemProvider.Select()` updates selection and label, and subsequent
value writes remain no-ops. The production template is unchanged. Inherited
expand/collapse state is exercised on an untemplated twin:
opening a templated popup on an unshown headless Window caused headless overlay
recursion and is not used as a native popup oracle. The separate existing flight
suite still uses the real source-free picker and asynchronous level loader.

The default runner now registers this isolated mode with its own required marker
and unique log/result name, reusing the one UiSmoke build. Registration tests
first failed on the missing mode, then all 114 runner unit tests passed, including
missing-marker refusal and no source/CUE resolution. The expected default full
gate is now 289 results (previously 288); that complete gate was not rerun here.

Integrated default Release at b8634c8 passed **289 checks, zero failures**,
including the newly registered automation mode. Report:
`/var/folders/ly/6mrqskxj60717y5ln83x8yjh0000gn/T/spyro-regression-8v63b11t/summary.json`.
The source gate retained beta47 identity; beta48 is a separate subsequent
package identity for native AX retesting. No Core/export logic changed.

These source checks do not exercise the macOS native AX bridge or certify popup
behavior in a packaged app. The separate [R11/beta48 packaged check](v6-beta48-packaged-checkpoint.md)
now passes the exact native AX SetValue("Toasty") reproducer: the editor stays
alive and Stone Hill stays selected. Ordinary keyboard selection then loads
Toasty; Level -> Fly Anywhere shows the copied saved OFF choice unchecked with
Save/Revert enabled. No new edits or exports were made in R11. The app quit
normally with exit0. R10 remains the unchanged diagnostic package.
