# Web file metadata

`web_file_stat` returns metadata for a provider-approved path. Site and path permission checks run before SSH execution. The result retains the resolved path, existence, type, size, octal mode, owner, group, UTC modification time and symlink flag. Metadata checks used by permission operations use the same reader.

The reader requires `/usr/bin/env`, `/bin/sh`, `/bin/bash` and compatible GNU `base64`, `realpath`, `stat`, and libc `getent` utilities. It clears the inherited environment, performs no file-content read or write, and does not require Python or sudo. An unavailable or incompatible utility produces an error without automatic installation or interpreter fallback. Other utility implementations need compatibility verification.

Path normalization retains the existing root-boundary rule and trims leading and trailing whitespace from the public path argument. Embedded whitespace remains intact. An inside-root symlink is reported as a symlink with its link metadata and resolved destination; a broken inside-root symlink still exists as a link. An outside-root destination is rejected. A missing path is reported with `exists: false`. Normalization and lstat are separate operations and do not provide an atomic namespace snapshot.

Numeric user and group IDs are returned as strings when name lookup has no entry. An account literally named `UNKNOWN` retains that name. Modes include special permission bits. Modification times are expressed in UTC with microsecond rounding compatible with the previous reader.

The fixed command uses a ten-second timeout, stdout limited to 65536 bytes and stderr to 2048 bytes. A versioned base64 response keeps special names intact. The host validates framing, canonical base64, UTF-8 and numeric ranges before returning metadata. Invalid or timed-out responses do not become successful metadata.

This change covers stat only. `web_file_hash` retains its existing Python implementation, no-follow open, bounded read and descriptor identity checks. See [ADR-0009](adr/ADR-0009-WEB-FILE-STAT.md).
