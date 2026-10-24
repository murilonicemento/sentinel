# Dependency Management Rules

## Purpose

Keep dependencies justified, secure, compatible, and maintainable across the microservices architecture.

## General Rules

- Prefer existing project capabilities and approved dependencies when they adequately meet the requirements.
- Do not introduce a new dependency without a clear technical justification.
- Avoid duplicate packages or libraries with overlapping responsibilities.
- Verify package compatibility with the project's target framework, runtime, and architecture.
- Evaluate package maintenance status, release history, known vulnerabilities, and community adoption.
- Review the package's actual maintainer and provenance when evaluating trustworthiness.
- Do not assume that a popular vendor or established company guarantees package security.
- Prefer official package registries and approved organizational feeds.
- Never install packages from untrusted sources or arbitrary URLs.
- Pin dependency versions according to the project's versioning and lockfile strategy.
- Do not use floating versions or unbounded version ranges unless explicitly approved.
- Do not upgrade major versions without evaluating breaking changes and migration requirements.
- Avoid unnecessary dependency upgrades unrelated to the current task.

## Dependency Policy

### Approved Package Sources

- Use NuGet.org and explicitly approved private NuGet feeds for .NET dependencies.
- Use official registries for other ecosystems when those ecosystems are present in the repository.
- Use approved container registries for base images and third-party images.
- Verify package identity, publisher, and source before adding unfamiliar dependencies.
- Do not add new registries or package sources without explicit approval.
- Prefer dependencies with a maintained release history and a responsible, verifiable maintainer.

### License Policy

- Prefer permissive licenses such as MIT, Apache-2.0, BSD-2-Clause, and BSD-3-Clause, subject to organizational policy.
- Review license obligations before introducing any dependency.
- Require explicit approval for licenses with significant distribution, source disclosure, or commercial-use obligations.
- Do not introduce dependencies with prohibited or incompatible licenses.
- If license compatibility is unclear, stop and request review instead of assuming the license is acceptable.
- Preserve required license notices and attribution.

### Versioning Strategy

- Prefer patch updates within the currently approved version line when they address bugs or security issues without introducing breaking changes.
- Evaluate minor updates when they provide security fixes, important bug fixes, required capabilities, or necessary compatibility improvements.
- Evaluate major updates when the current version is deprecated, unsupported, vulnerable without an available fix, or incompatible with required platform changes.
- Do not delay a necessary security update solely because it is a minor or major version change.
- Review release notes and migration guides before upgrading.
- Never downgrade a dependency to avoid addressing a compatibility issue without documenting the reason and associated risks.

### Lockfile Policy

- Commit lockfiles required by the project's package manager and deployment workflow.
- For .NET applications, use `packages.lock.json` when NuGet lockfile mode is enabled by the project.
- For JavaScript applications, commit the lockfile associated with the selected package manager when applicable.
- Keep dependency declarations and lockfiles synchronized.
- Regenerate lockfiles using the appropriate package manager rather than editing resolved dependency versions manually.
- Review lockfile changes to identify unexpected transitive dependency updates.
- Use locked restore in CI when the project has adopted NuGet lockfiles.
- Do not remove or bypass an established lockfile policy without explicit approval.

## Security

### Vulnerability Assessment

- Assess direct and transitive dependencies, not only packages explicitly declared by the project.
- Prioritize vulnerabilities using severity, exploitability, known exploitation in the wild, exposure, and the availability of a fix.
- Consider the number of known vulnerabilities as supporting information, not as the sole measure of risk.
- Prefer authoritative vulnerability advisories and package-manager audit information.
- Check whether a vulnerable dependency is actually present in the resolved dependency graph.
- Determine whether the vulnerable component is reachable or used in an exposed execution path when assessing remediation priority.
- Do not ignore a vulnerability solely because the affected code path appears unused.
- Never suppress, dismiss, or downgrade a security finding without documented justification and authorization.

### Update and Patch Policy

