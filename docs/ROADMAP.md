# Foundation Roadmap

Each phase ends in something that can be reviewed independently, but phases are
production-shaped integration checkpoints rather than disposable MVPs. The
[goal-first development and long-horizon AI decision record](GOAL_FIRST_DEVELOPMENT.md)
defines the project's delivery standard, future trainable tactical-policy
direction, and alpha adversarial-testing capstone.

The player-facing turn-replay interaction is specified separately in
[TURN_REPLAY_UX.md](TURN_REPLAY_UX.md). It uses a bounded, turn-segmented timeline
from the active player character's previous turn to their current turn and does
not imply permanent full-match replay storage. The active character retains
their normal gameplay camera throughout playback, and their previous-turn
segment is optional context rather than part of the default playback range.

## Encounter foundation — 2026-08-18

The encounter foundation now has one authoritative route from authored content
through live play, deterministic reduction, replay, and headless branching.
Enemy behavior content declares hearing range, sight and sound suspicion gains,
decay, alert threshold, optional patrol waypoints, and initial reinforcement
relationships; scenario assembly rejects incomplete sensing policy.

Application owns immutable awareness (`Unaware`, `Suspicious`, and `Alert`),
last-known evidence, patrol progress, participant scope, and sequenced journal
records. Unity supplies silhouette exposure, sound occlusion/distance evidence,
and traversable patrol routes, then plays a route before its prepared canonical
advance commits. It does not decide awareness or initiative.

Alert begins a scoped encounter containing the player party, alerting actor,
trigger subject, and transitive authored reinforcements. Unrelated actors stay
outside tactical initiative. Completing the encounter restores full exploration
initiative without refreshing budgets. The capability gate treats observation
and patrol as reachable AI inputs, while engine-free checks prove live/pure
parity, exact replay, headless branching, and destructible-aware sight/sound.

Playable encounter validation passed on 2026-08-18. Investigation movement,
dynamic join/leave after an encounter begins, richer awareness UI, and
editor-facing encounter authoring remain later tactical work. Generator
regeneration tests remain deferred.

Pinned actors now use one authoritative pinned-down target profile for player
acquisition, live/headless exposure, replay projection, and prone presentation.
Push Off restores the current stance profile and explicitly releases the held
fall layer. Movement collision is no longer accepted as an aiming silhouette.

## Pre-training tactical context and action economy — next goal

Before broad AI training or optimizer campaigns, finish two authoritative
gameplay foundations. Neither is an AI-only scoring shortcut: live play,
deterministic reduction, replay, and headless branches must consume the same
records.

The file-level execution order, migration policy, parity checks, commit gates,
and Codex 5.3 handoff instructions are maintained in
[PRE_TRAINING_SIMULATION_IMPLEMENTATION_PLAN.md](PRE_TRAINING_SIMULATION_IMPLEMENTATION_PLAN.md).

### 1. Portable tactical context

Introduce an immutable pre-action context snapshot assembled from canonical
state plus the action's already-required spatial evidence. The initial feature
vocabulary covers target awareness, mutual visibility, cover/exposure, range
band, stance, target isolation, nearby allies and threats, suppression or
displacement state, sound signature, resource pressure, and relevant tactical
environment subjects. Features must use stable identities and quantized or
enumerated values where continuous measurements are not required.

Contextual gameplay rules are declarative ordered definitions. Each rule has a
stable ID, explicit applicable capability/subject traits, predicates over the
frozen context, and authoritative consequences such as accuracy, damage,
reaction availability, action cost, or sound changes. AI valuation weights are
separate consumers of the resolved outcome features and cannot alter gameplay
resolution.

The first vertical proof is `Ambush`: an attacker unseen by an Unaware target
can receive authored direct-attack consequences. Preparing the attack freezes
the qualifying context; resolving it applies and records the exact rule IDs,
inputs, and resulting modifiers before attack sound updates awareness. Replay
uses the recorded applied context and consequences rather than reevaluating
LOS or awareness. Headless branches evaluate the same preparation and reducer
path.

This slice is complete only when:

- the context vocabulary and rule schema validate at content load;
- direct-attack preparation freezes context before any attack consequence or
  sound-driven awareness transition;
- the pure reducer validates and applies the recorded modifiers fail-closed;
- the action record, journal, replay projection, diagnostics, and headless
  trace preserve the same applied rule IDs and outcome features;
- tactical candidate scoring can read resolved outcome features without
  owning authoritative modifiers;
- capability coverage includes contextual preparation, reduction, replay,
  headless execution, and live installation for every reachable contextual
  profile; and
- parity tests cover qualifying and non-qualifying attacks, stale evidence,
  sound alert after resolution, replay seeks, and equivalent live/headless
  results.

Additional metas—flanking, crossfire, suppression, cover destruction, noise
diversion, isolation, and resource denial—extend the same feature and rule
registries after Ambush proves the route. They do not receive bespoke action
pipelines.

### 2. Banked personal-turn action points

Author scenario action-point economy as starting AP, personal-turn income, and
maximum held AP. The first balance profile is `start 4 / income 4 / cap 6`.
On the canonical transition into an actor's normal personal turn:

`resulting AP = min(maximum held AP, previous AP + personal-turn income)`

Movement opportunity continues to refresh from its existing allowance and
wound rules; emergency reactions retain their separately authored allowance.
Entering or leaving encounter scope, toggling voluntary turn mode, changing
selection, replaying, restoring a save, or reinstalling presentation must not
replenish AP. Encounter completion therefore stops refreshing every actor.

The turn record must preserve previous AP, granted AP, cap waste, and resulting
AP so replay and headless traces never infer the economy from UI state. Scenario
validation rejects negative values, starting AP above the cap, income above the
supported integer range, and actors without a complete economy definition.
Migration supplies the explicit legacy equivalent for older content before the
published scenario switches to 4/4/6.

