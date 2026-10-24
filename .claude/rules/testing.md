# Testing Rules

## Purpose

Define a consistent testing strategy that protects correctness, reliability, security, and compatibility across the microservices architecture.

## Testing Strategy

### Unit Tests

- Test individual units of behavior in isolation, especially domain rules, application logic, validation, calculations, and decision-making.
- Prefer testing public behavior and observable outcomes rather than private methods or internal implementation details.
- Keep unit tests fast, deterministic, and independent of external infrastructure.
- Use test doubles only for dependencies that require meaningful isolation.
- Do not mock the unit under test or replace so much behavior that the test no longer verifies meaningful functionality.
- Test business rules, boundary conditions, invalid input, and expected failure behavior.
- Keep domain tests independent of HTTP, databases, message brokers, and infrastructure frameworks whenever practical.

### Integration Tests

- Verify that application components work correctly with their actual dependencies and framework integrations.
- Cover relevant interactions involving databases, message brokers, caches, object storage, search indexes, HTTP clients, and dependency injection.
- Prefer real dependency implementations in integration tests when behavior depends on database semantics, serialization, transactions, indexing, or messaging guarantees.
- Use isolated test infrastructure, such as containers, when supported by the existing project and execution environment.
- Do not assume an in-memory substitute behaves identically to the production database or infrastructure.
- Verify persistence, query behavior, transaction boundaries, serialization, configuration, and error handling where relevant.
- Ensure test data is isolated and cleaned up without affecting other tests or environments.
- Test failure behavior when dependencies are unavailable, time out, or return invalid responses, where the service is expected to handle those conditions.

### End-to-End Tests

- Test critical user or business workflows across the relevant service boundaries.
- Focus on a small, high-value set of scenarios that validate the system as a whole.
- Include important successful flows and representative failure paths.
- Verify externally observable outcomes rather than internal implementation details.
- Avoid duplicating every unit and integration test at the end-to-end level.
- Keep end-to-end tests independent of production data and safe to execute repeatedly.
- Use a dedicated test environment and test identities where required.
- Do not execute disruptive end-to-end or load tests against production without explicit authorization.

### Contract Tests

- Verify compatibility between service providers and their consumers.
- Cover relevant HTTP APIs, asynchronous messages, event schemas, and external integration boundaries.
- Validate required fields, data types, serialization, error behavior, and compatibility expectations.
- Test backward compatibility when existing consumers may depend on an established contract.
- Treat message schemas and event payloads as contracts even when communication is asynchronous.
- Include representative malformed or unsupported payloads where appropriate.
- Use existing contract-testing tools and schema-validation mechanisms when available.
- Do not invent undocumented consumers, producers, endpoints, or message schemas.
- When the consumer or provider landscape is unknown, document the uncertainty rather than assuming a contract is unused.

## Rules

- Test observable behavior rather than implementation details.
- Cover successful scenarios, failures, and relevant edge cases.
- Include regression tests for fixed bugs.
- Keep tests deterministic and independent.
- Do not remove or weaken tests merely to make a build pass.
- Use mocks only when they provide meaningful isolation.
- Prefer explicit assertions that explain what behavior is expected.
- Ensure each test has a clear purpose and can fail when the relevant behavior is incorrect.
- Avoid tests that merely reproduce the implementation logic or assert details with no meaningful behavioral guarantee.
- Do not introduce arbitrary delays or fixed sleeps to synchronize asynchronous tests when deterministic synchronization mechanisms are available.
- Avoid order-dependent tests and shared mutable state.
- Use asynchronous test APIs correctly and await operations that must complete before assertions.
- Do not swallow exceptions or mark failing tests as successful without a justified, explicit test condition.
- Do not skip or disable failing tests without documenting the reason and obtaining required approval.
- Ensure regression tests reproduce the relevant failure condition and verify the expected correction.
- Keep production code testable without introducing unnecessary abstractions solely for testing.
- Do not expose secrets, production credentials, or real personal data in test fixtures or test output.

## Test Organization

### Test Directory Structure

- Follow the repository's existing test organization and naming conventions.
- Prefer keeping tests associated with their owning microservice.
- Where consistent with the existing repository structure, organize tests under:
  - `services/<service-name>/tests/UnitTests`
  - `services/<service-name>/tests/IntegrationTests`
- Place end-to-end and contract tests according to the repository's established solution and CI structure.
- Keep test projects separate from production projects where appropriate.
- Do not create new test projects or reorganize existing directories without a clear need.
- Keep test dependencies and project references explicit.
- Avoid coupling one service's tests to another service's internal implementation.

### Naming Conventions

- Follow the test framework and naming conventions already used in the repository.
- Use descriptive test names that communicate the scenario and expected behavior.
- Prefer names that identify the behavior, condition, and expected outcome.
- Group related scenarios consistently without creating unnecessary test classes or abstractions.
- Keep names stable and meaningful; avoid vague names such as `Test1`, `Works`, or `ShouldWork`.
- Do not force one naming convention across the repository if an established convention already exists.

