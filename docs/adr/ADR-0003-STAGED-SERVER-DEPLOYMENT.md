# ADR-0003: Staged Server Deployment Contract

## Status

Accepted on 2026-08-30; amended on 2026-09-07 for Lifecycle Assertion v2 and durable stage recovery, and on 2026-10-13 for safe stage diagnostic metadata and logging failure isolation.

## Context

Moyai delegates server deployment through the Streamable HTTP MCP endpoint and retains only a KelpieSSH target name or caller correlation ID. It must not retain SSH passwords, private keys, passphrases, or raw deployment content. A single opaque deploy call cannot distinguish upload, activation, verification, rollback, and cleanup failures.

## Decision

KelpieMCPServer exposes provider bootstrap, target status, six deployment stages, and `deploy_status`. A caller-supplied `deploymentId` is the durable correlation and idempotency key. A private SQLite journal stores the administrator-resolved Project UUID, immutable Target ID, profile, argument hash, deployment and transfer snapshots, and completed stage result. Reservation commits before dispatch; an interrupted stage remains result-unknown and cannot be automatically repeated. The implementation reuses the existing managed web policy, bounded archive transfer, atomic placement, metadata-only SHA-256 verification, commit, and rollback mechanisms.

The contract is MCP-specific because it is a provider protocol implemented for a service orchestrator. Existing `web_bulk_transfer_*` tools and Kelpie CLI-managed web operations remain the human/operator alternative. A second CLI spelling for every provider stage would duplicate protocol orchestration state and is intentionally not added.

## Alternatives

- A single deploy call was rejected because it cannot expose stage-specific failures or safe retry boundaries.
- Caller-supplied SSH credentials were rejected because they would move the credential boundary into Moyai.
- A separate unrestricted SFTP implementation was rejected because it would bypass the managed web policy and existing rollback transaction.
- Process-only deployment records were replaced because the v2 contract requires state resolution across server restarts.

## Consequences

Completed snapshots and results survive restart. Successful stages restore their transfer context for subsequent stages; unresolved reservations require operator investigation. Callers must query `deploy_status` after a transport failure. They must not create a new ID to bypass an unknown result. One deployment contains one artifact. Each stage is accepted once per deployment; identical normalized arguments return its cached result, and different arguments conflict. Property ordering is ignored; omitted optional arguments and explicit values are otherwise distinct.

## Security Conditions

Completed authenticated stages include an additive `diagnostic` object containing deployment ID, tool, validated request operation ID, UTC start/end times, and an allowlisted stage error code (null for success). Unknown provider codes become `unclassified-stage-failure`; exception messages, raw output, paths, credentials and assertion bytes are never copied into this object. The generic `deployment_stage_failed` contract remains unchanged. The complete stage response, including diagnostic metadata, is cached in the existing journal; replay returns the original operation ID and timestamps. Unexpected dispatch exceptions return safe diagnostic metadata with `deployment_result_unknown`, while the reservation remains pending rather than being marked complete. Authentication failures before dispatch do not expose stage diagnostics. Old cached responses without this optional object remain valid.

Logging-provider failures use a fixed, secret-free stderr fallback and cannot change authorization or journal outcomes. Raw provider diagnostics were rejected because they may disclose credentials or filesystem content. The additive metadata is documented in the MCP command reference and verified for secret exclusion, cached restart recovery, unknown reservations and failing log sinks.

- Target credentials remain in KelpieSSH profiles and secret stores.
- Responses and ordinary logs exclude credentials, artifact bytes, Base64 content, and raw provider output.
- Artifact SHA-256 is mandatory, and activation revalidates the local file before transfer.
- Destination paths remain constrained by the selected profile's managed web site policy.
- Rollback data is removed only after verification and cleanup.
- Lifecycle tools require the shared `Moyai.ProviderAuthentication` validator, ES256, protocol `2`, audience/provider `kelpiessh`, resource kind `kelpie_target`, an administrator-bound Project and Target, a single tool scope, and a request operation ID. `server` is never an accepted audience.
- Bootstrap does not require an assertion. Authorization is read only for the actual tool request. A lifecycle assertion cannot invoke other SSH or administrator tools. Existing non-Lifecycle tool/profile authorization remains a separate boundary.
- Mapping registration is offline administrator configuration protected by the existing configuration trust mechanism; assertions cannot register or change targets. Protect configuration, Trust Bundle and private databases from other writers.
- JWT parsing, key validation and persistent replay prevention come from the shared NuGet package. There is no legacy MCP Service Token verifier and therefore no dual-accept migration.

## Operational Conditions

The public endpoint remains loopback-only. Integration tests use a disposable SSH target restricted to `/tmp/kelpie-deploy-test`; the sample profile is `config_samples/servers/moyai-deploy-loopback.json`. Test credentials and target contents must be disposable and must never be committed.

Configure `Lifecycle` in the trusted MCP configuration before using deployment tools. Without that section the tools fail closed. Trust revocation is read for every request. Pending and completed journal records have no automatic expiry; database retention is an administrator responsibility. Database loss or corruption must not be treated as authorization to replay an operation.

## Implementation, Tests, and Documentation

The MCP implementation lives in `KelpieMCPServer`, while SSH policy and atomic file operations remain in the existing application provider. Automated tests cover idempotency, hash validation, successful stages, rollback success, and rollback failure. `MCP_COMMANDS.md` and its Japanese counterpart define the external contract; MT-143 records loopback discovery and state-machine verification.
