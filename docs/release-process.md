# Release Process

> **Status:** Bootstrap stub created alongside APP-8. Owned by review-deployment-squad
> (REL-4/REL-5). Expand when `release-please` patch (`rel-4-5.patch`) lands.

## Version surface

- **`version.txt`** at repo root is the single source of truth for the human-readable
  release version. `release-please` (REL-4) bumps this file on every release PR.
- **`GET /api/version`** (APP-8) exposes the deployed image's identity at runtime:

  ```json
  {
    "version": "0.1.0",
    "commit": "<git sha>",
    "buildTime": "<iso-8601 UTC>"
  }
  ```

  Fields:
  - `version` — read from `version.txt` at content root at startup.
  - `commit` — injected at container build time from the `GITHUB_SHA` build arg
    (CI passes `${{ github.sha }}`). Falls back to `"unknown"` for local dev.
  - `buildTime` — injected from the `BUILD_TIME` build arg (ISO-8601 UTC). Falls
    back to process start time when absent.

  Smoke tests (`tests/smoke/`, QA-1) assert this endpoint returns 200 and a
  non-empty `version` against the deployed staging URL.

## Release flow (post REL-4)

1. Merge feature PRs to `main` with Conventional Commits.
2. `release-please` opens/updates a release PR bumping `version.txt` + CHANGELOG.
3. Merge the release PR — tag `vX.Y.Z` is created and a GitHub Release is published.
4. `deploy-prod.yml` (REL-5) triggers on `release: published` and runs through the
   `prod` environment approval gate.
5. Post-deploy smoke run verifies `/api/version` reports the just-shipped tag.
