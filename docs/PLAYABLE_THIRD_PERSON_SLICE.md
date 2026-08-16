# Playable Tactical Camera Slice

This document is the working plan for turning the Depot Yard main level into the
first playable Grit Gud scenario. It refines roadmap phase 3 and is the checklist
to update as implementation proceeds.

## Target experience

The player starts near the Depot Yard's south gate with a humanoid character
framed by a tight, left-shoulder third-person camera. The player can toggle a
stance-aware first-person view without changing movement or tactical rules.
During exploration, movement is continuous and camera-relative. The player can
enter turn-based mode at any time, preview a continuous route with an animated
ghost, commit that route, and interact with the deck objective.

The first slice is deliberately narrow:

- one controllable actor and one inert target;
- one main-level objective on the raised deck;
- camera-relative exploration movement;
- explicit entry to and safe exit from turn-based mode;
- a deterministic movement budget and path record;
- object-owned interaction plus distinct End Turn and Exit Turn-Based
  commands; and
- WebGL and Windows results that agree on authoritative outcomes.

## Non-negotiable boundaries

### Gameplay owns outcomes

Domain and application state own position, facing, movement cost, AP, action
resolution, and replay records. Unity physics supplies spatial queries. The
Animator presents already-resolved state and never spends resources or decides
whether an action succeeds.

Animation events may produce presentation cues such as footsteps or effects,
but gameplay completion cannot depend on an animation event firing.

### Movement owns the root

Exploration and resolved turn movement use project-controlled kinematic motion.
Locomotion clips are in-place and `Animator.applyRootMotion` remains disabled.
This prevents clip choice, playback rate, frame rate, or retargeting differences
from changing an actor's authoritative destination.

Root-motion clips may be evaluated later for special actions, but any such action
must follow a recorded gameplay trajectory. Visual motion is fitted to that
trajectory rather than becoming its source.

### Project code owns the animation contract

Third-party clips and demo controllers remain vendor source material. Runtime
prefabs reference project-owned animation profiles and controllers beneath
`Assets/GritGud/Presentation/Actors`. A standard parameter contract allows the
same locomotion presenter to drive the player, planning ghost, and replay actor.

The initial semantic animation inputs are:

| Input | Meaning |
| --- | --- |
| `MoveX` | Local lateral movement, normalized to the configured run speed. |
| `MoveY` | Local forward movement, normalized to the configured run speed. |
| `Speed` | Absolute horizontal speed in world units per second. |
| `Grounded` | Result of the movement controller's stable ground query. |
| `TurnRate` | Signed presentation turn rate. |
| action trigger | A presentation request for a resolved one-shot action. |

Parameter names are implementation details contained by the project-owned
presenter; domain code does not know about Animator hashes or states.

## Animation selection

The candidate libraries currently available are:

- Kevin Iglesias Human Animations: neutral and military idles, turns,
  eight-direction walk/run, sprint, jump/fall/land, and firearm actions for male
  and female humanoids;
- DoubleL RPG Animations: one-hand-up locomotion, attacks, shield/block, hits,
  ladder actions, and NPC dialogue/actions.

For the first playable slice:

1. Start with Kevin Iglesias in-place idle, turn, walk, and run clips as the
   exploration and route-playback locomotion family.
2. Use one Synty Battle Royale humanoid as the first player presentation and
   verify Humanoid retargeting before building the final controller.
3. Add DoubleL only where it supplies a coherent physical action family. Do not
   mix locomotion families inside one stance merely because a clip exists.
   The crouched stance uses DoubleL's paired looping idle and in-place forward
   movement clips because those are the complete crouch family in the installed
   source package; all crouched movement projects onto that cycle until authored
   strafe and reverse clips exist.
4. Keep stairs and valid slopes on grounded locomotion. When normal route input
   reaches an authored traversal link, automatically plan its frozen jump,
   vault, or mantle segment; do not expose a separate general-purpose Jump
   button. The current full-body traversal layer binds the authored
   `rifle jump` clip, while vault and mantle share it until dedicated clips are
   selected.
5. Keep the inert target visually simple until player movement and turn flow are
   stable.

## Delivery checkpoints

### 1. Asset and animation integration

- [x] Inventory candidate animation families and root-motion/in-place variants.
- [x] Keep editable third-party packages in a separate private repository while
      distributing referenced content only through compiled Unity players.
