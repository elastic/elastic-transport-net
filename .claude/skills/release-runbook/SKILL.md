---
name: release-runbook
description: Use when drafting, creating, or updating a GitHub release for elastic/elastic-transport-net, or when asked to release, tag, or publish a new Elastic.Transport version.
---

# elastic-transport-net release runbook

How to draft a GitHub release for `elastic/elastic-transport-net`. Always work from the GitHub auto-generated baseline
and enhance it. Do not write release notes from scratch.

## Conventions (must follow)

**Always create the release as a draft.** Use `gh release create ... --draft` and never publish. The user reviews the
draft and publishes it. A request to "create release X.Y.Z" or to "release X.Y.Z" is not authorization to publish, so
default to `--draft` even when the request does not say "draft". Publishing the release creates the tag, and the tag
push starts the `ci.yml` run that pushes the packages to nuget.org. A published NuGet version cannot be replaced.

**`targetCommitish` is `main`.** The repo has no release branches.

**The tag and the title are the bare version.** Write `1.2.0`, never `v1.2.0`. MinVer derives the package version from
the tag, so a wrong tag publishes a wrong version.

**Read the version from the user's request.** If the request names none, propose one by semver against the previous
release (breaking change: major, new feature: minor, otherwise patch) and ask before you continue.

**Issue references.** Use `Fixes <full-issue-url>` on a main bullet and the `(#issue)` shorthand on a sub-bullet.

**Every PR gets up to 2 sub-bullets** that describe what changed, what was fixed, or what improved. Skip them when the
title is already fully self-describing.

**Breaking changes are never capped.** Every breaking change gets its own sub-bullet, prefixed `**Breaking**`, listed
after the other sub-bullets, and it does not count against the cap of 2. When a release carries a breaking change,
open the notes with a `> [!WARNING]` callout that tells readers to review the list before upgrading. A cap that drops
a breaking change leaves users to discover it as a compile error after the upgrade.

## Workflow

Five phases: gather, baseline, enhance, review, draft.

### 1. Gather the PRs since the previous release

Find the previous release:

```
gh release list -R elastic/elastic-transport-net --limit 10
```

List the PRs merged into `main` since the date of that release:

```
gh pr list -R elastic/elastic-transport-net --state merged --base main \
  --search "merged:>=<prev-release-date>" \
  --json number,title,author,body,url,closingIssuesReferences
```

### 2. Generate the GH baseline

```
gh api repos/elastic/elastic-transport-net/releases/generate-notes \
  -f tag_name=X.Y.Z -f target_commitish=main -f previous_tag_name=X.Y.W \
  -q .body
```

The result is the skeleton of the notes: `## What's Changed`, the bullets, an optional `## New Contributors` section,
and the `**Full Changelog**: .../compare/X.Y.W...X.Y.Z` line. Use it as it is. Do not add category headers such as
`### Bug Fixes` or `### Features`. Keep `## New Contributors` exactly as GitHub generated it, and do not add the
section when GitHub left it out.

### 3. Enhance each line

**Link related issues.** Take them from
`gh pr view <num> -R elastic/elastic-transport-net --json closingIssuesReferences,body` and render them inline as
`Fixes https://github.com/elastic/elastic-transport-net/issues/<n>`.

**Add up to 2 sub-bullets.** Sources, in priority order:

1. The PR body, when it explains the problem or the fix in plain language.
2. The title or body of the closing issue, for the symptom a user saw.
3. The diff (`gh pr diff <num> -R elastic/elastic-transport-net`), for a small targeted PR.

Skip the sub-bullets when the title already says it all, as with a dependency update or a rename. Do not pad.

**List every breaking change.** Look for a breaking-changes section in the PR body. In the diff, look for a removed or
renamed public member, a changed signature, or reduced visibility. Verify each one against the diff hunk before you
write it into the notes.

Format:

```
* <PR title> by @<author> in <PR url>
  * <bullet 1>
  * <bullet 2>
  * **Breaking** <what a consumer must change>
```

### 4. Pre-draft checklist

- [ ] The user confirmed the version, and the tag has no `v` prefix (`1.2.0`, not `v1.2.0`)
- [ ] `targetCommitish` is `main`
- [ ] Every PR with a related issue references it (`Fixes <url>` or `(#issue)`)
- [ ] Every PR has at most 2 sub-bullets, not counting breaking changes, or none when the title is self-describing
- [ ] Every breaking change is listed and prefixed `**Breaking**`
- [ ] A `> [!WARNING]` callout opens the notes when the release carries a breaking change
- [ ] `## New Contributors` is exactly as GitHub generated it, or absent
- [ ] The `**Full Changelog**: .../compare/<prev>...<current>` line is present
- [ ] No category headers
- [ ] The `gh release create` call includes `--draft`

### 5. Create the draft

```
gh release create X.Y.Z -R elastic/elastic-transport-net \
  --target main \
  --title X.Y.Z \
  --draft \
  --notes-file release-notes.md
```

Hand the URL of the draft to the user. Never include AI attribution anywhere in release notes.

## Common mistakes

| Mistake | Why wrong |
|---------|-----------|
| Publishing the release, or omitting `--draft` | Publishing starts the NuGet release, so the user does it |
| Targeting anything other than `main` | Releases are cut from `main` |
| Tagging as `v1.2.0` | Tags have no `v` prefix |
| Adding `### Bug Fixes` category headers | The GH baseline sets the structure |
| More than 2 non-breaking sub-bullets on a PR | The cap is 2 |
| Dropping a breaking change to respect the cap | Breaking changes are uncapped |
| Padding sub-bullets when the title already describes the PR | Skip them in that case |