This slice is complete only when reducer parity covers ordinary initiative,
skipped incapacitated actors, encounter onset/completion, voluntary world
turns, emergency reactions, save restoration, replay, and headless branching.
AI outcome features expose AP preserved, AP spent, and cap waste so patience is
valuable only while patrols, awareness, opponents, objectives, and hazards
continue to advance through authoritative world or personal turns.

### Delivery and playtest gate

Deliver the generic context model plus Ambush proof first, then the banked AP
economy. Each is a separate validated commit and hosted preview. After both are
green, run a focused player test for detection timing, attack qualification,
post-shot awareness, saved-AP carryover, cap behavior, and attempts to gain AP
through encounter or UI transitions. Broader tactical metas and training begin
only after that mandatory gate passes.

## Pre-encounter stabilization checkpoint — 2026-08-17

The repository-wide stabilization goal is complete on
`codex/stabilize-runtime-architecture`. It hardened cloud/Supabase response
boundaries and shared JSON import, added fork-safe CI plus collision-resistant
preview identities, partitioned gameplay lifecycle characterization, and
decomposed the enemy runtime, turn lifecycle, scenario combat assembly,
control routing, HUD, level-editor GUI/lifecycle, and displacement transaction
hotspots. The final follow-up also extracted gameplay outcome validators and
appliers, Task-based cloud commands, ordered gameplay installers, HUD feature
drawers, scenario policy-family assemblers, and one-file-per-rule level
validation. Tight coordinator budgets and source gates prevent those boundaries
from silently regressing. The detailed ownership record is in
[ARCHITECTURE_REVIEW.md](ARCHITECTURE_REVIEW.md).

This checkpoint added no encounter features. The planned encounter-onset and
lifecycle vertical slice is now the foundation above. The remaining follow-up
work is:

1. Define Application-owned awareness state and immutable sight/sound evidence
   records. Unity performs line-of-sight, occlusion, distance, and sound-world
   queries; it does not decide detection, suspicion, hostility, or initiative.
2. Add investigation and loss-of-contact behavior, then editor authoring only
   after those portable policy contracts stabilize.
3. Add dynamic join/leave rules for reinforcements that arrive after the
   initial scope. Those transitions must not replenish budgets or duplicate
   actor state.
4. Add richer awareness UI/debugging and published-level PlayMode lifecycle
   coverage before broadening enemy tactics.

The deployable drone, incendiary consumables, and broader enemy action selection
remain separate later goals. They should consume the encounter and simulation
contracts rather than expanding this first slice.

## Current restart checkpoint — 2026-08-11

The project has completed enough of the early tactical foundation that the
roadmap is active again at **Phase 7: authored player party, character creation, and
the close-quarters action family**. The authoritative close-quarters seams are
in place, but their production verification, authored presentation, and shared
simulation groundwork are not complete. The drone remains a late gameplay
expansion rather than the next overall slice. The repository-wide
[architecture and separation-of-concerns review](ARCHITECTURE_REVIEW.md) is the
decision record for this restart.

Work resumes in this order:

1. Run a stabilization sweep against the latest playable preview before adding
   another feature slice. Exercise every visible command, hotbar power,
   confirmation/cancel path, turn transition, and replay/diagnostic entry. Track
   a broken visible control as release-blocking rather than carrying it as an
   ordinary feature TODO. **Complete:** the sweep repaired grenade activation,
   scenario boot, turn gating and refresh, exploration firing, slow-projectile
   collision playback, rifle mounting, support-hand IK, and non-accumulating
   upper-body aim. The visible confirmation and cancellation paths were smoke
   tested in the editor.
2. Add fast script-compilation and EditMode-test gates ahead of full platform
   builds. This is the first delivery-hygiene checkpoint; workflow changes need
   a credential with GitHub Actions workflow scope. **Complete:** repository
   validation and the Unity EditMode suite now run before the WebGL build.
3. Replace the Push-specific actor shortcut with authored displacement action
   definitions, while preserving the current action-first UI behavior.
   **Complete:** Push is available in exploration and turn mode; its first
   confirmation commits an Application-approved subject to the farthest valid
   destination directly away from the actor. The authoritative path query
   shortens blocked pushes before confirmation, while preview/cancel do not
   mutate gameplay state. Actions such as Throw retain explicit destination
   selection because their intent does not imply a direction.
4. Add authored hand requirements and atomic equipment auto-stow planning so a
   weapon-to-displacement transition charges the complete, visible combined
   cost or charges nothing. **Complete:** Throw requires both hands; a blocking
   equipped weapon is stowed in the same action record, its authored cost is
   included in availability and confirmation UI, and failed validation changes
   neither equipment nor budget. Current Push intentionally requires no hands.
5. Move target-kind acceptance and rejection reasons into Application; Unity
   presentation should supply only a stable pointer candidate ID. **Complete
   for Push and Throw subjects and destinations.** Pointer rays are shared by
   attacks, explosives, and displacement; camera orbit no longer doubles as
   targeting, and the fixed center reticle has been removed.
6. Replace the inert depot target with one production hostile-rifleman vertical
   slice before expanding combat verbs. **Complete:** schema 8 authors
   allegiance, hostility, wound threshold, perception range, view angle,
   preferred engagement range, movement search radius, attack limit, equipped
   rifle, and attack response. Application records deterministic detect, move,
   attack, and end-turn decisions; Unity supplies LOS and traversable movement
   candidates, then presents only committed movement, firing, and
   incapacitation. Incapacitated actors are skipped by initiative and emergency
   responder construction, and the encounter closes once no capable hostile
   remains.
