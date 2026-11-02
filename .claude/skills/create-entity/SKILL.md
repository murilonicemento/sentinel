---
name: create-entity
description: Modela entidades, agregados e value objects no Domain de um serviço do Sentinel com identidade, invariantes, encapsulamento e regras de negócio. Use ao criar ou alterar um conceito de domínio (ex. novo agregado, value object, regra de transição de estado).
argument-hint: <serviço> <Conceito>
---

# Criar entidade, agregado ou value object

## 1. Contexto

1. Leia `docs/constitution.md` §4.1 e §11.2 e a especificação do serviço em `docs/services/`.
2. Abra o Domain do serviço e siga o estilo existente (exemplo: `AlertOrchestrator.Domain/Aggregates/AlertWindow.cs`,
   `ValueObjects/RiskType.cs`). Pastas usuais: `Aggregates/` ou `Entities/`, `ValueObjects/`, `Enums/`, `Events/`,
   `Interfaces/Repositories/`.
3. Nomes de enum: a constituição §5.1 pede o sufixo `Enum` e `.claude/rules/coding-standarts.md` o desaconselha;
   siga a convenção já usada no serviço (ex.: `AlertStatus` em alert-orchestrator, `PriorityEnum` em channels).

## 2. Escolha o tipo

| Pergunta | Resposta → tipo |
| --- | --- |
| Tem identidade que persiste enquanto os atributos mudam? | Entidade |
| É a raiz de consistência, carregada e salva como unidade? | Agregado (raiz) |
| É definido só pelos valores, imutável, substituível? | Value object (`sealed record`) |

Um agregado por transação. Referencie outros agregados por id, nunca por navegação.

## 3. Regras de implementação

- **Domain puro**: sem atributos de EF/Mongo, sem `using` de infraestrutura, sem DTOs. O mapeamento de
  persistência fica em Infrastructure (`Persistence/EntityConfigurations`, class maps).
- **Encapsulamento**: setters `private`, construtor `private` sem parâmetros só se o ORM exigir, criação por
  fábrica estática (`Open`, `Create`) que valida as invariantes, coleções expostas como `IReadOnlyList`.
- **Comportamento no modelo**: transições de estado como métodos (`Trigger`, `Close`, `Escalate`) que validam o
  estado atual e lançam exceção de domínio ou retornam resultado, conforme o padrão do serviço.
- **Value objects**: imutáveis, validam na construção, igualdade por valor (`record`), normalizam a entrada
  (ex.: `RiskType.FromString`).
- **Identidade**: `Guid` gerado no domínio ou id determinístico quando a idempotência exigir (ex.: ids derivados
  de evento de origem).
- **Tenant**: entidades que pertencem a um tenant carregam `TenantId` obrigatório e validado; nunca opcional
  quando o recurso é por tenant.
- **Tempo**: UTC; receba o instante como parâmetro quando a regra depender dele, para ser testável.
- **Eventos de domínio**: registre em lista interna e exponha somente leitura, como `AlertWindow.DomainEvents`.
- **Repositório**: interface em `Domain/Interfaces/Repositories` (ou na porta de Application, conforme o serviço),
  implementação em Infrastructure.

## 4. Persistência (quando houver)

- EF Core: configuração em Infrastructure, nova migration (`dotnet ef migrations add <Nome> --project
  .../Infrastructure --startup-project .../Api`) e regeneração da seção do serviço em
  `platform/init-db/postgres/2-schema.sql` com `dotnet ef migrations script --idempotent`.
- ingestion (Dapper): altere a seção `ingestion` de `2-schema.sql` à mão, idempotente (`ADD COLUMN IF NOT EXISTS`).
- Mudança de schema segue expand-and-contract (`.claude/rules/backward-compatibility.md`).

## 5. Testes

Teste unitário de cada invariante e transição (válida e inválida) no projeto UnitTests, sem infraestrutura.
Use a skill `test-generation`.
