import json
import re
import subprocess
import sys
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[1]
CONFLICT_MARKERS = ("<<<<<<< ", "=======", ">>>>>>> ")
TEXT_SUFFIXES = {
    ".compute",
    ".cs",
    ".hlsl",
    ".js",
    ".jslib",
    ".json",
    ".md",
    ".mjs",
    ".py",
    ".shader",
    ".sh",
    ".sql",
    ".yaml",
    ".yml",
}

ASSEMBLY_CONTRACTS = {
    "Assets/GritGud/Domain/GritGud.Domain.asmdef": {
        "references": [],
        "noEngineReferences": True,
    },
    "Assets/GritGud/Application/GritGud.Application.asmdef": {
        "references": ["GritGud.Domain"],
        "noEngineReferences": True,
    },
    "Assets/GritGud/Presentation/GritGud.Presentation.asmdef": {
        "references": [
            "GritGud.Domain",
            "GritGud.Application",
            "Unity.InputSystem",
            "Unity.RenderPipelines.Core.Runtime",
            "Unity.RenderPipelines.Universal.Runtime",
        ],
        "includePlatforms": [],
        "noEngineReferences": False,
    },
    "Assets/GritGud/Editor/GritGud.Editor.asmdef": {
        "references": [
            "GritGud.Domain",
            "GritGud.Application",
            "GritGud.Presentation",
        ],
        "includePlatforms": ["Editor"],
        "noEngineReferences": False,
    },
    "Assets/GritGud/Tests/EditMode/GritGud.Domain.Tests.asmdef": {
        "references": ["GritGud.Domain", "GritGud.Application"],
        "includePlatforms": ["Editor"],
        "noEngineReferences": True,
        "optionalUnityReferences": ["TestAssemblies"],
    },
    "Assets/GritGud/Tests/PresentationEditMode/GritGud.Presentation.Tests.asmdef": {
        "references": [
            "GritGud.Domain",
            "GritGud.Application",
            "GritGud.Presentation",
            "GritGud.Editor",
            "Unity.InputSystem",
        ],
        "includePlatforms": ["Editor"],
        "noEngineReferences": False,
        "optionalUnityReferences": ["TestAssemblies"],
    },
    "Assets/GritGud/Tests/PlayMode/GritGud.PlayMode.Tests.asmdef": {
        "references": [
            "GritGud.Domain",
            "GritGud.Application",
            "GritGud.Presentation",
            "Unity.InputSystem",
        ],
        "includePlatforms": [],
        "noEngineReferences": False,
        "optionalUnityReferences": ["TestAssemblies"],
    },
}

NEUTRAL_SOURCE_ROOTS = (
    Path("Assets/GritGud/Domain"),
    Path("Assets/GritGud/Application"),
)

FORBIDDEN_NEUTRAL_SOURCE_REFERENCES = (
    "using Unity",
    "UnityEngine",
    "GritGud.Presentation",
)

PRODUCTION_SOURCE_ROOTS = (
    Path("Assets/GritGud/Domain"),
    Path("Assets/GritGud/Application"),
    Path("Assets/GritGud/Presentation"),
    Path("Assets/GritGud/Editor"),
)

MAX_PRODUCTION_SOURCE_LINES = 1800
PRODUCTION_SOURCE_LINE_BUDGETS = {
    Path("Assets/GritGud/Application/Gameplay/GameplaySession.cs"): 2050,
    Path("Assets/GritGud/Presentation/LevelEditing/LevelEditorController.cs"): 1900,
}
UNITY_META_GUID = re.compile(r"^guid:\s*([0-9a-f]{32})\s*$", re.MULTILINE)


def tracked_files() -> list[Path]:
    result = subprocess.run(
        ["git", "ls-files", "-z"],
        cwd=REPOSITORY_ROOT,
        check=True,
        capture_output=True,
    )
    return [
        REPOSITORY_ROOT / relative_path
        for relative_path in result.stdout.decode("utf-8").split("\0")
        if relative_path
    ]


