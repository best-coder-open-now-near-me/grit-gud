# Architecture and Separation-of-Concerns Review

**Reviewed:** 2026-08-11; follow-up audits 2026-08-12, 2026-08-14, and 2026-08-15
**Scope:** the repository as a whole, with additional attention on the recent
emergency-reaction, projectile, explosive, progression, displacement, and HUD
work.

This review is the durable record of the architecture pass. It is intentionally
separate from the roadmap: this document records constraints, findings, and
decisions; the [roadmap](ROADMAP.md) records delivery order and completion.

## Executive assessment

The foundation is sound. The assembly graph points in the intended direction:
Domain is platform-neutral, Application depends on Domain, and Presentation is
the Unity adapter. Authoritative actions generally freeze their inputs and
outcomes for replay instead of attempting to reproduce Unity physics.

The primary risk is no longer a missing foundation. It is **boundary erosion as
features accumulate**. A few composition and presentation classes are becoming
secondary rules engines, while the central gameplay session and content
assembler are becoming change hotspots. Close-quarters displacement is the
right feature family to correct those trends before adding the drone.

### 2026-08-14 follow-up

The latest pass corrected the highest-risk correctness and lifecycle findings
without attempting a destabilizing rewrite:

- committed-level readiness now uses the same archetype, actor-template, and
  presentation catalogs as runtime loading; Authoring, Publish, and Runtime
  validation profiles have distinct enforcement semantics;
- document copies and read queries no longer normalize or mutate their source;
- nested terrain command groups project every patch, enforce per-document
  limits, and replace the visible terrain root only after construction succeeds;
- gameplay startup is failure-safe, teardown has one authoritative path, and
  HUD choice state plus generated textures have explicit session ownership;
- action observers are published only after the shared action, focused session,
  consequences, smoke fields, and journals finish committing; one failing
  observer does not prevent later observers from running; and
- attack, displacement, and thrown-explosive randomness derives stable named
  streams from the scenario seed instead of starting correlated generators from
  the same value.

The remaining hotspots are deliberate incremental work. `GameplaySession`
still centralizes actor state and outcome dispatch, `GameplayHud` still owns too
many feature drawers, and `GameplayController` is still a large composition
root. New behavior should continue moving behind focused Application services,
HUD panels, and feature binders when those seams become stable. Durable
cross-launch progression storage also remains explicitly deferred.

### 2026-08-15 editor and terrain follow-up

The editor expansion preserves the intended authority boundaries. Portable
terrain material samples, three-axis transforms, rotation pivots, interaction
points, destructible defaults, and spatial dressing records live in the Domain
document. Reversible mutations live in Application commands. Pointer input,
physics-assisted settling, mesh projection, shaders, and IMGUI controls remain
Presentation adapters. Repository inspection found no Unity or Presentation
reference in Domain or Application source.

This follow-up also turns the most important separation rule into a fast gate:
`tools/validate-repository.py` now verifies the Domain and Application assembly
contracts and rejects Unity or Presentation references in either neutral source
tree. Architecture review is therefore no longer dependent only on convention.

The current risk profile is acceptable but not finished:

- `GameplayHud` (2,641 lines), `GameplaySession` (2,219),
  `LevelEditorController` (1,815), and `GameplayScenarioAssembler` (1,554)
  remain the largest production change hotspots. Split only along stable feature
  seams; do not introduce a framework or broad rewrite.
- Serialized terrain material meaning currently depends on numeric palette
  indices shared by validation, UI ordering, and mesh-color projection. Before
  adding or reordering materials, introduce one stable palette contract with
  named IDs and a migration policy. UI array order must not silently redefine
  saved data.
- `TerrainHeightLevelEditorTool` now coordinates both height sculpting and
  material painting. Its stroke lifecycles are cohesive today, but the name and
  responsibility should become a generic terrain-brush coordinator or two
  focused tools if erosion, foliage, masks, or other brush families are added.
- The next cross-launch party work must preserve the same ports-and-adapters
  boundary: Application owns a versioned party-save use case and validation;
  Presentation supplies PlayerPrefs/browser and filesystem adapters. Gameplay
  controllers must not directly serialize authoritative party state.
