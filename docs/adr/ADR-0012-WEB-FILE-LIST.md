# ADR-0012: Web directory listing without remote Python

## Status

Accepted. Regression, Python-free SSH acceptance and old-Python differential comparison passed. Installation and Git delivery are tracked separately.

## Context

The listing backend requires remote Python even for read-only metadata. Name and text searches reuse listing. The replacement must preserve public arguments, permissions, metadata, depth and truncation behavior.

## Decision

Use a fixed isolated Bash command with GNU find, sort, realpath, stat and base64, plus getent for numeric identity fallback. Return bounded, framed base64 metadata and construct JSON on the host. Keep the existing ten-second timeout and enforce an two-MiB stdout, 2048-byte stderr and zero stdin limit. Transport errors discard partial output. Do not invoke Python, sudo or a privileged helper.

Enumerate each directory in filesystem order, emit that directory's entries in name order, then recurse into actual directories in their original enumeration order. Depth zero emits all immediate children. For positive depths, prune child directories and links to directories from the boundary level before emitting entries; boundary files and links to files remain visible. Batch stat calls and cache owner/group names per numeric ID within each request. Do not recurse through symlinks. Exclude entries whose canonical paths leave the canonical public root. Check the entry limit before the next candidate, including an outside-root candidate, to preserve truncation behavior. Emit lstat metadata for links.

## Alternatives

- Keep Python: preserves implementation but retains the deployment prerequisite.
- Glob-only recursion: changes recursive directory order.
- A recursive sorted find pipeline: changes traversal and truncation order.
- Privileged helper: unnecessary authority for read-only listing.

## Security and operational conditions

Run with the SSH user's existing authority and an isolated environment. Read directory entries and metadata only; do not read file contents, write files or start network connections. This is a live traversal, not an atomic snapshot. Concurrent filesystem changes can change results or cause failure. Paths are checked before enumeration and metadata access; portable pathname checks do not provide an atomic namespace lock. Invalid UTF-8 metadata is a sanitized failure. GNU utilities and Bash are prerequisites; missing utilities are explicit errors.

## Impact and validation

Only the internal listing command and host decoder change. Public list and dependent name/text searches retain their schemas. Hash remains Python. Update English/Japanese contracts, scenarios and result records. Require framing/metadata unit tests, old-Python differential fixtures, Python-free non-root SSH tests, read-only audit, full regression and installation verification before acceptance.
