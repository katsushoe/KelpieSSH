using System.Text;

namespace KelpieSSH.Application.Ssh;

/// <summary>Creates fixed, unregistered D1 startup probes; public tool wiring belongs to the later adapter stage.</summary>
public static class RuntimeDiagnosticStartup
{
    /// <summary>Creates a byte-bounded, environment-isolated PHP or Node startup probe.</summary>
    public static AllowedCommandDefinition CreateProbeDefinition(string runtime)
    {
        if (runtime is not ("php" or "node")) throw new ArgumentException("Runtime must be php or node.", nameof(runtime));
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(Launcher));
        var command = "/usr/bin/env -i PATH=/usr/bin:/bin HOME=/nonexistent LC_ALL=C /usr/bin/timeout -s KILL 5 /bin/sh -c "
            + "\"$(printf '%s' '" + encoded + "' | /usr/bin/base64 -d)\" -- " + runtime;
        return new AllowedCommandDefinition("runtime_" + runtime + "_startup_probe", command, TimeSpan.FromSeconds(15))
        {
            ExecutionLimits = new SshCommandExecutionLimits(16384, 2048),
            IgnoreEnvironmentOverrides = true,
        };
    }

    // GNU stat/readlink/timeout and /bin/sh are trusted OS bootstrap prerequisites.
    // Resolve components individually: canonicalizing first would hide untrusted link ancestors.
    internal const string Launcher = """
set -f
fail() { printf '%s\n' "$1" >&2; exit 65; }
cd / || fail runtime-startup-unsupported
inspect() {
    metadata=$(/usr/bin/stat -c '%u:%f:%d:%i:%s:%Y' -- "$1" 2>/dev/null) || fail runtime-startup-untrusted
    uid=${metadata%%:*}
    tail=${metadata#*:}
    hexmode=${tail%%:*}
    case "$uid:$hexmode" in *[!0-9a-fA-F:]*) fail runtime-startup-untrusted;; esac
    mode=$((0x$hexmode))
    [ "$uid" = 0 ] || fail runtime-startup-untrusted
    kind=$((mode & 61440))
    if [ "$kind" != 40960 ]; then
        [ $((mode & 18)) = 0 ] || fail runtime-startup-untrusted
        [ $((mode & 3072)) = 0 ] || fail runtime-startup-untrusted
    fi
}
resolve() {
    pending=${1#/}
    resolved=
    hops=0
    steps=0
    inspect /
    [ "$kind" = 16384 ] && [ -x / ] || fail runtime-startup-untrusted
    while [ -n "$pending" ]; do
        steps=$((steps + 1))
        [ "$steps" -le 128 ] || fail runtime-startup-untrusted
        segment=${pending%%/*}
        if [ "$segment" = "$pending" ]; then pending=; else pending=${pending#*/}; fi
        case "$segment" in ''|.) continue;; ..) [ -n "$resolved" ] || fail runtime-startup-untrusted; resolved=${resolved%/*}; continue;; esac
        current=$resolved/$segment
        inspect "$current"
        if [ "$kind" = 40960 ]; then
            hops=$((hops + 1))
            [ "$hops" -le 16 ] || fail runtime-startup-untrusted
            link=$(/usr/bin/readlink -- "$current" 2>/dev/null) || fail runtime-startup-untrusted
            case "$link" in ''|*[!a-zA-Z0-9_./+-]*) fail runtime-startup-untrusted;; esac
            [ "${#link}" -le 4096 ] || fail runtime-startup-untrusted
            case "$link" in /*) resolved=; link=${link#/};; esac
            if [ -n "$pending" ]; then pending=$link/$pending; else pending=$link; fi
            continue
        fi
        resolved=$current
        if [ -n "$pending" ]; then
            [ "$kind" = 16384 ] && [ -x "$resolved" ] || fail runtime-startup-untrusted
        else
            [ "$kind" = 32768 ] && [ -x "$resolved" ] || fail runtime-startup-untrusted
        fi
    done
    inspect "$resolved"
    [ "$kind" = 32768 ] && [ -x "$resolved" ] || fail runtime-startup-untrusted
    fingerprint=$metadata
}
case "$1" in php|node) runtime=$1;; *) fail runtime-startup-unsupported;; esac
# Check trusted parents before treating an absent fixed leaf as a missing candidate.
chosen=
for parent in /usr/bin /usr/local/bin; do
    # These fixed parents must be ordinary trusted directories; no PATH search is performed.
    inspect /
    inspect /usr
    [ "$kind" = 16384 ] && [ -x /usr ] || fail runtime-startup-untrusted
    if [ "$parent" = /usr/local/bin ]; then
        inspect /usr/local
        [ "$kind" = 16384 ] && [ -x /usr/local ] || fail runtime-startup-untrusted
    fi
    inspect "$parent"
    [ "$kind" = 16384 ] && [ -x "$parent" ] || fail runtime-startup-untrusted
    candidate=$parent/$runtime
    if [ ! -e "$candidate" ] && [ ! -L "$candidate" ]; then continue; fi
    resolve "$candidate"
    chosen=$resolved
    original=$fingerprint
    # Rewalk immediately before exec. A privileged administrator remains inside the OS trust boundary.
    resolve "$candidate"
    [ "$resolved" = "$chosen" ] && [ "$fingerprint" = "$original" ] || fail runtime-startup-changed
    break
done
[ -n "$chosen" ] || { printf '%s\n' runtime-startup-unavailable >&2; exit 127; }
case "$runtime" in
    php) exec "$chosen" -n -r 'if(PHP_SAPI!=="cli"){exit(65);} echo "KELPIE_RUNTIME_READY_V1\n";' ;;
    node) exec "$chosen" --input-type=commonjs --no-addons --eval 'process.stdout.write("KELPIE_RUNTIME_READY_V1\n")' ;;
esac
""";
}
