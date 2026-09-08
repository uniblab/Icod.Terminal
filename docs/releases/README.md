# Release Notes

This directory contains the curated GitHub Release body for each publishable `Icod.Terminal` version.

The tagged release workflow requires an exact file named:

```text
docs/releases/<package-version>.md
```

For example:

```text
docs/releases/1.0.0-rc1.md
docs/releases/1.0.0.md
```

The file is part of release qualification. It should be written for package consumers rather than as an internal development diary and should normally cover:

- release purpose/status;
- breaking changes and migration;
- supported TFMs/platforms;
- important contract or feature changes;
- security/compatibility boundaries;
- package/downstream validation evidence;
- known limitations or scope boundaries;
- links to permanent documentation and the changelog.

`release.yaml` does not fall back to automatically generated GitHub notes when this file is missing. A tag is therefore not publishable until its curated release notes exist in the tagged commit.
