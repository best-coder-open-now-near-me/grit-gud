# Enemy AI architecture and next steps

Enemy AI is a deterministic consumer of the same combat rules used by the
player. It does not own physics, transforms, animation, damage, equipment, or
turn progression. Application selects a target and records a tactical decision;
Unity captures line-of-sight evidence and candidate movement routes, then
presents the committed result.

## Current rifleman decision cycle

1. During exploration, the coordinator captures the best frozen exposure among
   capable hostile party members plus one-time authoritative action sound.
   Unity supplies the world evidence only; range, view cone, sound attenuation,
   hostility, suspicion gain/decay, and escalation are canonical rules.
2. `Unaware` enemies follow only their next authored patrol waypoint. A sound
   can produce `Suspicious`; qualifying sight can produce `Alert`. Each actual
   awareness or patrol change has a sequenced journal record containing the
   frozen evidence or exact route.
3. Alert creates a scoped encounter containing the player party, alerting
   enemy, observed subject, and transitive authored reinforcements. The scoped
   initiative order replaces the exploration order, so unrelated actors cannot
   receive a tactical turn.
4. During an enemy turn, every capable hostile party member is evaluated. The
   enemy selects the highest-chance shot, then prefers a more wounded target and
   shorter distance for deterministic ties, with stable actor identity as the
   final tie-breaker. This prevents a nearby concealed actor from hiding a
   clearly exposed threat farther away and makes exact ties independent of input
   enumeration order.
5. The equipped attack, current exposure, accuracy decay, distance, action
   budget, reach, and authored per-turn attack limit determine whether a shot is
   available.
6. A shot below the behavior's authored minimum hit chance triggers a bounded
   movement search. The enemy moves only when a candidate produces a strictly
   better hit chance; route cost, preferred range, and visibility break ties,
   followed by lexicographic route geometry for an order-independent result.
7. If no route improves a legal low-confidence shot, the enemy takes the best
   available shot instead of wasting its turn. If neither attack nor movement is
   legal, it records an end-turn decision with an explicit rationale.
8. Combat decisions, awareness transitions, patrol advances, and scoped
   encounter changes are immutable records. Replay and diagnostics consume
   frozen target, exposure, sound, route, and rationale rather than repeating
   Unity queries.
9. A selected projectile attack launches through the shared authoritative
   projectile session and impact-cycle controller. Enemy delivery therefore
   records and presents the same flight, emergency reaction, collision,
   diagnostics, and journal evidence as the equivalent player launch.

The depot rifleman's minimum attack hit chance is 35%. The value is authored per
behavior so a reckless suppressive unit can accept poor shots while a marksman
can spend movement seeking a cleaner angle.

## Ownership boundaries

- **Domain** validates behavior policy and owns immutable exposure, sound,
  awareness, patrol, and decision records.
- **Application** owns awareness evaluation, participant scope, patrol/action
  reduction, target scoring, attack confidence, movement-option scoring, attack
  limits, and journal ordering.
- **Presentation** supplies sight/sound/route evidence and plays committed
  patrol, movement, weapon, effect, and incapacitation presentation.
- **Scenario content** owns sensing policy, patrol routes, reinforcements,
  perception, preferred range, search radius, attack limit, and minimum
  acceptable hit chance.

## Encounter follow-up

The foundation is ready for playable validation. Do not fold sound into a
larger sight radius, start every authored actor in initiative, or store
awareness only on a Unity controller. Patrol, sight, and sound converge through
one portable evidence-to-transition boundary.

Dynamic reinforcements joining an already active encounter, explicit departure,
investigation movement from last-known positions, and an awareness overlay are
separate additions. They must reuse the current scope and observation records.

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
