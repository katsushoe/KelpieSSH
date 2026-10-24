# Normal Host Reboot

## Authorization and Setup

This feature performs one normal reboot of a saved Debian/RHEL SSH profile. It requires `HostReboot.Enabled`, Maintenance or Expert mode, a non-root user and a pinned `Host.HostKeyFingerprintSha256`. It does not accept arbitrary shell commands or raw `SshRemoteOperation` inputs. Existing CLI-only Policies do not enable it.

The service owner chooses the host, account, expected kernel and health checks. The host administrator installs and owns the exact sudo rule, verifies the host key out of band and approves downtime. The client must show the preview and obtain explicit human administrator approval for each request. A token expresses caller confirmation; it does not prove who approved the action.

After reviewing the exact executable paths on the host, an administrator may use `visudo` to install this narrow rule (replace only the account name):

```sudoers
kelpieops ALL=(root) NOPASSWD: /usr/bin/systemctl reboot
```

The remote root account owns the sudoers file and executable, without SSH-account write access. Do not grant wildcard arguments, a shell, other systemctl commands or forced reboot. The implementation uses the explicitly configured sudo and systemctl paths with exactly `-n` and `reboot` arguments; the example paths produce `/usr/bin/sudo -n /usr/bin/systemctl reboot`; it cannot prompt for a sudo password. Read-only probes additionally require `/usr/bin/cat`, `/usr/bin/uname`, `/usr/bin/systemctl` and `/usr/bin/curl` when HTTP checks are configured. Missing prerequisites must be repaired explicitly by the host administrator.

The local administrator protects profiles, `dat/host-reboots/` and logs from untrusted writes, grants access to the intended CLI/MCP runtime identities and uses the same data directory for both clients. An enabled policy or its modification is a trust privilege expansion; after administrator review use the existing `kelpiemcp profile add` or `profile reload --approve-privilege-expansion` workflow. No settings, sudo rules or host keys are changed automatically.

## Profile Settings

Add the optional root `HostReboot` object. Omitted settings keep reboot disabled.

| Key | Required | Initial value | Meaning |
| :--- | :--- | :--- | :--- |
| `Paths` | no | empty object | Explicit remote paths described below; no executable defaults. |
| `Enabled` | no | `false` | Explicit administrator enablement. |
| `ExpectedKernel` | no | empty string | Exact post-reboot `uname -r`; empty accepts any valid kernel. |
| `Services` | no | empty | Up to 32 systemd unit names, each 1–128 characters. |
| `HttpUrls` | no | empty | Up to 16 absolute HTTP(S) URLs, each at most 2048 characters, without embedded credentials, fragments or control characters. |
| `TimeoutSeconds` | no | `120` | Total deadline from dispatch, 10–600 seconds. |
| `PollSeconds` | no | `3` | Inspection interval, 1–30 seconds. |

```json
{
  "HostReboot": {
    "Enabled": false,
    "Paths": {
      "Sudo": "/usr/bin/sudo",
      "Systemctl": "/usr/bin/systemctl",
      "Cat": "/usr/bin/cat",
      "Uname": "/usr/bin/uname",
      "Curl": "/usr/bin/curl",
      "BootId": "/proc/sys/kernel/random/boot_id",
      "NullDevice": "/dev/null"
    },
    "ExpectedKernel": "",
    "Services": ["nginx.service"],
    "HttpUrls": ["http://127.0.0.1/health"],
    "TimeoutSeconds": 120,
    "PollSeconds": 3
  }
}
```

`Paths.Sudo`, `Systemctl`, `Cat`, `Uname` and `BootId` are required when Enabled. `Curl` and `NullDevice` are additionally required when HTTP checks are configured. All path defaults are empty strings. Paths must be absolute POSIX paths without whitespace, shell syntax or dot/dot-dot segments, at most 512 characters, with final names `sudo`, `systemctl`, `cat`, `uname`, `boot_id`, `curl` and `null` respectively. Administrators verify ownership and select paths before enabling; the exact sudoers command must match the configured systemctl path. The sample paths are examples, never automatic fallbacks.

Expected kernels accept letters, numbers and `_.+~-`, at most 128 characters. Units start with a letter, number or underscore and otherwise accept letters, numbers and `_@.:-`; shell syntax and patterns are rejected. HTTP checks run from the SSH host, require 2xx, validate TLS and do not follow redirects. Configure public, credential-free health endpoints only; URLs are retained in the journal. All configured checks must pass. Empty arrays mean no service/HTTP health assurance.

## CLI and MCP Contract

| Step | CLI | MCP tool and arguments |
| :--- | :--- | :--- |
| Preview | `kelpie host-reboot preview <profile>` | `ssh_host_reboot_preview(profileName)` |
| Request | `kelpie host-reboot request <profile> <operationId> <confirmation>` | `ssh_host_reboot_request(profileName, operationId, confirmation)` |
| Inspect | `kelpie host-reboot inspect <profile> <operationId> [--wait]` | `ssh_host_reboot_inspect(profileName, operationId, wait=false)` |