7. Deliver weight-limited Throw and toppling, opposed combatant displacement,
   then reach-limited knife attacks. **Combatant Push and authored Throw are
   complete through the same action-first targeting, opposed-control, atomic
   action, and journal paths. Pull is intentionally not a separate verb: Throw
   already permits any valid direction, while mass decay can limit a heavy
   subject to a nearby destination. The intervening core-attribute checkpoint
   is complete: schema 8 makes Strength, Dexterity, Grit, and Charisma mandatory
   1-5 ratings; Dexterity derives initiative and movement, while Strength is
   included in opposed displacement. **Prop toppling is implemented end to
   end:** schema 13 authors per-prop rotation/elevation profiles, live Push and
   Throw deterministically resolve eligible upright props into frozen toppled
   pose/posture evidence, and the final rotated collider volume is validated
   before the atomic commit. Replay and presentation consume that recorded
   state without rerunning physics. The published depot enables `Topple` and
   carries a named verification group. **Reach-limited knife
   attack rules are complete:** schema 9 adds an
   explicit contact-delivery contract to ordinary inventory attacks; the
   Combat Knife uses authored reach and AP, actor-only targeting, atomic
   out-of-reach rejection, seeded regional wounds, immutable reach evidence,
   complete combat diagnostics, contact-aware enemy movement, and a catalog-
   authored contact presentation with no firearm effects. **Authored close-
   quarters presentation is complete:** the generated controller uses the
   imported Knife Idle and Stabbing clips, normalizes the strike to a stable
   0.8-second presentation, and exposes its 40% contact point without delaying
   the committed wound. Successful hits enter a higher-priority full-body
   reaction channel only at that visual contact; ordinary reactions recover,
   while incapacitating torso/arm hits select Shoulder Hit And Fall and other
   regions select Fall Over. Equipment changes can interrupt the strike back to
   its owned idle state. Replay carries contact/reaction metadata, remaps the
   target reaction after the contact point, holds incapacitated presentation,
   suppresses transient contact effects during seeks, and restores the exact
   live animator, weapon, wound, and pose snapshot on exit.
   **The intervening wound-visibility checkpoint is also complete:** direct
   attacks preserve Head, Torso, left/right Arm, and left/right Leg wounds in
   authoritative actor state. The player HUD renders the same six regions as a
   proportioned body silhouette with a circular head and color-coded wound
   state; counts and diagnostic detail remain available on hover rather than as
   text embedded in the shapes;
   this does not introduce an HP pool or aimed body-part selection.
8. Return to shared blast policy, finite consumable quantities, durable party
   equipment persistence, and only then the deployable drone. **Shared blast
   policy is complete:** grenades and projectiles record the same distance,
   occlusion, falloff, exposure, subject-kind, and regional-injury evidence and
   resolve actor/prop consequences through one Application service. **Finite
   consumable quantities are complete:** schema 10 authors positive stack sizes;
   immutable actor snapshots, action records, replay validation, availability,
   hotbar labels/tooltips, Combat Diagnostics, and bug reports all consume the
   same authoritative before/after quantity state. Preview and cancellation do
   not mutate it, while a committed throw consumes exactly one matching item in
   the same action. **Schema 11 smoke-grenade follow-up is complete:** smoke is
   an authoritative finite-duration sight-obscurance field rather than a
   particle-system rule. Player acquisition, hit exposure, and enemy LOS share
   the same field query; scenario data owns volume, lifetime, and sight-block
   threshold; presentation data independently owns the thrown model, sparse
   lit cloud, fade, and calm camera-interior treatment. **The second-character
   checkpoint is complete:** Oren Vale is a separately authored party member
   with distinct identity, attributes, starting build, inventory, equipment, and
   runtime state. A dedicated roster surface supports click or Tab selection
   during exploration and communicates initiative-owned control during combat.
   **Durable party persistence is complete and corrected:** the versioned local
   save restores identity-bound equipment only. Regional wounds and action
   budgets are mission-transient and restart from the authored scenario.
   Character
   Creator defines appearance, attributes, skills, talents, and starting loadout
   before a level; there is no runtime progression, point budget, or advancement
   drawer. These character-system prerequisites no longer make the drone
   the next overall slice; existing combat, presentation, and verification work
   below takes priority.
9. Complete a restrained visual-presentation pass without moving gameplay rules
   into effects. **Complete:** the depot now uses level-authored lighting,
   atmosphere, practical pools, depth fog, portable decals, placed dust/haze,
   and spatial ambient-audio zones;
   the global theme owns grade, cel response, outlines, grounding, and tactical
   transition cadence; and archetypes select surface-authored concrete, wood,
   metal, or actor response for both shading and physical impacts. Rifle and
   launcher discharge adds authored short-lived muzzle illumination. The main
   menu uses a production depot backdrop and no longer exposes an internal-build
   footer. This presentation work does not change attack, collision, wound, or
   turn authority. The generated depot menu image has since been removed; the
   menu remains palette-authored until human-authored key art is available.
   A future lighting-polish pass can spend the available low-poly render budget
   on authored practical shadows, emissive/bloom hierarchy, selective shafts,
   and smoke-light interaction without introducing spell-like effects.
   Once authoritative toppled-prop pinning exists, a secondary character/prop
   polish pass should sell it with prop-aware pinned poses, struggle loops, a
   Push Off motion, and an authored get-up. Presentation consumes the committed
   pin/escape states and exact prop pose; it must not decide immobilization,
   move the prop independently, or rerun physics during replay.
10. Replace frame-relative weapon-hand correction with authored prop rigs.
    **Complete:** rifle, launcher, and knife presentation now instantiate
    project-owned rig prefabs containing the model, muzzle, support-hand, and
    elbow sockets. Exact blended humanoid IK consumes those sockets after
    animation and aim correction. The rifle upper-body layer uses the imported
    shooter idle, walk, run, strafe, and firing motions, while the generator and
    validation suite own the controller and socket contracts. Other imported
   shooter motions remain available for their eventual reload, grenade,
   reload, grenade, and additional turn behaviors; knife idle/strike plus the
   first wound/fall reactions are now owned by the generated controller.
