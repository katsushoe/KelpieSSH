# ADR-0016: MCP tray management and startup updates

Status: accepted for implementation by the user on 2026-11-22.

## Context

Windows services cannot display a user's notification-area icon. The user requests a tray, startup automatic updates and settings for KelpieMCPServer. The existing server must keep its service/headless and SSH trust boundaries.

## Decision

Add the Windows-only KelpieMCPTray companion, launched with kelpiemcp tray/settings and by interactive kelpiemcp start. It has a per-user, per-installation single-instance lock. Closing settings retains the tray; exiting the tray leaves the server running. Service/session-zero execution never launches UI. The existing server stays net8.0; the companion uses net8.0-windows and Designer/resx forms. This administrative UI is within the explicitly requested OSS server scope, separate from KelpiePro's VPS desktop.

Store only non-secret tray preferences atomically beneath LocalApplicationData, separated by installation identity. Keep SSH profiles, trust store and server security configuration out of this editor. Expose server status/start/stop, startup/update preferences, feed and log-folder access. Windows logon registration belongs only to the current user and installation.

Use the user-approved GitHub Releases MSI source. A bounded HTTPS latest-release response selects a newer stable four-part bundle version and one exact KelpieSSH-version-x64.msi asset. Require its SHA-256 digest and size; reject missing digest, wrong asset, downgrade, oversize and HTTP sources. Download to a unique user cache and verify before installation. GitHub's API digest contract is documented at https://docs.github.com/en/rest/releases/assets . A user-specified API must use the same schema and is a trusted user setting.

Copy a small updater host outside the install directory before exiting the tray. An unelevated coordinator waits for the installer worker, obtains Windows UAC consent when needed, and relaunches the tray as the original user. The worker rechecks the hash, uses the existing control CLI to stop/start the server, invokes Windows Installer with a bounded wait and preserves its exit/reboot status. Never change SSH policy, credentials or reboot the computer. Failure restarts the original server when possible and reports the result. No release is automatically published.

## Alternatives

Embedding WinForms in the service is rejected because Session 0 is not the user's desktop. Replacing the SSH desktop is outside scope. Running arbitrary downloaded executables, accepting unsigned metadata hashes from unrelated URLs, or modifying the service from MCP tools is rejected. Release metadata over the configured trusted HTTPS source and its digest define this updater's distribution trust; certificate pinning and a new signing service are not introduced here.

## Validation and compatibility

Test stable-version/asset selection, metadata/download limits, hash/size mismatch, settings persistence and isolation, process arguments and failure recovery. Build and inspect Designer UI. Include the companion in MSI/ZIP and verify the package. Existing MCP tool input/output and SSH policies remain unchanged; local management UI is intentionally not an AI-callable privilege or update API. Update CLI/config/public English/Japanese documentation and private acceptance records. Physical publication/update against a real new release is separate from synthetic installer-path acceptance.
