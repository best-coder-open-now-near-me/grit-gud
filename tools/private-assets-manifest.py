#!/usr/bin/env python3
"""Validate the exact private-assets overlay before and after installation."""

from __future__ import annotations

import argparse
import filecmp
import json
import os
from dataclasses import dataclass
from pathlib import Path, PurePosixPath
import re
import subprocess
import sys
from typing import Iterable


OBJECT_ID = re.compile(r"^[0-9a-f]{40}$")


class ManifestError(RuntimeError):
    """Raised when the licensed overlay does not match its public contract."""


@dataclass(frozen=True)
class SourceContract:
    repository: str
    revision: str
    sentinel_path: PurePosixPath
    sentinel_blob: str
    asset_tree_path: PurePosixPath
    asset_tree: str


@dataclass(frozen=True)
class EntryContract:
    destination: PurePosixPath
    kind: str
    git_object: str

    @property
    def relative_path(self) -> PurePosixPath:
        return self.destination.relative_to("Assets")


@dataclass(frozen=True)
class PrivateAssetManifest:
    source: SourceContract
    entries: tuple[EntryContract, ...]


@dataclass(frozen=True)
class GitTreeEntry:
    mode: str
    kind: str
    object_id: str
    path: PurePosixPath


@dataclass(frozen=True)
class VerifiedSource:
    root: Path
    files: tuple[PurePosixPath, ...]
    directories: tuple[PurePosixPath, ...]


def _require_keys(value: object, expected: set[str], context: str) -> dict:
    if not isinstance(value, dict):
        raise ManifestError(f"{context} must be an object.")
    actual = set(value)
    if actual != expected:
        raise ManifestError(
            f"{context} keys differ: expected {sorted(expected)}, "
            f"found {sorted(actual)}."
        )
    return value


def _require_text(value: object, context: str) -> str:
    if not isinstance(value, str) or not value.strip():
        raise ManifestError(f"{context} must be non-empty text.")
    return value


def _require_object_id(value: object, context: str) -> str:
    text = _require_text(value, context)
    if not OBJECT_ID.fullmatch(text):
        raise ManifestError(f"{context} must be a lowercase SHA-1 object ID.")
    return text


def _require_relative_path(value: object, context: str) -> PurePosixPath:
    text = _require_text(value, context)
    path = PurePosixPath(text)
    if path.is_absolute() or ".." in path.parts or "." in path.parts:
        raise ManifestError(f"{context} must be a normalized relative path.")
    if path.as_posix() != text:
        raise ManifestError(f"{context} must use normalized POSIX separators.")
    return path


