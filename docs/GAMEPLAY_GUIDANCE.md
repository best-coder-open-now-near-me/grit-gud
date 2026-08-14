# Gameplay Guidance Contract

Contextual guidance serves two audiences from one source:

- players receive concise tutorial expectations while learning a feature;
- testers see the intended behavior before deciding whether an observation is a
  bug or a design misunderstanding.

The exact runtime copy lives in
Assets/GritGud/Content/Resources/Guidance/gameplay-guidance.json. Each entry has
a stable ID, title, expected behavior, rationale, and player tip. The HUD selects
one entry from authoritative gameplay state; it does not infer behavior from
animation or duplicate the guidance strings in presentation code.

## Authoring rules

1. Give each independently testable behavior a stable, namespaced ID.
2. State what must happen in observable terms before explaining why.
3. Name blocked actions and the condition that unblocks them.
4. Keep rules authoritative and platform-neutral; put key or pointer help in the
   player tip.
5. Update or add a catalog test whenever an entry becomes part of a feature's
   acceptance criteria.
6. Prefer contextual replacement over a growing wall of tutorial text. The HUD
   should show the single rule most relevant to the player's current operation.

## Current selection priority

1. Authoritative movement playback.
2. Provisional route planning.
3. Initiated encounter economy.
4. Completed raised-deck objective.
5. In-range raised-deck interaction.
6. Active voluntary tactical interval.
7. Exploration entry into a voluntary tactical interval.

This ordering lets a narrow operation explain itself temporarily, then returns
the player to the broader session rule when the operation completes.

## Extension path

Contextual interaction, End Turn, Exit Turn-Based, and the raised-deck objective
now select guidance from authoritative object/session state. Hazards and future
combat resolution should follow the same pattern as their ordinary pipelines
land. The stable IDs can later drive hover tooltips, first-run tutorial
sequencing, accessibility narration, and telemetry without moving rule text
into gameplay logic.

The current entry is also embedded verbatim in the local `.txt` bug report
export described in [BUG_REPORTS.md](BUG_REPORTS.md). This keeps a reported
observation beside the exact behavior contract the player saw at that moment.
