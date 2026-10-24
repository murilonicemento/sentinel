# Observability Rules

## Purpose

Make application behavior, performance, and failures diagnosable across all microservices.

## General Principles

- Treat observability as a required part of service implementation.
- Use structured logs, metrics, distributed traces, and health checks where appropriate.
- Follow established platform-wide observability conventions.
- Ensure each service can be diagnosed independently and across service boundaries.
- Instrument important operations, dependencies, background jobs, and asynchronous message processing.
- Avoid unnecessary instrumentation, excessive telemetry volume, and high-cardinality dimensions.
- Do not introduce new observability platforms or agents without an explicit architectural decision.
- Ensure telemetry does not expose secrets, sensitive personal information, or protected tenant data.

## Logging

### Logging Requirements

- Use structured logging rather than string interpolation or concatenation for log message construction.
- Use the established logging framework and configuration.
- Include relevant context, such as service name, environment, operation, trace identifier, and error code.
- Use consistent log levels:
  - `Trace`: Very detailed diagnostic information, enabled only when needed.
  - `Debug`: Information useful for development and troubleshooting.
  - `Information`: Significant application events and normal operational milestones.
  - `Warning`: Unexpected conditions that do not prevent the operation from completing.
  - `Error`: Failed operations requiring investigation or intervention.
  - `Critical`: Failures that threaten the service's ability to operate.
- Do not log every successful operation at `Information` level when this would produce excessive volume.
- Log exceptions at the appropriate handling boundary.
- Avoid logging the same failure redundantly at multiple layers.
- Include relevant exception details in internal logs without exposing them to external clients.
- Ensure background jobs and message consumers produce actionable logs for failed processing.
- Avoid logging complete request or response bodies by default.

### Sensitive Information

- Never log passwords, access tokens, API keys, connection strings containing credentials, or secret values.
- Avoid logging sensitive personal information, full payloads, or confidential business data.
- Redact sensitive fields before logging when diagnostic details are necessary.
- Do not use raw request values as log dimensions without validating their sensitivity and cardinality.
- Apply appropriate access controls and retention policies to logs.
- Do not expose internal logs directly through public endpoints.

## Correlation and Distributed Tracing

### Correlation ID Strategy

- Use the established distributed tracing context as the primary correlation mechanism whenever available.
- Use W3C Trace Context (`traceparent` and `tracestate`) for interoperable trace propagation where supported.
- Use the trace ID to correlate related operations across services.
- Use a separate correlation identifier only when the platform has a distinct operational need for one.
- Validate externally supplied correlation identifiers and never trust them as proof of identity or authorization.
- Include trace or correlation identifiers in logs and error responses where appropriate.
- Do not use trace identifiers as substitutes for business identifiers, tenant authorization, or message idempotency keys.

### Distributed Tracing Strategy

- Instrument incoming HTTP requests and outgoing HTTP calls.
- Instrument database operations, messaging, background jobs, and external provider calls where supported and useful.
- Use the established .NET diagnostics and OpenTelemetry conventions when applicable.
- Propagate trace context across asynchronous messaging boundaries.
- Preserve trace context when work moves from an HTTP request to a background worker or message consumer.
- Record relevant failures and operation durations in spans.
- Avoid recording sensitive payloads, credentials, or unrestricted query parameters in spans.
- Use sampling appropriate to traffic volume, operational requirements, and cost.
- Ensure error traces remain available at a useful rate for troubleshooting.
- Do not assume that all operations belong to a single uninterrupted trace; preserve context where supported and create new traces when appropriate.

### Request Context Propagation

- Propagate trace context through supported HTTP and messaging mechanisms.
- Use standard propagation formats instead of inventing custom headers without a clear requirement.
- Propagate only the context required by the downstream operation.
- Resolve tenant context through the established authentication and authorization mechanisms.
- Never treat propagated tenant identifiers as authorization proof.
- Do not propagate credentials, secret values, or unnecessary personal information.
- Ensure context propagation does not break message processing, retries, or background execution.

## Metrics

### Business Metrics

- Define business metrics according to each service's responsibilities.
- Measure important outcomes, failures, and processing volumes where they provide operational value.
- Prefer metrics such as:
  - Successfully processed events.
  - Rejected or invalid events.
  - Alerts generated and delivered.
  - Failed delivery attempts.
  - Processing backlog and event age.
  - External data ingestion success and failure rates.
- Use consistent metric names, units, and documented meanings.
- Do not create business metrics that imply success when the underlying operation has failed.
- Avoid exposing sensitive tenant-specific information through metric labels.

### Technical Metrics

Where applicable, collect:

- Request rate, error rate, and latency.
- Request duration percentiles, including p50, p95, and p99 where useful.
- Dependency call duration and failure rates.
- Database connection pool usage, query latency, and connection failures.
- Message consumer lag, queue depth, processing duration, and retry counts.
- Background job success and failure rates.
- CPU, memory, process health, and container resource utilization.
- Cache hit rate, miss rate, and operation failures.
- Object storage operation failures and latency.
- Search and indexing latency, failures, and backlog where applicable.

