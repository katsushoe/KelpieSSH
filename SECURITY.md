# Security Policy

## Supported Versions

KelpieSSH is currently in early alpha development. Security fixes are applied to the latest public release and the default development branch.

## Reporting a Vulnerability

Please report security issues privately by using GitHub security advisories if available, or by contacting the maintainers through the repository owner profile.

Do not open a public issue for vulnerabilities that expose secrets, authentication bypasses, unsafe command execution, or unintended access to remote hosts.

## Security Model

KelpieSSH is intended to assist VPS diagnostics and maintenance over SSH while keeping command execution constrained.

- SSH profiles must not use direct `root` login.
- Plain text passwords must not be stored in JSON configuration files.
- Password authentication is kept in memory for the running `KelpieMCPServer` session.
- SSH command execution is policy-based and starts from allow-listed diagnostic operations.
- Path-based operations should be constrained with `AllowedRoots` and `SpecialPaths`.
- Secrets must not be displayed through MCP tools.

## User Responsibilities

- Review generated profile files before connecting to a server.
- Keep private keys and real profile files outside the public repository.
- Use the least-privileged SSH user that can perform the required diagnostics.
- Keep `config/`, `profiles/`, `keys/`, `dat/`, and `logs/` in a local Kelpie home directory, not in the source repository.
