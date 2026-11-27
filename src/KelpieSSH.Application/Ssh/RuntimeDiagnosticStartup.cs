using System.Text;

namespace KelpieSSH.Application.Ssh;

/// <summary>Creates fixed, unregistered D1 startup probes; public tool wiring belongs to the later adapter stage.</summary>
public static class RuntimeDiagnosticStartup
{
    /// <summary>Creates a byte-bounded, environment-isolated PHP or Node startup probe.</summary>
    public static AllowedCommandDefinition CreateProbeDefinition(string runtime)
        => CreateDefinition(runtime, Launcher, "startup_probe");

    /// <summary>Creates the fixed runtime.info operation using the same trusted startup boundary.</summary>
    public static AllowedCommandDefinition CreateInfoDefinition(string runtime)
        => CreateDefinition(runtime, Launcher[..Launcher.LastIndexOf("case \"$runtime\" in", StringComparison.Ordinal)] + InfoDispatch, "info");

    private static AllowedCommandDefinition CreateDefinition(string runtime, string launcher, string operation)
    {
        if (runtime is not ("php" or "node")) throw new ArgumentException("Runtime must be php or node.", nameof(runtime));
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(launcher));
        var command = "/usr/bin/env -i PATH=/usr/bin:/bin HOME=/nonexistent LC_ALL=C /usr/bin/timeout -s KILL 5 /bin/sh -c "
            + "\"$(printf '%s' '" + encoded + "' | /usr/bin/base64 -d)\" -- " + runtime;
        return new AllowedCommandDefinition("runtime_" + runtime + "_" + operation, command, TimeSpan.FromSeconds(15))
        {
            ExecutionLimits = new SshCommandExecutionLimits(16384, 2048),
            IgnoreEnvironmentOverrides = true,
        };
    }

    private const string InfoDispatch = """
case "$runtime" in
php) exec "$chosen" -n -r '
if(PHP_SAPI!=="cli" || PHP_MAJOR_VERSION!==8 || !function_exists("json_encode")){exit(66);}
$s=stream_get_contents(STDIN,4097);
if(!preg_match("/\\A{\"protocol\":1,\"operation\":\"runtime.info\",\"requestId\":\"([a-f0-9]{32})\",\"runtime\":\"php\"}\\z/",$s,$m)){exit(64);}
$hash=function_exists("hash_algos") && in_array("sha256",hash_algos(),true);
echo json_encode(["protocol"=>1,"operation"=>"runtime.info","requestId"=>$m[1],"runtime"=>"php","startupMode"=>"isolated","version"=>PHP_MAJOR_VERSION.".".PHP_MINOR_VERSION.".".PHP_RELEASE_VERSION,"features"=>["json"=>true,"sha256"=>$hash]],JSON_THROW_ON_ERROR);
' ;;
node) exec "$chosen" --input-type=commonjs --no-addons --eval '
const fs=require("node:fs"),b=Buffer.alloc(4097);let n=0,k;while(n<b.length&&(k=fs.readSync(0,b,n,b.length-n,null))>0)n+=k;
const m=/^\{"protocol":1,"operation":"runtime.info","requestId":"([a-f0-9]{32})","runtime":"node"\}$/.exec(b.subarray(0,n).toString("utf8"));
if(!m)process.exit(64);if(!/^(22|24)\.[0-9]+\.[0-9]+$/.test(process.versions.node))process.exit(66);
const crypto=require("node:crypto");process.stdout.write(JSON.stringify({protocol:1,operation:"runtime.info",requestId:m[1],runtime:"node",startupMode:"isolated",version:process.versions.node,features:{json:true,sha256:crypto.getHashes().includes("sha256")}}));
' ;;
esac
""";

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
