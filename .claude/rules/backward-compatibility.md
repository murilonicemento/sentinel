
# Backward Compatibility Rules

## Purpose
Prevent unintended breakage for existing consumers and integrations.

## Compatibility Scope
- Public APIs: TODO
- Database schemas: TODO
- Events and messages: TODO
- Shared libraries: TODO

## Rules
- Identify affected consumers before changing public contracts.
- Avoid removing or renaming public fields without a migration plan.
- Prefer additive changes when feasible.
- Define deprecation periods where required.
- Consider mixed application versions during deployments.

## Migration Strategy
- Compatibility window: TODO
- Deprecation policy: TODO
- Rollout and rollback strategy: TODO

## Validation
- Contract tests: TODO
- Compatibility checks: TODO