# ADR-0009: Python-free web file metadata

## Status

Accepted on 2026-10-30 after isolated SSH acceptance and regression.

## Context

Web file metadata currently depends on Python. The public result includes the resolved path, existence, lstat type and size, octal permissions, owner, group and UTC modification time. Path boundaries and link handling must retain their existing meaning.

## Decision

Use a fixed Bash program with a cleared environment, GNU-compatible base64, realpath and stat, and libc-compatible getent to disambiguate unknown IDs from accounts named UNKNOWN. Return a versioned response containing base64-encoded paths and NUL-delimited metadata. Validate fields and UTF-8 on the host and map them to the existing result. Limit SSH stdout to 65536 bytes and stderr to 2048 bytes. Missing or incompatible utilities fail without automatic installation or interpreter retry.

## Alternatives

Retaining Python preserves the existing dependency. Shell-generated JSON complicates escaping of special names. Reading file metadata through a dedicated remote helper introduces a separate installation prerequisite. The selected approach uses existing system utilities.

## Scope and security

This decision covers metadata only. Hashing retains its existing no-follow open and descriptor identity checks until an equivalent replacement is verified. The metadata command performs no writes or content reads, uses the normal site and SSH policy checks, and does not expand privileges. GNU compatibility is required; other utility implementations need separate verification. Path normalization and lstat are separate operations and do not provide an atomic namespace snapshot.

A GNU tar hashing prototype was rejected because a file-growth race exceeded the requested read limit. Stat is delivered independently; hashing continues to use the existing Python backend.

## Verification and operation

Python-free Debian SSH acceptance passed 18 conditions, including 13 audited conditions with no violations or fixture file changes. Positive controls detected all four prohibited activity categories. Release regression passed 1101 tests with no failures and three Unix-only skips. Unit tests cover provider/MCP results, malformed responses and simulated transport failures. These checks support local deployment; they do not authorize a public release or establish compatibility with every operating system.
