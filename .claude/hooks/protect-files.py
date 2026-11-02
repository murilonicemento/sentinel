#!/usr/bin/env python3
"""protect-files (PreToolUse): pede autorização explícita antes de alterar arquivos fundamentais do Sentinel.

Edit/Write/NotebookEdit sobre um caminho protegido e comandos Bash que escrevem, movem ou apagam um caminho
protegido recebem `permissionDecision: "ask"`: a alteração só acontece se a pessoa aprovar no prompt. Leituras
(Read, Grep, cat, git diff...) nunca são bloqueadas.
"""

from __future__ import annotations

import re
import shlex
import sys
from pathlib import Path

sys.dont_write_bytecode = True  # não criar __pycache__ dentro do repositório
sys.path.insert(0, str(Path(__file__).resolve().parent))

from hooklib import pre_tool_decision, project_dir, read_event, to_relative  # noqa: E402

# Caminhos relativos à raiz do repositório. Entradas terminadas em "/" protegem o diretório inteiro.
PROTECTED = (
    "CLAUDE.md",
    "docs/constitution.md",
    "docs/RFC-Arquitetura.md",
    "global.json",
    ".claude/settings.json",
    ".claude/hooks/",
    ".claude/rules/",
    ".github/workflows/",
)

FILE_TOOLS = {"Edit", "Write", "NotebookEdit", "MultiEdit"}

# Comandos cujo efeito sobre qualquer caminho citado é uma alteração.
MUTATING_COMMANDS = {"rm", "mv", "truncate", "chmod", "chown", "ln", "unlink", "shred", "dd"}
# Subcomandos git que reescrevem ou removem arquivos do working tree.
MUTATING_GIT = {"checkout", "restore", "rm", "mv", "reset", "stash", "apply", "am", "clean"}
SEGMENT_SPLIT = re.compile(r"\|\||&&|[;|&\n]")


def protected_match(relative: str | None) -> str | None:
    if relative is None:
        return None
    for entry in PROTECTED:
        if entry.endswith("/"):
            if relative.startswith(entry):
                return entry
        elif relative == entry:
            return entry
    return None


def words(segment: str) -> list[str]:
    try:
        return shlex.split(segment, posix=True)
    except ValueError:
        return segment.split()


def bash_targets(command: str, root: Path, cwd: str | None) -> set[str]:
    """Devolve os caminhos protegidos que o comando parece alterar (heurística conservadora, sem falso bloqueio)."""
    hits: set[str] = set()

    # Redirecionamentos de saída: > arquivo, >> arquivo, 2> arquivo.
    for match in re.finditer(r"\d*>>?\s*(['\"]?)([^\s'\";|&<>]+)\1", command):
        entry = protected_match(to_relative(match.group(2), root, cwd))
        if entry:
            hits.add(entry)

    for raw in SEGMENT_SPLIT.split(command):
        tokens = [t for t in words(raw.strip()) if t]
        while tokens and re.match(r"^[A-Za-z_][A-Za-z0-9_]*=", tokens[0]):
            tokens = tokens[1:]  # atribuições de ambiente antes do comando
        if tokens and tokens[0] in {"sudo", "env", "command", "xargs"}:
            tokens = tokens[1:]
        if not tokens:
            continue
        name = Path(tokens[0]).name
        args = tokens[1:]
        candidates = [a for a in args if not a.startswith("-")]

        if name in MUTATING_COMMANDS:
            targets = candidates
        elif name in {"cp", "install", "rsync"}:
            targets = candidates[-1:]  # só o destino é alterado
        elif name == "tee":
            targets = candidates
        elif name in {"sed", "perl", "ruby"} and any(a == "-i" or a.startswith("-i") or a == "--in-place" for a in args):
            targets = candidates
        elif name == "git" and args:
            sub = next((a for a in args if not a.startswith("-")), "")
            targets = candidates[1:] if sub in MUTATING_GIT else []
        else:
            targets = []

        for target in targets:
            entry = protected_match(to_relative(target, root, cwd))
            if entry:
                hits.add(entry)
            elif target in {".", "./"} or target.endswith("/"):
                # Operações em diretório (ex.: git checkout -- .) que contêm arquivos protegidos.
                base = to_relative(target, root, cwd)
                if base is not None and any(p.startswith("" if base == "." else base.rstrip("/") + "/") for p in PROTECTED):
                    hits.add(f"{target} (contém arquivos protegidos)")
    return hits


def main() -> int:
    event = read_event()
    tool = event.get("tool_name", "")
    tool_input = event.get("tool_input") or {}
    root = project_dir(event)
    cwd = event.get("cwd")

    if tool in FILE_TOOLS:
        path = tool_input.get("file_path") or tool_input.get("notebook_path") or ""
        entry = protected_match(to_relative(path, root, cwd))
        if entry:
            pre_tool_decision(
                "ask",
                f"protect-files: '{entry}' é um arquivo fundamental do Sentinel (constituição, regras, SDK, CI ou "
                "configuração dos hooks). A alteração exige autorização explícita da pessoa responsável.",
            )
        return 0

    if tool == "Bash":
        hits = bash_targets(str(tool_input.get("command", "")), root, cwd)
        if hits:
            pre_tool_decision(
                "ask",
                "protect-files: o comando altera arquivos protegidos do Sentinel ("
                + ", ".join(sorted(hits))
                + "). Confirme se essa alteração foi autorizada.",
            )
    return 0


if __name__ == "__main__":
    sys.exit(main())
