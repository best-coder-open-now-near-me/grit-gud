import { readdir, writeFile } from "node:fs/promises";
import { join, resolve } from "node:path";

const siteRoot = resolve(process.argv[2] ?? ".");
const previewRoot = join(siteRoot, "preview");
let entries = [];

try {
  entries = await readdir(previewRoot, { withFileTypes: true });
} catch (error) {
  if (error.code !== "ENOENT") {
    throw error;
  }
}

const previews = entries
  .filter((entry) => entry.isDirectory())
  .map((entry) => entry.name)
  .sort((left, right) => left.localeCompare(right));

const escapeHtml = (value) => value
  .replaceAll("&", "&amp;")
  .replaceAll("<", "&lt;")
  .replaceAll(">", "&gt;")
  .replaceAll('"', "&quot;")
  .replaceAll("'", "&#39;");

const links = previews.length > 0
  ? previews.map((name) => `      <li><a href="preview/${encodeURIComponent(name)}/">${escapeHtml(name)}</a></li>`).join("\n")
  : "      <li>No branch previews are currently published.</li>";

const html = `<!doctype html>
<html lang="en">
  <head>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <title>Grit Gud branch previews</title>
    <style>
      :root { color-scheme: dark; font-family: system-ui, sans-serif; background: #11181b; color: #eee9dc; }
      body { box-sizing: border-box; max-width: 54rem; margin: 0 auto; padding: 4rem 1.5rem; }
      h1 { margin-bottom: .5rem; font-size: clamp(2rem, 7vw, 4rem); }
      p { color: #aeb8b8; }
      ul { display: grid; gap: .75rem; padding: 0; list-style: none; }
      a { display: block; padding: 1rem 1.25rem; color: #fff; background: #243238; border-left: .25rem solid #b8611f; text-decoration: none; }
      a:hover, a:focus-visible { background: #b8611f; }
    </style>
  </head>
  <body>
    <h1>Grit Gud</h1>
    <p>Live WebGL branch previews</p>
    <ul>
${links}
    </ul>
  </body>
</html>
`;

await writeFile(join(siteRoot, "index.html"), html);
