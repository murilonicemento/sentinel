# Channels Service — Especificação Técnica

## Objetivo

O `Channels Service` é um microserviço de orquestração de entrega de notificações multi-canal.
Ele recebe eventos de notificação, resolve a configuração do tenant, seleciona provedores e garante entrega resiliente.

## Responsabilidades

- Receber notificações via API e via evento Kafka.
- Resolver configuração tenant-aware para canais habilitados, prioridade e fallback.
- Executar envio em múltiplos canais através de provedores abstratos.
- Aplicar políticas de retry, fallback e dead-letter.
- Persistir tentativas de entrega e status de cada canal.
- Emitir logs estruturados e métricas para observabilidade.

## Contratos

### API

Base `/api/v1/channels`. Envelope `ResponseBaseDTO` no sucesso e Problem Details nos erros, conforme `docs/compliance/services-validation.md` §4.2. Autenticação JWT (ADR-0002), com papéis `Channels.Read`, `Channels.Write` e `Channels.Admin`. O tenant da notificação precisa estar nas claims `tenant_id`; se não estiver, a resposta é 403 `TENANT_ACCESS_DENIED`.

| Método e rota | Papel | Respostas |
|---|---|---|
| `POST /notifications` | Write | `200` com o resultado da entrega quando ao menos um canal entrega; `400`; `403`; `502` `NOTIFICATION_DELIVERY_FAILED` quando nenhum canal entrega (a notificação vai para o DLQ) |
| `GET /notifications/{eventId}/attempts` | Read | `200` com as tentativas paginadas (`page`/`pageSize`), restritas aos tenants da identidade; `400` |
| `POST /dead-letters/reprocess` | Write | `202`: republica a notificação em `notification-events`; `400`; `403` |

As rotas antigas `/api/notifications` e `/api/dlq/reprocess` estão depreciadas até 2027-01-23 (ADR-0007).

### Eventos Kafka

| Tópico | Papel | Conteúdo |
|---|---|---|
| `notification-events` | consumido (grupo `channels-service-group`); também publicado pelo reprocessamento | `NotificationEvent` em JSON |
| `alert-commands` (`Kafka:AlertCommandsTopic`) | consumido | `TriggerAlertCommand` do alert-orchestrator, transformado em `NotificationEvent` (ADR-0008) |
| `notification-events-dlq` | publicado | notificação que nenhum canal entregou, com o erro |
| `channels-dead-letter` | publicado | mensagem que o consumidor não conseguiu processar (ADR-0003) |
| `tenant-usage-events` (`Kafka:TenantUsageTopic`) | publicado | `TenantUsageEvent` com `ChannelUsage: 1` por canal entregue (ADR-0009) |
| `reporting-events` (`Kafka:ReportingTopic`) | publicado | `NotificationSent` por canal entregue e `NotificationFailed` por canal que falhou, com região e score do `Metadata` da notificação (ADR-0010) |

Cada `TriggerAlertCommand` vira uma notificação com `EventId` `alert:{janela}:{nível}`: uma reentrega do mesmo comando não reenvia pelos canais que já entregaram, e cada escalonamento é entregue de novo. A prioridade é `High` no nível 1 e `Critical` a partir do 2. Título e corpo vêm do comando ou de um texto padrão com tipo de risco, região, score e nível; os canais vêm do comando ou, sem eles, da configuração do tenant. Comando sem tenant ou sem janela vai para `channels-dead-letter`.

Cada canal entregue gera um uso com `EventId` derivado do tenant, da notificação e do canal. Numa reentrega, o canal já entregue não é reenviado, mas o uso é reportado de novo com o mesmo id, e o tenants-billing o conta uma vez. Uma falha ao reportar faz a entrega falhar, para que a notificação seja tentada de novo. Notificação sem tenant em forma de GUID não gera uso. O resultado de cada canal vai também ao reporting, com o mesmo cuidado: o id deriva do tipo de evento, tenant, notificação e canal, e uma falha ao publicar faz a entrega ser tentada de novo.

## Modelo de domínio

### NotificationEvent

- eventId
- tenantId
- userId
- eventType
- correlationId
- priority
- message
- channels
- fallbackEnabled
- metadata

### DeliveryAttempt

- attemptId
- eventId
- tenantId
- channel
- provider
- status (`Sent`, `Retried`, `Failed`)
- attemptCount
- errorMessage
- timestamp

### DeliveryResult

- success
- error
- providerName

## Comportamento de entrega

