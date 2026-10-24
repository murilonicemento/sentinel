# Performance Rules

## Purpose

Prevent avoidable performance regressions and ensure services use computational, memory, database, and network resources efficiently.

## General Rules

- Measure before introducing complex optimizations.
- Prefer simple, maintainable solutions over speculative optimizations.
- Avoid unnecessary allocations, repeated computations, and redundant I/O operations.
- Prevent N+1 database queries.
- Avoid unbounded data retrieval and uncontrolled resource consumption.
- Consider memory usage, connection management, and resource cleanup.
- Use asynchronous I/O for operations that spend significant time waiting on external resources.
- Do not use asynchronous APIs when they provide no meaningful benefit.
- Do not block asynchronous execution unnecessarily.
- Dispose of resources according to their ownership and lifetime requirements.
- Avoid holding database transactions, locks, or connections longer than necessary.
- Do not sacrifice correctness, security, consistency, or maintainability for speculative performance gains.
- Avoid introducing additional infrastructure or dependencies solely for unverified performance benefits.
- Use existing observability instrumentation to identify bottlenecks before changing implementation.

## Performance Requirements

### Response Time

- Define response-time objectives for each service and operation based on its requirements and observed workload.
- Measure latency distributions, including p50, p95, and p99 where appropriate.
- Distinguish application processing time from dependency latency and end-to-end request latency.
- Establish separate objectives for synchronous APIs, background jobs, and asynchronous message processing.
- Do not introduce arbitrary global response-time limits without workload measurements and service requirements.
- Treat performance objectives as measurable requirements once they have been explicitly defined.

### Throughput

- Define expected request rates, message-processing rates, and peak throughput for relevant workloads.
- Measure throughput alongside latency, error rate, and resource consumption.
- Identify bottlenecks in CPU, memory, database connections, network I/O, and external dependencies.
- Avoid increasing throughput by creating unbounded queues or excessive concurrency.
- Ensure the service degrades predictably when demand exceeds available capacity.

### Memory Constraints

- Avoid loading entire datasets when filtering, pagination, streaming, or batching is appropriate.
- Avoid retaining large objects or request-scoped data longer than necessary.
- Prevent unbounded in-memory collections and uncontrolled buffering.
- Evaluate allocation rates, garbage collection, and retained memory when investigating .NET performance issues.
- Dispose of streams, database resources, and other disposable objects correctly.
- Use streaming for large payloads when supported and appropriate.
- Define memory limits according to deployment resources and workload requirements.
- Avoid premature pooling or object reuse without evidence that it improves performance.

### Expected Workload

- Identify normal, peak, and burst workloads for each service.
- Account for payload size, concurrency, request distribution, tenant distribution, and dependency latency.
- Include background processing, retries, scheduled tasks, and message replay in capacity considerations.
- Test representative workloads instead of relying exclusively on synthetic minimal examples.
- Document important workload assumptions and update them when usage patterns change.

## Database and Storage Performance

- Inspect query patterns and execution plans before making database optimizations.
- Retrieve only the fields and records required by the operation.
- Use pagination or bounded queries for potentially large result sets.
- Add indexes based on measured query patterns and write-performance trade-offs.
- Avoid N+1 queries and unnecessary database round trips.
- Use bulk operations or batching when supported and beneficial.
- Keep transactions short and avoid unnecessary lock contention.
- Configure connection pools and timeouts according to the workload and deployment environment.
- Avoid unbounded MongoDB queries, uncontrolled document growth, and unnecessary index overhead.
- Use Redis operations and data structures appropriate to the required access patterns.
- Avoid unnecessarily large Elasticsearch result windows and expensive queries.
- Use MinIO streaming or multipart operations for large objects when appropriate.
- Measure database and storage latency independently from application processing time.
- Do not introduce a new data store or duplicate persisted data solely to address an unverified bottleneck.

## Caching

### Caching Strategy

- Cache data only when measurements or access patterns justify it.
- Identify the authoritative source for every cached value.
- Prefer existing caching infrastructure and established service conventions.
- Define cache ownership, key format, data shape, and expiration policy.
- Use cache-aside or another explicitly documented caching pattern where appropriate.
- Avoid caching sensitive data unless its security and isolation requirements are addressed.
- Include tenant context in cache keys when cached data is tenant-specific.
- Prevent cross-tenant cache collisions and unauthorized data exposure.
- Avoid caching errors or incomplete results as successful responses.
- Define behavior for cache misses, stale values, and cache unavailability.
- Do not make Redis a mandatory dependency for operations that should remain available without it unless the architecture explicitly requires that behavior.
- Avoid introducing caching when its invalidation and consistency requirements cannot be implemented reliably.

