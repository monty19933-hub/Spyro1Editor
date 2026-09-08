# Fresh-window native read-only selection regression

On September8 the preserved R04 app actually reached its main window, then
Fresh Level. A macOS accessibility value write to its non-editable mode ComboBox
terminated PID36028 with SIGABRT at16:00:45. The retained managed stack ends in
ComboBoxAutomationPeer.IValueProvider.SetValue and AvnAutomationPeer.ValueProvider_SetValue.
This was an editor accessibility crash, not a ROM or DuckStation failure.

Avalonia12.0.3 reports this ComboBox value as read-only but throws from its setter;
the native bridge forwards that setter. Exact upstream sources:
[ComboBox peer](https://github.com/AvaloniaUI/Avalonia/blob/12.0.3/src/Avalonia.Controls/Automation/Peers/ComboBoxAutomationPeer.cs)
and [native bridge](https://github.com/AvaloniaUI/Avalonia/blob/12.0.3/src/Avalonia.Native/AvnAutomationPeer.cs).

The scoped38-line ReadOnlySelectionComboBox protects only the six Fresh-window
selection controls. Its cached value provider delegates live reads to the actual
base peer and ignores unsupported text writes. Ordinary selection, expand/collapse,
style and events remain the base implementation. No package upgrade, global
exception handler, free-text selection inference or new source/export permission.

Tests first reproduced the exact NotSupportedException after unchanged material48
and aggregate219 checks passed. The inclusive fixed build passed0warnings/0errors.
The next test failure was an incorrect boxed-integer identity assumption: actual
diagnostic values remained14, accessible text14 and SelectionChanged count0.
The test now checks integer value/index and reference identity for reference items,
including a baseline read before any enabled/value operation. Production stayed
unchanged. Actual repeat passed96 new checks plus material48/aggregate219.
Root then repeated actual-source inspection75 successfully and the full all-project
Release/default gate passed334/0. This headless result is not native acceptance
of a newly packaged app.

All logs and original managed/OS crash copies are retained in
`_local/research/readonly-selection-native-regression-r01.YlGG5M`.
The original R04 App/Core binaries and225-byte legacy seed remain exact; its
aggregate file was absent. After recovery, normal Down/Return keyboard selection
opened aggregate mode without editing/forking/saving. Only the Fresh window was
closed; the main R04 editor remained open. The preserved R04 has not been patched.
