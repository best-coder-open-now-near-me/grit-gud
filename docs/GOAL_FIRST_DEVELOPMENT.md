# Goal-first development and long-horizon AI

## Delivery principle

Grit Gud is developed toward the intended production capability rather than a
sequence of disposable MVPs. Before implementation begins, define the complete
player-facing goal, authoritative ownership, persistence and replay contracts,
presentation behavior, failure handling, polish bar, and verification strategy.
Then implement production-shaped vertical slices that converge directly on
that goal.

Intermediate integration checkpoints are useful for review, but they are not
permission to introduce temporary architecture, reduced rules that will be
discarded, duplicate implementations, or tests tied to knowingly incomplete
contracts. A checkpoint should exercise a stable part of the final design. Its
tests should remain valuable when the full capability ships.

The project therefore favors:

- final ownership boundaries before feature code;
- production data models and stable identifiers from the first committed slice;
- end-to-end behavior, usability, polish, diagnostics, and failure recovery as
  parts of the feature rather than post-MVP cleanup;
- tests against authoritative invariants and final contracts;
- completing connected capability families instead of accumulating isolated
  demonstrations; and
- explicit documentation when a reviewed checkpoint is intentionally partial,
  including exactly what remains before the feature is complete.

Research spikes may still answer a genuine unknown, but they remain isolated
experiments. A spike is not silently promoted into the product foundation; its
useful result must be re-expressed through the production architecture and
verification standard.

Code volume and initial implementation speed are not treated as the scarce
resources. The scarce resources are coherent design, integration confidence,
player-facing polish, exhaustive verification, and the avoidance of repeated
testing caused by throwaway implementations.

## Long-horizon tactical AI goal

The intended tactical-combat architecture is a layered, inspectable system:

1. **Intent and role** establish goals such as protection, delay, escape,
   objective commitment, or target elimination.
2. **Legal-action generation** uses authoritative game rules to enumerate valid
   movement, attacks, abilities, equipment, support, retreat, and turn-ending
   choices.
3. **Outcome prediction and bounded search** evaluate production-shaped future
   states without mutating the live session. Shallow beam search is the likely
   first search strategy; deeper or probabilistic search remains optional.
4. **Utility/value evaluation** scores interpretable normalized features such
   as expected wounds, kill probability, threat, reciprocal exposure,
   friendly-fire risk, range quality, resource cost, ally setup, survival, and
   objective progress.
5. **Personality policy** changes coherent preferences and planning depth rather
   than weakening legality or applying arbitrary stat bonuses.
6. **Execution** commits the chosen intent through the same validated,
   deterministic gameplay services used by player actions.

Legality is never trained. Learned or evolved policy may rank only candidates
that authoritative rules have already accepted. Stable deterministic
tie-breakers remain part of the shipped policy.

The likely first offline optimizer is CMA-ES or a comparable evolutionary
strategy over a compact, versioned, designer-readable utility policy. MAP-Elites
may later retain multiple strong behavioral identities instead of converging on
one universal policy. Neural value models, MCTS, or AlphaZero-shaped self-play
are research endpoints, not prerequisites for the production architecture.

This is a long-horizon goal rather than a claim that the current rifleman policy
already supplies a general legal-action enumerator, combat simulator, weighted
feature evaluator, or training environment.

## Shared verification and adversarial foundation

The tactical trainer and an alpha red-team harness should share infrastructure
without becoming the same product. The shared foundation should include:

- complete canonical authoritative snapshots;
- versioned, deterministic state and subsystem hashes;
- invariant checks before and after evaluation, commit, turn boundaries, and
  replay;
- disposable state reconstruction for speculative actions;
- deterministic scenario seeds and mirrored scenario families;
- a legal-action trajectory runner;
- frozen environmental evidence or trustworthy headless query adapters;
- per-step diagnostics and first-divergence reporting;
- reproducible failure capsules; and
- deterministic failure minimization into the shortest useful regression.

Distinct consumers then use that foundation:

- a **tactical policy trainer** optimizes combat quality and personality;
- a **legal-sequence exploit hunter** searches for pathological but permitted
  combinations;
- an **API-boundary fuzzer** submits stale, malformed, duplicated, and
  out-of-order records to verify atomic rejection; and
- a **Unity PlayMode chaos operator** exercises frame timing, UI overlap,
  cancellation, scene lifecycle, presentation, and save/load integration.

Every confirmed finding should become a permanent deterministic regression.
The full archive must be rerun after fixes because removing one dominant exploit
often exposes the next-best failure strategy.

## Groundwork order

Groundwork should be implemented as final shared infrastructure, not as a
throwaway trainer prototype:

1. Define complete authoritative snapshot ownership across gameplay subsystems.
2. Add canonical state hashing and structured subsystem diffs.
3. Establish hard invariants and atomic-transition checks.
4. Build deterministic action trajectories, repro capsules, and minimization.
5. Complete authoritative replay verification independently of visual replay.
6. Generalize enemy choices into legal action candidates with normalized,
   versioned feature records and explainable score contributions.
7. Add disposable simulation and bounded search.
8. Establish scripted, random, novelty, and adversarial baselines.
9. Run the first small mirrored CMA-ES experiment against held-out seeds.
10. Expand to MAP-Elites personalities and exploit archives only after the
    candidate vocabulary and simulator cover the intended combat systems.

