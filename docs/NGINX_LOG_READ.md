# Nginx log read contract

For Japanese, see [the Japanese contract](ja/NGINX_LOG_READ.ja.md).

## Inputs and execution

`service_logfile_read` keeps its existing inputs and response fields. The Nginx provider accepts registered `access` and `error` log keys. It checks canonical equality with an approved regular log file. The reader requires Bash and GNU-compatible base64, realpath, stat, dd, date and head, with readable remote timezone evidence for time filtering. It does not install utilities, use sudo, or require Python. Other implementations remain unverified until acceptance establishes compatibility. The command retains its ten-second deadline and read-only risk level.

`lines` defaults to 500 and clamps to 1..5000. `sinceMinutes` defaults to zero and clamps to 0..1440. The reader captures the file size, reads at most 262145 bytes from the appropriate tail offset, rejects NUL in any captured byte, keeps the final 262144 bytes, then decodes UTF-8 using replacement. File growth, shrinkage or rotation are not a transactionally frozen snapshot. A regular-file descriptor check prevents reading a special file after open; string-path authorization is not a guarantee against adversarial path replacement.

## Rows and time

Select the final requested rows using Python-compatible text line separators before timestamp filtering. Output uses LF and has a terminal LF only when at least one row remains. With `sinceMinutes=0`, timestamp syntax is not examined. Otherwise timestamps equal to or later than remote UTC now minus the interval remain; future timestamps remain eligible. The remote reference clock has microsecond precision.

Access timestamps use bracketed `dd/Mon/yyyy:HH:mm:ss +/-HHMM` anywhere in the row, with fixed English month names. For compatibility, an unknown three-letter month is interpreted as January. Recognized but invalid calendar values or offsets fail the read rather than becoming unrecognized rows. Error timestamps use leading `yyyy/MM/dd HH:mm:ss` and the SSH host's local timezone. Unknown rows are excluded and produce the partial-filter warning.

Remote TZif transitions and their POSIX footer, or an explicit POSIX TZ value, supply the timezone rules. Supported explicit rules include fixed offsets and recurring M, J and ordinal-day transitions. Repeated local times select the earlier occurrence; a gap uses the pre-transition offset, matching the legacy default fold behavior. A daylight abbreviation with implicit host-default rules, leap-second tables and unsupported time-rule suffixes are rejected. File timezone selection supports the default `/etc/localtime`, names below `/usr/share/zoneinfo`, and corresponding absolute paths; explicit zone files must remain in that tree after canonical resolution; arbitrary timezone file paths are unsupported. No Windows timezone database is substituted.

## Output and failure

The public result remains content, encoding `utf-8`, truncation, warnings and a safe error. `KELPIE_TRUNCATED=1` means the captured initial size exceeded 262144 bytes. `KELPIE_SINCE_FILTER_PARTIAL=1` means unrecognized rows were excluded by time filtering. Both conditions may apply independently.

The versioned internal envelope bounds raw log bytes, timezone evidence (131072 bytes) and total stdout (600000 bytes); stderr is bounded to 2048 bytes. The existing environment launcher has a 65536-byte stdin allowance; this read operation supplies no content stdin. Malformed metadata, excessive output, invalid timestamp values, unsupported timezone evidence, unreadable files, missing tools and command failure return an error without unfiltered content. Timeout/cancellation retains the execution layer's existing handling. No remote writes or automatic fallback to an interpreter occur.

## Acceptance

Acceptance passed on a Debian 12 GNU target without Python: 44 SSH cases and eight supplemental filesystem/transport cases, 40 legacy CPython comparisons and 12 timezone comparisons. The Windows regression passed 1196 tests with three Unix-only skips. Three race cases inject fixture mutations after the size snapshot; three clock cases inject a fixed reference time. These do not claim a transactionally frozen read or modify a real system clock. Other utility/OS implementations remain unverified. The paired [ADR](adr/ADR-0013-NGINX-LOG-READ.md) records the execution boundary and alternatives. Boundary tests cover row ordering, clocks, numeric offsets, DST folds/gaps, byte limits, malformed wire data and provider mapping. Real nonroot SSH, legacy-Python differential comparison and final deployment verification are separate required gates.
