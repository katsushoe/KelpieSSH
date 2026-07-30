# ADR-0002: Privileged Helper Update

## Status

Accepted

## Context

The root-owned web permission helper must be upgradeable from a Windows management terminal. The installed helper cannot safely replace itself, and neither an MCP callable tool nor an arbitrary privileged shell may modify the privileged boundary.

## Decision

`kelpiemcp helper update <profile> <local-artifact>` is a human-only command. It uploads the local artifact through internal SFTP to the fixed staging path `/tmp/kelpie-web-permission-helper.update`. After local and remote SHA-256 verification and explicit human confirmation, an internal wrapper sends one complete fixed script through KelpieSSH's existing encoded root-command channel. The script accepts only the validated SHA-256 argument.

The wrapper creates a fixed backup, copies the staged artifact to a root-owned temporary file in the target directory, verifies its SHA-256 and version output, sets `root:root` and mode `0755`, and atomically renames it to `/usr/local/libexec/kelpie/kelpie-web-permission-helper`. A failed transaction attempts rollback from the backup. Completion is sent to the system audit log.

## Security Requirements

- Direct root SSH login and an interactive root shell are prohibited.
- The workflow is not an MCP callable tool, is absent from `ssh_run_allowed_command`, and is not exposed through the MCP API or control pipe.
- Only the profile and local artifact path are external inputs. Remote staging, backup, temporary, target, and audit identifiers are fixed internally.
- The local artifact must be a non-reparse-point regular file no larger than 64 MiB.
- SFTP uses the selected profile and its host-key verification.
- SHA-256 is computed locally, checked after upload, and checked again on the root-owned temporary file immediately before replacement.
- The current version and current/proposed hashes are shown before a cryptographically random confirmation code is requested.
- The installed artifact must identify itself as `kelpie-web-permission-helper`.
- Backup ownership and mode are normalized to `root:root` and `0755`.
- Replacement is an atomic same-directory rename.
- Confirmed and completed events are sent to the system audit log. Any failed privileged step attempts rollback and returns a user-facing error without secrets.
- The helper update script is not registered as an MCP tool or allowed-command provider and cannot accept an external command, path, or shell fragment.

Detached release signatures are not yet part of the repository release pipeline. SHA-256 protects transfer integrity but does not independently establish publisher identity. Adding signed release manifests and a pinned verification key requires a separate release-signing decision before signature enforcement can be claimed.

## Rejected Alternatives

- MCP callable update: rejected because an AI process must not modify its own privileged helper.
- Helper self-update: rejected because the helper must not expand or replace its own privileged boundary.
- Root SSH login: rejected by the SSH security policy.
- Arbitrary SCP destination plus root shell: rejected because it permits unrelated privileged file replacement.
- Non-atomic `cp` directly over the target: rejected because interruption can leave a partial executable.

## Operational Requirements

The existing KelpieSSH internal root-command permission must already work for the selected profile. No helper-update-specific sudoers entry is added. The fixed backup supports automatic rollback of a failed update; administrators must retain an out-of-band recovery path for a damaged privilege configuration or filesystem.

## Implementation, Test, and Documentation Mapping

| Requirement | Implementation | Acceptance coverage | User documentation |
| :--- | :--- | :--- | :--- |
| Human-only entry point | `HelperUpdateCommand` | interactive and mismatch tests | `COMMANDS.md` |
| Internal SFTP staging | `SshNetFileUploader` | fake workflow and live acceptance | `COMMANDS.md` |
| Fixed privileged command wrapper | `SshHelperUpdateRemote` | external-surface absence and command tests | `SECURITY.md` |
| Hash, backup, atomic replacement, rollback | `SshHelperUpdateRemote` | hash mismatch and failure cases | `COMMANDS.md` |
| No MCP exposure | no MCP registration or command-provider entry | callable and allowed-command absence | `MCP_COMMANDS.md` |

## Consequences

The helper can be updated without using the old helper as an updater and without a separate sudoers bootstrap. Update transactions reuse the existing internal privileged channel and remain unavailable to MCP callable surfaces.
