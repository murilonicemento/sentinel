# API Design Rules

## Purpose

Define consistent, secure, predictable, and maintainable API contracts across all Sentinel services.

## API Conventions

- API style: RESTful HTTP APIs.
- Base path: `/api/<service-name>`.
- API versioning strategy: URL path versioning using `/api/v1/<service-name>`.
- Route names must use lowercase kebab-case.
- Request and response payloads must use JSON unless HTTP semantics require an empty response.
- Use explicit request and response DTOs.
- Do not expose domain entities or persistence models directly through public API contracts.
- Keep API contracts independent of internal implementation details.

### Tenant Context

- Requests must provide tenant context whenever required by the operation's contract.
- Operations that do not require a request body, including GET operations, must not be forced to carry `tenantId` in a JSON body.
- The client must provide the tenant identifier through the endpoint's documented path, query parameter, or request header.
- Use a consistent tenant identification and propagation strategy across all services.
- The tenant context must be validated against the authenticated identity and its authorized tenant memberships.
- Never trust a client-provided `tenantId` as proof of authorization.
- Propagate only validated tenant context to application and data-access layers.
- Reject requests that attempt to access resources belonging to an unauthorized tenant.
- Ensure tenant isolation across database queries, caches, background processing, and inter-service communication where applicable.

## HTTP Semantics

- Use HTTP methods according to their intended semantics.
- Use appropriate HTTP status codes.
- GET operations must not modify application state.
- Use POST, PUT, PATCH, and DELETE according to their intended operation semantics.
- Use PUT for complete resource replacement and PATCH for partial updates when supported by the resource contract.
- Return consistent validation and error responses.
- Ensure retryable operations are safe to retry when required.
- Use idempotency mechanisms for operations that require protection against duplicate execution.
- Use pagination for endpoints returning collections.
- Avoid response bodies for operations that explicitly return `204 No Content`.
- Do not return successful HTTP status codes for failed operations.
- Document expected HTTP status codes for every endpoint.
- Do not expose internal implementation details through HTTP responses.

## Contracts

### Request Validation

- Validate requests explicitly without third-party validation libraries unless an exception is approved.
- Validate required fields, formats, ranges, and business constraints at the appropriate application boundaries.
- Do not rely exclusively on frontend validation.
- Return consistent validation errors for invalid requests.
- Avoid duplicating the same business validation across layers without a clear reason.
- Reject malformed requests and unsupported parameter values consistently.
- Validate query parameters, path parameters, headers, and request bodies according to the endpoint contract.

### Success Response

Use the following envelope when a response body is required and the endpoint contract adopts the standard response format:

```csharp
public record ResponseBaseDTO<T>
{
    public int StatusCode { get; set; }
    public bool Success { get; set; }
    public required T Data { get; set; }
}
```

Rules:

- `Success` must be true for successful responses.
- `StatusCode` must match the actual HTTP status code.
- `Data` must represent the documented response payload.
- Define endpoint-specific behavior for empty results and operations that return no content.
- Do not use the success envelope for responses that explicitly return `204 No Content`.
- Keep the response structure consistent across services.
- Avoid adding redundant metadata unless it provides clear value to API consumers.

### Error Response

Use a consistent error contract across services. Evaluate RFC 9457 Problem Details as the preferred standard for HTTP error responses before finalizing the public error contract.

If a custom error DTO is adopted, use the following proposed structure:

```csharp
public record ResponseErrorBaseDTO<T>
{
    public string Title { get; set; }
    public string Type { get; set; }
    public int StatusCode { get; set; }
    public bool Success { get; set; }
    public required T Errors { get; set; }
}
```

Rules:

- `Success` must be false for failed operations.
- `StatusCode` must match the actual HTTP status code.
- Errors must not expose stack traces, secrets, internal implementation details, or sensitive information.
- Validation errors must identify the relevant fields when applicable.
- Use stable, machine-readable error codes that identify specific application error conditions.
- HTTP status codes must represent the HTTP-level outcome and must not be used as the sole application error identifier.
- Error codes must use a consistent naming convention, such as `VALIDATION_ERROR`, `RESOURCE_NOT_FOUND`, and `TENANT_ACCESS_DENIED`.
- Document error codes and their meanings.
- Use a consistent error structure across services.
- Do not expose database errors or internal exception messages directly to API consumers.
- Include request or correlation identifiers in error responses when appropriate, without exposing sensitive information.

