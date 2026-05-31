# Channels Service — Especificação Técnica

## Objetivo
O `Channels Service` é um microserviço de orquestração de entrega de notificações multi-canal.
Ele é responsável por receber eventos de notificação, resolver a configuração de tenant, selecionar provedores e garantir entrega resiliente.

## Responsabilidades
- Receber notificações via API e via evento Kafka.
- Resolver configuração tenant-aware para canais habilitados, prioridade e fallback.
- Executar envio em múltiplos canais através de provedores abstratos.
- Aplicar políticas de retry, fallback e dead-letter quando necessário.
- Persistir tentativas de entrega e status de cada canal.
- Emitir logs estruturados e métricas para observabilidade.

## Contratos
### API
`POST /api/v1/notifications`
- Payload: `NotificationEvent`
- Resposta:
  - `200 OK` quando qualquer canal for entregue com sucesso
  - `500 Internal Server Error` quando todas as tentativas falharem

### Evento Kafka
- Tópico padrão: `notification-events`
- Payload JSON compatível com `NotificationEvent`

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
- status
- attemptCount
- errorMessage
- timestamp

### DeliveryResult
- success
- error
- providerName

## Comportamento de entrega
1. Receber `NotificationEvent`.
2. Resolver configurações do tenant com `ITenantChannelSettingsProvider`.
3. Determinar canais candidatos:
   - se `notification.Channels` estiver preenchido, usa essa lista filtrada pelos canais habilitados;
   - caso contrário, usa canais habilitados do tenant;
   - se `FallbackOrder` existir, aplica essa ordem;
   - caso contrário, aplica `PriorityOrder`.
4. Para cada canal no fluxo:
   - buscar `IChannelProvider` apropriado;
   - enviar com política de retry;
   - persistir cada tentativa;
   - encerrar no primeiro envio bem-sucedido (ou continuar após falha se fallback habilitado).
5. Retornar sucesso se algum canal for entregue.
6. Se todas as tentativas falharem, registrar erro final e retornar falha.

## Resiliência
- Retry com backoff exponencial e jitter.
- Circuit breaker ou timeout podem ser adicionados posteriormente.
- DLQ para eventos que não obtiverem entrega após todas as tentativas.

## Observabilidade
- Métricas e tracing via OpenTelemetry.
- Logs estruturados com Serilog.
- Health check em `/health`.

## Configuração
- `Kafka:BootstrapServers`
- `Kafka:Topic`
- `Kafka:ConsumerGroupId`
- `ChannelsService:DefaultTenantSettings`
- `ChannelsService:Tenants:{tenantId}`

## Implementação atual e próximos passos
- Implementado fluxo de entrega básico, providers simulados e consumidor Kafka.
- Ainda a evoluir:
  - persistência real em PostgreSQL;
  - cache de tenant config;
  - provedores reais (Twilio, SendGrid, WhatsApp Business, MQTT);
  - DLQ explícito;
  - políticas de circuit breaker e timeout.
