# ADR-0011: Auditoria das ações administrativas de tenant (`audit-events`)

- **Status:** Proposto
- **Data:** 2026-11-02
- **Responsável pela decisão:** a definir (responsáveis pela plataforma)
- **Relacionados:** `docs/compliance/services-validation.md` (X-24), `ADR-0001` (audit-compliance), `ADR-0002` (autenticação), `docs/audit-compliance-service-status.md`

## Contexto

O audit-compliance consome o envelope v1 em `audit-events` e guarda registros imutáveis. Ele aceita só os eventos do seu catálogo: `TenantCreated`, `TenantUpdated`, `PlanChanged`, `AlertTriggered`, `AlertDispatched`, `NotificationSent`, `NotificationFailed` e `RiskUpdated`. Nenhum serviço publicava nesse tópico (X-24).

## Decisões

### 1. O que é auditado (decisão de 2026-11-02)

As ações administrativas sobre tenants, no tenants-billing:

| Ação | Evento | `Action` | Estado |
|---|---|---|---|
| `POST /api/v1/tenants-billing/tenants` (e a rota antiga) | `TenantCreated` | `tenant.create` | `AfterState`: nome, plano, status, região, fuso |
| `PATCH /api/v1/tenants-billing/tenants/{tenantId}/status` | `TenantUpdated` | `tenant.status.change` | `BeforeState` / `AfterState`: status |

Pedir o status atual, ou uma transição recusada, não muda nada e não é auditado. Alertas e notificações não vão para a auditoria; já chegam ao reporting (ADR-0010).

### 2. Ator

O ator é o `sub` do token já validado: `User` para um token de usuário, `Service` para o token de um serviço confiável (ADR-0002). Fora de uma requisição, o ator é `System` / `tenants-billing`. O ator nunca vem do corpo da requisição. A API implementa a porta `ICurrentActor`.

### 3. Entrega e identidade

- Publicação direta no Kafka depois de gravar, com a chave da mensagem igual ao tenant, como já são publicados os `tenants-billing-events` (decisão de 2026-11-02).
- `EventId`: `tenant-created:{tenant}` e `tenant-status:{tenant}:{ticks do UpdatedAt}`. Cada mudança tem o seu, e o audit-compliance descarta um `(SourceService, EventId)` repetido.
- `OccurredAt` é o instante da mudança em UTC; `CorrelationId` é o trace id da requisição.

## Alternativas consideradas

- **Outbox no tenants-billing.** Rejeitada por ora: exige tabela e relay novos. É a única forma de garantir que toda mudança gravada seja auditada (ver Consequências).
- **Ampliar o catálogo** com `PlanCreated`, `RiskCatalogPublished` e reprocessamentos de DLQ. Adiada: muda o audit-compliance e outros três serviços.

## Consequências

- **Perda possível.** Se a publicação falhar depois de gravar, a requisição responde 500, a mudança fica gravada e o registro de auditoria não existe. Uma nova requisição com o mesmo status é um no-op e não audita de novo. Só um outbox elimina essa perda.
- **Criação de plano não é auditada:** o catálogo não tem `PlanCreated`.
- Contrato testado dos dois lados com o mesmo JSON: `AuditEventContractTests` (tenants-billing) e `TenantsBillingContractTests` (audit-compliance, que ingere as amostras pelo handler real).
- **Implantação.** Qualquer ordem: o audit-compliance atual já aceita os dois eventos.
