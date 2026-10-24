# Security Rules

## Purpose

Prevent security vulnerabilities, unauthorized access, data exposure, and unsafe implementation choices across the microservices architecture.

## General Rules

- Never hardcode secrets, tokens, passwords, private keys, or credentials in source code, configuration files, tests, or documentation.
- Never expose secrets in logs, exceptions, HTTP responses, traces, or error messages.
- Validate untrusted input at appropriate application boundaries.
- Enforce authentication and authorization on the server side.
- Apply the principle of least privilege to users, services, databases, storage, and infrastructure.
- Use parameterized database queries and safe data-access APIs.
- Treat client input, external provider responses, uploaded files, message payloads, and persisted data as untrusted until validated as appropriate.
- Do not disable security controls to bypass errors or simplify implementation.
- Do not introduce custom cryptographic algorithms or authentication protocols.
- Prefer established platform security mechanisms and maintained libraries.
- Do not expose internal implementation details or sensitive infrastructure information to external clients.
- Follow the repository's established security architecture and organizational policies.
- Do not weaken existing security controls without explicit authorization and a documented justification.

## Authentication and Authorization

### Authentication Strategy

- Use the identity provider and authentication mechanisms explicitly configured for the platform.
- Prefer established standards such as OAuth 2.0 and OpenID Connect when appropriate to the application and identity provider.
- Validate access tokens using a trusted authentication library and the configured issuer, audience, signature, and lifetime requirements.
- Validate relevant token claims and accepted signing algorithms according to the identity provider's configuration.
- Never trust an unverified token or decode a token and treat its claims as authenticated identity.
- Reject expired, malformed, invalid, or otherwise unacceptable credentials.
- Do not implement custom password storage, token signing, or credential verification mechanisms when established identity infrastructure is available.
- Never log access tokens, refresh tokens, passwords, or authorization headers containing credentials.

### Authorization Model

- Enforce authorization on every protected operation, including internal service endpoints.
- Apply deny-by-default authorization where access has not been explicitly granted.
- Validate permissions against the authenticated identity and the requested resource or operation.
- Distinguish authentication failures from authorization failures.
- Enforce tenant isolation for every tenant-scoped operation.
- Never trust a client-supplied `tenantId`, user ID, role, or permission as proof of authorization.
- Derive tenant context from authenticated identity and validated membership or authorization rules.
- Prevent insecure direct object reference vulnerabilities by verifying access to the requested resource, not merely its identifier.
- Apply authorization to background jobs and message consumers when they perform tenant-scoped or privileged operations.
- Use service identities and narrowly scoped permissions for service-to-service communication.
- Do not assume that internal network access makes a request trustworthy.
- Avoid exposing whether another tenant's protected resource exists.

### Session and Token Handling

- Store credentials and tokens only in appropriate protected storage.
- Use short-lived access tokens according to the platform's identity policy.
- Protect refresh tokens and implement rotation or revocation mechanisms when supported and required.
- Validate token issuer, audience, expiration, and other required claims.
- Do not place access tokens or secrets in URLs.
- Use HTTPS for authenticated communication.
- Protect browser-based sessions with appropriate cookie attributes, including `Secure`, `HttpOnly`, and suitable `SameSite` settings.
- Implement CSRF protections for browser-based authentication flows when applicable.
- Do not assume cookie-based and bearer-token authentication have identical CSRF requirements.
- Never log or return tokens as part of debugging output.

## Data Protection

### Sensitive Data Classification

- Identify sensitive data before storing, transmitting, logging, or exposing it.
- Apply the organization's data classification and retention policies.
- Minimize the collection and retention of personal, confidential, and security-sensitive information.
- Avoid duplicating sensitive data across services and storage systems without a justified requirement.
- Ensure tenant-owned data remains isolated across all storage technologies.
- Apply appropriate deletion and retention procedures to databases, object storage, caches, search indexes, and derived data.
- Do not treat derived indexes, cached values, or exported files as exempt from data protection requirements.

### Encryption Requirements

