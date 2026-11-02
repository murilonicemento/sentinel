---
name: create-kafka-consumer
description: Cria um consumer Kafka em um serviço do Sentinel seguindo o ADR-0003 - contrato do evento, kit Messaging/Reliability (ReliableConsumerLoop, ReliableMessageProcessor, dead-letter), validação, idempotência via claim atômica no Redis, trace W3C, métricas e testes. Use ao consumir um tópico novo ou um evento novo em tópico existente.
argument-hint: <serviço> <tópico> <Evento>
---

# Criar consumer Kafka

## 1. Contrato primeiro

1. Leia `docs/constitution.md` §10, `docs/adr/ADR-0003-message-consumption-reliability.md`, o ADR do fluxo
   (ADR-0008 a ADR-0012) e o mapa de tópicos em `.claude/rules/backward-compatibility.md`.
2. Confirme produtor, payload, chave e semântica de cada campo (unidades, UTC, `TenantId`, `EventId`). Tópico
   sem produtor é lacuna conhecida (X-24), não autorização para inventar o contrato.
3. O DTO do evento fica em Application (`Events/`), com nomes e tipos do produtor (PascalCase, como os demais).
   O mesmo JSON de exemplo é testado nos dois lados (produtor e consumer).
4. Tópico novo: adicione ao `kafka-init` de `platform/docker-compose.yml` e documente produtor/consumidor. Decisão
   de contrato significativa → ADR (com o subagent `architecture-reviewer`).

## 2. Implementação (copie do serviço que já consome)

Use como modelo `services/alert-orchestrator/src/AlertOrchestrator.Infrastructure/Messaging/` e
`ServiceCollectionExtensions.cs`. Se o serviço ainda não consome nada, copie `Messaging/Reliability/` inteiro e
`Messaging/KafkaTracing`, ajustando namespaces e o `TransientFailureClassifier` às exceções do serviço.

- Consumer derivado do `KafkaEventConsumer<TEvent>` do serviço (ou do loop equivalente), com `IsValid` que
  rejeita eventos sem os campos obrigatórios (ex.: `EventId` vazio, `TenantId` ausente). Inválido = falha
  permanente → dead-letter, sem retry.
- Config com `ReliableConsumerLoop.CreateConfig(bootstrap, groupId)`. Nunca `EnableAutoCommit = true`; offset só
  é confirmado após processar ou enviar ao dead-letter; nunca commit em `catch`.
- Falha transitória: no máximo 3 tentativas com backoff; depois seek de volta e pausa (comportamento do kit).
- Dead-letter: `<serviço>-dead-letter` (ou o tópico já existente do serviço) com `DeadLetterEnvelope`.
- Idempotência: claim atômica `SET NX` com lease no Redis, liberada em falha, chave versionada e com prefixo do
  serviço (ex.: `alert-orchestrator:idempotency:v2:{eventId}`); a lógica fica no handler de Application via porta.
- Desserialização tolerante a campos novos (mensagens antigas e novas convivem na fila); defina o comportamento
  para mensagens antigas sem o campo novo.
- Trace: extraia o contexto W3C dos headers com `KafkaTracing`; logs estruturados com tópico, partição, offset e
  `EventId`, sem payload completo.
- Métricas: `MessagingMetrics` (`sentinel_messages_handled_total{topic,outcome}`); não use tenant como label.
- Registro: `AddHostedService` em `Add<Serviço>Infrastructure`, tópico e group id por configuração obrigatória
  (sem fallback `localhost`), valores locais em `appsettings.json`, compose sobrescreve.

## 3. Testes

- Unitários: `IsValid` (válido, cada campo obrigatório ausente), handler (efeito correto, evento duplicado sem
  efeito, falha transitória vs permanente), desserialização do JSON de exemplo do contrato e de uma mensagem antiga.
- Integração (Testcontainers Kafka/Redis, padrão de `tests/*/IntegrationTests`): processamento ponta a ponta,
  dead-letter de mensagem inválida, offset não confirmado em falha transitória.
- Tenant: evento de um tenant não altera dados de outro.

Use a skill `test-generation`.
