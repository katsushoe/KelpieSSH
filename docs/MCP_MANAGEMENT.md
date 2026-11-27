# MCP tray, settings and automatic updates

The Windows package includes KelpieMCPTray, the per-user notification-area manager for KelpieMCPServer. The server continues to support Windows Service and headless operation. Windows services do not display desktop UI; startup update checks run when the user tray starts, not in Session 0.

## Start and use

1. On the Windows PC where KelpieSSH is installed, open PowerShell as the normal Windows user. Run `kelpiemcp tray`, or `kelpiemcp settings` to open settings immediately. Interactive `kelpiemcp start` and direct interactive server startup also launch the installed tray. Use `--no-tray` to suppress automatic launch.
2. Find the KelpieMCPServer icon in the Windows notification area, including the hidden-icons menu. Double-click it, or right-click and select Settings. The current UI labels are Japanese; the settings command opens the form directly.
3. Select Windows logon startup, server startup, and startup automatic updates as needed. Save with the save button. Closing the settings window retains the tray; exiting the tray leaves the server running. Administrator controls use the normal Windows UAC prompt and existing CLI authorization.
4. The status entry reports the existing CLI status. Open logs from the menu. Server profiles, secret values and trust permissions are not edited by this window.

## Preferences

Non-secret preferences are stored atomically at `%LOCALAPPDATA%/Akatsukisoft/KelpieSSH/<installation-id>/tray.json`. Preferences are separate by installation path. One tray runs per Windows user and desktop session. A repeated settings request brings the existing window forward, including requests from another installation path. The management log and last installer result are in that directory. The normal server log folder remains unchanged.

| Key | Required | Initial value | Meaning |
| --- | --- | --- | --- |
| AutoStartServer | no | true | Start the server when the tray starts. |
| CheckUpdatesOnStartup | no | true | Check and automatically apply a verified newer MSI at tray startup. |
| StartAtLogon | no | false | Register only this installation in the current user Run key when saved. |
| ReleaseApiUrl | no | https://api.github.com/repos/katsushoe/KelpieSSH/releases/latest | Public trusted GitHub-compatible latest-release API. |

The preference file is not the server's trusted kelpiemcp.json. No automatic profile/config baseline acceptance occurs. Changing these preferences does not change SSH mode, roots, credentials or permissions. MSI uninstall removes this installation's owned logon entry and retains preferences; upgrade retains the entry. The tray requires .NET Desktop Runtime 8 x64 in addition to the existing server runtimes.

## Update contract

The user-approved source is GitHub Releases. The latest stable non-draft release must have a newer four-part numeric tag and exactly one `KelpieSSH-<version>-x64.msi` asset with a positive size up to 512 MiB and a `sha256:` digest. The bundle version comes from installed kelpie.exe. Metadata is limited to 1 MiB; HTTPS without URL credentials is required. A GitHub API source can only select downloads in the same GitHub repository. Custom API sources use the same schema and are trusted user settings; private feed authentication is not supported.

Downloads use unique cache files, declared-size bounds and SHA-256 verification. An updater host copied outside the installation coordinates one elevated worker, closes the tray, stops the server with the existing CLI, runs Windows Installer, and restores the prior server state. It rechecks the hash while keeping a read lock on the MSI. Windows Installer exit 0 and 3010 are accepted; no automatic PC reboot is requested. The original-user coordinator relaunches the tray with a one-time update skip. Existing configuration, profiles, keys and user data are retained.

Unavailable feeds or failed downloads do not prevent ordinary server startup. Known installation failure attempts server recovery. An unknown installer outcome does not restart the server or repeat installation automatically; inspect the management log and Windows Installer before retrying. Windows may require UAC consent to apply an update. A cancelled consent or failed update leaves a visible settings/error path. No release is published by enabling this feature.

See [ADR-0016](adr/ADR-0016-MCP-TRAY-UPDATE.md). GitHub's [release asset API](https://docs.github.com/en/rest/releases/assets) defines asset digests. MSI custom actions follow the [WiX schema](https://docs.firegiant.com/wix/schema/wxs/customaction/).

Right-click the tray icon and select **バージョン情報** (About) to see the tray, installed MCP server and bundle versions, publisher and license.