- [x] Add guarded, authenticated build-time installation of the private asset
      repository.
- [ ] Remove or exclude unnecessary Unreal duplicates, demo content, and source
      archives if the complete packages are not required.
- [x] Create the project-owned actor animation profile and presenter contract.
- [x] Retarget a representative in-place locomotion set to the chosen Synty
      military humanoid through a generated project-owned controller and profile.
- [ ] Validate loops, avatar configuration, root-motion disablement, and basic
      foot sliding in the Unity Editor.

**Exit:** a project-owned actor presentation can play idle and directional
locomotion without animation changing the actor root.

### 2. Exploration controller and gameplay camera

- [x] Create the initial player runtime projection, grounded south-gate spawn,
      fall recovery, and inert yard training target.
- [x] Add camera-relative WASD movement, acceleration, gravity, grounding,
      slopes, and stairs.
- [x] Connect both raised-deck approaches with two 1.5 m stair flights and a
      clear 3 m landing that the gameplay character controller can traverse.
- [x] Add an isolated perspective gameplay camera with mouse-look, pitch limits,
      over-the-shoulder framing, player-aware obstruction handling, and clean
      restoration of the menu/editor camera.
- [x] Toggle between a tight, left-shoulder third-person composition and a
      stance-aware first-person view with `V`, while keeping one shared look
      controller and excluding only the local player renderers in first person.
- [x] Preserve the intended shoulder distance through walls and clip a small,
      feathered screen-space opening only from wall color, depth, normal, and
      outline passes when a wall covers the player.
- [x] Remap both environment and player source materials through the shared cel
      surface path while preserving their authored colors and textures, and add
      the explicit skinned outline pass needed to read the player in dark scenes.
- [x] Feed resolved local velocity, grounding, and turn rate into the animation
      presenter without giving it movement authority.
- [x] Toggle the authoritative standing/crouched stance with `C`, preserve the
      capsule bottom while crouching, and reject standing beneath an obstruction.
- [x] Drive crouched idle and in-place sneak locomotion from the authoritative
      stance, limit crouched exploration movement to sneak speed, and preserve
      the stance on the tactical route ghost.
- [x] Translate the third-person camera down by the exact standing-to-crouched
      head displacement while preserving the complete shoulder offset, so the
      reduced view over nearby cover matches the actor's crouch.
- [x] Resolve stance intent through a spatial-query adapter, commit an immutable
      stance-change record, and project the accepted result afterward.
- [x] Project actor-specific stance shape, camera pivot, animation parameter, and
      stable Head, Torso, left/right Arm, and left/right Leg target-region
      volumes.
- [x] Keep the gameplay cursor released in Editor, Windows, and WebGL, preserve
      RMB-drag camera look, and reserve `Esc` for host/browser release without
      restarting gameplay or returning to the menu.

**Exit:** the player can traverse the Depot Yard and raised deck with readable,
animated movement in either tactical camera view.

### 3. Turn-session foundation

- [x] Add scenario, actor, objective, and gameplay-session contracts.
- [x] Enter turn mode explicitly without hostility and establish deterministic
      initiative.
- [x] Track AP and movement opportunity separately, distinguish voluntary turn
      sessions from initiated encounters, and replenish a voluntary session only
      after its completed cycle is exposed for matching environment behavior.
- [x] Enforce safe-exit rules in the application layer.

**Exit:** exploration and turn mode can be entered and exited without resource
exploits or presentation-owned rules.

### 4. Ghost-route planning and playback

- [x] Keep the authoritative actor still while directional input extends or
      revises a provisional continuous route.
- [x] Validate the full capsule path against ground, slope, step, and obstacle
      constraints.
- [x] Display route shape, destination, and movement cost.
- [x] Animate a non-authoritative ghost from planned velocity using the same
      animation profile.
- [x] Confirm into an immutable movement record; cancel without changing actor
      state.
- [x] Play the accepted route on the actor and derive final facing from its last
      meaningful tangent.

**Exit:** planning, confirmation, playback, and replay agree on destination,
cost, and facing regardless of rendering frame rate.

### 5. Actions, objective, and HUD

- [x] Invoke the raised-deck interaction from the object instead of exposing a
      generic Interact hotbar action; the target owns its label and turn cost.
