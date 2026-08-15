# Level Editor Expansion Plan

## Readiness decision

The level-editor foundation is ready to expand. New authoring features can be
added without replacing the document, command history, projection, validation,
schema migration, persistence, or tool-lifecycle boundaries.

This decision means the next editor work should extend the existing contracts;
it does not mean the first editor slice is feature-complete. The largest
remaining product gap from the original slice is authoring the gameplay metadata
that the portable document and validator already understand.

## Foundation already in place

| Capability | Expansion seam |
| --- | --- |
| Portable authored state | Add typed fields to `LevelDocument` and advance the schema through `ILevelDocumentMigration`. |
| Reversible edits | Implement `ILevelEditCommand`; group a gesture with `ExecuteTransaction`. |
| Editor tools | Implement `ILevelEditorTool`, consume `LevelEditorToolContext`, and register it in the composition root. |
| Scene synchronization | Report stable affected entity IDs for incremental projection, or request full projection for structural changes. |
| Selection | `LevelSelectionTarget` already represents entities, sub-elements, and multiple targets. |
| Validation | Add an `ILevelValidationRule` with stable issue codes and an explicit validation profile. |
| Persistence | Draft, import, export, and play preview all operate on detached document snapshots. |

The application and domain assemblies remain independent of Unity. Runtime
input, raycasts, prefabs, storage, and UI remain presentation adapters, so a new
feature should not need to move authoritative authoring state into a
`MonoBehaviour`.

## Recommended next vertical slice: gameplay metadata authoring

Implement interaction-point editing first. This closes the remaining authored
gameplay-metadata gap while exercising the intended sub-element, command, and
projection seams before encounter scripting or more complicated geometry tools
are introduced.

## Usability review

Navigation is the first usability pass because every later authoring tool
depends on it. The editor now supports right-drag orbit with pitch, responsive
distance-relative wheel zoom, accelerated keyboard panning with Shift, existing
Q/E keyboard orbit, and middle-drag view-relative panning. Camera gestures are
ignored while the pointer is over the interface.

Framing controls are also available from the toolbar: `F` frames the selected
entity and `Home` frames the complete authored level bounds. Entity hover uses a
distinct yellow outline, while the primary selection remains blue. Validation
issues that reference an entity can be clicked to select and frame that entity.
Ctrl-click toggles additional entities with green secondary outlines. Rotation
and deletion operate on the full selection as one composite history entry.

The next usability improvements, in priority order, are:

1. **Batch transforms:** multi-entity drag already preserves relative offsets,
   and the entity-array tool now creates repeated X/Z layouts as one composite
   command. A future numeric mixed-value Inspector can add explicit shared-pivot
   transforms when its UI semantics are designed.
2. **Transform feedback:** active drags show X/Z deltas and snap state; Esc
   restores the pre-drag state without creating history.
3. **Discoverability:** the compact controls reference and explicit tool buttons
   are implemented; add platform-appropriate tooltips when the temporary IMGUI
   adapter is replaced.
4. **Preferences:** camera, orthographic projection, snap, and grid settings are
   retained locally without putting user preferences in portable level JSON.
5. **Larger scenes:** palette search, a searchable entity hierarchy, portable
   named groups, lock/hide/isolate, category/group selection filters, and bulk
   selection are implemented. Isolation and filter state stay local to the
   editor.

These should remain presentation and tooling changes unless they mutate authored
level data. Camera state, hover state, open panels, and local preferences do not
belong in `LevelDocument` or command history.

### 1. Physical occlusion and concealment policy

- Do not add invisible cover grants for walls or props. Physical cover is derived
  from stance-adjusted target regions and the current projected collision geometry.
- Retain legacy cover-volume data only for loading existing fixtures; do not
  create it for newly placed entities or expand its editor workflow.
- Introduce explicitly named concealment fields only when smoke, darkness,
  foliage, or sensor interference enters the playable slice. These fields reduce
  perception confidence but do not block projectile collision.
- Treat any future AI position hints as search candidates that must still pass
  authoritative geometry and visibility queries.

### 2. Interaction-point editing

- Add reversible add, update, and remove commands keyed by entity and point IDs.
- Reuse sub-element selection for point handles.
- Author the typed interaction kind, local position, and radius.
- Extend validation only when the supported interaction-kind catalog is defined;
  do not introduce free-form behavior payloads.

### 3. Destructible defaults

- Add an entity command for enabled state, initial state, and integrity.
- Gate the inspector by the archetype capability profile.
- Keep gameplay-time damage isolated to the play-preview snapshot.

## Guardrails for every editor increment

Each increment is ready to merge when it:

1. Mutates authored state only through a reversible command.
2. Preserves undo, redo, saved-cursor dirty tracking, and redo invalidation.
3. Reports stable affected IDs and updates projection without rebuilding the
   world unless the change is genuinely structural.
4. Adds command/history tests plus a tool or projection integration test.
5. Round-trips through portable JSON and includes a migration when the schema
   changes.
6. Produces actionable authoring and publish validation without blocking
   temporary, valid-in-progress editing states unnecessarily.
7. Leaves play preview isolated from the authoring document.
8. Works through the shared runtime path on both WebGL and Windows.

## Known limits, not foundation blockers

- The IMGUI adapter is intentionally temporary. Feature logic must remain
  outside it so a later UI replacement is an adapter change.
- Current manipulation is position-and-yaw only. Pitch, roll, and scale should
  be introduced only with typed transform semantics and corresponding
  validation.
- The selection model supports multiple targets, but the current selection tool
  operates on one entity. Batch transforms should therefore be a separate
  transaction-based slice rather than an incidental change to metadata editing.
- Runtime navigation baking and arbitrary asset import remain outside the
  domain-specific editor's current scope.

After metadata authoring, duplicate/copy-paste and multi-entity transforms are
implemented through composite command history. Validation navigation, palette
search/category filters, the entity hierarchy, local editor preferences, and
transform feedback are also complete.
Encounter scripting should wait until its portable runtime contracts are known.

Portable heightfield terrain, patch-command history, chunked projection,
per-surface appearance, on-demand playability diagnostics, the transient slope
heatmap, surface decals, ambient-VFX placements, and spatial audio zones are
implemented. Decals, ambient VFX, audio zones, and practical lights now share a
queued map-placement workflow rather than being created indirectly at the camera
focus. Portable entity transforms now retain X/Y/Z rotation, and the first
physics-assisted **Drop & Settle** supports single props and multi-selection
pile settling with authored colliders or a visible-bounds fallback. The next
increment is destructible-cover pile testing and richer viewport transform tools.
Encounter authoring remains deferred until combat structure is stable.

The terrain appearance review's regional-material slice is complete. Surface
themes remain explicit whole-surface edits, while portable per-cell material
samples use the shared stroke/footprint lifecycle, undo as one patch, and rebuild
only affected visual chunks without invalidating collision or navigation.
