# Git Workflow Rules

## Purpose

Protect existing work, maintain a reliable change history, and ensure all code changes are reviewed and validated before completion.

## Repository Safety

- Inspect the current branch, working tree, and relevant recent commits before modifying files.
- Run `git status --short` before starting work.
- Preserve unrelated user changes, including staged, unstaged, and untracked files.
- Never discard, overwrite, reset, or clean uncommitted work without explicit authorization.
- Never use destructive Git commands to resolve uncertainty about repository state.
- Do not use `git reset --hard`, `git clean`, or equivalent destructive operations unless explicitly authorized.
- Do not rewrite shared Git history without explicit authorization.
- Do not push changes to a remote repository without explicit authorization.
- Do not create commits unless explicitly requested or authorized.
- Do not switch branches when doing so could disrupt or overwrite existing work.
- Do not modify Git configuration, hooks, remotes, or signing settings without authorization.
- Never bypass repository protections or required checks to force a change through.

## Commits

### Commit Message Convention

- Follow Conventional Commits for new commit messages.
- Use the format: `<type>(<scope>): <description>`.
- Use an appropriate type:
  - `feat`: Introduces functionality.
  - `fix`: Fixes a defect.
  - `refactor`: Changes internal structure without changing intended behavior.
  - `perf`: Improves performance.
  - `test`: Adds or updates tests.
  - `docs`: Changes documentation.
  - `build`: Changes build systems or dependency configuration.
  - `ci`: Changes continuous integration or delivery configuration.
  - `chore`: Performs maintenance that does not fit another category.
  - `revert`: Reverts a previous change.
- Use a concise description that explains the change.
- Use an imperative description where practical.
- Do not claim a change is complete if its required implementation or validation is missing.
- Add a scope when it improves clarity, preferably using the affected service or component name.
- Do not amend or rewrite existing commits unless explicitly authorized.

### Branch Naming Convention

- Use descriptive, lowercase branch names with hyphen-separated words.
- Use the following prefixes:
  - `feature/` for new functionality.
  - `fix/` for bug fixes.
  - `hotfix/` for urgent production fixes.
  - `refactor/` for structural improvements.
  - `docs/` for documentation changes.
  - `test/` for test-only changes.
  - `chore/` for maintenance tasks.
- Include the relevant issue identifier when one exists and the repository convention supports it.
- Keep branch names concise and related to the requested work.
- Inspect the current branch before creating or switching branches.
- Do not create a branch automatically if the task's branch requirements are unclear or the operation could disrupt existing work.

### Commit Signing

- Follow the repository's existing commit-signing policy.
- Do not disable signing requirements to make a commit succeed.
- Do not generate, access, or modify signing keys without explicit authorization.
- If signing is required but unavailable, report the blocker rather than bypassing the requirement.

## Pull Requests

### Required Description Format

When preparing a pull request, include the following sections unless the repository provides its own template:

- **Summary:** What changed and why.
- **Changes:** The main implementation or configuration changes.
- **Validation:** Tests, builds, and checks performed, including their results.
- **Risks:** Relevant compatibility, security, migration, or operational risks.
- **Deployment and Rollback:** Required deployment steps, migrations, or rollback considerations, when applicable.
- **Related Issues:** Relevant issue or task references, when available.

Do not invent issue references, validation results, approvals, or deployment outcomes.

### Review Requirements

- Follow the repository's branch protection and code review policies.
- Require the designated reviewers and approvals before merging.
- Do not approve your own changes on behalf of another reviewer.
- Do not merge a pull request without the required approvals and successful mandatory checks.
- Address relevant review feedback before merging.
- Do not dismiss review comments or resolve conversations without an appropriate resolution.
- Do not bypass review requirements without explicit authorization from an authorized maintainer.

### Required CI Checks

- Follow the checks configured by the repository and its branch protection rules.
- Run applicable local validation before requesting review.
- Require mandatory CI checks to pass before merging.
- Do not assume that a check passed merely because it was started.
- Report failed, skipped, unavailable, or inconclusive checks accurately.
- Do not disable, weaken, or bypass required CI checks without explicit authorization.
- Do not invent CI requirements that are not established by the repository or organizational policy.

## Change Management

- Keep changes focused on the requested task.
- Avoid unrelated refactoring, formatting changes, or dependency upgrades.
- Inspect the diff before completion.
- Review staged and unstaged changes separately when relevant.
- Check for accidental changes, secrets, credentials, generated files, and unrelated modifications.
- Never stage all repository changes blindly when unrelated user work may be present.
- Stage only the files or changes relevant to the requested task.
- Do not include user changes in a commit without explicit authorization.
- Report the relevant files changed and the validations performed.
- Disclose failed checks, incomplete work, assumptions, and known limitations.
- Do not claim that changes were committed, pushed, or merged unless those actions actually succeeded.

## Validation

Before completing a task:

- Run `git status --short` to inspect the working tree.
- Run `git diff --check` to detect whitespace errors in unstaged changes.
- Review `git diff` for unstaged changes.
- Review `git diff --cached` for staged changes.
- Confirm that unrelated user changes remain untouched.
- Run applicable builds, tests, linters, and security checks required by the task.
- Inspect the final working tree and report any remaining changes.

When relevant, inspect recent history and repository conventions using commands such as:

- `git log -5 --oneline`
- `git branch --show-current`
- `git diff --stat`

Do not assume that an empty diff means the task was completed successfully; verify the actual repository state and the intended changes.

## Implementation Guidance

Before performing Git operations:

1. Inspect the working tree and current branch.
2. Identify existing repository conventions and applicable contribution policies.
3. Preserve unrelated user changes.
4. Make the smallest appropriate change.
5. Review the final diff and run relevant validation.
6. Report the results and any remaining limitations.

Treat commit creation, branch changes that may disrupt work, history rewriting, pushing, and merging as explicit workflow decisions rather than automatic consequences of modifying code.
