# Character editor

The character editor is a standalone runtime authoring surface for reusable
character identities. Open it from **Character Editor** on the main menu. A
character owns a recognizable baseline build and starting loadout alongside its
appearance. It is a pre-level authoring tool, not a progression screen: the game
has no XP, advancement points, or player-side rating spending. Wounds, consumed
quantities, and current equipment state remain separate runtime state.

## Workflow

1. Choose an existing published character or press **New**.
2. Set the display name and choose a body under **Appearance**.
3. Cycle the visual slots on the right, or use **Randomize** for a complete
   deterministic recipe assembled from compatible options.
4. Drag the preview with either mouse button to orbit, use the mouse wheel to
   zoom, and press **F** to reset and frame the character. **View > Auto Rotate**
   remains available for checking clipping and silhouettes.
5. Author the four core attributes, starting skills, and talents under
   **Attributes** and **Skills**.
6. Add canonical equipment references, quantities, hotbar slots, and the
   initially equipped item under **Loadout**.
7. Resolve every issue under **Review**, then use **Save Draft** for local work
   or **Export** for a portable JSON document.
8. Place reviewed exports in
   `Assets/GritGud/Content/Resources/Characters/Published` so they become
   reusable published characters on the next asset refresh.
9. In the level editor, select a scenario actor and choose the published
   character under **Character Appearance**. Choosing **Template Default** keeps
   the actor template's original visual.

Character IDs are stable references. Renaming a character is safe; changing its
`characterId` after levels reference it is not.

## Appearance contract

The portable character document stores a body ID and at most one accessory per
slot. The current slots are armor, hair, facial hair, face accessory, headwear,
neck, back, waist, and patch. The presentation catalog maps those IDs to either:

- an embedded skinned renderer already bound to the shared humanoid skeleton;
- or an accessory prefab attached to an explicit humanoid head, neck, chest,
  hips, or shoulder socket.

Headwear can hide hair without deleting the saved hair choice. Generated
accessory colliders are disabled, and the actor cel-shading pass is reapplied
after projection so cosmetic meshes follow the same runtime material treatment.

The catalog is generated from the installed POLYGON Battle Royale assets with
**Grit Gud > Content > Rebuild Character Appearance Catalog**. The checked-in
catalog currently exposes 15 bodies and 87 accessories. Rebuilding fails fast
when a required preview prefab, renderer name, or accessory prefab is missing.

The editable Synty character prefabs, meshes, materials, and attachment sources
remain in `best-coder-open-now-near-me/private-assets` under the Grit Gud
overlay. The public repository contains only the project-owned catalog,
portable character documents, and authoring/runtime code. Catalog GUIDs must
resolve against the private revision pinned by `.github/private-assets-ref`;
the presentation test suite verifies every body, embedded armor renderer, and
accessory prefab after the overlay is installed.

## Persistence and validation

- Drafts use local `PlayerPrefs` storage and are intended for work in progress.
- Imports and exports use schema-versioned JSON with a size limit. Desktop and
  the Unity Editor read the configured import path; WebGL opens a browser-native
  file picker. Both paths feed the same validation and replacement flow.
- Schema 2 adds the baseline build and starting loadout. Appearance-only schema
  1 documents migrate to a balanced default build and empty loadout on import.
- Published documents are discovered from the Resources folder and rejected for
  duplicate IDs, unavailable bodies/accessories, wrong slots, or incompatible
  body-specific parts.
- Level schema 12 adds an optional character reference to each scenario actor.
  Empty references preserve all existing level and template visuals.
- Authoring validation warns about missing published characters; publish
  validation treats them as errors.

When a level actor references a published character, gameplay overlays the
character's display name, profile, and selected starting loadout onto the actor
template. Equipment entries remain references to canonical template definitions,
so portable character documents do not duplicate combat or consumable rules.

## Editor controls and organization

The compact top bar follows the same document-first organization as the level
editor. **File** owns new/load/save, cloud, import, and export actions; **Edit**
owns undo, redo, and randomize; and **View** owns preview-camera actions. The
current document name and saved state stay centered while the most common
actions, **Randomize** and **Save**, remain one click away.

| Input | Action |
| --- | --- |
| Left- or right-mouse drag over the preview | Orbit around the character |
| Mouse wheel over the preview | Zoom in or out |
| `F` | Reset and frame the character |
| `Ctrl/Cmd+S` | Save the local draft |
| `Ctrl/Cmd+Z` / `Ctrl/Cmd+Y` | Undo / redo |
| `Esc` | Close an open toolbar menu |

Camera input is limited to the center preview viewport, so scrolling an option
panel or interacting with toolbar menus cannot accidentally move the camera.
Manual orbit also stops auto rotation rather than making the two controls fight.

## Feature review and remaining gaps

The editor now covers identity selection, body and accessory authoring,
compatible randomization, preview orbit/zoom/framing, baseline attributes,
derived-stat previews, starting skills and talents, starting equipment/hotbar
authoring, a publish-readiness review, undo/redo, local drafts, cloud persistence,
JSON interchange, published-character loading, validation, gameplay overlay, and
dirty-change confirmation. The next UX investments should be:

1. **Searchable visual option browsing.** Replace previous/clear/next rows with
   thumbnail grids, slot-level search, and clear empty/locked/incompatible
   states. This is the largest remaining usability gap with 87 accessories.
2. **Responsive panels.** The fixed side widths are appropriate on desktop but
   should collapse into drawers or tabs at narrow browser widths.
3. **Catalog depth and rule metadata.** Move the initial skill, talent, and item
   choices into authored catalogs with descriptions, prerequisites, conflicts,
   and archetype packages for pre-level character authoring.
4. **Comparison and inspection tools.** Before/after comparison, hide-all by
   slot, lighting/background presets, and head/torso/full-body framing would
   make clipping and silhouette review faster.
5. **Library management.** Published entries need search, duplicate-as-new,
   explicit delete/archive behavior for drafts, and clearer separation between
   local, cloud, and shipped characters.
6. **Accessibility and discoverability.** Add scalable text/UI density,
   keyboard traversal, screen-reader-compatible labels when the runtime UI
   stack supports them, and remappable shortcuts.
