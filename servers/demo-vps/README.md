# KelpieSSH Demo VPS Container

This directory provides a Docker-based demo target that behaves like a small VPS for KelpieSSH demonstrations.

It runs an Ubuntu container with:

- OpenSSH server
- Login user: `kelpie`
- Demo password: `kelpie-demo`
- `sudo` enabled for the login user
- SSH exposed on localhost port `2222`

This is for local demos only. Do not use this password, SSH configuration, or container layout for production.

## Start

```powershell
docker compose -f servers\demo-vps\docker-compose.yml up -d --build
```

If port `2222` is already used:

```powershell
$env:KELPIE_DEMO_SSH_PORT = "2224"
docker compose -f servers\demo-vps\docker-compose.yml up -d --build
```

## Login

```powershell
ssh kelpie@127.0.0.1 -p 2222
```

Password:

```text
kelpie-demo
```

## Try Sudo

Inside the container:

```bash
sudo apt-get update
sudo apt-get install -y htop
```

## Override Demo User Or Password

PowerShell:

```powershell
$env:KELPIE_DEMO_USER = "ops"
$env:KELPIE_DEMO_PASSWORD = "change-me-demo"
docker compose -f servers\demo-vps\docker-compose.yml up -d --build
```

## Kelpie Profile Example

For password-session based testing, create a profile like this:

```json
{
  "Host": {
    "Address": "127.0.0.1",
    "Port": 2222
  },
  "Auth": {
    "Method": "password",
    "PasswordSecretName": "kelpie:demo-vps"
  },
  "DefaultUser": "kelpie",
  "Users": {
    "kelpie": {
      "Mode": "Safe",
      "AllowedRoots": {
        "/var/log": "ReadOnly",
        "/tmp": "ReadWrite"
      },
      "Platform": {
        "OsFamily": "ubuntu",
        "PackageManager": "apt"
      }
    }
  }
}
```

Then store the temporary password in the running Kelpie MCP server session:

```powershell
kelpiemcp password demo-vps
```

Enter:

```text
kelpie-demo
```

## Stop

```powershell
docker compose -f servers\demo-vps\docker-compose.yml down
```
