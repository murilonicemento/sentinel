# Documentation Rules

## Purpose

Keep project documentation accurate, useful, verifiable, and consistent with the implemented system.

## General Rules

- Update documentation affected by behavior, architecture, configuration, deployment, or contract changes.
- Document non-obvious decisions, important constraints, assumptions, and operational risks.
- Avoid comments that merely restate the code.
- Prefer clear explanations and examples that can be verified against the current implementation.
- Never document intended behavior as implemented behavior without verification.
- Inspect the existing documentation structure and conventions before creating or moving documentation.
- Reuse existing documentation where practical instead of creating duplicate sources of truth.
- Keep instructions actionable, with prerequisites, commands, expected outcomes, and relevant limitations.
- Use consistent terminology for services, domain concepts, configuration settings, and infrastructure components.
- Distinguish implemented capabilities from planned work, proposals, and unresolved decisions.
- Do not invent endpoints, environment variables, message schemas, dependencies, commands, deployment procedures, or infrastructure resources.
- Do not include secrets, credentials, private keys, sensitive personal data, or confidential production data.
- Use placeholders for sensitive configuration values and clearly identify examples that require replacement.
- Keep examples consistent with the actual code, configuration, and supported runtime versions.
- Remove or correct obsolete instructions when implementation changes make them invalid.
- Prefer concise documentation that answers a specific question over lengthy descriptions that repeat the source code.
- When information cannot be verified, explicitly document the uncertainty or identify what requires confirmation.

## Required Documentation

### Project Setup

Document the steps required for a developer to understand, configure, build, test, and run the project locally.

Include, where applicable:

- Project purpose and high-level capabilities.
- Required SDKs, runtimes, tools, and external dependencies.
- Repository structure and relevant entry points.
- Restore, build, test, and run commands.
- Local infrastructure requirements and supported setup procedures.
- Required configuration settings and safe ways to provide them.
- Database initialization, migrations, or other setup steps.
- Common setup failures and troubleshooting guidance.
- Differences between local development and deployed environments.

Verify commands against the repository and available tooling before documenting them as supported.

Do not assume every developer must run every infrastructure dependency locally. Document the actual requirements for the relevant workflows.

### Architecture Overview

Maintain an architecture overview that reflects the implemented system.

Document, where relevant:

- Service responsibilities and ownership boundaries.
- Communication between services, including synchronous APIs and asynchronous messaging.
- Data ownership and storage responsibilities.
- Important dependencies and external integrations.
- Authentication, authorization, and tenant-isolation boundaries.
- Background processing and scheduled workloads.
- Observability, failure handling, and recovery mechanisms.
- Important architectural constraints and known limitations.

Use diagrams when they make service relationships, data flow, or deployment structure easier to understand.

Ensure diagrams match the current implementation. Clearly label proposed or planned components instead of presenting them as existing infrastructure.

Do not assume every service uses every storage technology or infrastructure component available in the wider platform.

### API Contracts

- Document externally consumed APIs using the established contract format and tooling.
- For ASP.NET Core APIs, use OpenAPI when supported and configured by the project.
- Keep routes, HTTP methods, request and response schemas, authentication requirements, status codes, and error behavior consistent with the implementation.
- Document pagination, filtering, validation, and versioning behavior where applicable.
- Document stable error codes and relevant Problem Details responses where implemented.
- Describe tenant context and authorization requirements without exposing sensitive implementation details.
- Document compatibility constraints and deprecation behavior for consumers.
- Update API contracts when observable behavior or public schemas change.
- Document asynchronous event and message contracts, including relevant schemas, required fields, versioning, and compatibility expectations.
- Document external provider integrations, including required configuration, expected responses, failure handling, and rate limits where verified.
- Do not expose internal endpoints as public APIs unless they are intended for external consumption.
- Do not manually claim that an API contract is complete when its schema or implementation has not been verified.

### Environment Configuration

Document configuration required to build and run each supported environment.

Include, where applicable:

- Configuration setting names and their purpose.
- Required versus optional settings.
- Expected formats and accepted values.
- Defaults and validation behavior.
- Environment-specific differences.
- Supported secret-management mechanisms.
- Required external services and connectivity.
- Safe local development configuration.
- Startup failures caused by missing or invalid configuration.

Rules:

- Never include actual secrets in documentation, example files, screenshots, or command output.
- Use placeholders for secret values and explain how authorized developers obtain them.
- Distinguish configuration values that are safe to commit from secrets that must be provided externally.
- Verify configuration names against the options classes, configuration providers, deployment manifests, and startup validation.
- Document configuration precedence only after verifying the registered providers and their order.
- Do not claim a setting is optional when startup validation or runtime behavior requires it.
- Keep environment examples aligned with the configuration actually supported by the application.
- Document secret-management mechanisms only when they are configured or explicitly approved for the relevant environment.

### Operational Procedures

Document operational procedures that are required to deploy, maintain, troubleshoot, or recover the system.

Include, where applicable:

- Build and deployment procedures.
- Database migration and schema-change procedures.
- Configuration and secret rotation.
- Health checks and readiness verification.
- Log, metric, and trace locations or access methods.
- Common failure scenarios and troubleshooting steps.
- Retry, replay, and dead-letter handling for asynchronous processing where implemented.
- Backup, restore, and recovery procedures where defined.
- Rollback steps and relevant compatibility constraints.
- Escalation paths and ownership for operational incidents.

Operational instructions must be grounded in verified tooling, permissions, and infrastructure.

Do not invent deployment commands, production access procedures, recovery guarantees, or rollback capabilities.

