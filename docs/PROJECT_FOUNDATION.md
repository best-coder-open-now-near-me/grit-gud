# Project Foundation

This document records the decisions established before implementation. It is
the baseline for architecture and prototypes, not a promise that every proposed
mechanic will survive playtesting.

## Product shape

Grit Gud is a turn-based CRPG with continuous, gridless space. A WebGL build is
a first-class development client so the game can be played and its levels can
be edited from a browser. Windows is the initial desktop target.

The Unity Editor remains useful for asset import, animation, materials, unusual
prefab creation, and final visual tuning. Routine gameplay, data, and level work
should not depend on direct access to the Editor.

## Source-of-truth boundaries

| Concern | Source of truth |
| --- | --- |
| Gameplay rules | Platform-neutral C# domain code |
| Levels and encounters | Versioned, human-reviewable data |
| Runtime editing | The in-game level editor |
| Browser drafts | A storage adapter backed by browser-local storage |
| Desktop saves and exports | A storage adapter backed by files |
| Art and engine-only configuration | A minimal set of Unity assets and settings |
| Replays | Resolved actions, rolls, paths, and outcomes |

Unity scenes provide the application shell. They must not become a second level
format. The long-term target is one lightweight bootstrap scene and one level
loader shared by gameplay and editor modes.

## Architecture rules

1. **Keep authoritative rules outside engine objects.** Combatants, turns,
   wounds, rolls, and resolved actions should be ordinary serializable C# data.
   MonoBehaviours present state and translate Unity input, rendering, and
   physics queries at the boundary.
2. **Use physics as a query service.** Raycasts, overlaps, and segment tests may
   answer spatial questions. Uncontrolled Rigidbody simulation must not decide
   authoritative combat outcomes.
3. **Treat WebGL constraints as baseline constraints.** Core systems cannot
   require native plugins, direct filesystem access, unsupported threading, or
   dynamic code generation. Platform-specific facilities sit behind adapters.
4. **Prefer inspectable data.** Level and save formats need an explicit schema
   version, stable identifiers, validation, and migrations once a released
   schema changes.
5. **Record outcomes for replay.** A replay consumes the original resolved
   action record; it does not depend on two platforms reproducing floating-point
   physics bit for bit.
6. **Do not hide a grid in the rules.** Movement, range, paths, projectiles, and
   blasts use world-space distance. Optional snapping is only an authoring aid.
7. **Make randomness explicit.** All gameplay randomness comes from a seeded,
   testable service. The roll and relevant modifiers are part of the resolved
   action record.

## Spatial combat model

### Exploration and explicit turn entry

Exploration may run continuously when no encounter requires initiative, but the
player must also have a persistent **Enter Turn-Based Mode** action. Entering
turn-based mode cannot require hostility or a combat trigger. It should let the
player deliberately slow the world to coordinate positioning, stealth, traps,
environmental hazards, or other time-sensitive interactions.

The transition must establish an explicit initiative state for the controlled
party and any nearby actors or world processes whose timing can affect the
outcome. A voluntary turn session outside an initiated encounter is one complete
tactical interval: leaving it records the resolved actor state, advances the
matching nearby-actor and world-process behavior, and then replenishes AP and
movement for the next interval. Re-entering therefore begins fresh, but cannot
postpone hazards or gain unmatched actions because the environment receives its
corresponding step on every completed interval.

Completing a voluntary interval starts a re-entry lockout equal to the minimum
world-turn duration authored in the scenario timing data. Exploration remains
available during that time, but both
the HUD action and direct input must reject a new voluntary interval until the
world turn has elapsed. An initiated encounter may interrupt this lockout.

An initiated encounter is different. Entering or leaving its presentation mode
must not refresh AP, movement, cooldowns, or other resources. The encounter owns
its initiative and economy until its mandatory events are resolved. Transitions,
completed voluntary intervals, and initiative ordering are recorded as
authoritative events so replay does not depend on presentation time.

The player may request a return to continuous exploration when there are no
unresolved hostile turns, incoming impacts, or other mandatory initiative
events. The rules layer decides whether exiting is currently safe; the HUD
action only submits the request.

Firing an immediate weapon during exploration does not create hostility by
itself. The discharge is still recorded, faced, animated, and presented, but it
uses zero AP while continuous time owns the world. Encounter initiation belongs
to explicit target response data: an actor, prop, vehicle, alarm, or later world
process starts initiative when attacked only when its authored response says so.
Inert walls and unconfigured scenery remain ordinary surface discharges.

