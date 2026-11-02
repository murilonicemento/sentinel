#!/usr/bin/env python3
"""validate-secrets (PreToolUse): impede a introdução de segredos em arquivos versionados.

Analisa só o conteúdo novo (Write: arquivo inteiro; Edit: linhas de new_string que não existiam em old_string;
Bash: comandos que gravam arquivos). Arquivos ignorados pelo git (ex.: platform/.env) são permitidos, pois são o
lugar correto para segredos locais. As mensagens citam regra, arquivo e linha, nunca o valor encontrado.

Falsos positivos legítimos (ex.: fixture de teste com uma chave fictícia) podem levar o marcador
`sentinel:allow-secret` na mesma linha; a ocorrência passa a exigir aprovação humana em vez de ser negada.
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

sys.dont_write_bytecode = True  # não criar __pycache__ dentro do repositório
sys.path.insert(0, str(Path(__file__).resolve().parent))

from hooklib import is_git_ignored, pre_tool_decision, project_dir, read_event, to_relative  # noqa: E402

ALLOW_MARKER = "sentinel:allow-secret"

# Formatos com alta confiança: valem em qualquer arquivo.
HIGH_CONFIDENCE = [
    ("chave privada PEM", re.compile(r"-----BEGIN (?:RSA |EC |DSA |OPENSSH |PGP |ENCRYPTED )?PRIVATE KEY-----")),
    ("AWS access key", re.compile(r"\b(?:AKIA|ASIA)[0-9A-Z]{16}\b")),
    ("token do GitHub", re.compile(r"\b(?:gh[pousr]_[A-Za-z0-9]{36,}|github_pat_[A-Za-z0-9_]{60,})\b")),
    ("token do Slack", re.compile(r"\bxox[abprs]-[A-Za-z0-9-]{10,}")),
    ("API key do SendGrid", re.compile(r"\bSG\.[A-Za-z0-9_-]{22}\.[A-Za-z0-9_-]{43}\b")),
    ("API key do Twilio", re.compile(r"\bSK[0-9a-fA-F]{32}\b")),
    ("API key do Google", re.compile(r"\bAIza[0-9A-Za-z_-]{35}\b")),
    ("chave da Anthropic/OpenAI", re.compile(r"\bsk-(?:ant-|proj-)?[A-Za-z0-9_-]{32,}\b")),
    ("chave secreta do Stripe", re.compile(r"\b[sr]k_live_[A-Za-z0-9]{20,}\b")),
    ("JWT literal", re.compile(r"\beyJ[A-Za-z0-9_-]{10,}\.eyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{20,}")),
]

# Senha embutida em connection string (Npgsql/SQL Server) ou em URI com credenciais.
CONN_PASSWORD = re.compile(r"(?i)\b(?:password|pwd)\s*=\s*([^;\"'\s}]+)")
URI_CREDENTIALS = re.compile(r"\b[a-z][a-z0-9+.-]*://[^/\s:@\"']+:([^@\s/\"']+)@")

# Atribuição literal a uma chave sensível em arquivo de configuração (JSON, YAML, .env).
SENSITIVE_KEY = (
    r"[A-Za-z0-9_.:-]*(?:password|passwd|secret|signingkey|signing_key|apikey|api_key|authtoken|auth_token|"
    r"accesskey|access_key|secretkey|secret_key|privatekey|private_key|mapkey|map_key|token)"
)
CONFIG_ASSIGNMENT = [
    re.compile(r'(?i)"(' + SENSITIVE_KEY + r')"\s*:\s*"([^"]*)"'),  # JSON
    re.compile(r"(?i)^\s*-?\s*(" + SENSITIVE_KEY + r")\s*[:=]\s*['\"]?([^'\"#\s]+)"),  # YAML / .env / compose
]
CONFIG_SUFFIXES = (".json", ".yml", ".yaml", ".env", ".config", ".ini", ".properties", ".toml", ".xml")

# Valores que documentam a forma da configuração sem conter um segredo real.
PLACEHOLDER = re.compile(
    r"(?i)^$|^\$\{|^\$\(|^\$[A-Z_]+$|^%[A-Z_]+%$|^<.*>$|^\{\{.*\}\}$|^\*+$|^x{3,}$|"
    r"change.?me|troque|trocaessa|placeholder|example|exemplo|your[-_]|seu[-_]|sua[-_]|dummy|fake|sample|"
    r"redacted|not[-_]?set|todo|test|teste|integration|local|y[o0]u?r[-_]?str[o0]ng"
)
# Credenciais padrão de infraestrutura local (compose/Testcontainers), já usadas no repositório e não secretas.
LOCAL_DEFAULTS = {
    "postgres", "guest", "admin", "minioadmin", "mongoadmin", "password", "sentinel", "dev", "development",
    "redis", "kafka", "root", "user", "pass",
}


def is_placeholder(value: str) -> bool:
    value = value.strip().strip("'\"")
    return bool(PLACEHOLDER.search(value)) or value.lower() in LOCAL_DEFAULTS


def is_test_path(relative: str) -> bool:
    lowered = relative.lower()
    return "/tests/" in f"/{lowered}" or lowered.endswith(("tests.cs", "test.cs")) or "/fixtures/" in lowered


def scan(relative: str, text: str, first_line: int = 1) -> list[tuple[str, int, bool]]:
    """Devolve (regra, linha, tem_marcador) para cada ocorrência."""
    findings: list[tuple[str, int, bool]] = []
    is_config = relative.lower().endswith(CONFIG_SUFFIXES) or Path(relative).name.lower().startswith(".env")
    is_markdown = relative.lower().endswith((".md", ".txt"))
    test_file = is_test_path(relative)

    for offset, line in enumerate(text.splitlines()):
        number = first_line + offset
        marked = ALLOW_MARKER in line
        for rule, pattern in HIGH_CONFIDENCE:
            if pattern.search(line):
                findings.append((rule, number, marked))
        if is_markdown:
            continue
        for match in CONN_PASSWORD.finditer(line):
            if not is_placeholder(match.group(1)) and not test_file:
                findings.append(("senha em connection string", number, marked))
        for match in URI_CREDENTIALS.finditer(line):
            if not is_placeholder(match.group(1)) and not test_file:
                findings.append(("credencial embutida em URI", number, marked))
        if is_config and not test_file:
            for pattern in CONFIG_ASSIGNMENT:
                for match in pattern.finditer(line):
                    key, value = match.group(1), match.group(2)
                    if key.lower().endswith(("tokenurl", "token_url", "tokenendpoint")):
                        continue
                    if not is_placeholder(value):
                        findings.append((f"valor literal para a chave sensível '{key}'", number, marked))
    return findings


def new_lines(old: str, new: str) -> str:
    existing = set(old.splitlines())
    return "\n".join(line for line in new.splitlines() if line not in existing)


def bash_writes(command: str) -> bool:
    return bool(re.search(r">|\btee\b|\bsed\b.*\s-i|<<|\bcp\b|\bprintf\b|\becho\b", command))


def report(target: str, findings: list[tuple[str, int, bool]], line_label: str = "linha") -> None:
    unmarked = [f for f in findings if not f[2]]
    summary = "; ".join(sorted({f"{rule} ({line_label} {line})" for rule, line, _ in findings}))
    if unmarked:
        pre_tool_decision(
            "deny",
            f"validate-secrets: possível segredo em {target}: {summary}. Segredos não podem ir para arquivos "
            "versionados (constituição §8.2). Use User Secrets, variáveis de ambiente ou platform/.env, e deixe "
            "em appsettings*.json/.env.example só placeholders como ${VAR} ou 'troque-por-...'. Se for um valor "
            f"fictício de teste, marque a linha com '{ALLOW_MARKER}' para pedir aprovação humana.",
        )
    else:
        pre_tool_decision(
            "ask",
            f"validate-secrets: {target} contém valores com formato de segredo marcados como '{ALLOW_MARKER}' "
            f"({summary}). Confirme que são fictícios.",
        )


def main() -> int:
    event = read_event()
    tool = event.get("tool_name", "")
    tool_input = event.get("tool_input") or {}
    root = project_dir(event)
    cwd = event.get("cwd")

    if tool in {"Write", "Edit", "MultiEdit"}:
        path = tool_input.get("file_path", "")
        relative = to_relative(path, root, cwd)
        if relative is None or is_git_ignored(root, relative):
            return 0  # fora do repositório ou não versionado
        if tool == "Write":
            text = str(tool_input.get("content", ""))
        elif tool == "Edit":
            text = new_lines(str(tool_input.get("old_string", "")), str(tool_input.get("new_string", "")))
        else:
            text = "\n".join(
                new_lines(str(e.get("old_string", "")), str(e.get("new_string", "")))
                for e in tool_input.get("edits", []) if isinstance(e, dict)
            )
        findings = scan(relative, text)
        if findings:
            report(relative, findings, "linha" if tool == "Write" else "linha do trecho novo")
        return 0

    if tool == "Bash":
        command = str(tool_input.get("command", ""))
        if not bash_writes(command):
            return 0
        findings = [
            (rule, line, ALLOW_MARKER in command)
            for rule, pattern in HIGH_CONFIDENCE
            for line, text in enumerate(command.splitlines(), start=1)
            if pattern.search(text)
        ]
        if findings:
            report("comando Bash", findings, "linha do comando")
    return 0


if __name__ == "__main__":
    sys.exit(main())
