import json
import subprocess
import sys
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[1]
CONFLICT_MARKERS = ("<<<<<<< ", "=======", ">>>>>>> ")
TEXT_SUFFIXES = {
    ".compute",
    ".cs",
    ".hlsl",
    ".json",
    ".shader",
    ".yaml",
    ".yml",
}


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
    json_count = 0

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
