# ADR-0005: Single Dispatch Host Reboot and Bounded Verification

## Status

Accepted on 2026-10-22.

## Context

A reboot can close SSH before a response arrives. Retrying that request may reboot a recovered server a second time. Existing generic maintenance commands do not supply a durable operation identity or boot verification.

## Decision

Expose dedicated preview, request and inspection operations for configured Debian/RHEL profiles. An administrator explicitly enables `HostReboot`, approves its trust baseline, provisions one exact sudo command and approves each target and outage. Maintenance/Expert mode, a non-root SSH user and a pinned host key are required. CLI and MCP share the application service, SSH runner and local journal.

Persist an opaque preview ID, target, boot ID, kernel and checks. Expire previews after ten minutes. Bind approval to the operation and profile, recheck the boot identity before dispatch, and persist dispatch intent before sending the normal reboot command once. A lost response, cancellation or crashed dispatch remains uncertain and cannot be replayed. Serialize operations across processes by pinned host identity; unresolved operations block new requests for that identity.

Inspection never sends reboot. Within the original deadline it checks the same endpoint and host key, changed boot ID, optional expected kernel, configured systemd units and HTTP endpoints. It distinguishes accepted, unknown, reconnected, reboot observed, verified and failed observations. A disconnect is not proof of reboot. Boot observation is persisted before health checks. HTTP checks require 2xx and do not follow redirects. Empty health lists verify only boot identity and the optional kernel.

## Alternatives and Impact

- Generic shell reboot was rejected because it cannot enforce the dedicated approval and operation identity contract.
- In-memory request tracking was rejected because a process restart could permit a second reboot.
- Automatic command-path guessing was rejected because deployment paths and sudo ownership require administrator review. `HostReboot.Paths` explicitly configures absolute remote executable, boot-ID and null-device paths; command arguments remain fixed.

The change affects application policy/snapshots, the infrastructure SSH failure mapping and durable journal, CLI command routing, MCP tools/capabilities and both language references. CLI execution follows the existing KelpieSSH direct SSH runner architecture and shares the journal with MCP; no new service transport is introduced.

## Trust and Recovery

An enabled reboot policy is part of the profile authorization snapshot. Enabling or changing it requires privilege-expansion approval; disabling is a reduction. Legacy profiles remain disabled. The confirmation token records caller confirmation, not an independently authenticated human identity: clients must obtain explicit human administrator approval before submitting it. Local administrators protect profiles, journal and logs from untrusted modification and give only the intended CLI/MCP identities access.

Deadline expiry stays unknown if reboot was not observed. Repeated inspection does not extend the deadline. Operators investigate such operations out of band and reconcile the protected journal only after establishing the actual boot state. No API silently clears unknown operations, widens privileges, disables host-key checks or falls back to forced reboot. Separate installations with different data directories do not share duplicate protection.

## Verification

Automated fixtures cover policy/configuration rejection, stale and expired previews, exact approval, persistence/restart, duplicate/concurrent requests, response loss, host-key and authentication failures, deadline/cancellation, kernel/service/HTTP failure and CLI/MCP parity. Production reboot remains a separately approved operational action.
