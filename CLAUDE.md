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

# Build one service image outside compose (Dockerfiles read Directory.Packages.props from the repo root)
docker build --build-context repo-root=. -t <service> services/<service>

# EF Core migrations (example: tenants-billing)
dotnet ef migrations add <Name> \
  --project services/tenants-billing/src/TenantsBilling.Infrastructure \
  --startup-project services/tenants-billing/src/TenantsBilling.Api
```

Tests use xUnit and Moq. Integration tests use `WebApplicationFactory`, and some use Testcontainers (Postgres, Redis, Kafka).

NuGet versions are managed centrally in `Directory.Packages.props` (`ManagePackageVersionsCentrally`). `PackageReference` items in `.csproj` files carry no `Version`; add or bump versions only in `Directory.Packages.props`. A per-project exception uses `VersionOverride` and must be documented in `docs/compliance/services-validation.md` (X-06). Every Dockerfile copies that file from the `repo-root` build context, which `platform/docker-compose.yml` defines for each service.

**Solution and Project Organization**

The repository must have a single .NET solution: `Sentinel.sln`, located at the repository root.

- `Sentinel.sln` is the solution for the entire Sentinel platform, not for an individual microservice.
- Every application, infrastructure, domain, API, unit test, and integration test project must be included in `Sentinel.sln`.
- Microservices are organized into separate directories under `services/<service-name>/`, but their projects remain members of the root solution.
- **Never create a separate `.sln` file for an individual microservice.** Do not create files such as `services/tenants-billing/TenantsBilling.sln`.
- When creating a new project, add it to the existing `Sentinel.sln` using `dotnet sln Sentinel.sln add <project-path>`.
- When creating a new microservice, add all its projects to `Sentinel.sln`, including test projects.
- When modifying an existing service, preserve the single-solution structure and ensure all new projects are registered in `Sentinel.sln`.
- Project references must follow the dependency rules defined in `docs/constitution.md`. Solution membership does not imply that projects may reference one another freely.
- Build and test the entire platform from the root solution using `dotnet build Sentinel.sln` and `dotnet test Sentinel.sln`.
- Individual projects may still be built, tested, or executed directly using their `.csproj` paths when necessary. This does not justify creating additional solution files.

**Current repository state:** `Sentinel.sln` contains every .NET project in the repository, including `tenants-billing` (its former `TenantsBilling.sln` was migrated and removed). Verify with `dotnet sln Sentinel.sln list`.

**Non-negotiable rule:** One repository, one solution (`Sentinel.sln`), multiple microservices, and independently organized projects.

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
9. **audit-compliance** consumes auditable events from `audit-events` (and `audit-events-replay`), persists append-only records in MongoDB, and sends invalid messages to `audit-events-dlq`. It exposes queries and async CSV/JSON exports under `/api/v1/audit-compliance`. No source service publishes to `audit-events` yet. Status and contract are in `docs/audit-compliance-service-status.md`, decisions in `docs/adr/ADR-0001-audit-compliance-service.md`.
10. **api-gateway** uses Ocelot (`ocelot.json`) to route to the services.

### Conventions worth knowing

- Several services choose adapters from config flags, for example `UsePostgreSql` / `UseRedis` in `*InfrastructureOptions`. With the flags off, they fall back to in-memory repositories and idempotency services. Check these flags when a service "loses" data or ignores Redis.
- Consumers that publish domain events are expected to be idempotent, using a Redis-backed idempotency or dedup service.
- Observability goes through Serilog (console + Loki), OpenTelemetry, and a Prometheus exporter on some services. Dashboards are provisioned from `platform/observability/grafana/provisioning`.
- Database bootstrap scripts are in `platform/init-db/{postgres,mongodb}`.