### Movement and cover

- Movement budgets are expressed in world units.
- A path service returns a traversable path and cost; movement does not depend
  on frame rate.
- Line of sight and cover are sampled against stable target regions rather than
  exact animated mesh triangles.
- Standing and crouched stances move those stable regions. Physical geometry
  determines occlusion; stance does not grant an intrinsic cover bonus.
- Non-physical concealment such as smoke or darkness attenuates perception and
  attack confidence without automatically blocking projectile travel.
- Small, documented tolerances handle boundary cases such as grazing cover or
  arriving at the end of a movement budget.

Navigation data must be authored or produced consistently. It should not be
rebuilt independently on each client as part of resolving a turn.

### AP and movement economy

Action points and movement are separate turn resources. Traveling through the
world spends movement but does not automatically spend AP. Actions spend AP and
may also consume movement opportunity when their execution requires time that
could otherwise have been spent traveling.

Actions should describe their mobility requirements as data. The initial
working profiles are:

- **Mobile:** can be performed while moving and does not reduce movement.
- **Set:** requires slowing or briefly stopping and consumes part of the
  remaining movement budget.
- **Momentum:** requires, follows, or modifies a movement path, as with a charge
  or vehicle maneuver.

This makes action order consequential. A character who has exhausted their
movement can still use Mobile actions but may no longer meet the requirements
for a Set action. Exact movement costs and whether they use distance, a fraction
of the turn budget, or an internal time conversion must be settled through the
first action-economy prototype.

### Momentum and constrained paths

Combatants use free-turn, distance-budgeted movement and do not retain speed,
braking, or turning-radius state between routes. Vehicles retain a deterministic
momentum state containing at least current speed and forward direction. A
vehicle movement profile supplies maximum speed, acceleration, braking, and
turning limits.

The reachable area for a momentum-based mover is a curved fan extending from
its forward vector. Low speed permits short, wide movement; greater speed
produces a longer, narrower envelope. Starting, stopping, reversing, and sharp
turns therefore take space and time.

Path validity must be checked along the complete curve rather than only at its
endpoint. Each section respects a speed-dependent curvature or minimum turning
radius, preventing an invalid zigzag from remaining inside an otherwise valid
cone. The path's final tangent becomes the mover's new forward direction.

This is authoritative kinematic state, not Rigidbody simulation. Damage may
modify profiles—for example, engine damage can reduce acceleration, steering
damage can widen the minimum turning radius, and brake damage can lengthen the
stopping distance.

### Destructible cover and physical displacement

Destructible world objects are a required system. An authored destructible prop
has stable identity, integrity, damage responses, and explicit intact, damaged,
and destroyed states. A state change may replace its collision, cover,
navigation, interaction, and visual definitions. Arbitrary runtime mesh
fracturing is not required for the foundation; reliable gameplay state is.

The **Take Cover** action associates a combatant with suitable nearby geometry
without granting abstract protection. Actual exposure still comes from body
region visibility tests. Moving, displacing, or destroying the object can
invalidate that cover relationship immediately.

Throwing props and forcibly moving combatants should use one underlying
displacement resolution model. A Throw action selects an eligible subject and a
continuous-space destination or path:

- A prop can be pushed or thrown according to its mass, size,
  interaction points, and the actor's capability. Its path may strike a
  combatant or another destructible object.
- A combatant can be thrown to a nearby valid landing position after an opposed
  roll. Arbitrary throw direction covers pull-like placement; mass limits how
  far the subject can move. The cover must be low or open enough for the
  attacker to reach across it.
- The destination is constrained by reach, mass, obstructions, and the acting
  character's movement and AP resources. It is not an adjacent grid tile.

The skill pool must include an opposed physical-control skill used both to
perform and resist forced displacement. **Close-Quarters Control** is the
working name because it covers leverage, grappling, balance, and positional
control without implying unarmed damage alone. Physical state, mass, reach, and
geometry can provide specific constraints without introducing an abstract
defensive stance bonus.