def load_manifest(path: Path) -> PrivateAssetManifest:
    try:
        document = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        raise ManifestError(f"Could not read private asset manifest: {error}")

    root = _require_keys(
        document,
        {"schemaVersion", "source", "entries"},
        "Manifest",
    )
    if root["schemaVersion"] != 1:
        raise ManifestError("Unsupported private asset manifest schema.")

    source_value = _require_keys(
        root["source"],
        {"repository", "revision", "sentinel", "assetTree"},
        "Manifest source",
    )
    sentinel = _require_keys(
        source_value["sentinel"],
        {"path", "gitBlob"},
        "Manifest sentinel",
    )
    asset_tree = _require_keys(
        source_value["assetTree"],
        {"path", "gitTree"},
        "Manifest asset tree",
    )
    source = SourceContract(
        repository=_require_text(
            source_value["repository"], "Source repository"
        ),
        revision=_require_object_id(
            source_value["revision"], "Source revision"
        ),
        sentinel_path=_require_relative_path(
            sentinel["path"], "Sentinel path"
        ),
        sentinel_blob=_require_object_id(
            sentinel["gitBlob"], "Sentinel blob"
        ),
        asset_tree_path=_require_relative_path(
            asset_tree["path"], "Asset tree path"
        ),
        asset_tree=_require_object_id(
            asset_tree["gitTree"], "Asset tree object"
        ),
    )
    if source.asset_tree_path != PurePosixPath("grit-gud/Assets"):
        raise ManifestError("The asset source must be grit-gud/Assets.")
    if source.sentinel_path.parent != source.asset_tree_path.parent:
        raise ManifestError("The sentinel must be beside the asset tree.")

    entries_value = root["entries"]
    if not isinstance(entries_value, list) or not entries_value:
        raise ManifestError("Manifest entries must be a non-empty array.")
    entries: list[EntryContract] = []
    for index, value in enumerate(entries_value):
        entry = _require_keys(
            value,
            {"destination", "kind", "gitObject"},
            f"Manifest entry {index}",
        )
        destination = _require_relative_path(
            entry["destination"], f"Manifest entry {index} destination"
        )
        if destination.parent != PurePosixPath("Assets"):
            raise ManifestError(
                "Every permitted destination must be directly beneath Assets."
            )
        kind = _require_text(entry["kind"], f"Manifest entry {index} kind")
        if kind not in {"file", "directory"}:
            raise ManifestError(
                f"Manifest entry {index} has unsupported kind '{kind}'."
            )
        entries.append(
            EntryContract(
                destination=destination,
                kind=kind,
                git_object=_require_object_id(
                    entry["gitObject"],
                    f"Manifest entry {index} object",
                ),
            )
        )
    destinations = [entry.destination.as_posix() for entry in entries]
    if destinations != sorted(destinations):
        raise ManifestError("Manifest entries must be sorted by destination.")
    if len(destinations) != len(set(destinations)):
        raise ManifestError("Manifest destinations must be unique.")
    return PrivateAssetManifest(source=source, entries=tuple(entries))


def _run_git(checkout: Path, *arguments: str) -> bytes:
    result = subprocess.run(
        ["git", "-C", str(checkout), *arguments],
        check=False,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
    )
    if result.returncode != 0:
        detail = result.stderr.decode("utf-8", errors="replace").strip()
        raise ManifestError(
            f"Git inspection failed ({' '.join(arguments)}): {detail}"
        )
    return result.stdout


def _read_tree(
    checkout: Path,
    revision: str,
    path: PurePosixPath,
    *,
    recursive: bool = False,
    include_trees: bool = False,
) -> tuple[GitTreeEntry, ...]:
    arguments = ["ls-tree", "-z"]
    if recursive:
        arguments.append("-r")
    if include_trees:
        arguments.append("-t")
    arguments.extend([revision, "--", path.as_posix()])
    output = _run_git(checkout, *arguments)
    entries: list[GitTreeEntry] = []
    for raw_entry in output.split(b"\0"):
        if not raw_entry:
            continue
        header, raw_path = raw_entry.split(b"\t", 1)
        mode, kind, object_id = header.decode("ascii").split(" ")
        entries.append(
            GitTreeEntry(
                mode=mode,
                kind=kind,
                object_id=object_id,
                path=PurePosixPath(raw_path.decode("utf-8")),
            )
        )
    return tuple(entries)


def _read_tree_contents(
    checkout: Path,
    revision: str,
    path: PurePosixPath,
    *,
    recursive: bool = False,
    include_trees: bool = False,
) -> tuple[GitTreeEntry, ...]:
    arguments = ["ls-tree", "-z"]
    if recursive:
        arguments.append("-r")
    if include_trees:
        arguments.append("-t")
    arguments.append(f"{revision}:{path.as_posix()}")
    output = _run_git(checkout, *arguments)
    entries: list[GitTreeEntry] = []
    for raw_entry in output.split(b"\0"):
        if not raw_entry:
            continue
        header, raw_path = raw_entry.split(b"\t", 1)
        mode, kind, object_id = header.decode("ascii").split(" ")
        entries.append(
            GitTreeEntry(
                mode=mode,
                kind=kind,
                object_id=object_id,
                path=PurePosixPath(raw_path.decode("utf-8")),
            )
        )
    return tuple(entries)


