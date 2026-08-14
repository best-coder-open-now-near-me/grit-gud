# Terrain Editor Architecture

## Readiness decision

The core architecture remains suitable for terrain work, with one important
constraint: Unity `TerrainData`, scene objects, meshes, and colliders must remain
runtime projections rather than authored sources of truth. Terrain authoring must
follow the same document → command → validation → projection flow as entities.

The existing separation of concerns is healthy:

- Domain owns portable data and validation without Unity references.
- Application owns reversible edits, transactions, history, and migrations.
- Tools translate input gestures into application commands.
- Presentation owns raycasts, handles, meshes, materials, and colliders.
- Persistence serializes detached snapshots and preview consumes an isolated
  snapshot.

The recent camera, framing, hover, and multi-selection work has stayed on the
presentation side. Batch rotation and deletion use composite commands rather
than bypassing history. Those changes do not weaken the terrain boundary.

## Terrain product decision

Start with a bounded heightfield surface, not arbitrary voxel editing or Unity
Terrain serialization. A heightfield supports outdoor elevation, ramps, shallow
depressions, grounding, raycasts, and later navigation while remaining portable
to WebGL and reviewable as versioned level data.

The heightfield sample lattice is an authoring and storage representation. It
does not create grid-based movement or change the game's continuous world-space
rules.

The first slice deliberately excludes caves, overhangs, erosion simulation,
runtime terrain destruction, texture painting, foliage, and arbitrary imported
heightmaps.

## Ownership model

### Domain

Add a typed `TerrainSurfaceData` to `LevelDocument` containing:

- a stable surface ID;
- world-space origin;
- sample counts in X and Z;
- sample spacing in world units;
- minimum elevation and elevation increment; and
- quantized height samples in deterministic row-major order.

Quantized samples keep JSON deterministic and avoid platform-dependent float
noise. Limits for dimensions, spacing, elevation range, and total samples belong
in a dedicated terrain validation rule.

Do not store Unity asset GUIDs, `TerrainData`, mesh vertices, collider state,
brush settings, selected regions, or material instances in the document.

### Application

Terrain edits use patch commands rather than replacing the entire heightfield.
A patch records its rectangular sample region plus exact before/after values so
one completed brush stroke is one undoable command. Commands report the affected
surface and region through a terrain-specific change contract; they must not
pretend a terrain patch is an entity ID.

Introducing terrain requires a schema increment and a migration that gives
existing levels an explicit empty terrain collection. Old JSON must remain
loadable through `LevelDocumentMigrator`.

### Tooling

The first `TerrainHeightLevelEditorTool` should support:

1. Raise/lower with a fixed-radius brush.
2. A visible brush footprint projected onto the surface.
3. Strength and radius controls with safe limits.
4. A temporary preview during the gesture.
5. One patch command when the pointer is released.
6. `Esc` restoration of the exact pre-gesture samples.

Brush radius, strength, and visualization are local editor preferences, not level
data. Input remains centralized in `LevelEditorInputRouter`, and raycasts remain
in a narrow terrain query service.

### Presentation

Add a `TerrainWorldProjector` beside `LevelWorldProjector`. It owns generated
mesh chunks, renderers, and colliders and accepts document snapshots plus terrain
patch notifications. Entity projection must not gain terrain-specific branches.

Chunking is required even in the first implementation so a brush stroke rebuilds
only intersecting mesh and collider chunks. Chunk layout is a projection detail
and must not be serialized. Normals should be calculated consistently across
chunk borders.

Play preview receives a detached terrain snapshot. Runtime deformation, if added
later, must never flow back into authoring state without an explicit authoring
command.

## Separation-of-concerns review

The foundation is sound, but terrain should not be composed by adding more
terrain fields and branches directly to `LevelEditorController` or
`LevelEditorGui`. Both are already broad composition/UI adapters. Before the
terrain tool grows beyond its first slice:

- compose terrain through a dedicated terrain editing coordinator;
- give the GUI a terrain panel model or narrow callbacks rather than more direct
  projector dependencies;
- move reusable outline/handle lifecycles into feedback services; and
- keep entity and terrain selection targets distinguishable.