- [x] Keep End Turn separate from Exit Turn-Based: End Turn advances encounter
      initiative or runs the locked world-turn phase before a fresh voluntary
      interval, while Exit Turn-Based safely completes a voluntary interval and
      returns to exploration.
- [x] Add the raised-deck objective and proximity interaction prompt.
- [x] Show mode, active actor, AP, remaining movement, route cost, objective, and
      available controls.
- [x] Keep the command surface translucent over gameplay and give all eight
      hotkey slots the same filled, cyan-framed treatment as the turn-mode
      control.
- [x] Present contextual expected behavior, rationale, and player tips from the
      shared stable-ID [gameplay guidance catalog](GAMEPLAY_GUIDANCE.md).

**Exit:** the player can reach and complete the first Depot Yard objective using
the intended exploration-to-turn flow.

### 6. Replay, validation, and builds

- [x] Test gameplay calculations independently from Unity presentation.
- [x] Test animation projection from representative velocities and action states.
- [x] Verify that changing clips or Animator speed does not change gameplay end
      state.
- [x] Record and replay accepted path points, cost, destination, and facing.
- [x] Run Editor tests plus Windows and WebGL smoke builds.
- [x] Audit referenced assets and WebGL download/memory cost.

**Exit:** the first playable is deterministic, reviewable in the browser, and
does not pull unused animation demos into the player build.

The 2026-08-09 development WebGL audit produced 119.2 MiB across 18 deployed
files, with no raw `.fbx`, `.blend`, `.psd`, `.unitypackage`, or archive files in
the deployed directory. Unity's player report attributes 9.3 MiB compressed / 77.3
MiB uncompressed to the Bootstrap level. The largest referenced sources are the
21.3 MiB generic normal atlas, two 10.7 MiB generic color/emissive atlases, the
5.8 MiB Battle Royale character file, and about 4 MiB of Modern GDR demo images
under a `Resources` folder. Texture import/downscaling and removal of demo
`Resources` content are the next size-reduction targets; this audit does not
authorize publishing raw licensed sources.

### 7. Authoring expansion

- [ ] Add actor spawn/configuration to the level document and in-game editor.
- [ ] Add animation-profile selection per actor archetype.
- [ ] Add objective and encounter authoring tools.
- [ ] Add upper-body layers, avatar masks, equipment poses, reactions, and NPC
      animation profiles as gameplay requires them.

**Exit:** additional actors and objectives can be authored without modifying
core gameplay or animation-driver code.

## Primary risks and checks

| Risk | Guardrail |
| --- | --- |
| Root motion changes gameplay | In-place locomotion, disabled root motion, and tests against resolved positions. |
| Ghost and actor diverge | Both consume the same sampled route data and animation-state projection. |
| Mixed packs look incoherent | Select one locomotion family per stance and treat other packs as action candidates. |
| Camera clips during animated motion | Obstruction queries use a camera collision radius and are tested through stairs, doors, and tight cover. |
| A nearby wall collapses or blocks the shoulder view | Wall colliders remain authoritative for gameplay but do not shorten the camera arm; wall-only shader passes open a soft player-centered view whose right boundary remains circular while its left side extends one-sixth of the viewport through the shoulder-camera corridor, without hiding floors, props, or actors. |
| First person exposes or disables the local actor incorrectly | A camera-only layer excludes local renderers while preserving physics, world shadows, stance state, and third-person restoration. |
| Animation state leaks into rules | One-way application-to-presentation data flow; no Animator reads in domain code. |
| Repository and build bloat | Curate raw packages, avoid vendor demo dependencies, and audit referenced build assets. |
| WebGL input traps the player | Gameplay never requests cursor lock; `Esc` may release host/browser capture without triggering application navigation. |

## Traversal verification

The published Depot Yard places two tall crates in a row east of the player at
the south side of the yard. The nearer crate has the bidirectional authored
`jump.connected-crate` link; the farther crate is deliberately disconnected.

1. Press `T` to enter turn mode, then use the ordinary movement keys to extend
   the ghost route east through the nearer crate. No Jump button is involved.
2. Confirm that the route snaps to the takeoff, draws a raised arc, and reports
   `JUMP - 2.25 MOVE - 0 AP`; press `Enter` to commit it.
