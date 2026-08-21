import assert from "node:assert/strict";
import { mkdtemp, mkdir, rm, writeFile } from "node:fs/promises";
import { join } from "node:path";
import { tmpdir } from "node:os";
import { validateWebGlBuild } from "./webgl-build-smoke.mjs";

const root = await mkdtemp(join(tmpdir(), "grit-gud-webgl-smoke-"));

async function write(relativePath, contents = "artifact") {
  const path = join(root, relativePath);
  await mkdir(join(path, ".."), { recursive: true });
  await writeFile(path, contents);
}

try {
  await write(
    "index.html",
    `<!doctype html>
<canvas id="unity-canvas"></canvas>
<link rel="stylesheet" href="TemplateData/style.css">
<script src="Build/game.loader.js"></script>
<script>createUnityInstance(document.querySelector("#unity-canvas"), {});</script>`,
  );
  await write("TemplateData/style.css", "canvas { display: block; }");
  await write("Build/game.loader.js");
  await write("Build/game.data.unityweb");
  await write("Build/game.framework.js.unityweb");
  await write("Build/game.wasm.unityweb");

  const result = await validateWebGlBuild(root);
  assert.equal(result.fileCount, 6);

  await rm(join(root, "Build/game.wasm.unityweb"));
  await assert.rejects(
    validateWebGlBuild(root),
    /exactly one WebAssembly player artifact; found 0/,
  );

  await write("Build/game.wasm.unityweb");
  await write(
    "index.html",
    `<canvas id="unity-canvas"></canvas>
<script src="missing.loader.js"></script>
<script>createUnityInstance(document.querySelector("#unity-canvas"), {});</script>`,
  );
  await assert.rejects(
    validateWebGlBuild(root),
    /references a missing file: missing.loader.js/,
  );
} finally {
  await rm(root, { recursive: true, force: true });
}

console.log("WebGL browser artifact smoke tests passed.");
