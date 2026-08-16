# Character editor

The character editor is a standalone runtime authoring surface for reusable
visual character identities. Open it from **Character Editor** on the main menu.
It deliberately does not edit combat statistics, inventory, or equipped items;
those remain owned by gameplay actor templates and scenario content.

## Workflow

1. Choose an existing published character or press **New**.
2. Set the display name and choose a body.
3. Cycle the visual slots on the right, or use **Randomize** for a complete
   deterministic recipe assembled from compatible options.
4. Drag the preview with either mouse button to orbit, use the mouse wheel to
   zoom, and press **F** to reset and frame the character. **View > Auto Rotate**
   remains available for checking clipping and silhouettes.
5. Use **Save Draft** for local work and **Export** for a portable JSON document.
6. Place reviewed exports in
   `Assets/GritGud/Content/Resources/Characters/Published` so they become
   reusable published characters on the next asset refresh.
7. In the level editor, select a scenario actor and choose the published
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
- Imports and exports use schema-versioned JSON with a size limit.
- Published documents are discovered from the Resources folder and rejected for
  duplicate IDs, unavailable bodies/accessories, wrong slots, or incompatible
  body-specific parts.
- Level schema 12 adds an optional character reference to each scenario actor.
  Empty references preserve all existing level and template visuals.
- Authoring validation warns about missing published characters; publish
  validation treats them as errors.

The document intentionally leaves room for later stats and equipment sections
without coupling those systems to the appearance projector.

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
compatible randomization, preview orbit/zoom/framing, undo/redo, local drafts,
cloud persistence, JSON interchange, published-character loading, validation,
and dirty-change confirmation. The next UX investments should be:

1. **Searchable visual option browsing.** Replace previous/clear/next rows with
   thumbnail grids, slot-level search, and clear empty/locked/incompatible
   states. This is the largest remaining usability gap with 87 accessories.
2. **Browser-native import.** The current path-based JSON import works in the
   desktop player and editor, but WebGL should use a file-picker/upload bridge
   equivalent to the existing browser-aware export path.
3. **Responsive panels.** The fixed side widths are appropriate on desktop but
   should collapse into drawers or tabs at narrow browser widths.
4. **Comparison and inspection tools.** Before/after comparison, hide-all by
   slot, lighting/background presets, and head/torso/full-body framing would
   make clipping and silhouette review faster.
5. **Library management.** Published entries need search, duplicate-as-new,
   explicit delete/archive behavior for drafts, and clearer separation between
   local, cloud, and shipped characters.
6. **Accessibility and discoverability.** Add scalable text/UI density,
   keyboard traversal, screen-reader-compatible labels when the runtime UI
   stack supports them, and remappable shortcuts.
