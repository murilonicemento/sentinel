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

## 15. Próximos Passos

1. Definir contratos de eventos com outros serviços
2. Implementar middleware de tenant resolution
3. Criar mecanismo de quota em Redis
4. Implementar camada de persistence em PostgreSQL
5. Criar endpoints administrativos para tenants e planos
6. Preparar a base para billing e monetização