### Test Data and Fixtures

- Use synthetic or explicitly approved test data.
- Never use production secrets or unrestricted production data in tests.
- Create the minimum data necessary to express each scenario.
- Make test setup explicit and easy to understand.
- Use shared fixtures only when they simplify setup without creating hidden dependencies or shared mutable state.
- Ensure tests can run independently and repeatedly.
- Clean up resources created by tests, including database records, temporary files, objects, message subscriptions, and infrastructure where applicable.
- Use unique identifiers or isolated namespaces to prevent collisions between parallel tests.
- Keep test data representative of relevant domain constraints and realistic failure conditions.
- Avoid random test inputs unless randomness is controlled and reproducible.
- Do not depend on execution order, external service availability, or undeclared local configuration.
- When external providers cannot be called reliably, use controlled test doubles for unit tests and dedicated integration environments for actual provider behavior.
- Do not silently replace a required integration test with a mock that bypasses the behavior being validated.

## Coverage

### Required Coverage Policy

- Do not invent a universal code coverage percentage when the repository or organization has not defined one.
- Follow any existing coverage thresholds and CI enforcement policies.
- Use coverage reports to identify untested code and potential risk, not as the sole measure of test quality.
- Prioritize meaningful assertions and behavior coverage over maximizing line or branch counts.
- Add tests for new behavior and update existing tests when behavior changes.
- Avoid lowering coverage thresholds to accommodate untested changes without the required approval.
- If a coverage policy is missing, report the gap and recommend defining a baseline based on the repository's current state and risk profile.

### Critical Flows Requiring Additional Coverage

Prioritize stronger coverage for behavior that can cause significant business, security, or reliability impact, including:

- Authentication, authorization, tenant isolation, and resource ownership.
- Core domain rules and business invariants.
- Data persistence, consistency, transaction boundaries, and migrations.
- Message processing, duplicate delivery, idempotency, retries, and dead-letter handling where implemented.
- External provider integrations and ingestion failures.
- Deduplication, cooldowns, and other correctness-sensitive Redis operations.
- Alert creation, routing, and delivery workflows where applicable.
- File uploads, object access, and deletion workflows involving MinIO.
- Search and reporting behavior involving Elasticsearch.
- Failure recovery, timeouts, cancellation, and partial failures.
- Backward compatibility of public APIs and asynchronous message contracts.
- Security-sensitive configuration and secret handling.

Apply additional testing based on actual service responsibilities. Do not assume every service implements every listed capability.

## Validation

### Test Commands

- Use the commands and test framework already established by the repository.
- For .NET projects, use the following commands from the appropriate solution or project directory:

```bash
dotnet restore
dotnet build --no-restore
dotnet test
```

- Run a specific test project when investigating or validating a focused change:

```bash
dotnet test path/to/TestProject.csproj
```

- Collect code coverage when supported by the installed SDK and the project's test configuration:

```bash
dotnet test --collect:"XPlat Code Coverage"
```

- Verify command compatibility with the installed .NET SDK and existing CI configuration.
- Do not assume every command succeeds simply because it was invoked.
- Do not introduce additional testing or coverage tools without a clear justification and approval where required.

### Required Checks Before Completion

Before declaring a change complete:

1. Review the modified code and identify the behaviors and contracts affected.
2. Add or update tests for the new behavior, relevant failure paths, and fixed regressions.
3. Run the relevant unit tests.
4. Run applicable integration, contract, and end-to-end tests when the change affects those boundaries and the required environment is available.
5. Run the build and relevant static analysis or formatting checks required by the repository.
6. Inspect test failures and distinguish code defects from environment or infrastructure failures.
7. Confirm that no tests were removed, weakened, or skipped merely to obtain a passing result.
8. Review the final changes for accidental test data, secrets, configuration changes, or unrelated modifications.
9. Report which checks actually ran, their outcomes, and any checks that could not be executed.

- Do not claim that all tests pass when only a subset was executed.
- Do not claim integration or end-to-end coverage when the required dependencies were not exercised.
- If a test cannot run because infrastructure or configuration is unavailable, state the limitation and do not report the check as passed.
- Never bypass a failing test or required CI check without the applicable authorization.

## Implementation Guidance

When adding or changing functionality:

1. Identify the expected behavior, relevant business rules, and affected service boundaries.
2. Choose the smallest set of test levels that provides meaningful confidence.
3. Add fast unit tests for isolated business behavior.
4. Add integration tests for framework and infrastructure interactions.
5. Add contract tests for compatibility-sensitive service boundaries.
6. Add end-to-end tests for critical workflows that cannot be adequately validated at lower levels.
7. Run the relevant checks and review the results before completing the change.

Testing should provide evidence that the behavior is correct. The number of tests, the amount of mocking, and the coverage percentage are not substitutes for that evidence.
