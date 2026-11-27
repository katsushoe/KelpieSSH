# Nginx configuration helper

[English](NGINX_HELPER.md) | [日本語](ja/NGINX_HELPER.ja.md)

## Scope and prerequisites

The nginx configuration mutation provider uses `kelpie-web-permission-helper` 0.2.4.0 or later with nginx protocol 1. The initial implementation supports Linux x64. Six privileged Python routes are replaced; existing configuration/log reads still use their unprivileged Python provider. The migration does not remove that read dependency or enable PHP/Node alternatives.

Install the helper through the administrator-controlled helper update procedure. The executable and its ancestors must be root-owned and not writable by group/others. Configure `/etc/kelpie/nginx-config-helper-policy.json` separately from SSH profiles and WebPublicSites. The [sample](../config_samples/nginx-config-helper-policy.json) denies every caller and target until configured. No policy, sudoers entry or runtime is installed automatically when a command fails.

Create the configured state directory with root ownership and mode 0700 before enabling the helper. Grant only the exact reviewed nginx helper invocations and the separately reviewed `nginx -t` validation command to the intended SSH account. Do not grant a general interpreter or shell. The helper requires effective UID 0 and a nonzero `SUDO_UID` listed in its policy. These privileges grant that account explicit nginx configuration administration authority; the helper cannot authenticate an application's confirmation UI.

## Policy

The policy is strict JSON; unknown fields and duplicate keys are rejected. Its file and all ancestors must be root-owned and not writable by group/others. Symlinked policy/state paths are rejected.

| Key | Required | Initial value | Meaning |
| :--- | :--- | :--- | :--- |
| `protocol` | yes | `1` in sample | Required protocol version. |
| `revision` | yes | `configure-before-use` in sample | Administrator revision; policy bytes are bound into every transaction. |
| `stateDirectory` | yes | `/var/lib/kelpie/nginx-transactions` in sample | Private, root-owned persistent journal directory. |
| `callerUids` | yes | empty | Nonzero invoking user UIDs permitted through sudo. |
| `targets` | yes | empty | Exact canonical config paths. |
| `directories` | yes | empty | Exact parent directories whose immediate files may be edited; not recursive. |
| `sitesEnabledDirectory` | no | empty string | Optional root-owned directory for default-site symlinks. |
| `siteTargetDirectories` | yes | empty | Exact parent directories permitted for those symlinks' resolved targets. |

For a config-file symlink, both its logical path and resolved regular target must be authorized. Trusted directory traversal and descriptor-relative no-follow operations preserve the logical symlink. Unapproved targets, writable ancestors, nonregular files and hard-linked data files are refused. Default-site symlinks are a separate planned effect: only matching port-80 `default_server` links in the configured directory are disabled. At most 64 directory entries are inspected; arbitrary recursive discovery is not performed.

## Helper protocol

`nginx-capabilities` returns protocol/verb metadata without sudo. It does not grant authority or prove that a target policy exists. Operational invocations use the fixed helper path, one verb and argument `1`; one JSON request is supplied on stdin.

| Verb | Behavior |
| :--- | :--- |
| `nginx-plan 1` | Capture exact preconditions and persist a private plan; do not change nginx files or links. |
| `nginx-check 1` | Check policy and file/backup readiness; do not create a target or backup. |
| `nginx-apply 1` | Apply the confirmed digest and expected state with durable effect records. |
| `nginx-status 1` | Read/reconcile a transaction, including after a restarted host process. |
| `nginx-commit 1` | Retire that transaction's backups after orchestrator validation. |
| `nginx-rollback 1` | Restore only the transaction's unchanged post-state effects. |

Requests use camelCase fields: `protocol`, `requestId` (32 hexadecimal GUID characters), `target`, optional `contentBase64`, `expectedHash`, `expectedExists`, `disableDefaultSites`, `transactionId` and `intentDigest`. Absent transaction/digest fields are empty; explicit null for them is invalid. Base64 is encoding, not encryption or authorization. Apply requires the plan's transaction ID, digest and unchanged content. Status and backup recovery can resolve one unambiguous active transaction by target and invoking UID after a host restart.

Success/error replies contain `protocol`, request/transaction IDs, `ok`, `state`, file/link change counts, `bytesWritten`, intent/pre-state/current hashes, `recoverable` and a fixed error category. Errors return exit 1 with JSON on stdout; raw exceptions/configuration bodies are not returned. Unsupported/old helpers fail safely without interpreter fallback.

Maximum request size is 256 KiB, decoded config content 65536 bytes, response 64 KiB and accepted diagnostic stderr 4 KiB. Config must be valid UTF-8 without NUL. JSON parsing is bounded to 16 levels. Host operation timeout is 30 seconds; an ambiguous mutation gets one separate status query bounded by 10 seconds. The state lock is nonblocking and process-shared. Plans expire after five minutes. A store with 256 journal files refuses new plans; an administrator must manage retention without removing active recovery records.

## Recovery and public results

The adjacent backup suffix remains `.kelpiebakup`. Explicit metadata records whether the target originally existed; config bytes never act as a deletion sentinel. Existing legacy/unowned backups are refused rather than imported or overwritten. Reconcile old `.kelpiebakup` and `.kelpie-disabled-sites` records before migrating a target; do not delete an unknown backup to force an operation through.

Helper journals record hashes and inode identities before installing files. Rollback refuses competing edits, same-content replacements and recreated links. Multiple file/link effects are not globally atomic: interrupted operations retain recovery records. A status response can identify conflicts instead of claiming restoration. No automatic mutation retry, service reload or restart occurs.

`service_config_file_write`, `service_config_file_rollback`, `service_config_file_commit` and `ssh_service_config_nginx_enable_php` retain their existing input/confirmation rules and result fields, with additive `OutcomeKnown`. False means the effect fields are incomplete and the operation must be reconciled before retrying. Errors are not evidence of zero effects. A verified lost commit/rollback reply may be resolved by status without repeating the mutation.

PHP enablement groups its file and link changes, validates through the existing fixed `nginx -t`, commits after success and restores both after validation failure. A true no-op reports no changes, no validation, no commit and zero bytes. Commit cleanup failure keeps recovery data and is distinct from invalid nginx configuration.

## Upgrade and verification

Keep old backup artifacts until their ownership and intended recovery are established. Upgrade helper and provider together, configure the independent policy and verify the exact allowed sudo surface. Do not widen policy because a helper is missing or a path is denied. Existing read-only paths remain available subject to their previous dependencies and authorization.

Before deployment, run protocol/provider unit tests plus disposable Linux/SSH/nginx acceptance covering actual write/rollback/commit effects, new files, symlinks, competing edits, policy changes, byte limits, lost replies and process-restart reconciliation. Use synthetic data. The architecture and alternatives are recorded in [ADR-0006](adr/ADR-0006-NGINX-HELPER-TRANSACTIONS.md).
