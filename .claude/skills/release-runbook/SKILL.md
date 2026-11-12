---
name: release-runbook
description: Use when drafting, creating, or updating a GitHub release for elastic/elastic-transport-net, or when asked to release, tag, or publish a new Elastic.Transport version.
---

# elastic-transport-net release runbook

How to prepare a GitHub release for `elastic/elastic-transport-net`. Do not write release notes: the release run of
`ci.yml` generates them and replaces the body of the release.

## What publishing starts

Publishing the release creates the tag, and the tag push starts the `ci.yml` run. That run does the following, in
order:

1. Builds the notes from the closed PRs and issues that carry the version label `vX.Y.Z`. The type label of each one
   sets its section: `bug` goes to "Bug Fixes", `documentation` to "Docs Improvements", `enhancement` to "New
   Features", and anything else to "Uncategorized".
2. Appends a report of the public API changes of each package.
3. Replaces the body of the GitHub release with the result.
4. Creates the version labels for the next major, minor and patch release.
5. Pushes the packages to nuget.org.

## Conventions (must follow)

**Always create the release as a draft.** Use `gh release create ... --draft` and never publish. The user reviews the
draft and publishes it. A request to "create release X.Y.Z" or to "release X.Y.Z" is not authorization to publish, so
default to `--draft` even when the request does not say "draft". A published NuGet version cannot be replaced.

**`targetCommitish` is `main`.** The repo has no release branches.

**The tag and the title are the bare version.** Write `1.2.0`, never `v1.2.0`. MinVer derives the package version from
the tag, so a wrong tag publishes a wrong version. Only the version labels carry the `v` prefix.

**Read the version from the user's request.** If the request names none, propose one by semver against the previous
release (breaking change: major, new feature: minor, otherwise patch) and ask before you continue.

**The notes come from the labels.** A PR without the version label is missing from the notes. Leave the body of the
release empty, because the release run overwrites it.

## Workflow

### 1. List the PRs since the previous release

Find the previous release:

```
gh release list -R elastic/elastic-transport-net --limit 10
```

List the PRs merged into `main` since the date of that release, with their labels:

```
gh pr list -R elastic/elastic-transport-net --state merged --base main \
  --search "merged:>=<prev-release-date>" \
  --json number,title,author,labels,url
```

### 2. Complete the labels

Each PR that belongs in the notes needs the version label `vX.Y.Z` and one type label (`bug`, `documentation` or
`enhancement`). Show the user the PRs that lack a label, with the labels you propose, and add them after the user
agrees:

```
gh pr edit <num> -R elastic/elastic-transport-net --add-label "vX.Y.Z" --add-label "bug"
```

Ask the user when it is unclear whether a PR belongs in the notes. The version label normally exists, because the
previous release run created it. If it does not exist, create it:

```
gh label create "vX.Y.Z" -R elastic/elastic-transport-net --color e3e3e3
```

### 3. Create the draft

```
gh release create X.Y.Z -R elastic/elastic-transport-net \
  --target main \
  --title X.Y.Z \
  --draft \
  --notes ""
```

Hand the URL of the draft to the user.

## Common mistakes

| Mistake | Why wrong |
|---------|-----------|
| Publishing the release, or omitting `--draft` | Publishing starts the NuGet release, so the user does it |
| Targeting anything other than `main` | Releases are cut from `main` |
| Tagging as `v1.2.0` | Tags have no `v` prefix, only the version labels have it |
| Writing release notes into the draft | The release run overwrites the body |
| Leaving a PR without the version label | The PR is missing from the notes |
