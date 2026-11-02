# Tenants & Billing Service

O **Tenants & Billing Service** é responsável por gerenciar o isolamento lógico entre clientes (tenants), controlar planos, limites e consumo, e fornecer a base para a governança operacional da plataforma Sentinel.

Seu objetivo é garantir que cada cliente tenha acesso somente ao seu próprio contexto, seja responsável por seu consumo de recursos e possa operar dentro de regras claras de quota, billing e compliance.

## 1. Gestão de Tenants

O serviço atua como a autoridade de contexto do cliente dentro da plataforma.

### Responsabilidades principais

- Criação, atualização e desativação de tenants
- Manutenção de metadados do tenant
- Definição de região, timezone e configurações operacionais
- Gestão de status do tenant
- Centralização de contexto de acesso e permissões

### Estados de tenant

- `Active`
- `Suspended`
- `Canceled`
- `Pending`

## 2. Gestão de Planos

O serviço define e aplica os planos disponíveis para cada tenant.

### Planos sugeridos

- `Free`
- `Pro`
- `Enterprise`
- `Custom`

### Características por plano

- Limite de eventos por mês
- Limite de alertas
- SLA e prioridade de suporte
- Canais disponíveis
- Regras de feature flag
- Limites de API e throughput

## 3. Limites e Quotas

O serviço centraliza a forma como o consumo do sistema é medido e controlado por tenant.

### Métricas monitoradas

- Eventos ingeridos
- Alertas disparados
- Requisições de API
- Uso de canais
- Uso de recursos por região

### Estratégias de controle

- `Soft limit` → gera alerta e notificação
- `Hard limit` → bloqueia operação ou rejeita ações

### Regras sugeridas

- O uso deve ser acumulado por período
- Mudanças de plano devem refletir imediatamente
- Tenants suspensos não podem consumir recursos críticos

## 4. Billing e Governança

O billing inicial pode ser simples, mas precisa ser preparado para evolução.

### Cenários de billing

- Pay-as-you-go
- Cobrança por uso mensal
- Cobrança por plano fixo
- cobrança por canal/evento

### Governança

- Regras por tenant
- Feature flags por cliente
- Políticas de compliance
- Auditoria das ações administrativas
- Versionamento de contratos e regras

## 5. Bounded Context

Este serviço é um **bounded context isolado** e deve operar com autonomia.

Ele NÃO deve:

- conhecer lógica de risco
- processar regras de geolocalização
- decidir disparo de alertas
- ter regras de negócio de outros contextos

Ele DEVE:

- fornecer contexto para outros serviços
- validar limites e permissões
- expor o estado de consumo do tenant
- servir como autoridade de governança para recursos e acesso

## 6. Modelo de Domínio

### Entidades principais

#### Tenant

- Id
- Name
- Status
- PlanId
- CreatedAt
- UpdatedAt
- Region
- Timezone

#### Plan

- Id
- Name
- Limits
- Features
- IsActive

#### Usage

- TenantId
- Period
- EventsConsumed
- AlertsTriggered
- ApiRequests
- UpdatedAt

#### TenantPolicy

- TenantId
- FeatureFlag
- AllowedChannels
- MaxAlertThreshold
- SoftLimitThreshold
- HardLimitThreshold

## 7. Comunicação com Outros Serviços

### Entrada

- Criação de tenant via API administrativa
- Atualização de plano
- Solicitação de limites e contexto
- Atualização de uso

### Saída

#### Eventos publicados

- `TenantCreated`
- `TenantUpdated`
- `TenantSuspended`
- `TenantReactivated`
- `PlanChanged`
- `TenantQuotaExceeded`
- `TenantQuotaBlocked`

#### Eventos consumidos (opcional)

- `AlertDispatched` → para contabilizar uso de alertas
- `SensorEventDetected` → para contabilizar ingestão
- `NotificationSent` → para billing por canal/entrega

## 8. Integrações

### 8.1 Com Ingestion Service

- Validar quota antes de aceitar eventos
- Bloquear ou alertar quando a ingestão ultrapassar regras do plano

