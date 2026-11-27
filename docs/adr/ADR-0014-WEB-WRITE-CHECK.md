# ADR-0014: Python-free Web write checks

## Status

Accepted on 2026-11-02 for the measured Debian12 Bash/GNU SSH scope after regression, nonroot ACL/path audits and legacy comparison.

## Context

The current Python check returns an advisory filesystem permission result shared by public and secret callers. It does not mutate files and does not promise atomic-write success.

## Decision

Use a fixed Bash/GNU metadata-only probe, with a bounded versioned Base64 path envelope adapted to the existing JSON contract in C#. Preserve parent/target containment, symlink distinctions, missing-parent optimism, group/ACL permission evaluation, reason strings, confirmation and helper routing. Keep atomic writes and hash unchanged.

## Alternatives

Keep Python: retains an unnecessary read-only target dependency. Attempt a real write: rejected because checking must not mutate. Use only Unix mode bits: rejected because ACL/group/traversal behavior differs. Install a helper: unnecessary for this probe.

## Security and operations

No content read, probe-file write, sudo, installation or interpreter fallback. Fix PATH/locale, ignore profile environment overrides, bound transport/path data, retain timeout/cancellation and fail safely. Require ordinary SSH identities with matching real/effective user IDs. Canonical string checks do not eliminate replacement races.

## Implementation and validation

Pair [the contract](../WEB_WRITE_CHECK.md), public tool references, scenarios and results. Compare legacy Python on isolated nonroot owner/group/ACL/traversal/symlink fixtures and audit syscall behavior. Verify ordinary/secret/helper output mapping, malformed and excessive transport, then regression, publish, protected deployment state and Git delivery. Unsupported utilities/OS remain unverified.
