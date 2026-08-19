# Pre-Training Simulation Foundations: Codex 5.3 Execution Roadmap

## Purpose and authority

This document is the implementation handoff for the two remaining gameplay
foundations that must exist before broad tactical training or optimizer work:

1. portable, authoritative tactical context, proven end to end by Ambush; and
2. banked personal-turn action points, initially tuned to start 4 / income 4 /
   maximum held 6.

Follow this document together with `SIMULATION_ARCHITECTURE.md` and the active
section of `ROADMAP.md`. If code contradicts an assumption here, stop that
slice, record the contradiction in this document, and repair the plan before
changing production behavior. Do not invent a compatibility seam merely to
keep moving.

This is not permission to begin broad AI training, implement every tactical
meta, redesign encounter presentation, or regenerate lower-priority generator
fixtures. The completion point is a focused player-validation build containing
the generic context route, one Ambush rule, and the 4/4/6 economy.

## Non-negotiable invariants

- Canonical state and immutable evidence decide gameplay. Unity supplies
  spatial measurements and presentation only.
- Preparing an action freezes all contextual facts used by its resolution.
  Commit validates the frozen record; it does not query current LOS, awareness,
  physics, transforms, or presentation state again.
- Attack consequences resolve before the attack's sound changes awareness.
- Replay, diagnostics, and headless execution consume recorded context and
  applied consequences. They never ask whether an old attack qualifies now.
- AI valuation may rank recorded or projected outcome features. It may not
  create authoritative accuracy, damage, reaction, sound, cost, or initiative
  modifiers.
- A capability that is reachable from content must remain fail-closed across
  candidate construction, legality/evidence, pure reduction, domain events,
  replay, headless execution, and live installation.
- Target kind is part of the route. Actor-target Ambush support must not imply
  destructible-target Ambush support.
- AP replenishes only when a normal personal turn canonically becomes active.
  Encounter onset/completion, voluntary-mode toggles, selection, replay, load,
  and presentation installation cannot grant AP.
- Emergency reactions keep their separate allowance and do not bank normal AP.
- Existing user work and the untracked root `AGENTS.md` are out of scope.

## Repository facts already verified

- `GameplayTacticalSubjectCatalog` already discovers Actor, Objective,
  DestructibleProp, and Vehicle subjects with tactical affordances.
- `GameplayReachableInputEnumerator`, `GameplayCapabilityRegistry`, and
  `GameplayCapabilityCoverageValidator` already provide scenario-load and
  fail-closed route validation.
- `GameplayAttackSession.TryPrepareResolve` currently freezes exposure, seeded
  rolls, wounds, and budgets into `GameplayActionRecord` before commit.
- `GameplayActionRecord` currently has request, cost, previous/resulting budget,
  and outcomes, but no generic context or applied-modifier record.
- Actor attacks use `AttackResolutionRecord`; non-actor direct fire uses
  `WeaponDischargeRecord`. Do not accidentally apply actor-only Ambush rules to
  world-position or destructible discharge.
- Encounter awareness is canonical in `GameplayEncounterStateSnapshot` and is
  keyed by authored enemy actor. It is not yet a fully symmetric per-observer
  knowledge model.
- Headless reverse sight can be produced by
  `GameplayHeadlessEncounterEvidence.CaptureSight` using destructible-aware
  spatial evidence.
- Live actor attacks enter through `GameplayAttackController` and
  `TargetAcquisitionPresenter`; AI actor attacks ultimately use the same
  `GameplayAttackSession` resolution route.
- `GameplayEncounterActionTransition.BeginAfterCommittedAction` is currently a
  Presentation helper. It can begin encounter scope after an action, but that
  path does not itself record attack sound evidence or update target awareness.
  A headless attack transition also does not automatically receive that
  follow-up. Post-action sensing/encounter consequences need Application
  ownership before Ambush ordering is complete.
- Turn replay projects journal entries and canonical checkpoints. There is no
  independent replay-save schema to mutate for this work.
- `GameplayTurnTransitionReducer` currently replaces the next actor's AP with
  `TurnActionPointAllowance`, and encounter completion calls
  `RefreshAllActors`. `GameplayCoreTransitionReducer` duplicates the normal
  encounter refresh behavior. Both must converge on one economy rule.
- Live gameplay still has a mutable lifecycle path through `GameplaySession`
  and `GameplayTurnLifecycle` in addition to the pure semantic reducers. The
  mutable path refreshes one/all budgets through `IGameplayTurnLifecycleHost`.
  A change to only one path is a live/headless parity bug.
