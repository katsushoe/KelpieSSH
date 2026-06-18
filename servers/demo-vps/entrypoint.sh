#!/usr/bin/env sh
set -eu

if [ -n "${DEMO_USER:-}" ] && [ -n "${DEMO_PASSWORD:-}" ]; then
    printf '%s:%s\n' "${DEMO_USER}" "${DEMO_PASSWORD}" | chpasswd
fi

ssh-keygen -A

exec "$@"
