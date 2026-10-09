# 2026-10-09 — VR: locked-in tutorial, click tools, separate panels, clearer data

## Context

Fourth playtest in the Quest 3 (participant's view, by the developer). Feedback
(12 points), in short:

1. New users need a very short tutorial at the start that **locks them in**
   until they understand the controls and the UI objects.
2. Right thumbstick left/right switched views by accident: remove it.
3. Names were confusing ("pop-out copy", "Move (grip / pinch)").
4. Help should not be a wrist-menu tab; it should be a brochure (pictures,
   little text) that also explains the menu.
5. Panels could be pushed into the table.
6. Copy should not be "select, then press Copy": choose a Copy button, then
   each click copies and the copy pops up.
7. Legend and selection info were one board: separate them; the legend may
   stay at the table, the info panel should move.
8. The table toolbar mixed table and non-table functions; every button there
   should act on the table. "Clear" should clear the data off the table
   (only plain buildings stay).
9. Compare should work like Copy (choose Compare, click areas) and show its
   result on a panel; copy and compare should not be related.
10. Several different things may be copied, but only one copy of each.
11. Voting-district boundaries should be bolder.
12. Close values (turnout 70 / 74 / 75 %) were impossible to tell apart:
    height and colour differences should be more extreme and varied.

## Chosen option

- **Tutorial** (`XRTutorial`): 13 short steps (welcome, select, show data,
  panels, move table, size, copy, grab, compare, move a panel, wrist menu,
  clear, ready). Action steps advance by themselves when the user did the
  thing; reading steps have **Next**. While it runs, `XRFeatureLock` allows
  only what has been taught so far: toolbar buttons are greyed out, city
  clicks (`InteractionManager.CityClicksEnabled`), grabbing, the table grip,
  resizing and the wrist menu are off. The card stands at the far table edge
  beside the legend; the toolbar button to press is outlined. Facilitator
  skip: **F8** on the PC; **Restart tutorial** on the wrist menu. Logged as
  `vr_tutorial` (step, id, start/done/skip/finish, seconds).
- **Click tools** (`XRClickTools`, toolbar CLICK row): **Select · Copy ·
  Compare**. Every click selects; with Copy it also copies
  (`XRSelectionCopies.Copy(entity)`), with Compare it fills slot A, then B
  (`ComparisonManager.CopyToNextSlot`) and opens the **Compare panel**. The
  hover label says "Click to copy / compare" while a tool is on. A (right)
  no longer copies.
- **Copies:** pop up from the original (0.45 s rise with overshoot); one per
  entity — copying it again makes the existing copy pulse (`vr_copy` how =
  `exists`).
- **Panels** (`XRTablePanel` base): **Legend** (`XRTableLegend`, far edge,
  fixed to the table, not grabbable), **Info** (`XRInfoPanel`, right of the
  user, Grab bar, Y shows/hides), **Compare** (`XRComparePanel`, left of the
  user, Grab bar), **Help** (`XRHelpPanel`, six picture cards, in front of
  the user), **Tutorial**. The old `XRTableBoard` is removed.
- **Toolbar** (`XRTableToolbar`): DATA (< View, view name, View >, **Clear
  table** = view + selection + copies off), CLICK (Select, Copy, Compare,
  Remove copies, Buildings on/off), TABLE (Smaller, Bigger, Turn left/right,
  Bring here). Board / Menu / Hands / Reset panels / Help moved to the wrist
  menu's **Panels** tab.
- **Wrist menu:** tabs Views · Task · Panels (Info / Legend / Compare / Help
  show-hide, Reset panels, Restart tutorial, hand tracking experimental).
- **Names:** grab bar "Grab", "Copy", "Compare", "Unselect", "Clear table",
  "Select its area".
- **Panels kept out of the table:** `TabletopRig.KeepOutOfTable` moves a
  panel the shortest way (up, or sideways past the nearest edge) so it does
  not overlap the table footprint below 25 cm above the table top
  (`panelClearanceMeters`); applied while carrying (`XRGrabber`) and to
  automatic placement. Users may still step into the table.
- **Outlines** (`AreaOutlineSettings` on a layer spec, `AreaOutlineBuilder`):
  25 m wide dark lines (about 6 mm on the 2.6 m table) along each area's boundary, on the flat area (Surface)
  or on the column tops (HeightSurface; not with InsetExtrusion). Enabled for
  the voting-district (S1–S3) and DeSO (S4) layers.
- **More contrast:** maximum column heights doubled and minimum heights
  halved in all evaluation views (district/DeSO 0.01–0.6, grid
  0.006–0.4 up, 0.004–0.24 down, Unity units = km). Single-variable colour
  stories (S1 and S2 turnout, X1 population, S6 children) use the new
  **turbo** palette (blue → green → yellow → red); two-palette views keep
  viridis / ylorrd / magma so the layers stay distinguishable.

## Alternatives considered

- **Tutorial as a skippable overlay:** rejected; the feedback asked for a
  lock-in. The facilitator key remains for emergencies.
- **Quantile classes** for colour: crisp steps, but close values in the same
  class become identical — the opposite of the request. Scales stay Rank.
- **Local contrast / per-view rescaling:** changes the meaning of the legend;
  not done.
- **Magnifying single copies:** distorts comparisons between copies; copies
  stay at table scale.

## Consequences

- Turbo is not perceptually uniform in lightness and is harder for some
  colour-vision deficiencies than viridis; differences between close values
  are easier to see. Rank scales mean neighbouring ranks still look alike;
  exact values remain on the Info panel and hover label.
- Taller columns occlude more of the city behind them; the table can be
  turned and resized.
- Tutorial timings (`vr_tutorial` seconds) give a measure of learning effort
  per control.
- Layer specs gained an optional `outline` block (JsonUtility; missing =
  off), so older specs are unaffected.

## Found while testing: map colours did not match the legend

The project renders in **linear** colour space, but
`UrbanAnalytics/VertexColorUnlit` returned the palette's sRGB vertex colours
unconverted, so every map colour was shown too light (pastel) while the UI
legend showed the correct palette. The shader now converts vertex colours
with `SRGBToLinear` (not in gamma colour space). Map and legend now match,
and all palettes show their full contrast (desktop and VR). Materials using
the shader: `SpatialVertexColor.mat` (all map colours, area outlines, copies)
and `VR/VRPointerRay.mat` (rays, copy stems: slightly darker colours).

## Follow-up after the next playtest (2026-10-09)

- **Tutorial card too far away** (far edge, ~2.5 m): moved next to the user —
  their side of the table, 0.8 m to the left, centre ~1.6 m above the floor,
  about 0.9 m from the eyes (0.9 mm per canvas unit). The Compare panel's
  default centre went down to 0.33 m above the table so the two do not
  overlap during the compare step.
- **A = copy again** (removed by mistake with the "select then copy" flow):
  A copies what the ray points at, or the selection when nothing is hovered.
  The Copy tool stays; both make the same copies (one per entity).
- **Buildings 2× as tall in VR** (`UrbanContextManager.buildingHeightScale`,
  display only; true heights stay in all info and logs). Footprints cannot be
  enlarged without overlaps; for bigger buildings overall the table can be
  made bigger (up to 4 m).
