# Architecture and Separation-of-Concerns Review

**Reviewed:** 2026-08-11; follow-up audits 2026-08-12, 2026-08-14, 2026-08-15,
2026-08-17, 2026-08-18, and 2026-08-20
**Scope:** the repository as a whole, with additional attention on the recent
emergency-reaction, projectile, explosive, party-persistence, displacement, and HUD
work.

This review is the durable record of the architecture pass. It is intentionally
separate from the roadmap: this document records constraints, findings, and
decisions; the [roadmap](ROADMAP.md) records delivery order and completion.

## Executive assessment

The foundation is sound. The assembly graph points in the intended direction:
Domain is platform-neutral, Application depends on Domain, and Presentation is
the Unity adapter. Authoritative actions generally freeze their inputs and
outcomes for replay instead of attempting to reproduce Unity physics.

The primary risk is no longer a missing foundation. It is **regression of the
focused boundaries as features accumulate**. The reviewed composition,
presentation, session, scenario-import, and validation hotspots now delegate to
explicit collaborators, and the repository gate freezes their coordinator
budgets. New work must extend those owners instead of rebuilding central rules
engines.

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

The reviewed hotspots were subsequently decomposed on 2026-08-18.
`GameplaySession` retains authoritative ordering while delegating
outcome-specific validation and application; HUD feature drawers and gameplay
installers have focused owners; and editor commands, scenario policy families,
and level-validation rules no longer accumulate in their former coordinators.
Durable cross-launch party-state storage remains explicitly deferred.

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

The current risk profile is bounded and executable:

- The reviewed coordinators now measure `GameplaySession` (1,315 lines),
  `LevelEditorController` (1,447), `GameplayController` (949),
  `GameplayHudRenderer` (1,071), `GameplayScenarioAssembler` (151), and
  `LevelValidator` (183). The repository gate assigns each a tighter growth
  budget so future behavior must extend focused collaborators rather than
  silently regrow a god file.
- The initial terrain-paint implementation coupled serialized numeric values to
  UI ordering and mesh-color projection. This follow-up resolves that risk with
  the explicitly numbered Domain-owned `TerrainMaterialKind` contract; UI
  options and rendering now map named values, and tests freeze the serialized
  numbers. New values must append or arrive through a schema migration.
- `TerrainHeightLevelEditorTool` now coordinates both height sculpting and
  material painting. Its stroke lifecycles are cohesive today, but the name and
  responsibility should become a generic terrain-brush coordinator or two
  focused tools if erosion, foliage, masks, or other brush families are added.
- Cross-launch party work must preserve the same ports-and-adapters
  boundary: Application owns a versioned party-save use case and validation;
  Presentation supplies PlayerPrefs/browser and filesystem adapters. Gameplay
  controllers must not directly serialize authoritative party state.
- Planning text had drifted behind implemented interaction-point,
  destructible, batch-transform, and three-axis work. Roadmap statements should
  be treated as delivery records, not as substitutes for executable boundary
  gates.

No architecture blocker requires reverting the current editor slice. Durable
party equipment save/load is complete; wounds and action budgets are transient
mission state and deliberately start from authored scenario defaults. The
earlier advancement UI and
point model were removed on 2026-08-17 after product clarification: Character
Creator authors the starting character before a level, and there is no runtime
progression or player-side spending. The drone is not
the next overall slice. Destructible toppling must first become a complete live
action rather than only a record/replay capability: production displacement
does not yet resolve `Topple`, prop eligibility is not authored, and the depot's
actions allow no result policies. The next project slice is end-to-end
destructible/toppling completion plus a published pile fixture. Knife/action
animation completion and shared simulation groundwork follow before the drone;
richer viewport transform tools can proceed alongside those gameplay slices.

### 2026-08-17 pre-encounter stabilization checkpoint

The stabilization goal prompted by the repository-wide follow-up review is
complete. It deliberately preserved current encounter behavior while reducing
the cost and risk of the next encounter slice:

- successful cloud-draft mutations now retain their authoritative returned
  identity, revision, and local state even when the best-effort list refresh
  fails; refresh failure is a warning rather than a failed write;
- Supabase authentication, document, draft, and RPC responses cross centralized
  parsing and validation boundaries, and every request finishes through one
  success, failure, or cancellation path;
- browser and desktop JSON import share reusable text-transfer seams for both
  level and character documents;
- pull-request/default-branch CI separates fork-safe validation from jobs that
  require licensed private assets, and preview publication, concurrency, and
  deletion use one collision-resistant slug-plus-hash identifier;
- gameplay characterization is partitioned by lifecycle, and runtime ownership
  is divided among enemy registry, exploration evidence orchestration,
  combat-turn execution, outcome presentation, application-owned turn
  lifecycle, scenario validation/combat assembly, and an explicit control
  router;
- HUD ownership is divided among binding/input, model projection,
  layout/hit-testing, rendering, and style resources while `GameplayHud`
  remains the Unity-facing facade;
- the level-editor GUI consumes six capability interfaces instead of one broad
  action surface, and a tested session-lifecycle scope owns symmetrical event
  release; and