### 8.2 Com Alert Orchestrator

- Verificar limites antes de disparar alertas
- Bloquear alertas críticos quando o tenant está suspenso ou sem permissão

### 8.3 Com Channels Service

- Resolver quais canais o tenant pode usar
- Aplicar políticas de delivery conforme plano

### 8.4 Com Reporting Service

- Fornecer contexto de uso para dashboards e relatórios
- Contribuir para análise operacional do consumo por tenant

### 8.5 Com Audit / Observability

- Registrar ações administrativas
- Expor métricas por tenant para monitoração

## 9. Persistência

| Tecnologia     | Responsabilidade                                            |
| -------------- | ----------------------------------------------------------- |
| **PostgreSQL** | Persistência estrutural de tenants, planos, políticas e uso |
| **Redis**      | Cache de limites, quotas e contexto de tenant               |
| **Kafka**      | Publicação de eventos de governança e mudanças de tenancy   |

A persistência deve ser isolada e específica para este bounded context.

## 10. Regras de Negócio

1. Todo tenant deve possuir um plano válido.
2. Tenant suspenso não pode ingerir eventos ou disparar alertas.
3. A cobrança deve ser acumulada por período configurado.
4. Mudança de plano deve refletir imediatamente em permissões e limites.
5. Uso deve ser medido e comparado com limites em tempo real.
6. Quando o limite é atingido:
   - soft limit gera alerta
   - hard limit bloqueia operação
7. Eventos de governança devem ser auditáveis.
8. Operações administrativas devem registrar quem alterou o contexto do tenant.

## 11. Segurança

- Isolamento lógico por `TenantId`
- Propagação de tenant via:
  - JWT claims
  - header `X-Tenant-Id`
  - contexto de request
- Validação obrigatória em todos os serviços downstream
- Regras de acesso devem ser aplicadas por tenant e por plano

## 12. Observabilidade

O serviço deve expor métricas e logs com tenant awareness.

### Métricas recomendadas

- uso por tenant
- limites atingidos
- bloqueios por quota
- erros de billing
- latência de consulta de contexto
- ações administrativas por tipo

### Logs e tracing

- Logs estruturados com `TenantId`
- Corrrelation IDs para ações administrativas
- Tracing distribuído com OpenTelemetry
- Health checks para readiness/liveness

## 13. Padrões Arquiteturais Aplicados

- DDD → `Tenant` como Aggregate Root
- CQRS → consultas rápidas para limites e uso
- Clean Architecture → separação de camadas
- Event-driven → publicação de mudanças e governança
- Multi-tenancy first → todo fluxo deve considerar `TenantId`

## 14. Fluxo de Alto Nível

1. Um tenant é criado
2. O plano é associado ao tenant
3. O serviço aplica limites e políticas
4. Outros serviços consultam o tenant context antes de operar
5. O uso é registrado em tempo real
6. Quando um limite é excedido:
   - soft limit dispara alerta
   - hard limit bloqueia operação
7. A mudança de plano ou suspensão é publicada como evento
8. Outros serviços ajustam automaticamente seu comportamento

## 15. Estado da implementação (verificado em 2026-11-02)

As seções anteriores descrevem o desenho. Esta descreve o que o código em `services/tenants-billing` faz hoje.

**API.** Base `/api/v1/tenants-billing`, contrato comum de `docs/compliance/services-validation.md` §4.2 e autenticação JWT (ADR-0002):

| Rota | Papel | Resultado |
|---|---|---|
| `POST /tenants` | Write | cria o tenant (`TENANT_ALREADY_EXISTS` em 409, `PLAN_NOT_FOUND` em 422) |
| `GET /tenants/{tenantId}` | Read | tenant com `status` em texto (`TENANT_NOT_FOUND` em 404); é a rota que os `TenantBillingGateway` de ingestion, alert-orchestrator e channels consultam com token de serviço |
| `POST /tenants/{tenantId}/usage` | Write | registra uso do tenant |
| `POST /plans` | Write | cria um plano |
| `GET /tenants` | Read | tenants das claims `tenant_id` da identidade (todos, para um serviço confiável), ordenados por nome e depois id; filtro opcional `status` (`Active`, `Suspended`, `Canceled`, `Pending`) e paginação `page`/`pageSize` |
| `PATCH /tenants/{tenantId}/status` | Admin | muda o status para `Active`, `Suspended` ou `Canceled` (corpo `{ "status": "Suspended" }`) e devolve o tenant |
| `GET /plans` | Read | planos ordenados por nome e depois id, paginados |