- `GameplayLifecycleTransitionReducer` also refreshes all actors when voluntary
  turn mode exits and when a voluntary world cycle completes, and refreshes the
  interrupted actor when emergency reactions complete.
- Emergency begin currently overwrites an actor's normal budget with the
  emergency allowance. No separate canonical field preserves the interrupted
  normal budget; exact restoration must be added before banked AP is safe.
- `GameplayCurrentCapabilityCatalog.RegisterComplete` currently attaches the
  same general implementation IDs to every discovered profile. It validates
  broad route presence but cannot prove a particular tactical-rule predicate or
  consequence is supported.
- Scenario content currently requires exact schema version 15 in
  `GameplayScenarioAssembler`; unlike levels, scenarios have no general
  migration chain. A schema change must address authored and test content
  atomically or introduce an intentional scenario migration boundary.

## Required delivery cadence

Use the existing feature branch unless it has diverged or contains unrelated
tracked edits. Before every slice, check status and current branch. For each
slice below:

1. add or update the narrow tests that define the contract;
2. implement the complete production path for that slice;
3. run repository validation and relevant engine-free tests;
4. inspect the staged diff and commit only the slice;
5. push and require the hosted EditMode, PlayMode, WebGL smoke, and preview gates
   to pass before beginning a dependent slice.

Do not launch a second Unity editor while the project is open. Local direct C#
compilation is useful only if the editor-generated response files include the
current assembly graph. If they are stale, report that fact and rely on the
hosted Unity pipeline rather than treating unrelated missing-type errors as a
product failure.

## Phase A — Freeze the domain vocabulary before integration

### A1. Add immutable tactical context records

Create a focused Domain file, preferably
`Assets/GritGud/Domain/Gameplay/TacticalContext.cs`, containing value objects,
not services:

- `TacticalAwarenessBand`: Unknown, Unaware, Suspicious, Alert.
- `TacticalVisibilityRelation`: Unknown, Neither, AttackerOnly, TargetOnly,
  Mutual. Do not encode this as two loosely related booleans.
- `TacticalRangeBand`: Contact, Close, Effective, Long, Extreme. Preserve exact
  recorded distance separately where attack resolution already requires it.
- `TacticalContextSnapshot`: stable attacker ID, subject reference, capability
  signature, evidence/world revision, target awareness, visibility relation,
  attacker and target stance, range band, cover/exposure band, isolation band,
  nearby ally/threat counts, suppression/displacement flags, normalized sound
  signature, and resource-pressure values.
- `AppliedTacticalModifier`: stable rule ID plus explicit deltas or overrides.
  Start with accuracy delta percent, damage/wound delta, reaction permission,
  sound multiplier, AP-cost delta, and named outcome feature IDs. Fields that
  are unsupported in the first reducer must validate to their neutral value;
  never accept and ignore a non-neutral consequence.
- `ResolvedTacticalContext`: one snapshot plus an ordered, immutable list of
  applied modifiers and derived outcome-feature IDs.

Construction must reject empty IDs, undefined enums, non-finite numbers,
negative counts, duplicate rule IDs, duplicate outcome features, impossible
visibility/awareness combinations that the first model can prove, and modifiers
outside explicitly bounded ranges. Equality/validation helpers must compare
every authoritative field.

Do not put AI weights in these records. Do not reference Unity types.

### A2. Define declarative rule content

Add scenario-domain data for ordered tactical-context rules. The production
definition needs:

- stable rule ID and display/debug name;
- applicable semantic capability signature or explicit capability + required
  traits;
- accepted subject kinds;
- ordered predicates over supported context features;
- explicit consequences; and
- explicit outcome feature IDs.

The first authored rule is `rule.ambush.direct-attack.actor`. It applies only to
DirectAttack routes whose target kind is Actor. Minimum qualification:

- target awareness is Unaware; and
- visibility relation proves the attacker is not visible to the target.

Choose conservative initial consequences in authored data, not constructors.
If product tuning is not otherwise specified, use an accuracy-only proof with a
clearly named value and leave damage, reaction, cost, and sound neutral. The
architecture must represent those later consequence types, but the reducer must
reject unsupported non-neutral values until their authoritative semantics are
implemented.

### A3. Add pure rule evaluation