def _require_single_tree_entry(
    checkout: Path,
    revision: str,
    path: PurePosixPath,
    expected_mode: str,
    expected_kind: str,
    expected_object: str,
) -> None:
    entries = _read_tree(checkout, revision, path)
    if len(entries) != 1:
        raise ManifestError(f"Expected exactly one tracked entry at {path}.")
    entry = entries[0]
    expected = (expected_mode, expected_kind, expected_object, path)
    actual = (entry.mode, entry.kind, entry.object_id, entry.path)
    if actual != expected:
        raise ManifestError(
            f"Tracked entry {path} differs from the public manifest."
        )


def _filesystem_entries(root: Path) -> tuple[set[PurePosixPath], set[PurePosixPath]]:
    files: set[PurePosixPath] = set()
    directories: set[PurePosixPath] = set()
    for current, directory_names, file_names in os.walk(root):
        current_path = Path(current)
        for name in directory_names:
            path = current_path / name
            relative = PurePosixPath(path.relative_to(root).as_posix())
            if path.is_symlink():
                raise ManifestError(
                    f"Private asset source contains symlink directory {relative}."
                )
            directories.add(relative)
        for name in file_names:
            path = current_path / name
            relative = PurePosixPath(path.relative_to(root).as_posix())
            if path.is_symlink():
                raise ManifestError(
                    f"Private asset source contains symlink file {relative}."
                )
            files.add(relative)
    return files, directories


def verify_source(
    manifest: PrivateAssetManifest,
    checkout: Path,
) -> VerifiedSource:
    checkout = checkout.resolve()
    if not (checkout / ".git").exists():
        raise ManifestError(f"Private asset checkout is not a Git repository: {checkout}")
    head = _run_git(checkout, "rev-parse", "HEAD^{commit}").decode().strip()
    if head != manifest.source.revision:
        raise ManifestError(
            f"Private asset revision differs: expected {manifest.source.revision}, "
            f"found {head}."
        )

    dirty = _run_git(
        checkout,
        "status",
        "--porcelain=v1",
        "-z",
        "--untracked-files=all",
        "--",
        manifest.source.asset_tree_path.parent.as_posix(),
    )
    if dirty:
        raise ManifestError("Private asset source has modified or untracked files.")

    _require_single_tree_entry(
        checkout,
        head,
        manifest.source.sentinel_path,
        "100644",
        "blob",
        manifest.source.sentinel_blob,
    )
    _require_single_tree_entry(
        checkout,
        head,
        manifest.source.asset_tree_path,
        "040000",
        "tree",
        manifest.source.asset_tree,
    )

    top_level = _read_tree_contents(
        checkout,
        head,
        manifest.source.asset_tree_path,
    )
    actual_top_level: dict[PurePosixPath, tuple[str, str]] = {}
    for entry in top_level:
        kind = "directory" if entry.kind == "tree" else "file"
        actual_top_level[entry.path] = (kind, entry.object_id)
    expected_top_level = {
        entry.relative_path: (entry.kind, entry.git_object)
        for entry in manifest.entries
    }
    if actual_top_level != expected_top_level:
        raise ManifestError(
            "Private asset top-level entries differ from the public manifest."
        )

    recursive_entries = _read_tree_contents(
        checkout,
        head,
        manifest.source.asset_tree_path,
        recursive=True,
        include_trees=True,
    )
    tracked_files: set[PurePosixPath] = set()
    tracked_directories: set[PurePosixPath] = set()
    for entry in recursive_entries:
        relative = entry.path
        if entry.kind == "tree" and entry.mode == "040000":
            tracked_directories.add(relative)
        elif entry.kind == "blob" and entry.mode in {"100644", "100755"}:
            tracked_files.add(relative)
        else:
            raise ManifestError(
                f"Unsupported private asset entry mode at {relative}: "
                f"{entry.mode} {entry.kind}."
            )

    source_root = checkout.joinpath(*manifest.source.asset_tree_path.parts)
    if not source_root.is_dir() or source_root.is_symlink():
        raise ManifestError("The private asset source tree is missing or unsafe.")
    actual_files, actual_directories = _filesystem_entries(source_root)
    if actual_files != tracked_files or actual_directories != tracked_directories:
        raise ManifestError(
            "Private asset worktree contains missing or unexpected filesystem entries."
        )
    return VerifiedSource(
        root=source_root,
        files=tuple(sorted(tracked_files, key=lambda value: value.as_posix())),
        directories=tuple(
            sorted(tracked_directories, key=lambda value: value.as_posix())
        ),
    )


