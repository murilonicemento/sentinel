# Backward Compatibility Rules

## Purpose

Prevent unintended breakage for existing consumers, integrations, data contracts, and infrastructure dependencies across Sentinel services.

## Compatibility Scope

The tables below were verified against the code and `platform/docker-compose.yml` on 2026-10-29. Each service's `docs/services/*.md` has an "Estado da implementação" section with the details.

### Public APIs and External Integrations

Every service exposes its public contract under `/api/v1/<service>`; the older routes are frozen and deprecated until 2027-01-23 (`docs/adr/ADR-0007-api-versioning-and-deprecation.md`). Internal callers of a deprecated route must move to v1 before the sunset; no known internal caller uses a deprecated route since ingestion and risk-catalog moved to geospatial's v1 operations (X-25).

| Service               | API or Integration                                   | Purpose                                                       | Authentication / Configuration                                                                     |
| --------------------- | ---------------------------------------------------- | ------------------------------------------------------------- | -------------------------------------------------------------------------------------------------- |
| ingestion             | USGS Earthquake (`earthquake.usgs.gov`)              | Earthquake data (`fdsnws/event/1/query`)                      | No API key required; polling off unless `POLLING_EARTHQUAKE_ENABLED=true`                          |
| ingestion             | NASA FIRMS (`firms.modaps.eosdis.nasa.gov`)          | Fire detections (VIIRS S-NPP NRT CSV, FRP in MW)              | Map key in the request path, from `POLLING_FIRMS_MAP_KEY`                                          |
| ingestion             | NOAA NCEI CDO v2 (`ncei.noaa.gov/cdo-web/api/v2`)    | `Flood`, `Landslide`, `TemperatureAnomaly`, `HumidityAnomaly` | `token` header, from `POLLING_NOAA_TOKEN`                                                          |
| ingestion             | Stormglass v2 (`api.stormglass.io/v2/weather/point`) | `WindGust`, `Rainfall`, `PressureChange`                      | Raw key in the `Authorization` header, from `POLLING_STORMGLASS_API_KEY`                           |
| ingestion             | MinIO (S3)                                           | Raw payloads (`raw/{collectionId}.json`); bucket `sentinel-ingestion-service` | `MinIO:AccessKey` / `SecretKey`, from `MINIO_ROOT_USER` / `MINIO_ROOT_PASSWORD` in compose |
| channels              | SendGrid                                             | Email delivery                                                | `SENDGRID_API_KEY`                                                                                 |
| channels              | Twilio                                               | SMS and WhatsApp delivery                                     | `TWILIO_ACCOUNT_SID` and `TWILIO_AUTH_TOKEN`                                                       |
| channels              | MQTT broker                                          | MQTT notification channel (`notifications/mqtt`)              | `ChannelsService:Providers:MQTT:Options` (local default `localhost:1883`)                         |
| channels              | HTTP Push and HTTP Siren                             | Custom push and siren providers                               | `localhost:5010/api/push`, `localhost:5011/api/siren`; `PUSH_API_KEY`, `SIREN_API_KEY`             |
| geospatial, reporting | Elasticsearch 8.17.4                                 | `geospatial-events` and `reporting-events` indices            | No authentication configured                                                                       |

Polling subscriptions (tenant, data source, area) live in `platform/config/ingestion-polling.json` (`docs/adr/ADR-0005-external-polling-and-region-identity.md`). Provider credentials never go in `appsettings*.json`.

### Database Schemas

PostgreSQL 15.5 is used, with one database per service, created by `platform/init-db/postgres/1-database.sql`.

| Database           | Tables and Schema Notes                                                                                                                                                                  |
| ------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| ingestion          | `tenant`, `data_source`, `event_type_permission`, `data_collection`, `sensor_sample`, `outbox`. Hand-written, idempotent SQL in the `ingestion` section of `2-schema.sql` (Dapper, no EF). |
| risk_catalog       | `users`, `event_type`, `severity`, `severity_criterion`, `idf_curve`, `regional_parameter` (unique `region_code`), `risk_matrix`                                                         |
| alert_orchestrator | `alert_windows`, `alert_signals`                                                                                                                                                         |
| channels           | `delivery_attempts`                                                                                                                                                                      |
| tenants_billing    | `tenants`, `plans`, `tenant_usage`, `recorded_usage_events`                                                                                                                              |