- Planning text had drifted behind implemented interaction-point,
  destructible, batch-transform, and three-axis work. Roadmap statements should
  be treated as delivery records, not as substitutes for executable boundary
  gates.

No architecture blocker requires reverting the current editor slice. The
highest-value next work remains durable party save/load and advancement UI;
editor work can proceed independently with destructible pile verification and
richer viewport transform tools.


## Non-negotiable boundaries

1. **Domain owns gameplay vocabulary and immutable records.** Definitions,
   requests, outcomes, snapshots, and invariants must not depend on Unity.
2. **Application owns use cases and authoritative commits.** Turn validation,
   affordability, equipment transitions, contests, journaling, replay
   validation, and state mutation belong here.
3. **Presentation owns input and projection.** Unity may map a raycast hit to a
   stable entity ID and render a committed result. It must not decide which
   target kinds, costs, hand requirements, or consequences an action accepts.
4. **Content authors policy.** Costs, reach, subject kinds, required free hands,
   auto-stow behavior, contests, toppling eligibility, and result policy are
   authored definitions, not action-name conditionals.
5. **Queries return evidence; records freeze decisions.** Physics adapters may
   answer reach, path, landing, collision, and exposure questions. Application
   code converts that evidence into a committed record; replay consumes the
   record without querying the world again.
6. **Composite actions are atomic from the player's perspective.** If a
   displacement intent requires stowing a weapon, the combined equipment and
   displacement costs are prevalidated and shown before confirmation. A failed
   displacement must not leave the weapon stowed or spend only part of the
   budget.

## What is working well

### Assembly direction

The Domain and Application assemblies remain independent of Unity. This is the
most important structural property in the repository and must remain enforced
by assembly references and fast compile tests.

### Replay-oriented action records

Movement, attacks, projectile advances, explosions, and displacement are built
around resolved records. Recent emergency-cycle work also uses an
application-owned lifecycle rather than hiding initiative changes in a
projectile presenter.

### Stable, versioned content

Scenario content uses stable IDs and an explicit schema. The assembler provides
a useful validation boundary between serialized data and runtime definitions.

### Physics as a port

Line of sight, projectile travel, throw paths, and blast exposure enter the
rules through query interfaces. That pattern is appropriate for deterministic,
cross-platform gameplay even though the returned evidence still needs to be
generalized for non-actor blast subjects.

### Authored projectile presentation

Projectile visuals remain downstream of committed flight records. Model, trail,
and impact prefabs plus their transforms, scale, acceleration ramp,
ghost cadence, and effect lifetime are authored in the projectile presentation
catalog. `ProjectileFlightPresenter` orchestrates recorded movement and the
trajectory ghost; `ProjectileEffectPresenter` owns effect instantiation and
emission lifecycle. Neither component can alter collision, turn advancement, or
impact outcomes.

## Findings and decisions

### A1 — Generalize close-quarters actions before adding more intents

**Priority: immediate**

The current path stores `pushCost` beside a throw capability, exposes a
`PushCost` shortcut on the actor definition, and validates a literal
`close-quarters.push` action in the central session. Presentation also recreates
application definitions from raw content and decides that Push accepts props.
That original shortcut would have multiplied conditionals for Throw, topple,
combatant displacement, and knife attacks. It has now been replaced.

**Decision:** author one `DisplacementAbilityDefinition` on each capable actor.
The ability owns its stable ID, display name, hotbar assignment, and ordered
`DisplacementActionDefinition` options. Each action definition carries:

- stable action ID and display name;
- intent (`Push`, `Lift`, or `Throw`);
- AP and movement-opportunity cost;
- accepted subject kinds;
- reach, maximum displacement distance, maximum subject mass and size, plus an
  optional authored mass-to-distance decay function;
- hand requirement and authored auto-stow policy;
- whether an opposed Close-Quarters Control check is required;
- allowed result policies such as topple, release, or collision damage.

The hotbar binds the family-level `Displace` ability once. Activating it opens a
generic option flyout populated from the authored action collection; Push,
Lift, and Throw are not independent hotbar entries. Presentation submits
the selected action ID plus a stable candidate ID. Application evaluates target
validity and returns a reason suitable for `INVALID TARGET` details.