Create an Application-owned evaluator that takes a validated rule catalog and a
frozen `TacticalContextSnapshot`, then returns `ResolvedTacticalContext`.
Evaluation order is authored order followed by stable rule ID as a tie-breaker.
It must be deterministic, allocation-bounded for search use, and free of Unity,
clock, random, and global-state access.

Add Domain/Application tests for:

- neutral context produces no applied rules;
- qualifying Ambush produces exactly one stable applied rule and outcome
  feature;
- Suspicious and Alert targets do not qualify;
- target-visible and Mutual visibility do not qualify;
- Unknown never silently qualifies;
- unsupported consequences fail validation;
- input ordering cannot change the resolved result; and
- records reject duplicate or inconsistent evidence.

### A4. Commit gate

Commit message suggestion: `Define portable tactical context rules`.

Do not proceed if these records require Unity types, if modifiers can be
accepted but ignored, or if deterministic equality is incomplete.

## Phase B — Assemble identical live and headless evidence

### B1. Introduce one context-evidence boundary

Add an Application interface such as `IGameplayTacticalContextQuery` whose
input is canonical combat state plus a semantic candidate/request and whose
output is a `TacticalContextSnapshot`. The interface may request spatial facts;
the rule evaluator itself may not.

Do not put this interface on presentation objects and do not let it mutate
awareness. Evidence capture reads the pre-action state only.

### B2. Implement headless evidence first

Build the headless implementation from `GameplayCombatStateSnapshot`,
`GameplayHeadlessSpatialEvidence`, and
`GameplayHeadlessEncounterEvidence.CaptureSight` in both directions:

- attacker-to-target sight supplies attack exposure;
- target-to-attacker sight supplies visibility asymmetry;
- canonical encounter awareness supplies the target awareness band where the
  current model owns it;
- missing symmetric awareness must map to Unknown/Alert according to an
  explicit conservative rule and must not grant Ambush.

Derive cover/exposure from target-region samples and the resulting destructible
state. Destroying cover in a branch must therefore change later context.

### B3. Implement the live adapter through the same contract

Reuse `UnityTargetExposureQuery`/target-region sampling and current canonical
world revision. Add reverse target-to-attacker exposure capture; do not infer
visibility from renderers, cursor acquisition, movement colliders, or animation
pose. Pinned actors must continue using `ActorTargetProfileCatalog`.

The live adapter returns the same portable snapshot shape as headless. Add
comparison tests using equivalent authored geometry and canonical states.

### B4. Address asymmetric awareness honestly

The first Ambush proof may only target actors with canonical awareness state.
Do not fabricate player awareness so enemies can use Ambush. Record this as a
deliberate first-rule applicability limit. If symmetric observer knowledge is
required for the proof, promote awareness to a stable observer/subject relation
in its own migration slice before continuing.

### B5. Commit gate

Tests must cover standing, crouched, pinned, occluded, mutually visible,
destructible-opened LOS, and stale world revision. Commit suggestion:
`Freeze live and headless tactical evidence`.

## Phase C — Carry context through actor attack resolution

### C1. Attach context to the action record

Extend `GameplayActionRecord` with an optional Domain-owned
`IGameplayActionContext` contract implemented by Application's
`ResolvedTacticalContext`. This preserves the dependency direction: Domain
action/replay records own authoritative context identity, consequence values,
and canonical digest, while Application owns evidence capture and rule
evaluation. Existing non-contextual action constructors may omit context. Do
not add parallel Ambush-only fields to `AttackResolutionRecord` and
`GameplayActionRecord`.

Validate that context attacker, subject, capability, action request, and world
revision agree. Context is authoritative action evidence, not presentation
metadata.

### C2. Apply the first supported consequence

Extend attack hit-chance calculation with the recorded contextual accuracy
delta. The final formula and clamping order must be explicit and used by:

- preview/evaluation;
- seeded resolution;
- `AttackResolutionRecord` constructor validation;
- commit/outcome validators;
- diagnostics; and
- enemy/headless candidate projection.

Recommended order:

1. calculate geometric exposure chance;
2. apply distance accuracy as currently defined;
3. add the summed recorded contextual accuracy delta;
4. clamp once to the existing legal hit-chance range.

Do not mutate `TargetExposureSnapshot` to fake a bonus. Preserve geometric and
distance components so diagnostics and learning features remain intelligible.

### C3. Preserve prepare/commit ordering

`GameplayAttackSession.TryPrepareResolve` must:

