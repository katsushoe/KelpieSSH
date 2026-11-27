# ADR-0013: Python-free Nginx log reads

## Status

Accepted on 2026-11-01 for Bash/GNU targets after unit, isolated SSH, legacy and timezone comparison acceptance. Other utility/OS implementations remain unverified.

## Context

The bounded log reader uses the SSH host clock and local timezone for timestamps without an offset. Moving filtering to Windows must not use Windows timezone rules. Python is unnecessary for byte reads, but replacing its datetime behavior requires remote timezone evidence.

## Decision

Use a fixed Bash/GNU reader with bounded Base64 transport for raw log bytes, remote UTC time and the remote TZif data or POSIX TZ specification. The application validates this envelope, performs replacement UTF-8 decoding and Python-compatible row selection, and filters timestamps using the supplied remote timezone. No public schema or configuration keys change. Retain the existing profile, path, mode and timeout gates. Preserve the row-limit-before-time-filter ordering, cutoff inclusion, NUL rejection and warning markers.

Interpret repeated local times with the first occurrence and nonexistent local times with the pre-transition offset, matching the legacy fold=0 path. TZif transitions and supported POSIX rules supply offsets; unsupported timezone encodings fail explicitly instead of using local Windows data. No remote timezone installation or clock modification occurs.

## Alternatives

Keep Python: useful as a comparison oracle, but retains the target dependency. Use the Windows timezone database: rejected because it can differ from the SSH host. Run date once per log line: rejected because up to 5000 subprocesses would threaten the existing deadline. Add a target helper: rejected because this read-only route needs no new deployment.

## Security and operations

Read only approved canonical regular log paths. Bound raw bytes, timezone bytes and transport output independently. Preserve target TZ selection while fixing utility search paths and locale. Do not invoke an application runtime, sudo, or install tools. Reject malformed metadata and unsupported rules without returning unfiltered content. Existing race limitations remain explicit; this is not a descriptor-based confinement guarantee.

## Implementation and acceptance

Application-owned wire and timezone parsers are separate from the public provider. Verify byte/text boundaries, remote clock skew, numeric offsets, DST folds/gaps, invalid timestamps, error mapping and timeout/cancellation against the legacy implementation. Compare on isolated nonroot SSH targets with and without Python. Update paired specifications, scenarios and results before delivery. Injected failures do not establish physical power-loss behavior.

Specification sources: [TZif format](https://www.rfc-editor.org/rfc/rfc9636.html), [Python datetime](https://docs.python.org/3/library/datetime.html).
