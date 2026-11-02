---
name: code-review
description: Revisão sistemática das alterações do Git no Sentinel - escopo pelo diff, regras da constituição, bugs, vulnerabilidades, testes, impacto em outros microsserviços e contratos, com achados classificados por severidade (arquivo, linha, evidência, recomendação). Use quando a pessoa pedir revisão das mudanças atuais, de uma branch ou de um caminho.
argument-hint: "[branch base | caminho] (padrão: alterações locais)"
---

# Revisão de código do Sentinel

Alvo: `$ARGUMENTS` (vazio = alterações locais não commitadas e commits da branch em relação a `master`).

## 1. Escopo

```bash
git status --short
git diff --stat; git diff --cached --stat
git log --oneline master..HEAD          # ou a base informada
git diff master...HEAD --stat
```

Classifique os arquivos alterados: serviço, camada, tipo (domínio, aplicação, API, infraestrutura, mensageria,
schema, configuração, teste, documentação, plataforma).

## 2. Revisão especializada, só onde agrega

A revisão linha a linha cabe ao subagent `code-reviewer`; delegue a ele com a lista de arquivos e o contexto da
mudança. Acione os demais apenas quando o diff justificar, em paralelo:

| Sinal no diff | Subagent |
| --- | --- |
| `.csproj`, projeto ou serviço novo, agregado novo, contrato/tópico novo, mudança de camada | `architecture-reviewer` |
| `Security/`, controllers, autorização, filtros de tenant, consumers com efeito por tenant, segredos, integrações externas | `security-auditor` |

Diff pequeno e local (um arquivo, sem contrato) pode ser revisado diretamente, sem subagent.

## 3. Verificações transversais (você mesmo)

- **Contratos e outros serviços**: mudou payload de evento, rota pública, código de erro, schema, cache key ou
  configuração consumida por outro serviço? Use o mapa de tópicos de `.claude/rules/backward-compatibility.md` e
  `git grep` nos outros serviços para achar consumidores. Mudança incompatível sem expand-and-contract é Crítica.
- **Cópias sincronizadas**: alteração em `Security/`, `Observability/`, `Messaging/Reliability`,
  `KafkaTracing`, `TenantBillingGateway` ou `DeprecatedRouteAttribute` precisa das cópias dos outros serviços.
- **Testes**: comportamento novo ou corrigido tem teste que falharia sem a mudança?
- **Documentação**: `docs/services/*.md`, ADRs, `services-validation.md` atualizados quando o comportamento muda.
- **Plataforma**: tópico novo no `kafka-init`, variável nova em `platform/.env.example`, serviço novo no
  Prometheus e no gateway.

Build, formatação e testes unitários são verificados pelo hook `validate-build`; não repita a suíte aqui, só
cite o resultado se for relevante.

## 4. Consolidação

Remova duplicatas entre os relatórios dos subagents, confirme cada achado lendo o código e descarte o que não se
sustenta. Apresente em português:

1. Resumo em até 3 linhas e veredito (aprovar / aprovar com ressalvas / mudanças necessárias).
2. Achados por severidade — **Crítica**, **Alta**, **Média**, **Baixa** — no formato:
   `[Severidade] título — caminho:linha — evidência — recomendação`.
3. O que não foi verificado.

Não edite arquivos durante a revisão, salvo pedido explícito.