1. capture the canonical pre-action state;
2. capture context evidence from that state;
3. evaluate declarative rules;
4. resolve seeded attack consequences using the applied modifiers;
5. construct the immutable action and projected resulting state; and only then
6. permit commit and encounter/sound follow-up.

Commit must reject context whose identities, evidence revision, modifier sums,
or attack result do not match the record. It must not rerun rule predicates.

### C4. Move post-action sensing and encounter onset into Application

Add an Application-owned committed-action consequence planner/coordinator. For
an attack with an authored sound signature it must prepare a deterministic
ordered transition sequence:

1. reduce the already-prepared attack using frozen pre-action context;
2. produce sound evidence for each eligible observer from the resulting world
   state and the attack's recorded origin/signature;
3. reduce awareness changes in stable observer-ID order; and
4. begin or expand encounter scope only from the resulting Alert transitions or
   an explicit authored attack-response rule.

The sequence identities and intermediate state hashes must be preserved in
headless trajectories and exact replay. Live presentation subscribes to the
committed domain events and may show the contact banner, but
`GameplayAttackController`/`GameplayEncounterActionTransition` must no longer be
the sole owner of gameplay follow-up.

Do not make the attack reducer query sound propagation. Sound evidence is a
separate prepared semantic transition so destructible-aware live/headless
queries remain explicit. Do not mark every survivor Alert merely because an
attack occurred; use the authored sound/awareness policy.

### C5. Keep non-actor direct fire explicit

Rifle discharge at a destructible or world point remains a contextual-neutral
DirectAttack route until a rule explicitly accepts that target kind. Coverage
tests must prove actor Ambush support does not leak into those discharges.

### C6. Record and expose outcomes

Update journal/action diagnostics to list frozen feature values, applied rule
IDs, each consequence, and outcome features. Avoid player-facing prose until
the gameplay rule is stable; combat diagnostics are sufficient for the first
playtest.

Replay timelines and world-state samplers should carry the action record
unchanged. Add assertions that replay seeks and forward crossings preserve the
same context object/data and never invoke an evidence query.

Headless trace digest/verification must include context and applied modifiers,
not only event type names, or parity can pass while tactical consequences
diverge.

Concretely, `GameplayTrajectoryStep` needs a deterministic transition/payload
digest (or exact canonical transition encoding) that includes every
authoritative context property. `GameplayExactReplay` must verify that digest in
addition to resulting state and event types. Merely placing context on the
transition is insufficient because the current replay verifier compares only
resulting state hashes and event type names.

### C7. Coverage gate extension

Do not add another blanket capability stage. Add a separate
`GameplayTacticalRuleCoverageValidator` (or equivalent real registry) keyed by
rule ID, exact capability signature, subject kind, predicate-feature IDs, and
consequence-feature IDs. It must report missing live evidence, headless
evidence, evaluator predicates, reducer consequences, replay encoding,
diagnostic projection, and outcome-feature projection. Feed its blocking report
into `GameplayCapabilityCoverageGate.RequireCurrent`/scenario assembly.

`GameplayCurrentCapabilityCatalog.RegisterComplete` is broad route metadata and
must not be cited as proof that a contextual rule is complete. It may be
refactored to register concrete implementation objects later, but that cleanup
is not required if the dedicated rule validator is substantive and fail-closed.

For reachable contextual profiles, scenario-load validation must prove:

- context query is installed for live and headless;
- every referenced rule consequence is supported by the reducer;
- replay and diagnostic encoding recognize the record; and
- actor and target kinds match the rule.

### C8. Commit gate

Required tests include qualifying/non-qualifying Ambush, deterministic seeds,
modifier tampering, stale evidence, exact replay, headless/live parity,
post-attack awareness ordering, and destructible discharge neutrality. Commit
suggestion: `Apply frozen context to direct attacks`.

## Phase D — Make tactical outcomes available to search without policy coupling

Add an immutable outcome-feature projection from a prepared/reduced action.
It may expose:

- applied tactical feature IDs such as `outcome.ambush`;
- hit probability before the random roll;
- resulting wound/incapacitation facts;
- AP spent/preserved/cap-waste facts once Phase F lands;
- sound emitted; and
- spatial affordance changes caused by destructibles.

`GameplayTacticalCandidateBuilder` may attach this projection to candidates or
a downstream scorer input. It must not contain preference weights. Keep the
current subject pruning and fail-closed candidate route intact.

Add tests proving two policies can value the same immutable outcome differently
without changing reduction. Commit suggestion: `Expose tactical outcome features`.

