import { createHash } from "node:crypto";
import { pathToFileURL } from "node:url";

const MAX_SLUG_LENGTH = 48;
const HASH_LENGTH = 12;

export const createReadableSlug = (ref) => {
  if (typeof ref !== "string" || ref.trim().length === 0) {
    throw new TypeError("A non-empty preview ref is required.");
  }

  const slug = ref
    .trim()
    .normalize("NFKD")
    .replace(/[\u0300-\u036f]/g, "")
    .toLowerCase()
    .replace(/[^a-z0-9._-]+/g, "-")
    .replace(/^-+|-+$/g, "")
    .slice(0, MAX_SLUG_LENGTH)
    .replace(/-+$/g, "");

  return slug.length === 0 || /^\.+$/.test(slug)
    ? "preview"
    : slug;
};

export const createPreviewId = (ref) => {
  const slug = createReadableSlug(ref);
  const hash = createHash("sha256")
    .update(ref, "utf8")
    .digest("hex")
    .slice(0, HASH_LENGTH);
  return `${slug}-${hash}`;
};

const isMainModule = process.argv[1]
  && import.meta.url === pathToFileURL(process.argv[1]).href;

if (isMainModule) {
  const args = process.argv.slice(2);
  const legacy = args[0] === "--legacy";
  const ref = legacy ? args[1] : args[0];
  try {
    console.log(legacy ? createReadableSlug(ref) : createPreviewId(ref));
  } catch (error) {
    console.error(error.message);
    process.exitCode = 1;
  }
}
