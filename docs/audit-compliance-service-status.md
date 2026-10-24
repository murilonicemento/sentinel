# Audit & Compliance Service — Estado da Implementação

Serviço: `services/audit-compliance` (projetos `AuditCompliance.*`, registrados na `Sentinel.sln`).
Especificação: `docs/services/Compliance-Audit.md`. Decisões: `docs/adr/ADR-0001-audit-compliance-service.md`.

## Implementado

| Área | Situação |
| --- | --- |
| Domínio | `AuditRecord`, imutável, com invariantes de catálogo, versão, origem, timestamps e limites. `AuditExport` com transições de estado e expiração. |
| Ingestão | Consumidor Kafka de `audit-events` e `audit-events-replay`. Confirma o offset só após a persistência ou após a publicação confirmada no dead-letter. |
| Idempotência | Índice único `(SourceService, EventId)`, verificado com entregas concorrentes e duplicadas. |
| Falhas | Até 3 tentativas com backoff exponencial e jitter. Se o armazenamento continuar indisponível, o consumo pausa sem confirmar a mensagem. Mensagens inválidas vão para `audit-events-dlq`. |
| Minimização | Mascara chaves sensíveis e aplica limites de estrutura e tamanho em `metadata`, `beforeState` e `afterState`. |
| Integridade | Hash SHA-256 por registro, verificado na leitura (`integrityVerified`). Papel MongoDB sem `update`/`remove` em `auditRecords`. |
| API | Consulta paginada e filtrada, detalhe por id, exportações assíncronas CSV/JSON com download auditado. |
| Segurança | JWT Bearer, papéis `Audit.*`, validação de `X-Tenant-Id` contra as claims `tenant_id` e rate limiting por identidade (`429` com `Retry-After`). |
| Erros | RFC 9457 Problem Details com as extensões `code` e `traceId`. |
| Observabilidade | Serilog (console e Loki), métricas OpenTelemetry em `/metrics`, tracing com propagação de `traceparent` vindo do Kafka, health checks `/health/live`, `/health/ready` e `/health`. |
| Plataforma | Serviço no `docker-compose` (porta 7007), tópicos no `kafka-init`, usuário, papel e índices em `platform/init-db/mongodb`, job no Prometheus e rotas no `api-gateway`. |

## Não implementado / pendente

- **Nenhum serviço de origem publica eventos de auditoria ainda.** Cada serviço precisa publicar o envelope v1 em `audit-events`, de preferência via Transactional Outbox.
- Retenção, arquivamento e eliminação de registros (aguardam definição de política).
- Encadeamento de hashes, assinaturas ou armazenamento imutável contra adulteração privilegiada.
- Autenticação dos produtores no Kafka (SASL/ACL).
- Dashboards Grafana e regras de alerta para as métricas `audit_*`.
- Exemplos de requisição e resposta no OpenAPI. Os status codes estão documentados via `ProducesResponseType`, mas sem exemplos.
- Exportação de traces: as fontes estão registradas, mas não há exporter OTLP configurado (mesma situação dos demais serviços).
- Testes de carga e de compatibilidade de contrato automatizados.

## Contrato do evento (v1)

Publicar em `audit-events`, com a chave da mensagem recomendada igual ao `tenantId`:

```json
{
  "eventId": "8f0c5d1e-1b7a-4c8e-9a51-2f4a5c7e9b10",
  "eventType": "PlanChanged",
  "eventVersion": 1,
  "sourceService": "tenants-billing",
  "occurredAt": "2026-10-24T12:00:00Z",
  "tenantId": "00000000-0000-0000-0000-000000000001",
  "correlationId": "4bf92f3577b34da6a3ce929d0e0e4736",
  "causationId": "cmd-123",
  "action": "plan.change",
  "actor": { "type": "User", "id": "user-42" },
  "resource": { "type": "Tenant", "id": "00000000-0000-0000-0000-000000000001" },
  "metadata": { "fromPlan": "basic", "toPlan": "pro" },
  "beforeState": { "planId": "basic" },
  "afterState": { "planId": "pro" }
}
```

