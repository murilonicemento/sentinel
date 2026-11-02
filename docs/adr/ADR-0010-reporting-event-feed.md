# ADR-0010: Alimentação do reporting (`reporting-events`)

- **Status:** Proposto
- **Data:** 2026-11-02
- **Responsável pela decisão:** a definir (responsáveis pela plataforma)
- **Relacionados:** `docs/compliance/services-validation.md` (X-24), `ADR-0008` (contratos do pipeline), `ADR-0009` (medição de uso), `docs/services/Reporting.md` §6.12

## Contexto

O reporting consome `ReportingEventEnvelope { EventId, EventType, TenantId, Region, Severity, Channel, RiskScore, Status, Timestamp, Source }` em `reporting-events`. Os dashboards contam `AlertTriggered` (alertas) e `NotificationSent` / `NotificationFailed` (entregas) e calculam o score médio. Nenhum serviço publicava nesse tópico (X-24); os dados chegavam só pela API.

O contrato exigia `Channel` em todo evento e aceitava só `sms`, `email`, `push` e `webhook`. Um alerta disparado não tem canal, e o channels também entrega por WhatsApp, MQTT e sirene.

## Decisões

### 1. Eventos e produtores (decisão de 2026-11-02)

| Evento | Produtor | Quando | Campos |
|---|---|---|---|
| `AlertTriggered` | alert-orchestrator | cada janela disparada; escalonamentos não geram outro | `Severity` `HIGH` (a do comando de alerta), `Channel` `none`, `Status` `SUCCESS`, `RiskScore` o score do disparo |
| `NotificationSent` | channels | cada canal em que a notificação foi entregue | `Status` `SUCCESS`, `Severity` a prioridade da notificação |
| `NotificationFailed` | channels | cada canal que falhou depois das tentativas, ou sem provedor | `Status` `FAILED` |

No channels, região e score vêm do `Metadata` da notificação (`region`, `riskScore`), que os comandos de alerta preenchem. Sem eles, a região é `unknown` e o score é 0.

`RiskUpdated` e `SensorEventDetected` continuam fora: o volume segue o dos sensores e dominaria o score médio.

### 2. O reporting aceita mais canais

Mudança aditiva no normalizador: `Channel` aceita também `whatsapp`, `mqtt`, `siren` e `none`. `none` é para eventos que não são entrega; uma notificação com `none` é rejeitada. Mensagens antigas continuam válidas.

### 3. Identidade e entrega

- O `EventId` é determinístico: `alert-triggered:{janela}` e `{EventType}:{tenant}:{notificação}:{canal}`. O reporting grava cada `event_id` uma vez (Postgres `ON CONFLICT DO NOTHING`, Elasticsearch pelo id), então uma reentrega não duplica.
- Publicação direta no Kafka depois de gravar, como o uso (ADR-0009). Uma falha ao publicar faz o processamento falhar e ser tentado de novo. No channels, o canal já entregue não é reenviado, só reportado de novo.

## Alternativas consideradas

- **Produtores adaptarem o canal** (canal fixo no alerta, WhatsApp/MQTT/sirene mapeados para o mais próximo). Rejeitada: distorce os relatórios por canal.
- **Publicar também `RiskUpdated` e `SensorEventDetected`.** Adiada pelo volume (decisão 1).

## Consequências

- Os dashboards passam a contar alertas e entregas reais por tenant, região e canal.
- **Perda possível no alert-orchestrator.** A janela disparada é gravada antes de publicar. Se a publicação falhar, a nova tentativa encontra a janela já disparada e não publica de novo. É o mesmo limite do comando e do uso.
- **Score fora de [0, 1].** O reporting rejeita e manda ao DLQ. O disparo por `region-intersected` usa a severidade do evento como score. Esse tópico ainda não tem produtor, e as janelas dele não têm tenant, então não geram evento.
- **Um disparo abaixo de 0,6 é rejeitado.** O reporting exige score ≥ 0,6 para `AlertTriggered`. As regras padrão do alert-orchestrator disparam a partir de 0,6.
- Contratos testados dos dois lados com o mesmo JSON: `ReportingEventContractTests` (alert-orchestrator e channels) e `ProducerContractTests` (reporting).
- **Implantação.** O reporting deve subir antes dos produtores: o reporting antigo rejeita `Channel` `none` e os canais novos.