Close-quarters actions should share this spatial and action-economy foundation.
Push and Throw are distinct authored intents recorded on the shared displacement
result, not unrelated transform animations. A future Lift action is justified
only by a persistent held state; Pull is not separate from arbitrary-direction
Throw. A knife is an equipped attack: it uses authored reach and AP cost,
seeded attack resolution,
ordinary wound consequences, and replay diagnostics. Eligible props may also
enter an explicit toppled state whose collider, cover, and navigation effects
are authoritative. A toppled prop may also pin a character as a first-class
gameplay result when its committed contact geometry and authored mass rules
qualify. The pinned state identifies the responsible prop, restricts movement
and incompatible actions, and is cleared only by a committed escape result.
The character's Push Off action must atomically record the released prop pose,
cleared actor state, cost, and get-up transition; animation cannot decide any
of those outcomes. These close-quarters interactions precede deployable
companions on the implementation roadmap.

Displacement targeting is action-first rather than pointer-gated. The player
opens Displace, chooses an authored intent, and only then aims that intent at a
subject. The action list is therefore stable and discoverable even when the
pointer is over empty space. While an intent is armed, pointer hover reports the
subject that the selected action will evaluate; hover never decides which
actions are visible in the menu. A crosshair-shaped cursor appears only over an
attackable target or while an explicit world action is armed. It follows the
pointer and is never fixed to screen center.

### Direct and slow attacks

An attack roll decides whether an attack connects. Spatial queries decide which
targets and body regions are valid, what cover applies, whether the target is in
range, and whether an object intercepts a traveling projectile.

Slow projectiles advance a fixed distance according to turn rules. Their impact
is resolved against the world state at arrival, which allows a target to move,
take cover, or be replaced by an interceptor. Fixed, guided, area, and piercing
projectiles should be behaviors built on the same projectile state model.

### Impact cycles for high-threat ordnance

Rockets and similarly threatening slow ordnance may initiate a temporary
**impact cycle**. This turns the incoming projectile into an initiative event
and gives combatants a constrained opportunity to respond instead of treating
delayed damage as a passive timer.

The intended AP-time sequence is:

1. The attacker pays the weapon's launch AP cost. A heavy rocket may consume a
   full normal turn, while another weapon may leave AP available. The accuracy
   result establishes the projectile's trajectory or scatter.
2. Remaining AP after the launch cost represents time still available in the
   attacker's turn. That interval contributes projectile travel before the next
   initiative window; firing early therefore gives the projectile a head start,
   while firing as the final action gives it none.
3. After accounting for that interval, compare time to predicted impact with one
   normal turn allowance (currently four AP). A farther impact remains in normal
   initiative. An eligible threat inside that window opens an impact cycle.
4. Derive the abbreviated reaction allowance from the remaining travel time,
   capped by the normal allowance. Every eligible combatant receives a chance to
   act within that same interval; sequential UI turns do not spend the interval
   again for each responder.
5. After all responses, advance the projectile through the recorded shared
   interval, query the resulting segment against the updated world, and resolve
   any collision immediately.
6. Convert a predicted fractional interval to whole AP with ceiling, clamped to
   at least one AP and no more than the launcher's normal-turn allowance. An
   impact reached during committed pre-reaction travel resolves immediately and
   does not create a zero-AP reaction window.
7. The impact cycle ends and normal AP budgets resume. AP remains a fraction of
   one authoritative turn; Presentation never derives gameplay timing from
   animation seconds.

Emergency AP may be spent through the ordinary action system on affordable
responses such as moving toward cover, crouching, spreading out, changing
equipment, or firing when the rules permit it. The impact cycle does not invent
defensive powers or special movement, and existing wounds still constrain the
actions a combatant can take.

Impact cycles need the following guardrails:

- Reserve them for attacks whose threat justifies interrupting the normal combat
  rhythm. Ordinary arrows should not automatically create one.
- Launching another projectile cannot restart, extend, or postpone an active
  cycle.
- Resolve an early collision as soon as the projectile crosses blocking
  geometry rather than waiting for the initiative deadline.
- Reveal exact trajectory information only to combatants who perceive it. An
  audible warning may communicate danger without exposing precise direction.
- Record the phase transition, abbreviated turns, projectile movement, and
  impact in the resolved action log so replay does not reconstruct the timing.

Weapon-specific launch costs, eligible projectile types, response actions, and
simultaneous-projectile rules remain playtest decisions. The emergency allowance
is derived from travel time rather than authored as one universal constant.

### Thrown items and explosions

Thrown accuracy selects a landing point rather than directly selecting a victim.
The first prototype should model an uncertainty region influenced by range and
throwing capability. The resolved landing position is recorded before the blast
is evaluated.

Blast effects depend on distance and geometry from the actual landing point.
Cover can attenuate or block a blast; friendly fire is possible. Blasts may
damage or destroy authored world objects. Object weight, elevation, delayed
detonation, rolling, and environmental effects are candidates for later
experiments, not foundation requirements.