The `risk_catalog`, `alert_orchestrator`, `channels` and `tenants_billing` sections of `platform/init-db/postgres/2-schema.sql` are generated from the EF Core migrations (`dotnet ef migrations script --idempotent`) and must be regenerated after a migration. The integration tests of ingestion and risk-catalog run those sections on PostgreSQL 15.5.

**MongoDB 7**

- **ingestion (read):** `climaticEvents` and `disasterEvents` (unique indexes on `EventId`), written by the pipeline and read by v1. The frozen legacy routes read `events`, which nothing writes.
- **risk-evaluation:** `RiskEvaluations` (exact `double` coordinates; documents stored before RE-18 hold whole-degree integers and stay readable), `RiskModels`, `RiskFactors`, `CatalogVersions` and `CatalogSnapshots`.
- **audit-compliance:** `AuditComplianceDatabase` with `auditRecords` (append-only, unique index on `SourceService` + `EventId`), `auditExports`, and the GridFS bucket `auditExportFiles`. The custom role `auditComplianceApp` denies `update`/`remove` on `auditRecords`. Index and role definitions live in `platform/init-db/mongodb` and `MongoIndexInitializer`.

**Redis 7**

- **ingestion:** Deduplication claims `ing:{tenant}:{dataSource}:{yyyyMMddHHmmss}` (HTTP collections) and `ing:{tenant}:{dataSource}:src:{providerEventId}` (polled readings), with a lease while processing and a TTL after it.
- **risk-catalog:** Read cache of risk matrices and IDF curves (TTL of 1 hour).
- **risk-evaluation:** Recent scores by exact position under `risk:score:v2:{latitude}:{longitude}` (TTL of 5 minutes; the older whole-degree keys `risk:score:{lat}:{lon}` expire on their own), and consumption claims `risk-evaluation:consumed:v1:{key}`.
- **alert-orchestrator:** Idempotency claims `alert-orchestrator:idempotency:v2:{eventId}` and cooldown state keyed by `(region, riskType, tenantId)`.
- **channels:** Tenant settings under `ChannelsService:TenantSettings:{tenantId}`, with a TTL of 300 seconds.
- **reporting:** `reporting:events:{tenantId}` (TTL of 5 minutes) and `reporting:event:{tenantId}:{eventId}` (TTL of 10 minutes).

**Elasticsearch**

- `geospatial-events`: Geospatial operations and consumed sensor events.
- `reporting-events`: Reporting data.

### Events and Messages

Producers and consumers were verified in the code. A topic with no producer or no consumer is a known integration gap (X-24 in `docs/compliance/services-validation.md`), not proof that the topic is unused: keep it until the gap is resolved.

