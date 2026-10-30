# Web public directory listing

The list backend uses Bash and GNU utilities without remote Python. See [ADR-0012](adr/ADR-0012-WEB-FILE-LIST.md) for the design and acceptance status. Hash and other unmigrated operations retain their own prerequisites.

## Inputs and configuration

Public inputs remain `profileName`, `siteKey`, `path`, `maxDepth` (0..5, default 0) and `limit` (1..500, default 100). The selected web site's root and existing read/path policies remain authoritative. No new configuration keys are added. Name and text searches reuse this backend; their filtering and content-reading rules remain unchanged.

## Results and traversal

The existing result contains site identity, requested and resolved paths, `exists`, `entries`, `truncated`, warnings and an optional error. Entries retain name, root-relative path, resolved path, type, byte size, octal permissions, owner, group, UTC last-modified time, depth and symlink flag. Unmapped owner/group IDs are decimal strings. Link metadata describes the link; its resolved path describes the target.

Depth zero lists immediate children. Greater depths list each directory's entries in name order, then visit actual subdirectories in filesystem enumeration order. The requested directory's children have depth zero. For a positive maxDepth, entries in directories at that depth include files and links to files, while child directories and links to directories are excluded before counting or truncation. Depth zero keeps its separate immediate-list behavior and includes directories. Recursive directory order is not globally sorted. Symlinks are listed but never followed for recursion. Entries resolving outside the site root are excluded. A requested inside-root directory link resolves to that directory before traversal.

The limit counts emitted entries. Reaching exactly the limit is not truncation unless a subsequent candidate is encountered. That candidate can be an excluded outside-root link, matching the previous behavior. Missing directories return `exists=false` and an empty list. Existing non-directories fail. An unreadable requested directory fails at depth zero; recursive traversal skips an unreadable directory, matching the former walk behavior.

## Execution and errors

The command runs as the existing SSH user in an isolated environment. Bash, GNU base64/realpath/stat/find/sort and getent are required. It reads directory entries and metadata without opening file contents. Sorting cannot spill to a writable temporary directory; an exhausted sort buffer fails. No sudo, helper, file writes or outbound connections are requested.

The command timeout is ten seconds. Stdout is capped at two MiB, stderr at 2048 bytes and stdin at zero bytes. Missing utilities, permission failures, malformed responses, invalid UTF-8 metadata and transport limits return sanitized errors. Partial output is discarded on failure. Cancellation follows the existing SSH cancellation contract. Invalid public bounds and denied paths fail before dispatch.

Results represent a live filesystem traversal, not a snapshot. Concurrent changes may alter results or fail an operation. Canonical checks retain the existing pathname boundary; they do not provide an atomic namespace lock.

## Verification

Acceptance covers framing, arbitrary valid UTF-8 names including newlines, metadata types and permissions, identity fallback, depth and count boundaries, ordering, truncation, inside/outside/broken links, unreadable and missing directories, utility failure, policy modes, dependent searches, timeout/transport failure and a non-root Python-free SSH audit. Release regression: 1152 passed, zero failed and three Unix-only skips. Python-free non-root SSH: 37 passed, with 32 audited dispatches and five pre-dispatch denials; five positive audit controls detected. The 31 comparable cases matched the former Python 3.12.16 backend, including boundary-directory pruning and recursive enumeration order.