def main() -> int:
    failures: list[str] = []
    files = tracked_files()
    relative_files = {
        path.relative_to(REPOSITORY_ROOT): path for path in files
    }
    json_count = 0
    meta_guids: dict[str, Path] = {}

    for path in files:
        if path.suffix.lower() not in TEXT_SUFFIXES:
            continue
        try:
            text = path.read_text(encoding="utf-8-sig")
        except UnicodeDecodeError:
            continue

        relative_path = path.relative_to(REPOSITORY_ROOT)
        for line_number, line in enumerate(text.splitlines(), start=1):
            if line.startswith(CONFLICT_MARKERS):
                failures.append(
                    f"{relative_path}:{line_number}: unresolved conflict marker"
                )

        if path.suffix.lower() == ".json":
            json_count += 1
            try:
                json.loads(text)
            except json.JSONDecodeError as error:
                failures.append(
                    f"{relative_path}:{error.lineno}:{error.colno}: {error.msg}"
                )

        if path.suffix.lower() == ".cs" and any(
            relative_path.is_relative_to(root)
            for root in PRODUCTION_SOURCE_ROOTS
        ):
            line_count = len(text.splitlines())
            line_budget = PRODUCTION_SOURCE_LINE_BUDGETS.get(
                relative_path,
                MAX_PRODUCTION_SOURCE_LINES,
            )
            if line_count > line_budget:
                failures.append(
                    f"{relative_path}: production source has {line_count} lines; "
                    f"split cohesive responsibilities before exceeding "
                    f"{line_budget}"
                )

        if path.suffix.lower() == ".meta" and relative_path.is_relative_to(
            Path("Assets")
        ):
            match = UNITY_META_GUID.search(text)
            if match is None:
                failures.append(f"{relative_path}: Unity meta file has no valid GUID")
            else:
                guid = match.group(1)
                if guid in meta_guids:
                    failures.append(
                        f"{relative_path}: duplicate Unity meta GUID {guid}; "
                        f"already used by {meta_guids[guid]}"
                    )
                else:
                    meta_guids[guid] = relative_path

    for relative_path in relative_files:
        if relative_path.suffix.lower() != ".cs" or not relative_path.is_relative_to(
            Path("Assets")
        ):
            continue
        meta_path = Path(str(relative_path) + ".meta")
        if meta_path not in relative_files:
            failures.append(f"{relative_path}: tracked Unity source is missing {meta_path}")

    for relative_name, expected in ASSEMBLY_CONTRACTS.items():
        path = REPOSITORY_ROOT / relative_name
        try:
            assembly = json.loads(path.read_text(encoding="utf-8-sig"))
        except (OSError, json.JSONDecodeError) as error:
            failures.append(f"{relative_name}: cannot validate assembly contract: {error}")
            continue
        for field, expected_value in expected.items():
            if assembly.get(field) != expected_value:
                failures.append(
                    f"{relative_name}: {field} must be {expected_value!r}, "
                    f"found {assembly.get(field)!r}"
                )

    for path in files:
        relative_path = path.relative_to(REPOSITORY_ROOT)
        if path.suffix.lower() != ".cs" or not any(
            relative_path.is_relative_to(root) for root in NEUTRAL_SOURCE_ROOTS
        ):
            continue
        text = path.read_text(encoding="utf-8-sig")
        for line_number, line in enumerate(text.splitlines(), start=1):
            for forbidden in FORBIDDEN_NEUTRAL_SOURCE_REFERENCES:
                if forbidden in line:
                    failures.append(
                        f"{relative_path}:{line_number}: platform-neutral source "
                        f"must not reference {forbidden!r}"
                    )

    if failures:
        print("Repository validation failed:", file=sys.stderr)
        for failure in failures:
            print(f"- {failure}", file=sys.stderr)
        return 1

    print(
        f"Validated {len(files)} tracked files and parsed {json_count} JSON files."
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
