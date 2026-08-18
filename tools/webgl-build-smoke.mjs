import { readdir, readFile, stat } from "node:fs/promises";
import { resolve, relative, sep } from "node:path";
import { pathToFileURL } from "node:url";

const REQUIRED_ARTIFACTS = [
  ["Unity loader", /\.loader\.js$/],
  ["game data", /\.data(?:\.br|\.gz)?$/],
  ["JavaScript framework", /\.framework\.js(?:\.br|\.gz)?$/],
  ["WebAssembly player", /\.wasm(?:\.br|\.gz)?$/],
];

async function listFiles(root, directory = root) {
  const files = [];
  for (const entry of await readdir(directory, { withFileTypes: true })) {
    const path = resolve(directory, entry.name);
    if (entry.isDirectory()) {
      files.push(...await listFiles(root, path));
    } else if (entry.isFile()) {
      files.push(relative(root, path).split(sep).join("/"));
    }
  }
  return files;
}

function localDocumentReferences(html) {
  const references = [];
  const attribute = /\b(?:src|href)\s*=\s*["']([^"']+)["']/gi;
  for (const match of html.matchAll(attribute)) {
    const value = match[1].trim();
    if (!value
      || value.startsWith("data:")
      || value.startsWith("http://")
      || value.startsWith("https://")
      || value.startsWith("//")
      || value.startsWith("#")) {
      continue;
    }
    references.push(decodeURIComponent(value.split(/[?#]/, 1)[0]));
  }
  return references;
}

export async function validateWebGlBuild(buildDirectory) {
  const root = resolve(buildDirectory);
  const rootInfo = await stat(root).catch(() => null);
  if (!rootInfo?.isDirectory()) {
    throw new Error(`WebGL build directory does not exist: ${root}`);
  }

  const files = await listFiles(root);
  const fileSet = new Set(files);
  if (!fileSet.has("index.html")) {
    throw new Error("WebGL build is missing index.html.");
  }

  const html = await readFile(resolve(root, "index.html"), "utf8");
  if (!/id=["']unity-canvas["']/.test(html)) {
    throw new Error("WebGL index does not contain the Unity canvas.");
  }
  if (!/createUnityInstance\s*\(/.test(html)) {
    throw new Error("WebGL index does not start the Unity loader.");
  }

  for (const [label, pattern] of REQUIRED_ARTIFACTS) {
    const matches = files.filter((file) => pattern.test(file));
    if (matches.length !== 1) {
      throw new Error(
        `WebGL build requires exactly one ${label} artifact; found ${matches.length}.`,
      );
    }
    const info = await stat(resolve(root, matches[0]));
    if (info.size === 0) {
      throw new Error(`WebGL ${label} artifact is empty: ${matches[0]}`);
    }
  }

  for (const reference of localDocumentReferences(html)) {
    const target = resolve(root, reference);
    const targetRelative = relative(root, target);
    if (targetRelative.startsWith(`..${sep}`) || targetRelative === "..") {
      throw new Error(`WebGL index escapes the build root: ${reference}`);
    }
    if (!fileSet.has(targetRelative.split(sep).join("/"))) {
      throw new Error(`WebGL index references a missing file: ${reference}`);
    }
  }

  return {
    fileCount: files.length,
    root,
  };
}

if (import.meta.url === pathToFileURL(process.argv[1]).href) {
  const buildDirectory = process.argv[2] ?? "Builds/Web";
  try {
    const result = await validateWebGlBuild(buildDirectory);
    console.log(
      `Validated WebGL browser artifact with ${result.fileCount} files at ${result.root}.`,
    );
  } catch (error) {
    console.error(error instanceof Error ? error.message : error);
    process.exitCode = 1;
  }
}