11. Replace singleton player ownership before authoring companions. **Complete
   through the player-facing vertical slice:** schema 12 owns the ordered player
   party and stable character identities; exploration selection and turn-mode
   command authority are Application state; encounter-opening actions commit
   atomically for the initiator; and Unity retargets camera, input, HUD, hotbar,
   targeting, equipment, consumables, displacement, projectiles, and persistent
   held weapons together. Enemy detection, target selection, encounter
   relevance, defeat, party-state ownership, and bug-report diagnostics now
   evaluate actual party members rather than a singleton `player` actor. Mara
   Vance and Oren Vale now exercise separate profiles, budgets, inventories,
   equipment, hotbars, and weapon presentation. The roster UI
   supports click and Tab switching in exploration; combat follows Dexterity-
   derived friendly initiative and disables manual selection. Automated boot,
   selection, retargeting, and alternating-friendly-turn coverage is green.
   **The durable-party follow-up is complete:** schema 3 saves are keyed by
   stable character identity, require an exact authored roster, validate
   equipment, and restore only equipment before gameplay systems bind. Explicit
   schema 1/2 migration discards legacy wounds and action budgets.
   PlayerPrefs supplies the browser/desktop adapter while Application owns the
   versioned save contract and validation. Authoritative equipment changes flush
   immediately. Runtime progression and player-side spending are
   explicitly outside the product model.
   **Next: complete the existing-system verification sequence below.**

## Current next production sequence — 2026-08-16

The next work closes and proves existing systems before adding another
initiative participant:

1. **Complete destructible toppling end to end.** **Complete 2026-08-16:** the
   implementation, full Unity runner, and hands-on fixture acceptance pass.
   Author prop eligibility and a
   deterministic result-policy resolver that freezes the resulting pose,
   posture, and environmental evidence in the committed displacement. Add a
   dedicated published depot fixture with an isolated prop, mixed crate/barrel
   contacts, and a settled pile. Verify targeting, obstruction, collision,
   exposure/cover, replay scrubbing, and exact live restoration. Add
   Application tests for resolution and PlayMode lifecycle coverage for the
   published fixture.
2. **Complete direct-fire destructibles and authored break presentation.**
   **Complete 2026-08-16:** rifle impacts now freeze point, normal, surface,
   target, world revision, and the nearest stable fracture chunk before
   atomically committing authored weapon/material integrity damage. Crates and
   metal barrels own editor-baked 12-cell Voronoi profiles; authoritative
   snapshots record a deterministic detached-chunk mask shared by rifle,
   launcher, and frag damage. Attached chunks provide real collision and
   exposure geometry, live damage emits bounded presentation-only debris, and
   replay forward crossings, seeks, scrubbing, and exit restore exact masks
   without rerunning fracture or physics. The published depot lifecycle and the
   full EditMode/PlayMode gates cover the resulting state and presentation.
3. **Add first-class toppled-prop pinning and escape.** **Complete 2026-08-16:**
   `Pin` resolves only when the final toppled footprint supplies stable actor
   contact evidence and the authored prop mass/depth rules accept it. The
   committed displacement freezes the responsible prop, actor, contact, pin
   transition, and exact poses; canonical state, replay sampling, and journal
   projection preserve them without rerunning physics. Pinned actors cannot
   move or use incompatible attack, equipment, inventory, stance, interaction,
   projectile, explosive, or displacement actions. Player and enemy content
   now owns a costed, capability-checked **Push Off** action that atomically
   moves the exact pinning prop, releases the actor, and projects get-up. The
   published depot includes a dedicated pinning fixture, level authoring
   exposes toppling/pinning limits, and automated domain, Unity-contact,
   migration, replay, published-content, AI-turn, and PlayMode restoration
   coverage is green. **Directional placement follow-up complete 2026-08-16:**
   player Push Off automatically locks the exact pinning prop, then accepts a
   chosen heading and previews its farthest valid final pose with the same
   oriented footprint used by commit validation. Swept prop collision, final
   cover geometry, and the actor's full standing clearance are validated before
   the chosen prop/release/cost record commits; blocked headings shorten along
   that ray and fully blocked get-up space reports a dedicated failure. Enemy
   use deterministically selects among the same validated headings. Replay
   consumes the resulting exact displacement record without rerunning the
   choice. Prop-aware lying, struggle, bounded ragdoll, Push Off motion, and
   authored get-up animation remain secondary presentation polish; those
   visuals consume the committed state rather than deciding it.
4. **Add authored jump, vault, and mantle traversal.** **Primary slice complete
   2026-08-16:** ordinary movement remains grounded and automatic while schema 15
   traversal links authorize specific jump, vault, or mantle crossings with
   stable takeoff, landing, direction, clearance, movement/AP cost, action
   identity, arc, and playback duration. Planning selects a valid link from the
   player's normal movement input, validates the whole capsule arc and landing,
   and freezes the resulting segment and budget for commit, playback, and replay.
   The player, ghost, replay projector, and enemy tactical candidates consume the
   same resolver. The published Depot Yard includes connected and deliberately
   disconnected tall-crate fixtures; real-collider coverage verifies below-limit
   45-degree, at-limit 50-degree, and rejected 55-degree slopes as well as
   one-way direction, obstruction, stale commits, cancellation, deterministic
   arc sampling, replay semantics, and exact live restoration. The new full-body
   traversal layer uses the authored `rifle jump` clip. Vault and mantle retain
   distinct first-class action identities but temporarily share that binding
   until their dedicated clips and published fixtures are authored.
5. **Finish close-quarters presentation.** **Authored slice complete
   2026-08-16:** Knife Idle owns the equipped melee pose and Stabbing owns a
   stable 0.8-second upper-body action. The committed wound remains immediate,
   while a presentation-only scheduler starts the full-body hit/fall reaction
   at the authored 40% contact point. Equipment changes interrupt the strike to
   its owned idle; reaction priority suppresses weapon IK; replay remaps contact
   progress, holds incapacitated actors down, emits no contact transient during
   seeks, and restores the exact live animator/equipment/wound state. The next
   bounded presentation experiment is now complete for incapacitating direct
   and contact attacks: at normalized time `0.72`, the authored fall hands its
   evaluated pose to a generated 12-body/11-joint rig. Recorded attack direction
   and region produce a clamped impulse; the rig only contacts static level
   geometry, settles/freezes within `2.25` seconds, and never moves the
   authoritative actor root. A journal-keyed, versioned 20 Hz trace stores
   millimetre root-relative positions and quantized rotations for seekable
   replay instead of rerunning PhysX, while replay exit restores exact live
   bodies, velocities, and transforms. Projectile and thrown-blast reactions
   remain a later evidence hookup to this same presentation seam.
   Reload, grenade, and additional turn clips remain distinct later action-
   presentation bindings rather than part of the knife slice.
