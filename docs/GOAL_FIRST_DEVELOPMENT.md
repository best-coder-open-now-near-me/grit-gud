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
