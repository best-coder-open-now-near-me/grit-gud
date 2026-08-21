import re
import sys
from dataclasses import dataclass
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[1]
MIGRATION_ROOT = REPOSITORY_ROOT / "supabase" / "migrations"
REPOSITORY_SOURCE = (
    REPOSITORY_ROOT
    / "Assets"
    / "GritGud"
    / "Presentation"
    / "Supabase"
    / "SupabaseLevelDraftRepository.cs"
)
PARSER_SOURCE = (
    REPOSITORY_ROOT
    / "Assets"
    / "GritGud"
    / "Presentation"
    / "Supabase"
    / "SupabaseLevelDraftResponseParser.cs"
)

FUNCTION_PATTERN = re.compile(
    r"create\s+or\s+replace\s+function\s+public\.(?P<name>[a-z0-9_]+)"
    r"\s*\((?P<parameters>.*?)\)\s*returns\s+"
    r"(?:table\s*\((?P<columns>.*?)\)|(?P<scalar>[a-z ]+?))"
    r"\s+language\s+(?P<language>[a-z]+)\s+"
    r"(?P<header>.*?)\s+as\s+\$\$(?P<body>.*?)\$\$;",
    re.IGNORECASE | re.DOTALL,
)


@dataclass(frozen=True)
class RpcContract:
    parameters: tuple[tuple[str, str], ...]
    columns: tuple[tuple[str, str], ...] = ()
    scalar: str = ""


SUMMARY_COLUMNS = (
    ("draft_id", "uuid"),
    ("name", "text"),
    ("revision", "bigint"),
    ("updated_at", "timestamptz"),
    ("level_id", "text"),
    ("display_name", "text"),
    ("schema_version", "integer"),
)

EXPECTED_CONTRACTS = {
    "list_level_draft_library": RpcContract((), SUMMARY_COLUMNS),
    "load_level_draft_by_id": RpcContract(
        (("requested_id", "uuid"),),
        SUMMARY_COLUMNS + (("document", "text"),),
    ),
    "create_level_draft": RpcContract(
        (("requested_name", "text"), ("requested_document", "jsonb")),
        SUMMARY_COLUMNS,
    ),
    "save_level_draft": RpcContract(
        (
            ("requested_id", "uuid"),
            ("expected_revision", "bigint"),
            ("requested_document", "jsonb"),
        ),
        SUMMARY_COLUMNS,
    ),
    "rename_level_draft_by_id": RpcContract(
        (("requested_id", "uuid"), ("requested_name", "text")),
        SUMMARY_COLUMNS,
    ),
    "duplicate_level_draft": RpcContract(
        (("requested_id", "uuid"), ("requested_name", "text")),
        SUMMARY_COLUMNS + (("document", "text"),),
    ),
    "archive_level_draft": RpcContract(
        (("requested_id", "uuid"),),
        scalar="void",
    ),
}


def normalize(value: str) -> str:
    return " ".join(value.lower().split())


def declarations(value: str) -> tuple[tuple[str, str], ...]:
    if not value.strip():
        return ()
    result = []
    for declaration in value.split(","):
        parts = normalize(declaration).split(" ", maxsplit=1)
        if len(parts) != 2:
            raise ValueError(f"invalid SQL declaration: {declaration.strip()!r}")
        result.append((parts[0], parts[1]))
    return tuple(result)


def signature_types(contract: RpcContract) -> str:
    return ", ".join(field_type for _, field_type in contract.parameters)


