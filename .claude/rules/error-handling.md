# Error Handling Rules

## Purpose

Establish consistent error classification, handling, logging, and failure propagation across all microservices.

## Error Classification

### Validation Errors

- Represent requests that are malformed, incomplete, or contain invalid field values.
- Return HTTP `400 Bad Request` for invalid request data.
- Include actionable field-level validation details when appropriate.
- Do not expose internal validation implementation details.
- Reject invalid input before executing operations that depend on it.

### Business Rule Violations

- Represent operations that violate domain invariants or business rules.
- Use domain-specific results or exceptions according to the established project pattern.
- Map business errors to appropriate HTTP status codes.
- Use `409 Conflict` for conflicts with the current resource state.
- Use `422 Unprocessable Content` when the request is syntactically valid but cannot be processed because of its semantic content, where appropriate.
- Do not use HTTP `500 Internal Server Error` for expected business rule violations.
- Do not expose internal domain details that are not intended for clients.

### Authentication and Authorization Errors

- Return HTTP `401 Unauthorized` when authentication is missing or invalid.
- Return HTTP `403 Forbidden` when the authenticated identity lacks permission to perform the operation.
- Never treat an untrusted tenant identifier or client-supplied identity claim as proof of authorization.
- Avoid responses that reveal whether protected resources or other tenants' data exist.
- Do not log credentials, access tokens, session secrets, or other authentication material.

### Infrastructure Failures

- Include database failures, message broker unavailability, storage failures, network errors, timeouts, and failures from external providers.
- Handle transient failures using the retry, timeout, and resilience policies established for the affected dependency.
- Return HTTP `503 Service Unavailable` when the service cannot fulfill a request because a required dependency is temporarily unavailable.
- Return HTTP `504 Gateway Timeout` when the service is acting as a gateway and an upstream dependency fails to respond in time.
- Use other appropriate status codes when they better represent the failure.
- Do not expose infrastructure details, connection strings, internal hostnames, or provider diagnostics to external clients.
- Preserve the original exception and diagnostic context in internal logs when appropriate.
- Do not retry non-transient failures or operations that are unsafe to repeat.

### Unexpected Errors

- Represent unanticipated failures that cannot be safely handled by the application.
- Return HTTP `500 Internal Server Error` for unexpected server-side failures.
- Log sufficient diagnostic context for investigation.
- Do not expose stack traces, exception messages, internal types, SQL statements, or implementation details to external clients.
- Do not silently convert unexpected failures into successful responses or empty results.
- Allow the centralized error-handling mechanism to produce a safe response when recovery is not possible.

## Error Handling Rules

- Never silently swallow exceptions.
- Do not use exceptions for ordinary control flow.
- Catch exceptions only when the current layer can recover, translate, enrich, or handle the failure meaningfully.
- Avoid catching generic exceptions without a clear recovery or propagation strategy.
- Preserve the original exception as the inner exception when wrapping it.
- Never discard the original diagnostic context when translating an exception.
- Handle errors at the appropriate responsibility boundary.
- Keep domain and application layers independent of HTTP-specific response types.
- Translate internal failures into transport-specific responses at the API or messaging boundary.
- Use cancellation tokens and propagate cancellation through asynchronous operations where supported.
- Do not treat expected request cancellation as an unexpected application failure.
- Do not retry an operation after a non-transient failure.
- Ensure retries cannot cause duplicate side effects.
- Do not return success when a required operation has failed.
- Avoid exposing internal exception messages directly to clients.

## Error Representation

### Internal Error Model

- Use the established application error model or result pattern when representing expected failures.
- Prefer explicit error types or stable error codes over string matching against exception messages.
- Domain errors must represent domain rules and remain independent of HTTP status codes.
- Application errors must communicate failure meaning without depending on infrastructure implementation details.
- Infrastructure-specific exceptions must be translated at an appropriate application or infrastructure boundary.
- Do not introduce a new error abstraction when an existing project abstraction already meets the requirements.

### Public HTTP Error Response

- Use RFC 9457 Problem Details for HTTP API errors.
- Use ASP.NET Core Problem Details support where appropriate.
- Return the correct HTTP status code independently of the descriptive error text.
- Use a consistent response structure across services.
- Include standard Problem Details fields where applicable:
  - `type`: A URI identifying the problem type.
  - `title`: A short, stable summary of the problem.
  - `status`: The HTTP status code.
  - `detail`: A safe, client-facing explanation.
  - `instance`: An identifier for the specific occurrence, when appropriate.
