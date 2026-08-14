# Basic Level Editor

The basic level editor is a runtime tool shared by Windows and WebGL builds.
Open it from **Level Editor** on the start menu. **Play Main Level** opens the
same portable main level through the gameplay-side loader with authoring locked.

## Source of truth

The in-memory `LevelDocument` is authoritative while editing. Unity GameObjects
are disposable projections of that document and are never scraped to produce a
save. Each edit is applied to `LevelEditorWorkspace` through a reversible
command. Ordinary entity edits update only affected projections; document
replacement, imports, and preview transitions retain the validated atomic
full-world replacement path.

Portable JSON contains:

- an explicit schema version;
- stable level and entity IDs;
- stable archetype IDs rather than Unity paths or asset GUIDs;
- world-space position and yaw;
- interaction points plus legacy cover-volume compatibility data;
- optional initial destructible state and integrity; and
- typed, quantized heightfield terrain surfaces.

The loader validates the whole document before exposing a replacement world.
An invalid import therefore leaves the currently loaded level untouched.

## Controls

| Input | Action |
| --- | --- |
| `WASD` or arrow keys | Pan the camera |
| `Q` / `E` | Rotate the camera |
| Right-mouse drag | Orbit the camera and adjust its pitch |
| Middle-mouse drag | Orbit the camera and adjust its pitch |
| Mouse wheel | Zoom in responsive, distance-relative steps |
| `Shift` while using movement keys | Pan the camera three times faster |
| `F` | Frame the selected entity |
| `Home` | Frame the authored level bounds |
| Create → Place entry, then left click | Place the selected archetype |
| Create → Place ↺ / ↻ buttons | Rotate the active placement stamp left / right |
| Left click | Select an entity |
| `Ctrl` + left click | Add or remove an entity from the selection |
| Left drag | Move the selected entity on its current elevation |
| `R` | Rotate the placement preview or selected entity |
| `Delete` | Delete the selected entity |
| `Ctrl+Z` / `Ctrl+Y` | Undo / redo |
| `Esc` | Cancel placement or clear selection |
| Create → Terrain Raise/Lower, then left click | Adjust height samples with the configured brush |
| `Shift` while using the terrain brush | Temporarily lower terrain |

A primary touch uses the same press, drag, and release lifecycle as a left
mouse gesture. This supports selection, placement, and complete terrain-stroke
commits in touch-enabled WebGL players. Camera orbit and keyboard shortcuts
still require their documented mouse or keyboard inputs.

The inspector also accepts invariant-culture numeric position and yaw values.
Snapping is optional. Structural pieces default to the Synty kit's 2.5-unit
module and 90-degree rotation; props use finer position and angle increments.
Snapping affects authoring only and does not introduce a gameplay grid.

Hovering an entity in selection mode shows a yellow outline; the selected entity
uses a blue outline, and additional selected entities use green outlines.
Rotation and deletion apply to the complete selection as one undoable
transaction. Entity-specific validation messages are buttons that select and
frame the affected entity.

The Terrain create mode exposes raise/lower modes plus brush radius and quantized
strength. A green or red world-space footprint previews the raise or lower brush
radius under the pointer. Press and drag to preview a continuous stroke; moving
into a new terrain sample applies the brush once at that sample. Releasing the
pointer commits the complete stroke as one reversible patch command and rebuilds
only affected terrain chunks. `Esc` restores the authored terrain without adding
history.
Use **Frame Terrain** in the terrain panel to fit every authored terrain surface
in the camera view, including its minimum and maximum sampled elevations.

The toolbar is split into navigation/history and persistence rows so controls do
not run together on ordinary window sizes. The left panel has three stable
workspaces: **Create**, **Outline**, and **Scenario**. Create then switches among
Select, Place, and Terrain and shows only the active mode's controls. Outline
searches both world entities and scenario objects. Scenario owns player-start
and actor management; selecting an actor opens its editable properties in the
right Inspector. Use the **Shortcuts** toolbar button to show or hide the input
reference without leaving the editor.

## Archetype catalog

`Assets/GritGud/Content/Resources/DefaultLevelArchetypeCatalog.asset` is the
Unity-facing bridge between stable portable IDs and curated prefab references.
The first palette contains six entries:

- standard floor;
- standard wall;
- doorway wall;
- stairs;
- destructible crate; and
- destructible metal barrel.

The catalog owns stable definitions while exposing presentation, placement,
capability, and gameplay-default profiles separately. Levels never refer
directly to the third-party Synty directory. Future art replacement can
therefore preserve existing level files.

## Drafts and portable files

**Save Draft** writes the active document through the local draft adapter. On
WebGL this uses Unity's browser-backed `PlayerPrefs` storage. Drafts have a
750,000-character safety limit and should be treated as recoverable local
working state, not as the version-controlled source of truth.

**Export** writes portable JSON:

- WebGL starts a browser download through the committed `.jslib` bridge.
- Desktop and the Unity Editor write beneath
  `Application.persistentDataPath/Exports` and report the complete path in the
  status bar.

**Import** opens a browser file picker on WebGL. On desktop, enter a JSON path
in the inspector first. Imported text is size-limited, deserialized, and fully
validated before it can replace the active document.

The runtime and editor start from the committed main level at
`Assets/GritGud/Content/Resources/Levels/main-level.json`. The smaller
`basic-construction.json` remains a focused automated-test fixture.
The main Depot Yard now includes a flat `depot-ground` heightfield covering its
authored bounds, so terrain tools are immediately usable when the editor opens.

## Level Preview

**Level Preview** constructs a deep snapshot of the current document and locks
all authoring controls. Returning to edit mode discards the preview world and
rebuilds from the unchanged authoring document. Future movement, damage, and
destruction must continue using this boundary so runtime changes cannot leak
into saved levels.

**Test Play** launches the normal gameplay runtime from an isolated snapshot.
It uses exactly the authored scenario party, actor starts, objectives, physics
props, vehicles, and hostiles. Return to Editor discards gameplay state and
resumes the same authoring workspace.

## Deliberate v1 limits

- Position and yaw are authored; arbitrary pitch, roll, and scaling are not.
- Dragging preserves the entity's current elevation.
- Legacy cover-volume data remains readable for file compatibility but has no
  authoring surface. Destructible metadata is authored through entity capability
  controls.
- No runtime navigation bake, terrain sculpting, arbitrary asset import, or
  encounter scripting is included. Terrain's planned ownership and delivery
  boundaries are documented in the
  [terrain-editor architecture plan](TERRAIN_EDITOR_ARCHITECTURE.md).
- The runtime UI is intentionally code-driven while the final visual language
  and broader gameplay UI remain unsettled.

The extension contracts, ownership rules, and procedure for adding tools are
documented in [the level-editor architecture guide](LEVEL_EDITOR_ARCHITECTURE.md).