### Cache Invalidation

- Define how cached data is invalidated or refreshed when authoritative data changes.
- Use explicit invalidation, versioned keys, bounded TTLs, or other documented strategies appropriate to the data.
- Ensure cache invalidation remains correct during concurrent updates and retries.
- Consider stale reads during deployment and across multiple service instances.
- Do not assume updating the database automatically updates Redis or other caches.
- Document cache invalidation behavior for changes that affect multiple services.
- Avoid broad invalidation operations that can cause unnecessary load spikes.

### TTL and Consistency

- Choose TTLs according to data volatility, freshness requirements, and operational cost.
- Do not apply one universal TTL to every cached value.
- Document whether stale values are acceptable and for how long.
- Use atomic operations where concurrent cache updates require them.
- Prevent cache stampedes for expensive operations when necessary.
- Use TTL jitter or request coalescing where appropriate to avoid synchronized expiration spikes.
- Treat deduplication keys, cooldown state, and correctness-critical coordination differently from ordinary performance caches.
- Do not allow cache expiration or eviction to silently violate required correctness guarantees.

## Concurrency and Resource Limits

### Parallelism

- Introduce parallel execution only when it improves measured throughput or latency.
- Avoid blocking asynchronous operations unnecessarily.
- Use bounded concurrency for CPU-intensive work, external requests, and background processing.
- Avoid unbounded task creation and uncontrolled parallel loops.
- Consider downstream capacity before increasing concurrency.
- Avoid nested parallelism that multiplies resource usage unexpectedly.
- Preserve required ordering and consistency guarantees.
- Ensure cancellation and failure propagation work correctly when parallel operations fail.
- Use established concurrency and resilience policies rather than introducing independent limits for the same resource.

### Resource Limits

- Define limits for concurrent requests, active database operations, message-processing workers, and large payloads where needed.
- Use bounded queues and backpressure for sustained or bursty workloads.
- Avoid retry storms and excessive parallel requests during dependency degradation.
- Configure timeouts and connection pools according to the capacity of downstream systems.
- Release resources promptly when operations complete or fail.
- Handle overload predictably instead of allowing resource exhaustion.
- Align application concurrency with container, process, and infrastructure limits.
- Do not increase resource limits without evaluating the impact on dependent services.

## Validation

### Benchmarking Tools

- Use the repository's existing benchmarking tools where available.
- For .NET microbenchmarks, consider BenchmarkDotNet when isolated performance comparisons are needed.
- Use application metrics and distributed traces to investigate production-like bottlenecks.
- Measure representative workloads before and after significant optimizations.
- Keep benchmark conditions consistent when comparing results.
- Avoid treating microbenchmark improvements as proof of end-to-end system improvements.
- Do not introduce a benchmarking dependency unless it is justified by the task.

### Load Testing Strategy

- Test normal, peak, and burst workloads where required.
- Include realistic payload sizes, concurrency, and dependency behavior.
- Measure latency percentiles, throughput, error rate, CPU, memory, and dependency saturation.
- Include sustained tests when memory leaks, resource exhaustion, or queue growth are concerns.
- Test recovery after temporary dependency failures and traffic spikes when relevant.
- Validate backpressure and bounded concurrency under overload.
- Avoid running disruptive load tests against production without explicit authorization and an approved plan.
- Document test environment, workload assumptions, and limitations.

### Regression Thresholds

- Establish performance regression thresholds based on a measured baseline and service requirements.
- Compare relevant latency percentiles, throughput, allocation rates, memory usage, and error rates.
- Account for measurement noise and environment differences.
- Require additional investigation when a change produces a material regression in a critical path.
- Do not invent universal percentage thresholds without an agreed baseline and measurement methodology.
- Record benchmark results for performance-sensitive changes when practical.
- Do not claim a performance improvement without supporting measurements.

## Implementation Guidance

Before making performance-sensitive changes:

1. Identify the affected service, operation, and expected workload.
2. Inspect existing metrics, traces, query patterns, and resource usage.
3. Establish a baseline when practical.
4. Identify the bottleneck and evaluate simpler alternatives.
5. Implement the smallest change that addresses the measured problem.
6. Verify correctness, tenant isolation, and consistency.
7. Run relevant tests and compare performance against the baseline.
8. Document trade-offs, workload assumptions, and any remaining limitations.

Do not introduce speculative optimizations, arbitrary performance targets, or additional infrastructure without a clear technical justification.