### Pagination and Filtering

- All collection endpoints must support pagination.
- Use `page` as the page number parameter and `pageSize` as the page size parameter.
- Page numbering must start at `1`.
- The default page size is `10` items.
- The maximum page size is `50` items.
- Reject or normalize invalid pagination parameters according to a consistent policy.
- Use consistent pagination parameter names across services.
- Paginated responses must provide the current page, page size, total item count, and total page count when the endpoint can reliably calculate these values.
- Provide filtering where supported by the resource's requirements.
- Validate filter fields and values.
- Do not expose unrestricted dynamic query expressions.
- Apply tenant and authorization filters before returning paginated results.
- Use deterministic sorting to ensure stable pagination.
- Document pagination and filtering behavior in OpenAPI.

### Sorting

- Define deterministic default sorting for each collection endpoint.
- Sort numeric fields in ascending order by default, from lowest to highest, unless the resource specifies otherwise.
- Sort textual fields alphabetically by default when appropriate for the resource.
- Document the field and collation behavior used for alphabetical sorting where relevant.
- Support explicit sorting parameters only for approved fields.
- Use a consistent sorting parameter convention across services.
- Support descending order through an explicit sorting parameter when the endpoint permits it.
- Include a stable tie-breaker, such as a unique identifier, when needed for consistent pagination.
- Reject unsupported sorting fields or handle them according to a documented, consistent policy.
- Document supported sorting fields and default sorting behavior in OpenAPI.

### Date, Time, and Timezone

- Use ISO 8601-compatible date and time representations.
- Represent timestamps as ISO 8601 date-time strings.
- Use UTC (`Z`) for timestamps representing absolute instants, unless the contract explicitly requires an offset.
- Use `YYYY-MM-DD` for date-only values.
- Do not use ambiguous locale-dependent date formats.
- Use `America/Sao_Paulo` as the default business timezone for operations that depend on Brazilian local time, unless the business domain explicitly requires another timezone.
- Do not convert date-only values between timezones.
- Convert local business times to absolute timestamps using the applicable timezone rules when necessary.
- Document timezone expectations for endpoints involving scheduling, reporting, deadlines, or other time-sensitive business operations.

## Compatibility

### Backward Compatibility

- Preserve backward compatibility for existing API consumers whenever feasible.
- Prefer additive changes that do not alter the existing behavior or meaning of the API contract.
- Treat a change as breaking when it can require existing consumers to modify their requests, response handling, or business logic to continue working.
- The following changes are considered breaking:
  - Removing or renaming an endpoint, request field, response field, or query parameter.
  - Changing the data type or meaning of an existing field.
  - Making an optional request field required.
  - Changing existing validation rules in a way that rejects previously valid requests.
  - Changing the meaning of existing HTTP status codes or application error codes.
  - Changing authentication, authorization, or tenant-isolation requirements in a way that affects existing consumers.
  - Removing or changing existing behavior that consumers may rely on.
  - Changing the format or structure of an existing response in a way that breaks consumers.
- Adding optional request fields or response fields may be considered backward-compatible, provided existing consumers can tolerate the change.
- Adding new values to enums or changing response structures must be evaluated for compatibility with existing clients.
- Changes to default behavior must be evaluated for compatibility even when the request and response schemas remain unchanged.
- Every breaking change must be reviewed and approved before implementation or release.
- Breaking changes must include a migration plan and an explicit communication strategy for affected consumers.
- Automated contract checks should be used to detect incompatible changes whenever tooling supports them.

### Deprecation Policy

- Do not remove or disable a public API endpoint, field, parameter, or behavior without a documented deprecation process.
- Document deprecated elements in the OpenAPI specification using `deprecated: true` where supported.
- Deprecation documentation must identify the affected element, the reason for deprecation, the recommended replacement, and migration instructions.
- The default deprecation period is 90 calendar days from the official announcement.
- The API owner must notify affected consumers and coordinate migration before removal.
- The deprecation period may be extended or shortened only with documented justification and approval from the responsible service owner and affected stakeholders.
- Before removal, verify that affected consumers have migrated or that the responsible stakeholders have explicitly approved the removal.
- Update API documentation and release notes throughout the deprecation process.
- When appropriate and supported by the infrastructure, expose deprecation and planned removal information through HTTP response headers.
- Record deprecation and removal decisions in the relevant service documentation.
- Security incidents, legal requirements, or urgent operational risks may require an expedited deprecation process with documented approval and communication.

