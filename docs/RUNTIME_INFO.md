# Isolated runtime information

CLI: `kelpie runtime-info <profile> <php|node> [--refresh]`. MCP: `ssh_runtime_info(profileName, runtime, refresh=false)`.

The required runtime is exactly `php` or `node`. Paths, code, module names, raw arguments and environment dictionaries are not accepted. Current pinned, non-root SSH profiles and the normal command policy pipeline are required. ReadOnly, Safe, Maintenance and Expert permit this read-only operation through CLI and MCP. No new profile configuration, helper, sudo or package installation is needed.

The result contains `protocol=1`, a host-generated 32-character `requestId`, `operation=runtime.info`, `runtime`, `status`, `startupMode`, `version`, `features.json`, `features.sha256`, host UTC `checkedAt`, `cached=false`, and `warnings=[]`. Success has `status=ok`, `startupMode=isolated`, a normalized numeric version, JSON true and the explicitly measured SHA-256 boolean. Failed observations have null mode/version/features. No remote paths, settings, stderr, environment or process arguments are returned.

Statuses are `ok`, `unavailable`, `unsupported`, `denied`, `invalid-input`, `invalid-response`, `timeout`, `cancelled`, and `connection-failed`. Only a missing same-family fixed candidate is unavailable. An untrusted candidate is denied. Bootstrap/major-version incompatibility is unsupported. Malformed, duplicate, unknown, oversized, mismatched or extra response data is invalid-response. The CLI exits 0 only for ok and 1 otherwise. Invalid CLI syntax and missing profiles use the existing CLI input-error path; missing MCP profiles use the existing MCP input-error path.

Every call resolves the current profile and rechecks the executable. This increment has no preparation/result cache; refresh is accepted and every observation is fresh. Capability registration indicates the operation is implemented, not that a runtime is installed or healthy. Existing target inventory is unchanged.

For the fixed invocation, PHP exit 1 (CLI option rejection, including a CGI executable) and Node exit 9 (invalid startup option) are `unsupported`. These mappings apply only to their respective runtime families. Other nonzero exits remain invalid-response unless a documented timeout, trust, or availability classification applies. No remote error text is exposed and no retry follows a startup rejection. See [PHP CLI-only options](https://www.php.net/manual/en/features.commandline.options.php) and [Node exit codes](https://nodejs.org/docs/latest-v22.x/api/process.html#exit-codes).

See [the startup boundary](RUNTIME_DIAGNOSTIC_STARTUP.md) and [ADR-0007](adr/ADR-0007-BOUNDED-RUNTIME-STARTUP.md). D1 readiness probes remain private to the library. The registered fixed definitions are `runtime_php_info` and `runtime_node_info`. Supported versions are PHP 8 and Node 22/24. D3 acceptance covers these runtimes on a Debian bookworm fixture with unit tests for policy and protocol boundaries; it does not establish support for every OS or actual AI desktop client. PHP runs without php.ini and reports isolated CLI built-ins, not FPM or application health. Node loads only the product-owned built-in fs/crypto modules. No application import, subprocess or network action is part of the operation.

Limits: request 4096 bytes; stdout 16384 bytes plus 96 transport framing bytes; stderr 2048 bytes; JSON depth 8; numeric version 64 characters; remote phase 5 seconds; overall request 15 seconds. Remote phase timeout can return exit 137 and is normalized to timeout. Transport cancellation does not prove arbitrary child termination; the fixed launcher has its independent deadline. No automatic retry or cross-runtime fallback occurs.

Primary references: [PHP CLI](https://www.php.net/manual/en/features.commandline.options.php), [PHP hash algorithms](https://www.php.net/manual/en/function.hash-algos.php), [Node built-in crypto](https://nodejs.org/docs/latest-v22.x/api/crypto.html#cryptogethashes).

## Example success result

```json
{
  "protocol": 1,
  "requestId": "0123456789abcdef0123456789abcdef",
  "operation": "runtime.info",
  "runtime": "php",
  "status": "ok",
  "startupMode": "isolated",
  "version": "8.2.35",
  "features": {
    "json": true,
    "sha256": true
  },
  "checkedAt": "2026-10-27T00:00:00Z",
  "cached": false,
  "warnings": []
}
```