- `GameplayDisplacementSession` is now the atomic action/journal boundary.
  Availability/target policy, destination and path evidence, prop and contest
  resolution, pin transitions, and commit validation have focused Application
  owners. Replay coverage freezes the exact toppled prop and pin state and
  rejects duplicate authoritative sequences without rerolling or remutating.

No patrol, suspicion/awareness, sound detection, new line-of-sight policy,
scoped initiative, encounter authoring, or encounter UI was added in this
checkpoint. Those belong to the next goal. Unity supplies spatial evidence and
presentation; Application owns awareness, participant scope, initiative
entry/exit, and recorded decisions; authored content owns sensing and patrol
policy.

### 2026-08-18 contract, lifecycle, and allocation follow-up

The repository-wide follow-up closed several cross-layer correctness gaps and
put explicit invalidation around the highest-frequency read paths:

- scenario authoring, publish validation, and runtime loading now share timing,
  mobility, momentum, starting-speed, and objective-HUD invariants; a complete
  authored-level regression crosses all three boundaries;
- gameplay lifecycle, party-control, level-session, and editor-workspace
  observers publish only after commit, attempt every subscriber, and preserve
  committed state when a projection fails;
- `GameplaySession.Revision` is monotonic across authoritative mutations.
  Actor state owns cached immutable full and inventory snapshots, while
  targeting consumes an allocation-free state snapshot;
- HUD projection reuses its object graph until a session, binding, route,
  availability, warning, pending-action, or hotbar input changes. Targeting
  reuses its target-region buffer and preview when exposure evidence is
  unchanged;
- mutable actor/objective implementation moved behind focused Application
  state collaborators. Runtime-editor Rigidbody settling moved behind a
  disposable Presentation coordinator that restores temporary colliders and
  transforms before projected-world teardown; and
- the fast repository gate now freezes all runtime/test assembly contracts,
  Unity source/meta pairing and GUID uniqueness, neutral-source boundaries,
  and a bounded production-file growth budget. WebGL publication validates the
  generated browser artifact before it can replace a branch preview.

### 2026-08-18 reviewed-hotspot closure

Every verified finding from the repository-wide god-object and separation pass
has a concrete owner and a regression gate:

- `GameplaySession` delegates action-outcome validation and application while
  preserving one authoritative mutation and journal-order boundary;
- `LevelEditorController` exposes focused GUI capability adapters, while cloud
  commands and navigation return `Task` and carry cancellation/generation race
  protection;
- `GameplayController` binds through an ordered installer pipeline with
  rollback, order, and failure coverage;
- `GameplayHudRenderer` retains top-level render state and layout while hotbar,
  guidance, status, and modal feature drawers own their presentation branches;
- `GameplayScenarioAssembler` is a thin coordinator over actor, inventory,
  displacement, objective, prop, vehicle, combat, and attack-response policy
  assemblers;
- `LevelValidator` retains the public service/facade, with every concrete rule
  in its own file and a completeness test for default registration; and
- HUD matrix restoration, control routing without enum ordinals, controller
  binding order, router status clearing, cloud cancellation/races, and
  Presentation `async void` absence have focused tests or source gates.

The remaining large files are authoritative aggregates or composition roots,
not catch-all feature owners. Repository budgets, one-rule-per-file validation,
and the Presentation `async void` gate make that distinction enforceable.

The Unity EditMode/PlayMode suites and player builds remain licensed-host or
CI-owned. A licensed Windows host passed both complete suites plus WebGL and
Windows builds on 2026-08-18. The browser-artifact smoke then validated the
actual Unity 6 `.unityweb` output. It catches missing/empty loader, data,
framework, WebAssembly, canvas, startup, and static-reference failures; it does
not replace an eventual automated deployed-browser interaction pass for pointer
input, storage, import/export, and responsive editor layout.


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

## Cloud draft library decision — 2026-08-15

Private cloud drafts use the same ports-and-adapters direction as the rest of
the editor. Application owns immutable draft identity, name policy, revision
conflicts, records, and repository use cases. Presentation owns Supabase auth,
HTTP transport, coroutine bridging, operation cancellation, navigation, and UI.

A cloud draft UUID never changes. Its user-facing name is independent and
unique per account. Saves carry an expected revision; the database commits the
document and immutable revision snapshot atomically or reports a conflict.
Local PlayerPrefs storage is recovery, not the cloud-library identity.

`LevelDraftLibraryCoordinator` is the shared Presentation state boundary for
menu and editor operations. UI renders its immutable Application summaries and
submits intents; it does not construct Supabase requests. Async navigation is
cancellable and must verify the application mode before opening gameplay or an
editor. Cloud play-test uses Runtime validation and sandbox semantics rather
than bypassing the committed-level readiness boundary.

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

Exact tactical ties finish with stable actor identity for target selection and
lexicographic route geometry for movement, so caller enumeration order cannot
change the recorded decision. Projectile attacks use the same authoritative
projectile session, journal, impact-cycle, diagnostics, and presentation path as
player launches rather than being silently excluded from enemy affordability.