- Apply compatible security patches promptly according to severity and organizational policy.
- Evaluate minor or major upgrades when no safe patch is available in the current version line.
- Upgrade to a supported version when a dependency is deprecated or has reached end of support.
- If immediate remediation is not possible, document the risk, mitigation, owner, and remediation deadline.
- Verify that the update does not introduce regressions or new vulnerabilities.
- Do not claim a dependency is secure merely because an audit command completed successfully.

### Exceptions and Risk Acceptance

- Document the affected package, vulnerability or policy violation, business justification, and available alternatives.
- Record compensating controls, the responsible approver, and an expiration or review date.
- Require explicit approval from the designated security or engineering authority.
- Keep exceptions scoped to the affected dependency and purpose.
- Reassess exceptions when a fix becomes available or the dependency changes.
- Never create an exception solely to make CI pass.

## Dependency Changes

### Required Justification

Before introducing, replacing, or upgrading a dependency:

- Explain the problem the dependency solves.
- Explain why existing project capabilities or approved dependencies are insufficient.
- Evaluate maintenance, compatibility, security, license, and transitive dependency implications.
- Consider the operational cost, package size, and long-term maintenance burden.
- Prefer the smallest dependency footprint that satisfies the requirements.

### Compatibility Assessment

- Verify compatibility with the target .NET version and supported runtime environments.
- Check transitive dependency changes and possible version conflicts.
- Review release notes, breaking changes, and migration instructions.
- Verify compatibility with the project's architecture and existing dependency injection configuration.
- Consider interactions with container images, build pipelines, and deployment environments when applicable.
- Do not introduce a dependency that requires an unsupported runtime or unapproved infrastructure change.

### Required Tests After Updates

- Restore dependencies successfully.
- Build the affected projects.
- Run relevant unit and integration tests.
- Run formatting, static analysis, and other established quality checks.
- Re-run vulnerability and license checks when available.
- Verify startup, dependency injection, and configuration for affected services.
- Test important behavior that may be affected by the upgrade.
- Review the final dependency diff and lockfile changes before completing the task.

## Validation

Use the package manager and security tools already configured by the repository. Do not install a new auditing tool without justification and approval.

### .NET / NuGet

Run the following commands from the relevant solution or project directory:

- Restore dependencies:

  `dotnet restore`

- Build the solution:

  `dotnet build --no-restore`

- List vulnerable direct and transitive packages:

  `dotnet list package --vulnerable --include-transitive`

- List outdated packages for review:

  `dotnet list package --outdated`

- List transitive dependencies when investigating dependency changes:

  `dotnet list package --include-transitive`

Use the command syntax supported by the installed .NET SDK. Newer SDKs may provide equivalent noun-first command forms.

### Other Ecosystems

- Run the ecosystem's established vulnerability audit command when that ecosystem is present.
- Validate container images with the repository's approved image-scanning process when available.
- Run license and policy checks when an approved tool is configured.
- Do not assume an audit tool is installed or that a missing tool means the dependencies are safe.

### CI Requirements

Where supported by the repository's existing tooling, CI should:

- Restore dependencies reproducibly.
- Build affected projects.
- Run relevant automated tests.
- Detect known vulnerable direct and transitive dependencies.
- Enforce the established lockfile policy.
- Fail on vulnerabilities that violate the configured security policy.
- Report license or provenance violations according to organizational policy.
- Require explicit, documented approval for exceptions.

## Implementation Guidance

Before modifying dependencies:

1. Inspect existing package references, central version management, lockfiles, and package sources.
2. Identify the direct and transitive dependency impact.
3. Review security, maintenance, licensing, and compatibility.
4. Make the smallest justified change.
5. Run the applicable restore, build, audit, and test commands.
6. Review all dependency and lockfile changes.
7. Document unresolved vulnerabilities, required approvals, or operational follow-up.

Do not invent organizational approvals, claim that a package source is approved without evidence, or silently introduce new dependency-management tooling.