- Add stable extension fields when needed, such as `code`, `traceId`, and structured validation errors.
- Do not expose stack traces, raw exception messages, database details, secrets, or internal implementation data.
- Do not return an HTTP success status for failed operations.
- Do not create different public error formats for individual endpoints without an explicit requirement.

### Error Codes and Correlation Identifiers

- Assign stable, machine-readable application error codes to errors that clients need to identify.
- Use consistent naming conventions for error codes across the platform.
- Do not use exception messages or localized display text as machine-readable identifiers.
- Preserve correlation and tracing context across service boundaries whenever possible.
- Include a trace identifier in error responses when supported by the request context.
- Use the platform's established tracing and correlation mechanisms rather than creating competing identifiers.
- Do not trust arbitrary client-supplied correlation identifiers without validation.
- Never use tenant identifiers, user identifiers, or other sensitive values as substitutes for trace identifiers.

## Logging

### Logging Requirements

- Use structured logging rather than constructing log messages through string concatenation.
- Include relevant diagnostic context, such as service name, operation, trace identifier, and error code.
- Record exceptions with their stack traces in internal logs when useful for diagnosis.
- Log unexpected failures at the appropriate error level.
- Log recoverable transient failures at a level appropriate to their operational impact.
- Avoid logging the same exception repeatedly at every layer.
- Log an exception where it is handled or where it becomes an actionable failure.
- Include retry attempt information when retrying an operation.
- Record exhausted retries, failed background jobs, and messages moved to a dead-letter queue.
- Avoid logging expected validation or business errors as unexpected system failures.
- Ensure logs support troubleshooting without exposing unnecessary sensitive information.

### Sensitive Information Restrictions

- Never log passwords, access tokens, API keys, connection strings containing credentials, or secret values.
- Do not log full payment details, sensitive personal information, or complete request bodies by default.
- Redact sensitive headers, fields, and provider responses before logging.
- Avoid logging raw database queries or parameter values when they may contain sensitive information.
- Apply access controls and retention policies to diagnostic logs.
- Do not include sensitive data in public error messages, trace identifiers, or error codes.

## Asynchronous Messaging and Background Jobs

- Distinguish permanent processing failures from transient failures.
- Retry transient failures according to the established retry policy.
- Use bounded retries with backoff and jitter where appropriate.
- Ensure message processing is idempotent when duplicate delivery is possible.
- Do not acknowledge a message before the required processing or durable failure handling has completed.
- Route messages that exceed the retry policy to the configured dead-letter mechanism when available.
- Preserve message identifiers and relevant correlation context for troubleshooting.
- Record enough diagnostic information to investigate failed background jobs without exposing sensitive payloads.
- Do not retry invalid messages indefinitely.
- Document recovery and replay procedures for failed messages and jobs.

## Validation and Testing

Tests must cover the applicable scenarios below:

- Invalid request data returns the expected validation response.
- Business rule violations map to stable error codes and appropriate HTTP status codes.
- Unauthenticated requests return `401`.
- Unauthorized operations return `403`.
- Unexpected exceptions produce safe `500` responses.
- Infrastructure timeouts and unavailable dependencies produce appropriate responses.
- Public error responses do not expose stack traces, secrets, or internal implementation details.
- Error responses follow the established Problem Details format.
- Correlation identifiers are preserved or propagated where expected.
- Expected cancellation does not generate misleading unexpected-error logs.
- Retry policies do not repeat unsafe side effects.
- Message processing handles duplicate delivery and exhausted retries correctly.
- Sensitive data is excluded or redacted in logs.
- Errors are observable without being logged redundantly at every layer.

## Implementation Guidance

Before changing error handling:

1. Inspect the existing error model, middleware, filters, exception handlers, and logging configuration.
2. Identify the layer responsible for handling or translating the failure.
3. Reuse established error codes and response formats.
4. Preserve diagnostic context while keeping public responses safe.
5. Add tests for expected failures and relevant failure scenarios.
6. Verify that logging, retries, and status codes match the failure's actual meaning.
7. Update API documentation when public error behavior changes.

Do not introduce a new error-handling framework, public response envelope, or exception hierarchy without a clear technical justification.