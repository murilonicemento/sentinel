#!/usr/bin/env python3
"""validate-architecture (PostToolUse): verifica as regras de dependência da constituição (§4, §5.3, §11.1).

O que é verificado de forma determinística:
- Grafo de projetos (.csproj, lido como XML do MSBuild): direção Domain ← Application ← Infrastructure/Api,
  ausência de referências entre microsserviços e de pacotes de persistência, transporte ou web em Domain e
  Application. A direção definida pelos .csproj é a mesma que o compilador impõe aos `using`, então não há
  análise de código C# aqui; o build do hook validate-build cobre o restante.
- appsettings*.json: connection strings que apontam para o banco de outro microsserviço.

O que não pode ser decidido por script é sinalizado para revisão (ex.: controller importando um namespace de
Infrastructure, que compila porque a Api referencia Infrastructure para a composição de DI).

Violações retornam `decision: "block"` para o Claude corrigir; sinais de revisão retornam `additionalContext`.
"""

from __future__ import annotations

import json
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path, PurePosixPath

sys.dont_write_bytecode = True  # não criar __pycache__ dentro do repositório
sys.path.insert(0, str(Path(__file__).resolve().parent))

from hooklib import emit, project_dir, read_event, to_relative  # noqa: E402

# Pacotes que Domain e Application não podem referenciar (prefixos, comparação sem diferenciar maiúsculas).
INFRA_PACKAGES = (
    "microsoft.entityframeworkcore", "npgsql", "dapper", "mongodb.", "confluent.kafka", "stackexchange.redis",
    "microsoft.extensions.caching.stackexchangeredis", "nest", "elastic.", "minio", "microsoft.aspnetcore.",
    "aspnetcore.healthchecks", "twilio", "sendgrid", "mqttnet", "ocelot", "polly", "microsoft.extensions.http",
    "serilog", "opentelemetry", "scalar.", "csvhelper", "system.data.", "microsoft.data.",
)
LAYERS = ("Domain", "Application", "Infrastructure", "Api")
ALLOWED = {
    "Domain": set(),
    "Application": {"Domain"},
    "Infrastructure": {"Application", "Domain"},
    "Api": {"Application", "Infrastructure"},
}


def layer_of(project_name: str) -> str | None:
    parts = project_name.split(".")
    for index in range(len(parts) - 1, 0, -1):
        if parts[index] in LAYERS:
            return parts[index]
    return None


def service_of(relative: str) -> str | None:
    parts = PurePosixPath(relative).parts
    return parts[1] if len(parts) > 2 and parts[0] == "services" else None


def is_test_project(relative: str) -> bool:
    return "/tests/" in relative or PurePosixPath(relative).stem.endswith(("Tests", "Test"))


def local_name(tag: str) -> str:
    return tag.rsplit("}", 1)[-1]


def check_project(root: Path, relative: str) -> tuple[list[str], list[str]]:
    violations: list[str] = []
    reviews: list[str] = []
    path = root / relative
    try:
        tree = ET.parse(path)
    except (ET.ParseError, OSError) as error:
        return [f"{relative}: não foi possível ler o projeto ({error.__class__.__name__})."], []

    project = tree.getroot()
    name = path.stem
    service = service_of(relative)
    layer = layer_of(name)
    test_project = is_test_project(relative)
    sdk = (project.get("Sdk") or "").lower()

    for element in project.iter():
        tag = local_name(element.tag)
        include = element.get("Include") or ""
        if tag == "ProjectReference" and include:
            target = (path.parent / include.replace("\\", "/")).resolve()
            target_rel = to_relative(str(target), root)
            target_name = target.stem
            target_service = service_of(target_rel) if target_rel else None
            if target_rel is None or target_service != service:
                violations.append(
                    f"{relative}: referencia '{target_name}', fora do microsserviço '{service}'. Serviços não "
                    "compartilham código nem projetos (constituição §5.2, §5.3); integre por API ou evento."
                )
                continue
            if test_project or layer is None:
                continue
            target_layer = layer_of(target_name)
            if target_layer and target_layer not in ALLOWED[layer]:
                if layer == "Api" and target_layer == "Domain":
                    reviews.append(
                        f"{relative}: a Api referencia Domain diretamente. A constituição define Api → Application; "
                        "confirme que a Api não expõe entidades de domínio no contrato (§7.2)."
                    )
                else:
                    violations.append(
                        f"{relative}: {layer} não pode referenciar {target_layer} ('{target_name}'). Direção "
                        "permitida: Api → Application → Domain, Infrastructure → Application/Domain (§4.5)."
                    )
        elif tag in {"PackageReference", "FrameworkReference"} and include and not test_project:
            lowered = include.lower()
            if layer == "Domain":
                violations.append(
                    f"{relative}: Domain referencia '{include}'. Domain não depende de frameworks nem de "
                    "pacotes externos (§4.1)."
                )
            elif layer == "Application" and (
                tag == "FrameworkReference" or lowered.startswith(INFRA_PACKAGES)
            ):
                violations.append(
                    f"{relative}: Application referencia '{include}', um detalhe de infraestrutura ou transporte. "
                    "Declare uma porta (interface) em Application e implemente-a em Infrastructure (§4.2)."
                )

    if layer in {"Domain", "Application"} and not test_project and sdk.endswith(".web"):
        violations.append(f"{relative}: {layer} usa o SDK Microsoft.NET.Sdk.Web; use Microsoft.NET.Sdk (§4.1, §4.2).")
    return violations, reviews