If a procedure is not documented or cannot be verified, identify the gap rather than presenting an untested procedure as safe.

## Architecture Decisions

### ADR Format and Location

- Follow the repository's existing Architecture Decision Record (ADR) format and location.
- If no convention exists, propose a consistent ADR structure and location before introducing a new convention.
- A suggested fallback location is `docs/adr/`.
- Use sequential identifiers and descriptive filenames if that convention is adopted.
- Each ADR should include, where relevant:
  - Title and status.
  - Date and decision owner, if known.
  - Context and problem statement.
  - Constraints and requirements.
  - Options considered.
  - Decision and rationale.
  - Consequences and trade-offs.
  - Security, reliability, operational, and migration implications.
  - Links to related decisions and implementation changes.

Use clear statuses such as Proposed, Accepted, Rejected, Deprecated, or Superseded when consistent with the adopted convention.

### When an ADR Is Required

Create or update an ADR when a change introduces or materially revises a significant architectural decision, such as:

- Service boundaries or ownership responsibilities.
- Data ownership, persistence strategy, or cross-service consistency.
- Synchronous versus asynchronous communication patterns.
- Public API or event-versioning strategy.
- Authentication, authorization, or tenant-isolation architecture.
- Cross-service reliability, idempotency, or failure-recovery mechanisms.
- Major technology choices or infrastructure dependencies.
- Significant security, scalability, cost, or operational trade-offs.
- A decision that invalidates or supersedes an existing ADR.

Routine implementation details and small refactorings do not require an ADR unless they materially change an architectural constraint.

Rules:

- Record the decision and its rationale, not merely the implementation steps.
- Include meaningful alternatives and trade-offs when they were actually evaluated.
- Do not fabricate discussions, approvals, measurements, or alternatives that were never considered.
- Update the status of an existing ADR when a decision changes.
- Preserve historical decisions and mark them as superseded rather than silently rewriting history.
- Do not mark a proposed decision as accepted without the appropriate approval.

## Code Documentation

### Public API Documentation

- Document public interfaces, APIs, and reusable components when their intended use is not sufficiently clear from their names and types.
- Use the documentation conventions established by the language and framework.
- For C#, use XML documentation comments where appropriate for public APIs and libraries.
- Explain purpose, parameters, return values, relevant exceptions, side effects, and usage constraints where they are not self-evident.
- Document nullability, cancellation behavior, asynchronous behavior, and resource ownership when relevant.
- Keep API documentation consistent with the actual signature and behavior.
- Avoid generating verbose comments for obvious getters, simple assignments, or self-explanatory code.
- Follow existing compiler and analyzer policies for missing public API documentation.
- Do not add blanket documentation requirements to every internal type unless the repository's standards require them.

### Complex Business Rules

- Document non-obvious business rules, domain invariants, important assumptions, and surprising edge cases.
- Explain why a constraint exists when the reason is not apparent from the code.
- Prefer documenting domain concepts and rationale near the relevant implementation.
- Use comments for important context that cannot be expressed clearly through names, types, or structure alone.
- Document units, time-zone assumptions, rounding rules, ordering requirements, and boundary conditions when they affect correctness.
- Explain unusual workarounds, external-provider limitations, and compatibility constraints.
- Update comments when the corresponding behavior changes.
- Do not use comments to excuse unclear code when a safe simplification or better name would solve the problem.
- Never leave comments that describe behavior the implementation no longer performs.
- Do not place secrets, credentials, or sensitive operational details in code comments.

## Examples and Diagrams

- Ensure commands and code examples match the current project structure, supported tool versions, and actual interfaces.
- Prefer minimal, complete examples that can be reproduced safely.
- Use synthetic data and placeholder credentials.
- Clearly identify assumptions, prerequisites, and expected results.
- Verify referenced file paths, scripts, configuration keys, endpoints, and command options.
- Use diagrams to explain architecture or workflows when they add value beyond prose.
- Keep diagrams version-controlled and update them alongside relevant architectural changes.
- Do not present illustrative examples as verified production configuration.
- Label incomplete, hypothetical, or proposed examples explicitly.

## Validation

### Documentation Review Process

Before completing a change:

1. Identify documentation affected by the code, configuration, API, event, or architectural changes.
2. Inspect the existing documentation and relevant implementation.
3. Update the authoritative documentation instead of creating unnecessary duplicates.
4. Verify commands, paths, configuration names, schemas, and examples against the repository.
5. Check that security-sensitive values and confidential data are not exposed.
6. Review links, diagrams, terminology, and references affected by the change.
7. Ensure the documentation distinguishes implemented behavior from proposals and unresolved decisions.
8. Run available documentation checks, link validation, schema validation, or generated-documentation checks when configured.
9. Report any known gaps or verification limitations.

- Do not claim that documentation was validated by a tool unless the check was actually executed.
- Do not claim that examples work when they have not been verified.
- Do not generate large amounts of unrelated documentation to accompany a focused code change.
- Do not skip required documentation updates merely because the code compiles or tests pass.
- If a change has no meaningful documentation impact, do not modify unrelated documents just to satisfy a checklist.

## Implementation Guidance

When implementing a feature or changing existing behavior:

1. Identify the affected services, interfaces, configuration, and operational procedures.
2. Determine which documentation is authoritative for each affected area.
3. Update the relevant setup instructions, architecture descriptions, contracts, or code comments.
4. Update or create an ADR when a significant architectural decision requires one.
5. Verify documentation against the actual implementation.
6. Include documentation changes in the final change summary and disclose any unresolved gaps.

Documentation is part of the deliverable when a change alters how the system is used, integrated, configured, operated, or understood.
