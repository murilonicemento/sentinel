
# API Design Rules

## Purpose

Define consistent, secure, predictable, and maintainable API contracts across all Sentinel services.

## API Conventions

- API style: RESTful HTTP APIs.
- Base path: `/api/<service-name>`.
- API versioning strategy: TODO.
- Route names must use lowercase kebab-case.
- Request and response payloads must use JSON unless the HTTP semantics require an empty response.
- Use explicit request and response DTOs.
- Do not expose domain entities or persistence models directly through public API contracts.

### Tenant Context

- Requests must provide tenant context whenever required by the operation's contract.
- Operations that do not require a request body, including GET operations, must not be forced to carry `tenantId` in a JSON body.
- The tenant context must be validated against the authenticated identity and its authorized tenant memberships.
- Never trust a client-provided `tenantId` as proof of authorization.
- Reject requests that attempt to access resources belonging to an unauthorized tenant.
- Tenant identification and propagation strategy: TODO.

## HTTP Semantics

- Use HTTP methods according to their intended semantics.
- Use appropriate HTTP status codes.
- GET operations must not modify application state.
- Use POST, PUT, PATCH, and DELETE according to the intended operation semantics.
- Return consistent validation and error responses.
- Ensure retryable operations are safe to retry when required.
- Use pagination for endpoints returning collections.
- Avoid response bodies for operations that explicitly return `204 No Content`.
- Do not return successful HTTP status codes for failed operations.

## Contracts

### Request Validation

- Validate requests explicitly without third-party validation libraries.
- Validate required fields, formats, ranges, and business constraints at the appropriate application boundaries.
- Do not rely exclusively on frontend validation.
- Return consistent validation errors for invalid requests.
- Avoid duplicating the same business validation across layers without a clear reason.

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

### Error Response

Use a consistent error contract across services.

Current proposal:

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
- `Success` must be false.
- `StatusCode` must match the actual HTTP status code.
- Errors must not expose stack traces, secrets, or internal details.
- Validation errors must identify the relevant fields when applicable.
- Define a consistent error code strategy: TODO.
- Evaluate RFC 9457 Problem Details before finalizing the public error contract.

### Pagination and Filtering

- All collection endpoints must support pagination.
- Define default page size and maximum page size.
- Reject or normalize invalid pagination parameters according to a consistent policy.
- Use consistent pagination parameter names across services.
- Provide filtering where supported by the resource's requirements.
- Validate filter fields and values.
- Do not expose unrestricted dynamic query expressions.
- Pagination strategy: TODO.
- Default page size: TODO.
- Maximum page size: TODO.

### Sorting

- Define deterministic default sorting for each collection endpoint.
- Where alphabetical sorting is required, use the appropriate field and a documented collation policy.
- Where numerical sorting is required, define ascending order as the default unless the resource specifies otherwise.
- Support explicit sorting parameters only for approved fields.
- Include a stable tie-breaker, such as a unique identifier, when needed for consistent pagination.
- Sorting parameter conventions: TODO.

### Date, Time, and Timezone

- Use ISO 8601-compatible date and time representations.
- Represent timestamps as ISO 8601 date-time strings.
- Use UTC (`Z`) for timestamps representing absolute instants, unless the contract explicitly requires an offset.
- Use `YYYY-MM-DD` for date-only values.
- Do not use ambiguous locale-dependent date formats.
- Define timezone requirements for business operations: TODO.

## Compatibility

- Avoid breaking existing consumers without an approved migration.
- Prefer additive contract changes when feasible.
- Document deprecated fields and endpoints.
- Define a deprecation period and removal process.
- Breaking change policy: TODO.
- Deprecation policy: TODO.
- Versioning policy: TODO.

## API Documentation

- Document APIs using OpenAPI.
- Use Scalar as the API documentation interface.
- Document authentication requirements for JWT and API Key schemes.
- Provide request and response examples for every endpoint.
- Document success responses, error responses, status codes, validation rules, and relevant parameters.
- Keep documentation synchronized with implemented contracts.
- Do not include real credentials, tokens, or sensitive data in examples.

## Security

### Authentication

- Support JWT and API Key authentication according to the endpoint's documented requirements.
- Define which authentication scheme is permitted for each endpoint or service.
- Validate JWT signature, issuer, audience, expiration, and applicable claims.
- Validate API Keys securely and support revocation or rotation.
- Never accept credentials from undocumented or insecure locations.

### Authorization

- Validate the authenticated identity's permissions for every protected operation.
- Validate tenant access independently of client-provided values.
- Enforce authorization policies and permissions on the server.
- Apply resource-level authorization where necessary.
- Follow the principle of least privilege.
- Deny access by default when authorization cannot be established.

### Rate Limiting

- Define rate limits by endpoint, client, identity, or tenant according to the risk and workload.
- Return HTTP 429 when applicable.
- Define rate limit policies and response headers: TODO.
- Define trusted client identification and proxy handling: TODO.

## Validation

- Define API contract testing strategy: TODO.
- Validate request and response schemas where tooling supports it.
- Test authentication, authorization, and tenant isolation.
- Test pagination, filtering, sorting, and error responses.
- Test compatibility for changes to public contracts.
- Ensure API documentation reflects the actual implementation.
- Required automated checks: TODO.
