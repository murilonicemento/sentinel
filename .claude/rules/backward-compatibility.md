# Backward Compatibility Rules

## Purpose

Prevent unintended breakage for existing consumers, integrations, data contracts, and infrastructure dependencies across Sentinel services.

## Compatibility Scope

### Public APIs and External Integrations

The following external APIs, providers, and infrastructure integrations are part of the current compatibility scope.

| Service               | API or Integration                                   | Purpose                                                       | Authentication / Configuration                                                                     |
| --------------------- | ---------------------------------------------------- | ------------------------------------------------------------- | -------------------------------------------------------------------------------------------------- |
| ingestion             | USGS Earthquake (`earthquake.usgs.gov`)              | Earthquake data (`fdsnws/event/1/query`)                      | No API key required                                                                                |
| ingestion             | NASA FIRMS (`firms.modaps.eosdis.nasa.gov`)          | Fire detections (VIIRS CSV)                                   | `MapKey`                                                                                           |
| ingestion             | NOAA NCEI CDO v2 (`ncei.noaa.gov/cdo-web/api/v2`)    | `Flood`, `Landslide`, `TemperatureAnomaly`, `HumidityAnomaly` | Token configured in `appsettings.Development.json`; URLs use `*.example.com` in `appsettings.json` |
| ingestion             | Stormglass v2 (`api.stormglass.io/v2/weather/point`) | `WindGust`, `Rainfall`, `PressureChange`                      | `APIKey`                                                                                           |
| ingestion             | MinIO (S3)                                           | File storage; bucket `sentinel-ingestion-service`             | Access key and secret key in configuration                                                         |
| channels              | SendGrid                                             | Email delivery                                                | `ApiKey`                                                                                           |
| channels              | Twilio                                               | SMS and WhatsApp delivery                                     | `AccountSid` and `AuthToken`                                                                       |
| channels              | MQTT broker                                          | MQTT notification channel (`notifications/mqtt`)              | Defaults to `localhost:1883`                                                                       |
| channels              | HTTP Push and HTTP Siren                             | Custom push and siren providers                               | `localhost:5010/api/push`, `localhost:5011/api/siren`; API key required for push                   |
| geospatial, reporting | Elasticsearch 8.17.4                                 | `geospatial-events` and `reporting-events` indices            | No authentication configured                                                                       |

### Database Schemas

PostgreSQL 15.5 is used, with one database per service.

| Database           | Tables and Schema Notes                                                                                                                                                |
| ------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| ingestion (write)  | `tenant`, `data_source`, `event_type_permission`, `data_collection`, `sensor_sample`, `outbox`. The `outbox` schema is managed through manual SQL rather than EF Core. |
| risk_catalog       | `users`, `event_type`, `severity`, `severity_criterion`, `idf_curve`, `regional_parameter`, `risk_matrix`                                                              |
| alert_orchestrator | `alert_windows`, `alert_signals`                                                                                                                                       |
| channels           | `delivery_attempts`                                                                                                                                                    |
| tenants_billing    | `tenants`, `plans`                                                                                                                                                     |

The file `platform/init-db/postgres/2-schema.sql` is outdated: it does not include `users`, and the channels database name differs from the application configuration (`channels_service`). Treat the actual EF Core models and current database migrations as the sources of truth for EF-managed schemas. Verify manually managed SQL schemas separately.

**MongoDB 7**

- **ingestion (read):** `climaticEvents` and `disasterEvents` collections, with unique indexes on `EventId`.
- **risk-evaluation:** `RiskEvaluations`, `RiskModels`, and `RiskFactors` collections.

**Redis 7**

- **ingestion:** Event deduplication with TTL.
- **risk-catalog:** Generic cache.
- **risk-evaluation:** Recent scores using the `risk-evaluation:` prefix.
- **alert-orchestrator:** Cooldown state keyed by `(region, riskType, tenantId)`.
- **channels:** Tenant settings under `ChannelsService:TenantSettings:{tenantId}`, with a TTL of 300 seconds.
- **reporting:** Query cache with a TTL of 5 minutes and event cache with a TTL of 10 minutes.

**Elasticsearch**

- `geospatial-events`: Geospatial data.
- `reporting-events`: Reporting data.

### Events and Messages

The following event topics, producers, consumers, and payloads are part of the compatibility scope. Entries marked as not identified require verification against the actual implementation and runtime configuration.

| Topic                                                                 | Producer                                                               | Consumer           | Payload                                                                                                                    |
| --------------------------------------------------------------------- | ---------------------------------------------------------------------- | ------------------ | -------------------------------------------------------------------------------------------------------------------------- |
| `disaster-event-detected` / `climatic-event-detected`                 | ingestion outbox, using the `{domain}-event-detected` type convention  | risk-evaluation    | `SensorEventDetected { CollectionId, EventType, Intensity, Latitude, Longitude, CollectedAt }`                             |
| `sensor-event-detected`                                               | Not identified                                                         | geospatial         | Same `SensorEventDetected` payload                                                                                         |
| `region-intersected`                                                  | geospatial                                                             | alert-orchestrator | `RegionIntersectedEvent { EventId, Region, IntersectingRegion, RiskType, Severity, Timestamp }`                            |
| `risk-catalog-published`                                              | risk-catalog                                                           | risk-evaluation    | `{ Version: int, Notes, PublishedAt, PublishedBy }`                                                                        |
| `risk-events` (configured topic)                                      | risk-evaluation                                                        | Not identified     | `RiskEvaluatedEvent { EvaluationId, Latitude, Longitude, Score, Level, Timestamp }`                                        |
| `risk-updated` / `risk-evaluated`                                     | Not identified                                                         | alert-orchestrator | `RiskUpdatedEvent { EventId, Region, RiskType, RiskScore, TenantId, Timestamp, Metadata }`                                 |
| `alert-orchestrator-events`                                           | alert-orchestrator                                                     | Not identified     | `AlertTriggered`, `AlertConfirmed`, `AlertEscalated`, `AlertClosed`, `AlertWindowOpened`, `AlertWindowExpired`             |
| `alert-commands`                                                      | alert-orchestrator                                                     | Not identified     | Alert commands                                                                                                             |
| `notification-events` / `notification-events-dlq`                     | Published by the channels service through `KafkaNotificationPublisher` | channels           | `NotificationEvent { EventId, TenantId, Channels, PriorityEnum, Message, Metadata }`                                       |
| `tenants-billing-events`                                              | tenants-billing                                                        | Not identified     | Tenant events                                                                                                              |
| `tenant-usage-events` and associated `-dlq` and `-replay` topics      | Not identified                                                         | tenants-billing    | `TenantUsageEvent { TenantId, EventsConsumed, AlertsTriggered, ApiRequests, ChannelUsage }`                                |
| `reporting-events` and associated `-dead-letter` and `-replay` topics | Not identified                                                         | reporting          | `ReportingEventEnvelope { EventId, EventType, TenantId, Region, Severity, Channel, RiskScore, Status, Timestamp, Source }` |
| `geospatial-dead-letter`                                              | geospatial                                                             | Not identified     | `DeadLetterEnvelope`                                                                                                       |

### Shared Libraries

No shared libraries have been identified.

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
