
# Concurrency Rules

## Purpose
Ensure correctness when operations execute concurrently.

## Rules
- Identify shared mutable state.
- Avoid race conditions and unsafe synchronization.
- Use transactions or concurrency controls where appropriate.
- Define behavior for duplicate and retried operations.
- Avoid holding locks across unnecessary I/O.
- Respect cancellation and resource lifetime requirements.

## Concurrency Strategy
- Locking and synchronization: TODO
- Optimistic concurrency: TODO
- Distributed coordination: TODO
- Idempotency strategy: TODO

## Failure Handling
- Timeout policy: TODO
- Retry policy: TODO
- Deadlock prevention: TODO

## Validation
- Concurrency tests: TODO
- Stress testing: TODO