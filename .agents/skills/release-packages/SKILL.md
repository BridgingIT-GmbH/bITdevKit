---
name: release-packages
description: Prepare and publish a bITdevKit release. Updates CHANGELOG.md from the git history since the last tag, commits, creates the annotated version tag, and pushes main and the tag to both remotes (origin and github). Use this when asked to prepare, cut, tag or publish a release such as 10.0.115, bump the version, or push a release to both remotes.
---

# Release version

Repeatable process for cutting a release of the repo. The version comes from the git tag (MinVer), so a release is: changelog entry, release commit, annotated tag, push to both remotes.

## Facts about this repo

- Tags are plain version numbers without a prefix (`10.0.114`, not `v10.0.114`) and are annotated.
- Branch: `main`. Remotes: `origin` (internal GHE) and `github` (public). Both must receive `main` and the tag.
- `CHANGELOG.md` has one `## [x.y.z] - yyyy-mm-dd` section per tag, newest first, plus the intro line at the top. Bullets start with `Added`, `Changed`, `Fixed`, `Removed`, `Updated`, or `Improved`.
- The release commit message is `chore: release x.y.z`.
- Git may print `credential-manager-core is not a git command` on fetch and push. It is harmless when the push result lines follow.

## Rules

- Run git commands one at a time. Never force push, never move or delete an existing tag, never skip hooks.
- The changelog must be updated with the `changelog-generator` skill. Load and follow it for steps 2 to 4 instead of writing entries ad hoc.
- Only push when the user asked to publish or push. If the request is only to prepare, stop after the tag is created locally and say so.
- Never create a tag that already exists. If `git tag --list <version>` returns it, stop and ask.
- Version: if the user gives a version, use it as is. Otherwise increment the patch number of the latest tag by 1 (latest tag `10.0.115` gives `10.0.116`) and proceed without asking. State the chosen version at the start.

## Process

### 1. Preflight

```bash
git branch --show-current          # must be main
git status --porcelain             # review what is uncommitted
git remote -v                      # confirm origin and github exist
git fetch --all --tags             # see if remotes moved
git for-each-ref refs/tags --sort=-creatordate --format="%(refname:short) %(creatordate:short)" | head -5
```

- If the branch is not `main`, or local `main` is behind a remote, stop and report.
- Determine the version `x.y.z` used in the steps below: the user's version if given, otherwise the latest tag with its patch number incremented by 1. Take the latest tag from the list above (newest by creation date), and check that the new tag does not already exist.
- Uncommitted files other than `CHANGELOG.md` are normally included in the release commit (for example `packages.lock.json` changes from a restore). Look at their diff first and mention them in the final summary so the user can object.

### 2. Collect the commits

Use the `changelog-generator` skill (read its SKILL.md first) for this step and the next two. Use the last tag as the start of the range:

```bash
git describe --tags --abbrev=0
git log <last-tag>..HEAD --no-merges --pretty=format:"%h %ad %s" --date=short
```

If the changelog is missing sections for existing tags (a tag newer than the newest changelog heading), reconcile first: list `<previous-tag>..<tag>` for each missing tag and add a dated section for it, using the tag date from `git for-each-ref`.

### 3. Classify and filter

Apply the product-only policy and commit triage workflow of the `changelog-generator` skill. Keep only changes that affect shipped behavior, public API, reliability, performance, security, or supported SDK and package baselines.

Exclude: documentation-only changes, `.agents/` and skill changes, CI and workflow changes, test-only changes, formatting and rename-only refactors, changelog-only commits, and spec or ADR commits.

Check the code before believing a commit message. A commit titled as a feature may only add a spec (for example a "remove X" spec with no code change). Verify with `git show --stat <hash>` and, when unsure, with `git diff <last-tag>..HEAD --stat -- src`. A squash-merge commit can also contain work that already shipped in an earlier tag. If there is no `src` change since the last tag for it, it is not new.

When unsure whether something is user-visible, leave it out and mention it in the final summary.

### 4. Write the changelog section

Write the section through the `changelog-generator` skill, applying the repo-specific format below.

- Insert `## [x.y.z] - yyyy-mm-dd` (today's date, absolute) directly under the intro paragraph, above the previous release.
- If an `## [Unreleased]` or `## [x.y.z] - Unreleased` section exists, rename it to the new version and date instead of adding a second section, then merge in new entries.
- Write user-facing bullets in the existing style. Name the public API, option, or setting that changed. Call out breaking changes explicitly.
- Do not duplicate entries that an earlier section already lists.

### 5. Commit

```bash
git add -A
git commit -m "chore: release x.y.z"
```

Only stage files that belong in the release. If unrelated work is in the tree, ask the user before including it.

### 6. Tag

```bash
git tag -a x.y.z -m "x.y.z"
git tag --points-at HEAD
```

### 7. Push to both remotes

Push each remote separately so a failure on one is visible:

```bash
git push origin main x.y.z
git push github main x.y.z
```

If a push is rejected as non-fast-forward, stop. Do not force. Report which remote rejected it and let the user decide.

### 8. Verify and report

```bash
git ls-remote --tags origin x.y.z
git ls-remote --tags github x.y.z
git log -1 --format="%h %s"
```

Final summary to the user: version, release commit hash, entries added, anything excluded or uncertain, extra files included in the commit, and whether both remotes received `main` and the tag.

## Common pitfalls

- WRONG: Tagging before the changelog commit. CORRECT: Commit first, then tag that commit, so the tag contains the changelog.
- WRONG: Trusting commit titles. CORRECT: Confirm in the diff that the code change shipped.
- WRONG: Creating `v10.0.115`. CORRECT: Use `10.0.115`.
- WRONG: Pushing only one remote. CORRECT: Push `origin` and `github`, then verify with `git ls-remote`.
- WRONG: Re-tagging to fix a mistake after pushing. CORRECT: Ask the user, since moving a published tag affects consumers.

## References

- [changelog-generator skill](../changelog-generator/SKILL.md) for the product-only filtering policy
- [git-commit skill](../git-commit/SKILL.md) for commit message conventions
- [update-packages skill](../update-packages/SKILL.md) to refresh dependencies before a release
- [CHANGELOG.md](../../../CHANGELOG.md)