def main() -> int:
    failures: list[str] = []
    migrations = sorted(MIGRATION_ROOT.glob("*.sql"))
    migration_names = [path.name for path in migrations]
    if not migrations:
        failures.append("no Supabase migrations were found")
    for name in migration_names:
        if not re.fullmatch(r"\d{12}_[a-z0-9_]+\.sql", name):
            failures.append(f"migration name is not ordered and portable: {name}")
    if len(migration_names) != len(set(migration_names)):
        failures.append("Supabase migration names are not unique")

    sql = "\n".join(path.read_text(encoding="utf-8-sig") for path in migrations)
    definitions = {}
    for match in FUNCTION_PATTERN.finditer(sql):
        definitions[match.group("name").lower()] = match

    for name, expected in EXPECTED_CONTRACTS.items():
        match = definitions.get(name)
        if match is None:
            failures.append(f"missing RPC definition: public.{name}")
            continue
        try:
            actual_parameters = declarations(match.group("parameters"))
            actual_columns = declarations(match.group("columns") or "")
        except ValueError as error:
            failures.append(f"public.{name}: {error}")
            continue
        actual_scalar = normalize(match.group("scalar") or "")
        if actual_parameters != expected.parameters:
            failures.append(
                f"public.{name}: parameters {actual_parameters!r} do not match "
                f"{expected.parameters!r}"
            )
        if actual_columns != expected.columns:
            failures.append(
                f"public.{name}: return columns {actual_columns!r} do not match "
                f"{expected.columns!r}"
            )
        if actual_scalar != expected.scalar:
            failures.append(
                f"public.{name}: scalar return {actual_scalar!r} does not match "
                f"{expected.scalar!r}"
            )

        signature = signature_types(expected)
        effective_security_definer = (
            "security definer" in normalize(match.group("header"))
            or re.search(
                rf"alter\s+function\s+public\.{name}\s*\(\s*"
                rf"{re.escape(signature)}\s*\)\s+security\s+definer\s*;",
                sql,
                re.IGNORECASE,
            )
            is not None
        )
        if not effective_security_definer:
            failures.append(f"public.{name} is not SECURITY DEFINER")

        for permission in (
            f"revoke all on function public.{name}({signature}) from public;",
            f"grant execute on function public.{name}({signature}) to authenticated;",
        ):
            if normalize(permission) not in normalize(sql):
                failures.append(f"missing RPC permission boundary: {permission}")

    save_definition = definitions.get("save_level_draft")
    if save_definition is not None:
        save_body = normalize(save_definition.group("body"))
        null_guard = "expected_revision is null"
        distinct_guard = (
            "current_draft.revision is distinct from expected_revision"
        )
        update_statement = "update public.level_drafts"
        if null_guard not in save_body:
            failures.append(
                "public.save_level_draft must reject a null expected_revision"
            )
        if distinct_guard not in save_body:
            failures.append(
                "public.save_level_draft must compare revisions with "
                "IS DISTINCT FROM"
            )
        guard_positions = (
            save_body.find(null_guard),
            save_body.find(distinct_guard),
        )
        update_position = save_body.find(update_statement)
        if (
            min(guard_positions) >= 0
            and update_position >= 0
            and max(guard_positions) > update_position
        ):
            failures.append(
                "public.save_level_draft must validate expected_revision "
                "before updating the draft"
            )

    repository = REPOSITORY_SOURCE.read_text(encoding="utf-8-sig")
    for name in EXPECTED_CONTRACTS:
        if f'"{name}"' not in repository:
            failures.append(f"Supabase repository does not invoke {name}")
    duplicate_match = re.search(
        r"DuplicateAsync\(.*?\n\s*public\s+Task\s+DeleteAsync",
        repository,
        re.DOTALL,
    )
    if duplicate_match is None:
        failures.append("could not inspect DuplicateAsync")
    elif "LoadAsync" in duplicate_match.group(0):
        failures.append("DuplicateAsync must not perform a post-mutation load")

    parser = PARSER_SOURCE.read_text(encoding="utf-8-sig")
    for field_name, _ in SUMMARY_COLUMNS + (("document", "text"),):
        if re.search(rf"public\s+\w+\s+{field_name}\s*;", parser) is None:
            failures.append(f"draft response parser is missing field {field_name}")

    hardening_migration = MIGRATION_ROOT / (
        "202608170001_harden_level_draft_rpc_contracts.sql"
    )
    if hardening_migration.exists():
        hardening_sql = normalize(hardening_migration.read_text(encoding="utf-8-sig"))
        for name in (
            "load_level_draft_by_id",
            "create_level_draft",
            "save_level_draft",
            "rename_level_draft_by_id",
            "duplicate_level_draft",
        ):
            if f"drop function if exists public.{name}" not in hardening_sql:
                failures.append(
                    f"return-shape migration must drop public.{name} before recreation"
                )
    else:
        failures.append(f"missing hardening migration: {hardening_migration.name}")

    if failures:
        print("Supabase contract validation failed:", file=sys.stderr)
        for failure in failures:
            print(f"- {failure}", file=sys.stderr)
        return 1

    print(
        f"Validated {len(migrations)} Supabase migrations and "
        f"{len(EXPECTED_CONTRACTS)} RPC contracts."
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