These are scaling guardrails, not blockers for the first bounded prototype.

### Current architecture health

The dependency direction remains healthy: portable terrain does not reference
Unity, commands own authored mutation, and generated meshes/colliders remain
disposable presentation projections. Preview strokes operate on detached terrain
copies and commit through one application command.

Presentation composition is now the main scaling pressure. `LevelEditorController`,
`LevelEditorGui`, and `TerrainHeightLevelEditorTool` are broad enough that new
terrain modes should not add more unrelated responsibilities directly to them.
The projection slice has been separated into `TerrainWorldProjector`,
`TerrainMeshBuilder`, and `TerrainChunkTag` so lifecycle, deterministic geometry,
and ray-pick identity can evolve independently.

Before adding smoothing, flattening, painting, or additional terrain selection
modes:

- **Implemented:** stroke accumulation/commit is isolated in
  `TerrainStrokeAccumulator`, with brush patch creation in
  `TerrainBrushCommandFactory`;
- **Implemented:** brush footprint rendering and resource lifecycle are isolated
  in `TerrainBrushFootprint`;
- **Implemented:** `TerrainToolPanelModel` owns terrain-panel state, clamping,
  activation intents, and framing delegation instead of exposing the terrain
  tool directly to the GUI; and
- move selection-outline ownership out of `LevelEditorController`.

This is a **green** assessment for Domain and Application boundaries and a
**yellow** assessment for Presentation class size. There is no need to rewrite
the architecture, but the listed extractions should precede another large editor
feature family.

## Delivery sequence

### Phase 1 — portable terrain core

- **Implemented:** schema v2 terrain type, v1 migration, normalization, deep
  copy, and validation.
- **Implemented:** rectangular terrain patch commands with exact undo/redo.
- **Implemented:** serializer round-trip, migration, validation, and patch tests.
- **Implemented:** a small committed deterministic heightfield fixture for mesh
  and collider tests.

### Phase 2 — projection

- **Implemented:** generate chunked meshes and colliders from a snapshot in both
  editor and gameplay loading paths.
- **Implemented:** update only chunks intersecting a terrain patch.
- **Implemented:** sample-to-world mapping, chunk-border, triangle, and bounds
  tests. Collider refresh receives the same replacement mesh.

### Phase 3 — authoring tool

- **Implemented:** click-based raise/lower brush commits one patch command.
- **Implemented:** bounded radius and quantized strength controls, with Shift as
  a temporary lower modifier.
- Terrain validation remains integrated with the existing validation panel.
- **Implemented:** a world-space brush footprint previews radius and raise/lower
  intent without mutating authored data.
- **Implemented:** continuous strokes preview projected patches, commit one
  command on release, and restore authored projection on cancel.
- **Implemented:** terrain-specific framing includes all surface extents and
  sampled elevation ranges.
- **Implemented:** the centralized pointer route maps a primary touch to the
  same press/drag/release lifecycle used by mouse terrain strokes and preserves
  the release frame required for one-command commit.
- Verify the routed mouse and touch behavior in a deployed WebGL player.

### Phase 4 — gameplay integration

- **Implemented:** the main Depot Yard carries a projected terrain collider, and
  shared level-loader tests require terrain identity and collider mesh creation.
- **Implemented:** gameplay spawn placement and turn-based movement routes ground
  their actor roots against projected collider elevation. Focused projection
  tests cover both initial placement and route destination resolution.
- **Implemented policy boundary:** visual mesh/collider previews update immediately,
  but never invalidate navigation. A committed terrain patch publishes its exact
  sample rectangle through `TerrainWorldProjector.NavigationInvalidated`; full
  projection fallbacks request a full refresh. A future navigation adapter owns
  batching and rebuilding from those notifications rather than coupling that work
  to brush rendering.
- Confirm cover, line of sight, projectiles, and saved gameplay poses all use the
  same world-space terrain surface.

## First-slice acceptance criteria

Terrain is ready to expand when one small surface can be authored, undone,
redone, exported, imported, loaded on WebGL and Windows, and previewed without
mutating the authoring document. A brush stroke must rebuild only affected
chunks, and old schema documents must migrate without changing their entity
content.