- Use TLS for network communication involving credentials, sensitive data, or protected service traffic.
- Validate certificates and use secure transport configurations.
- Do not disable certificate validation to work around connection errors.
- Use approved secret-management mechanisms, such as Azure Key Vault where configured, for production secrets.
- Prefer managed identities or workload identities over long-lived credentials when supported by the deployment environment.
- Protect stored sensitive data using the encryption capabilities supported by the selected database, storage platform, and infrastructure.
- Evaluate encryption-at-rest requirements for PostgreSQL, MongoDB, Redis, MinIO, Elasticsearch, backups, and exported data.
- Do not implement custom encryption when established platform mechanisms meet the requirements.
- Store encryption keys separately from the data they protect.
- Define key access, rotation, and revocation procedures according to organizational policy.
- Do not assume that encryption at rest replaces access control or tenant isolation.

### Logging Restrictions

- Never log passwords, API keys, access tokens, private keys, or connection strings containing secrets.
- Avoid logging complete request bodies, response bodies, message payloads, or uploaded file contents by default.
- Redact sensitive headers and fields before logging.
- Avoid exposing personal data, tenant-specific confidential data, or authentication claims unnecessarily.
- Do not include raw SQL parameters or provider responses in logs when they may contain sensitive information.
- Ensure error responses and distributed traces do not expose secrets or sensitive payloads.
- Apply appropriate access controls and retention policies to security logs.
- Record security-relevant events without recording the credentials or sensitive content involved.

## Threat Prevention

### SQL and NoSQL Injection

- Use parameterized queries and established ORM APIs.
- In EF Core, prefer LINQ queries and parameterized SQL APIs.
- Never concatenate untrusted input into SQL statements.
- When raw SQL is necessary, parameterize values and strictly validate dynamic identifiers that cannot be parameterized.
- Do not assume that MongoDB query objects are safe when built from arbitrary client-supplied structures.
- Construct database filters explicitly rather than passing untrusted query operators or documents directly to the database.
- Validate sort fields, filters, field projections, and pagination parameters against allowed values.
- Apply least-privilege database permissions.
- Avoid exposing database error details to clients.

### Cross-Site Scripting and Cross-Site Request Forgery

- Encode untrusted content according to its output context.
- Use established framework protections against cross-site scripting.
- Avoid rendering untrusted content as executable HTML or JavaScript.
- Sanitize HTML only when rendering user-provided HTML is an explicit requirement.
- Use Content Security Policy and appropriate security headers for browser-facing applications where applicable.
- Implement CSRF protections for cookie-authenticated state-changing operations.
- Do not rely on CORS as an authorization mechanism or a substitute for CSRF protection.
- Restrict allowed origins to the required origins rather than allowing arbitrary origins with credentials.
- Avoid unsafe DOM manipulation and equivalent rendering patterns in frontend code.

### SSRF and Unsafe Network Requests

- Validate and constrain URLs used for outbound requests.
- Prefer configured, trusted provider endpoints over arbitrary client-supplied URLs.
- Restrict outbound network access where infrastructure controls support it.
- Prevent requests to unauthorized internal services, loopback addresses, link-local addresses, and cloud metadata endpoints.
- Revalidate resolved destinations and redirects when needed to prevent bypasses through DNS resolution or redirect behavior.
- Apply timeouts, response-size limits, and appropriate content-type validation to external requests.
- Do not automatically follow untrusted redirects without evaluating the security implications.
- Avoid exposing internal network topology or provider credentials through error messages.

### File and Object Storage Security

- Treat uploaded files and external data files as untrusted.
- Validate file size, allowed content types, file signatures where appropriate, and expected formats.
- Do not trust client-supplied filenames, extensions, or content types.
- Generate safe object keys instead of using raw user-supplied paths.
- Prevent path traversal and unsafe filesystem operations.
- Do not execute uploaded files or treat them as trusted configuration or code.
- Apply least-privilege bucket and object permissions in MinIO.
- Use short-lived, appropriately scoped presigned URLs when required.
- Do not expose unrestricted object-storage credentials to clients.
- Apply malware scanning when required by the file types and threat model.
- Define safe handling for partial uploads, failed deletions, and orphaned objects.
- Ensure file access is authorized against the owning tenant and resource.

