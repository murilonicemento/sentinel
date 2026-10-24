# Architecture Rules

## Purpose

Define the architectural principles, boundaries, and constraints that all changes to Sentinel must respect.

## Architectural Principles

- Domain-Driven Design (DDD) for modeling business domains.
- Clean Architecture for dependency management and separation of concerns.
- CQRS when command and query responsibilities require separation.
- Event-driven communication when asynchronous processing or decoupling between services is appropriate.
- Microservices with independent data ownership.
- Observability and resilience designed into relevant components.
- Test-Driven Development (TDD) as the default development practice.

These principles must be applied according to the requirements and constraints of the affected component. Do not introduce additional patterns without a clear justification.

## Architecture Overview

- Architecture style: DDD, Clean Architecture, and microservices.
- Service source code: services/<service-name>/src/
- Service tests: services/<service-name>/tests/
- Architecture Decision Records: docs/adr/

### Dependency Direction

Dependencies must point toward the domain and application policies, not toward infrastructure implementations.

- Domain must not depend on Application, Infrastructure, or API.
- Application may depend on Domain.
- Infrastructure may depend on Application and Domain abstractions.
- API/Presentation may depend on Application and configure the composition of application services and infrastructure.
- Domain and Application must not reference web frameworks, database implementations, message brokers, or other infrastructure technologies unless an explicitly documented exception is approved.

Project references and dependency injection must respect these boundaries.

## Module Responsibilities

### Domain

- Entities, Value Objects, AggregateRoots, Aggregates, Enums, Repositories Interfaces.
- Domain Services and Domain Events.
- Business invariants and domain rules.
- Domain abstractions when they belong to the domain model.

### Application

- Use cases and application orchestration.
- Commands, Queries, and their Handlers when CQRS is used.
- Application DTOs and input validation.
- Application ports and abstractions for external capabilities.
- Transaction and use-case coordination policies.

### Infrastructure

- Persistence and Entity Framework implementations.
- Repository implementations.
- Cache and messaging implementations.
- External service integrations.
- Implementations of application and domain abstractions.
- Infrastructure-specific configuration.

### API / Presentation

- Controllers and HTTP endpoints.
- Request and response mapping.
- Authentication and authorization integration.
- Middleware and dependency injection composition.
- Health checks and API documentation.
- Transport-level input validation.

## Microservice Boundaries

- Each service owns its business capabilities and data.
- A service must not directly access another service's database.
- Cross-service data access must use explicitly defined contracts.
- Prefer asynchronous messaging when decoupling and eventual consistency are appropriate.
- Use synchronous communication when immediate responses are required and the coupling is justified.
- Do not assume distributed operations are atomic.
- Define ownership and consistency requirements for cross-service workflows.

## Resilience and Observability

- External calls must have appropriate timeouts and cancellation.
- Retries must be bounded and used only when the operation is safe to retry.
- Operations that may be delivered more than once must define appropriate idempotency behavior.
- Failures must be observable without exposing sensitive information.
- Critical workflows must define their relevant logging, metrics, tracing, and health-check requirements.

## Architectural Decision Records

- Record significant architectural decisions in docs/adr/.
- Document context, the decision, alternatives, and consequences.
- Review existing ADRs before introducing conflicting patterns.
- Update or supersede an ADR when a decision changes.
- Do not silently change established architectural boundaries.

## Prohibited Practices

- Sharing a database between independently owned microservices.
- Reversing dependency direction across architectural boundaries.
- Placing business rules in controllers or infrastructure adapters.
- Introducing cross-service database access as an integration strategy.
- Adding architectural patterns without a clear requirement.
- Making significant architectural changes without evaluating their impact on existing services and ADRs.

## Validation

Before completing an architectural change:

- Inspect the affected service and its existing conventions.
- Verify project references and dependency direction.
- Run relevant automated tests.
- Run architecture tests or static dependency checks when available.
- Review the changes against existing ADRs.
- Report any architectural constraints that could not be verified.
