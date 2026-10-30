# Web file reads

`web_file_read` reads one provider-approved site path through the normal MCP policy checks. The full-file reader uses a fixed Bash program and GNU-compatible `base64`, `realpath`, `stat` and `head`, with `/usr/bin/env`, `/bin/sh`, `/bin/bash` and Linux `/proc/self/fd`. Python and sudo are unnecessary for this operation. Missing or incompatible utilities fail without automatic installation or interpreter fallback.

The public arguments, site configuration and response fields remain unchanged. Public path normalization trims leading and trailing whitespace. Embedded whitespace, quotes and Unicode are retained. An inside-root symlink resolves to its target. An outside-root target is rejected; missing targets, including broken links, return `exists: false`. Only regular files are read.

The fixed command clears its environment and has a ten-second timeout. It rejects an observed size above the site's `MaxReadBytes`, opens the resolved path, checks the opened descriptor's regular-file type and resolved path, then reads at most `MaxReadBytes + 1` bytes. A growth beyond the limit returns an error without content. These checks do not establish an atomic namespace snapshot or guarantee a coherent snapshot during concurrent writes. Hash-specific identity and no-follow guarantees are separate.

The response carries exact content bytes as canonical base64, including empty content, NUL and non-UTF-8 bytes. The existing `encoding: utf-8` response label is retained and does not validate the content's encoding. Size is the number of bytes returned; modification time is read from the opened descriptor and rounded to UTC microseconds consistently with stat. The host validates framing, metadata UTF-8, timestamp and base64 before producing the public result. Invalid or timed-out responses do not yield successful content.

The existing general SSH reception path is retained; this change does not add a separate streaming byte cap to it. The fixed utility program bounds its read and encoded output using the configured maximum, and the host checks the resulting payload length. A hostile remote program is not covered by that utility-level bound.

This contract covers full-file reads. [Head/tail](WEB_FILE_SLICE.md) use a separate fixed byte reader and host line selection. Directory listing, hashing and write operations retain their existing implementations. See [ADR-0010](adr/ADR-0010-WEB-FILE-READ.md).