6. **Add incendiary consumables.** Author Molotov cocktails as first-class
   throwable consumables that reuse frozen landing and exposure evidence, then
   create authoritative persistent fire fields with ignition, duration,
   turn-by-turn injury, area denial, environmental interaction, enemy use,
   replay scrubbing, and exact live restoration. Treat napalm as a stronger
   payload/field variant of the same incendiary system unless its delivery
   method later requires a distinct action family. Include friendly fire,
   destructible ignition, overlapping fields, extinguishing, stale commits,
   depleted stacks, and PlayMode lifecycle coverage.
7. **Extend the shared verification foundation.** Migrate the remaining action
   families to canonical prepare/commit transitions, then add deterministic
   action trajectories, reproducible failure capsules, minimization, API
   fuzzing, disposable simulation, and scripted/random seed baselines.
8. **Harden authored content, enemy choices, editor workflows, and delivery.**
   Exercise broader enemy action selection, destructible-pile authoring,
   viewport transforms, Windows/WebGL artifacts, and browser-playable preview
   handoff against the same authoritative contracts.
9. **Add the deployable drone as the final substantial gameplay expansion.**
   Deployment, ownership, command range, initiative insertion, destruction,
   and removal must use the proven party, transition, replay, and simulation
   seams.
10. **Run the alpha adversarial capstone.** Complete the scripted, random,
   novelty, optimized, mirrored, held-out, and archived search corpus only after
   the intended gameplay systems, including the drone, are present.

### Current playable verification baseline

The published depot now contains a `Toppling Verification` group covering an
isolated crate, a mixed crate/barrel contact chain, and a three-crate authored
pile with round-tripped X/Y/Z rotation. Seven stable props in that group are
registered as displacement subjects, and both player characters' Push and
Throw actions allow `Topple`. A PlayMode lifecycle test pushes the near-spawn
pinning crate, verifies the authoritative and scene pose, projects an earlier replay
state, then restores the exact live toppled pose. Blast damage from the launcher
and frag grenade still exercises the same props. Rifles now damage them as
well: wood takes four integrity per hit and metal takes two. Crates and barrels
switch to their baked Voronoi chunks on first damage, remove deterministic
impact-local pieces, and restore their exact live chunk mask after replay. Both
player characters can now choose Push Off direction after a pin: the exact
pinning prop is locked automatically, a wireframe previews its final
collider-backed cover, a ring shows required get-up space, and invalid headings
remain uncommitted. Enemy Push Off chooses deterministically through the same
validator. Both player characters also carry the Combat Knife in hotbar slot 5,
so reach rejection, committed wounds, diagnostics, the authored stab/contact
timing, and wound-driven reactions can be tested now.

The 2026-08-12 review follow-up is complete: silhouette exposure is cached,
displacement and blast rules use canonical Application paths, scenario content
is assembled once, availability/diagnostics are authoritative projections, and
EditMode plus sustained-frame PlayMode gates run before WebGL. The reviewed
`GameplaySession`, HUD, controller, editor, scenario-assembly, and validation
splits are complete and protected by focused tests and repository budgets.

## 0. Repository and engine scaffold

- Choose the Unity release and render pipeline.
- Create the minimal Unity project and bootstrap scene.
- Separate platform-neutral domain assemblies from Unity-facing presentation.
- Add pure-C# edit-mode tests and target-specific smoke-test hooks.
- Establish WebGL and Windows build profiles.

**Exit:** the empty application builds for both targets, and domain tests run
without entering play mode.

## 1. Portable level pipeline

- Define the first schema-versioned level document and stable entity IDs.
- Represent cover, interaction points, and authored destructible-object states.
- Implement validation and the shared level loader.
- Add desktop-file and browser-storage adapters.
- Create a tiny hand-authored fixture level for automated tests.

**Exit:** one data file loads into the same logical level on WebGL and Windows,
and invalid data produces actionable validation errors.

## 2. In-game editor slice

- Add camera navigation and entity selection.
- Place, transform, delete, undo, and redo a small set of primitives.
- Author physical-interaction and destructible-state properties; derive physical
  cover from projected collision geometry rather than authored cover volumes.
- Import/export level data and save a browser-local draft.
- Switch directly between edit and play modes.

**Exit:** a level can be created in a browser, exported, committed, and loaded
unchanged by a desktop build.

The core editor architecture and construction workflow are implemented. The
metadata-authoring path and folder-discovered committed level library are also
implemented: a browser export uploaded as one JSON file can be validated,
listed, edited, and played by the next branch preview. The recommended order
for further expansion is tracked in the
[level-editor expansion plan](LEVEL_EDITOR_NEXT_STEPS.md).

## 3. Gridless turn slice

The concrete tactical-camera implementation sequence, animation boundaries,
and active checklist for this phase are maintained in
[PLAYABLE_THIRD_PERSON_SLICE.md](PLAYABLE_THIRD_PERSON_SLICE.md).

- Add a persistent HUD action that enters turn-based mode from continuous
  exploration without requiring combat or hostility.
- Establish deterministic initiative for relevant nearby actors. Outside an
  initiated encounter, returning to exploration completes the current tactical
  interval, advances matching environment behavior, and replenishes resources;
  initiated encounters retain their economy until resolved.
- Add one controllable combatant and one authored hostile combatant.
- Preserve readable shoulder-camera framing at walls with a presentation-only
  player cutout that does not change collision, cover, or future line-of-sight
  rules.