## Phase E — Scenario schema and migration decision

Do this before adding AP fields to published content.

### E1. Preferred path: add a scenario migrator

Mirror the level migration discipline with a presentation/application load
boundary that can advance scenario schema 15 to 16. The migration must deep-copy
the document, populate explicit legacy economy values equivalent to the old
behavior, update the version, and then pass normal validation. JSON loaded from
Resources and editor-created/authored scenarios must use the same route.

Legacy equivalent means:

- starting AP = existing `turnBudget.actionPoints`;
- personal-turn income = existing `turnBudget.actionPoints`; and
- maximum held AP = existing `turnBudget.actionPoints`.

That preserves full refresh for old content without special reducer branches.
Published `depot-yard.json` can then deliberately author 4/4/6.

If adding a migrator would create a second scenario-loading path, instead update
all schema-15 authored and test fixtures atomically and document that old
scenario JSON is intentionally unsupported. Do not leave missing JSON integers
to deserialize as zero and then guess their meaning.

### E2. Validation

Reject negative values, zero cap when actions cost AP, start above cap, and
income above cap unless a documented design requires overflow income. Ensure
every actor definition receives one complete economy profile.

### E3. Commit gate

Tests must cover schema 15 migration, schema 16 round trip/load, malformed
economies, published content, editor-authored content, and deterministic
assembly. Commit suggestion: `Version scenario action economy`.

## Phase F — Implement canonical 4/4/6 personal-turn income

### F1. Replace allowance semantics with explicit economy semantics

Introduce a domain value such as `TurnActionPointEconomy` containing Starting,
IncomePerPersonalTurn, and MaximumHeld. Thread it through
`ScenarioDefinition`, actor runtime definitions/snapshots, combat-state hashing,
copy helpers, projectile records that currently store allowance, HUD maximums,
save/restore boundaries, and diagnostics.

The concrete replacement audit must include:

- `ScenarioTurnBudgetData`, `ScenarioActorDefinition`, and
  `GameplayActorAssembler`;
- `GameplayActorState`, `GameplayActorSnapshot`, their copy/projector helpers,
  and canonical hashing/invariant validation;
- `GameplaySessionState` initialization and party save restoration;
- `GameplayTurnLifecycle` plus its `IGameplayTurnLifecycleHost` methods;
- `GameplayTurnTransitionReducer`, `GameplayCoreTransitionReducer`, and
  `GameplayLifecycleTransitionReducer`;
- projectile launch/advance records and impact-cycle calculations that use the
  old allowance as a denominator;
- `GameplayHudModel` maximum AP projection; and
- fixtures that directly construct actor snapshots or `TurnEndRecord`.

Do not keep `TurnActionPointAllowance` as an ambiguous alias once production
code depends on both income and cap. A short atomic rename is safer than a long
dual-semantics period.

### F2. Create one pure personal-turn grant rule

One Domain/Application function calculates:

- previous AP;
- requested income;
- granted AP;
- cap waste;
- resulting AP = min(cap, previous + income).

It returns an immutable record used by normal extended and core reducers.
Movement refresh remains separate and continues to apply wound penalties.

### F3. Change every transition intentionally

- Normal encounter next actor: grant personal-turn income and refresh movement.
- Incapacitated actor skipped: no grant merely for being scanned; only the actor
  that becomes active receives it.
- Same actor after a one-capable-actor cycle: receives one grant per canonical
  completed personal-turn transition.
- Encounter onset: no AP grant.
- Encounter completion: no party-wide AP refresh and no hidden grant.
- Voluntary mode exit: it is a UI/session transition and grants no AP. It may
  queue/record the world cycle but cannot modify budgets.
- Voluntary world-cycle completion: this is the canonical start of the next
  exploration interval. Grant each capable actor exactly once from the actor
  snapshots frozen in `VoluntaryTurnCycleRecord`; incapacitated actors receive
  no grant. Character selection within that interval grants nothing.
- Emergency reaction begin: freeze the interrupted actor ID and exact normal
  budget in canonical emergency state before installing an emergency budget.
- Emergency responder changes: install only each responder's emergency
  allowance; never read or modify their stored normal bank.
- Emergency reaction completion: restore the interrupted actor's exact frozen
  normal budget and movement opportunity. Do not grant normal income and do not
  full-refresh it.
- Selection change, replay entry/exit, save restoration, presentation reinstall:
  never grant.

