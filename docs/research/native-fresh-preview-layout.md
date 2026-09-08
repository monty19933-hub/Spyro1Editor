# Schematic caption layout repair

The actual1120×800 source-thumbnail frame showed the schematic overlapping its
caption. A tests-first headless case reproduced it: preview bottom372, caption
top370, after the unchanged48/219/96 checks passed. The Grid had less remaining
space than the schematic's360 minimum height.

The left preview panel now scrolls as one unit when necessary; the schematic,
caption and authored projection retain their existing sizes/semantics. No source,
document, rendering/compilation or publication logic changed. Release build0/0
and18 layout checks pass across1120×800,940×680 and1400×1000 in both aggregate and
legacy modes, alongside the existing48/219/96 gates. Actual-source inspection75
also passed after this change, including complete original finals/closes.

Root viewed the newly rendered source frame, with no caption overlap:
`/var/folders/ly/6mrqskxj60717y5ln83x8yjh0000gn/T/spyro-material-inspection-source-ui-ljGnQH/actual-source-thumbnails-window.png`.
The explanatory note is scrollable below the schematic when the panel is short.
Logs10–14 are retained in `_local/research/readonly-selection-native-regression-r01.YlGG5M`.
This focused gate follows the334/0 checkpoint; the next integrated gate will
include it with the saved-level research action. Preserved native apps are unchanged.