- Support a shared-look first/third-person toggle, with a tight left-shoulder
  third-person composition and stance-aware eye position in first person.
- Project authoritative crouch state into a looping crouched idle/sneak family
  and prevent sprint input from bypassing crouched movement speed.
- Keep authored multi-level routes physically connected and covered by
  character-controller traversal tests.
- Preview a continuous path and its world-unit cost.
- Keep AP and movement separate, with one Mobile and one Set action demonstrating
  different movement-opportunity costs.
- Resolve authoritative movement without frame-rate dependence.
- Record and replay the resolved movement action.

**Exit:** the player can explicitly enter and safely leave turn-based mode, and
WebGL and Windows agree on the meaningful end state of the same recorded
movement and action-economy result.

## 4. Cover, displacement, and vehicle momentum slice

- Keep combatant movement free-turn and distance-budgeted; do not add retained
  speed, braking, or turning-radius state to characters.
- Add standing and crouched stances, then derive protection from stance-adjusted
  target regions and the current geometry of one destructible prop.
- Add explicit STAND and CROUCH command-bar buttons backed by the same stance
  command as keyboard input. Author the stance-change AP cost in gameplay data,
  show it in the button tooltip, and make availability reflect active turn,
  remaining AP, and spatial clearance. Do not leave stance changes as a free
  presentation-only toggle.
- Damage the prop through explicit states and update its collision, navigation,
  and cover contribution.
- Use one Throw resolution path to move either the prop or a combatant to a
  continuous-space destination.
- Resolve combatant displacement through an opposed Close-Quarters Control
  check with at least one talent modifier.
- Add momentum only to one vehicle mover, with speed, acceleration, braking, a
  visible forward movement envelope, whole-path curvature validation, and
  retained final speed and facing.

**Exit:** crouching changes which stable target regions geometry exposes;
destroying an obstruction changes that result; props or combatants can be
displaced through recorded, deterministic actions; and vehicle momentum visibly
limits a vehicle path without affecting character movement.

**Status:** implemented. Target exposure is captured as an immutable six-region
observer-relative silhouette raster. The nearest authored body-region volume
claims each flattened cell, so nearer anatomy removes hidden regions before the
hit-location roll; real world colliders then remove covered cells. Destructible
props use recorded intact,
damaged, and destroyed states whose presentation changes the physical collider.
Prop and combatant Throw resolution share one recorded continuous-space path;
combatants add a recorded opposed Close-Quarters Control check with the
`talent.leverage` modifier. Vehicle speed, braking distance, forward envelope,
whole-path curvature, final speed, and final facing live in a vehicle-only
momentum session. Temporary hotkeys were deliberately not added; later action
selection can call these focused controllers without inventing duplicate rules.

## 5. Line-of-sight attack slice

- Query visibility and cover by region.
- Resolve a seeded hit roll and one functional wound effect.
- Record rolls, modifiers, and outcomes for replay.
- Render each recorded attack through Combat Diagnostics down to its input
  values, modifiers, formulas, seeded rolls, and final outcome.

**Exit:** moving behind geometry changes exposed regions, and the recorded attack
replays consistently across targets.

**Status:** implemented. Stable Head, Torso, left/right Arm, and left/right Leg
volumes produce an immutable stance-adjusted exposure snapshot from the current
geometry. The Unity query flattens them from the attacking actor's origin into
a small deterministic region-ID raster, accumulating only six visible/total
counter pairs rather than storing a texture. While
the player is active in turn mode, `LMB` resolves the pointer target through
seeded hit and visible-region rolls, spends the attack's authored cost, and
applies a cumulative movement penalty on a hit. The action journal records the
frozen exposure, seed, rolls, wound transition, and resulting budget so replay
never repeats a physics query. The collapsed Dialogue transcript can show the
complete formula through its Combat Diagnostics filter. Pointer acquisition
continues to show chance to hit and a depth-tested fluorescent-orange body
outline whenever the target is directly visible; its electric-blue ground halo
remains limited to the player's active turn.

The snapshot contract remains independent of its Unity query adapter. Seeded
resolution, replay, diagnostics, and HUD wound state consume only the frozen
six-region counts and never rerun or retain the raster.

**Encounter-trigger follow-up complete:** immediate weapons can discharge in
continuous exploration at zero AP without manufacturing an encounter for an
inert wall or target. Actor, prop, and vehicle content can explicitly author an
attack response that starts initiative. Surface acquisition preserves stable
level-entity IDs through the recorded discharge, while unconfigured geometry
uses the inert world target. Slow projectiles retain their separate impact-cycle
policy because a delayed blast can threaten subjects other than the pointer
target.

**Hostile-rifleman follow-up complete:** the depot now contains an authored
rifleman rather than an inert capsule. A platform-neutral decision session owns
hostility, perception/FOV acceptance, attack limits, movement scoring, and the
immutable journal record. A focused Unity tactical-query adapter captures LOS
and validates candidate routes; a per-enemy presenter owns weapon, movement,
and incapacitation visuals; and the turn director only composes those services.
Enemy attacks use the same seeded attack session, target-region exposure,
weapon catalog, muzzle effects, animation presenter, AP budget, and combat
diagnostics as player attacks.

**Tactical-confidence follow-up complete:** active-turn target selection now
compares every capable party member and prefers the highest-chance shot rather
than blindly choosing the nearest actor. Rifleman behavior authors a minimum
acceptable hit chance; low-confidence exposure requests bounded route evidence,
moves only for a strictly better firing position, and falls back to the legal
shot when no candidate improves it. The detailed ownership contract and next AI
slices are recorded in [ENEMY_AI.md](ENEMY_AI.md).

**Exploration-detection follow-up complete:** detection no longer commits to the
first visible actor in roster order. Each scan captures frozen exposure for the
whole capable party, rejects candidates outside authored perception, and selects
the most exposed detection with distance and stable party order as deterministic
tie-breakers.

## 6. Projectile and explosion slice

- Advance one slow projectile through turn time with segment collision queries.
- Resolve impact using the world state at arrival.
- Read launch AP cost from the weapon rather than imposing a universal full-turn
  cost.
