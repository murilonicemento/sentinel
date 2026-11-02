# ADR-0009: Medição de uso por tenant (`tenant-usage-events`)

- **Status:** Proposto
- **Data:** 2026-11-02
- **Responsável pela decisão:** a definir (responsáveis pela plataforma)
- **Relacionados:** `docs/compliance/services-validation.md` (X-24), `ADR-0003` (consumo Kafka), `ADR-0008` (contratos do pipeline), `docs/services/Tenants-Billing.md` §15

## Contexto

O tenants-billing acumula o uso de cada tenant por mês e compara com os limites do plano (eventos, alertas, requisições de API e uso de canais). Ele consome `tenant-usage-events`, mas nenhum serviço publicava nesse tópico (X-24): as quotas só mudavam por `POST /tenants/{tenantId}/usage`.

O consumidor já conta cada `EventId` uma vez (`recorded_usage_events`) e manda ao DLQ um uso de tenant desconhecido. O payload é `TenantUsageEvent { TenantId, EventsConsumed, AlertsTriggered, ApiRequests, ChannelUsage, EventId }` em PascalCase.

## Decisões

### 1. Quem reporta cada recurso

| Recurso | Produtor | Quando | Quantidade |
|---|---|---|---|
| `EventsConsumed` | ingestion | cada coleta (HTTP ou polling) que publica eventos | número de eventos publicados pela coleta |
| `AlertsTriggered` | alert-orchestrator | cada janela de alerta disparada (`TryTrigger`) | 1; escaladas não contam de novo |
| `ChannelUsage` | channels | cada canal em que uma notificação foi entregue | 1 por canal |
| `ApiRequests` | ninguém | — | continua só pela rota de uso |

### 2. Entrega

- **ingestion:** uma linha do outbox transacional por coleta, gravada na mesma transação dos eventos. O uso é publicado se, e somente se, os eventos forem.
- **alert-orchestrator e channels:** publicação direta no Kafka (`Acks.All`, idempotente) depois de gravar, como já fazem com `alert-commands` e as notificações. Esses serviços não têm outbox (decisão de 2026-11-02).

### 3. Identidade de cada uso

O `EventId` é determinístico (UUID v8 do SHA-256), então republicar o mesmo uso não conta duas vezes:

- ingestion: chave da coleta + `/usage`;
- alert-orchestrator: `alert-triggered:{janela}`;
- channels: `channel-delivered:{tenant}:{notificação}:{canal}`.

Uso sem tenant em forma de GUID não é publicado, e o serviço registra um aviso.

## Alternativas consideradas

- **Outbox em todos os produtores.** Rejeitada por ora: exige tabela e relay novos no alert-orchestrator e no channels. Reavaliar se a perda descrita abaixo aparecer.
- **Contar alertas pelo channels (comandos recebidos).** Rejeitada: uma escalada gera um novo comando e contaria o mesmo alerta de novo.
- **Contar requisições de API no api-gateway.** Adiada: o gateway não conhece o tenant de cada requisição sem validar o token, e o volume pede agregação antes de publicar.

## Consequências

- As quotas passam a refletir o uso real de eventos, alertas e canais. `TenantQuotaSoftLimitReached` / `TenantQuotaHardLimitReached` podem ser publicados sem chamada manual.
- **Perda possível no alert-orchestrator.** A janela disparada é gravada antes de publicar o comando e o uso. Se a publicação falhar, a nova tentativa encontra a janela já disparada e não publica de novo. O comando tinha esse limite antes desta decisão.
- **channels.** Uma falha ao reportar o uso faz a entrega falhar e a notificação ser tentada de novo. O canal já entregue não é reenviado, só reportado de novo com o mesmo `EventId`.
- **Tenant desconhecido no tenants-billing.** O uso vai ao `tenant-usage-events-dlq` e não é reprocessado sozinho. Isso acontece com um tenant que existe no ingestion (tabela local `tenant`) mas não no tenants-billing.
- Contratos testados dos dois lados com o mesmo JSON: `TenantUsageContractTests` em ingestion, alert-orchestrator e channels, e o `TenantUsageContractTests` do tenants-billing, que lê as três amostras.
- **Implantação.** Qualquer ordem: o tenants-billing atual já lê o payload. Antes desta mudança, o tópico estava vazio.
