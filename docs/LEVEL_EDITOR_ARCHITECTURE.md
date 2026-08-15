# Level Editor Architecture

The editor is a runtime product surface, not a Unity-scene authoring shortcut.
Windows and WebGL use the same document, history, tools, projection, validation,
and persistence coordination paths.

## Ownership and data flow

`LevelDocument` is the only authored source of truth. The live document is
private to `LevelSession`; all consumers receive detached snapshots or entity
copies. This prevents tools from bypassing history, dirty tracking, validation,
and change notification.

The normal mutation flow is:

1. An active `ILevelEditorTool` interprets an input frame.
2. The tool submits one reversible `ILevelEditCommand` or a composite
   transaction to `LevelEditorWorkspace`.
3. The workspace updates history and registered validation rules, then publishes
   a `LevelSessionChangedEventArgs` containing the affected stable entity IDs.
4. `LevelWorldProjector` updates only those entity views. Environment commands
   refresh the shared `GameplayEnvironmentLighting` projection from the same
   document snapshot used by Test Play. Replacement documents
   and commands marked for full projection use the staging loader instead.
5. The controller caches one detached `LevelEditorViewState` per workspace
   revision. All IMGUI panels render that same snapshot instead of cloning the
   document independently.
6. `LevelEditorPresentationState` coordinates workspace navigation, create mode,
   and exclusive Inspector focus across world selections and scenario actors.

Play preview always uses a deep document snapshot. Runtime state must never be
copied back into the authoring workspace.

## Separation of concerns

| Area | Responsibility |
| --- | --- |
| Domain | Portable data, validation contracts, and default validation rules |
| Application | Workspace, private document session, history, commands, migrations, and selection model |
| Tooling | Tool lifecycle and input-to-command behavior |
| Core presentation services | Input capture, camera control, snapping, and scene queries |
| Runtime projection | Validated construction and incremental entity-view updates |
| Persistence coordinator | Drafts, import/export, platform transfer, and publish validation |
| Presentation state | Create/Outline/Scenario navigation and contextual Inspector focus |
| UI action contract | Typed boundary for user intents, persistence, and history operations |
| UI shell and panels | IMGUI rendering, transient text fields, and local disclosure state |
| Scenario authoring coordinator | Scenario invariants, transactions, and actor/link use cases |
| Environment authoring coordinator | Numeric parsing, lighting invariants, and undoable atmosphere/practical-light use cases |
| Layout coordinator | Bounds validation, local grid/view settings, and transaction-based entity arrays |
| Organization model | Group visibility, locks, isolation, and transient category/group selection policy |
| Organization coordinator | Group lifecycle, bulk assignment, filtering, and selection use cases |
| Controller | Composition, Unity lifecycle, preview boundary, and cross-service routing |

Domain and Application assemblies have no Unity references. Unity asset paths,
prefabs, raycasts, cameras, input devices, `PlayerPrefs`, browser JavaScript,
and GUI calls remain in Presentation.

The GUI must not query `LevelEditorWorkspace` or persistence services directly.
It receives an immutable view state and submits intent through
`ILevelEditorGuiActions`. GUI dimensions live in `LevelEditorGuiMetrics`, skin-
dependent styles in `LevelEditorGuiStyles`, and semantic UI/world colors in
`LevelEditorTheme`.

## Adding a tool

1. Implement `ILevelEditorTool` in `Presentation/LevelEditing/Tools`.
2. Give it a stable, unique tool ID.
3. Accept dependencies only through `LevelEditorToolContext` or a deliberately
   added narrow service.
4. Read authored state through `LevelEditorWorkspace` snapshots.
5. Express every authored mutation as a reversible command. Use
   `ExecuteTransaction` when a gesture changes multiple entities.
6. Keep temporary visual feedback in projected views; submit the final command
   at the gesture boundary.
7. Register the tool in the composition root. Do not add a tool-type branch to
   the controller's update loop.
8. Add command/history tests and at least one tool or projection integration
   test for the new behavior.

Tools should not read `Keyboard.current`, call `Physics.Raycast`, write files,
or construct prefabs directly. Those operations belong to the centralized
input, query, persistence, and projection services.

## History and transactions

History uses a cursor rather than independent undo/redo stacks. The saved cursor
is tracked explicitly, so undoing back to the saved state clears dirty status.
Editing from an undone state invalidates an unreachable savepoint.

`CompositeLevelEditCommand` applies multiple commands atomically and rolls back
already-applied children when a later child fails. One composite occupies one
history entry. A command must accurately report affected entity IDs and whether
it requires full projection.

## Validation and schema evolution

Validation is a registered `ILevelValidationRule` pipeline. Rules receive an
explicit Authoring, Publish, or Runtime profile and report stable issue codes.
Feature-specific validation belongs with a feature rule instead of another
branch in the editor host.

Portable schema changes must include an `ILevelDocumentMigration`. The migration
chain advances one declared source version at a time and rejects missing,
cyclic, malformed, or future-version paths. Do not silently reinterpret old
JSON or add an untyped property bag to avoid a schema change.

## Archetypes

The ScriptableObject catalog remains the Unity adapter for stable archetype IDs.
Each definition exposes four distinct concerns:

- presentation: prefab and local selection bounds;
- placement: position and angle increments;
- capabilities: placement surface, cover, and destructibility flags; and
- gameplay defaults: portable data placed into a new entity.

New palette filters and tools should depend on capabilities or placement rules,
not on prefab names or third-party asset paths.

## Environment and lighting

Schema 6 stores atmosphere, fog, the directional key, fixture presentation, and
practical spotlights in `LevelDocument.environment`. `SetLevelEnvironmentCommand`
is the single reversible boundary for these settings. Both the editor and
gameplay call `GameplayEnvironmentLighting` with this portable data, so Level
Preview, Test Play, exported JSON, and committed play cannot select different
lighting values.

`LevelLightingCatalog` is deliberately limited to Unity prefab references for
ambient effects. Those references remain Presentation-owned until ambient VFX
placements become portable authored data; the catalog no longer duplicates
atmosphere or practical-light values.

## Entity organization

Schema 7 stores named entity groups and each entity's optional stable group ID.
Group names, lock state, and hidden state are authored data changed only through
`ILevelOrganizationEditCommand` implementations. Group deletion is a composite
transaction that first ungroups every member, so undo restores both the group
and its membership atomically.

`LevelEditorOrganizationModel` is the presentation-owned selection policy. It
combines portable lock/hidden state with transient isolation and category/group
filters, applies authoring visibility to projected entity views, and prevents
scene picking or hierarchy focus from bypassing that policy. Isolation and
filters deliberately stay out of `LevelDocument`: they describe the current
author's view, not the playable or portable level. Preview and Test Play rebuild
from the document snapshot without authoring visibility applied, so hidden
groups are never mistaken for disabled gameplay content.

## Scaling rules

- Prefer a new tool or service over another mode flag in the controller.
- Keep document selection and Inspector focus coordinated; do not store a
  second selected object privately inside a panel.
- Render every panel from the same revision snapshot.
- Prefer typed portable data plus a migration over stringly typed metadata.
- Prefer stable IDs over object references across commands and persistence.
- Prefer a composite command over several independently undoable fragments of
  one user gesture.
- Prefer incremental projection for valid entity-local changes and atomic full
  replacement for imports or structural document changes.
- Keep browser storage as recoverable draft state; exported JSON remains the
  portable, version-controlled artifact.