The shared technical term is **displacement**. Player-facing verbs currently
remain Push and Throw. Pull is intentionally omitted: arbitrary-direction Throw
already covers its useful destinations, and authored mass decay shortens heavy
throws to adjacent space. Lift should appear only if a future persistent-held
state gives it behavior distinct from Throw. Existing `Throw*` common types should be migrated to
`Displacement*` names incrementally when doing so does not obscure behavior
changes.

### A2 — Model hands and weapon stowing as authored action policy

**Priority: immediate, in the displacement definition migration**

Unequipping must not be an unconditional hard-coded requirement. A shoulder
check may be usable with a weapon equipped; a two-handed lift may not be. Each
action definition therefore declares `None`, `OneHandFree`, or `BothHandsFree`
and whether the application may satisfy the requirement by automatically
stowing equipment.

The application composes an atomic plan:

1. determine required equipment transitions;
2. calculate the total authored cost;
3. validate the target, path, contest prerequisites, and entire budget;
4. expose that plan for UI confirmation;
5. commit ordered equipment and displacement records together.

This preserves the desired two-action tax without coupling displacement rules
to a particular HUD or equipped item.

**Status:** implemented for Throw. Inventory definitions author occupied hands;
Application availability exposes the resolved combined cost and automatic stow;
one action record commits the equipment and displacement outcomes in order.

### A13 — Pointer targeting is independent from camera orbit

**Status: implemented**

The unlocked pointer is the authoritative screen-space targeting input. The
shared acquisition presenter converts the current pointer position into a
camera ray, then character-origin LOS and range queries decide whether the world
point or actor is valid. RMB may orbit the camera without moving an implicit
center aim point. The fixed HUD reticle is removed.

An exposed target under the pointer enables the crosshair cursor and a direct
basic attack. Explicit world-point actions keep the crosshair cursor active
while armed and accept arbitrary visible geometry. HUD and dialogue rectangles
block both hover acquisition and world confirmation, so UI clicks cannot leak
into gameplay.

### A14 — Hostile actors share rules but not player input ownership

**Status: implemented for the depot rifleman**

Hostile configuration is authored as actor combat data: allegiance, hostile
allegiances, wound threshold, perception range and view angle, preferred range,
movement search radius, and per-turn attack limit. The Application-owned enemy
decision session accepts frozen exposure and route candidates and records one
deterministic detect, move, attack, or end-turn decision. It does not raycast,
move transforms, drive animation, or synthesize effects.

Unity responsibilities are split three ways. `UnityEnemyTacticalQuery` adapts
colliders into target exposure and traversable route candidates.
`GameplayEnemyActorPresenter` owns a hostile actor's weapon, route playback,
and incapacitation presentation. `GameplayEnemyController` coordinates
detection and active turns through the shared attack and emergency-cycle
sessions. Enemy shots therefore use the same seeded resolution, journal,
diagnostics, weapon catalog, humanoid pose, and muzzle-origin path as player
shots. Incapacitation remains authoritative session state; initiative and
emergency responder selection skip actors that can no longer act.

### A15 - Core attributes own their derived rules

**Status: implemented for the authored roster**

Character profiles own one typed 1-5 set of Strength, Dexterity, Grit, and
Charisma ratings. Scenario schema 8 no longer authors initiative or movement
allowance beside those ratings. Application assembly derives initiative and
movement through `CharacterDerivedStatistics`, while the Domain-owned opposed
displacement record includes Strength in its complete recorded formula.

The wound threshold remains a small, separately authored incapacitation rule;
Grit does not manufacture an HP pool. Its typed status-resistance modifier is
the input boundary for later bleeding, concussion, and status resolvers.
Charisma has the equivalent social-check boundary but receives no unrelated
combat effect while social resolution is absent.

### A3 — Reduce central change hotspots by extracting cohesive services

**Priority: high, incremental**

**Status: partially resolved 2026-08-14.** Blast, displacement, equipment,
projectile, thrown-explosive, party, and progression behavior already has
focused sessions. Gameplay startup is now split into explicit bootstrap, world,
session, binding, and interface stages with one teardown path. HUD choice state
and generated texture ownership have moved into small lifecycle objects. The
central session outcome switch and the remaining HUD feature drawers should be
extracted only along proven behavior seams.