As rotas antigas `/api/v1/tenants/*` e `/api/v1/plans` estão depreciadas até 2027-01-23 (ADR-0007). A rota antiga devolve `status` numérico; os gateways aceitam os dois formatos (TB-09).

**Ciclo de vida do tenant (TB-12, 2026-11-02).** Um tenant `Active` pode ser suspenso; um `Suspended` (ou `Pending`) pode ser ativado; qualquer um pode ser cancelado. `Canceled` é terminal: sair dele é 409 `TENANT_STATUS_TRANSITION_NOT_ALLOWED`. Pedir o status atual devolve o tenant sem mudança e sem evento, então repetir a requisição não tem efeito. A gravação só vale se o status guardado ainda é o lido; se outra requisição mudou o status no meio, a resposta é 409 `TENANT_STATUS_CONFLICT` e nada é sobrescrito. A rota exige o papel `TenantsBilling.Admin` e o tenant nas claims da identidade; tokens de serviço não recebem esse papel. Um tenant suspenso ou cancelado fica em hard limit e os `TenantBillingGateway` dos outros serviços o tratam como inativo.

**Uso e quotas.** O uso se acumula por tenant e mês civil no fuso do tenant (fuso vazio ou inválido usa `America/Sao_Paulo`). Cada `SourceEventId` conta uma vez. O status de quota é o maior percentual dos quatro recursos (eventos, alertas, requisições de API, uso de canais) contra os limites do plano e os percentuais de soft e hard limit, e não regride dentro do período. Nenhum outro serviço bloqueia operações pelo status de quota: os gateways só conferem se o tenant está ativo.

**Mensageria.**

| Tópico | Papel | Conteúdo |
|---|---|---|
| `tenant-usage-events` | consumido | `TenantUsageEvent { TenantId, EventsConsumed, AlertsTriggered, ApiRequests, ChannelUsage, EventId }`. Publicado por ingestion (eventos de cada coleta), alert-orchestrator (janela disparada) e channels (canal entregue), com `EventId` determinístico (ADR-0009). Nenhum serviço reporta `ApiRequests`. Um tenant desconhecido vai ao DLQ |
| `tenant-usage-events-dlq` | publicado e consumido | falhas do consumo de uso; o consumidor do DLQ republica as recuperáveis no replay |
| `tenant-usage-events-replay` | consumido | lido pelo consumidor de uso junto com o tópico principal |
| `audit-events` (`Kafka:AuditTopic`) | publicado | envelope v1 do audit-compliance: `TenantCreated` na criação e `TenantUpdated` em cada mudança de status, com o `sub` do token como ator e o estado antes/depois, chave igual ao tenant (ADR-0011). A criação de plano não é auditada |
| `tenants-billing-events` | publicado | `TenantCreated`, `PlanCreated`, `PlanPolicyUpdated`, `TenantQuotaSoftLimitReached`, `TenantQuotaHardLimitReached`, `TenantSuspended`, `TenantReactivated` e `TenantCanceled` (`{ Id, Name, Status }`, publicados por `PATCH /tenants/{tenantId}/status`); nenhum serviço consome esse tópico ainda |

**Persistência.** PostgreSQL `tenants_billing`: `tenants`, `plans`, `tenant_usage` e `recorded_usage_events` (migrations EF Core, seção do `2-schema.sql`). Redis não é usado.

**Próximos passos.**

1. Reportar `ApiRequests` (ADR-0009 deixa em aberto).
2. Decidir se os outros serviços devem consultar o status de quota antes de operar.
