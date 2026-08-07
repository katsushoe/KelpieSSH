# ADR-0001: Remote Web Policy Management

## Status

Accepted

## Context

KelpieSSH is administered from a Windows management terminal while the managed web permission policy belongs to a VPS selected by an SSH profile. The policy file is `/etc/kelpie/web-permission-helper-policy.json` on that VPS. Treating `kelpiemcp web-policy` as a Unix-local command caused Windows to inspect a meaningless local `/etc/kelpie` path and did not satisfy the product workflow.

This ADR is the source of truth for the remote web policy management boundary. Command references and profile guides link here instead of duplicating the design rationale.

## Decision

`kelpiemcp web-policy` runs on the Windows management terminal and requires an explicit SSH profile. It connects directly to the selected VPS and invokes only `/usr/local/libexec/kelpie/kelpie-web-permission-helper` through non-interactive `sudo`.

```text
kelpiemcp web-policy list <profile> [<site-root>]
kelpiemcp web-policy add <profile> <site-root> <file-path> <Update|Create>
kelpiemcp web-policy remove <profile> <site-root> <file-path>
kelpiemcp web-policy apply <profile> <manifest.json>
kelpiemcp web-policy rollback <profile>
```

Windows-local `/etc/kelpie` paths are never read or modified. The SSH profile selects the only VPS whose policy may be accessed.

## Security Requirements

- Direct root SSH login remains prohibited.
- The Windows command may invoke only the fixed helper path with validated action names and Base64-encoded validated arguments.
- The MCP server and MCP callable tools cannot invoke this workflow, approve it, or elevate their own privileges.
- `add`, `remove`, `apply`, and `rollback` require a human terminal on both standard input and standard output.
- A complete current/proposed JSON comparison is displayed before mutation.
- The operator must type an exact cryptographically random confirmation code. There is no bypass option.
- The helper rechecks the current SHA-256 hash after confirmation and refuses a changed policy.
- Policy JSON must match the strict `Sites.<site-root>.AllowedFiles.<file-path>` schema. Only `Update` and `Create` are accepted.
- Existing entries are retained. Duplicate additions and removal of missing entries are rejected.
- Bulk manifests are finite, strict-schema, add-only inputs. All changes are validated before one manifest-bound confirmation and one atomic replacement; duplicate paths and stale local or remote state fail closed.
- Every mutation creates a timestamped backup before replacement.
- Backup and replacement files preserve the policy UID, GID, and mode.
- Replacement uses a temporary file in the policy directory followed by an atomic rename.
- Rollback restores only the latest validated managed backup and creates a backup of the state being replaced.
- Audit events are written before and after replacement to the root-owned mode `0600` JSON Lines audit log.
- Audit output contains operation metadata and paths, never credentials, keys, secret values, or policy file contents.
- Missing helper sudo permission, unsafe ownership/mode, symlinks, malformed JSON, schema mismatch, stale previews, confirmation mismatch, and non-interactive mutation all fail closed.

## Execution and State Boundaries

| Boundary | Decision |
| :--- | :--- |
| Execution terminal | Human-operated Windows terminal running `kelpiemcp`. |
| Target selection | Explicit SSH profile argument. |
| Changed state | Policy, backup, and audit files on the selected VPS only. |
| Privileged boundary | Installed `kelpie-web-permission-helper` invoked by narrowly scoped sudoers permission. |
| MCP boundary | No MCP tool, MCP confirmation, or MCP self-elevation path. |

## Rejected Alternatives

- Local `/etc/kelpie`: rejected because the authoritative policy is on the selected VPS.
- Root-side `kelpiemcp`: rejected because it expands the privileged deployment surface.
- MCP callable mutation: rejected because an AI process must not grant itself privileged web-write capability.
- Arbitrary shell, editor, or JSON replacement: rejected because it bypasses validation and least privilege.
- Client-only merge and overwrite: rejected because privileged revalidation, backup, audit, and atomic replacement belong at the server boundary.

## Implementation, Test, and Documentation Mapping

| ADR requirement | Implementation | Acceptance coverage | User documentation |
| :--- | :--- | :--- | :--- |
| Explicit remote profile | `RemoteWebPolicyCommand` | `CT-050` list and profile cases | `COMMANDS.md`, `PROFILE_GUIDE.md` |
| Human comparison and confirmation | `RemoteWebPolicyCommand` | non-interactive and mismatch cases | `COMMANDS.md` |
| Schema and entry preservation | `ManagedWebPolicyCommand` | existing, duplicate, add, remove cases | `CONFIG.md` |
| Manifest-bound atomic bulk apply | `RemoteWebPolicyCommand`, `ManagedWebPolicyCommand` | eight-entry, duplicate, invalid-path, stale-policy cases | `COMMANDS.md` |
| Backup, atomic replacement, metadata, rollback | `ManagedWebPolicyCommand` | add/remove/rollback cases | `COMMANDS.md` |
| Audit without secrets | `ManagedWebPolicyCommand` | audit and secret-absence cases | `SECURITY.md` |
| MCP self-elevation prohibited | No MCP registration | callable-tool absence check | `MCP_COMMANDS.md` |
| No Windows-local `/etc/kelpie` | Remote-only CLI | Windows integration test | `COMMANDS.md` |

## Consequences

The VPS helper and Windows `kelpiemcp` must be updated together. Existing helper versions without `policy` actions fail safely. Operators must configure narrowly scoped passwordless sudo for the helper executable; broad shell or editor sudo permission is not acceptable.

The helper update trust boundary is defined separately by [ADR-0002](ADR-0002-PRIVILEGED-HELPER-UPDATE.md).