- Let one high-threat projectile create an abbreviated emergency initiative pass
  without restarting or extending it when another projectile is launched.
- Resolve blocking-geometry collisions early and the remaining impact before the
  original attacker's next normal turn.
- Add a thrown explosive with a visible uncertainty region.
- Resolve landing, blast distance, obstruction, and friendly fire.

**Exit:** moving, intercepting, perception, and cover meaningfully affect a
recorded impact cycle and delayed attack, with no authoritative Rigidbody
dependency.

**Slow-projectile checkpoint complete:** projectile-capable weapons now carry
authored speed, radius, maximum range, stance-aware launch heights, emergency
reaction eligibility, and their own action cost. Launch creates an immutable
action outcome and in-flight state; explicit turn-time advances query each
traveled sphere-cast segment against the current Unity world, prefer the nearest
blocker, and freeze its stable entity ID and world revision at impact. Each
advance is journaled and can be committed during replay without repeating the
collision query. The production depot scenario equips a rocket that stages at
the position committed from the launcher's remaining AP, keeps spinning with an
authored smoke trail, and projects a fluorescent-orange wireframe ghost to the
predicted reaction endpoint. Committed advances play with an authored short
acceleration ramp.

**Emergency-reaction checkpoint complete:** emergency triggers now use a shared,
application-owned cycle session; slow projectiles plug into it through a small
resolution adapter rather than owning initiative themselves. Eligible launches
arm one cycle only when predicted impact is inside the next normal turn. When
the attacker yields initiative, the cycle freezes the remaining combatants in
initiative order and grants each the same travel-derived AP budget through the
ordinary action system. The trigger advances once after the complete response
pass and queries the updated world before restoring the attacker's normal
budget. Additional launches cannot restart or extend the active window. Pending,
active, and completed transitions plus every flight advance are journaled as
immutable records; Unity presentation only plays committed results.
The same lifecycle can support later alarms, collapsing terrain, overwatch, or
other timed threats without duplicating emergency budget and rotation rules.

**AP-time impact-cycle follow-up complete:** the immutable launch record carries
the normal-turn allowance and post-cost AP. Remaining AP commits pre-reaction
travel, so firing earlier stages the rocket farther along while firing as the
final action gives it no head start. A side-effect-free prediction opens a
reaction only for impact inside the next normal turn. Ceiling converts the
predicted fraction to whole AP; every responder receives that allowance, and
the projectile re-queries and advances once after the full response pass.

**Thrown-explosive foundation complete:** immutable throwable definitions now
describe authored AP cost, range, uncertainty growth, and blast radius. The
application resolves and records the sampled landing point, geometry-adjusted
landing point, world revision, and exposed blast candidates behind abstract
uncertainty and world-query ports. A deterministic seeded sampler stays inside
the advertised region, while a side-effect-free preview reports the same radius
without sampling or spending AP. Unity aiming visuals, playback, and recorded
blast consequences consume that same authored definition and frozen result.
The depot scenario now authors a frag grenade as a hotbar consumable, and throw
commit validation requires the actor to own an exactly matching throwable
definition rather than accepting caller-created combat data.
Hotbar activation now routes the authored grenade into a Unity world-query
adapter: the pointer chooses the intended point, collision determines the
recorded landing point, and actor blast exposure is frozen before playback.
Hotbar activation now enters an explicit grenade-aiming state before commitment.
The actor holds the selected grenade ready in their right hand while a sampled
trajectory connects that presentation origin to the intended landing point.
Separate ground rings display the deterministic uncertainty region and authored
blast radius; pressing the reassigned hotkey a second time commits the throw,
while the shared cancel command dismisses the held grenade and preview without
spending AP or sampling a landing. Committed throws now play a
short arc to the recorded landing point and a blast-radius-scaled impact flash;
their recorded exposure applies authoritative wound and movement consequences,
including friendly fire, without repeating the world query during replay.
Schema 10 gives each consumable stack an authored positive quantity. Throw
actions freeze the exact previous, consumed, and resulting count alongside the
landing and blast evidence; stale or duplicated consumption is rejected before
budget or inventory mutation. Depleted stacks remain visible at `x0`, are
disabled by shared Application availability, and explain the requirement in
their data-derived tooltip.

## 7. Authored roster and character-persistence slice

- Define one character with fixed baseline attributes, skills, and talents. **Complete:** depot yard now authors Mara Vance as a Field Operative with fixed ratings and talents.
- Make core attributes authoritative rather than duplicating their outputs in
  scenario data. **Complete:** schema 8 requires Strength, Dexterity, Grit, and
  Charisma for every actor profile; base initiative and movement are derived
  from Dexterity, while initial combat order maps Dexterity to a deterministic
  reaction advance sized by the friendly-plus-hostile participant count and
  publishes the full formula in Dialogue. Opposed displacement includes
  Strength, and Grit/Charisma expose
  typed resistance/social modifiers without adding an HP system or placeholder
  social combat behavior.
- Equip and replace an item without altering the character's authored identity. **Foundation complete:** equipment remains actor runtime state rather than character identity data.
- Keep Character Creator and runtime state separate. **Corrected 2026-08-17:**
  appearance, attributes, skills, talents, and starting loadout are authored
  before a level. No runtime XP, points, bonuses, advancement options, or
  player-side spending UI exists.
- Persist equipment separately from authored identity and mission combat state. **Complete:** a
  versioned Application-owned party save validates the exact identity roster
  and equipped-item ownership, while a PlayerPrefs adapter persists equipped
  items across launches without rewriting authored identity. Wounds and action
  budgets are deliberately not durable, including when legacy saves are loaded.