### API and Abuse Prevention

- Apply rate limiting to sensitive, expensive, or abuse-prone endpoints.
- Define request-size limits and appropriate timeouts.
- Validate pagination limits and prevent unbounded queries.
- Apply concurrency limits to expensive operations and external provider calls.
- Use quotas or throttling where required by service capacity or business rules.
- Avoid exposing detailed authentication or account-enumeration information.
- Protect password recovery, authentication, and other sensitive flows against abuse when applicable.
- Ensure rate limits and quotas account for the appropriate identity or tenant context.
- Do not rely exclusively on client-side validation or rate limiting.
- Return appropriate status codes for rejected, throttled, or oversized requests.

### Messaging and Microservice Security

- Authenticate and authorize service-to-service communication according to the platform's security model.
- Restrict access to message brokers, topics, queues, and consumer groups.
- Validate incoming messages before processing them.
- Do not assume that a message is trustworthy merely because it originated from an internal broker.
- Enforce tenant isolation when processing messages and performing downstream operations.
- Avoid placing credentials or unnecessary sensitive data in message payloads.
- Ensure retries and dead-letter workflows do not leak sensitive information.
- Prevent unauthorized message replay from triggering privileged operations.
- Make security-sensitive operations idempotent where duplicate delivery is possible.
- Use separate, least-privilege identities for services and background workers where supported.

## Security Validation

### Required Security Checks

Before completing security-sensitive changes, verify the applicable requirements:

- Authentication and authorization behavior.
- Tenant isolation and resource-level access control.
- Input validation and injection resistance.
- Secret handling and sensitive-data redaction.
- Dependency vulnerability findings.
- Secure HTTP and outbound request configuration.
- File upload and object-storage authorization.
- Rate limiting and request-size restrictions.
- Database and infrastructure permissions.
- Security-relevant logging and error responses.
- Regression tests for previously fixed vulnerabilities.

### Vulnerability Scanning Tools

- Use existing dependency auditing and security scanning tools configured by the repository.
- For .NET dependencies, use supported NuGet vulnerability auditing commands, including `dotnet list package --vulnerable --include-transitive` where supported by the installed SDK.
- Run established static analysis, secret scanning, and container image scanning tools when available.
- Review findings based on severity, exploitability, exposure, and remediation availability.
- Do not introduce new security scanning tools without justification and approval.
- Do not suppress or dismiss findings without documented justification and the required authorization.
- Do not claim a security scan passed if it was not executed or if its result was inconclusive.

### Security Incident Escalation

- Treat suspected credential exposure, unauthorized access, cross-tenant data exposure, and active exploitation as security incidents.
- Follow the organization's incident response and escalation procedures.
- Notify the designated security or engineering authority promptly.
- Preserve relevant logs and diagnostic evidence without unnecessarily copying sensitive data.
- Do not attempt destructive cleanup or evidence alteration.
- Rotate or revoke exposed credentials through the approved incident response process.
- Document affected services, potential exposure, mitigation actions, and remaining risks.
- Do not independently disclose incidents externally or make unsupported claims about their impact.
- If no incident response procedure is documented, report the gap and escalate to the responsible maintainer.

## Implementation Guidance

Before implementing a security-sensitive change:

1. Identify the assets, trust boundaries, and relevant threat scenarios.
2. Inspect existing authentication, authorization, validation, and secret-management mechanisms.
3. Reuse established platform security controls.
4. Apply least privilege and minimize exposed data.
5. Add tests for unauthorized access, invalid input, and relevant attack scenarios.
6. Run applicable security and dependency checks.
7. Document unresolved risks, required approvals, and operational follow-up.

Do not invent identity-provider settings, security approvals, or organizational policies. When a security-critical requirement is undefined, preserve existing protections and request an explicit architectural decision rather than weakening security.
