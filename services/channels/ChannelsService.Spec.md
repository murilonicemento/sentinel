# Channels Service — Especificação Técnica

## Objetivo

O `Channels Service` é um microserviço de orquestração de entrega de notificações multi-canal.
Ele recebe eventos de notificação e garante entrega resiliente através de provedores abstraídos.

## Responsabilidades

- Receber notificações via API e via Kafka.
- Resolver configuração tenant-aware para canais habilitados, prioridade e fallback.
- Executar envio em múltiplos canais com retry e fallback.
- Persistir tentativas de entrega em PostgreSQL.
- Publicar eventos não entregues em DLQ.
- Expor consulta de tentativas por evento.
- Registrar logs e fornecer observabilidade.

## Fluxo

1. Receber `NotificationEvent`.
2. Buscar configurações do tenant.
3. Determinar canais candidatos.
4. Enviar para cada canal com retry.
5. Persistir cada tentativa de entrega.
6. Em caso de falha total, publicar no DLQ.

## Persistência

- `delivery_attempts`
- Chave primária: `attempt_id`
- Campos de auditoria e métrica por canal e provider

## Observabilidade

- Logs estruturados com Serilog.
- Traces e métricas via OpenTelemetry.
- Health check em `/health`.

## Configuração

- `Kafka:BootstrapServers`
- `Kafka:Topic`
- `Kafka:DeadLetterTopic`
- `ConnectionStrings:ChannelsServiceDatabase`
- `ChannelsService:DefaultTenantSettings`
- `ChannelsService:Tenants:{tenantId}`

## Evolução prevista

- Providers reais (SendGrid, Twilio, WhatsApp Business, MQTT).
- Cache de tenant config em Redis.
- Circuit breaker e timeout por provider.
- Retry e DLQ com painel de operação.