- Complete the close-quarters action family before companion deployment:
  - Replace `pushCost` and other verb-specific actor shortcuts with an authored
    displacement-action collection containing stable ID, intent, cost, accepted
    subjects, reach, distance, mass, size, hand requirement, auto-stow policy,
    contest policy, and allowed results. **Definition checkpoint complete:** scenario
    schema 8 retains one family-level `Displace` ability with its hotbar slot
    and an ordered collection carrying the complete policy per action; assembled actor definitions,
    action-first targeting, resolution, cost validation, and journal records now
    carry the stable action ID without a Push-specific shortcut or runtime
    presentation factory. **Selection hierarchy restored:** the hotbar and
    reassignment list expose `Displace` once, and its laser-reveal flyout lists
    the authored intents with availability, cost, hover details, mouse selection,
    numbered selection, same-hotkey close, outside-click close, and Esc close.
  - Prevalidate any required weapon stow and displacement as one atomic action
    plan. Display the combined authored cost before confirmation; failure or
    cancellation spends nothing and leaves equipment unchanged. **Complete for
    Throw:** inventory weapons author occupied hands, availability exposes the
    combined stow plus action cost, and commit records both outcomes atomically.
    Current Push has no free-hand requirement.
  - Keep target eligibility in Application. Presentation maps the pointer hit
    to a stable candidate ID and renders the structured validity result.
    **Eligibility checkpoint complete:** assembled subject profiles now own kind,
    mass, and size, while Application returns explicit unavailable, self-target,
    wrong-kind, overweight, oversized, and out-of-reach results. Presentation no
    longer guesses target kind from raw scenario content.
  - Expose the recorded prop/combatant Throw paths through normal action
    selection and authored AP costs. **Complete:** Throw selects a subject and
    then an arbitrary valid destination. Authored size gates eligibility and a
    continuous mass-decay function determines the subject-specific maximum
    distance.
  - Push props through the shared displacement query rather than a
    presentation-only transform; the first authoritative Push path now records
    its action kind through the existing displacement journal. **Push action
    checkpoint complete:** scenario-authored AP cost, active-turn validation,
    affordability, ordinary action journaling, and the committed displacement
    journal now form one application command path.
    The presentation contract is action-first: select Displace and its intent,
    then point at a subject; invalid pointer subjects show `INVALID TARGET`
    instead of filtering the action list before engagement. **Player-facing
    checkpoint complete:** Push now previews its intent-derived path away from
    the actor while the pointer is on an eligible subject, finds the farthest
    unobstructed destination up to its authored distance, and commits directly
    when that subject is confirmed. Explicit destination aiming remains part of
    Throw rather than Push. Exploration Push records the same action and
    displacement evidence at zero AP; turn-mode Push spends its authored AP.**
  - Add toppling state for eligible props, including collision, cover, and
    navigation changes after they fall.
  - Add opposed pushes and throws against combatants using
    Close-Quarters Control, reach, mass, and obstruction constraints.
    **Combatant Push checkpoint complete:** the existing target actor is an
    accepted Push subject; preview remains roll-free, confirmation records the
    opposed check, failed control spends the action without moving the target,
    and successful control commits the actor displacement. Combatant Throw now
    uses the same contest and destination flow; Pull is subsumed by directional
    Throw rather than exposed as a redundant action.
  - Add knife attacks as authored reach-limited attacks with ordinary AP,
    seeded hit resolution, wounds, animation, and replay diagnostics.
    **Complete:** contact delivery is an explicit schema/domain contract;
    Application owns actor-only reach validation and records the evidence,
    while targeting, tooltips, enemy decisions, diagnostics, and the authored
    strike presentation consume that same definition.
- Explore a deployable-drone player ability that adds the launched drone as a
  temporary character in combat initiative. Treat deployment, ownership,
  command range, destruction, and removal from rotation as authored gameplay
  rules rather than a presentation-only companion effect.

**Exit:** the character can develop and change equipment without becoming a
blank classless build or requiring a broad character creator.

## Queued after the first headless battle export — character generation

Once the first deterministic headless battle is exported and replayed outside
the live Unity session, add a character-generation workstream. This is
explicitly subsequent to that headless checkpoint, not a prerequisite for it.
The workstream must preserve the existing boundary: generated characters are
authored pre-level inputs with stable identities and starting capabilities;
they do not introduce runtime XP, point spending, or a second mutable character
progression model.

1. Define a seedable character-generation contract that produces validated
   character documents, appearance selections, attributes, skills, talents,
   and starting loadouts from an explicit content/catalogue revision.
2. Record generator seed, generator version, input catalogue hash, generated
   roster hash, and any validation rejections in the battle/fixture provenance
   so a headless encounter can recreate its exact roster.
3. Add deterministic roster and scenario-population fixtures: same inputs must
   generate the same roster; invalid combinations must fail closed; distinct
   seeds must remain reviewable as named cases rather than anonymous random
   output.
4. Run generated rosters through the same reducer, replay, and eventual
   scoreboard/battle-review route as authored rosters. Generator output is a
   test and experimentation input, never an alternate gameplay authority.
5. After the contract is stable, decide which generated roster families are
   retained as regression cases and which are disposable battle branches.

## Pre-release human pacing and battle review

- Add a human-facing dead-time compression policy for live AI turns and visual
  battle review. Skip or accelerate intervals with no meaningful movement,
  decision, reaction opportunity, threat change, or visible consequence, and
  provide a clear speed/skip control for the remaining playback. Preserve the
  complete authoritative transition order, causal timing, replay coordinates,
  and scoreboard data underneath; this is presentation-time compression, not
  simulation-time deletion. Validate that compression never hides a player
  reaction window, awareness change, projectile arrival, fire/world-state
  change, or other tactically relevant event, and that uncompressed playback
  remains available for debugging.

## 8. Continuous delivery loop

- Build WebGL previews and Windows artifacts from repository changes.
- Export a local plain-text gameplay snapshot with the current expected-behavior
  guidance for low-friction bug handoff.
- Publish browser previews in an access-controlled location if needed.
- Surface test and build failures before a change is merged.
- Document the browser-only edit/export/commit workflow.

**Exit:** a source change can become a browser-playable preview without installing
the Unity Editor on the testing computer.