`GameplaySession` is the authoritative aggregate and should remain the owner of
actor state and journal ordering, but outcome-specific validation and
application should move behind focused internal collaborators. Start with
displacement and blast consequences; do not perform a broad rewrite.

`GameplayScenarioAssembler` should be split into schema validation, definition
factories, and assembly orchestration. Runtime presentation must consume the
assembled definitions rather than call assembler factory methods.

`GameplayHud` should retain top-level layout and interaction state while
feature drawers own displacement, hotbar assignment, guidance, dialogue, and
bug-report panels. `GameplayController` remains the scene composition root, but
feature binding should move into small installers as dependencies stabilize.

### A4 — Unify blast policy and broaden blast subjects

**Priority: high, after the close-quarters definition migration**

Projectile and thrown-explosive adapters currently duplicate blast exposure
policy and primarily enumerate actors. Introduce one blast query/result port
that can report combatants, destructible props, and later vehicles. A shared
application consequence service should translate recorded exposure into
authored wound, integrity, and movement effects.

### A5 — Finish inventory semantics before relying on consumables

**Priority: high**

**Status: resolved 2026-08-12.** Schema 10 authors a positive starting
quantity for each consumable stack. `GameplaySession` owns current quantity and
projects it through immutable actor inventory snapshots. A committed throw
records its exact before/consumed/after transition beside the frozen throw
evidence; action validation rejects missing, duplicated, stale, mismatched, or
overdrawn consumption before applying budget or inventory state. Preparation,
preview, and cancellation remain mutation-free. Shared availability, HUD
labels/tooltips, Combat Diagnostics, replay, and bug reports read that same
state rather than maintaining presentation counters.

**Original finding:** inventory items identified a grenade definition without
quantity, charges, or a consumed-item record. The corrective requirement was
authoritative stack state with before/after evidence in the same committed
action while keeping preview and cancellation side-effect free.

### A6 — Connect progression to runtime ownership and persistence

**Priority: medium**

**Status: runtime ownership resolved 2026-08-12; durable save/load remains.**
`GameplayPartyProgressionSession` now composes one identity-bound progression
aggregate for every authored player-party member and captures progression,
equipment, and wounds without allowing one actor to be persisted through
another character's identity. Bug reports project those per-character snapshots
alongside selected and command authority. A durable cross-launch store and the
player-facing advancement UI remain separate delivery work.

### A7 — Eliminate duplicate scenario assembly

**Priority: medium**

Scenario content is assembled during loading and then assembled again after
presentation grounds actor poses. Establish a single authoritative assembly
pass. If grounding is required, make it an explicit spatial adaptation stage
whose resolved poses are inputs to that pass, or record a deterministic pose
override without revalidating unrelated content.

### A8 — Improve diagnostics completeness

**Priority: medium**

Every new journal kind needs a purposeful diagnostic formatter. Emergency
reaction changes currently risk appearing only as a generic entry kind. Add a
small completeness test that fails when a journal entry kind lacks either a
specific formatter or an explicit generic-policy declaration.

### A9 — Put fast validation before full platform builds

**Priority: immediate delivery hygiene**

**Status: resolved.** The branch-preview workflow validates tracked source and
JSON, runs the complete EditMode and PlayMode suites, verifies that tests leave
the workspace clean, and only then builds WebGL. Because license and private
asset credentials are intentionally unavailable to forks, trusted
contributions must be pushed to a repository branch before merge so this gate
can run.

The WebGL build is valuable but too slow to be the first signal. CI should run
conflict-marker and JSON checks, Unity script compilation, and EditMode tests
before platform builds. Presentation EditMode tests should include assembly
boundary and content-assembly coverage. Windows and WebGL builds remain release
checks.

### A10 — Keep support-hand IK authored and weapon-local

**Priority: preserve while adding weapon poses**

The accepted rifle pose includes an authored support-hand correction. Weapon
presentation content is user-authored and must not be rewritten to satisfy a
generic pose assumption. If this scalar correction grows into a richer system,
author a weapon-local support-grip transform (position, rotation, and weight)
rather than deriving an IK target from camera motion or the current animated
hand. Pose tests should protect data flow and stability, not impose one model's
orientation on another.