1. Receber `NotificationEvent`.
2. Confirmar no tenants-billing que o tenant está ativo (`GET /api/v1/tenants-billing/tenants/{id}`). Tenant inativo ou desconhecido não recebe envio e conta como falha de entrega.
3. Resolver as configurações do tenant (`ITenantChannelSettingsProvider`).
4. Determinar os canais candidatos:
   - se `notification.Channels` estiver preenchido, usa essa lista filtrada pelos canais habilitados;
   - caso contrário, usa os canais habilitados do tenant;
   - se o tenant tiver `FallbackOrder` e a notificação tiver `fallbackEnabled`, usa a ordem de `FallbackOrder`, e um candidato que não esteja nela fica de fora;
   - caso contrário, ordena por `PriorityOrder`.
5. Para cada canal, na ordem:
   - pular o canal que já entregou esta notificação (uma reentrega não reenvia um SMS já enviado);
   - enviar pelo provedor do canal, com retry (`MaxRetries` é o total de tentativas do canal);
   - persistir cada tentativa.

   Todos os canais candidatos são tentados; a entrega não para no primeiro sucesso.
6. A entrega é bem-sucedida quando ao menos um canal entrega. Se nenhum entregar, a notificação vai para `notification-events-dlq`.

## Provedores

| Canal | Provedor | Configuração (`ChannelsService:Providers:<Canal>`) |
|---|---|---|
| Email | SendGrid | `Options:ApiKey`, `FromEmail`, `FromName` |
| Sms | Twilio | `Options:AccountSid`, `AuthToken`, `FromNumber` |
| WhatsApp | Twilio WhatsApp | `Options:AccountSid`, `AuthToken`, `FromNumber` |
| Push | HTTP | `Options:Endpoint`, `ApiKey`, `DefaultTarget` |
| Siren | HTTP | `Options:Endpoint`, `ApiKey`, `DefaultDeviceId` |
| MQTT | broker MQTT | `Options:Broker`, `Port`, `Topic`, `ClientId` |

Cada provedor tem `Resilience` própria: `TimeoutSeconds` e circuit breaker (`CircuitBreakerFailureThreshold`, `CircuitBreakerDurationSeconds`, `CircuitBreakerSamplingDurationSeconds`). As credenciais vêm de `platform/.env` no compose ou de User Secrets, nunca do `appsettings.json`.

## Resiliência

- Retry com backoff exponencial e jitter, limitado a 3 tentativas por canal.
- Timeout e circuit breaker por provedor.
- DLQ (`notification-events-dlq`) para notificações que nenhum canal entregou, com reprocessamento pela API.
- O consumidor segue o ADR-0003: commit só depois de tratar ou enviar ao `channels-dead-letter`.

## Persistência

- PostgreSQL, banco `channels`, tabela `delivery_attempts` (migrations EF Core; seção `channels` do `platform/init-db/postgres/2-schema.sql`).
- Redis: configuração de canais por tenant em `ChannelsService:TenantSettings:{tenantId}`, com TTL de `Redis:TenantSettingsCacheTtlSeconds` (padrão 300 s). Sem entrada no cache, vale `ChannelsService:Tenants:{tenantId}` e, na falta dela, `ChannelsService:DefaultTenantSettings`; o valor resolvido é guardado no cache. Com o Redis indisponível, a configuração é lida direto dos arquivos.

## Observabilidade

- Serilog, OpenTelemetry e `/metrics` conforme o ADR-0004; health checks em `/health/live`, `/health/ready` e `/health`.
- Métricas de tentativa e de latência por canal e provedor, mais as do kit de consumo (`sentinel_messages_handled_total`).
- Dashboard em `platform/observability/grafana/provisioning/dashboards/channels-service`, e o alerta "Falhas de entrega de notificações" (ADR-0006).

## Configuração

- `Kafka:BootstrapServers`, `Kafka:ConsumerGroupId`, `Kafka:Topic`, `Kafka:AlertCommandsTopic`, `Kafka:DeadLetterTopic`, `Kafka:ConsumerDeadLetterTopic`
- `ConnectionStrings:ChannelsServiceDatabase`
- `Redis:Configuration`, `Redis:TenantSettingsCacheTtlSeconds`
- `ChannelsService:DefaultTenantSettings`, `ChannelsService:Tenants:{tenantId}`, `ChannelsService:Providers:*`
- `Jwt:*`, `ServiceToken:*` (papel `TenantsBilling.Read`) e `Services:TenantsBillingBaseUrl`

Os valores locais estão em `ChannelsService.Api/appsettings.json`, e o compose os sobrescreve.

## Próximos passos

- Revisar se um canal habilitado fora de `FallbackOrder` deve mesmo ficar de fora quando o fallback está ligado.
