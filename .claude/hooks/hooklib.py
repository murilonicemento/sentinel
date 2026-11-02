"""Utilitários compartilhados pelos hooks do Sentinel (somente biblioteca padrão do Python 3.9+)."""

from __future__ import annotations

import json
import os
import subprocess
import sys
from pathlib import Path
from typing import Any, Optional


def read_event() -> dict[str, Any]:
    """Lê o JSON do evento enviado pelo Claude Code no stdin. Entrada inválida vira dicionário vazio."""
    try:
        data = json.load(sys.stdin)
        return data if isinstance(data, dict) else {}
    except (json.JSONDecodeError, ValueError):
        return {}


def project_dir(event: dict[str, Any]) -> Path:
    root = os.environ.get("CLAUDE_PROJECT_DIR") or event.get("cwd") or os.getcwd()
    return Path(root).resolve()


def to_relative(path: str, root: Path, cwd: Optional[str] = None) -> Optional[str]:
    """Converte um caminho (absoluto ou relativo ao cwd) em caminho POSIX relativo à raiz; None se estiver fora dela."""
    if not path:
        return None
    candidate = Path(path)
    if not candidate.is_absolute():
        candidate = Path(cwd or root) / candidate
    try:
        resolved = candidate.resolve()
    except OSError:
        resolved = Path(os.path.abspath(candidate))
    try:
        return resolved.relative_to(root).as_posix()
    except ValueError:
        return None


def emit(payload: dict[str, Any]) -> None:
    print(json.dumps(payload, ensure_ascii=False))


def pre_tool_decision(decision: str, reason: str) -> None:
    emit({
        "hookSpecificOutput": {
            "hookEventName": "PreToolUse",
            "permissionDecision": decision,
            "permissionDecisionReason": reason,
        }
    })


def git(root: Path, *args: str, timeout: int = 30) -> Optional[str]:
    try:
        result = subprocess.run(
            ["git", "-C", str(root), *args],
            capture_output=True, text=True, timeout=timeout, check=False,
        )
    except (OSError, subprocess.TimeoutExpired):
        return None
    return result.stdout if result.returncode == 0 else None


def is_git_ignored(root: Path, relative: str) -> bool:
    try:
        result = subprocess.run(
            ["git", "-C", str(root), "check-ignore", "-q", "--no-index", relative],
            capture_output=True, timeout=10, check=False,
        )
    except (OSError, subprocess.TimeoutExpired):
        return False
    return result.returncode == 0