Remove `RefreshAllActors` from encounter completion and voluntary-mode exit.
Replace duplicated logic in `GameplayTurnLifecycle`,
`GameplayTurnTransitionReducer`, `GameplayCoreTransitionReducer`, and
`GameplayLifecycleTransitionReducer` with the same pure grant helper and record
validation. The mutable lifecycle may apply the pure rule's returned record; it
must not contain a separate arithmetic implementation.

### F4. Extend turn records and replay/headless verification

`TurnEndRecord` or an attached personal-turn-start record must freeze previous
AP, requested/granted income, cap waste, resulting AP, and refreshed movement.
Reducers validate it exactly. Combat-state digest and headless traces include
these facts. Replay endpoints obtain budgets from reduced checkpoints, never
from current content tuning.

### F5. HUD and diagnostics

HUD maximum AP uses MaximumHeld, not starting AP or income. The turn diagnostic
shows `previous + granted = resulting`, and separately reports cap waste. Do not
promise the player income that will be discarded at cap.

### F6. Tests

At minimum:

- starting actor begins at 4;
- spending to 0 then advancing returns 4;
- preserving 1 returns 5;
- preserving 2 returns 6;
- preserving 5 grants 1 and records 3 cap waste;
- waiting at 6 grants 0 and records 4 cap waste;
- encounter onset and completion grant 0;
- selection and replay round trips grant 0;
- emergency reactions preserve normal banked AP;
- skipped incapacitated actors gain nothing;
- save/load preserves exact current AP and does not grant;
- core and extended reducers produce identical state/records;
- headless branch and live session digests match; and
- patrol, awareness, sound, enemy turns, objectives, hazards, and smoke/world
  duration continue advancing while AP is banked.

Commit suggestion: `Bank action points across personal turns`.

## Phase G — Mandatory integrated validation gate

Add one published-scenario lifecycle test and one engine-free simulation check
covering this sequence:

1. begin with 4 AP outside detection;
2. preserve AP while patrol/awareness advances;
3. enter encounter without receiving AP;
4. attack an Unaware actor from asymmetric visibility;
5. freeze and apply Ambush before sound changes awareness;
6. replay/seek across the attack without querying current spatial evidence;
7. end turns until the actor becomes active and receives +4 capped at 6;
8. destroy or move tactical cover and show a later headless context changes;
9. finish the encounter without party-wide refresh; and
10. reproduce the exact final digest in a headless trace.

Run:

- repository validation;
- simulation checks and parity checks;
- relevant Domain/Application/EditMode suites;
- hosted EditMode tests;
- hosted PlayMode lifecycle tests;
- hosted WebGL build and browser smoke test; and
- published preview URL verification.

The gate fails if any reachable action becomes contextual in live play but not
headless/replay, if a replay query touches Unity spatial state, if AP can be
generated by a scope/UI transition, or if old content silently receives zero
income/cap.

Commit suggestion: `Gate tactical context and banked AP parity`.

## Phase H — Focused player validation and stop point

Player checks:

- the diagnostic identifies why an attack did or did not qualify as Ambush;
- firing alerts survivors only after the shot resolves;
- crouched/pinned silhouettes agree between selection and context evidence;
- saved AP visibly carries into the next personal turn;
- AP never exceeds 6;
- entering/leaving combat, switching characters, replaying, and reloading do not
  generate AP; and
- waiting has observable world cost because patrols/awareness/opponents/world
  effects advance.

Stop after fixing failures from this gate. Do not proceed automatically to
flanking, crossfire, suppression, cover-destruction valuation, noise diversion,
resource denial, broad policy training, or optimizer campaigns. Those are
separate goals built on the proven registries.

## Explicitly deferred work

- information-safe camera handoff presentation;
- richer awareness UI and editor authoring;
- investigation movement and dynamic encounter joins/leaves;
- additional tactical modifier consequence types not proven by Ambush;
- broad enemy action selection and learned policy weights;
- generator regeneration tests;
- deployable drone and incendiary gameplay; and
- permanent/full-match replay storage.

## Final handoff report format

Codex 5.3 must report:

- each completed phase and commit hash;
- hosted run IDs and conclusions;
- preview URL;
- migrations performed and schema versions;
- exact tests added and any intentionally deferred coverage;
- player-validation instructions;
- current branch and push status;
- all remaining tracked/untracked files, explicitly preserving `AGENTS.md`; and
- the next unstarted roadmap item or a statement that the stop point was
  reached with no active implementation todo.