### A11 — Reaction time is authoritative

**Status: resolved before adding another emergency-triggering projectile**

Projectile time is expressed in the Application layer using AP as the current
turn-time proxy. Each launch freezes both the normal-turn allowance and its
post-cost AP. Remaining attacker AP commits pre-reaction travel, then a
side-effect-free segment prediction determines whether impact falls inside the
next normal turn. Reaction AP is the predicted fraction converted back to whole
AP with ceiling, bounded to one through the normal allowance. All responders act
inside that one shared interval; the projectile is queried again and advanced
once after the response pass so reactions can change its collision. Presentation
only renders the committed position and forward predicted endpoint.

### A12 — Target response owns encounter initiation

**Status: resolved for immediate-fire weapons**

An immediate exploration shot no longer enters turn mode merely because a
weapon discharged. The ray adapter preserves the stable actor or level-entity
identifier, while Scenario content authors whether attacking that target starts
an encounter. The Application layer owns that lookup and records exploration
discharges at zero AP; Presentation only requests the mode transition when the
target policy requires it. Unconfigured geometry remains inert. Projectile and
blast-driven encounter policy remains separate because their delayed threat can
involve subjects other than the pointer target.

### A16 - Cache silhouette evidence and compare normalized exposure

**Priority: immediate correctness and WebGL performance**

**Status: resolved 2026-08-12.** Exposure queries are cached by observer,
target geometry/stance, and world revision. Enemy movement compares final hit
chance (or normalized visible fraction when no attack exists), then authored
range preference and movement cost. Unequal-raster regressions are covered.

The silhouette raster is materially more accurate than the earlier fixed sample
set, but pointer acquisition currently rebuilds it every `LateUpdate`. Each
capture allocates projection collections and performs one physics visibility
query for every painted cell. Exposure must be cached by observer pose, target
pose and stance, authored target-region geometry, and world-state revision.
Pointer motion over the same target must not repeat an unchanged world query.

Adaptive projected bounds also mean raw painted-cell counts are not comparable
between viewpoints. Enemy movement must rank attack positions by final hit
chance, then preferred range and movement cost. Normalized visible fraction is
the fallback score when no attack definition is available. Tests must include
candidates whose larger raw visible count represents a smaller exposed share.

### A17 - Finish the generic displacement seam before toppling

**Priority: immediate before toppling**

**Status: resolved 2026-08-12.** The bypass APIs and Throw-specific contracts
were retired. Canonical actions now freeze the complete budget/equipment
transition and authoritative prop pose, posture, and applied result policy;
presentation only replays that result.

Production uses the canonical `TryDisplaceAction` path, but public
`TryThrowProp`, `TryPushProp`, and `TryThrowCombatant` methods still commit
records without the ordinary action, budget, equipment, and outcome path. Tests
exercise those bypasses and therefore preserve two definitions of a successful
displacement. Retire them and test only the canonical atomic command.

Common `Throw*` contracts now describe Push and Throw and must move to generic
`Displacement*` vocabulary. Toppling must not be a presentation-only rotation:
the authoritative prop snapshot and committed displacement outcome must freeze
the previous and resulting pose/posture, including the result policy that was
applied. Presentation updates collision, cover, and navigation only from that
recorded result.

### A18 - One blast policy and truthful regional injuries

**Priority: high after the displacement seam**

**Status: resolved 2026-08-12.** Grenades and projectiles share one blast world
query and consequence resolver for actors and destructible props. Records
preserve distance, occlusion, falloff, exposure, and actual injury region;
unresolved regions remain explicitly unlocalized.

Thrown explosives currently use binary obstruction exposure and can enumerate
responsive level entities, while projectile blasts independently implement
distance falloff and enumerate actors. Both consequence paths silently record
actor blast wounds as torso wounds. This is incompatible with the six-region
HUD because the interface presents an invented location as authoritative fact.

