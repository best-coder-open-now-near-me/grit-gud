# Third-Party Asset Inventory

Trusted development and build environments install these Synty source-asset
packages under `Assets/Synty` from the private-assets overlay:

- POLYGON Battle Royale;
- POLYGON Generic; and
- Synty Package Helper.

These files remain governed by their original third-party licences. They are
not granted for reuse or redistribution by this source repository and must not
be committed to it. Limit private-assets access to collaborators who have any
licences or seats required for their work on the project.

The imported packages have been validated with Unity `6000.4.10f1` and the
project's Universal Render Pipeline `17.4.0` configuration.

## Animation candidates

The following licensed animation packages are being evaluated locally for the
first playable third-person slice:

- DoubleL RPG Animations; and
- Kevin Iglesias Human Animations.

They contain Humanoid clips, root-motion and in-place variants, demo scenes,
demo Animator controllers, and source/export material for engines other than
Unity. Project runtime code must not depend directly on their demo controllers.
The selected clips will be consumed through project-owned animation profiles
and controllers as described in
[PLAYABLE_THIRD_PERSON_SLICE.md](PLAYABLE_THIRD_PERSON_SLICE.md).

The private `Assets/Mixamo` overlay additionally contains project-selected
`Knife Idle`, `Stabbing`, `Push`, `Shoulder Hit And Fall`, and `Fall Over`
source clips. Their Unity `.meta` files are part of the private overlay contract
and must be preserved alongside the FBX files. Project-owned import tooling will
enforce Humanoid retargeting, disabled root motion, the intended loop policy,
and semantic controller/profile bindings rather than relying on workstation
import defaults.

Their licences permit use in compiled Unity players. Editable source assets stay
in a separate private repository and are installed into the project only for
local development and trusted builds. Public Web previews contain only the
Unity-generated player output; the workflow must never publish the private
repository checkout or raw package files directly.

The private repository should preserve every Unity `.meta` file alongside its
asset so GUID references remain stable. Its checkout layout mirrors the Unity
project's `Assets` directory. The public repository ignores those installed
paths and contains the project-owned controllers, profiles, prefabs, and code
that consume the licensed assets.

Build-runner and local installation details are recorded in
[PRIVATE_ASSETS.md](PRIVATE_ASSETS.md).

Before the first asset-backed build, decide whether the private repository needs
the complete packages or a curated Unity-only subset. Unreal duplicates, demo
content, and source archives should be retained only when they have an identified
development use.
