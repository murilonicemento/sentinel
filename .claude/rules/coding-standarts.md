# Coding Standards

## Purpose

Establish consistent coding practices across the Sentinel project to improve readability, maintainability, and code quality.

## General Principles

- Prefer readable and maintainable code over clever solutions.
- Follow existing conventions before introducing new ones.
- Keep classes and methods focused on clear responsibilities.
- Avoid unnecessary duplication and premature abstractions.
- Do not modify unrelated code during a task.
- Prefer explicit, idiomatic C# over unnecessary complexity.
- Follow the existing architecture and dependency boundaries.
- Do not introduce new dependencies without a clear justification.

## Naming Conventions

- **Local variables and parameters:** Use `camelCase`.
- **Methods and functions:** Use `PascalCase`.
- **Classes, records, interfaces, structs, and enums:** Use `PascalCase`.
- **Interfaces:** Prefix names with `I`, such as `IEventPublisher`.
- **Constants:** Use `PascalCase`, following standard C# conventions, such as `DefaultPageSize`.
- **Enum members:** Use `PascalCase`.
- **Files:** Use `PascalCase` for files containing C# types, generally matching the primary type name.
- **Service directories:** Use `kebab-case`, such as `risk-evaluation` and `alert-orchestrator`.
- **Project and layer directories:** Follow the existing project structure and naming conventions.

Do not add an `Enum` suffix to enum type names unless explicitly required by an established project convention.

## Code Organization

Follow the existing Sentinel repository structure.

```text
sentinel/
├── services/
│   ├── api-gateway/
│   ├── ingestion/
│   │   ├── src/
│   │   │   ├── Domain/
│   │   │   ├── Application/
│   │   │   ├── Infrastructure.Read/
│   │   │   ├── Infrastructure.Write/
│   │   │   └── Api/
│   │   └── tests/
│   │       ├── UnitTests/
│   │       └── IntegrationTests/
│   ├── risk-catalog/
│   ├── geospatial/
│   ├── risk-evaluation/
│   ├── alert-orchestrator/
│   ├── channels/
│   ├── reporting/
│   ├── tenants-billing/
│   └── compliance-audit/
├── platform/
│   ├── init-db/
│   ├── k8s/
│   ├── helm/
│   ├── docker-compose.yml
│   └── observability/
│       ├── grafana/
│       ├── prometheus/
│       ├── loki/
│       └── otel-collector/
├── libs/
│   ├── BuildingBlocks/
│   │   ├── Messaging/
│   │   ├── Outbox/
│   │   └── Observability/
│   └── SharedKernel/
├── docs/
│   ├── RFC-Arquitetura.md
│   └── diagrams/
│       ├── architecture.mmd
│       └── classDiagram.mmd
└── README.md
```

- Preserve the existing separation between domain, application, infrastructure, and API responsibilities.
- Keep unit tests and integration tests in their designated locations.
- Place shared abstractions in shared libraries only when they are genuinely needed by multiple services.
- Do not introduce new architectural layers or directory conventions without justification.

## Complexity and Maintainability

- Prefer small, focused methods with clear responsibilities.
- Avoid deeply nested conditionals and unnecessarily complex control flow.
- Extract reusable logic when doing so improves clarity or prevents meaningful duplication.
- Avoid arbitrary method-length or complexity limits unless the team establishes measurable thresholds.
- Refactor existing code only when relevant to the task or necessary to maintain correctness.
- Preserve established behavior unless a change is explicitly required.

## Formatting and Static Analysis

- Use `dotnet format` to validate and apply the repository's configured formatting conventions.
- Follow the C# compiler diagnostics and the analyzers configured in the relevant projects.
- Prefer repository-level `.editorconfig` settings for consistent formatting and code-style rules.
- Do not introduce a separate formatter or linter when the existing .NET tooling already covers the requirement.
- Fix warnings introduced by a change and do not suppress diagnostics without justification.
- Keep formatting changes separate from unrelated functional changes when practical.

## Prohibited Practices

- Comments that merely repeat what the code already expresses.
- Dead code and unused imports.
- Silent workarounds for unresolved problems.
- Unjustified suppression of compiler warnings or analyzer diagnostics.
- Unnecessary dependencies, abstractions, or duplicated implementations.
- Unrelated refactoring during narrowly scoped tasks.

## Validation

Before considering a code change complete:

- Ensure the affected projects build successfully.
- Run the relevant unit tests and integration tests.
- Verify that no new unexplained compiler warnings or analyzer violations have been introduced.
- Run `dotnet format` in verification mode when supported by the repository configuration.
- Verify that the change respects existing architecture, naming conventions, and project boundaries.

Required CI checks should be defined and enforced by the repository's pipeline. Documentation alone does not enforce these rules.