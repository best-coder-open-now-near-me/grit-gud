# First deterministic battle and visual review contract

## Completion boundary

The first simulation milestone is complete only when one multi-enemy battle:

1. starts from assembled production scenario and spatial content;
2. generates legal semantic candidates from the installed capability routes;
3. evaluates public evidence and policy-neutral expected outcomes;
4. lets one deterministic policy select a candidate without observing hidden
   random samples;
5. prepares the selected semantic transition directly from canonical state;
6. executes it through `GameplaySimulationRuntime` and the production reducer;
7. reaches a declared terminal result without a script choosing its actions;
8. exports one versioned authoritative battle artifact;
9. loads that artifact in a clean process and verifies exact replay;
10. derives its scoreboard solely from transitions and domain events; and
11. plays the same artifact through the existing Unity replay presentation
    contracts with optional presentation-time dead-time compression.

The first retained Depot battle must contain multiple hostile actors and cover
the intended first-simulation systems: ordinary actor movement and attacks,
persistent fire, concussive current-AP reduction, and the controllable drone's
movement, perception, targeting, damage, and initiative behavior.

## One execution spine

The permanent execution route is:

```text
canonical state
  -> reachable inputs and tactical subjects
  -> candidate/evidence route
  -> policy-neutral expected outcome
  -> deterministic policy selection
  -> selected-candidate transition preparation
  -> GameplaySimulationRuntime
  -> trajectory and domain events
  -> battle artifact
  -> exact replay
  -> scoreboard and visual-review projections
```

There is no scripted battle executor, mutable-session shadow simulator,
viewer-only action format, scoreboard-owned gameplay counter, or alternate
visualization reducer. Live gameplay may continue to have adapters around the
same application services, but simulation authority belongs to the semantic
runtime above.

## Authority convergence gate

The first battle cannot be accepted while live play, replay, or AI retains an
independent implementation of an authoritative rule. Before the runner is
trusted:

- live commands prepare semantic payloads, execute reducers, atomically install
  the resulting root, and project presentation from installed state/events;
- mutable gameplay, explosive, drone, smoke, movement, and turn sessions no
  longer apply a second copy of those consequences;
- replay samples reducer-produced boundary states and events, with only visual
  interpolation remaining presentation-owned;
- Unity and headless evidence use the same projected target raster and sound
  attenuation rules, differing only in their spatial-obstruction backend;
- action and consequence identities derive from canonical transition/action
  identity rather than private collection counts; and
- live and replay movement/projectile playback use shared pure samplers.

The live enemy executor becomes an adapter over the same candidate/policy
runner. It may pace and present decisions, but cannot select or commit actions
through a second AI implementation.

Only the selected candidate may resolve addressed random samples. Candidate
evaluation uses expected outcomes and frozen public evidence, so visiting or
sorting candidates cannot change the result.

## Concrete route registry

Capability coverage metadata is not sufficient proof of executable
simulation. Each exact reachable capability profile must resolve to a concrete
route that owns:

- candidate construction and subject applicability;
- legality and required evidence capture;
- policy-neutral expected outcome projection;
- selected-candidate transition preparation;
- the compatible reducer and domain events;
- scoreboard projection; and
- visual-review projection or an explicit no-visual-event declaration.

Scenario assembly and the first-battle runner fail closed when an exact
reachable profile lacks a concrete route. Numeric weapon/item variation stays
data-only when it uses an existing route.

## Policy boundary

A policy consumes immutable evaluated candidates. It may assign preferences
for survival, wounds, incapacitation, AP efficiency, saved AP, cap waste,
cover, awareness, sound, fire, drone safety, objectives, and other portable
features. It cannot create legality, evidence, hit chance, damage, resource
cost, initiative, or consequences.

The baseline policy records its stable policy ID/version, candidate-set digest,
selected candidate ID, score components, and tie-break reason. This decision
telemetry is diagnostic and does not enter canonical state hashes.

## Termination and failure

A battle terminates only with a typed result such as party victory, hostile
victory, objective completion/failure, or authored draw. The runner also has
hard safeguards for maximum transitions, repeated canonical states, repeated
no-progress turns, unresolved mandatory reactions/projectiles, and absence of
a legal end-turn route. Safeguard exits are failures, not draws.

Candidate construction, evidence, scoring, preparation, reduction, and
installation each have monotonic stage timing plus per-decision, per-turn, and
whole-battle deadlines. AI/search runs behind a cancellable worker boundary;
optimizer batches use process isolation when cooperative cancellation cannot
guarantee termination. A timeout emits a typed failure and partial diagnostic
artifact without a fallback mutation. Timing telemetry never enters canonical
hashes or deterministic artifact equality. Contract tests include a policy
that deliberately fails to return.

## Authoritative artifact

One versioned artifact contains execution identities, provenance, initial
canonical state, ordered selected decisions, semantic transitions, resulting
hashes, ordered domain-event payloads, terminal result, and parent/fork lineage.
Canonical serialization uses stable ordering, invariant numeric formatting,
and no timestamps or machine paths. A strict reader validates bounds, type
discriminators, identities, contiguous hashes/sequences, payload digests, and
schema compatibility before constructing typed state.

Derived scoreboards, HTML, screenshots, and videos are not authoritative.

## Scoreboard and time

The scoreboard distinguishes considered, attempted, committed, and resolved
actions. Considered and rejected attempts come from non-authoritative decision
telemetry; committed and resolved facts come from transitions and events.

Canonical alignment uses transition sequence, personal-turn boundaries, and
causal action identifiers. Visual seconds are derived presentation time and do
not identify gameplay state.

## Visual review and dead time

The battle viewer loads the same verified artifact and projects it through the
existing replay world/action presentation seams. It never runs another policy
or re-resolves gameplay. Human playback may compress intervals containing no
meaningful movement, decision, reaction opportunity, threat change, or visible
consequence. Compression cannot remove or reorder authoritative transitions and
cannot hide awareness changes, reaction windows, projectile arrivals,
persistent-world changes, or terminal events. Uncompressed deterministic
playback remains available for debugging.

## Validation gates

Before the battle is accepted:

- two fresh executions produce identical candidate-set digests, selections,
  transitions, events, terminal result, scoreboard, and final hash;
- artifact write/read/write is byte stable;
- exact replay succeeds in a separate process;
- all canonical invariants and exact reachable-route coverage pass;
- parent/fork state identity is verified;
- scoreboard totals reconcile against transition/event facts;
- compressed and uncompressed visual playback reach the same endpoint; and
- hosted EditMode, PlayMode, WebGL build, browser smoke, and preview publication
  gates pass.
