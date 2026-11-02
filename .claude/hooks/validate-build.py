#!/usr/bin/env python3
"""validate-build (Stop): compila e testa só o que mudou antes de o Claude encerrar o turno.

Fluxo:
1. Lê as alterações do working tree (git: modificados, staged e não rastreados) e mapeia cada arquivo para o
   .csproj mais próximo. Sem alteração .NET relevante, encerra em silêncio.
2. Se o conjunto alterado (impressão digital do conteúdo) já foi validado com sucesso nesta sessão, não repete.
3. Executa, como no CI: `dotnet build <projeto> -warnaserror`, `dotnet format whitespace <projeto>
   --verify-no-changes` e `dotnet test` nos projetos *UnitTests dos serviços afetados. Alterações em
   Directory.Packages.props, Directory.Build.* ou global.json compilam a Sentinel.sln inteira. Testes de integração
   (Testcontainers) só rodam com SENTINEL_VALIDATE_BUILD_INTEGRATION=1.
4. Em falha, devolve `decision: "block"` com o resumo dos erros para o Claude corrigir.

Proteções contra ciclos: no máximo MAX_BLOCKS bloqueios consecutivos por sessão; se o Claude parar de novo sem
alterar nada desde a última falha, o hook não roda outra vez e avisa a pessoa usuária em vez de bloquear.
SENTINEL_VALIDATE_BUILD=0 desliga o hook.
"""

from __future__ import annotations

import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
import time
from pathlib import Path, PurePosixPath

sys.dont_write_bytecode = True  # não criar __pycache__ dentro do repositório
sys.path.insert(0, str(Path(__file__).resolve().parent))

from hooklib import emit, git, project_dir, read_event  # noqa: E402

MAX_BLOCKS = 3
TOTAL_BUDGET_SECONDS = 840  # o timeout do hook em settings.json é 900 s
SOLUTION_WIDE = {"Directory.Packages.props", "Directory.Build.props", "Directory.Build.targets", "global.json"}
RELEVANT_SUFFIXES = (".cs", ".csproj", ".json", ".props", ".targets", ".resx", ".razor", ".sql", ".xml")
ERROR_LINE = re.compile(r"(error|warning) [A-Z]+[0-9]+|\bFailed\b|\[FAIL\]|Assert\.|Expected|Exception:|formatting", re.I)


def changed_files(root: Path) -> list[str]:
    output = git(root, "status", "--porcelain=v1", "-z", "--untracked-files=all") or ""
    files: list[str] = []
    entries = output.split("\0")
    index = 0
    while index < len(entries):
        entry = entries[index]
        index += 1
        if len(entry) < 4:
            continue
        status, path = entry[:2], entry[3:]
        if "R" in status or "C" in status:
            index += 1  # o caminho de origem vem na entrada seguinte
        if "D" in status:
            continue
        files.append(path)
    return files


def owning_project(root: Path, relative: str) -> str | None:
    current = (root / relative).parent
    services = root / "services"
    while services in current.parents or current == services:
        if current == services:
            return None
        projects = sorted(current.glob("*.csproj"))
        if projects:
            return projects[0].relative_to(root).as_posix()
        current = current.parent
    return None


def service_of(relative: str) -> str | None:
    parts = PurePosixPath(relative).parts
    return parts[1] if len(parts) > 2 and parts[0] == "services" else None


def fingerprint(root: Path, files: list[str]) -> str:
    digest = hashlib.sha256((git(root, "rev-parse", "HEAD") or "").encode())
    for relative in sorted(files):
        digest.update(relative.encode())
        try:
            digest.update(hashlib.sha256((root / relative).read_bytes()).digest())
        except OSError:
            digest.update(b"<missing>")
    return digest.hexdigest()


def state_path(event: dict) -> Path:
    base = event.get("scratchpad_dir") or os.path.join(tempfile.gettempdir(), "sentinel-claude-hooks")
    session = re.sub(r"[^A-Za-z0-9_-]", "", str(event.get("session_id") or "default"))
    return Path(base) / f"validate-build-{session}.json"


def load_state(path: Path) -> dict:
    try:
        return json.loads(path.read_text())
    except (OSError, json.JSONDecodeError):
        return {}


def save_state(path: Path, state: dict) -> None:
    try:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(state))
    except OSError:
        pass


def summarize(output: str, limit: int = 40) -> str:
    lines = []
    for line in output.splitlines():
        line = line.strip()
        if line and ERROR_LINE.search(line) and line not in lines:
            lines.append(line[:400])
    if not lines:
        lines = [line[:400] for line in output.strip().splitlines()[-limit:]]
    return "\n".join(lines[:limit])


def run(root: Path, label: str, command: list[str], deadline: float) -> tuple[bool, str]:
    remaining = deadline - time.monotonic()
    if remaining <= 10:
        return False, f"{label}: não executado (orçamento de tempo do hook esgotado)."
    try:
        result = subprocess.run(
            command, cwd=root, capture_output=True, text=True, timeout=remaining, check=False,
            env={**os.environ, "DOTNET_NOLOGO": "1", "DOTNET_CLI_TELEMETRY_OPTOUT": "1"},
        )
    except subprocess.TimeoutExpired:
        return False, f"{label}: tempo esgotado."
    if result.returncode == 0:
        return True, ""
    return False, f"{label}:\n{summarize(result.stdout + result.stderr)}"