| Topic                                                       | Producer                                                                | Consumer                       | Payload                                                                                                                                  |
| ----------------------------------------------------------- | ----------------------------------------------------------------------- | ------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------- |
| `disaster-event-detected` / `climatic-event-detected`       | ingestion outbox (`{domain}-event-detected`)                            | risk-evaluation                | `SensorEventDetected { CollectionId, EventType, Intensity, Latitude, Longitude, CollectedAt, EventId, TenantId }` (`TenantId` since ADR-0008; absent in older messages) |
| `sensor-event-detected`                                     | None                                                                    | geospatial                     | Same `SensorEventDetected` payload                                                                                                       |
| `region-intersected`                                        | None (geospatial stopped publishing on HTTP `intersects`, ADR-0008)     | alert-orchestrator             | `RegionIntersectedEvent { EventId, Region, IntersectingRegion, RiskType, Severity, Timestamp }`; older messages hold a `GeospatialOperationEvent` and are dead-lettered |
| `risk-catalog-published`                                    | risk-catalog                                                            | risk-evaluation                | `{ Version: int, Notes, PublishedAt, PublishedBy }`                                                                                      |
| `risk-events` (`Kafka:Topic`)                               | risk-evaluation                                                         | None                           | `RiskEvaluatedEvent { EvaluationId, Latitude, Longitude, Score, Level, Timestamp }` and `HighRiskDetectedEvent` (decimal coordinates since RE-18; older messages hold whole degrees) |
| `risk-updated` (`Kafka:RiskUpdatedTopic`)                   | risk-evaluation (ADR-0008), keyed `{tenant}:{region}`                   | alert-orchestrator             | `RiskUpdatedEvent { EventId, Region, RiskType, RiskScore, TenantId, Timestamp, Metadata }`; `Region` is the catalog `regionCode`, `TenantId` is required |
| `risk-evaluated`                                            | None                                                                    | None                           | Created by `kafka-init` only                                                                                                             |
| `alert-orchestrator-events`                                 | alert-orchestrator                                                      | None                           | `AlertWindowOpened`, `AlertTriggered`, `AlertConfirmed`, `AlertEscalated`, `AlertClosed`, `AlertWindowExpired`                           |
| `alert-commands`                                            | alert-orchestrator                                                      | channels (ADR-0008)            | `TriggerAlertCommand` (PascalCase, `EscalationLevel` as a number, `TenantId` required by channels)                                      |
| `notification-events` / `notification-events-dlq`           | channels (reprocessing endpoint) / channels (undelivered notifications, including alert commands) | channels / None | `NotificationEvent { EventId, TenantId, UserId, EventType, CorrelationId, PriorityEnum, Message, Channels, FallbackEnabled, Metadata }` |
| `tenants-billing-events`                                    | tenants-billing                                                         | None                           | `TenantCreated`, `PlanCreated`, `PlanPolicyUpdated`, `TenantQuotaSoftLimitReached`, `TenantQuotaHardLimitReached`, `TenantSuspended`, `TenantReactivated`, `TenantCanceled` |
| `tenant-usage-events` and its `-dlq` and `-replay` topics   | ingestion (outbox), alert-orchestrator, channels (ADR-0009); the `-dlq` and `-replay` topics are written by tenants-billing | tenants-billing                | `TenantUsageEvent { TenantId, EventsConsumed, AlertsTriggered, ApiRequests, ChannelUsage, EventId }` (deterministic `EventId`, counted once) |
| `reporting-events`                                          | alert-orchestrator (`AlertTriggered`), channels (`NotificationSent` / `NotificationFailed`) (ADR-0010) | reporting                      | `ReportingEventEnvelope { EventId, EventType, TenantId, Region, Severity, Channel, RiskScore, Status, Timestamp, Source }`; `Channel` also accepts `whatsapp`, `mqtt`, `siren` and `none` since ADR-0010 |
| `reporting-events-dead-letter` / `reporting-events-replay`  | reporting                                                               | reporting / reporting          | Dead-letter envelope / replay wrapper `{ sourceTopic, reprocessedBy, replayRequested, requestedAtUtc, originalPayload }` (REP-09)        |
| `audit-events` and its `-replay` topic                      | None                                                                    | audit-compliance               | `AuditEventEnvelope` v1 `{ EventId, EventType, EventVersion, SourceService, OccurredAt, TenantId, CorrelationId, CausationId, Action, Actor, Resource, Metadata, BeforeState, AfterState }` |
| `audit-events-dlq`                                          | audit-compliance                                                        | None                           | `DeadLetterEnvelope { OriginalTopic, Partition, Offset, Key, Payload, ErrorCode, ErrorReason, FailedAt, ConsumerGroup }`                 |
| `<service>-dead-letter`, `geospatial-dead-letter`           | each consuming service (ADR-0003)                                       | None                           | `DeadLetterEnvelope`                                                                                                                     |

### Shared Libraries

No shared libraries exist. Copied code (security, observability, the reliability kit, `TenantBillingGateway`, `DeprecatedRouteAttribute`) must be kept in sync across services.

## Rules

- Identify affected consumers and integrations before changing public contracts, database schemas, event payloads, or externally consumed configuration.
- Treat API contracts, event schemas, database schemas, cache key formats, and third-party integration requirements as compatibility-sensitive interfaces.
- Avoid removing or renaming public fields, event properties, database columns, topics, configuration keys, or cache keys without a migration plan.
- Prefer additive changes when feasible.
- Evaluate whether adding fields or enum values could break consumers that use strict schema validation or exhaustive enum handling.
- Preserve existing event semantics, including field meanings, units, timestamps, identifiers, and tenant context.
- Do not assume that all producers and consumers are deployed simultaneously or run the same application version.
- Consider mixed application versions during deployments and rolling upgrades.
- Maintain compatibility with existing persisted data and queued messages during migrations.
- Treat changes to database migrations, indexes, constraints, serialization formats, and cache key conventions as potential compatibility risks.
- Treat changes to external API versions, authentication requirements, rate limits, and provider-specific payloads as integration risks.
- Update affected documentation, configuration examples, contract tests, and migration instructions when a contract changes.
- Verify undocumented or unconfirmed producers and consumers before modifying related topics or payloads.
- Do not assume that an integration, topic, table, or configuration entry is unused solely because its consumer or producer has not been identified.

