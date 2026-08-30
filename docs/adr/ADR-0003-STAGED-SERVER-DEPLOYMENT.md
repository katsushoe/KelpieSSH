# ADR-0003: Staged Server Deployment Contract

## Status

Accepted on 2026-08-30 for the Moyai provider integration.

## Context

Moyai delegates server deployment through the Streamable HTTP MCP endpoint and retains only a KelpieSSH target name or caller correlation ID. It must not retain SSH passwords, private keys, passphrases, or raw deployment content. A single opaque deploy call cannot distinguish upload, activation, verification, rollback, and cleanup failures.

## Decision

KelpieMCPServer exposes `provider_version`, `provider_capabilities`, `target_status`, and six staged deployment tools. A caller-supplied `deploymentId` is both the correlation and idempotency key. Deployment state is process-local and bounded. The implementation reuses the existing managed web policy, bounded archive transfer, atomic placement, metadata-only SHA-256 verification, commit, and rollback mechanisms.

The contract is MCP-specific because it is a provider protocol implemented for a service orchestrator. Existing `web_bulk_transfer_*` tools and Kelpie CLI-managed web operations remain the human/operator alternative. A second CLI spelling for every provider stage would duplicate protocol orchestration state and is intentionally not added.

## Alternatives

- A single deploy call was rejected because it cannot expose stage-specific failures or safe retry boundaries.
- Caller-supplied SSH credentials were rejected because they would move the credential boundary into Moyai.
- A separate unrestricted SFTP implementation was rejected because it would bypass the managed web policy and existing rollback transaction.
- Persistent deployment records were deferred because the v1 contract requires bounded orchestration, not recovery across an MCP server restart.

## Consequences

Deployment IDs cannot resume after the MCP server process restarts. Callers must query capabilities and target status, use unchanged inputs for retries, and create a new deployment after process loss. One deployment currently contains one artifact; multi-file deployment remains available through `web_bulk_transfer_*`.

## Security Conditions

- Target credentials remain in KelpieSSH profiles and secret stores.
- Responses and ordinary logs exclude credentials, artifact bytes, Base64 content, and raw provider output.
- Artifact SHA-256 is mandatory, and activation revalidates the local file before transfer.
- Destination paths remain constrained by the selected profile's managed web site policy.
- Rollback data is removed only after verification and cleanup.

## Operational Conditions

The public endpoint remains loopback-only. Integration tests use a disposable SSH target restricted to `/tmp/kelpie-deploy-test`; the sample profile is `config_samples/servers/moyai-deploy-loopback.json`. Test credentials and target contents must be disposable and must never be committed.

## Implementation, Tests, and Documentation

The MCP implementation lives in `KelpieMCPServer`, while SSH policy and atomic file operations remain in the existing application provider. Automated tests cover idempotency, hash validation, successful stages, rollback success, and rollback failure. `MCP_COMMANDS.md` and its Japanese counterpart define the external contract; MT-143 records loopback discovery and state-machine verification.
