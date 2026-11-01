# ADR-0008: Contratos do pipeline de eventos (sensor → risco → alerta → entrega)

- **Status:** Proposto
- **Data:** 2026-11-01
- **Responsável pela decisão:** a definir (responsáveis pela plataforma)
- **Relacionados:** `docs/compliance/services-validation.md` (X-24, RE-18), `ADR-0003` (consumo Kafka), `ADR-0005` (identidade de região), `.claude/rules/backward-compatibility.md` (Events and Messages)

## Contexto

A revisão da Fase 9 mostrou que o pipeline não funcionava de ponta a ponta (X-24):

- o ingestion publicava `SensorEventDetected` sem o tenant;
- o risk-evaluation publicava `RiskEvaluatedEvent` em `risk-events`, sem região, tipo de risco nem tenant, e ninguém consumia esse tópico;
- o alert-orchestrator consumia `RiskUpdatedEvent` em `risk-updated`, que ninguém publicava;
- o geospatial publicava em `region-intersected` toda vez que uma chamada HTTP de `intersects` dava verdadeiro, ou seja, quando o risk-catalog validava o polígono de uma região, com um payload que o alert-orchestrator não entende;
- o alert-orchestrator publicava `TriggerAlertCommand` em `alert-commands`, e ninguém consumia; o channels lia só `notification-events`.

## Decisões

### 1. Tenant no evento do sensor

`SensorEventDetected` ganha `TenantId`, campo aditivo: os consumidores que não o conhecem o ignoram. Eventos publicados antes dele, e os que ainda estiverem em fila ou em DLQ, chegam sem tenant. Esses eventos continuam sendo avaliados, mas não geram alerta.

### 2. risk-evaluation publica `risk-updated`

Para cada avaliação vinda de um evento de sensor, o risk-evaluation publica `RiskUpdatedEvent { EventId, Region, RiskType, RiskScore, TenantId, Timestamp, Metadata }` em `risk-updated` (`Kafka:RiskUpdatedTopic`):

- `EventId` é o id do evento do sensor, determinístico por amostra; uma reentrega publica o mesmo id e o alert-orchestrator a descarta pela reserva de idempotência.
- `Region` é o `regionCode` da região da cópia do catálogo que contém o ponto (ADR-0005). Com mais de uma, vale a de maior fator; o empate é desfeito pelo código. Cópias tiradas antes de o risk-catalog expor o código usam `legacy-<id>`, o mesmo código que o risk-catalog deu às linhas antigas.
- `RiskType` é o tipo do evento em minúsculas (`flood`, `wildfire`, `earthquake`, `landslide`, `rainfall`...).
- `Metadata` leva o id e o nível da avaliação, o tipo de evento, a posição original, a intensidade e a versão do catálogo.
- A chave da mensagem é `{tenant}:{região}`, para que os sinais de uma janela de alerta fiquem na mesma partição.

Uma avaliação sem tenant, sem id de evento (chamadas HTTP de avaliação) ou fora de todas as regiões do catálogo não publica `risk-updated`: uma janela de alerta pertence a um tenant e a uma região.

`risk-events` continua sendo publicado, sem mudança, até ser depreciado (expand-and-contract). Passa a ser criado pelo `kafka-init`.

### 3. alert-orchestrator exige o tenant e conta só sinais de risco

`RiskUpdatedEvent` sem `TenantId` é inválido e vai para `alert-orchestrator-dead-letter`.

Como todo sinal vem de `risk-updated`, e o risk-evaluation não informa se a leitura veio de sensor ou de satélite, todos contam como `SENSOR`. As regras padrão deixam de exigir satélite e fontes distintas; o quorum é o número mínimo de sinais de risco na janela (2 ou 3, conforme o tipo). É uma solução provisória: quando houver uma segunda fonte de sinal com dono definido, as exigências podem voltar.

### 4. O `intersects` HTTP não publica evento

Uma interseção pedida por HTTP é um cálculo, não uma detecção. O geospatial continua indexando a operação em `geospatial-events`, mas não publica mais nada em `region-intersected`; o publisher e a opção `KafkaProducerOptions` foram removidos. O tópico, o consumidor do alert-orchestrator e o contrato `RegionIntersectedEvent` continuam existindo, sem produtor, até haver um dono para as áreas monitoradas.

### 5. channels consome `alert-commands`

O channels assina `alert-commands` (`Kafka:AlertCommandsTopic`) junto com `notification-events` e transforma cada `TriggerAlertCommand` em um `NotificationEvent`:

- `EventId` = `alert:{janela}:{nível}`: uma reentrega do mesmo comando não reenvia pelos canais que já entregaram, e cada escalonamento gera uma nova entrega;
- prioridade `High` no nível 1 e `Critical` a partir do 2;
- título e corpo do próprio comando quando vierem; senão, um texto padrão com tipo de risco, região, score e nível;
- canais do comando quando vierem (nomes desconhecidos são ignorados); senão, a configuração de canais do tenant.

O alert-orchestrator passa a preencher `TenantId` no comando. Um comando sem tenant ou sem janela vai para `channels-dead-letter`.

## Alternativas consideradas

- **alert-orchestrator consumir `risk-events`.** Rejeitada: o evento não tem região, tipo de risco nem tenant, e derivá-los no orquestrador repetiria regras do catálogo.
- **Tenant em cabeçalho Kafka.** Rejeitada: fica fora do contrato e se perde em replays e DLQs que só guardam o payload.
- **Ingestion publicar `sensor-event-detected` e o geospatial publicar `RegionIntersectedEvent`.** Adiada: o geospatial precisaria conhecer as regiões, que pertencem ao risk-catalog.
- **alert-orchestrator publicar em `notification-events`.** Rejeitada: levaria a escolha de canais e o texto das mensagens para o orquestrador.

## Consequências

- O caminho sensor → avaliação → janela de alerta → notificação passa a existir quando há um catálogo publicado com a região do ponto, o tenant ativo e canais configurados.
- Contratos novos têm teste dos dois lados com o mesmo exemplo de JSON: `RiskUpdatedContractTests` (risk-evaluation) e `EventConsumerPollingTests` (alert-orchestrator); `TriggerAlertCommandContractTests` (alert-orchestrator) e `NotificationConsumerHostedServiceTests` (channels); `RiskEvaluationEventConsumerTests` lê o evento como o outbox do ingestion o grava.
- **Rollback.** Uma versão anterior do risk-evaluation não lê as cópias de catálogo gravadas com `RegionCode` (o documento de região não ignorava campos extras; agora ignora). Voltar o serviço exige republicar o catálogo depois do rollback ou apagar as cópias novas. Os outros serviços aceitam as mensagens antigas e novas.
- **Implantação.** O alert-orchestrator deve subir antes do channels: um comando antigo, sem tenant, vai para o `channels-dead-letter` do channels novo. Ingestion e risk-evaluation podem subir em qualquer ordem: sem o tenant, a avaliação só não publica `risk-updated`, e o risk-evaluation antigo ignora o campo novo.
- Continua em aberto (X-24): `sensor-event-detected` e `region-intersected` sem produtor; `alert-orchestrator-events`, `tenants-billing-events` e `risk-events` sem consumidor; `reporting-events`, `tenant-usage-events` e `audit-events` sem produtor.
- O risk-evaluation truncava as coordenadas do cache, da avaliação gravada e de `risk-events` (RE-18, corrigido em 2026-11-01). `risk-updated` sempre levou a posição original no `Metadata`, e a região é escolhida com ela.
