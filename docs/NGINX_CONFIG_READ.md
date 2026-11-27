# Nginx configuration reading

The nginx provider reads an approved main configuration or a file directly within an approved include directory. `service_config_file_read` returns `ServiceConfigFileReadResult`; `service_config_file_check_read` returns access information without returning file contents. Discovery uses `nginx -V` and reads the main file to discover include patterns. Existing profile, policy and approved-path checks apply before the fixed internal command.

The fixed reader uses `/usr/bin/env`, `/bin/sh`, `/bin/bash`, and compatible `base64`, `realpath`, `dirname`, and `head` utilities. It also requires `/proc/self/fd`. It clears inherited environment variables and does not require Python, sudo, a helper, or installation of additional packages. A system missing a required operation returns a read error; no interpreter fallback or automatic installation is attempted. Other OS and utility implementations require separate compatibility verification.

The path is canonicalized and compared against the provider-approved exact paths and direct include directories. A regular file is opened once and the opened descriptor is checked before reading. The host receives bounded base64 and validates the decoded bytes and UTF-8 before exposing content. This reduces pathname replacement races but does not claim an atomic guarantee against a privileged administrator changing the target.

Public reads return at most 65536 bytes. Read-access checks use a one-byte limit and do not return content. The internal reader accepts positive limits up to 999999 bytes; its stdout transport is limited to 1333340 bytes and stderr to 2048 bytes. A further byte detects truncation. NUL, invalid UTF-8, malformed or oversized transport data and an invalid multibyte sequence at the truncation boundary produce errors without exposing captured output. A successful truncated read retains the existing truncated flag and warning. A failed main-file discovery is not retried for the same main file in the subsequent read or access check.

No remote write, temporary file, privileged operation, configuration change, runtime fallback or automatic retry after an ambiguous transport failure is part of this reader. Cancellation and transport errors follow the existing command-service error path. The fixed command and the host decoder use the normal SSH policy pipeline. The public result fields and profile configuration are unchanged.

See [ADR-0008](adr/ADR-0008-NGINX-CONFIG-READ.md) for the design and limitations. Linux SSH acceptance remains a separate verification gate for deployment.