Preview performs read-only SSH probes and stores a ten-minute preview. Show its target, fingerprint, user, current boot/kernel, `Checks`, `Impact` and `Confirmation` to the administrator. `NextKernel` is null because the boot loader's next selection is not inferred; `ExpectedKernel` is an administrator expectation, not a detected next kernel. `ssh_get_capabilities` reports profile availability for all three tools. Generic allowed-command execution cannot dispatch reboot.

Example workflow with placeholders (output JSON uses PascalCase; MCP input arguments use camelCase):

```text
kelpie host-reboot preview sample
kelpie host-reboot request sample <OperationId-from-preview> <Confirmation-from-preview>
kelpie host-reboot inspect sample <same-OperationId> --wait
```

```json
{"name":"ssh_host_reboot_inspect","arguments":{"profileName":"sample","operationId":"11111111111111111111111111111111","wait":true}}
```

All three operations return `HostRebootOperation`: `OperationId`, `TargetKey`, `Binding`, `ProfileName`, `Host`, `Port`, `UserName`, `HostKeyFingerprint`, `BeforeBootId`, `BeforeKernel`, `Checks`, `CreatedAt`, `DispatchedAt`, `State`, `Error`, `BootId`, `Kernel`, `RebootObserved`, `Reconnected`, `FailedServices`, `FailedHttpUrls`, `DispatchRejected`, `Confirmation`, `Impact`, `NextKernel`, `Deadline`. The hashes identify the target and approved configuration; they are not credentials. Boot/kernel and failure lists are null until observed. Times are UTC offsets. `Confirmation` must exactly equal `host_reboot:<ProfileName>:<OperationId>`. Store that operation ID before request; never replace it automatically when a response is lost.

CLI prints structured JSON, returns zero for an error-free result, and returns nonzero for an error, Failed or Unknown result. Invalid inputs, denied profiles, missing/corrupt journals and target mismatch throw without dispatch. MCP exposes those as tool errors. A busy target fails immediately; inspect the existing operation. Client cancellation interrupts inspection without clearing dispatch history.

## States, Errors and Recovery

| State | Meaning |
| :--- | :--- |
| `Preview` | No reboot has been requested. |
| `Dispatching` | Durable intent exists; a crashed process may have sent reboot. |
| `Accepted` | The normal reboot command returned zero; completion is unverified. |
| `Unknown` | Dispatch or reconnect result is uncertain. Never automatically resend. |
| `Reconnected` | SSH responded; boot change or health checks are incomplete. |
| `RebootObserved` | A changed boot ID was durably recorded before health checks. |
| `Verified` | Changed boot ID, expected kernel and all configured checks passed. |
| `Failed` | A definite rejection or verification failure occurred. |

| Error | Behavior |
| :--- | :--- |
| `ApprovalRequired`, `PreviewExpired`, `PreviewStale`, `ProfileChanged` | No dispatch; obtain a valid new preview and approval after resolving the reason. |
| `OperationInProgress` | Another unresolved operation blocks dispatch for the same pinned identity. |
| `PermissionDenied` | sudo rejected the normal reboot; no automatic privilege change. |
| `RequestRejected` | The normal reboot command returned a different nonzero result. |
| `HostKeyMismatch`, `AuthenticationFailed` | Stop on the security failure; administrator investigation is required. |
| `DispatchUnknown` | Lost response, timeout or cancellation after intent was stored; inspect the same ID. |
| `ReconnectPending`, `RebootNotObserved` | Continue observations only within the original deadline. |
| `ReconnectTimeout` | Deadline expired; Unknown unless a changed boot was already observed. |
| `BootChangedDuringVerification` | Boot identity changed again during health checks; verification is incomplete. |
| `KernelMismatch` | Changed boot but running kernel differs from the approved expectation. |
| `ServiceStopped`, `HttpFailed` | Health incomplete; waiting inspection retries until deadline and then fails. |

Inspect never sends reboot and never extends `Deadline`. Keep the approved mode and HostReboot policy unchanged until inspection completes; a mismatch returns ProfileChanged. It requires the same configured endpoint, user, profile and pinned key. Reusing an existing request ID returns its stored dispatch result without resending. Unresolved dispatches block a new operation, including across process restarts. After deadline expiry the protected record remains for investigation; there is no automatic reset, forced reboot or privilege fallback. An administrator must establish the actual boot state out of band before reconciling an unresolved journal. The MCP server fixes its reboot journal directory at startup, including any explicit `--dat-dir` override. CLI and MCP must use the same data directory to share operation history. Separate data directories have separate replay protection; do not dispatch through independent installations.

## Validation

Automated fixtures exercise success, authorization and configuration failures, stale previews, duplicates, persistence and process interruption, response loss, reconnect deadline, host-key/authentication failures, kernel mismatch, service stop and HTTP failure. Real-host reboot requires a separately approved maintenance window. See [ADR-0005](docs/adr/ADR-0005-HOST-REBOOT-VERIFICATION.md) and the [systemctl reference](https://www.freedesktop.org/software/systemd/man/latest/systemctl.html) for command behavior.
