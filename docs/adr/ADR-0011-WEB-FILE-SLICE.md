# ADR-0011: Python-free web file slices

## Status

Accepted on 2026-10-30 after Python-free SSH acceptance and regression.

## Context

The previous script reads bounded bytes, selects byte-based lines and emits base64. Tail seeks using a size observed before opening. Replacing it with a direct end-relative tail command changes growth-race behavior. Text line APIs can change raw bytes and line boundaries.

## Decision

Use GNU head for head bytes and GNU dd byte-count/skip flags for the observed tail offset. Perform Python-compatible byte line selection on the host with a bounded queue for tail line starts. Reuse the full-read response framing and stat timestamp conversion, and bound SSH reception within the existing two-MiB transport contract. Keep public policy, arguments, warnings and byte representation unchanged.

## Alternatives

Retaining Python keeps the dependency. GNU tail at the current end changes observed-offset semantics. Text decoding can replace invalid bytes and recognize extra line boundaries. A dedicated remote helper adds installation requirements. The fixed utility reader and host byte processing avoid these differences.

## Scope and security

No new configuration, sudo or remote writes are introduced. The descriptor is checked, but normalization/open and concurrent writes are not atomic. Missing tools and invalid or oversized responses fail without fallback or retry. Hash and listing are separate migrations. Tool annotations and the existing MCP response representation are unchanged.

## Verification and operation

Compare host results with the original Python byte algorithm, then test actual SSH head/tail, CR/LF/CRLF, byte boundaries, arbitrary bytes, missing and nonregular files, links, permissions, warnings, modes, output limits, timeout/cancellation and deterministic growth/shrink controls. Use a non-root Python-free Linux fixture with audit positive controls. Run regression, update bilingual scenarios/results, publish, verify protected installation state and deliver through the registered Git gateway. Public release requires separate authorization.

Verification passed 1760 host-provider comparisons with CPython 3.12.14 and 58 actual SSH conditions. The 50 dispatched cases had zero audit violations and unchanged synthetic file snapshots; eight policy cases were rejected before dispatch. Separate injected growth/shrink controls confirmed the observed tail offset 120 for an initial 128-byte file and an eight-byte limit: growth returned eight original bytes and shrink returned zero bytes. Head growth read eight bytes. Regression passed 1136 tests, with zero failures and three Unix-only skips. External NuGet advisory retrieval remains unverified; offline build validation is not a vulnerability audit.
