# Animation Architecture

## Purpose

Grit Gud uses Unity's Animator/Mecanim for authored state selection and clip
blending, followed by one ordered post-animation transform solve for weapon
contacts. Gameplay state remains authoritative outside both systems. Animator
states, parameters, clip time, and procedural transforms are presentation
details and never decide gameplay outcomes.

## Runtime flow

1. `ThirdPersonMotor` resolves movement and facing from its authored
   `ActorMotionProfile` during `Update`.
2. `WeaponAimPresenter` may rotate an actor toward a presentation target.
3. `ActorLocomotionAnimationPresenter` projects the resulting movement and
   rendered turn into one semantic `ActorAnimationFrame`.
4. `ActorAnimationCoordinator` resolves the frame, weapon pose, and action
   requests through an `ActorAnimationProfile` and writes through
   `AnimatorDriver`.
5. Mecanim evaluates locomotion, turn, weapon-pose, recoil, masks, and clips.
6. `WeaponAimRig` performs the only post-animation weapon solve in
   `LateUpdate`: animated primary grip, body aim, primary-grip realignment,
   weapon aim, procedural recoil, primary-wrist rotation, then support-arm IK.

The script order is declared by `ActorAnimationUpdateOrder`:

- motor: `-200`
- aim presentation: `100`
- locomotion projection: `200`
- post-animation solve: `300`

The post-animation solver recomputes its corrections from the Animator-authored
base pose every frame. No other component may write the same humanoid bones or
held-weapon anchor after Mecanim. A new dependent procedural operation must be
added to this solver in an explicit order; it must not install another rig
graph or an independent `LateUpdate` writer.

## Ownership boundaries

### Gameplay and application

Own movement, equipment, attacks, hit results, stance, incapacitation, and
authoritative actor facing. They do not depend on controller states, layers,
parameter hashes, clips, or IK targets.

Scenario JSON is compiled once into `ScenarioActorRuntimeDefinition`,
`ScenarioObjectiveRuntimeDefinition`, and `ScenarioVehicleRuntimeDefinition`
at the application boundary. Presentation consumes those definitions; it does
not reinterpret raw content DTOs. `ActorPresentationCatalog` maps each stable
actor presentation ID to its prefab and initial input policy.

### Semantic animation contract

- `ActorAnimationAction` names action intent such as interact, fire, reload,
  throw, jump, hit reaction, and incapacitation.
- `ActorAnimationPoseIds` provides stable animation-set identifiers.
- `ActorAnimationProfile` maps semantic requests to controller-specific
  layers, states, parameters, weights, transitions, turn-in-place policy, aim
  rates, and recoil transition policy.
- `ActorMotionProfile` owns physical walk, sprint, crouch, acceleration,
  gravity, facing, and fall-reset tuning used by `ThirdPersonMotor`.
- Each `ActorWeaponAnimationSet` owns its pose and recoil playback, layer,
  transition, kick, hold, and recovery values.
- `ActorAnimationCoordinator` is the normal entry point for frames, poses, and
  actions.
- `AnimatorDriver` is the only component that writes ordinary Animator state.

Callers may submit semantic action and animation-set IDs. They must not
introduce controller state names, layer names, parameter strings, or weapon
kind conditionals.

Runtime defaults must not duplicate authored profile values. The default actor
generator writes the canonical profile assets and focused validators compare
the generated controller, profiles, and prefab bindings against that recipe.

### Weapon presentation

- `GameplayWeaponPresenter` coordinates committed gameplay events and
  animation intent.
- `WeaponMountPresenter` owns held-prefab lifetime, sockets, visual materials,
  render layers, disabled physics, and contact-swing mount rotation.
- `WeaponActionEffectsPresenter` owns muzzle flash, light, tracer lifetime, and
  contact-strike timing.
- `WeaponAimPresenter` projects targeting state and authoritative facing into
  the post-animation solver.
- `WeaponAimRig` owns the single ordered post-animation weapon solve.
- `WeaponRigSocketSet` is the authored prefab contract for the primary grip,
  muzzle, support grip, and elbow hint.

## Adding content

### Weapon animation set

1. Author the states or blend trees on the intended controller layers.
2. Add an `ActorWeaponAnimationSet` with a unique stable ID to the actor
   animation profile.
3. Set the weapon presentation entry's `animationSetId` to that exact ID.
4. Author and validate the weapon prefab's `WeaponRigSocketSet`.
5. Run the generic profile/controller validation and add any weapon-specific
   pose or contact coverage.

### Animation action

1. Reuse an existing `ActorAnimationAction` when it expresses the intent.
2. Otherwise add a semantic action and bind it in every applicable actor
   profile.
3. Request it through `ActorAnimationCoordinator.TryRequestAction`.

### Post-animation operation

Add a dependent operation to `WeaponAimRig.SynchronizeAfterAnimation` in the
order in which it consumes and changes transforms. Extend the PlayMode contact
test whenever the operation can affect either hand, the weapon muzzle, or aim.

## Mecanim policy

Mecanim remains responsible for ordinary locomotion, Avatar Masks, retargeted
clips, blend trees, and authored transitions. The semantic profile boundary
contains its string-based controller contract, and validation must fail before
play when that contract is incomplete.

Move specialized state selection to Playables only if controller combinations
grow substantially, layers must be assembled dynamically per actor, or precise
deterministic presentation timelines become a product requirement. Root motion
remains disabled because movement is authoritative outside animation.

## Authored close-quarters and ragdoll handoff

Primary character motion remains authored rather than synthesized from weapon
mount transforms. The private Mixamo overlay now supplies `Knife Idle`,
`Stabbing`, `Push`, `Shoulder Hit And Fall`, and `Fall Over` with stable Unity
`.meta` files. The project-owned controller and animation profile will bind
those clips to knife pose, contact attack, displacement, hit-reaction, and
incapacitation semantics after Humanoid/in-place import validation. Procedural
code remains limited to bounded aim, weapon contact alignment, recoil, and IK.

An incapacitating reaction may hand off from `Shoulder Hit And Fall` or
`Fall Over` to a ragdoll at an authored normalized time. Application still owns
the incapacity result and authoritative actor pose. Presentation captures the
current animated skeleton, enables a joint-limited ragdoll, applies a bounded
impulse derived from recorded attack or blast evidence, and freezes the body
after it settles. Ragdoll contacts may affect presentation but never revise the
committed wound, position, initiative, collision policy, or other gameplay
outcomes.

Replay must not rerun PhysX to recreate that fall. The bounded live replay
window records a compact, quantized trace for an explicitly versioned set of
ragdoll bones from handoff through settle. Replay samples and interpolates that
trace during forward playback or seeking; backward seeking is therefore
reversible, and replay exit restores the untouched live presentation exactly.
If no valid trace exists, replay uses the authored fall without inventing a new
physics result.

## Validation

Presentation EditMode tests validate animation-channel ownership, profiles,
controllers, masks, prefabs, and weapon socket contracts. PlayMode tests must
exercise the production prefab through committed equipment and attack events,
and verify stable pose selection, contact stability, recoil restart, and full
recovery across actual Animator evaluation.
