import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { createPreviewId, createReadableSlug } from "./preview-id.mjs";

const ref = "codex/Fix Rifle Movement";
const previewId = createPreviewId(ref);

assert.match(previewId, /^codex-fix-rifle-movement-[a-f0-9]{12}$/);
assert.equal(createPreviewId(ref), previewId);
assert.notEqual(createPreviewId(ref.toLowerCase()), previewId);
assert.notEqual(createPreviewId("feature/a b"), createPreviewId("feature/a-b"));
assert.equal(createReadableSlug("..."), "preview");
assert.equal(createReadableSlug("  Feature/Depot  "), "feature-depot");
assert.ok(createPreviewId("x".repeat(300)).length <= 61);
assert.throws(() => createPreviewId("  "), /non-empty preview ref/);

const workflow = readFileSync(
  new URL("../.github/workflows/web-preview.yml", import.meta.url),
  "utf8",
);
assert.match(
  workflow,
  /group: branch-preview-\$\{\{ needs\.identity\.outputs\.preview_id \}\}/,
);
assert.ok(
  workflow.match(/PREVIEW_ID: \$\{\{ needs\.identity\.outputs\.preview_id \}\}/g)
    ?.length >= 3,
);
assert.doesNotMatch(workflow, /PREVIEW_SLUG|steps\.preview\.outputs\.slug/);

console.log("Preview ID contract tests passed.");
