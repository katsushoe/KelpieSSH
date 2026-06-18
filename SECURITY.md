# Security Policy

## Supported Versions

KelpieSSH is currently in early alpha development. Security fixes are applied to the latest public release and the default development branch.

## Reporting a Vulnerability

Please report security issues privately by using GitHub security advisories if available.

If GitHub security advisories are not available, contact the maintainer directly at [shoe0604@akatsukisoft.com](mailto:shoe0604@akatsukisoft.com).

Do not open a public issue for vulnerabilities that expose secrets, authentication bypasses, unsafe command execution, or unintended access to remote hosts.

When reporting a vulnerability, include the affected version, environment, reproduction steps, expected impact, and any relevant logs or screenshots. Do not include real passwords, private keys, passphrases, production profile files, or raw logs containing secrets.

## Security Model

KelpieSSH is intended to assist VPS diagnostics and maintenance over SSH while keeping command execution constrained.

- SSH profiles must not use direct `root` login.
- Plain text passwords must not be stored in JSON configuration files.
- Password authentication is kept in memory for the running `KelpieMCPServer` session.
- SSH command execution is policy-based and starts from allow-listed diagnostic operations.
- Path-based operations should be constrained with `AllowedRoots` and `SpecialPaths`.
- Secrets must not be displayed through MCP tools.
- Operations that may change remote state should use dedicated commands, policy checks, and explicit confirmation where applicable.

## User Responsibilities

- Review generated profile files before connecting to a server.
- Use KelpieSSH only on systems you own or are authorized to manage.
- Keep private keys and real profile files outside the public repository.
- Use the least-privileged SSH user that can perform the required diagnostics.
- Review profiles, permissions, confirmation strings, command scopes, target hosts, and expected changes before allowing operations that may modify remote state.
- Test maintenance, package, service, web file, permission, and configuration changes in a safe environment before applying them to production systems.
- Keep restorable backups for important servers and data. KelpieSSH safety checks reduce risk, but they do not replace backups, change review, or operational recovery planning.
- Keep `config/`, `profiles/`, `keys/`, `dat/`, and `logs/` in a local Kelpie home directory, not in the source repository.

KelpieSSH is provided as-is, without warranties of any kind. The authors and contributors are not responsible for data loss, service outage, security incidents, configuration damage, business interruption, or any other damage caused by use or misuse of the software.
