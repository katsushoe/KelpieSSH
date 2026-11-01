# Web write-check contract

For Japanese, see [the Japanese contract](ja/WEB_WRITE_CHECK.ja.md).

## Behavior

Web public and secret write checks preserve existing site/path/content rules, profile modes, confirmation and helper routing. The fixed remote Bash/GNU probe resolves the site root and parent, rejects parent or existing-target escape, and returns existence and advisory writability without opening file content, creating parents or writing a probe file. Existing in-root file symlinks are followed by this check; the separate atomic writer continues to reject target symlinks. Missing/dangling links, special files and unavailable traversal permissions retain their distinct outcomes.

An existing regular file uses the SSH user writability predicate; a missing target uses its existing parent predicate. If the parent does not exist, canWrite follows CreateDirectories, including the legacy optimistic result when an ancestor cannot be searched. A writable file in an unwritable parent may pass this check but fail replacement. canWrite is advisory, not a promise of ACL/traversal/atomic-replace success. SSH real and effective user identities are expected to coincide. The predicate includes applicable group and POSIX ACL permissions on the accepted Linux targets.

## Requirements and bounds

The target needs Bash and GNU-compatible base64, realpath and dirname; Python, sudo and an additional helper are not required for this probe. Utility search paths and locale are fixed and profile environment overrides are ignored. No public content stdin is supplied. The versioned internal envelope limits stdout to32768 bytes, stderr to2048 and resolved UTF-8 paths to12288 bytes. Malformed, inconsistent, oversized or unsupported path responses fail without returning file content. The existing ten-second deadline, cancellation and policy gates remain. Missing tools and unresolvable paths produce a safe error. Path checks use strings and do not guarantee confinement against concurrent adversarial replacement.

Public input/configuration/output keys remain unchanged. Existing JSON reasons, ReasonCode/guidance, helper capabilities and secret metadata are mapped by the application. The plain atomic writer and hash backend remain unchanged.

## Acceptance

Accepted on Debian12 with Bash/GNU:27 nonroot public/secret SSH cases,27 exact CPython3.12.16 JSON/error comparisons and five supplemental failure/transport cases passed. All file hashes, ownership, modes and ACLs remained unchanged, with zero read-only audit violations. Windows regression passed1215 tests with three Unix-only skips; build warnings/errors were zero. Helper routing was verified by regression, without a new privileged helper installation. Verify ordinary and secret callers, owner/group/ACL permissions, inaccessible parents, missing parents with creation on/off, symlinks, malformed transport and helper branches against the legacy reader on isolated nonroot SSH. Read-only auditing must show no file content reads, writes or outbound network. Supported utility/OS scope is limited to measured acceptance. See [ADR-0014](adr/ADR-0014-WEB-WRITE-CHECK.md).