Introduce one blast query/result port and one Application consequence resolver
for actors, destructible props, and later vehicles. The record must freeze the
common distance, occlusion, falloff, and affected-subject evidence. Actor blast
injuries must either resolve a real body region from the blast origin or be
stored as explicitly unlocalized injuries; they may not default to Torso.

### A19 - Project availability and diagnostics from authoritative services

**Priority: high before adding more hotbar actions**

**Status: resolved 2026-08-12.** Hotbar display and activation consume the same
typed inventory-power availability. Equipment switch readiness and its complete
combined cost are owned by `GameplayEquipmentSession`. Combat diagnostic text
is projected from immutable action/journal records by one Application
formatter, with completeness tests for all outcome types and journal kinds.

The HUD rebuilds weapon, consumable, and equipment affordability from raw actor
state even though focused Application sessions own commit readiness. Follow the
existing displacement pattern: project typed availability returned by the
owning use case, including the rejection reason and resolved combined cost.

Diagnostics must similarly derive from immutable records through Application
formatters. Presentation controllers should not each reconstruct formulas as
ad hoc strings. Add a completeness test requiring every action outcome and
diagnostic-relevant journal kind to have a specific formatter or an explicit
non-diagnostic declaration.

### A20 - Assemble logical content once

**Priority: medium, bounded composition cleanup**

**Status: resolved 2026-08-12.** Content loading performs the sole logical
assembly/validation pass. Unity grounding now applies a bounded resolved-pose
adaptation to the compiled scenario without reparsing content or rebuilding
unrelated definitions.

Default content is assembled during loading, then the whole scenario is
assembled again after Unity grounds actor transforms. Compile and validate the
logical definitions once. Grounding is a separate spatial adaptation stage
that supplies resolved starting poses without reparsing or revalidating
unrelated content.

### A21 - Risk-based coverage gates

**Priority: continuous**

**Status: current gate resolved 2026-08-14.** The listed regressions have
focused tests. The local gate passes 536 EditMode tests. PlayMode sustains the
default gameplay session for 180 frames and separately boots and tears down
every playable committed level. CI runs both suites before the WebGL build.
This remains a continuous requirement for future gameplay slices.

The repository has broad Domain/Application and Presentation EditMode coverage,
a sustained-frame default-content PlayMode lifecycle smoke, and CI gates before
WebGL. Coverage remains risk-based rather than optimized for a line percentage.
New mechanics must add focused authoritative-state, replay, presentation seam,
and lifecycle coverage in proportion to their failure risk.

### A22 - Author the visual language through presentation profiles

**Status: resolved 2026-08-12.** Visual upgrades now consume three explicit
Presentation-owned authoring boundaries instead of accumulating constants in
scene controllers.

- `GameplayVisualTheme` owns post-processing, cel-band response, actor surface
  response, outline widths, contact grounding, and tactical-transition cadence.
- `LevelDocument.environment` owns portable atmosphere, key-light, and practical
  fixture values used by both editing and play. `LevelLightingCatalog` now owns
  only Unity prefab references for ambient effects pending their portable
  placement schema.
- `SurfacePresentationCatalog` owns concrete, wood, metal, and actor material
  response together with their impact prefab, scale, lifetime, and decal
  treatment. Level archetypes select a stable surface ID.

Runtime classes are now projectors of those assets. Environment styling caches
cel materials by source material, cutout mode, and surface variant so assigning
different responses does not create one material per level instance or let the
first archetype silently determine all later instances. Normal maps remain out
of the cel shadow band to preserve the clean geometric gradient; restrained
specular and edge response provide material separation without restoring the
old splotchy shadow artifacts.

The same boundary applies to effects. Surface impacts, muzzle light, projectile
effects, environmental dust/haze, grounding, and transition timing are authored
data or prefab references. Presentation may raycast to orient an impact mark,
but it cannot alter the recorded attack, collision, wound, or turn result.
Generated menu art is a replaceable Resources asset; the menu remains native
Unity interaction and contains no environment or gameplay policy.

### A23 - Weapon props own their attachment sockets

**Status: resolved 2026-08-12.** Every equipped prop is now a project-owned rig
prefab whose root is the right-hand mount and whose children author the visual,
muzzle, support-hand pose, and optional support-elbow hint. The weapon catalog
selects that rig but no longer duplicates model-local grip rotations, barrel
axes, muzzle positions, or support-hand offsets.

