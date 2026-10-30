# Web file head and tail reads

`web_file_head` and `web_file_tail` use the same site, content-type, path and MCP policy checks as before. Public arguments, response fields and warnings remain unchanged. Requested `maxBytes` is 1–1048576 and is reduced to the site's `MaxReadBytes`; `maxLines` is 0–1000, with zero disabling the line limit.

The fixed reader uses an environment-cleared Bash program, GNU-compatible `base64`, `realpath` and `stat`, plus `head` for head reads or GNU `dd` with byte-count/skip flags for tail reads. It also requires `/usr/bin/env`, `/bin/sh`, `/bin/bash` and Linux `/proc/self/fd`. Python, sudo and a remote helper are unnecessary. Missing or incompatible utilities fail without installation, interpreter fallback or retry.

Head reads at most the effective byte limit from the beginning. Tail observes the source size before opening and starts at `max(0, observedSize - effectiveMaxBytes)`, reading at most the effective limit. Tail does not switch to the new end of a concurrently growing file. Files larger than the limit are allowed. Path normalization, inside-root links, missing targets and regular-file requirements follow the [full-read contract](WEB_FILE_READ.md). The opened descriptor is checked and supplies the modification time. Concurrent writes are not an atomic snapshot; hash-specific guarantees are separate.

After byte selection, the host applies the previous Python `bytes.splitlines(keepends=True)` behavior: CR, LF and CRLF delimit lines and their terminators are retained. Head keeps the first N lines and tail keeps the last N, without adding an empty line after a trailing terminator. Other byte values, including NUL, vertical tab, form feed and invalid UTF-8, are preserved. A byte boundary can split a multibyte character or CRLF. The `encoding: utf-8` label remains and does not validate content. Returned size is the number of selected bytes.

The command timeout is ten seconds. SSH reception is bounded to 2097152 stdout bytes, 2048 stderr bytes and zero stdin bytes. The versioned response reuses the full-reader framing and host timestamp conversion, and rejects malformed, noncanonical or oversized content before line selection. Transport-limit and invalid metadata failures return errors without content; cancellation keeps its existing behavior. Existing slice-bound warnings, including site-limit reduction warnings, remain.

Listing, hashing and write operations are unchanged. See [ADR-0011](adr/ADR-0011-WEB-FILE-SLICE.md).
