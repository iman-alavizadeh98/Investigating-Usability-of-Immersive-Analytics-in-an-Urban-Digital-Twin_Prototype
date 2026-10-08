# VR tabletop: resizable table, wrist menu, table board

Date: 2026-10-08

## Context

The first headset run (Quest 3 over Link) gave this feedback:
- the table and the model were far too small (1.17 × 1.80 m, 1:6517; a 20 m
  building was 3 mm tall);
- the panels showed keyboard wording or were not in the VR space, and the
  controls did not feel like VR controls;
- the menu should be on the left wrist.

The plan had the table fixed at one scale, and pan/zoom inside the table was
an open question (it needs a table-edge clipping shader).

## Decision

1. **Resizable table, no pan.** The table starts at 2.6 m long (1:4380) and
   the right thumbstick (up/down, held) resizes it between 1.2 and 4.0 m
   around its fixed physical centre. Only the rig scale S changes
   (`TabletopRig.SetTableLength`); the city stays where it is, as in the
   scale-the-rig decision. Every finished resize is logged.
2. **Wrist menu that opens when looked at.** The hand menu stands above the
   left wrist, opens on a watch-reading gesture and closes when the wrist is
   lowered; X pins it in place (open, world-locked) and back.
3. **A board at the table** in addition to the wrist menu: legend, current
   view and selection on a 1.28 × 0.80 m board beyond the far table edge,
   moving around the table to stay opposite the user. Y hides it.
4. **Controller shortcuts** replace every desktop key: A copy to compare, B
   clear, thumbstick left/right switches views, thumbstick up/down resizes.
   No keyboard or mouse wording is shown in VR (Help tab, study `questionVr`);
   the PC monitor shows only the facilitator's keys.

## Alternatives considered

- **Fixed larger table (2.6 or 3.5 m).** Simpler and identical for every
  participant, but no way to inspect small buildings without bending over.
  Rejected for flexibility; the resize is logged so sessions stay
  reproducible.
- **Pan/zoom inside a fixed table.** Needs a clipping shader for every
  renderer and changes what part of the city is visible; kept open.
- **Menu always on the wrist / always in front.** Always-on covers the table
  when the left hand is used for pointing; a head-locked menu is uncomfortable.
- **Wrist menu only.** Reading the legend requires raising the hand all the
  time; the board keeps it in view.

## Consequences

- The user's distance to the table edge changes when resizing (the centre is
  fixed): users step back when the table grows. The Quest boundary must allow
  a 4 m table plus walking space, or the maximum must be lowered
  (`TabletopRig.maxTableLengthMeters`).
- The view a participant sees can differ in scale between sessions; analyses
  must use the logged `vr_table_resize` events (S per time) when comparing
  pose or distance data.
- The wrist gesture thresholds and the board's text size were set without a
  headset and must be tuned in the next headset test.

## Addendum (same day): movable table, hands, compass

- The table can also be **moved and turned** (left grip grab, left thumbstick
  turn, stick press brings it in front of the user), so users do not have to
  walk around it. As with resizing, only the rig is placed differently; the
  city stays in world units. Moves are logged (`vr_table_move`).
- **Virtual glove hands** replace the controller models (users asked to see
  their hands; real hand tracking would only work with the controllers put
  down and would need gesture interaction).
- **Compass letters** on the table edges, and a labelled controller diagram
  on the Help tab.