Instrumentation should begin during development because it strengthens ordinary
debugging, replay, save/load, and regression testing. Large-scale training and
the adversarial gauntlet belong near the end of alpha, after the combat rules and
content contract are sufficiently complete to make their results durable.

## Current production goals

The active production sequence deliberately completes shared foundations before
policy optimization begins:

1. **Complete turn replay.** Replay must reconstruct every authoritative visual
   consequence in its bounded window while retaining the active character's
   ordinary camera and never mutating live state. Actor poses alone are an
   integration checkpoint, not completion.
2. **Canonical state and authoritative verification.** All combat-owning
   subsystems must contribute immutable, schema-versioned state to one canonical
   capture. The capture must have deterministic hashing, structured first-field
   differences, hard invariant validation, and replay verification. The canonical
   representation is production infrastructure for replay, saves, repro capsules,
   regression tests, and later high-throughput simulation—not an AI-only model.
3. **Shared prepare/commit transitions.** Every action family must be able to
   prepare a non-mutating predicted transition from a canonical state, reject a
   stale state before mutation, commit through the authoritative owner, and
   compare the actual result with the prediction. Presentation controllers and
   offline consumers may request or inspect transitions but may not implement
   parallel game rules.

The canonical state contract now includes session mode and phase, scenario and
initiative identity, emergency-turn context, actors, objectives, destructibles,
vehicles, projectiles, smoke fields, and sequence boundaries. Its hash is based
on explicit invariant-culture fields rather than runtime object identity or
collection iteration order. The shared transition coordinator enforces pre-state
freshness and reports post-commit prediction divergence using those same fields.
This is the common seam that individual action owners will adopt; it is not a
claim that every action family or every replay visual has already been migrated.

The first adopted action family is direct weapon resolution and world-point
weapon discharge. Both now expose a non-mutating prepared transition containing
the immutable before state, deterministic action record, and predicted resulting
state. Their existing immediate APIs are retained as prepare-then-commit
conveniences, and prepared records reject intervening turn or state changes
before authoritative mutation. The actor snapshot carries authored AP and
movement allowances so wound-induced movement clamping can be predicted from the
snapshot without consulting Unity or a second rules implementation.

Projectile launch now uses the same contract. Preparation freezes the stable
projectile ID, launch definition and trajectory, resource cost, attacker facing,
and initial in-flight snapshot without adding a live projectile. Commit rejects
an intervening state change before mutation and compares the authoritative flight
registry with that prediction. Projectile advancement and impact consequences
remain a separate transition family because they consume fresh environmental
collision evidence at arrival time.

Projectile advancement and impact now form an evidence-bearing prepared
transition as well. Preparation samples the current segment once, freezes its
world revision, collision and blast evidence, and projects the resulting flight,
localized actor wounds, movement clamping, destructible integrity, and shared
journal advancement without mutation. Commit requires the complete canonical
pre-state to remain unchanged, applies the recorded evidence without querying
Unity again, and compares the resulting authoritative state to the prediction.
This preserves the gameplay rule that a separate preparation performed after an
emergency reaction samples the changed world rather than reusing earlier
evidence.

Turn replay now consumes a bounded canonical checkpoint timeline recorded after
normal turn completion and after earlier turn-end subscribers have finalized
projectile, smoke, and other subsystem consequences. The replay window must map
to an exact starting checkpoint and one canonical endpoint per segment, and its
endpoint hash must still match live authoritative state before replay can open.
This creates the stable state source for subsequent persistent visual projection
without turning replay into a second mutable gameplay session.

Replay sampling now produces one immutable presentation sample containing actor
state and sampled poses, destructibles, vehicles, projectile flights, and smoke.
Projectile replay uses isolated presenters while live projectile visuals are
suppressed, and destructible replay restores authoritative presentation on exit.
This keeps scrubbing reversible and prevents presentation work from writing back
to canonical sessions.

Vehicle and smoke replay now follow the same isolation rule. Sampled vehicle
transforms never enter `VehicleMomentumSession`, live momentum envelopes remain
hidden during replay, and authoritative transforms return on close. Live smoke
duration is paused while replay is open; presentation-only smoke fields follow
the sampled canonical set and are replaced by the untouched authoritative set on
exit.

Actor replay presentation now follows that isolation rule as well. Deterministic
event time projects seekable attack, equipment, throw, displacement, and
reaction states; sampled wounds and equipped items drive presentation adapters
without changing the live actor or inventory. Audio and particle adapters receive
only continuous-forward event-boundary crossings, while direct or backward seeks
clear active one-shots. Replay exit restores the captured Animator, held model,
wound variants, transforms, stance, and presentation-component lifecycle rather
than asking authoritative gameplay to rebuild presentation state.

## Alpha-exit use

The adversarial gauntlet is intended to cap alpha, but it cannot prove the
absence of defects. The exit gate must specify a repeatable search budget and
require:

- no unresolved known crash, authoritative-invariant, replay-divergence, or
  reproducible softlock findings;
- permanent passing regressions for all confirmed exploits;
- deterministic reproduction or principled rejection of every high-severity
  headless finding in Unity;
- stable state hashes across supported execution paths; and
- completion of the defined scripted, random, novelty, optimized, archived,
  mirrored, and held-out search corpus without a new high-severity category.

The compute budget, scenario corpus, seeds, policy versions, content revisions,
and termination rules must be recorded so the alpha gate can be rerun rather
than treated as a one-time demonstration.
