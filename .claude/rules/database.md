# Database and Storage Rules

## Purpose

Define mandatory rules for data persistence, storage, integrity, security, and evolution across the microservices architecture.

## Core Principles

- Each microservice owns its data and controls changes to its schemas, collections, indexes, and stored objects.
- Services must not directly read from or write to another service's data stores.
- Services must communicate through explicitly defined APIs or asynchronous messaging.
- Never introduce a new database technology or storage system without an explicit architectural decision.
- Use each storage technology only for its intended purpose.
- Data access must remain within the Infrastructure layer, behind appropriate application-level abstractions.
- Data persistence must support tenant isolation, observability, failure handling, and recovery.

## Data Ownership and Isolation

- Each microservice must have clearly defined data ownership.
- Services must not share tables, collections, indexes, buckets, or internal storage structures as a substitute for service communication.
- Separate databases, schemas, collections, buckets, or key prefixes do not automatically establish sufficient security isolation.
- Cross-service data requirements must be addressed through APIs, events, or service-owned projections.
- Every service must document its authoritative data stores and the data it owns.
- Shared infrastructure is allowed, but ownership and access permissions must remain explicit.

## PostgreSQL

Use PostgreSQL for relational data, transactional operations, and integrity constraints.

- Use EF Core for relational persistence where it is already established and appropriate.
- Use EF Core migrations to manage EF Core-controlled schema changes.
- Version and validate manually managed SQL schemas separately from EF Core migrations.
- Use primary keys, foreign keys, unique constraints, check constraints, and appropriate indexes to enforce data integrity.
- Keep transactions local to the service and database operation.
- Use asynchronous database APIs and cancellation tokens where supported.
- Avoid loading unnecessary columns or records.
- Use pagination for potentially large result sets.
- Inspect generated SQL and query execution plans when investigating performance issues.
- Avoid N+1 queries and unnecessary database round trips.
- Never execute schema changes automatically in production application startup unless explicitly approved by the deployment strategy.

## MongoDB

Use MongoDB for document-oriented data when its document model fits the access patterns.

- Define clear ownership and expected structure for each collection.
- Use schema validation where appropriate.
- Create indexes based on actual query patterns and uniqueness requirements.
- Evaluate index size, write overhead, and query performance before adding indexes.
- Use atomic document operations when they can safely enforce the required consistency.
- Design document boundaries around access patterns and ownership.
- Avoid unbounded document growth and unbounded embedded arrays.
- Treat collection, document, validation, and index changes as controlled schema evolution.
- Use transactions only when necessary and supported by the deployment configuration.
- Ensure retryable operations are safe against duplicate execution where applicable.

## Redis

Use Redis for caching, deduplication, cooldowns, and other explicitly defined temporary or coordination state.

- Define a clear purpose, ownership, and expiration policy for each key.
- Use TTLs for temporary data whenever expiration is part of the intended behavior.
- Use consistent, namespaced key conventions to prevent collisions.
- Include tenant identifiers in keys whenever the data is tenant-specific.
- Use atomic Redis operations or appropriate Lua scripts when multiple operations must behave as one unit.
- Do not assume separate Redis commands form an atomic operation.
- Do not treat cached data as the authoritative source unless explicitly designed and documented.
- Define behavior for cache misses, Redis unavailability, and stale values.
- Document persistence and recovery requirements for Redis data that cannot be safely discarded.
- Treat key format changes as compatibility changes when old and new application versions may run simultaneously.

## MinIO

Use MinIO for object storage, including files and other binary artifacts.

- Assign clear ownership of buckets and stored objects to a microservice.
- Use explicit bucket and object-key naming conventions.
- Do not use MinIO as a substitute for relational queries or document databases.
- Store object metadata in an appropriate data store when it must be queried independently.
- Persist object references rather than duplicating large binary content in PostgreSQL or MongoDB.
- Define upload size limits, accepted content types, and validation requirements.
- Validate file metadata and content as appropriate; never trust client-supplied filenames or content types.
- Enforce access control through service authorization and appropriately scoped storage permissions.
- Use presigned URLs only when appropriate, with limited permissions and expiration.
- Never expose permanent credentials or unrestricted bucket access to clients.
- Define lifecycle, retention, deletion, backup, and recovery policies according to the data's requirements.
- Handle partial failures between object storage and database operations explicitly.
- Do not assume a database transaction can roll back an object-storage operation.
- Make upload and deletion workflows resilient to retries and partial completion.

