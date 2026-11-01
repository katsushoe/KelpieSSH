# ADR-0015: Void Linux XBPS package provider

## Status

Accepted; provider and isolated acceptance completed.

## Context

Alpine/apk, Arch/pacman and SUSE/zypper exist in source, while the provider inventory still lists the latter two as unimplemented. Void/XBPS is an independent missing Linux package family with an official isolated container and native unprivileged dry runs.

## Decision

Add the existing eight package operations for void/voidlinux with xbps inference. Use cached query/dry-run operations without repository refresh. Preserve row bounds, native failure status, profile mode/confirmation and sudo boundaries. Keep runit service support separate. Reconcile the inaccurate Arch/SUSE inventory in the same documentation change.

## Alternatives

FreeBSD requires a different common-command and VM acceptance scope. Adding only another alias to a supported package family would not add a missing backend. Automatically refreshing indexes conflicts with read-only operations.

## Security and operations

No arbitrary flags, package paths, key import, repository edits or implicit full upgrade. Parameter allowlists, timeouts and existing trusted profile/confirmation apply. Actual changes require configured sudo. Cache freshness is an operator responsibility.

## Validation and documentation

Verify selection/defaults, rendering, injection and mode/confirmation gates; exercise real XBPS reads/dry runs with package/index hashes unchanged and synthetic install/remove on a dedicated nonroot SSH target. Update paired config, CLI/MCP specifications, scenarios/results and product versions, then publish/install with protected state, Git delivery and fixture cleanup. [Contract](../VOID_XBPS.md).