Humanoid animation supplies posture and motion. After animation and bounded
upper-body aim, `WeaponRigIkDriver` blends the support hand to the rig's exact
position and rotation and applies the authored elbow hint. Unequip preserves
the last pose long enough to blend the IK influence out; no frame-relative hand
nudge or camera-dependent clamp remains. Rifle and launcher sockets are baked
from the model studio's matching reference poses, and the rig inspector exposes
the actual socket transforms for direct scene calibration.

The generated weapon layer is now part of `DefaultActorAssetGenerator` instead
of an unowned manual addition that regeneration could erase. Current rifle
idle, walk, run, strafe, and fire states use the imported shooter pack through
an upper-body locomotion blend tree. Reload, grenade toss, hit reaction, jump,
and turn clips remain source motions for the gameplay states that will own them;
they are not played speculatively by the locomotion presenter.

### A24 - Player control is party-owned, not singleton-owned

**Status: resolved 2026-08-12.** Schema 12 authors an ordered player party with
stable, unique character identities. `GameplayPartyControlSession` owns
selection in exploration and follows friendly initiative in turn mode; enemy
turns expose no command actor. Presentation retargets camera, cutout, movement,
stance, targeting, HUD, hotbar, equipment, consumables, displacement, and
projectiles as one control transaction. Every party member keeps a persistent
held-weapon presenter and its own reassigned hotbar layout.

Encounter-opening actions commit for their actual initiating actor before
initiative changes. Enemy detection consumes Unity exposure evidence but
Application selects the actual visible party target; turn decisions select the
nearest capable hostile from authoritative poses. Encounter relevance and
defeat evaluate the whole party, including one-way responsive hostility, and
diagnostics record the detected, selected, commanding, wounded, and progressing
character IDs explicitly.

The production scenario now proves the boundary with two separately authored
characters rather than test-only roster data. `GameplayPartyHud` is a focused
presentation surface outside the already-large command HUD: it projects an
Application-built roster model, supports click and Tab selection in exploration,
blocks manual selection when initiative owns control, and displays each member's
budget and wounds without caching gameplay state. Oren's higher Dexterity gives
him the first friendly turn, followed by Mara, and the routing smoke verifies
that control transactions retarget between both characters before the enemy
turn. The full EditMode gate passes 429 tests.

## Execution sequence

1. Add fast CI compile and EditMode gates.
2. Introduce authored displacement definitions and migrate prop Push without
   changing its player-facing behavior.
3. Add hand requirements and atomic auto-stow planning.
4. Move target acceptance into Application and make Presentation submit stable
   candidate IDs only.
5. Add weight-limited Throw for props and combatants, followed by toppling.
6. Add opposed combatant displacement.
7. Add authored knife attacks.
8. Generalize blast effects and consumable quantities. **Complete.**
9. Integrate progression persistence. **Runtime party ownership complete;
   durable cross-launch storage remains.**
10. Resume the drone slice only after close-quarters exit criteria pass.

## Review gates for every gameplay slice

- The action is discoverable without a valid pointer target.
- Content, not a presenter or action ID string, defines cost and eligibility.
- Application returns structured rejection reasons.
- Preview/cancel perform no authoritative mutation or random sampling.
- Commit validates the complete action atomically.
- The journal contains enough evidence for diagnostics and replay.
- Replay performs no Unity world query.
- Concrete VFX and presentation cadence are authored assets/data rather than
  particle-system recipes embedded in gameplay presenters.
- Domain and Application tests run without entering Play Mode.
- WebGL-specific constraints are respected by the core implementation.

## Explicitly deferred

- A general-purpose ability graph or scripting language. Typed authored
  definitions are sufficient until repeated mechanics justify a richer model.
- A dependency-injection framework. Small installers and explicit constructors
  are preferable at the current scale.
- A full `GameplaySession` rewrite. Focused extraction should follow feature
  seams and preserve journal ordering.
- The deployable drone. It remains valuable, but adding another initiative
  participant before completing action definitions, equipment requirements,
  and close-quarters targeting would deepen the current hotspots.