def normalize(name: str) -> str:
    return re.sub(r"[^a-z0-9]", "", name.lower()).removesuffix("database").removesuffix("db")


def check_appsettings(root: Path, relative: str) -> list[str]:
    service = service_of(relative)
    if not service:
        return []
    try:
        content = json.loads((root / relative).read_text(encoding="utf-8-sig"))
    except (OSError, json.JSONDecodeError):
        return []
    owner = normalize(service)
    reviews: list[str] = []

    def walk(node: object, key_path: str) -> None:
        if isinstance(node, dict):
            for key, value in node.items():
                walk(value, f"{key_path}:{key}" if key_path else key)
        elif isinstance(node, list):
            for index, value in enumerate(node):
                walk(value, f"{key_path}:{index}")
        elif isinstance(node, str):
            for match in re.finditer(r"(?i)\b(?:database|initial catalog)\s*=\s*([A-Za-z0-9_-]+)", node):
                database = normalize(match.group(1))
                if database and not (database.startswith(owner) or owner.startswith(database)):
                    reviews.append(
                        f"{relative} ({key_path}): a connection string aponta para o banco '{match.group(1)}', que "
                        f"não parece pertencer a '{service}'. Acesso ao banco de outro serviço é proibido (§11.1)."
                    )

    walk(content, "")
    return reviews


def check_controller(root: Path, relative: str) -> list[str]:
    folder = PurePosixPath(relative).parent.name
    if folder not in {"Controllers", "Endpoints"} or ".Api/" not in relative:
        return []
    try:
        text = (root / relative).read_text(encoding="utf-8-sig")
    except OSError:
        return []
    imports = sorted(set(re.findall(r"^\s*using\s+([A-Za-z0-9_.]+\.Infrastructure(?:\.[A-Za-z0-9_.]+)?)\s*;", text, re.M)))
    if not imports:
        return []
    return [
        f"{relative}: o controller importa {', '.join(imports)}. A referência Api → Infrastructure é só para "
        "composição de DI (§4.4); controllers devem usar casos de uso/portas de Application. Revise."
    ]


def changed_paths(event: dict, root: Path) -> tuple[list[str], bool]:
    """Arquivos alterados pela ferramenta e se é preciso varrer todos os .csproj (comando dotnet/csproj via Bash)."""
    tool = event.get("tool_name", "")
    tool_input = event.get("tool_input") or {}
    if tool in {"Edit", "Write", "MultiEdit"}:
        relative = to_relative(tool_input.get("file_path", ""), root, event.get("cwd"))
        return ([relative] if relative else []), False
    if tool == "Bash":
        command = str(tool_input.get("command", ""))
        full_scan = bool(re.search(r"\bdotnet\s+(add|remove|new|sln)\b|\.csproj\b|\.props\b", command))
        return [], full_scan
    return [], False


def main() -> int:
    event = read_event()
    root = project_dir(event)
    paths, full_scan = changed_paths(event, root)

    projects: set[str] = set()
    others: list[str] = []
    for relative in paths:
        if relative.endswith(".csproj"):
            projects.add(relative)
        elif relative.startswith("services/"):
            others.append(relative)
    if full_scan:
        projects.update(
            p.relative_to(root).as_posix()
            for p in (root / "services").rglob("*.csproj")
            if "/obj/" not in p.as_posix() and "/bin/" not in p.as_posix()
        )

    violations: list[str] = []
    reviews: list[str] = []
    for relative in sorted(projects):
        if (root / relative).exists():
            found_violations, found_reviews = check_project(root, relative)
            violations += found_violations
            reviews += found_reviews
    for relative in others:
        name = PurePosixPath(relative).name
        if re.fullmatch(r"appsettings(\.[A-Za-z0-9]+)?\.json", name):
            reviews += check_appsettings(root, relative)
        elif name.endswith(".cs"):
            reviews += check_controller(root, relative)

    if violations:
        emit({
            "decision": "block",
            "reason": "validate-architecture encontrou violações da constituição:\n- " + "\n- ".join(violations)
            + "\nCorrija a referência ou registre uma exceção aprovada em ADR antes de seguir.",
        })
    elif reviews:
        emit({
            "hookSpecificOutput": {
                "hookEventName": "PostToolUse",
                "additionalContext": "validate-architecture pede revisão:\n- " + "\n- ".join(reviews),
            }
        })
    return 0


if __name__ == "__main__":
    sys.exit(main())