The first tactical-confidence extension preserves that boundary. Application
scores frozen exposure for every capable party target and owns an authored
minimum acceptable hit chance. Unity supplies bounded route/exposure options
only when the current shot is obstructed, out of reach, or below that threshold;
Application moves only for a strictly improved firing solution and otherwise
records the legal fallback shot. Future investigation, reciprocal-cover, and
squad coordination work is tracked in [ENEMY_AI.md](ENEMY_AI.md).

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

**Status: resolved for the reviewed hotspot set on 2026-08-18; continuous.**
Blast, displacement, equipment, projectile, thrown-explosive, and
party-persistence behavior have focused sessions. `GameplaySession` remains the
authoritative aggregate for actor state, mutation order, and journaling, while
outcome-specific validators and appliers own feature policy.

Mutable actor/objective state and snapshot invalidation have focused
Application owners. Physics-assisted editor settling owns cleanup in a
disposable Presentation coordinator. The level-editor GUI is divided across six
capability adapters, and cloud commands expose `Task`-returning services with
race and cancellation coverage.

Gameplay startup is an ordered installer pipeline with one teardown path and
rollback tests. The HUD retains top-level layout and interaction state while
focused drawers own its feature branches. Scenario import is a 151-line
coordinator over focused policy-family assemblers, and the 183-line level
validator facade composes one concrete rule per file.

The repository gate now enforces tighter budgets for each reviewed coordinator,
rejects Presentation `async void`, and enforces one level-validation rule per
matching source file. A future split should still follow proven behavior seams;
this status is not permission for a broad framework rewrite.

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

### A6 — Connect authored party identity to runtime persistence

**Priority: medium**

**Status: corrected 2026-08-20.** `GameplayPartySave` is a versioned,
exact-roster Application contract keyed by the stable identities authored in the
pre-level Character Creator. It validates equipped-item ownership and captures
only mutable equipment state. Schema 3 and the explicit schema 1/2 migration
path discard legacy wounds and action budgets, so every mission starts with the
scenario's authored combat state. The PlayerPrefs adapter supplies local
browser/desktop durability, and equipment changes flush immediately. Persistence
observers are isolated from storage outcomes, so a subscriber exception cannot
turn a successful read or write into a reported storage failure.
The previously implemented points, bonuses, advancement options, progression
session, save fields, diagnostics, and runtime drawer were based on a mistaken
product assumption and have been removed. Authored attributes, skills, talents,
appearance, and starting loadout remain creator-owned inputs, not spendable
runtime state.

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

**Status: resolved 2026-08-12; allocation follow-up 2026-08-18.** Exposure queries are cached by observer,
target geometry/stance, and world revision. Enemy movement compares final hit
chance (or normalized visible fraction when no attack exists), then authored
range preference and movement cost. Unequal-raster regressions are covered.

Pointer acquisition now also reuses its converted target-region buffer and
`TargetAcquisitionPreview` when the cached exposure, accuracy definition,
distance, and contact reach are unchanged. Actor pose reads no longer construct
or sort inventory snapshots.

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

**Status: current gate updated 2026-08-18.** The listed regressions have
focused tests. The local gate passes 854 EditMode tests and 10 PlayMode tests.
PlayMode sustains the default gameplay session for 180 frames and separately
boots and tears down every playable committed level. CI runs both suites before
the WebGL build. This remains a continuous requirement for future gameplay
slices.

The latest additions cover the complete authoring-to-runtime scenario
contract, committed-state visibility under throwing observers, actor snapshot
reuse, HUD projection invalidation, and unchanged targeting-preview reuse. Fast
source CI also tests the WebGL artifact validator; the branch workflow runs it
against the actual generated player before publication.

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
turn. The current full EditMode gate passes 854 tests.

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
9. Integrate party persistence. **Corrected and complete:** versioned,
   identity-bound equipment-only storage is live; combat wounds and budgets are
   mission-transient, and runtime progression and its advancement surface do not
   exist.
10. Complete live toppling resolution, authored prop eligibility, and the
    published destructible-pile verification fixture. **Implemented
    2026-08-16; full Unity runner and hands-on fixture acceptance remain.**
11. Add first-class toppled-prop pinning, an authoritative pinned actor state,
    and an atomic Push Off/escape action with exact replay restoration.
    **Complete 2026-08-17:** pin establishment/release, directional Push Off,
    destination evidence, atomic commit validation, and exact prop/pin replay
    restoration are covered through focused Application collaborators.
12. Finish knife, reaction, equipment, reload, grenade, pinned struggle,
    push-off, get-up, and turn presentation through gameplay-owned semantic
    animation states.
13. Migrate remaining action families to canonical prepare/commit transitions
    and establish deterministic trajectory, repro, minimization, fuzzing, and
    seed-baseline infrastructure.
14. Resume the drone only after those existing-system exit criteria pass, then
    run the full alpha adversarial capstone with the drone included.

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
  participant before end-to-end toppling, close-quarters animation, and the
  shared simulation foundation are proven would deepen the current hotspots
  and force the final alpha gauntlet to be rerun around unfinished seams.
