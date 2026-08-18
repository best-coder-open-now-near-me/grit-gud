# Executable simulation foundation

## Delivery boundary

The executable simulation foundation is complete when every current
authoritative mechanic can capture, reconstruct, fork, reduce, install, record,
and replay through one consequence path. The first user hands-on playtest begins
after that candidate is technically verified. Playtest-driven stabilization,
persistent fire, the broader trainable decision laboratory, and optimizer
campaigns are later goals.

## Identity boundaries

Simulation artifacts carry independent identities:

- **Gameplay content identity** covers the scenario schema, gameplay rules
  schema, and actor, item, action, objective, and consequence definitions.
- **Spatial content identity** covers the level schema, collision, terrain,
  traversal, navigation, and evidence-algorithm version.
- **Scenario run identity** covers the run ID, authored seed, and random-schema
  version.
- Presentation content remains outside pure-combat compatibility. Changing an
  animation or effect must not invalidate a combat trajectory unless it also
  changes gameplay or spatial content.

## Numeric determinism

Authoritative values must be finite. Canonical float text uses invariant
culture, five decimal places, and midpoint rounding away from zero. Collections
use stable ordinal identity ordering. Candidate ties use stable candidate IDs.
Serialization and random mixing define byte order explicitly. Exact record and
sequence identity is used where transitions require equality; documented
tolerance applies only to physical evidence and float state comparisons.

## Randomness

Randomness is addressed by scenario seed, random-schema version, exact
transition sequence and kind, actor and subject IDs, purpose, and sample index.
Preview, candidate evaluation, cancellation, failed preparation, and branch
visitation never advance mutable random state. Candidate scoring sees expected
consequences; only the selected exact transition receives resolved hidden
samples.

## Execution spine

The permanent flow is:

1. capture a complete canonical state;
2. generate candidate skeletons from an actor observation;
3. freeze the deterministic world evidence required for legality and expected
   features;
4. select one exact candidate without observing hidden random samples;
5. resolve its addressed random samples into a semantic transition;
6. reduce the transition into a new canonical state and ordered domain events;
7. atomically install the state in live gameplay or retain it as a detached
   branch; and
8. record the semantic step for exact replay, diagnostics, and promotion into
   Unity verification.

`GameplaySimulationRuntime` owns the live immutable root. It verifies the
exact capability route, reduces without mutating the installed state, validates
the complete result, swaps the root once, records the trajectory step, and only
then publishes domain events. A presentation listener failure cannot roll back
or orphan the authoritative state or its trajectory. Detached branches begin
from an immutable root and never inherit mutable trajectory storage.

Repro bundles validate a contiguous pre-state/transition/post-state chain and
carry gameplay, spatial, run, random, numeric, evidence, and schema identities.
Their dependency-free portable JSON representation includes the complete
canonical initial state and resolved semantic payload graph; it is a cold-path
bug-report artifact, not a hot-loop serialization format. Exact replay verifies
both every canonical endpoint and the ordered domain-event types.

The first vertical proof is one complete direct-rifleman turn. The same spine
then expands across movement, stance, equipment, displacement, pins,
projectiles, emergency reactions, blasts, destructibles, consumables, smoke,
vehicles, objectives, and encounter lifecycle.

## Reachable capability coverage gate

Every player or AI input reachable from assembled content maps to an exact
semantic capability-and-subject profile. Subject kinds are explicit and include
actors, tactical destructible props, vehicles, objectives, world positions,
inventory items, projectiles, and system state. A route for `DirectAttack ->
Actor` does not satisfy `DirectAttack -> DestructibleProp`. Profiles include
traits that change behavioral
architecture, such as immediate versus turn-flight delivery, targeting mode,
inventory consumption, blast or smoke consequences, emergency windows, and
displacement policies. Numeric data such as range, damage, accuracy, and action
cost remains definition data and does not create a new route.

The capability registry records these stages independently:

1. candidate construction;
2. legality and frozen-world evidence;
3. pure state reduction;
4. domain-event production;
5. replay encoding and reduction;
6. headless execution; and
7. live installation.

Scenario assembly rejects reachable profiles missing any stage. The same
validator reports registered profiles that no assembled input can reach.
Candidate output is fail-closed: a candidate cannot leave generation unless
its exact profile has legality, reducer, event, and headless support. CI
assembles every committed scenario and runs this contract before licensed Unity
tests.

Ordinary weapon, item, and ability additions remain data-only when they use an
existing profile. Content that introduces new semantics cannot load until the
new profile has a complete route.

## Tactical subjects and spatial evidence

Candidate APIs use generic semantic subject references rather than enemy-only
target IDs. Canonical actors, incomplete objectives, vehicles, and registered
authoritative destructibles advertise tactical affordances. Destroyed props are
removed from tactical discovery. Decorative level objects never enter this
catalog merely because they have a collider or presentation object.

Headless spatial evidence is stamped by the static spatial identity plus a
dynamic fingerprint derived from actor poses, destructible state and pose,
vehicles, and smoke. The engine-free destructible evidence adapter projects
authored cover volumes through the resulting canonical prop's full position,
pitch, yaw, and roll for line of sight, route obstruction, and blast occlusion.
When a prop fractures, content assembly materializes portable per-chunk tactical
volumes from its registered fracture profile and only attached chunks remain in
later evidence. Detached chunks without a matching profile fail closed rather
than silently retaining or removing the wrong obstruction. Damage,
displacement, toppling, and fracture therefore invalidate later evidence;
destroying a cover object removes its obstruction from the next branch
evaluation. The Unity parity corpus covers intact, rotated, toppled, moved,
partially fractured, and destroyed cover, and must pass before hands-on
playtesting.

## Extension rule

New weapons and abilities reuse existing typed state, candidate, transition,
and feature families whenever their mechanics already exist. A genuinely new
mechanic must register its canonical state, validation, reduction, diagnostics,
domain events, candidate policy, and replay behavior. No mechanic may introduce
a trainer-only rule implementation.
