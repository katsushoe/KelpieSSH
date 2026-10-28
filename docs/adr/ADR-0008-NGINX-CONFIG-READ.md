# ADR-0008: Python-free nginx configuration reading

## Status

Implemented on 2026-10-28. Disposable Linux/SSH acceptance and release regression passed; deployment verification is tracked separately.

## Context

The existing fixed nginx configuration reader requires Python for an approved path, a regular-file check and bounded UTF-8 content. It is used by discovery, read/access checks and pre-mutation checks. An SSH connection must enforce policy and limits before retaining output, and a path may change between canonicalization and opening.

## Decision

Use a fixed nonprivileged shell program with a cleared environment and GNU-compatible system utilities. Canonicalize against the existing approved exact-file and direct-directory rules, open the selected regular file once, verify its descriptor path and read at most the configured limit plus one byte. Encode the bytes as bounded base64 for strict host-side validation and preserve the public result model. Prevent retry of the same failed main read after discovery. Keep exceptions and raw content out of malformed-response errors. Use the normal command policy and existing cancellation semantics.

Expand the opt-in SSH stdout limit ceiling from 1048576 to 2097152 bytes so the existing maximum internal reader limit of 999999 bytes plus one can be encoded without silently reducing its accepted range. The reader selects 1333340 stdout and 2048 stderr bytes; other existing limits do not change.

## Alternatives

Keeping Python retains the dependency. Directly forwarding unvalidated text cannot safely establish bounded binary and strict UTF-8 behavior. A privileged helper expands installation and permission requirements for a read-only operation. Generic interpreter execution is outside the approved operation boundary. All are rejected for this reader.

## Scope, security and operations

The decision affects nginx main/include reads and operations that inspect that content; it does not replace the nginx log reader, Web file backend, or privileged helper. `/bin/bash`, compatible `base64`, `realpath`, `dirname`, `head`, `/usr/bin/env`, `/bin/sh`, and `/proc/self/fd` are required. Missing functionality fails without installation or interpreter fallback. Descriptor validation reduces path replacement races but is not an atomic guarantee against privileged target mutation. No remote file is created or written by this read.

## Implementation and verification

Application owns the fixed template, byte response decoder, and result mapping. Infrastructure continues to own the bounded SSH reception. English/Japanese MCP documentation, scenarios and result records accompany the implementation. The 16-condition disposable Linux/SSH plan covers path rules, content boundaries, unsupported tools, policy, transport failure and side effects. Windows MSYS and unit checks are supporting evidence only; they do not substitute for Linux/SSH acceptance. No release follows automatically from this ADR.
