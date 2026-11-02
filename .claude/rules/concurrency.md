# Concurrency Rules

## Purpose

Ensure correctness, consistency, and predictable behavior when operations execute concurrently across Sentinel services, database transactions, background workers, and distributed components.

## General Rules

- Identify shared mutable state and define how concurrent access is controlled.
- Avoid race conditions, unsafe synchronization, and unnecessary shared state.
- Use database transactions and concurrency controls when required to preserve data consistency.
- Define behavior for duplicate requests, retried operations, and concurrently processed messages.
- Avoid holding locks while performing unnecessary I/O or long-running operations.
- Respect cancellation tokens, timeout limits, and resource lifetime requirements.
- Do not assume that in-memory synchronization protects resources across multiple service instances.
- Preserve tenant isolation when accessing shared resources concurrently.

## Concurrency Strategy

### Locking and Synchronization

- Prefer thread-safe data structures and asynchronous coordination primitives when appropriate.
- Use `lock` for short, synchronous critical sections involving in-process state.
- Do not use `lock` around `await` operations or long-running I/O.
- Use `SemaphoreSlim` when asynchronous in-process coordination is required.
- Ensure every acquired lock or synchronization primitive is released reliably, including when exceptions occur.
- Avoid nested locks when possible. If multiple locks are necessary, acquire them in a consistent order.
- Do not use in-memory locks to coordinate operations across multiple service instances.

### Optimistic Concurrency

- Prefer optimistic concurrency when conflicts are uncommon and operations can detect conflicting updates.
- Use database concurrency tokens, version columns, or conditional updates where appropriate.
- Detect concurrency conflicts and handle them explicitly rather than silently overwriting changes.
- Retry a conflicting operation only when the operation can be safely repeated.
- Use appropriate database constraints to enforce invariants that must hold under concurrent writes.

### Distributed Coordination

- Do not rely on process-local locks for distributed coordination.
- Prefer database constraints, atomic Redis operations, or other appropriate distributed mechanisms when multiple instances can modify the same state.
- Define lock ownership, expiration, and recovery behavior when distributed locks are necessary.
- Avoid distributed locks when an atomic operation, transaction, or uniqueness constraint provides a simpler solution.
- Ensure coordination mechanisms remain safe when a process crashes, a lease expires, or a network request times out.
- Preserve tenant boundaries when constructing distributed coordination keys.

### Idempotency Strategy

- Define idempotency requirements for operations that may be retried or delivered more than once.
- Use stable operation identifiers or idempotency keys where appropriate.
- Enforce uniqueness at the database or persistence layer when duplicate processing must be prevented.
- Treat message delivery as potentially repeated and make consumers safe against duplicate events.
- Use the existing outbox and deduplication mechanisms where applicable.
- Define how long idempotency records must be retained and what happens when the retention period expires.
- Do not assume that a timeout means the original operation failed. Its outcome may be unknown.

## Failure Handling

### Timeout Policy

- Use 60 seconds as the default upper bound for operations that require a timeout, unless a more specific limit is defined.
- Configure timeouts according to the operation type, external provider, and expected latency.
- Use shorter timeouts for fast operations such as cache access when appropriate.
- Ensure timeout settings are compatible across service calls so that downstream operations do not outlive the caller's deadline unnecessarily.
- Propagate cancellation tokens through asynchronous calls whenever supported.
- Distinguish timeouts from caller cancellation and other failures.

### Retry Policy

- Use exponential backoff for retryable transient failures.
- Allow a maximum of 3 attempts per operation, including the initial attempt.
- Apply retries only to failures classified as transient.
- Retry only when repeating the operation is safe or when idempotency is enforced.
- Add jitter where appropriate to prevent synchronized retries across service instances.
- Do not retry validation errors, authorization failures, or other permanent failures without a specific recovery strategy.
- Avoid multiplying retries across service layers. Define retry ownership to prevent excessive downstream requests.
- Respect cancellation, the overall operation deadline, and external provider rate limits.
- For message consumers, configure retry behavior and dead-letter handling according to the messaging infrastructure and the operation's failure mode.

### Deadlock Prevention and Recovery

- Keep database transactions and critical sections as short as practical.
- Acquire multiple locks or resources in a consistent order.
- Avoid unnecessary nested locks and blocking waits in asynchronous code.
- Use appropriate transaction isolation levels and database indexes to reduce unnecessary contention.
- Detect and handle database deadlock or serialization failures when the database reports them.
- Retry an aborted transaction only when the operation is safe to repeat and the retry policy allows it.
- Do not assume thread synchronization primitives alone prevent database or distributed deadlocks.

## Validation

### Concurrency Tests

- Test concurrent requests that update the same resource.
- Verify that uniqueness constraints and business invariants remain valid under concurrent execution.
- Test duplicate message delivery and repeated requests.
- Verify that concurrent operations cannot bypass tenant isolation.
- Test cancellation, timeout, and retry behavior where relevant.

### Stress Testing

- Stress-test high-contention operations and critical shared resources.
- Evaluate throughput, latency, resource consumption, and failure rates under concurrent load.
- Include realistic scenarios involving multiple service instances and message consumers.
- Verify that retries do not amplify load uncontrollably during dependency failures.
- Document observed bottlenecks and validate fixes with repeatable tests.

## Implementation Guidance

- Follow existing concurrency patterns and infrastructure before introducing new synchronization mechanisms.
- Prefer the simplest mechanism that preserves the required correctness guarantees.
- Document important concurrency assumptions, especially where correctness depends on database constraints, message delivery behavior, or distributed coordination.
- Add automated tests for concurrency-sensitive behavior when modifying the relevant code.
- Do not introduce distributed locks, custom retry frameworks, or additional infrastructure without a clear need.
