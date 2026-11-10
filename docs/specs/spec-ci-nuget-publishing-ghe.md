---
title: NuGet Publishing from GHE
created: 2026-11-10
updated: 2026-11-10
status: draft
---

# NuGet Publishing from GHE

> Keep package publishing on GitHub Enterprise Cloud with scoped API keys until nuget.org supports the enterprise's OIDC issuer.

## Decision and status

The current operating decision is to continue publishing from `https://bridgingitgruppe.ghe.com/bit-intern/bITdevKit` with a scoped NuGet API key. The key has a maximum lifetime of 30 days. Rotation remains a manual operation. Follow the [current 30-day API key workaround](#current-30-day-api-key-workaround) to set up, rotate, or recover this credential.

Trusted Publishing is a future migration, blocked by external GHE.com support. This specification defines the prerequisites and expected behavior. It does not implement the migration, change repository settings, or authorize a release.

## Upstream tracking

Tracking issue: [NuGet/login #46: Support OIDC tokens from GHE.com](https://github.com/NuGet/login/issues/46).

The primary release maintainer owns follow-up, with the backup maintainer covering absences. Subscribe to the issue and review updates during each API key rotation and before proposing the migration. Record the review date and any supported configuration or delivery guidance in this section.

Last upstream review: **2026-11-10**. The issue remains open, with no maintainer response, assignee, milestone, or announced delivery date. Recheck this status before implementation.

Keep the 30-day API key workaround in place until R2 is satisfied. An issue closure, announced target date, or merged pull request alone does not prove that the required support is deployed on nuget.org.

## Scope

Included:

- Rotation and recovery for the existing `NUGET_API_KEY` secret.
- A check for GHE.com support during each rotation and before a migration proposal.
- A future change to authentication in the existing CI publishing job.
- Verification that publishing stays on the canonical GHE repository.
- An explicit rollback path if Trusted Publishing fails.

Excluded:

- Moving package publishing to the public GitHub.com mirror or another CI provider.
- Automatically switching authentication when an upstream issue changes state.
- Automated nuget.org account login, browser-driven key renewal, or a custom token broker.
- New vault infrastructure, scheduled reminder workflows, or a permanent authentication abstraction.
- Changes to toolkit layers, package contents, version calculation, or public APIs.

## Current workflow contract

The source of truth is [github-actions.yml](../../.github/workflows/github-actions.yml), with operational context in the [workflows README](../../.github/workflows/README.md).

- `version`, `build`, and `tests` precede package publishing. The `publish` job depends on `version` and `tests`.
- Tags are publishable. Branch previews require the comma-separated `PUBLISHABLE_BRANCHES` repository variable, whose workflow default is empty.
- A branch run on a tagged commit leaves publishing to the tag run.
- Packing requires a publishable ref. Pushing additionally requires `NUGET_PUSH=true`.
- The push step receives `secrets.NUGET_API_KEY` through its `API_KEY` environment variable.
- The destination is `https://api.nuget.org/v3/index.json`.
- Existing package versions are skipped. Transient failures have bounded retries, quota waits use `NUGET_QUOTA_WAIT_STEPS`, and the `nuget-publish` concurrency group serializes publishing within a repository.
- Pushes that change only `.github/workflows/**` or `.github/aw/**` do not trigger CI. Manual dispatch is available.

These behaviors must survive the authentication migration. The concurrency group does not coordinate separate GHE and GitHub.com repositories. The current workflow also has no explicit canonical-host check. A host check is a future requirement below, not an existing guarantee.

## External dependency

GitHub documents a separate issuer for GHE.com. The expected default issuer for this enterprise is `https://token.actions.bridgingitgruppe.ghe.com`, rather than the GitHub.com issuer `https://token.actions.githubusercontent.com`. See [GitHub's GHE.com OIDC substitutions](https://docs.github.com/en/enterprise-cloud@latest/actions/reference/security/oidc#substituted-values-on-ghecom).

At this review, NuGet's [GitHub token validator](https://github.com/NuGet/NuGetGallery/blob/main/src/NuGetGallery.Services/Authentication/Federated/GitHubTokenPolicyValidator.cs) uses the GitHub.com issuer. The open support request and this implementation are the basis for deferring migration. No live token exchange from this GHE repository has been qualified by this specification.

Changing the policy's repository owner to `bit-intern`, changing the token audience, or using a self-hosted runner does not establish trust for the enterprise issuer. A policy for the public GitHub.com mirror does not cover the GHE repository.

## Configuration and ownership

The identities have separate purposes:

- Publishing host: `bridgingitgruppe.ghe.com`.
- GitHub organization and repository: `bit-intern/bITdevKit`.
- Workflow file: `.github/workflows/github-actions.yml`.
- NuGet package owner: `BridgingIT`.
- Allowed package pattern: `BridgingIT.DevKit.*`.
- Current credential: GHE Actions repository secret `NUGET_API_KEY`.
- Publishing switch: GHE Actions repository variable `NUGET_PUSH=true`.

Keep `NUGET_PUSH=false` on the public mirror. Source and tag synchronization may continue; synchronizing a tag does not authorize package publishing from another host.

Assign a primary release maintainer and a backup who can manage the NuGet organization and update the GHE secret. Record the key name, issuing account, issue time, expiry time, and next rotation date in the team's approved operational record. Keep credential values out of that record, source control, issue comments, logs, and artifacts.

## Requirements and acceptance criteria

### R1. Maintain publishing with scoped API keys

As a release maintainer, I want to replace the publishing credential before it expires, so that releases can continue from GHE.

Ready: **Yes**, as an operational procedure. This document does not establish that the current secret has already been replaced.

1. Given the API-key approach, when a key is created, then its owner is `BridgingIT`, its package pattern is `BridgingIT.DevKit.*`, and its scopes allow new packages and new versions without unlisting access.
2. When a key is issued, then its actual expiry is recorded and the next rotation is scheduled for the earlier of 21 days after issuance or seven days before expiry. A key with less than seven days remaining needs immediate attention.
3. When the replacement is installed, then it updates `NUGET_API_KEY` on the canonical GHE repository without changing package versions or publish conditions.
4. When the key is absent, expired, revoked, or rejected, then publishing fails without exposing the value. It does not switch hosts or credentials automatically.
5. When every candidate package already exists, then a skipped push run is not treated as proof that the replacement key can publish.

### R2. Qualify GHE.com Trusted Publishing support

As a release maintainer, I want evidence that nuget.org trusts our GHE identity, so that we can migrate without breaking releases.

Ready: **No**. NuGet must provide supported GHE.com issuer and policy handling before implementation can proceed.

1. When support is proposed, then the review links current NuGet documentation or explicit maintainer instructions covering GHE.com, including host or issuer identification in the trust policy.
2. Given those instructions, when a policy is prepared, then it identifies `bridgingitgruppe.ghe.com`, `bit-intern/bITdevKit`, and `github-actions.yml` through the supported configuration. Field names and action versions must be resolved from that guidance.
3. When authentication is qualified, then a run of the actual trusted workflow on GHE successfully exchanges its OIDC token for a temporary NuGet key. A GitHub.com run, decoded token, or local syntax check alone is insufficient.
4. When the issuer, workflow, or policy does not match, then authentication fails and no package push occurs.
5. If supported configuration or a successful GHE exchange is missing, then the API-key approach remains in effect and the migration stays blocked.

### R3. Replace authentication in the existing job

As a release maintainer, I want the existing GHE publishing job to obtain temporary credentials, so that routine API key rotation can end.

Ready: **No**. Depends on R2 and an agreed release for rollout.

1. When publishing is evaluated, then only `github.server_url == 'https://bridgingitgruppe.ghe.com'` together with `github.repository == 'bit-intern/bITdevKit'` may authorize a push. Another host or repository cannot authenticate or push, even if its `NUGET_PUSH` variable is enabled.
2. For normal release and preview runs, given a publishable ref and `NUGET_PUSH=true`, when packing succeeds, then login runs immediately before pushing. Disabled publishing, ineligible refs, and failed packing do not request publishing credentials. The deliberate authentication-only qualification below is a temporary exception.
3. When OIDC permissions are added, then `id-token: write` is limited to the publishing job. Explicit job permissions retain the reads needed for checkout and artifact download without changing other jobs' permissions.
4. When a temporary key is returned, then the existing push logic consumes it without logging or persisting its value. Credential acquisition accounts for the documented lifetime and existing retry waits; authentication failures do not silently fall back to `NUGET_API_KEY`.
5. When migration is complete, then existing versioning, packing, duplicate handling, retries, concurrency, and summaries still work, and the obsolete stored publishing key is removed and revoked.

Use the supported NuGet login action rather than a custom exchange implementation. The current [Trusted Publishing guide](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing) describes `NuGet/login`, a temporary key output, and a one-hour key lifetime. Recheck that contract when GHE.com support ships. This specification deliberately does not prescribe executable OIDC YAML before then.

If the supported action requires a username setting such as `NUGET_USER`, use the NuGet login username required by that action. Do not substitute the `BridgingIT` organization name. Recheck organization membership and policy-creator requirements at rollout. There is currently no GitHub Actions environment on the publishing job; adding one is a separate rollout choice that must match the policy.

### R4. Recover without moving a released tag

As a release maintainer, I want an explicit rollback procedure, so that an authentication failure does not require changing an existing release's identity.

Ready: **Yes**, as recovery requirements for the migration proposal.

1. When Trusted Publishing fails, then operators can disable further publishing with `NUGET_PUSH=false` while build and test work remains available.
2. When API-key publishing is restored, then a workflow revision explicitly restores the secret-based step and uses a valid scoped key. An expired or revoked key is not a rollback credential.
3. When a new workflow revision is required, then it is committed through the normal repository process and included in a new release ref. Existing release tags are not moved or force-pushed.
4. Given a partially published version, when recovery is proposed, then operators inspect which packages exist and choose a reviewed recovery revision. Duplicate skipping must not be represented as successful publication of every package.
5. When recovery completes, then the operational record identifies the active authentication approach, next key expiry if applicable, and remaining upstream blocker.

## Current 30-day API key workaround

This is the current approach for publishing directly from GHE while Trusted Publishing support is unavailable. It uses the existing workflow and requires no OIDC policy, `NUGET_USER` setting, or OIDC permission change. The key expires after at most 30 days even if it is actively used. A successful release does not renew it.

### Create and install the key

1. Sign in to nuget.org with an account that can publish for the `BridgingIT` organization. Open the account menu, select **API Keys**, and choose **Create**.
2. Name the key `bitdevkit-ghe-ci` and choose an expiry of 30 days, or a shorter period if required by organizational policy.
3. Select package owner **BridgingIT**. Restrict the package glob pattern to `BridgingIT.DevKit.*` and allow **Push new packages and package versions**. Do not grant unlisting access.
4. Create the key and copy it directly into the approved secret-management interface. The value is shown only during creation or regeneration; do not put it in chat, commands, source files, or screenshots.
5. Open the [GHE repository's Actions secrets](https://bridgingitgruppe.ghe.com/bit-intern/bITdevKit/settings/secrets/actions), under **Settings > Secrets and variables > Actions**. Add or update the repository secret named `NUGET_API_KEY` with the copied value.
6. In the same repository's **Variables** tab, set `NUGET_PUSH` to `true`. Leave `PUBLISHABLE_BRANCHES` unset or empty for tag-only publishing. Keep `NUGET_PUSH=false` on the public GitHub.com mirror.
7. Record the actual expiry, responsible maintainer, backup maintainer, and next rotation date. The default rotation is after 21 days; use the earlier deadline defined in R1 for shorter-lived keys.

The existing push step already binds the credential through `API_KEY: ${{ secrets.NUGET_API_KEY }}`. Installing a replacement secret takes effect for subsequent job executions without a workflow commit or a new release tag.

The account that creates the key must have the required organization access. A backup maintainer may issue a replacement using their own authorized account; recovery must not depend on shared personal login credentials. See [NuGet scoped API keys](https://learn.microsoft.com/en-us/nuget/nuget-org/scoped-api-keys).

### Rotate before expiry

1. Review issue #46 and current NuGet guidance. Continue with API keys unless R2 can be satisfied.
2. Create a separate replacement key with the settings above while the old key is still valid. This permits overlap. Regenerating the existing key invalidates its previous value immediately.
3. Update the GHE `NUGET_API_KEY` secret and record the new expiry and rotation date. Confirm that expiry notifications reach the responsible maintainers. This specification creates no automated reminder or renewal workflow.
4. Verify the replacement during the next authorized release before expiry. Confirm that at least one previously unpublished package version was accepted and that the expected package set becomes available. A run that skips every package does not verify publishing access.
5. Revoke the superseded key after verification. Do not upload a disposable production package solely to test a credential.

### Recover after expiry or rejection

1. Create a fresh scoped key and update the GHE secret as described above. If the old key already expired, overlap is unavailable; do not attempt to reuse it.
2. Check the failed release run. Confirm that its source builds and packs successfully and that authentication caused the failure. A missing package file, failed test, or packing error requires its own correction.
3. For an otherwise valid authorized release, re-run the failed publishing job if its `BuildOutput` artifact is still available. That artifact currently has one-day retention. If it expired, re-run the complete workflow from the same valid release ref to rebuild the inputs.
4. Let duplicate handling skip versions already accepted during a partial publication. Confirm the results for every expected package before treating recovery as complete.
5. If recovery requires source or workflow changes, use a reviewed revision and release process. Do not move an existing release tag to make the retry pick up newer code.

Updating a secret addresses credential failures. It does not remove NuGet quotas, renew a key automatically, or repair unrelated release failures. Until R2 is satisfied, continue the rotation procedure for each replacement key.

## Future migration sequence

1. Confirm the documented GHE.com support required by R2. Record the supported action version, policy settings, issuer, and any enterprise prerequisites in the migration PR. R2 remains incomplete until the live exchange in step 4. Keep tokens and temporary keys out of that evidence.
2. Prepare one focused workflow change for the existing `publish` job. Add the canonical GHE identity check and preserve the current package evaluation. Update the workflows README with the actual configuration and rollback instructions.
3. Arrange an authentication-only qualification in the same trusted workflow, with package pushing explicitly disabled for that qualification. Normal `NUGET_PUSH=false` behavior skips login and is not an OIDC test. The migration PR must explain the deliberate qualification path and remove temporary diagnostic controls afterward.
4. Verify conditions and failure paths with static checks and isolated shell checks. Use a GHE run to prove token exchange and complete R2 before enabling Trusted Publishing for releases. Do not dump raw token claims or credentials into logs.
5. Select an authorized release whose tag contains the updated workflow. Workflow-only pushes require manual validation. Re-running an older run or dispatching an older tag does not load a newer workflow revision.
6. Complete a real package publication from GHE. Confirm that a previously unpublished version was accepted, expected packages become available, and the public mirror did not publish. A successful login followed only by duplicate skips is incomplete rollout evidence.
7. Remove the obsolete `NUGET_API_KEY` secret, revoke the old key, and end its rotation schedule. Mark the specification implemented only after these steps and the workflow documentation are complete.

Rollback uses R4. Retaining a valid API key during qualification is acceptable, but the Trusted Publishing path must never consume it as a fallback. After cleanup, rollback requires issuing a fresh key if no valid one remains.

## Verification scope

Use normal repository checks and focused workflow validation. Do not add a separate acceptance framework or run the full .NET test suite solely for an authentication-only YAML change.

The migration review must cover:

- Eligible release tag, publishing enabled: login after packing, followed by package push.
- Normal runs with publishing disabled, an ineligible branch, failed tests, or failed packing: no login and no push. Check the temporary authentication-only qualification separately.
- Explicitly configured preview branch: existing preview behavior remains available on GHE.
- Branch pointing at a tagged commit: publishing remains the tag run's responsibility.
- GitHub.com mirror or another repository with publishing enabled: no login and no push.
- Missing username setting, mismatched policy, or denied issuer: actionable failure without credential disclosure or fallback.
- Existing versions and partial prior publication: duplicate handling and per-package results remain accurate.
- Throttling and quota waits: bounded recovery stays within the temporary credential's supported lifetime, or fails for a fresh run instead of retrying expired credentials indefinitely.
- Explicit API-key rollback: valid key required, canonical GHE identity preserved, and no released tag moved.

Keep run links and application-specific execution evidence in the migration PR or approved operational record, outside `docs/`. Documentation may describe the procedure and final supported configuration.

## Completion criteria

Until upstream support is qualified, completion means that the scoped key is maintained and the next rotation has an owner and a date. It does not mean that Trusted Publishing is implemented.

The future migration is complete when R2 and R3 are proven on GHE, a real release succeeds, rollback instructions are current, the public mirror cannot publish, and the stored NuGet publishing key has been retired.

## References

- [NuGet API key lifetime changes](https://devblogs.microsoft.com/dotnet/strengthening-nuget-supply-chain-security-reducing-api-key-lifetime/): source for the current 30-day maximum, effective from August 17, 2026, and expiry of earlier keys on November 1, 2026.
- [GHE.com support request](https://github.com/NuGet/login/issues/46): upstream dependency and future status updates.
- [NuGet Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing): supported providers, policy configuration, action usage, and credential lifetime.
- [GitHub workflow re-run behavior](https://docs.github.com/en/actions/how-tos/manage-workflow-runs/re-run-workflows-and-jobs): re-runs retain the original commit and ref.
