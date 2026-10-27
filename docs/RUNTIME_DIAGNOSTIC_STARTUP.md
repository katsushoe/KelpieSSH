# Optional runtime startup infrastructure

[Japanese](ja/RUNTIME_DIAGNOSTIC_STARTUP.ja.md). The [accepted design](adr/ADR-0007-BOUNDED-RUNTIME-STARTUP.md) defines the trust boundary and alternatives.

## Scope

This D1 increment supplies library infrastructure for future PHP/Node diagnostics. `RuntimeDiagnosticStartup.CreateProbeDefinition("php" | "node")` creates a fixed read-only definition, with no caller code, executable path or raw arguments. Its definitions are intentionally absent from the default command catalog. No new CLI command, MCP callable tool or user profile key is available from this increment; adapters, registration and structured runtime feature results are later work.

## Startup contract

Fixed candidates are `/usr/bin/php`, `/usr/local/bin/php`, `/usr/bin/node` and `/usr/local/bin/node`. The launcher checks ownership, write/set-id bits, directories and symlink components using trusted GNU stat/readlink. Root-owned system links are permitted only through trusted searchable parents. At most 16 symlinks and 128 components are expanded; link targets must be bounded ASCII system paths. Rewalk and compare resolved-path metadata immediately before execution. A present untrusted candidate fails rather than switching candidates. Only a missing fixed candidate permits checking the next candidate in the same family.

The launcher requires trusted `/bin/sh`, `/usr/bin/env`, `/usr/bin/base64`, `/usr/bin/stat`, `/usr/bin/readlink` and `/usr/bin/timeout`, and trusted fixed candidate parents. Missing bootstrap support fails without package installation or sudo. It starts from `/` with a cleared environment containing only fixed PATH, HOME and locale. PHP uses `-n -r` with CLI-SAPI validation; Node uses `--input-type=commonjs --no-addons --eval`. Both execute only fixed readiness code and print `KELPIE_RUNTIME_READY_V1` on success. This is startup readiness, not application health, configured PHP extensions, FPM state or the proposed runtime.info result.

GNU timeout stops the remote runtime group after five seconds. The outer request has a 15-second deadline. A remote timeout can have exit 137 instead of the transport's TimedOut flag; later adapters must interpret the fixed startup contract. Cancellation/connection loss does not prove that an arbitrary remote command stopped; the runtime phase has its independent remote deadline. No retry, cross-runtime fallback, autoload/import, package manager or service change occurs.

## Opt-in bounded SSH contract

| Library property | Behavior |
| :--- | :--- |
| `AllowedCommandDefinition.ExecutionLimits` | Optional limits copied by SshCommandService after normal command/policy validation. Null keeps the previous transport. |
| `SshCommandRequest.ExecutionLimits` | Selects direct bounded reception; text input only. |
| `SshCommandExecutionLimits` | stdout 1..1048576 bytes; stderr 1..65536; input 0..65536. Startup probes use 16384/2048/4096. |
| `IgnoreEnvironmentOverrides` | The fixed definition opts out of stored/profile environment exports; the launcher also clears inherited runtime variables. |

The bounded path uses non-PTY shell support and a random exit frame, without whole-output SshCommand buffering. Allocation is fixed to the output allowance plus 96 framing bytes and the separate stderr allowance. On overflow it returns no partial payload and throws a sanitized `SshCommandOutputLimitException`; invalid UTF-8 or incomplete framing also fails. The mode and channel rules are unchanged. Application callers must continue resolving current trusted profiles and using the normal policy pipeline.

No SSH profile, trust store, environment store, helper policy or sudoers change is required. This feature never persists a detected runtime into a profile. Unsupported servers fail without reverting to an unbounded path. Existing commands do not automatically acquire new byte limits.

## Verification and later stages

Unit tests validate stream bounds, UTF-8 byte accounting, completion frames, literal stdin and definition forwarding. Actual isolated SSH acceptance checks real PHP/Node startup, hostile inherited hooks, output floods, cancellation/deadlines, symlink trust and missing-family behavior. These checks are narrower than full diagnostic adapter acceptance. Later stages must add strict typed results, capability freshness, complete policy coverage, all planned runtime fixtures and public tool references before claiming diagnostic availability.
