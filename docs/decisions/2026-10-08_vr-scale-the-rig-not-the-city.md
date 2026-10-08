# VR tabletop: scale the XR rig, not the city

Date: 2026-10-08

## Context

The VR mode shows the city as a table-sized model (about 1.8 m long). The city
is built in world units of 1 unit = 1 km (Helsingborg is 6.57 × 10.69 units).
Something has to make the city appear about 6,500 times smaller.

## Decision

Scale the **XR Origin** up by S = world units per real metre (S = 6.517 for
Helsingborg) and leave the city untouched. `TabletopRig` computes S at load,
from the city bounds, and logs it.

## Alternatives considered

- **Scale and move `CityRoot`.** Rejected: highlights (`EntityHighlighter`,
  under `InteractionSystem`) and comparison copies (100 units below the city)
  are not children of `CityRoot` and are built from world-space geometry. They
  would all need changing, and the "1 unit = 1 km" rule used by picking, tests
  and comparison fairness would no longer hold.
- **Rebuild the city at table scale.** Rejected: it would duplicate the
  geometry pipeline for one mode.

## Consequences

- Picking, highlights, comparison, logging and tests work unchanged in VR.
- Anything sized in world units on the rig side must be multiplied by S:
  - camera clip planes (set by `TabletopRig`);
  - ray length, line width and reticle (`XRControllerPointer`);
  - XRI's own line visuals, which are therefore turned off.
- World-space UI added later must be sized in metres × S, or parented under
  the rig so it inherits S.
- Logged head poses are in world units. Divide by S (logged at startup) to get
  metres.
- XRI's two-handed scaling also works by scaling the XR Origin. This keeps a
  zoom feature possible later, if the map ever needs pan/zoom.
