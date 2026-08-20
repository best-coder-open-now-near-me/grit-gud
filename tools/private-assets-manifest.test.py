#!/usr/bin/env python3
"""Adversarial tests for the exact private-assets overlay contract."""

from __future__ import annotations

import importlib.util
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest


sys.dont_write_bytecode = True
MODULE_PATH = Path(__file__).with_name("private-assets-manifest.py")
SPEC = importlib.util.spec_from_file_location(
    "private_assets_manifest", MODULE_PATH
)
MANIFEST_MODULE = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
sys.modules[SPEC.name] = MANIFEST_MODULE
SPEC.loader.exec_module(MANIFEST_MODULE)


class PrivateAssetManifestTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)
        self.checkout = self.root / "private"
        self.workspace = self.root / "public"
        self.asset_root = self.checkout / "grit-gud" / "Assets"
        self.asset_root.mkdir(parents=True)
        self.workspace.mkdir()
        (self.checkout / "grit-gud" / ".grit-gud-private-assets").write_text(
            "private\n", encoding="utf-8"
        )
        (self.asset_root / "Allowed").mkdir()
        (self.asset_root / "Allowed" / "asset.txt").write_text(
            "licensed bytes\n", encoding="utf-8"
        )
        (self.asset_root / "Allowed.meta").write_text(
            "guid: fixture\n", encoding="utf-8"
        )
        self._git("init", "--quiet")
        self._git("config", "user.name", "Manifest Tests")
        self._git("config", "user.email", "manifest@example.invalid")
        self._git("add", "grit-gud")
        self._git("commit", "--quiet", "-m", "fixture")
        self.manifest_path = self.root / "manifest.json"
        self.document = self._create_manifest_document()
        self._write_manifest()

    def tearDown(self) -> None:
        self.temporary.cleanup()

    def _git(self, *arguments: str) -> str:
        result = subprocess.run(
            ["git", "-C", str(self.checkout), *arguments],
            check=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
        )
        return result.stdout.strip()

    def _object(self, specifier: str) -> str:
        return self._git("rev-parse", specifier)

    def _create_manifest_document(self) -> dict:
        return {
            "schemaVersion": 1,
            "source": {
                "repository": "example/private-assets",
                "revision": self._object("HEAD"),
                "sentinel": {
                    "path": "grit-gud/.grit-gud-private-assets",
                    "gitBlob": self._object(
                        "HEAD:grit-gud/.grit-gud-private-assets"
                    ),
                },
                "assetTree": {
                    "path": "grit-gud/Assets",
                    "gitTree": self._object("HEAD:grit-gud/Assets"),
                },
            },
            "entries": [
                {
                    "destination": "Assets/Allowed",
                    "kind": "directory",
                    "gitObject": self._object(
                        "HEAD:grit-gud/Assets/Allowed"
                    ),
                },
                {
                    "destination": "Assets/Allowed.meta",
                    "kind": "file",
                    "gitObject": self._object(
                        "HEAD:grit-gud/Assets/Allowed.meta"
                    ),
                },
            ],
        }

    def _write_manifest(self) -> None:
        self.manifest_path.write_text(
            json.dumps(self.document, indent=2) + "\n", encoding="utf-8"
        )

    def _load(self):
        return MANIFEST_MODULE.load_manifest(self.manifest_path)

    def _install_fixture(self) -> None:
        shutil.copytree(
            self.asset_root,
            self.workspace / "Assets",
            dirs_exist_ok=True,
        )

    def test_exact_source_and_installed_copy_are_accepted(self) -> None:
        manifest = self._load()
        source = MANIFEST_MODULE.verify_source(manifest, self.checkout)
        self.assertEqual(len(source.files), 2)
        self._install_fixture()
        MANIFEST_MODULE.verify_installed(
            manifest, self.checkout, self.workspace
        )

    def test_manifest_missing_source_entry_is_rejected(self) -> None:
        self.document["entries"] = self.document["entries"][:-1]
        self._write_manifest()
        with self.assertRaisesRegex(
            MANIFEST_MODULE.ManifestError, "top-level entries differ"
        ):
            MANIFEST_MODULE.verify_source(self._load(), self.checkout)

    def test_manifest_with_extra_entry_is_rejected(self) -> None:
        self.document["entries"].append(
            {
                "destination": "Assets/Z-Unexpected.meta",
                "kind": "file",
                "gitObject": "0" * 40,
            }
        )
        self._write_manifest()
        with self.assertRaisesRegex(
            MANIFEST_MODULE.ManifestError, "top-level entries differ"
        ):
            MANIFEST_MODULE.verify_source(self._load(), self.checkout)

    def test_missing_or_untracked_source_file_is_rejected(self) -> None:
        (self.asset_root / "Allowed" / "asset.txt").unlink()
        with self.assertRaisesRegex(
            MANIFEST_MODULE.ManifestError, "modified or untracked"
        ):
            MANIFEST_MODULE.verify_source(self._load(), self.checkout)
        self._git("restore", "grit-gud/Assets/Allowed/asset.txt")
        (self.asset_root / "Allowed" / "extra.txt").write_text(
            "extra\n", encoding="utf-8"
        )
        with self.assertRaisesRegex(
            MANIFEST_MODULE.ManifestError, "modified or untracked"
        ):
            MANIFEST_MODULE.verify_source(self._load(), self.checkout)

    def test_shadowed_destination_is_rejected_before_install(self) -> None:
        shadow = self.workspace / "Assets" / "Allowed"
        shadow.mkdir(parents=True)
        (shadow / "public.txt").write_text("public\n", encoding="utf-8")
        with self.assertRaisesRegex(
            MANIFEST_MODULE.ManifestError, "shadowed"
        ):
            MANIFEST_MODULE.verify_preinstall(
                self._load(), self.checkout, self.workspace
            )

    def test_altered_and_extra_installed_files_are_rejected(self) -> None:
        manifest = self._load()
        self._install_fixture()
        installed = self.workspace / "Assets" / "Allowed" / "asset.txt"
        installed.write_text("altered\n", encoding="utf-8")
        with self.assertRaisesRegex(
            MANIFEST_MODULE.ManifestError, "content differs"
        ):
            MANIFEST_MODULE.verify_installed(
                manifest, self.checkout, self.workspace
            )
        installed.write_text("licensed bytes\n", encoding="utf-8")
        (installed.parent / "extra.txt").write_text(
            "extra\n", encoding="utf-8"
        )
        with self.assertRaisesRegex(
            MANIFEST_MODULE.ManifestError, "missing or extra"
        ):
            MANIFEST_MODULE.verify_installed(
                manifest, self.checkout, self.workspace
            )


if __name__ == "__main__":
    unittest.main()