- Os campos `eventId`, `eventType`, `eventVersion`, `sourceService` e `occurredAt` são obrigatórios. `sourceService` deve estar em kebab-case.
- `eventType` precisa ser um dos tipos do catálogo: `TenantCreated`, `TenantUpdated`, `PlanChanged`, `AlertTriggered`, `AlertDispatched`, `NotificationSent`, `NotificationFailed` ou `RiskUpdated`. `AuditExportRequested` e `AuditExportDownloaded` são emitidos pelo próprio serviço.
- `actor.type` aceita `User`, `Service` ou `System`.
- `metadata`, `beforeState` e `afterState` precisam ser objetos JSON.
- Um cabeçalho `traceparent` (W3C) na mensagem é usado como pai do span de ingestão.

Códigos de rejeição enviados ao dead-letter: `MALFORMED_MESSAGE`, `MISSING_REQUIRED_FIELD`, `INVALID_EVENT_ID`, `UNKNOWN_EVENT_TYPE`, `UNSUPPORTED_EVENT_VERSION`, `INVALID_SOURCE_SERVICE`, `INVALID_TIMESTAMP`, `INVALID_ACTOR`, `INVALID_RESOURCE`, `INVALID_FIELD`, `PAYLOAD_LIMIT_EXCEEDED` e `UNEXPECTED_PROCESSING_ERROR`.

## API

Base: `/api/v1/audit-compliance`. Todas as rotas exigem `Authorization: Bearer <jwt>` e `X-Tenant-Id`.

| Método | Rota | Papel | Respostas |
| --- | --- | --- | --- |
| GET | `/records` | `Audit.Read` | 200, 400, 401, 403, 429 |
| GET | `/records/{id}` | `Audit.Read` (estados exigem também `Audit.Sensitive`) | 200, 400, 401, 403, 404, 429 |
| POST | `/exports` | `Audit.Export` | 202 (`Location`), 400, 401, 403, 422, 429 |
| GET | `/exports/{id}` | `Audit.Export` | 200, 400, 401, 403, 404, 429 |
| GET | `/exports/{id}/download` | `Audit.Export` | 200 (arquivo), 400, 401, 403, 404, 409, 410, 429 |

Regras de consulta e exportação:

- `from` (inclusivo) e `to` (exclusivo) são obrigatórios, no formato ISO 8601 com offset. O intervalo máximo é de 90 dias para consulta e 366 dias para exportação.
- Paginação com `page` (a partir de 1, padrão 1) e `pageSize` (padrão 10, máximo 50). Parâmetros inválidos são rejeitados com `400`.
- Ordenação com `sortBy` (`occurredAt`, que é o padrão, ou `recordedAt`) e `sortDirection` (`desc`, que é o padrão, ou `asc`), usando o `Id` como desempate.
- Filtros disponíveis: `eventType`, `actorId`, `sourceService`, `resourceType`, `resourceId` e `correlationId`.
- Exportação: o corpo é `{ "format": "csv" | "json", "from", "to", ...filtros }`.

Códigos de erro (`code`): `VALIDATION_ERROR`, `AUTHENTICATION_REQUIRED`, `ACCESS_DENIED`, `TENANT_CONTEXT_REQUIRED`, `INVALID_TENANT_CONTEXT`, `TENANT_ACCESS_DENIED`, `RESOURCE_NOT_FOUND`, `EXPORT_NOT_READY`, `EXPORT_FAILED`, `EXPORT_EXPIRED`, `EXPORT_LIMIT_EXCEEDED`, `RATE_LIMIT_EXCEEDED` e `INTERNAL_ERROR`.

## Configuração