3. Confirm that the actor plays the authored jump action and lands on the exact
   committed endpoint. Plan back across it to verify the bidirectional link.
4. Try to continue through the farther crate. Planning must stop because no
   traversal link authorizes that crossing.
5. Open replay and scrub across the committed movement. Forward playback must
   reproduce the same arc and Jump state; backward seeking and arbitrary
   scrubbing must not emit transient effects, and exiting replay must restore
   the exact live pose and animation/equipment state.

Slope boundaries are covered with real Unity colliders in automated tests:
45 degrees and the authored 50-degree limit are accepted, while 55 degrees is
rejected. They are intentionally not extra pitched modules in the published
yard.

## Directional Push Off verification

The south-yard `crate-pin-demo` starts directly between Mara and Oren so the
full pin-and-escape flow can be exercised without editing the level.

1. With Mara selected, activate hotbar slot `4` (`Displace`), choose `Push`,
   point at `crate-pin-demo`, and confirm. The intent-derived push sends the
   crate toward Oren; its toppled footprint must establish the authoritative
   pin.
2. Press `Tab` to select Oren, activate slot `4`, and choose `Push Off`. The
   exact pinning crate must lock immediately—there is no second prop-selection
   click—and the prompt must change to directional aiming.
3. Move the pointer around Oren. The line/ring and oriented crate wireframe are
   green for a legal heading and red when the swept path, final prop footprint,
   or standing get-up volume is blocked. The wireframe is the exact final
   collider-backed cover pose, not an animation estimate.
4. Confirm a green heading. Oren must become unpinned, the crate must remain at
   the previewed pose as usable collision/cover, and the action must spend its
   authored cost exactly once. Canceling or clicking while red must spend
   nothing and leave the pin intact.
5. Open replay and scrub across the Push Off. Replay must reproduce the exact
   chosen prop and actor state; exiting replay must restore the live pose,
   equipment, wounds, and animation presentation.

Enemies do not aim a cursor. A pinned enemy evaluates the same fixed heading
set and deterministically commits the farthest legal candidate, with stable
tie ordering.

## Current next action

Phase 3 acceptance remains complete after WebGL traversal, turn-cycle,
objective, camera, cursor, and shadow smoke testing. Phase 4 now provides
stance-adjusted target-region exposure, collider-backed destructible states,
recorded prop/combatant Throw resolution, opposed Close-Quarters Control, and a
separate vehicle-only momentum envelope. The collapsed Dialogue transcript is
ready to filter ordinary dialogue, system messages, and multiline Combat
Diagnostics independently. Crosshair acquisition now exposes that geometry as
a chance-to-hit preview and bright-orange body outline whenever the target is
directly visible; its cyan ground halo appears only during the player's active
turn. Phase 5 consumes that frozen exposure through a seeded hit roll and a
weighted visible-region roll, records the complete result for replay, applies a
functional wound movement penalty, and publishes every input and formula to the
Combat Diagnostics channel. The player fires the current crosshair target with
`LMB` during their active turn; no body-part aim controls are exposed.
Phase 6 now advances the depot scenario's authored rocket through turn time and
resolves its eventual collision against the world state at arrival. The
platform-neutral flight record, stance-aware launch profile, per-segment Unity
collision adapter, arrival-world revision, journal entry, and physics-free
replay path drive the real Synty rocket model. At launch, post-cost AP commits
its initial travel and a fluorescent-orange ghost previews the predicted impact
interval ahead. The rocket keeps rotating with an authored smoke trail, then
uses a short acceleration ramp to play each committed movement record. Ordinary
firearms remain immediate; emergency-response eligibility is explicitly authored
only on high-threat ordnance. Eligible impacts derive one shared reaction AP
allowance and re-query the world once after every responder has acted.
Authored automatic traversal is now complete through schema, planning,
clearance, playback, replay, animation, published connected/disconnected
fixtures, and EditMode/PlayMode lifecycle coverage. The next production slice
is close-quarters presentation. Directional Push Off now freezes a player-
chosen cover placement and validates the actor's get-up space; its remaining
lying, struggle, push, get-up, and bounded ragdoll work is presentation polish.
Next comes authored knife action timing, reactions, equipment transitions, IK,
interruption, replay, and restoration, followed by the bounded
incapacitation-to-ragdoll presentation experiment.
