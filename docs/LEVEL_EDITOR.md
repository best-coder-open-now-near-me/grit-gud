# Basic Level Editor

The basic level editor is a runtime tool shared by Windows and WebGL builds.
Choose a committed level on the start menu, then use **Edit Selected** or
**Play Selected**. Both routes begin from detached snapshots of the same
portable document.

## First work session

For a first production authoring session:

1. Open **New Level**, switch to **Scenario**, give the level a useful display
   name, and apply it. The stable ID is generated once and should not be edited.
2. Block out terrain and geometry, then configure actors and gameplay links.
3. Use **Save Draft** early and after meaningful edits. The draft is local to
   that browser profile or machine and is a recovery slot, not a portable file.
4. Use **Test Play** before handoff. Publish validation must pass before the
   isolated gameplay session can start.
5. Use **Export** before leaving the machine. The resulting JSON is the portable
   artifact to copy, commit, or move to another workstation.

A new level begins as an **UNSAVED DRAFT**. Back, New, Reload Source, Load Draft,
and Import ask for confirmation before replacing unsaved work. If a recovery
draft references a scenario actor template that is not installed, the actor
remains visible and editable as unavailable, while publish validation prevents
an invalid export or test play.

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
- optional initial destructible state and integrity;
- typed, quantized heightfield terrain surfaces; and
- scenario actor instances, objectives, props, vehicles, and complete objective
  action costs.

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
| Create → Terrain, then left drag | Raise, lower, smooth, or flatten with the configured brush |
| `Shift` while using Raise | Temporarily lower terrain |

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

The Terrain create mode exposes Raise, Lower, Smooth, and Flatten plus brush
radius and quantized strength. The world-space footprint changes color with the
active mode. Smooth moves samples toward their local neighborhood average;
Flatten captures the quantized elevation beneath the pointer when the stroke
begins and moves every visited sample toward that fixed height. Press and drag
to preview a continuous stroke; moving into a new terrain sample applies the
brush once at that sample. Releasing commits the complete stroke as one
reversible patch command and rebuilds only affected terrain chunks. `Esc`
restores the authored terrain without adding history.
Use **Frame Terrain** in the terrain panel to fit every authored terrain surface
in the camera view, including its minimum and maximum sampled elevations.

New levels begin with one flat `ground` surface covering their 50 × 50 meter
authored bounds. The Terrain Surface section edits the selected surface's width,
depth, and grid spacing; resizing keeps the surface centered, resamples existing
height detail, rebuilds its projection, and participates in undo/redo. Width and
depth must be whole multiples of the grid spacing. Imported legacy levels with
no terrain expose **Add Flat Terrain** in the same panel.

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
The default palette includes structural modules, traversal pieces, cover,
destructible props, vehicles, and larger environment props.

The catalog owns stable definitions while exposing presentation, placement,
capability, and gameplay-default profiles separately. Levels never refer
directly to the third-party Synty directory. Future art replacement can
therefore preserve existing level files.

## Drafts and portable files

**Save Draft** writes the active document through the local draft adapter. On
WebGL this uses Unity's browser-backed `PlayerPrefs` storage. Drafts have a
750,000-character safety limit and should be treated as recoverable local
working state, not as the version-controlled source of truth.

While a document is dirty, the editor also writes a rolling recovery snapshot
after 15 seconds without another edit. The three newest snapshots are available
under **Portable Files → Recovery Autosaves**. Loading one intentionally keeps
the document marked unsaved so it cannot be mistaken for a deliberate save.
Recovery history is local to the browser profile or machine and never replaces
the manually saved active draft.

**Export** writes portable JSON:

- WebGL starts a browser download through the committed `.jslib` bridge.
- Desktop and the Unity Editor write beneath
  `Application.persistentDataPath/Exports` and report the complete path in the
  status bar.

**Import** opens a browser file picker on WebGL. On desktop, enter a JSON path
in the inspector first. Imported text is size-limited, deserialized, and fully
validated before it can replace the active document.

The runtime and editor level chooser discovers committed JSON files directly
under `Assets/GritGud/Content/Resources/Levels/Published/`. Uploading one
exported JSON file to that folder is enough for the next build to validate it
and add it to the menu; there is no separate manifest to maintain. The smaller
`basic-construction.json` outside that folder remains a focused automated-test
fixture and is not shown in the player-facing library.
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
- No runtime navigation bake, arbitrary asset import, or encounter scripting is
  included. Terrain's ownership and delivery boundaries are documented in the
  [terrain-editor architecture plan](TERRAIN_EDITOR_ARCHITECTURE.md).
- The runtime UI is intentionally code-driven while the final visual language
  and broader gameplay UI remain unsettled.

The extension contracts, ownership rules, and procedure for adding tools are
documented in [the level-editor architecture guide](LEVEL_EDITOR_ARCHITECTURE.md).