### Functional wounds

The combat foundation uses six functional regions:

- Head
- Torso
- Left arm
- Right arm
- Left leg
- Right leg

There is no general hit-point or vitality pool. Actors tolerate a small authored
number of wounds, normally two or three, before incapacitation. A successful
attack determines its struck region as an outcome; ordinary attacks do not ask
the player to select a body part. The authoritative wound snapshot preserves
left/right limb location independently and the player HUD exposes those six
regions without introducing an HP bar. Local wounds and later status processes
can carry functional effects such as reduced perception, bleeding, concussion,
attack penalties, dropped weapons, and reduced movement. Explicit body-part
selection is reserved for a future exceptional action if a concrete scenario
needs it.

## Authored characters and starting builds

Characters are authored identities. Their baseline attributes, starting skills,
talents, appearance, starting loadout, and general capabilities are selected in
the pre-level Character Creator and remain fixed during play. There is no
runtime XP, advancement-point, or player-side rating-spending system.

Every character has exactly four core attributes rated from 1 to 5:

- Strength contributes to melee and opposed displacement checks.
- Dexterity determines base initiative and movement allowance. At session start,
  Dexterity maps onto a reaction advance from `1` through `N`, where `N` is the
  total number of friendly and hostile initiative participants. The advance is
  `1 + floor(((clamp(Dexterity, 1, 5) - 1) * (N - 1)) / 4)` and is retained in
  the Application result. Movement allowance is four plus Dexterity world units
  per turn.
- Grit contributes to resistance checks for consequences such as concussion,
  bleeding, and other statuses. It does not create hit points or increase the
  number of ordinary gunshot wounds an actor can survive.
- Charisma contributes to social checks when social resolution is introduced.

Reaction-advance ties prefer higher Dexterity and then stable actor ID, so
identical content produces the same ordering on every platform. The complete
Dexterity, combatant count, reaction advance, and final position are projected
into the Dialogue window's Combat channel. Opposed Close-Quarters Control currently
uses `d20 + Strength + control skill + talent modifier`.

Customization comes primarily from:

- Pre-level appearance, attribute, skill, talent, and starting-loadout choices.
- Equipped weapons, armor, tools, and carried objects.
- Tactical consequences such as wounds, recovery, and changes in equipment or
  role.

Skills are rated competencies used by shared resolution rules, including the
opposed Close-Quarters Control check. Talents are authored capabilities or rule
modifiers that distinguish characters and can provide specific counters. Their
values are authored before a level and do not expose an in-game spending path.

## In-game level editor

The editor should manipulate the same level model loaded by gameplay. Its first
useful version needs:

- Placement, selection, movement, rotation, and deletion of supported entities.
- Optional position and angle snapping without grid-based gameplay semantics.
- Undo and redo through reversible commands.
- Validation with useful object-level errors.
- Browser-local draft persistence.
- Import/export of portable text level data.
- A direct transition between editing and playing the current level.

It is deliberately domain-specific. It is not intended to recreate the Unity
Editor or handle arbitrary asset import in a browser.

## Cross-platform acceptance rule

A feature is not complete when it works only in the desktop Editor. Starting
with the first playable slice, both WebGL and Windows builds must be produced
regularly. Automated pure-C# tests should cover authoritative rules and data;
small target-specific smoke checks should cover adapters and serialization.

Bit-identical rendering and floating-point values are not required. Given the
same resolved action record, both targets must agree on the meaningful gameplay
outcome.

## Decisions intentionally left open

- Exact Unity release and package versions.
- Render pipeline and visual style.
- Camera perspective and controls.
- Normal turn structure, standard AP budget, and broader initiative rules.
- Exact movement opportunity costs for Mobile, Set, and Momentum
  actions.
- Momentum parameters, vehicle controls, and which non-vehicle actions retain
  speed between turns.
- Which attacks initiate impact cycles and how simultaneous projectiles behave.
- Party and roster structure, skill taxonomy, talent pools, and pre-level build
  constraints.
- Destructible-object granularity, material rules, and debris behavior.
- Exact hit-roll formula and defensive statistics.
- Vitality, wound severity, healing, recovery, and permanent-death rules.
- Navigation implementation and authoring workflow.
- JSON versus another text serialization format.
- Hosting and build-automation provider configuration.

These should be decided by the smallest relevant prototype or a short decision
record, not inferred silently during implementation.
