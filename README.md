# Grit Gud

Grit Gud is a code-first, turn-based CRPG built around continuous, gridless
positioning. The project is designed so that most gameplay and level work can
be done through source control and tested in a browser-hosted WebGL build.

The current repository contains the project foundation, a portable runtime
level editor, and a playable tactical vertical slice with party control,
turn-based actions, attacks, projectiles, displacement, consumables, wounds,
hostile turns, and replay-oriented diagnostics.

> [!CAUTION]
> **Asset boundary — read before adding or committing Unity files.**
> `E:\Projects\grit-gud-clean` is the canonical public-source workspace.
> Licensed or third-party editable assets—including Synty, Mixamo, animation
> packages, textures, FBXs, demo content, and their `.meta` files—belong only
> in `best-coder-open-now-near-me/private-assets`. Never copy those source
> files into this public repository, its `gh-pages` branch, a build artifact,
> or a broad `git add`. See [the private-assets guide](docs/PRIVATE_ASSETS.md)
> before changing asset references or setting up a new workstation.

## Design pillars

- Freeform positioning measured in world units; editor snapping is optional.
- Players can explicitly enter turn-based mode outside combat to coordinate
  stealth, positioning, and time-sensitive interactions.
- AP and movement are separate resources, but actions may consume movement
  opportunity when they require slowing, stopping, or carrying momentum.
- Momentum-based movers use speed-dependent curved paths and forward movement
  envelopes rather than pivoting freely.
- Geometry matters for range, line of sight, exposed anatomy, cover, and
  projectile interception.
- Cover and props are destructible and can be pushed, pulled, or thrown into
  combatants through shared displacement rules.
- Attacks resolve through rules and hit rolls rather than precision physics.
- Slow projectiles remain in the world and resolve when they arrive.
- High-threat ordnance can compress initiative into an abbreviated emergency
  response cycle before impact.
- Explosives trade direct-hit accuracy for uncertain landing and spatial blast
  effects.
- Wounds can impair movement, perception, and attacks without requiring a
  separate health bar for every limb.
- Characters have authored identities and starting capabilities; appearance,
  starting build, loadout, and equipment provide customization. There is no
  runtime XP, point-spending, or character-progression system.
- Levels are portable, versioned data edited through an in-game level editor.

## Development targets

- **WebGL:** browser-playable development previews and in-game level editing.
- **Windows:** initial desktop shipping and validation target.
- **Other Unity targets:** possible later, provided they preserve the same
  platform-neutral game rules and data formats.

Start with [the project foundation](docs/PROJECT_FOUNDATION.md), then see
[the implementation roadmap](docs/ROADMAP.md) and
[development setup](docs/DEVELOPMENT.md). Third-party source material is listed
separately in the [asset inventory](docs/ASSETS.md). The runtime construction
workflow and controls are documented in the [basic level editor guide](docs/LEVEL_EDITOR.md).
Reusable visual character authoring is documented in the
[character editor guide](docs/CHARACTER_EDITOR.md).
Editor extension points and ownership rules are documented separately in the
[level-editor architecture guide](docs/LEVEL_EDITOR_ARCHITECTURE.md). The active
editor readiness decision and sequenced expansion work are in the
[level-editor expansion plan](docs/LEVEL_EDITOR_NEXT_STEPS.md). The active
terrain ownership model and delivery sequence are documented in the
[terrain-editor architecture plan](docs/TERRAIN_EDITOR_ARCHITECTURE.md). The active
plan for the first playable character, shoulder camera, turn movement, and
animation integration is tracked in the
[playable third-person slice](docs/PLAYABLE_THIRD_PERSON_SLICE.md).