## Migration Strategy

### Compatibility Window

- **Public APIs:** Use the deprecation period defined by the team, currently proposed as 90 days.
- **Internal APIs and events:** Maintain compatibility throughout the gradual rollout until the relevant consumers have been migrated.
- **Messages already in queues:** Ensure they remain processable during the transition, including retained messages and dead-letter queues.

### Deprecation Policy

Assign an owner to each deprecation, document the replacement, and specify the planned removal date. For public APIs, use the default 90-day deprecation period unless an exception is approved. For internal topics, removal must also depend on confirmation that consumers have migrated and pending messages have been handled.

### Rollout and Rollback Strategy

Use an expand-and-contract approach for potentially breaking changes:

1. **Expand:** Add the new field or behavior without removing the existing one.
2. **Migrate:** Update consumers to accept the new format.
3. **Switch:** Start relying on the new field or behavior.
4. **Contract:** Remove the old format only after confirming that no remaining consumer depends on it.

### Database Schema Migration Strategy

Use gradual database migrations. For example, when replacing `name` with `display_name`:

1. Add `display_name` while keeping `name`.
2. Update the application to read and write both fields compatibly.
3. Migrate existing data.
4. Confirm that no active application version depends on `name`.
5. Remove `name` in a subsequent migration.

### Event and Message Schema Evolution Strategy

Prefer additive changes, preserve the meaning of existing fields, and define explicit serialization and event evolution rules. Test old, new, and invalid messages. Also account for messages retained in queues, retries, and dead-letter queues.

### Cache Key and Cached Data Migration Strategy

- Temporarily support both key formats when necessary.
- Read from the old key and write to the new format during the transition.
- Use a versioned prefix, such as `v2`, to separate incompatible formats.
- Allow cached data to expire naturally when discarding the old data is safe.

### External Provider API Upgrade Strategy

- Encapsulate each integration in a dedicated client or adapter.
- Validate incoming data formats before processing responses.
- Create tests using representative examples of external responses.
- Keep credentials out of source code and version-controlled configuration files.
- Update an integration only after reviewing the provider documentation and testing the new contract.
- Handle timeouts, transient failures, and invalid responses.
- When possible, support a gradual migration between provider API versions.

### Configuration and Secret Rotation Strategy

1. Create a new credential.
2. Update the service to use the new credential.
3. Verify that requests succeed.
4. Revoke the old credential.
5. Confirm that no service is still using the old key.

## Validation

### Contract Tests

Create tests for the most important HTTP and event contracts first. There is no need to test everything at once.

### API Compatibility Checks

Keep OpenAPI specifications versioned in the repository and add a comparison tool to CI. The specific tool can be selected later based on the technology stack and build process.

### Event Schema Compatibility Checks

Keep event examples or schemas in a version-controlled location and validate changes before approving them. If Sentinel adopts a schema registry in the future, evaluate its compatibility policies.

### Database Migration Validation

Use automated tests to validate migrations against a representative schema, inspect the resulting data, and verify relevant constraints. Apply this validation to both EF Core migrations and manual SQL scripts.

### Backward Compatibility Tests for Mixed Application Versions

For significant changes to inter-service contracts, test combinations of versions that could realistically coexist during a rollout. There is no need to test every mathematically possible combination, as that would become a project of its own.

### Integration Tests for External Providers

Use mocked responses for frequent and predictable tests. Reserve real provider checks for appropriate scenarios because they may depend on network access, credentials, usage limits, and external availability.

### Rollback Validation

Test rollback to the previous version for high-risk changes. Prefer schema changes that allow temporary coexistence between application versions, and document any irreversible changes or changes that require a specific recovery procedure.

### Required Automated Checks in CI

Start with the build, existing tests, OpenAPI validation, and critical tenant isolation tests. Add API, event, and database compatibility checks as the contracts become formalized.
