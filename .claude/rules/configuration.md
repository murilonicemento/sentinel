# Configuration Rules

## Purpose

Standardize application configuration across environments while ensuring secure secret management, predictable configuration precedence, and reliable application startup.

## General Rules

- Keep environment-specific configuration outside application source code.
- Never commit secrets, credentials, access tokens, private keys, or other sensitive values.
- Store configuration templates and non-sensitive defaults in version-controlled configuration files.
- Validate mandatory configuration at startup whenever practical.
- Fail fast with clear, actionable error messages when required configuration is missing or invalid.
- Define explicit defaults only when they are safe and appropriate for every environment where they may apply.
- Do not log secrets or sensitive configuration values.
- Use strongly typed configuration options and validation where appropriate.
- Avoid duplicating the same configuration value across multiple sources unless there is a clear operational reason.
- Do not assume that configuration values are identical across services or environments.

## Configuration Sources

### Environment Variables

- Use environment variables to supply environment-specific configuration and override non-sensitive defaults.
- In local development, use .NET User Secrets for local secrets and environment variables when appropriate.
- Use `appsettings.json` to document configuration structure and provide safe, non-sensitive defaults.
- Keep secret values out of committed `appsettings*.json` files.
- Document required configuration keys and provide example values only when they are non-sensitive.
- In deployed environments, inject configuration and secrets through the approved deployment platform or secret provider.

### Configuration Files

- Use `appsettings.json` for shared, non-sensitive configuration defaults.
- Use `appsettings.Development.json` for local development overrides that are safe to commit. Do not commit real credentials or secrets.
- Use environment-specific configuration files only when necessary, and never rely on them as the primary storage for production secrets.
- Keep configuration keys consistent across environments unless a difference is explicitly required.

### Secret Management

- Use Azure Key Vault for secrets in Azure-hosted environments.
- For other environments, use the approved secret management service, such as the platform's native secret manager or an equivalent solution.
- Grant each service only the permissions required to access its own secrets.
- Prefer managed identities or workload identities over stored credentials when supported.
- Rotate credentials according to the relevant security and operational requirements.
- Never expose secret values in logs, exception messages, diagnostic output, or application responses.

### Configuration Precedence

- Define and document the configuration provider order explicitly in the application bootstrap code.
- Use the following precedence from lowest to highest priority, unless a documented deployment requirement dictates otherwise:
  1. `appsettings.json`
  2. `appsettings.{Environment}.json`
  3. .NET User Secrets in local development
  4. Environment variables
  5. Azure Key Vault or the approved external secret provider
- When the same key exists in multiple sources, the highest-priority registered provider takes precedence.
- Do not assume that a secret provider automatically overrides other configuration sources. Verify provider registration and precedence in the actual application setup.
- Avoid defining the same secret in multiple sources.

## Environments

### Development

- Use `appsettings.json` for shared, non-sensitive defaults.
- Use `appsettings.Development.json` for local overrides that contain no real secrets.
- Use .NET User Secrets or environment variables for local credentials and sensitive values.
- Provide a documented configuration template listing the required keys without exposing their actual secret values.
- Allow local development to run without Azure Key Vault when a secure local configuration mechanism is available.

### Testing

- Use isolated test configuration and non-production credentials.
- Use environment variables or the test platform's secret store for credentials required by automated tests.
- Avoid requiring Azure Key Vault for ordinary unit tests.
- Use Azure Key Vault or an equivalent secret provider for integration or deployment tests only when the test environment requires it.
- Never use production secrets or production data unnecessarily.

### Staging

- Use the approved secret management provider, such as Azure Key Vault.
- Keep staging credentials and configuration separate from production.
- Use staging settings that reflect production behavior where practical without sharing production secrets.
- Validate configuration and secret access as part of deployment verification.

### Production

- Use Azure Key Vault or the approved secret management provider for sensitive configuration.
- Prefer managed identities or workload identities for accessing secrets.
- Do not depend on local configuration files for production secrets.
- Restrict access according to least privilege.
- Validate required configuration during startup and deployment.
- Ensure secret rotation can be performed without unnecessary service disruption where supported.

## Configuration Validation

- Define required configuration keys for each service and environment.
- Use strongly typed options classes and the .NET options validation mechanisms where appropriate.
- Validate mandatory settings during startup rather than waiting for the first request that uses them.
- Validate formats, ranges, endpoint URLs, and related configuration constraints.
- Fail startup when mandatory settings are missing or invalid.
- Do not include secret values in validation errors.
- Add automated tests for configuration validation, including missing values, invalid formats, and environment-specific requirements.
- Document required configuration keys and safe example values in the service's configuration documentation.

## Validation

- **Configuration completeness:** Verify that every required setting is documented and supplied in each deployment environment.
- **Secret protection:** Ensure that real secrets are not committed to the repository or exposed through logs.
- **Precedence verification:** Test that configuration providers resolve duplicate keys according to the documented order.
- **Startup validation:** Verify that applications fail clearly when mandatory configuration is missing or invalid.
- **Environment isolation:** Verify that development, testing, staging, and production use the appropriate settings and credentials.
- **CI checks:** Add automated checks for configuration templates, required settings, and accidental secret exposure where supported.