def _workspace_path(workspace: Path, path: PurePosixPath) -> Path:
    return workspace.joinpath(*path.parts)


def verify_preinstall(
    manifest: PrivateAssetManifest,
    checkout: Path,
    workspace: Path,
) -> VerifiedSource:
    source = verify_source(manifest, checkout)
    workspace = workspace.resolve()
    shadows = [
        entry.destination.as_posix()
        for entry in manifest.entries
        if _workspace_path(workspace, entry.destination).exists()
        or _workspace_path(workspace, entry.destination).is_symlink()
    ]
    if shadows:
        raise ManifestError(
            "Private asset destinations are shadowed by existing workspace "
            f"paths: {', '.join(shadows)}"
        )
    return source


def _compare_files(source: Path, destination: Path, relative: PurePosixPath) -> None:
    if not destination.is_file() or destination.is_symlink():
        raise ManifestError(f"Installed private asset file is missing: Assets/{relative}")
    if not filecmp.cmp(source, destination, shallow=False):
        raise ManifestError(
            f"Installed private asset content differs: Assets/{relative}"
        )


def verify_installed(
    manifest: PrivateAssetManifest,
    checkout: Path,
    workspace: Path,
) -> VerifiedSource:
    source = verify_source(manifest, checkout)
    workspace = workspace.resolve()
    destination_root = workspace / "Assets"
    for relative in source.directories:
        destination = _workspace_path(destination_root, relative)
        if not destination.is_dir() or destination.is_symlink():
            raise ManifestError(
                f"Installed private asset directory is missing: Assets/{relative}"
            )
    for relative in source.files:
        _compare_files(
            _workspace_path(source.root, relative),
            _workspace_path(destination_root, relative),
            relative,
        )

    installed_files: set[PurePosixPath] = set()
    installed_directories: set[PurePosixPath] = set()
    for entry in manifest.entries:
        root = _workspace_path(workspace, entry.destination)
        if entry.kind == "file":
            if not root.is_file() or root.is_symlink():
                raise ManifestError(
                    f"Installed manifest destination is missing: {entry.destination}"
                )
            installed_files.add(entry.relative_path)
            continue
        if not root.is_dir() or root.is_symlink():
            raise ManifestError(
                f"Installed manifest destination is missing: {entry.destination}"
            )
        files, directories = _filesystem_entries(root)
        prefix = entry.relative_path
        installed_directories.add(prefix)
        installed_files.update(prefix / path for path in files)
        installed_directories.update(prefix / path for path in directories)
    if installed_files != set(source.files) or installed_directories != set(
        source.directories
    ):
        raise ManifestError(
            "Installed private asset destinations contain missing or extra entries."
        )
    return source


def _parse_arguments(arguments: Iterable[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "mode",
        choices=("source", "preinstall", "installed"),
        help="Validation phase to run.",
    )
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--checkout", type=Path, required=True)
    parser.add_argument("--workspace", type=Path, default=Path("."))
    return parser.parse_args(arguments)


def main(arguments: Iterable[str] | None = None) -> int:
    options = _parse_arguments(arguments if arguments is not None else sys.argv[1:])
    try:
        manifest = load_manifest(options.manifest)
        if options.mode == "source":
            source = verify_source(manifest, options.checkout)
        elif options.mode == "preinstall":
            source = verify_preinstall(
                manifest,
                options.checkout,
                options.workspace,
            )
        else:
            source = verify_installed(
                manifest,
                options.checkout,
                options.workspace,
            )
        print(
            f"Validated private asset manifest: {len(source.files)} files, "
            f"{len(source.directories)} directories."
        )
        return 0
    except ManifestError as error:
        print(f"Private asset manifest validation failed: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