## Elasticsearch

Use Elasticsearch for search, filtering, geospatial queries, and derived reporting or analytical projections where appropriate.

- Define ownership for each index and its indexing pipeline.
- Treat indexed documents as derived data when another service or store is authoritative.
- Do not use Elasticsearch as the authoritative transactional store unless explicitly approved by the architecture.
- Define mappings deliberately for important fields, including identifiers, dates, numeric values, and geospatial data.
- Apply tenant filters to every relevant query involving tenant-specific documents.
- Create indexes and mappings based on documented query requirements.
- Treat mapping changes as controlled index migrations.
- Use versioned indexes and aliases when needed for safe reindexing and cutover.
- Document how indexes can be rebuilt from authoritative data or replayable events.
- Handle indexing failures, duplicate events, and eventual consistency explicitly.
- Use pagination strategies appropriate to the expected result size and query pattern.
- Avoid unrestricted queries and unnecessarily large result windows.

## Transactions and Distributed Consistency

- Use database transactions for atomic operations within a single service-owned transactional store.
- Do not attempt to create a distributed transaction across microservices or unrelated storage systems without an explicit architectural decision.
- Prefer asynchronous messaging and eventual consistency for cross-service workflows.
- Use the transactional outbox pattern when a database update and event publication must remain consistent.
- Design message consumers and background jobs to tolerate duplicate delivery.
- Make retryable operations idempotent where required.
- Document consistency guarantees and recovery behavior for workflows spanning multiple storage systems.

## Multi-Tenancy and Security

- Derive tenant context from authenticated identity and validated membership or authorization rules.
- Never trust a client-supplied tenant identifier as proof of authorization.
- Apply tenant isolation consistently to database queries, document operations, Redis keys, object access, and Elasticsearch queries.
- Test that one tenant cannot access another tenant's data or objects.
- Apply least-privilege credentials and permissions to each service.
- Keep credentials and sensitive configuration outside source control.
- Use the established secret-management mechanism for each environment.
- Never log credentials, sensitive object contents, or unnecessary personal data.

## Schema and Storage Evolution

- Review schema, collection, index, mapping, bucket, and object-key changes before implementation.
- Use versioned migrations or deployment procedures appropriate to each storage technology.
- Prefer backward-compatible expand-and-contract changes when application versions may overlap during deployment.
- Validate migrations against representative data and realistic environments.
- Document destructive or irreversible changes and their recovery strategy.
- Ensure application rollback procedures account for storage changes that cannot be automatically reversed.
- Keep infrastructure initialization scripts aligned with the actual service configuration and schema ownership.

## Performance and Reliability

- Add indexes based on measured query patterns rather than speculation.
- Monitor query latency, connection pools, storage capacity, index size, cache hit rates, and operation failures.
- Define timeouts and retry policies appropriate to each operation.
- Retry only transient failures and account for duplicate execution.
- Avoid unbounded queries, unbounded document growth, and uncontrolled object retention.
- Establish backup, restore, and disaster-recovery requirements based on data criticality.
- Do not assume that cache contents or derived search indexes can always be reconstructed without validating their recovery sources.

## Validation

When applicable, verify:

- Schema and migration correctness.
- Data integrity and uniqueness constraints.
- Tenant isolation across every relevant storage technology.
- Query performance and index effectiveness.
- Concurrency and idempotency behavior.
- Object upload, retrieval, deletion, and authorization.
- Elasticsearch indexing, mapping compatibility, and reindexing.
- Redis expiration, atomicity, and failure behavior.
- Backup restoration and recovery procedures.
- Integration tests against representative storage configurations.

## Implementation Guidance

Before changing persistence or storage code:

1. Identify the owning microservice and authoritative data store.
2. Inspect the existing implementation, configuration, migrations, and tests.
3. Follow established patterns for the relevant storage technology.
4. Determine whether the change affects data integrity, tenant isolation, consistency, or backward compatibility.
5. Implement the smallest appropriate change.
6. Add or update tests covering expected behavior and failure scenarios.
7. Document any required migration, infrastructure change, or operational procedure.

Do not assume that every microservice must use every storage technology. Use only the stores required by its responsibilities.
