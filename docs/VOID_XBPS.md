# Void Linux XBPS provider

For Japanese, see [the Japanese contract](ja/VOID_XBPS.ja.md).

## Selection and configuration

Platform.OsFamily accepts void or voidlinux (case-insensitive); the latter normalizes to void. PackageManager defaults to xbps when omitted, or can be set explicitly. A different configured manager does not select this provider. See [the sample](../config_samples/servers/void.json). No new public tool or CLI argument is introduced. Existing package commands select the provider using the trusted profile; generic Linux support is not a promise of runit lifecycle or all Web/Nginx utility compatibility.

## Operations

| Named operation | Remote command | Limit/deadline | Risk |
| :--- | :--- | :--- | :--- |
| pkg_check_updates | xbps-install -un | 60s | Read-only dry-run |
| pkg_info | xbps-query -R package | 30s | Read-only |
| pkg_search | xbps-query -Rs query | final first0..999 rows;45s | Read-only |
| pkg_list_installed | xbps-query -l then literal case-insensitive filter | final first0..999 rows;45s | Read-only |
| pkg_simulate_install | xbps-install -n package | 60s | Read-only dry-run |
| pkg_simulate_remove | xbps-remove -n package | 60s | Read-only dry-run |
| pkg_install | sudo -n xbps-install -y package | 10min | Confirmation required |
| pkg_remove | sudo -n xbps-remove -y package | 10min | Confirmation required |

Package tokens are1..128 ASCII letters/digits plus . + : _ - and start with a letter/digit. Query/filter tokens are1..64 with the same character rules. Shell fragments, spaces, path separators and leading option flags are rejected. Search uses XBPS native pattern semantics; installed filtering is literal. Row limits are0..999, with existing public defaults preserved; zero emits no rows. The upstream XBPS exit code is retained even after filtering/truncation, so errors are not converted to success. Native output is returned through the existing result model; nonzero status, stderr, timeout and cancellation keep existing mapping.

## State and safety

Read-only and dry-run commands omit -S and -M and use cached indexes, which may be stale or missing. The provider does not refresh metadata, import signing keys or install sudo. A human manages repository configuration and signing trust. Actual changes require existing profile mode/confirmation checks plus administrator-configured sudo access. A new signing-key prompt can still block despite -y; it is bounded by the existing timeout and must be handled by the administrator, not auto-trusted. Package expressions with version comparator operators are not accepted by this initial allowlist.

## Acceptance

Validated on isolated Void Linux with XBPS 0.60.7: 15 nonroot SSH cases passed, including unchanged database/cache/repository hashes for read and dry-run operations, missing-package errors, row limits, ReadOnly rejection and synthetic install/remove. Four published CLI cases passed, including voidlinux alias/default-manager selection and confirmation preview without execution. Release regression: 1236 passed, 0 failed, 3 Unix-only skipped (1239 total); 21 added provider cases cover selection, rendering and unsafe arguments. Runit service operations, automatic repository refresh and full system upgrades are outside scope.

Sources: [XBPS query](https://man.voidlinux.org/xbps-query.1), [install](https://man.voidlinux.org/xbps-install.1), [remove](https://man.voidlinux.org/xbps-remove.1), [official images](https://github.com/void-linux/void-containers). Decision: [ADR-0015](adr/ADR-0015-VOID-XBPS.md).
