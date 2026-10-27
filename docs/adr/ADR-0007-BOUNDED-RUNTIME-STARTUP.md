# ADR-0007: Bounded optional runtime startup

## Status

Accepted for the D1 library infrastructure increment, 2026-10-27. PHP/Node diagnostic adapters and public tool registration remain a later stage.

## Context

Optional runtime diagnostics need trusted executable selection, isolated startup and byte limits applied before whole-output buffering. SSH.NET 2026.0.0 `SshCommand` owns growing `PipeStream` receivers, so checking `Result.Length` or draining them concurrently does not establish a fixed memory bound. Existing SSH commands must retain their transport and result contracts.

## Decision

Add optional `ExecutionLimits` to fixed command definitions and requests, preserving their positional constructors. Only requests that opt in use a non-PTY SSH.NET Shell with caller-owned, fixed-capacity stdout/stderr sinks. A random per-channel completion frame carries the exit status; missing/invalid frames fail rather than infer success. The transport does not call `SshCommand.Result` or `Error` on this path. The normal null-limits path is unchanged.

The stdout receiver reserves 96 bytes beyond the configured payload limit for the completion frame. Its allocation never grows. Payload bytes are checked again after removing the frame; stderr has its own exact limit. Overflow discards captured data and throws a sanitized `SshCommandOutputLimitException`. Text input is bounded and Base64-encoded as data inside the shell input script. Binary input is unsupported on this path. UTF-8 decoding is strict. Cancellation closes the dedicated channel; it does not prove termination of an arbitrary remote child process.

The fixed shell input ends with an explicit exit. After sending those bytes, its input stream reports a dedicated local end-of-input exception so SSH.NET finishes its input listener before channel disposal. Only that locally created termination is ignored by the error callback. This avoids the upstream zero-byte-send/close race; it does not accept a missing remote completion frame.

`IgnoreEnvironmentOverrides` is an opt-in definition/request property for fixed isolated launchers. Runtime startup probes additionally use `env -i`, a neutral working directory, fixed system utility paths, root-owned non-writable components, bounded symlink resolution and immediate metadata revalidation. GNU timeout kills the runtime process group after five seconds. Only literal PHP/Node selectors are accepted. These definitions are not added to the default catalog in D1.

## Alternatives

- Post-execution length checks or concurrent SshCommand draining: do not bound the library's internal receiver.
- Reflection into private SSH.NET fields or a vendored transport fork: couples security behavior to undocumented internals and creates maintenance burden.
- OpenSSH subprocesses: would require a separate credential, host-key and password-session integration.
- Non-PTY Shell with bounded sinks: uses the installed library's public direct-output API while keeping the existing authentication/host-key verifier. Selected only for opt-in requests.

## Security and operating conditions

The OS, SSH login environment and fixed bootstrap utilities (`/bin/sh`, `/usr/bin/env`, stat, readlink, timeout and base64) are trusted prerequisites. Cleanup cannot undo malicious login-shell or loader code already executed. Candidate runtime links and ancestors are checked individually; resolving a canonical path first could hide a writable intermediate directory. Symlink expansion allows only bounded ASCII system paths. Privileged administrator replacement remains inside the trusted OS boundary; metadata checks do not provide a kernel-pinned executable descriptor.

The bounded path requires a server that permits a non-PTY shell. Banners, forced commands or channel failures can invalidate the frame. Never fall back to an unbounded transport after failure. Runtime probes use the normal SSH command-service policy pipeline and reject direct root login there. Trust/profile resolution and adapter result mapping remain with their normal callers and the later D2 stage; D1 is not a new authorization mechanism.

## Implementation and verification

Application defines the opt-in limits and fixed startup catalog assets. Infrastructure owns authentication, direct-output reception, framing and channel lifetime. Existing MCP/CLI result types do not change. Unit tests cover capacities, UTF-8 byte boundaries, framing, data encoding, policy forwarding and default-catalog exclusion. Isolated actual SSH acceptance must cover output floods, cancellation, deadlines, startup hooks and untrusted candidates/links. Runtime features and all planned B3 acceptance conditions must not be declared implemented merely because D1 startup works.

See the [library contract](../RUNTIME_DIAGNOSTIC_STARTUP.md) and [Japanese decision](../ja/ADR-0007-BOUNDED-RUNTIME-STARTUP.ja.md).

## Primary implementation references

- [SSH.NET SshCommand, tag 2026.0.0](https://github.com/sshnet/SSH.NET/blob/2026.0.0/src/Renci.SshNet/SshCommand.cs)
- [SSH.NET PipeStream, tag 2026.0.0](https://github.com/sshnet/SSH.NET/blob/2026.0.0/src/Renci.SshNet/Common/PipeStream.cs)
- [SSH.NET Shell, tag 2026.0.0](https://github.com/sshnet/SSH.NET/blob/2026.0.0/src/Renci.SshNet/Shell.cs)