### Versioning Policy

- Use URL path versioning for public API contract versions.
- The standard route format is `/api/v1/<service-name>`.
- Start new APIs at version `v1` unless a different initial version is explicitly approved.
- Increment the major API version when a breaking change cannot be introduced compatibly.
- Backward-compatible changes must not require a new major API version.
- When a new major version is released, provide migration documentation and maintain the previous version during its approved deprecation period.
- Multiple major versions may coexist temporarily when required to support existing consumers.
- Each supported API version must have an accurate OpenAPI specification.
- API versioning is independent of the application's internal release version.
- The service owner is responsible for documenting version support status, migration requirements, and planned retirement dates.
- Do not introduce a new API version solely for internal implementation changes that do not affect the public contract.

## API Documentation

- Document APIs using OpenAPI.
- Use Scalar as the API documentation interface.
- Document authentication requirements for JWT and API Key schemes.
- Provide request and response examples for every endpoint.
- Document success responses, error responses, status codes, validation rules, and relevant parameters.
- Document pagination, filtering, sorting, and timezone behavior where applicable.
- Document tenant context requirements and authorization expectations without exposing sensitive security implementation details.
- Keep documentation synchronized with implemented contracts.
- Do not include real credentials, tokens, or sensitive data in examples.
- Document deprecated endpoints and fields, replacement options, and migration guidance.
- Maintain separate OpenAPI specifications for each supported major API version when necessary.

## Security

### Authentication

- Support JWT and API Key authentication according to the endpoint's documented requirements.
- Define which authentication scheme is permitted for each endpoint or service.
- Validate JWT signature, issuer, audience, expiration, and applicable claims.
- Validate API keys securely and support revocation or rotation.
- Never accept credentials from undocumented or insecure locations.
- Never log authentication credentials, access tokens, or API keys.
- Store and compare API keys using secure practices appropriate to the authentication system.
- Use HTTPS for API communication outside explicitly approved local development scenarios.

### Authorization

- Validate the authenticated identity's permissions for every protected operation.
- Validate tenant access independently of client-provided values.
- Enforce authorization policies and permissions on the server.
- Apply resource-level authorization where necessary.
- Follow the principle of least privilege.
- Deny access by default when authorization cannot be established.
- Prevent cross-tenant data access through queries, caches, background jobs, and inter-service calls where applicable.
- Do not rely on client-side checks to enforce authorization.
- Ensure authorization failures do not disclose sensitive resource information.

### Rate Limiting

- Define rate limits by endpoint, client, identity, or tenant according to the risk and workload.
- Apply stricter limits to authentication, resource-intensive, and abuse-prone endpoints where appropriate.
- Return HTTP `429 Too Many Requests` when a request exceeds its applicable rate limit.
- Use consistent rate limit policies across services, with documented exceptions where necessary.
- Document applicable rate limit response headers, such as `Retry-After` and any standardized rate limit headers supported by the platform.
- Define trusted client identification and proxy handling.
- Use authenticated identity or validated tenant context for rate limit attribution where appropriate.
- Trust forwarded client IP headers only when they are supplied by explicitly trusted proxies.
- Do not trust arbitrary client-provided headers as proof of identity or origin.
- Keep rate limit thresholds configurable and monitor their effectiveness.

## Validation and Testing

- Define and maintain an API contract testing strategy.
- Validate request and response schemas where tooling supports it.
- Test authentication, authorization, and tenant isolation.
- Test pagination, filtering, sorting, and error responses.
- Test compatibility for changes to public contracts.
- Ensure API documentation reflects the actual implementation.
- Include positive, negative, and boundary-value test cases for API contracts.
- Test expected HTTP status codes and response structures.
- Test retry behavior and idempotency where applicable.
- Test deprecated API behavior during the approved deprecation period.
- Validate OpenAPI specifications in the CI pipeline.
- Run automated contract compatibility checks when supported by the selected tooling.
- Required automated checks: build validation, automated tests, OpenAPI validation, authorization and tenant-isolation tests, and API contract compatibility checks where tooling supports them.
- Document exceptions to these rules and obtain approval from the responsible service owner.
