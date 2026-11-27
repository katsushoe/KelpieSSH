# ADR-0004: Validate Existing Windows Trust Key ACLs on Read

## Status

Accepted on 2026-10-11.

## Context

TrustStore version 4 stores its AES-GCM key in an adjacent ACL-protected file. Replacing its ACL on every read requires permission to change permissions and removes explicit grants for other runtime identities. A runtime with administrator-approved Read access cannot start under that behavior.

## Decision

Create new keys with a protected ACL granting FullControl to LocalSystem, Administrators, and the creating identity. For existing version 4 keys, validate the ACL and read the key without modifying permissions. Reject inherited or null discretionary ACLs and Allow rules for broad principals (Everyone, Authenticated Users, Builtin Users, Builtin Guests, Anonymous, Network, Interactive, and application-package aggregate identities). Preserve explicit grants and Deny rules. Actual file access remains enforced by Windows.

Legacy version 2 migration retains its existing ACL protection step. On Unix, the adjacent key file is restricted to owner read/write (`0600`) when loaded or saved; an unsafe existing mode is corrected without replacing the key or trust-store contents.

## Alternatives

- Grant every runtime permission to change the ACL: rejected because reading a key should not require authority to rewrite its access policy.
- Skip ACL validation: rejected because inherited or broad permissions can expose the encryption key.

## Security and Operational Conditions

Administrators approve explicit named user, service, or trusted group grants and protect the containing directory. Validation does not authorize unknown principals or classify arbitrary domain groups. Key contents, existing trust records, and explicit identity grants remain unchanged on normal reads. Invalid ACLs fail closed; administrators repair them explicitly. Normal Read access includes permission to inspect the ACL. Saving still requires access to the data directory and trust-store file.

## Impact and Verification

The change affects Windows TrustStore reading shared by CLI and MCP startup. Regression tests verify that Read-only access succeeds without changing ACLs or key contents, broad or inherited ACLs fail without replacing files, and denied key access remains rejected. Matching backup pairs must be restored with protected key ACLs. The English and Japanese MCP guides describe these requirements.