| Chave | Obrigatória | Padrão | Observação |
| --- | --- | --- | --- |
| `MongoDb:ConnectionString` | sim | — | Segredo. Use User Secrets ou variável de ambiente. |
| `MongoDb:DatabaseName` | não | `AuditComplianceDatabase` | |
| `MongoDb:EnsureIndexesOnStartup` | não | `false` (`true` em Development) | Em produção, use o script de `init-db`. |
| `MongoDb:ServerSelectionTimeoutSeconds` / `SocketTimeoutSeconds` | não | 5 / 30 | |
| `Jwt:Issuer` / `Jwt:Audience` | sim | `sentinel` | |
| `Jwt:SigningKey` | sim | — | Segredo com no mínimo 32 caracteres. A aplicação não inicia sem ele. |
| `Kafka:BootstrapServers` | sim | `localhost:9093` | |
| `Kafka:ConsumerEnabled` | não | `true` | |
| `Kafka:ConsumerGroupId` | não | `audit-compliance-group` | |
| `Kafka:AuditEventsTopic` / `ReplayTopic` / `DeadLetterTopic` | não | `audit-events` / `audit-events-replay` / `audit-events-dlq` | |
| `Kafka:MaxProcessingAttempts` | não | 3 | Aceita de 1 a 3. |
| `Kafka:InitialRetryDelayMilliseconds` | não | 500 | |
| `Kafka:StorageUnavailablePauseSeconds` | não | 30 | |
| `AuditQuery:MaxQueryRangeDays` / `MaxExportRangeDays` | não | 90 / 366 | |
| `Exports:WorkerEnabled` | não | `true` | |
| `Exports:MaxRecords` | não | 10000 | |
| `Exports:FileExpirationHours` | não | 24 | |
| `Exports:ProcessingLeaseMinutes` | não | 10 | |
| `Exports:MaxProcessingAttempts` | não | 3 | |
| `Exports:PollIntervalSeconds` | não | 10 | |
| `RateLimiting:QueriesPerMinute` / `ExportsPerMinute` | não | 120 / 10 | Por identidade (`sub`). |

As opções são validadas na inicialização (`ValidateOnStart`), e um valor ausente ou inválido impede o serviço de subir.

## Execução local

```bash
# Segredos locais (não versionados)
dotnet user-secrets --project services/audit-compliance/src/AuditCompliance.Api set "Jwt:SigningKey" "<chave-com-32+-caracteres>"
dotnet user-secrets --project services/audit-compliance/src/AuditCompliance.Api set "MongoDb:ConnectionString" "mongodb://audit_compliance_app:<senha>@localhost:27017/AuditComplianceDatabase?authSource=AuditComplianceDatabase"

dotnet run --project services/audit-compliance/src/AuditCompliance.Api

# Ou via compose: defina AUDIT_COMPLIANCE_DB_PASSWORD e AUDIT_COMPLIANCE_JWT_SIGNING_KEY em platform/.env
docker compose -f platform/docker-compose.yml up -d audit-compliance
```

O usuário `audit_compliance_app` e os índices são criados pelos scripts de `platform/init-db/mongodb`, que só rodam quando o volume do MongoDB é novo. Em um volume existente, execute os blocos "Audit & Compliance" de `1-database.js` e `2-schema.js` manualmente, com o usuário root.

## Procedimentos operacionais

- **Reprocessar mensagens do dead-letter:** depois de corrigir a causa, leia `audit-events-dlq`, extraia o campo `payload` de cada mensagem e publique-o em `audit-events-replay`. O processamento é idempotente, então reenviar um evento já gravado não gera duplicata.
- **Armazenamento indisponível:** o consumidor para de avançar o offset, registra erros (`audit_processing_failures`) e retoma sozinho quando o MongoDB volta. Acompanhe o lag do grupo `audit-compliance-group`.
- **Verificação de integridade:** `GET /records/{id}` retorna `integrityVerified`. Um valor `false` indica que o registro mudou depois da ingestão e deve ser tratado como incidente de segurança.

Métricas: `audit_events_received`, `audit_events_persisted`, `audit_events_duplicated`, `audit_events_rejected{reason}`, `audit_events_dead_lettered{reason}`, `audit_processing_failures{stage}`, `audit_fields_redacted`, `audit_ingestion_lag_seconds`, `audit_message_age_seconds`, `audit_export_duration_seconds`, `audit_export_records` e `audit_exports_failed{reason}`, além das métricas HTTP do ASP.NET Core.

## Testes

```bash
dotnet test services/audit-compliance/tests/AuditCompliance.UnitTests
dotnet test services/audit-compliance/tests/AuditCompliance.IntegrationTests   # requer Docker (Testcontainers: MongoDB 7 e Kafka)
```

Os testes de integração cobrem:

- autenticação e autorização, incluindo isolamento entre tenants;
- validação de parâmetros e formato Problem Details;
- deduplicação concorrente;
- detecção de adulteração;
- fluxo completo de exportação CSV/JSON com autoauditoria;
- pipeline Kafka (entrega duplicada, dead-letter e replay);
- recuperação após indisponibilidade do MongoDB, sem perda de mensagem.