def plan(root: Path, files: list[str]) -> tuple[list[str], list[str], bool]:
    """Devolve (projetos a compilar, projetos de teste a executar, compilar a solution inteira)."""
    solution_wide = any(f in SOLUTION_WIDE for f in files)
    projects: set[str] = set()
    services: set[str] = set()
    for relative in files:
        if not relative.startswith("services/") or not relative.endswith(RELEVANT_SUFFIXES):
            continue
        if "/bin/" in relative or "/obj/" in relative:
            continue
        project = owning_project(root, relative)
        if project:
            projects.add(project)
            services.add(service_of(project) or "")

    include_integration = os.environ.get("SENTINEL_VALIDATE_BUILD_INTEGRATION") == "1"
    tests: set[str] = set()
    for service in filter(None, services):
        for candidate in (root / "services" / service).rglob("*.csproj"):
            relative = candidate.relative_to(root).as_posix()
            if "/obj/" in relative or "/bin/" in relative or "/tests/" not in relative:
                continue
            if candidate.stem.endswith("UnitTests") or (include_integration and candidate.stem.endswith("IntegrationTests")):
                tests.add(relative)
    # Projetos de teste também são compilados, então não precisam aparecer duas vezes.
    build = sorted(p for p in projects if p not in tests) + sorted(tests)
    return build, sorted(tests), solution_wide


def main() -> int:
    if os.environ.get("SENTINEL_VALIDATE_BUILD") == "0":
        return 0
    event = read_event()
    if event.get("background_tasks"):
        return 0  # o turno espera trabalho em segundo plano; validar agora concorreria com ele
    root = project_dir(event)
    if not (root / "Sentinel.sln").exists() or shutil.which("dotnet") is None:
        return 0

    files = [f for f in changed_files(root) if f in SOLUTION_WIDE or f.startswith("services/")]
    build, tests, solution_wide = plan(root, files)
    if not build and not solution_wide:
        return 0

    state_file = state_path(event)
    state = load_state(state_file)
    current = fingerprint(root, files)
    if state.get("fingerprint") == current:
        if state.get("status") == "fail":
            emit({"systemMessage": "validate-build: as falhas da última validação continuam sem correção "
                                   "(nenhum arquivo mudou desde então). Não considere a tarefa concluída."})
        return 0

    deadline = time.monotonic() + TOTAL_BUDGET_SECONDS
    failures: list[str] = []
    if solution_wide:
        ok, detail = run(root, "dotnet build Sentinel.sln", ["dotnet", "build", "Sentinel.sln", "-warnaserror", "--nologo", "-v", "q"], deadline)
        if not ok:
            failures.append(detail)
    else:
        for project in build:
            ok, detail = run(root, f"dotnet build {project}", ["dotnet", "build", project, "-warnaserror", "--nologo", "-v", "q"], deadline)
            if not ok:
                failures.append(detail)
    if not failures:
        for project in build:
            ok, detail = run(
                root, f"dotnet format whitespace {project}",
                ["dotnet", "format", "whitespace", project, "--verify-no-changes", "--no-restore"], deadline,
            )
            if not ok:
                failures.append(detail + "\nCorrija com: dotnet format whitespace " + project)
        for project in tests:
            ok, detail = run(root, f"dotnet test {project}", ["dotnet", "test", project, "--no-build", "--nologo", "-v", "q"], deadline)
            if not ok:
                failures.append(detail)

    scope = "Sentinel.sln" if solution_wide else ", ".join(PurePosixPath(p).stem for p in build)
    if not failures:
        save_state(state_file, {"fingerprint": current, "status": "pass", "blocks": 0})
        skipped = "" if os.environ.get("SENTINEL_VALIDATE_BUILD_INTEGRATION") == "1" else " Testes de integração não executados."
        emit({"systemMessage": f"validate-build: build, formatação e testes unitários OK ({scope}).{skipped}"})
        return 0

    blocks = int(state.get("blocks", 0)) + 1
    save_state(state_file, {"fingerprint": current, "status": "fail", "blocks": blocks})
    report = "\n\n".join(failures)
    if blocks > MAX_BLOCKS:
        emit({"systemMessage": f"validate-build: falhas persistem após {MAX_BLOCKS} tentativas de correção ({scope}). "
                               "O hook parou de bloquear para evitar um ciclo; revise manualmente.\n" + report[:3000]})
        return 0
    emit({
        "decision": "block",
        "reason": f"validate-build encontrou falhas ({scope}); a tarefa não está concluída. Corrija e finalize de "
                  f"novo, ou explique à pessoa usuária por que a falha não é causada pelas suas alterações "
                  f"(tentativa {blocks}/{MAX_BLOCKS}).\n\n" + report[:6000],
    })
    return 0


if __name__ == "__main__":
    sys.exit(main())