### Metric Design

- Prefer standard instrumentation and established semantic conventions.
- Use consistent units and names across services.
- Keep metric labels low-cardinality.
- Never use unbounded values such as raw user IDs, tenant IDs, request IDs, or arbitrary URLs as metric labels.
- Use histograms for latency distributions where supported and appropriate.
- Avoid duplicating metrics already collected reliably by the platform.
- Document custom metrics and their operational purpose.

### Alerting Thresholds

- Define alert thresholds based on service objectives, baselines, capacity, and business impact.
- Prioritize user-visible failures, sustained error rates, latency degradation, and growing processing backlogs.
- Alert on resource exhaustion and dependency failures when they threaten service operation.
- Use sustained conditions or appropriate evaluation windows to reduce noisy alerts.
- Distinguish actionable alerts from informational dashboards.
- Assign an owner and a response procedure to each production alert.
- Do not invent universal numeric thresholds without workload and service-level requirements.
- Review alert effectiveness after incidents and adjust thresholds when necessary.

## Health Checks

### Liveness Checks

- Indicate whether the application process is alive and able to continue operating.
- Keep liveness checks lightweight and fast.
- Do not make liveness depend on every external service or database.
- Avoid restarting a service solely because a recoverable downstream dependency is temporarily unavailable.
- Ensure health-check execution does not create significant load or recursive dependency calls.

### Readiness Checks

- Indicate whether the service is ready to receive the traffic or workload assigned to it.
- Include only dependencies that are essential for the service to perform its intended work.
- Keep readiness checks bounded by timeouts.
- Use readiness to prevent traffic from reaching a service that cannot safely handle requests.
- Distinguish temporary dependency degradation from complete inability to serve traffic.
- Do not expose sensitive infrastructure details through public health endpoints.

### Dependency Health Checks

- Check critical dependencies only when their health is relevant to service readiness or operational diagnosis.
- Use bounded timeouts and avoid expensive queries.
- Cover applicable dependencies such as PostgreSQL, MongoDB, Redis, Kafka, MinIO, Elasticsearch, and external providers.
- Do not make every dependency a mandatory liveness requirement.
- Report dependency status in an operationally useful manner without disclosing credentials, internal addresses, or connection details.
- Avoid aggressive health-check polling that can amplify an outage.
- Follow the deployment platform's health-check conventions.

## Dashboards and Operations

### Dashboard Conventions

- Organize dashboards by service and operational responsibility.
- Provide a service overview with request volume, errors, latency, resource usage, and dependency health where applicable.
- Include relevant business metrics for services that process events, evaluate risk, or orchestrate and deliver alerts.
- Use consistent names, units, time ranges, and labels across dashboards.
- Make it possible to navigate from an incident metric to related logs and traces when the platform supports this.
- Distinguish current values, historical trends, and configured thresholds.
- Avoid dashboards that rely on high-cardinality dimensions or expose sensitive information.
- Document dashboard ownership and intended use.

### Incident Diagnosis Requirements

- Make it possible to identify the affected service, operation, time window, and relevant trace or correlation identifier.
- Preserve enough context to investigate failures across HTTP requests, messaging, background jobs, and storage operations.
- Record retries, exhausted retry policies, dead-letter events, and relevant dependency failures.
- Document important service dependencies and recovery procedures.
- Ensure production telemetry is accessible to authorized operators.
- Define appropriate retention for logs, metrics, and traces based on operational and compliance requirements.
- Do not claim an operation succeeded solely because a request was accepted or a message was published.
- Review observability gaps after incidents and add instrumentation when it improves diagnosis.

## Validation

Verify the applicable requirements during development and before deployment:

- Logs use structured fields and consistent severity levels.
- Sensitive information is excluded or redacted.
- Trace context propagates across relevant service boundaries.
- HTTP requests, dependency calls, and asynchronous processing are instrumented where required.
- Important business and technical metrics are emitted correctly.
- Metric labels avoid unbounded cardinality.
- Liveness and readiness checks have distinct, appropriate semantics.
- Health checks respect timeout and dependency requirements.
- Failures and retries are observable without redundant logging.
- Dashboards and alerts support the service's operational needs.
- Automated tests cover important instrumentation and health-check behavior.
- Telemetry configuration works in the intended deployment environment.

## Implementation Guidance

Before modifying observability:

1. Inspect the existing logging, metrics, tracing, and health-check configuration.
2. Reuse established libraries, instrumentation, and naming conventions.
3. Identify the operations and failure scenarios that must be observable.
4. Add instrumentation at the appropriate application or infrastructure boundary.
5. Avoid duplicate instrumentation and unnecessary telemetry.
6. Validate context propagation, sensitive-data handling, and failure behavior.
7. Document any new metrics, dashboards, alerts, or operational requirements.

Do not introduce a new observability stack or assume a specific backend is available without verifying the repository and deployment configuration.
