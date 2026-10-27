# ADR-0003: Consumo confiável de mensagens Kafka

- **Status:** Proposto
- **Data:** 2026-10-26
- **Responsável pela decisão:** a definir (responsáveis pela plataforma)
- **Relacionados:** `docs/compliance/services-validation.md` (achados RE-01, AO-01, AO-04, CH-01, CH-04, TB-07, ING-07, ING-13, GEO-12), `ADR-0001` (consumidor do audit-compliance, modelo adotado aqui)

## Contexto

A auditoria encontrou perda silenciosa de mensagens em quatro consumidores e problemas de vazão e duplicidade nos demais:

- **Commit automático ou fora de hora:** risk-evaluation e channels usavam `EnableAutoCommit = true`; alert-orchestrator e tenants-billing não confirmavam a mensagem com falha, mas seguiam o laço, e o commit seguinte avançava além dela.
- **Falha ao publicar no DLQ:** geospatial, reporting e o consumidor de DLQ do tenants-billing engoliam a exceção e confirmavam o offset.
- **Vazão:** risk-evaluation lia uma mensagem a cada 30 s; geospatial esperava 30 s depois de cada mensagem.
- **Duplicidade:** a idempotência do alert-orchestrator e a deduplicação do ingestion faziam "verificar e depois gravar" em duas operações Redis; channels reenviava SMS e e-mail ao reprocessar uma notificação.

As regras do projeto (`.claude/rules/concurrency.md`, `error-handling.md`) exigem no máximo 3 tentativas com backoff e jitter, retentativa só para falhas transitórias, nenhum commit antes do tratamento durável e consumidores seguros contra entrega duplicada.

## Decisões

### 1. Entrega pelo menos uma vez com o modelo do audit-compliance

Cada serviço consumidor tem uma cópia do kit `Infrastructure/Messaging/Reliability` (não há biblioteca compartilhada; ver `CLAUDE.md`):

- `ReliableConsumerLoop`: `EnableAutoCommit = false`, `EnableAutoOffsetStore = false`; confirma o offset só depois de um resultado durável. Sem resultado durável, volta ao mesmo offset (`Seek`) e pausa, sem pular mensagens.
- `ReliableMessageProcessor`: até 3 tentativas (incluindo a primeira) com backoff exponencial e jitter para falhas transitórias. Mensagens rejeitadas (`INVALID_PAYLOAD`) e falhas não transitórias (`UNEXPECTED_PROCESSING_ERROR`, sem a mensagem da exceção) vão para o DLQ. Se o DLQ falha, a mensagem fica sem commit.
- `TransientFailureClassifier`: escrito por serviço, conforme as dependências (timeouts, rede, Kafka não fatal, `DbException.IsTransient`, Mongo, Redis, HTTP 5xx/408/429, Elasticsearch indisponível, concorrência otimista).

O consumidor de DLQ do tenants-billing não tem DLQ próprio: uma falha não transitória ao agendar o replay pausa o consumo até intervenção.

### 2. Um tópico de DLQ por serviço consumidor

Os serviços sem DLQ de consumo ganharam `alert-orchestrator-dead-letter`, `risk-evaluation-dead-letter` e `channels-dead-letter`, no formato `DeadLetterEnvelope` do audit-compliance (`OriginalTopic`, `Partition`, `Offset`, `Key`, `Payload`, `ErrorCode`, `ErrorReason`, `FailedAt`, `ConsumerGroup`). `notification-events-dlq` continua só com falhas de entrega. Os tópicos com formato próprio mantêm o formato e recebem campos aditivos: `geospatial-dead-letter` (+ `ErrorCode`, `ErrorReason`) e `tenant-usage-events-dlq` (formato lido pelo consumidor de DLQ, + `errorCode`, `failedAt`, `consumerGroup`).

### 3. Idempotência por reivindicação atômica

Quem consome eventos com efeito colateral reivindica a mensagem com `SET NX` e um lease curto (`processing`), marca como concluída (`done`, com retenção) e libera a reivindicação em caso de falha. Outra instância com a reivindicação ativa faz a mensagem ser tratada como falha transitória e relida depois.

| Serviço | Chave | Observação |
|---|---|---|
| alert-orchestrator | `alert-orchestrator:idempotency:v2:{eventId}` | lê também a chave antiga `idempotency:{eventId}` até expirar (24 h) |
| risk-evaluation | `risk-evaluation:consumed:v1:{eventId ou tópico:partição:offset}` | catálogo publicado é upsert por versão, sem reivindicação |
| ingestion | `ing:{tenant}:{dataSource}:{yyyyMMddHHmmss}` (inalterada) | marcador concluído mantém o valor `"1"`, lido por versões antigas com `EXISTS` |

channels usa os registros de `delivery_attempts`: canais já entregues para o mesmo `EventId` e tenant não são reenviados. tenants-billing não acumula uso (ver TB-10), então repassa o identificador da origem (`SourceEventId`) nos eventos de cota para que os consumidores descartem duplicatas.

### 4. `EventId` aditivo e determinístico nos eventos de sensor

`SensorEventDetected` (ingestion) passa a ter `EventId`, igual ao da projeção Mongo e derivado da chave da coleta e do índice da amostra (GUID versão 8). Uma coleta retentada republica os mesmos ids; a projeção ignora a chave duplicada e risk-evaluation descarta o evento repetido. `TenantUsageEvent` ganha `EventId` opcional. Consumidores sem o campo continuam funcionando (campo aditivo).

## Consequências

- Uma dependência fora do ar pausa a partição em vez de perder mensagens; a recuperação é automática quando ela volta.
- Mensagens irrecuperáveis ficam visíveis no DLQ de cada serviço; o acesso aos tópicos de DLQ deve ser restrito a operadores, porque carregam o payload original.
- Entregas por SMS continuam podendo duplicar se o processo cair entre o envio ao provedor e o registro em `delivery_attempts`; o risco foi reduzido, não eliminado.
- As cópias do kit precisam ser mantidas em sincronia até existir uma biblioteca compartilhada.

## Decisões ainda pendentes

- Contrato de `risk-catalog-published` (RE-13): o produtor publica `Version` numérico e `Notes`; o consumidor espera `Version` texto, `Parameters` e `Formula`. Hoje todas as publicações vão para `risk-evaluation-dead-letter`.
- Procedimento de replay para os novos DLQs (somente tenants-billing e reporting têm replay automatizado).
