# Enemy AI architecture and next steps

Enemy AI is a deterministic consumer of the same combat rules used by the
player. It does not own physics, transforms, animation, damage, equipment, or
turn progression. Application selects a target and records a tactical decision;
Unity captures line-of-sight evidence and candidate movement routes, then
presents the committed result.

## Current rifleman decision cycle

1. During exploration, authored perception range, view cone, hostility, and
   frozen exposure snapshots determine whether the enemy detects the party. The
   full capable party is evaluated before the enemy chooses the most exposed
   detected member, with distance and stable party order resolving ties.
2. During an enemy turn, every capable hostile party member is evaluated. The
   enemy selects the highest-chance shot, then prefers a more wounded target and
   shorter distance for deterministic ties. This prevents a nearby concealed
   actor from hiding a clearly exposed threat farther away.
3. The equipped attack, current exposure, accuracy decay, distance, action
   budget, reach, and authored per-turn attack limit determine whether a shot is
   available.
4. A shot below the behavior's authored minimum hit chance triggers a bounded
   movement search. The enemy moves only when a candidate produces a strictly
   better hit chance; route cost, preferred range, and visibility break ties.
5. If no route improves a legal low-confidence shot, the enemy takes the best
   available shot instead of wasting its turn. If neither attack nor movement is
   legal, it records an end-turn decision with an explicit rationale.
6. Detection, movement, attack, and end-turn decisions are immutable journal
   records. Replay and diagnostics consume their frozen target, exposure, route,
   and rationale rather than repeating Unity queries.

The depot rifleman's minimum attack hit chance is 35%. The value is authored per
behavior so a reckless suppressive unit can accept poor shots while a marksman
can spend movement seeking a cleaner angle.

## Ownership boundaries

- **Domain** validates behavior policy and owns immutable exposure, route, and
  decision records.
- **Application** owns target scoring, attack confidence, movement-option
  scoring, attack limits, and decision journaling.
- **Presentation** supplies exposure/route candidates and plays committed
  movement, weapon, effect, and incapacitation presentation.
- **Scenario content** owns perception, preferred range, search radius, attack
  limit, and minimum acceptable hit chance.

## Next production goal: encounter onset and lifecycle

The 2026-08-17 stabilization pass split Unity runtime registry, exploration
coordination, combat-turn execution, and incapacitation/completion presentation
without changing the current detection rule. The existing exploration
coordinator is therefore an adapter seam, not a completed awareness system.

The next slice adds first-class patrol and awareness before expanding combat
heuristics:

- Application owns patrol progress, suspicion/awareness, last-known evidence,
  escalation/decay, detection decisions, scoped encounter participants, and
  initiative entry/exit;
- Unity supplies frozen line-of-sight and sound-world evidence, patrol route
  reachability, and presentation only;
- scenario content authors patrol routes, sight/hearing thresholds, response
  policy, and reinforcement relationships; and
- journals and diagnostics freeze every awareness and participant transition so
  replay/debugging never re-query the world.

Do not fold sound into a larger sight radius, start every authored actor in
initiative, or store awareness only on a Unity controller. Patrol, sight, and
sound must converge through one portable evidence-to-decision boundary.

## Next tactical slices

The following are deliberately not hidden inside the rifleman heuristic:

1. **Knowledge and investigation.** Record last-known hostile positions and
   confidence decay so losing sight causes a bounded investigation rather than
   omniscient pursuit or immediate forgetfulness.
2. **Defensive exposure.** Extend movement evidence with reciprocal exposure so
   an enemy can compare outgoing shot quality against how exposed it becomes.
3. **Coordination.** Add explicit squad roles, claimed destinations, focus-fire
   limits, and suppression/flanking intents without reading other controllers'
   transient state.
4. **Action selection.** Compare equipped weapons, grenades, displacement,
   reloads, and stance changes through shared availability and cost results.
5. **Navigation depth.** Replace the radial local probe with authored tactical
   anchors or a deterministic navigation search that understands multi-step
   routes, hazards, elevation, and occupied destinations.
6. **Behavior archetypes.** Compose authored policies for riflemen, rushers,
   marksmen, support units, and noncombatants rather than accumulating special
   cases in one controller.
7. **Debugging.** Add an optional tactical overlay for perception cones,
   candidate routes, hit-chance scores, rejected options, and current knowledge.

Each slice should retain the current evidence/decision split: Unity may discover
possibilities, but only Application selects and records gameplay intent.
