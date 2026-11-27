# ADR-0010: Python-free full web file reads

## Status

Accepted on 2026-10-30 after Python-free SSH acceptance and regression.

## Context

Full web file reads currently use Python for root normalization, size checks, content encoding and modification time. Public content is base64 and can represent arbitrary bytes. Head/tail, listing, hashing and writes have distinct contracts.

## Decision

Use a fixed environment-cleared Bash program with compatible GNU utilities and Linux descriptor paths. Normalize and enforce the existing root boundary, reject non-regular files and observed oversize files, read through a checked descriptor with a maximum-plus-one probe, and validate a versioned response on the host. Reuse stat's timestamp conversion. Preserve the existing general SSH reception path and document its utility-level rather than streaming limit. No new site configuration, sudo or remote helper is introduced.

## Alternatives

Keeping Python retains the dependency. A dedicated remote helper adds installation requirements. Shell-generated JSON complicates byte and path escaping. Base64 framing preserves arbitrary content and special paths with a smaller remote program.

## Scope and security

The normal site, content-type and SSH channel policy remains in force. Missing tools fail without installation, fallback or retry. No remote writes occur. Descriptor checks do not make normalization and opening atomic, and concurrent writes may change content. Hash guarantees are not claimed. Full reads alone migrate in this step.

## Verification and operation

Validate empty, arbitrary byte and boundary-size content, missing files, links, special names, timestamps, missing utilities, malformed responses, timeout and cancellation. Use a non-root Python-free Linux SSH fixture with side-effect audit and positive controls. Run provider/MCP regression, update bilingual scenarios/results, publish and verify the local installation before Git delivery. A public release requires separate authorization.

Acceptance passed 27 SSH conditions: 24 dispatched cases have zero audit violations and unchanged synthetic file snapshots; three policy cases are rejected before dispatch. A separate instrumented growth case read exactly 65 bytes for a 64-byte limit and discarded content. Regression passed 1113 tests, with zero failures and three Unix-only skips. NuGet advisory retrieval was unavailable; offline build validation does not claim a vulnerability audit.
