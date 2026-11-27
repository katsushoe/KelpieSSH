# ADR-0006: Dedicated nginx helper transactions

## Status

Accepted for implementation. Deployment requires the documented helper policy and successful regression/isolated Linux acceptance. This does not authorize changes to a production server's sudoers or nginx configuration.

## Context

Six internal nginx operations used privileged shell/Python templates. Configuration edits and disabled default-site links need coordinated recovery. A lost SSH response cannot establish whether an update happened. Existing result booleans cannot distinguish an unknown outcome from no effects.

## Decision

Replace those routes with fixed nginx helper protocol 1 operations: plan, check, apply, status, commit and rollback. Preserve configuration read/log providers and the separate `nginx -t` command. Application providers own intent, profile/channel authorization, confirmation and validation orchestration; the standalone helper independently enforces caller UID, server policy, canonical paths and effect preconditions.

Persist a root-owned transaction journal, pre-state hashes and inode identities before applying changes. Use descriptor-relative operations in trusted directories and no-follow file opens on Linux x64. Recover file and default-site-link effects together; never execute an arbitrary interpreter or service command through the helper. Serialize helper state operations with a nonblocking process-shared lock. Bind content and expected state to an immutable intent digest.

Keep `.kelpiebakup` as the physical adjacent backup. New records express original absence explicitly instead of interpreting config bytes as a sentinel. Refuse unrelated/legacy backups. Do not automatically migrate old backups or old disabled-site markers. Preserve those artifacts for explicit operator reconciliation.

Add `OutcomeKnown` to configuration write, backup-action and nginx PHP-enable results. Existing fields and constructors remain available. A false value means other effect fields are incomplete and callers must reconcile before retrying. After an ambiguous mutation, make one bounded status query; never repeat the mutation automatically. A verified terminal commit/rollback may resolve a lost reply successfully.

## Alternatives

- Keep restricted Python templates: rejected because the privilege boundary still includes a general interpreter.
- Send arbitrary commands to the existing web helper: rejected because web site authority must not implicitly authorize nginx configuration or shell execution.
- Report timeout as an ordinary failure with zero changes: rejected because it loses recoverability and can cause a duplicate mutation.

## Security and operation

The helper policy is independent of profile discovery and WebPublicSites. Its callers and paths are empty by default. Policy, binary, state and path ancestors must be root-owned and not writable by group/others; state is private. Caller UID must come from a privileged invocation with a nonzero `SUDO_UID`, not a request field. This intentionally grants only explicitly configured callers nginx configuration authority.

Initially support Linux x64 only; other platforms fail before filesystem operations. Bounded input/output, strict JSON and fixed errors prevent unbounded parsing and raw diagnostic leakage. Capability output is metadata, not authorization. Completed records remain for deduplication; the state store is limited to 256 records and requires operator retention/cleanup management. No automatic backup deletion or policy expansion occurs to make room.

No automatic reload/restart follows edits. Validation is a separate allowed operation; commit is issued by the orchestrator after validation. If the caller has direct OS access to the granted helper operations, it already holds the explicitly configured nginx administration authority; the helper does not authenticate an application's confirmation UI.

## Implementation, tests and documentation

Implement the helper protocol, Linux filesystem primitive, provider adapter and safe recovery mapping together. Test actual effects, competing edits, same-content inode replacement, policy changes, symlinks, restart reconciliation, malformed replies and byte limits. Mock tests do not replace disposable Linux/SSH/nginx acceptance. Document installation, policy, protocol, `OutcomeKnown`, old-backup handling and update compatibility in both languages, and update command scenarios/results before delivery.
