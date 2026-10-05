# Spyro Editor 1.0.9

## Changes since Spyro Editor 1.0.8

- Inventory: every row now uses one text size, so level names, dragonfly eggs, treasure and dragons line up and fit.
- Inventory: the dragonfly egg column has a spinning gold dragonfly egg above it, like the treasure chest and dragon. The egg picture and project total at the top of the screen are gone; levels without dragonfly eggs show a dash.
- Terrain: select several tiles with Cmd-click (Ctrl-click on Windows) or Cmd-drag, then remove them together with one confirmation. Undo Terrain brings removed ones back.
- Terrain: new brush option "Whole tiles" (on by default) raises, lowers or levels every tile the circle touches evenly instead of pulling single corners into spikes.
- Terrain: Create BIN no longer fails with "stored in N independent HP vertex slots" after removing or editing tiles. Shared corners are updated in every copy the game keeps, and tiny accidental moves from clicking are ignored.
- Terrain: clicking a tile or corner never nudges it; dragging starts after a short movement. Dragging a corner so far that the ground would fold over itself is blocked with an explanation.

All existing gameplay restrictions are retained. Universal Moby gameplay remains incomplete. The Mac package is not notarized; the Windows package has not been run on Windows.
