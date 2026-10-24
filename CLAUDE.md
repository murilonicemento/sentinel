# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

Sentinel is a .NET monorepo for a distributed natural-events risk detection and alerting platform (floods, landslides, fires, earthquakes, extreme rain). It uses microservices, one per bounded context, with DDD, Clean Architecture, CQRS where it applies, and Kafka-based event-driven integration. It is multi-tenant. Docs and most comments are in Portuguese.

`docs/constitution.md` holds the project's binding architecture rules. Read it before you create a service or change project references. Each service has a spec in `docs/services/<Service>.md`, and `docs/RFC-Arquitetura.md` is the architecture RFC.

## Commands

The SDK is pinned to .NET 9 (`global.json`: 9.0.115). All projects target `net9.0`.

```bash
# Build / test everything in the root solution
dotnet build Sentinel.sln
dotnet test Sentinel.sln

# Single test project / single test
dotnet test services/geospatial/tests/Geospatial.UnitTests
dotnet test services/geospatial/tests/Geospatial.UnitTests --filter "FullyQualifiedName~GeoPointTests"
dotnet test <proj> --filter "FullyQualifiedName=Namespace.Class.Method"

# Run one service locally
dotnet run --project services/<service>/src/<Service>.Api

# Local infrastructure + services (compose file lives in platform/, copy .env.example to .env first)
docker compose -f platform/docker-compose.yml up -d
docker compose -f platform/docker-compose.yml down

# EF Core migrations (example: tenants-billing)
dotnet ef migrations add <Name> \
  --project services/tenants-billing/src/TenantsBilling.Infrastructure \
  --startup-project services/tenants-billing/src/TenantsBilling.Api
```

Tests use xUnit and Moq. Integration tests use `WebApplicationFactory`, and some use Testcontainers (Postgres, Redis, Kafka).

**Solution membership:** `Sentinel.sln` includes every service except `tenants-billing`, which only has its own `services/tenants-billing/TenantsBilling.sln`. The constitution says each service should have its own `.sln`. In practice, the root solution is what holds most services, so when you add a project, add it to the right solution(s).

## Architecture

### Service layout and dependency rules

Every service lives in `services/<kebab-name>/` and follows the same layout:

```bash
src/<Pascal>.Api  →  <Pascal>.Application  →  <Pascal>.Domain
src/<Pascal>.Infrastructure  →  Application + Domain
tests/<Pascal>.UnitTests, tests/<Pascal>.IntegrationTests
Dockerfile (multi-stage, publishes only .Api) + .dockerignore
```

The constitution enforces these rules:

- **Domain** depends on nothing: no EF, HTTP, messaging, DTOs or frameworks.
- **Application** holds commands, queries, handlers (MediatR in several services), DTOs and ports (interfaces). It has no infrastructure code.
- **Infrastructure** implements the Application ports: EF Core and Npgsql, Kafka (Confluent.Kafka), Redis, HTTP clients and hosted services.
- **Api** contains controllers, middleware, DI wiring, health checks and OpenAPI/Scalar, and nothing else. No business logic belongs here. The constitution says Api → Application only, but in practice every Api project also references Infrastructure so it can call the `Add...Infrastructure` DI extension. Keep that reference limited to DI registration.
- Services share no databases and no code. There is no shared `libs/` yet, even though the README mentions it, so each service has its own copy of things like `ResponseBaseDTO`, `ResponseWrapperFilter`, `ExceptionHandlingMiddleware` and `TenantBillingGateway`.
- New services must be scaffolded with `dotnet new` / `dotnet sln add` / `dotnet add reference` only (exact steps are in constitution §6).

`ingestion` is the full-CQRS exception. It splits infrastructure into `Infrastructure.Write` (Postgres, outbox, Kafka publishing, polling clients for external APIs, MinIO, Redis dedup) and `Infrastructure.Read` (MongoDB).

### Event pipeline

Topics are created by the `kafka-init` container in `platform/docker-compose.yml`. When you add a topic, add it there too.

1. **ingestion** polls external sources (USGS earthquakes, FIRMS fire, NOAA, Stormglass, etc.) and takes in sensor data. It writes through a transactional **outbox** (`OutboxHostedService`) and publishes `sensor-event-detected`, `disaster-event-detected` and `climatic-event-detected`.
2. **risk-catalog** manages event types, severities, IDF curves, risk matrices and regional parameters, and publishes `risk-catalog-published`.
3. **geospatial** handles geometry operations with NetTopologySuite and Elasticsearch. It also exposes a synchronous HTTP API that ingestion, risk-catalog and risk-evaluation call. It consumes `sensor-event-detected`, publishes `region-intersected`, and sends failures to `geospatial-dead-letter`.
4. **risk-evaluation** consumes sensor events and catalog updates, scores risk, caches recent scores in Redis, and publishes `risk-updated` / `risk-evaluated`.
5. **alert-orchestrator** consumes `risk-updated` and `region-intersected`. It manages the `AlertWindow` aggregate (quorum, cooldown, escalation and expiration workers) and publishes to `alert-orchestrator-events` and `alert-commands`.
6. **channels** delivers notifications (SMS/WhatsApp via Twilio, email via SendGrid, push, MQTT, sirens) with retry, fallback and DLQ handling (`notification-events`, `notification-events-dlq`).
7. **reporting** consumes events for dashboards, analytics and exports, with a DLQ consumer. Its implementation status is tracked in `docs/reporting-service-status.md`.
8. **tenants-billing** manages tenants and plans. It consumes `tenant-usage-events` (with `-dlq` / `-replay` topics) and publishes `tenants-billing-events`. Other services check whether a tenant is active over synchronous HTTP (`GET /api/v1/tenants/{id}`) through their own `TenantBillingGateway`.
9. **api-gateway** uses Ocelot (`ocelot.json`) to route to the services.

### Conventions worth knowing

- Several services choose adapters from config flags, for example `UsePostgreSql` / `UseRedis` in `*InfrastructureOptions`. With the flags off, they fall back to in-memory repositories and idempotency services. Check these flags when a service "loses" data or ignores Redis.
- Consumers that publish domain events are expected to be idempotent, using a Redis-backed idempotency or dedup service.
- Observability goes through Serilog (console + Loki), OpenTelemetry, and a Prometheus exporter on some services. Dashboards are provisioned from `platform/observability/grafana/provisioning`.
- Database bootstrap scripts are in `platform/init-db/{postgres,mongodb}`.
