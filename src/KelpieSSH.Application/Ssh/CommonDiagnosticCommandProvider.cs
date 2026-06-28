namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Provides OS-family neutral diagnostic SSH commands.
/// </summary>
public sealed class CommonDiagnosticCommandProvider : IAllowedCommandProvider
{
    private const string CertificatePathPattern = "^/(etc/letsencrypt/(live|archive)/[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+\\.(pem|crt|cer)|etc/(ssl|pki)(/[A-Za-z0-9_.-]+)+\\.(pem|crt|cer))$";
    private const string BoundedListLimitPattern = "^(200|1[0-9]{2}|[1-9][0-9]?)$";
    private const string CronExpressionPattern = "^[0-9A-Za-z*/?,#LW.-]+( [0-9A-Za-z*/?,#LW.-]+){4}$";
    private const string CronLogPathPattern = "^/var/log/[A-Za-z0-9_./-]{1,160}$";
    private const string ServiceNamePattern = "^[a-zA-Z0-9_.@-]{1,128}$";
    private const string ServiceStatePattern = "^[a-zA-Z,-]{1,64}$";
    private const string ShellCommandTextPattern = "^[A-Za-z0-9_./:+=,@ -]{1,128}$";
    private const string CronTargetTypePattern = "^(user|system)$";
    private const string UserNamePattern = "^[a-z_][a-z0-9_-]{0,31}\\$?$";
    private const string GroupNamePattern = "^[a-z_][a-z0-9_-]{0,31}\\$?$";
    private const string GroupListPattern = "^[a-z_][a-z0-9_-]{0,31}(,[a-z_][a-z0-9_-]{0,31}){0,15}$";
    private const string GroupChangeModePattern = "^(append|replace)$";
    private const string LoginStatePattern = "^(enabled|disabled|unchanged)$";
    private const string PermissionStatePattern = "^(present|absent|unchanged)$";
    private const string PrincipalTypePattern = "^(user|group)$";
    private const string OwnershipScanRootPattern = "^/(etc|home|opt|srv|var|var/log|var/www)(/[A-Za-z0-9_.-]+){0,4}$";
    private const string BackupScanRootPattern = "^/(etc|home|opt|srv|var|var/log|var/www)(/[A-Za-z0-9_.-]+){0,4}$";
    private const string BackupPathPattern = "^/var/backups/kelpie(/[A-Za-z0-9_.-]+){1,6}(\\.tar|\\.tgz|\\.tar\\.gz)$";
    private const string FirewallActionPattern = "^(add|remove)$";
    private const string FirewallTargetPattern = "^(service|port)$";
    private const string FirewallRuleValuePattern = "^([A-Za-z0-9_.-]{1,64}|([1-9][0-9]{0,4})/(tcp|udp))$";
    private const string FirewallZonePattern = "^[A-Za-z0-9_.-]{1,64}$";
    private const string BooleanPattern = "^(true|false)$";
    private const string AuditLogPathPattern = "^/var/log/kelpie(/[A-Za-z0-9_.-]+){0,4}\\.log$";
    private const string DepthPattern = "^[0-5]$";
    private const string ProcessSortByPattern = "^(cpu|memory)$";
    private const string LoginShellPattern = "^/(bin|sbin|usr/bin|usr/sbin)/[A-Za-z0-9_.-]{1,64}$";
    private const string DaysPattern = "^(3650|36[0-4][0-9]|3[0-5][0-9]{2}|[1-2][0-9]{3}|[1-9][0-9]{0,2}|0)$";
    private const string LinesPattern = "^[0-9]{1,4}$";
    private const string LimitPattern = "^[0-9]{1,3}$";

    private static readonly AllowedCommandParameterDefinition ServiceParameter =
        new("service", Pattern: ServiceNamePattern);

    private const string UserApplyPermissionChangeScript = """
import base64, os, pwd, shutil, subprocess, sys
user = sys.argv[1]
shell = sys.argv[2]
login = sys.argv[3]
sudo = sys.argv[4]
u = next((x for x in pwd.getpwall() if x.pw_name == user), None)
if u is None:
    print('exists=false')
    raise SystemExit(2)
if not os.path.exists(shell):
    print('exists=true')
    print('shellExists=false')
    raise SystemExit(2)
visudo = shutil.which('visudo') or '/usr/sbin/visudo'
if sudo != 'unchanged' and not os.path.exists(visudo):
    print('exists=true')
    print('visudoExists=false')
    raise SystemExit(2)
managed_name = user.replace('$', '_dollar')
sudo_path = '/etc/sudoers.d/kelpie-' + managed_name
backup_dir = '/var/backups/kelpie/users'
os.makedirs(backup_dir, exist_ok=True)
backup_path = backup_dir + '/' + user + '-permissions-latest.meta'
locked = 'unknown'
passwd = shutil.which('passwd') or '/usr/bin/passwd'
status = subprocess.run([passwd, '-S', user], text=True, capture_output=True) if os.path.exists(passwd) else subprocess.CompletedProcess(args=[], returncode=127, stdout='', stderr='')
if status.returncode == 0:
    parts = status.stdout.split()
    locked = 'true' if len(parts) > 1 and parts[1] in ('L', 'LK', 'NP') else 'false'
elif os.path.isfile('/etc/shadow') and os.access('/etc/shadow', os.R_OK):
    for line in open('/etc/shadow', errors='replace'):
        if line.startswith(user + ':'):
            secret = line.split(':', 2)[1]
            locked = 'true' if secret.startswith('!') or secret.startswith('*') else 'false'
            break
sudo_exists = os.path.isfile(sudo_path)
sudo_payload = ''
if sudo_exists:
    sudo_payload = base64.b64encode(open(sudo_path, 'rb').read()).decode('ascii')
with open(backup_path, 'w') as backup:
    backup.write('shell=' + u.pw_shell + '\n')
    backup.write('locked=' + locked + '\n')
    backup.write('sudoExists=' + str(sudo_exists).lower() + '\n')
    backup.write('sudoBase64=' + sudo_payload + '\n')
changed_shell = False
changed_login = False
changed_sudo = False
result = subprocess.CompletedProcess(args=[], returncode=0, stdout='', stderr='')
if u.pw_shell != shell:
    result = subprocess.run(['usermod', '-s', shell, user], text=True, capture_output=True)
    changed_shell = result.returncode == 0
    if result.returncode != 0:
        print('user=' + user)
        print('changed=false')
        print('standardErrorSummary=' + (result.stderr.splitlines()[0][:120] if result.stderr.splitlines() else ''))
        raise SystemExit(result.returncode)
if login == 'disabled':
    result = subprocess.run(['usermod', '-L', user], text=True, capture_output=True)
    changed_login = result.returncode == 0
elif login == 'enabled':
    result = subprocess.run(['usermod', '-U', user], text=True, capture_output=True)
    changed_login = result.returncode == 0
if result.returncode != 0:
    print('user=' + user)
    print('changed=false')
    print('standardErrorSummary=' + (result.stderr.splitlines()[0][:120] if result.stderr.splitlines() else ''))
    raise SystemExit(result.returncode)
if sudo == 'present':
    content = user + ' ALL=(ALL) NOPASSWD:ALL\n'
    tmp = sudo_path + '.tmp'
    with open(tmp, 'w') as handle:
        handle.write(content)
    os.chmod(tmp, 0o440)
    check = subprocess.run([visudo, '-cf', tmp], text=True, capture_output=True)
    if check.returncode != 0:
        os.remove(tmp)
        print('user=' + user)
        print('changed=false')
        print('standardErrorSummary=' + (check.stderr.splitlines()[0][:120] if check.stderr.splitlines() else ''))
        raise SystemExit(check.returncode)
    os.replace(tmp, sudo_path)
    changed_sudo = True
elif sudo == 'absent':
    if os.path.exists(sudo_path):
        os.remove(sudo_path)
        changed_sudo = True
print('user=' + user)
print('shellChanged=' + str(changed_shell).lower())
print('loginChanged=' + str(changed_login).lower())
print('sudoChanged=' + str(changed_sudo).lower())
print('backupPath=' + backup_path)
print('rollbackConfirmation=user_rollback_permission_change:' + user)
print('standardErrorSummary=' + (result.stderr.splitlines()[0][:120] if result.stderr.splitlines() else ''))
""";

    private const string TargetInventoryScript = """
if [ ! -r /etc/os-release ]; then
    echo "ERROR	os-release not readable"
    exit 2
fi

NAME=""
VERSION_ID=""
ID=""
. /etc/os-release
printf 'OS	%s	%s	%s\n' "${NAME:-}" "${VERSION_ID:-}" "${ID:-}"

first_line() {
    printf '%s\n' "$1" | sed -n '/[^[:space:]]/{s/[	\r]/ /g;p;q;}'
}

run_item() {
    category="$1"
    name="$2"
    executable="$3"
    shift 3

    case "$executable" in
        /*)
            if [ ! -x "$executable" ]; then
                printf 'ITEM	%s	%s	%s	127	command not found\n' "$category" "$name" "$executable"
                return
            fi
            ;;
        *)
            if ! command -v "$executable" >/dev/null 2>&1; then
                printf 'ITEM	%s	%s	%s	127	command not found\n' "$category" "$name" "$executable"
                return
            fi
            ;;
    esac

    if command -v timeout >/dev/null 2>&1; then
        output=$(timeout 8 "$executable" "$@" 2>&1)
        code=$?
    else
        output=$("$executable" "$@" 2>&1)
        code=$?
    fi

    detail=$(first_line "$output")
    if [ -z "$detail" ]; then
        detail="exit code $code"
    fi

    printf 'ITEM	%s	%s	%s	%s	%s\n' "$category" "$name" "$executable" "$code" "$detail"
}

run_item helper Python python3 --version
run_item helper PHP php --version
run_item helper kelpie-web-permission-helper /usr/local/libexec/kelpie/kelpie-web-permission-helper --version
run_item software Node.js node --version
run_item software npm npm --version
run_item software Composer composer --version
run_item software Git git --version
run_item software curl curl --version
run_item software wget wget --version
run_item software OpenSSL openssl version
run_item software systemctl systemctl --version
run_item software journalctl journalctl --version
run_item software findmnt findmnt --version
run_item software ss ss --version
run_item software ip ip -Version
run_item software nginx nginx -v
run_item software firewall-cmd firewall-cmd --version
""";

    private const string CertificateExpiryCheckScript = """
path="$1"
days="$2"

if ! command -v openssl >/dev/null 2>&1; then
    echo "openssl command was not found" >&2
    exit 127
fi

seconds=$((days * 86400))
openssl x509 -in "$path" -noout -checkend "$seconds" -enddate
""";

    private const string UserListScript = """
limit="$1"

getent passwd | awk -F: -v limit="$limit" 'NR <= limit {
    print $1 ":" $3 ":" $4 ":" $6 ":" $7
}'
""";

    private const string UserInfoScript = """
user="$1"
row=$(getent passwd "$user")
if [ -z "$row" ]; then
    echo "user not found" >&2
    exit 2
fi

IFS=: read -r name _ uid gid _ home shell_path <<EOF
$row
EOF

primary=$(getent group "$gid" | awk -F: '{ print $1 }')
supplementary=$(getent group | awk -F: -v user="$user" '
{
    count = split($4, members, ",")
    for (i = 1; i <= count; i++) {
        if (members[i] == user) {
            print $1
        }
    }
}' | sort | paste -sd, -)

printf 'user=%s\n' "$name"
printf 'uid=%s\n' "$uid"
printf 'gid=%s\n' "$gid"
printf 'primaryGroup=%s\n' "$primary"
printf 'supplementaryGroups=%s\n' "$supplementary"
printf 'home=%s\n' "$home"
printf 'shell=%s\n' "$shell_path"
""";

    private const string GroupListScript = """
limit="$1"

getent group | awk -F: -v limit="$limit" 'NR <= limit {
    print $1 ":" $3 ":" $4
}'
""";

    private const string GroupInfoScript = """
group="$1"
row=$(getent group "$group")
if [ -z "$row" ]; then
    echo "group not found" >&2
    exit 2
fi

IFS=: read -r name _ gid members <<EOF
$row
EOF

printf 'group=%s\n' "$name"
printf 'gid=%s\n' "$gid"
printf 'members=%s\n' "$members"
""";

    private const string ServiceResidualConfigCheckScript = """
service="$1"
limit="$2"
base="$service"
case "$base" in
    *.service) base=${base%".service"} ;;
esac

printf 'service=%s\n' "$service"
printf 'baseName=%s\n' "$base"

count=0
print_path() {
    path="$1"
    if [ "$count" -ge "$limit" ]; then
        return
    fi

    if [ -d "$path" ]; then
        type="dir"
    elif [ -f "$path" ]; then
        type="file"
    elif [ -e "$path" ]; then
        type="other"
    else
        type="other"
    fi

    if [ -e "$path" ]; then
        exists="true"
    else
        exists="false"
    fi

    printf '%s:exists=%s:type=%s\n' "$path" "$exists" "$type"
    count=$((count + 1))
}

for path in \
    "/etc/systemd/system/$service" \
    "/usr/lib/systemd/system/$service" \
    "/lib/systemd/system/$service" \
    "/etc/$base" \
    "/etc/$base.conf" \
    "/var/lib/$base" \
    "/var/log/$base" \
    "/run/$base"
do
    print_path "$path"
done

for extra in /etc/"$base".d/*; do
    [ -e "$extra" ] || continue
    print_path "$extra"
done

printf 'pathsChecked=%s\n' "$count"
""";

    private const string SupportReportCollectScript = """
limit="$1"

printf 'reportVersion=1\n'
printf 'kernel=%s\n' "$(uname -srm 2>/dev/null)"

if [ -r /etc/os-release ]; then
    while IFS='=' read -r key value; do
        case "$key" in
            ID|NAME|VERSION_ID)
                value=${value#\"}
                value=${value%\"}
                printf 'osRelease.%s=%s\n' "$key" "$value"
                ;;
        esac
    done < /etc/os-release
fi

uptime_output=$(uptime 2>&1)
uptime_code=$?
printf 'uptimeExitCode=%s\n' "$uptime_code"
printf 'uptimeSummary=%s\n' "$uptime_output"

free_output=$(free -m 2>&1)
free_code=$?
printf 'memoryExitCode=%s\n' "$free_code"
printf '%s\n' "$free_output" | sed -n '1,3{s/^/memory=/;p;}'

df_output=$(df -h --output=fstype,size,used,avail,pcent,target 2>&1)
df_code=$?
printf 'diskExitCode=%s\n' "$df_code"
printf 'diskRows=%s\n' "$(printf '%s\n' "$df_output" | awk -v limit="$limit" 'NR > 1 && NR <= limit + 1 { count++ } END { print count + 0 }')"
printf '%s\n' "$df_output" | awk -v limit="$limit" 'NR <= limit + 1 { print "disk=" $0 }'

if command -v systemctl >/dev/null 2>&1; then
    failed_output=$(systemctl --failed --no-pager --plain --no-legend 2>/dev/null)
else
    failed_output=""
fi

printf 'failedServices=%s\n' "$(printf '%s\n' "$failed_output" | awk 'NF { count++ } END { print count + 0 }')"
printf '%s\n' "$failed_output" | awk -v limit="$limit" 'NF && count < limit { print "failedService=" $1; count++ }'
""";

    private const string FirewallStatusScript = """
if command -v firewall-cmd >/dev/null 2>&1; then
    firewalld_available="true"
    firewalld_state=$(firewall-cmd --state 2>/dev/null)
    firewalld_zone=$(firewall-cmd --get-default-zone 2>/dev/null)
    firewalld_services=$(firewall-cmd --list-services 2>/dev/null)
else
    firewalld_available="false"
    firewalld_state="unavailable"
    firewalld_zone=""
    firewalld_services=""
fi

if command -v ufw >/dev/null 2>&1; then
    ufw_available="true"
    ufw_output=$(ufw status 2>/dev/null)
else
    ufw_available="false"
    ufw_output=""
fi

printf 'firewalldAvailable=%s\n' "$firewalld_available"
printf 'ufwAvailable=%s\n' "$ufw_available"
printf 'firewalldState=%s\n' "$firewalld_state"
printf 'firewalldDefaultZone=%s\n' "$firewalld_zone"
printf 'firewalldServiceCount=%s\n' "$(printf '%s\n' "$firewalld_services" | awk '{ count += NF } END { print count + 0 }')"
printf 'ufwStatusLineCount=%s\n' "$(printf '%s\n' "$ufw_output" | awk 'NF { count++ } END { print count + 0 }')"
""";

    private const string BackupVerifyScript = """
path="$1"

printf 'backupPath=%s\n' "$path"
if [ ! -f "$path" ]; then
    printf 'exists=false\n'
    printf 'size=0\n'
    printf 'archiveReadable=false\n'
    printf 'verifyExitCode=2\n'
    printf 'standardErrorSummary=\n'
    exit 2
fi

printf 'exists=true\n'
printf 'size=%s\n' "$(wc -c < "$path" | tr -d ' ')"
tar_error=$(tar -tf "$path" 2>&1 >/dev/null)
tar_code=$?
if [ "$tar_code" -eq 0 ]; then
    readable="true"
else
    readable="false"
fi

printf 'archiveReadable=%s\n' "$readable"
printf 'verifyExitCode=%s\n' "$tar_code"
printf 'standardErrorSummary=%s\n' "$(printf '%s\n' "$tar_error" | sed -n '1{s/^.\{120\}/&/;s/^\(.\{120\}\).*/\1/;p;}')"
exit "$tar_code"
""";

    private static readonly AllowedCommandDefinition[] Commands =
    [
        new("get_system_info", "uname -a", TimeSpan.FromSeconds(10)),
        new("get_os_release", "cat /etc/os-release", TimeSpan.FromSeconds(10)),
        new("target_inventory", CreateEncodedShellCommand(TargetInventoryScript), TimeSpan.FromSeconds(120)),
        new("get_uptime", "uptime", TimeSpan.FromSeconds(10)),
        new("get_disk_usage", "df -h", TimeSpan.FromSeconds(15)),
        new("get_memory_usage", "free -m", TimeSpan.FromSeconds(10)),
        new(
            "get_process_summary",
            "sh -c 'limit=\"$1\"; sort_by=\"$2\"; case \"$sort_by\" in cpu) sort_key=\"-%cpu\";; memory) sort_key=\"-%mem\";; *) echo \"invalid sortBy\" >&2; exit 2;; esac; ps -eo pid,ppid,user,comm,%cpu,%mem --sort=\"$sort_key\" | head -n \"$((limit + 1))\"' sh {limit} {sortBy}",
            TimeSpan.FromSeconds(20),
            [
                new AllowedCommandParameterDefinition("sortBy", Pattern: ProcessSortByPattern),
                new AllowedCommandParameterDefinition("limit", Pattern: LimitPattern),
            ]),
        new("get_inode_usage", "df -ih", TimeSpan.FromSeconds(15)),
        new("get_mounts", "findmnt -rno TARGET,SOURCE,FSTYPE,OPTIONS", TimeSpan.FromSeconds(15)),
        new("get_network_addresses", "ip addr show", TimeSpan.FromSeconds(15)),
        new("get_routes", "ip route show", TimeSpan.FromSeconds(15)),
        new("get_dns_config", "cat /etc/resolv.conf", TimeSpan.FromSeconds(10)),
        new(
            "cron_list",
            "python3 -c \"import glob,os,subprocess,sys; limit=int({limit}); rows=[]; files=[p for p in ['/etc/crontab']+sorted(glob.glob('/etc/cron.d/*')) if os.path.isfile(p)]; [rows.append(path+':'+line.strip()) for path in files for line in open(path, errors='replace') if line.strip() and not line.lstrip().startswith('#')]; result=subprocess.run(['crontab','-l'], text=True, capture_output=True); [rows.append('user-crontab:'+line.strip()) for line in result.stdout.splitlines() if line.strip() and not line.lstrip().startswith('#')]; print('\\n'.join(rows[:limit])); print(result.stderr, end='', file=sys.stderr) if result.returncode not in (0,1) else None\"",
            TimeSpan.FromSeconds(20),
            [
                new AllowedCommandParameterDefinition("limit", Pattern: BoundedListLimitPattern),
            ]),
        new(
            "cron_validate",
            "python3 -c \"import re,sys; expr={cronExpression}; run_user={runUser}; command={command}; log_path={logPath}; parts=expr.split(); base_user=run_user[:-1] if run_user.endswith('$') else run_user; cron_ok=len(parts)==5 and all(re.match('^[0-9A-Za-z*/?,#LW.-]+$', part) for part in parts); user_ok=len(run_user)<=32 and bool(re.match('^[a-z_][a-z0-9_-]*$', base_user)); command_ok=bool(command.strip()); log_ok=log_path.startswith('/var/log/') and '..' not in log_path; ok=cron_ok and user_ok and command_ok and log_ok; print('valid=' + str(ok).lower()); print('cronExpression=' + expr); print('runUser=' + run_user); print('logPath=' + log_path); raise SystemExit(0 if ok else 1)\"",
            TimeSpan.FromSeconds(10),
            [
                new AllowedCommandParameterDefinition("cronExpression", MaxLength: 128, Pattern: CronExpressionPattern),
                new AllowedCommandParameterDefinition("runUser", Pattern: UserNamePattern),
                new AllowedCommandParameterDefinition("command", MaxLength: 128, Pattern: ShellCommandTextPattern),
                new AllowedCommandParameterDefinition("logPath", MaxLength: 180, Pattern: CronLogPathPattern),
            ]),
        new(
            "cron_check_write",
            "python3 -c \"import pwd,sys; target={targetType}; run_user={runUser}; expr={cronExpression}; command={command}; log_path={logPath}; users=[u.pw_name for u in pwd.getpwall()]; exists=run_user in users; target_path='user-crontab' if target=='user' else '/etc/cron.d/kelpie-managed'; print('targetType=' + target); print('target=' + target_path); print('runUser=' + run_user); print('userExists=' + str(exists).lower()); print('cronExpression=' + expr); print('logPath=' + log_path); print('requiresConfirmation=true'); print('confirmation=cron_write:' + target + ':' + run_user); print('rollbackSupported=true'); raise SystemExit(0 if exists else 2)\"",
            TimeSpan.FromSeconds(10),
            [
                new AllowedCommandParameterDefinition("targetType", Pattern: CronTargetTypePattern),
                new AllowedCommandParameterDefinition("runUser", Pattern: UserNamePattern),
                new AllowedCommandParameterDefinition("cronExpression", MaxLength: 128, Pattern: CronExpressionPattern),
                new AllowedCommandParameterDefinition("command", MaxLength: 128, Pattern: ShellCommandTextPattern),
                new AllowedCommandParameterDefinition("logPath", MaxLength: 180, Pattern: CronLogPathPattern),
            ]),
        new(
            "cron_write",
            "sudo -n python3 -c \"import base64,sys; exec(base64.b64decode('aW1wb3J0IG9zLCBzdWJwcm9jZXNzLCBzeXMKdGFyZ2V0PXN5cy5hcmd2WzFdCnJ1bl91c2VyPXN5cy5hcmd2WzJdCmV4cHI9c3lzLmFyZ3ZbM10KY29tbWFuZD1zeXMuYXJndls0XQpsb2dfcGF0aD1zeXMuYXJndls1XQpiYWNrdXBfZGlyPScvdmFyL2JhY2t1cHMva2VscGllL2Nyb24nCm9zLm1ha2VkaXJzKGJhY2t1cF9kaXIsIGV4aXN0X29rPVRydWUpCm1hcmtlcj0nIyBrZWxwaWUtbWFuYWdlZDonICsgdGFyZ2V0ICsgJzonICsgcnVuX3VzZXIKaWYgdGFyZ2V0ID09ICd1c2VyJzoKICAgIGxpc3RfcmVzdWx0PXN1YnByb2Nlc3MucnVuKFsnY3JvbnRhYicsJy11JyxydW5fdXNlciwnLWwnXSwgdGV4dD1UcnVlLCBjYXB0dXJlX291dHB1dD1UcnVlKQogICAgZXhpc3RlZD1saXN0X3Jlc3VsdC5yZXR1cm5jb2RlID09IDAKICAgIGN1cnJlbnQ9bGlzdF9yZXN1bHQuc3Rkb3V0IGlmIGV4aXN0ZWQgZWxzZSAnJwogICAgYmFja3VwX3BhdGg9YmFja3VwX2RpciArICcvdXNlci0nICsgcnVuX3VzZXIgKyAnLWxhdGVzdC5jcm9uJwogICAgdGFyZ2V0X3BhdGg9J3VzZXItY3JvbnRhYicKZWxzZToKICAgIHRhcmdldF9wYXRoPScvZXRjL2Nyb24uZC9rZWxwaWUtbWFuYWdlZCcKICAgIGV4aXN0ZWQ9b3MucGF0aC5leGlzdHModGFyZ2V0X3BhdGgpCiAgICBjdXJyZW50PW9wZW4odGFyZ2V0X3BhdGgsIGVycm9ycz0ncmVwbGFjZScpLnJlYWQoKSBpZiBleGlzdGVkIGVsc2UgJycKICAgIGJhY2t1cF9wYXRoPWJhY2t1cF9kaXIgKyAnL3N5c3RlbS0nICsgcnVuX3VzZXIgKyAnLWxhdGVzdC5jcm9uJwpvcGVuKGJhY2t1cF9wYXRoLCAndycpLndyaXRlKGN1cnJlbnQpCm9wZW4oYmFja3VwX3BhdGggKyAnLm1ldGEnLCAndycpLndyaXRlKCdleGlzdGVkPScgKyBzdHIoZXhpc3RlZCkubG93ZXIoKSArICdcbicpCmxpbmVzPVtsaW5lIGZvciBsaW5lIGluIGN1cnJlbnQuc3BsaXRsaW5lcygpIGlmIG1hcmtlciBub3QgaW4gbGluZV0KaWYgdGFyZ2V0ID09ICd1c2VyJzoKICAgIGxpbmVzLmFwcGVuZChleHByICsgJyAnICsgY29tbWFuZCArICcgPj4gJyArIGxvZ19wYXRoICsgJyAyPiYxICcgKyBtYXJrZXIpCiAgICBwYXlsb2FkPSdcbicuam9pbihsaW5lcykucnN0cmlwKCkgKyAnXG4nCiAgICByZXN1bHQ9c3VicHJvY2Vzcy5ydW4oWydjcm9udGFiJywnLXUnLHJ1bl91c2VyLCctJ10sIGlucHV0PXBheWxvYWQsIHRleHQ9VHJ1ZSwgY2FwdHVyZV9vdXRwdXQ9VHJ1ZSkKZWxzZToKICAgIGxpbmVzLmFwcGVuZChleHByICsgJyAnICsgcnVuX3VzZXIgKyAnICcgKyBjb21tYW5kICsgJyA+PiAnICsgbG9nX3BhdGggKyAnIDI+JjEgJyArIG1hcmtlcikKICAgIHBheWxvYWQ9J1xuJy5qb2luKGxpbmVzKS5yc3RyaXAoKSArICdcbicKICAgIG9wZW4odGFyZ2V0X3BhdGgsICd3Jykud3JpdGUocGF5bG9hZCkKICAgIG9zLmNobW9kKHRhcmdldF9wYXRoLCAwbzY0NCkKICAgIHJlc3VsdD1zdWJwcm9jZXNzLkNvbXBsZXRlZFByb2Nlc3MoYXJncz1bXSwgcmV0dXJuY29kZT0wLCBzdGRvdXQ9JycsIHN0ZGVycj0nJykKcHJpbnQoJ3RhcmdldFR5cGU9JyArIHRhcmdldCkKcHJpbnQoJ3RhcmdldD0nICsgdGFyZ2V0X3BhdGgpCnByaW50KCdydW5Vc2VyPScgKyBydW5fdXNlcikKcHJpbnQoJ2NoYW5nZWQ9JyArIHN0cihyZXN1bHQucmV0dXJuY29kZSA9PSAwKS5sb3dlcigpKQpwcmludCgnYmFja3VwUGF0aD0nICsgYmFja3VwX3BhdGgpCnByaW50KCdyb2xsYmFja0NvbmZpcm1hdGlvbj1jcm9uX3JvbGxiYWNrOicgKyB0YXJnZXQgKyAnOicgKyBydW5fdXNlcikKcHJpbnQoJ3N0YW5kYXJkRXJyb3JTdW1tYXJ5PScgKyAocmVzdWx0LnN0ZGVyci5zcGxpdGxpbmVzKClbMF1bOjEyMF0gaWYgcmVzdWx0LnN0ZGVyci5zcGxpdGxpbmVzKCkgZWxzZSAnJykpCnJhaXNlIFN5c3RlbUV4aXQocmVzdWx0LnJldHVybmNvZGUp'))\" {targetType} {runUser} {cronExpression} {command} {logPath}",
            TimeSpan.FromSeconds(30),
            [
                new AllowedCommandParameterDefinition("targetType", Pattern: CronTargetTypePattern),
                new AllowedCommandParameterDefinition("runUser", Pattern: UserNamePattern),
                new AllowedCommandParameterDefinition("cronExpression", MaxLength: 128, Pattern: CronExpressionPattern),
                new AllowedCommandParameterDefinition("command", MaxLength: 128, Pattern: ShellCommandTextPattern),
                new AllowedCommandParameterDefinition("logPath", MaxLength: 180, Pattern: CronLogPathPattern),
            ],
            SshCommandRiskLevel.ConfirmRequired),
        new(
            "cron_rollback",
            "sudo -n python3 -c \"import base64,sys; exec(base64.b64decode('aW1wb3J0IG9zLCBzdWJwcm9jZXNzLCBzeXMKdGFyZ2V0PXN5cy5hcmd2WzFdCnJ1bl91c2VyPXN5cy5hcmd2WzJdCmJhY2t1cF9kaXI9Jy92YXIvYmFja3Vwcy9rZWxwaWUvY3JvbicKYmFja3VwX3BhdGg9YmFja3VwX2RpciArICgnL3VzZXItJyArIHJ1bl91c2VyICsgJy1sYXRlc3QuY3JvbicgaWYgdGFyZ2V0ID09ICd1c2VyJyBlbHNlICcvc3lzdGVtLScgKyBydW5fdXNlciArICctbGF0ZXN0LmNyb24nKQptZXRhX3BhdGg9YmFja3VwX3BhdGggKyAnLm1ldGEnCmlmIG5vdCBvcy5wYXRoLmlzZmlsZShiYWNrdXBfcGF0aCkgb3Igbm90IG9zLnBhdGguaXNmaWxlKG1ldGFfcGF0aCk6CiAgICBwcmludCgnYmFja3VwUGF0aD0nICsgYmFja3VwX3BhdGgpCiAgICBwcmludCgnYmFja3VwRXhpc3RzPWZhbHNlJykKICAgIHJhaXNlIFN5c3RlbUV4aXQoMikKbWV0YT1vcGVuKG1ldGFfcGF0aCwgZXJyb3JzPSdyZXBsYWNlJykucmVhZCgpCmV4aXN0ZWQ9J2V4aXN0ZWQ9dHJ1ZScgaW4gbWV0YQpwYXlsb2FkPW9wZW4oYmFja3VwX3BhdGgsIGVycm9ycz0ncmVwbGFjZScpLnJlYWQoKQppZiB0YXJnZXQgPT0gJ3VzZXInOgogICAgaWYgZXhpc3RlZDoKICAgICAgICByZXN1bHQ9c3VicHJvY2Vzcy5ydW4oWydjcm9udGFiJywnLXUnLHJ1bl91c2VyLCctJ10sIGlucHV0PXBheWxvYWQsIHRleHQ9VHJ1ZSwgY2FwdHVyZV9vdXRwdXQ9VHJ1ZSkKICAgIGVsc2U6CiAgICAgICAgcmVzdWx0PXN1YnByb2Nlc3MucnVuKFsnY3JvbnRhYicsJy11JyxydW5fdXNlciwnLXInXSwgdGV4dD1UcnVlLCBjYXB0dXJlX291dHB1dD1UcnVlKQogICAgdGFyZ2V0X3BhdGg9J3VzZXItY3JvbnRhYicKZWxzZToKICAgIHRhcmdldF9wYXRoPScvZXRjL2Nyb24uZC9rZWxwaWUtbWFuYWdlZCcKICAgIGlmIGV4aXN0ZWQ6CiAgICAgICAgb3Blbih0YXJnZXRfcGF0aCwgJ3cnKS53cml0ZShwYXlsb2FkKQogICAgICAgIG9zLmNobW9kKHRhcmdldF9wYXRoLCAwbzY0NCkKICAgIGVsaWYgb3MucGF0aC5leGlzdHModGFyZ2V0X3BhdGgpOgogICAgICAgIG9zLnJlbW92ZSh0YXJnZXRfcGF0aCkKICAgIHJlc3VsdD1zdWJwcm9jZXNzLkNvbXBsZXRlZFByb2Nlc3MoYXJncz1bXSwgcmV0dXJuY29kZT0wLCBzdGRvdXQ9JycsIHN0ZGVycj0nJykKcHJpbnQoJ3RhcmdldFR5cGU9JyArIHRhcmdldCkKcHJpbnQoJ3RhcmdldD0nICsgdGFyZ2V0X3BhdGgpCnByaW50KCdydW5Vc2VyPScgKyBydW5fdXNlcikKcHJpbnQoJ2JhY2t1cEV4aXN0cz10cnVlJykKcHJpbnQoJ3Jlc3RvcmVkPScgKyBzdHIocmVzdWx0LnJldHVybmNvZGUgPT0gMCkubG93ZXIoKSkKcHJpbnQoJ3N0YW5kYXJkRXJyb3JTdW1tYXJ5PScgKyAocmVzdWx0LnN0ZGVyci5zcGxpdGxpbmVzKClbMF1bOjEyMF0gaWYgcmVzdWx0LnN0ZGVyci5zcGxpdGxpbmVzKCkgZWxzZSAnJykpCnJhaXNlIFN5c3RlbUV4aXQocmVzdWx0LnJldHVybmNvZGUp'))\" {targetType} {runUser}",
            TimeSpan.FromSeconds(30),
            [
                new AllowedCommandParameterDefinition("targetType", Pattern: CronTargetTypePattern),
                new AllowedCommandParameterDefinition("runUser", Pattern: UserNamePattern),
            ],
            SshCommandRiskLevel.ConfirmRequired),
        new(
            "cert_inspect",
            "openssl x509 -in {path} -noout -issuer -subject -dates -ext subjectAltName",
            TimeSpan.FromSeconds(10),
            [
                new AllowedCommandParameterDefinition("path", MaxLength: 256, Pattern: CertificatePathPattern),
            ]),
        new(
            "cert_expiry_check",
            CreateEncodedShellCommand(CertificateExpiryCheckScript, "{path} {days}"),
            TimeSpan.FromSeconds(10),
            [
                new AllowedCommandParameterDefinition("path", MaxLength: 256, Pattern: CertificatePathPattern),
                new AllowedCommandParameterDefinition("days", Pattern: DaysPattern),
            ]),
        new(
            "user_list",
            CreateEncodedShellCommand(UserListScript, "{limit}"),
            TimeSpan.FromSeconds(10),
            [
                new AllowedCommandParameterDefinition("limit", Pattern: BoundedListLimitPattern),
            ]),
        new(
            "user_info",
            CreateEncodedShellCommand(UserInfoScript, "{user}"),
            TimeSpan.FromSeconds(10),
            [
                new AllowedCommandParameterDefinition("user", Pattern: UserNamePattern),
            ]),
        new(
            "group_list",
            CreateEncodedShellCommand(GroupListScript, "{limit}"),
            TimeSpan.FromSeconds(10),
            [
                new AllowedCommandParameterDefinition("limit", Pattern: BoundedListLimitPattern),
            ]),
        new(
            "group_info",
            CreateEncodedShellCommand(GroupInfoScript, "{group}"),
            TimeSpan.FromSeconds(10),
            [
                new AllowedCommandParameterDefinition("group", Pattern: GroupNamePattern),
            ]),
        new(
            "sudoers_check",
            "python3 -c \"import glob,grp,os,pwd,re; kind={targetType}; name={name}; users=pwd.getpwall(); groups=grp.getgrall(); user=next((u for u in users if u.pw_name==name), None); group_names=[g.gr_name for g in groups]; exists=(user is not None) if kind=='user' else (name in group_names); member_groups=sorted(g.gr_name for g in groups if kind=='user' and (name in g.gr_mem or (user is not None and g.gr_gid==user.pw_gid))); admin=set(['wheel','sudo','admin']); admin_hits=sorted((set(member_groups) if kind=='user' else set([name])) & admin); files=[p for p in ['/etc/sudoers']+sorted(glob.glob('/etc/sudoers.d/*')) if os.path.isfile(p)]; readable=[p for p in files if os.access(p, os.R_OK)]; token=('%'+name) if kind=='group' else name; pattern=re.compile(r'(^|[\\s,])'+re.escape(token)+r'([\\s,=]|$)'); rows=[(p,line.strip()) for p in readable for line in open(p, errors='replace') if line.strip() and not line.lstrip().startswith('#')]; match_paths=sorted(set(p for p,line in rows if pattern.search(line))); print('principalType=' + kind); print('name=' + name); print('exists=' + str(exists).lower()); print('adminGroups=' + ','.join(admin_hits)); print('sudoersFilesReadable=' + str(len(readable))); print('sudoersMatches=' + str(len(match_paths))); print('sudoersMatchSources=' + ','.join(match_paths))\"",
            TimeSpan.FromSeconds(10),
            [
                new AllowedCommandParameterDefinition("targetType", Pattern: PrincipalTypePattern),
                new AllowedCommandParameterDefinition("name", Pattern: GroupNamePattern),
            ]),
        new(
            "user_service_usage_check",
            "python3 -c \"import subprocess,sys; kind={targetType}; name={name}; limit=int({limit}); result=subprocess.run(['systemctl','list-units','--type=service','--all','--no-legend','--plain'], text=True, capture_output=True); units=[line.split()[0] for line in result.stdout.splitlines() if line.strip()][:limit]; matches=[]; fields=['User','Group','SupplementaryGroups']; [matches.append(unit+':'+field+'='+value) for unit in units for field in fields for value in [subprocess.run(['systemctl','show',unit,'-p',field,'--value'], text=True, capture_output=True).stdout.strip()] if (kind=='user' and field=='User' and value==name) or (kind=='group' and field in ('Group','SupplementaryGroups') and name in [part for part in value.replace(',', ' ').split() if part])]; print('principalType=' + kind); print('name=' + name); print('unitsChecked=' + str(len(units))); print('matches=' + str(len(matches))); print('\\n'.join(matches[:limit])); print(result.stderr, end='', file=sys.stderr) if result.returncode not in (0,1) else None\"",
            TimeSpan.FromSeconds(30),
            [
                new AllowedCommandParameterDefinition("targetType", Pattern: PrincipalTypePattern),
                new AllowedCommandParameterDefinition("name", Pattern: GroupNamePattern),
                new AllowedCommandParameterDefinition("limit", Pattern: BoundedListLimitPattern),
            ]),
        new(
            "user_file_ownership_check",
            "python3 -c \"import base64,sys; exec(base64.b64decode('aW1wb3J0IGdycCwgb3MsIHB3ZCwgc3lzCmtpbmQgPSBzeXMuYXJndlsxXQpuYW1lID0gc3lzLmFyZ3ZbMl0Kcm9vdCA9IG9zLnBhdGguYWJzcGF0aChzeXMuYXJndlszXSkKZGVwdGggPSBpbnQoc3lzLmFyZ3ZbNF0pCmxpbWl0ID0gaW50KHN5cy5hcmd2WzVdKQp1c2VycyA9IHB3ZC5nZXRwd2FsbCgpCmdyb3VwcyA9IGdycC5nZXRncmFsbCgpCnRhcmdldF91aWQgPSBuZXh0KCh1LnB3X3VpZCBmb3IgdSBpbiB1c2VycyBpZiB1LnB3X25hbWUgPT0gbmFtZSksIE5vbmUpCnRhcmdldF9naWQgPSBuZXh0KChnLmdyX2dpZCBmb3IgZyBpbiBncm91cHMgaWYgZy5ncl9uYW1lID09IG5hbWUpLCBOb25lKQpwcmludCgnc2NhblJvb3Q9JyArIHJvb3QpCnByaW50KCdwcmluY2lwYWxUeXBlPScgKyBraW5kKQpwcmludCgnbmFtZT0nICsgbmFtZSkKcHJpbnQoJ2RlcHRoPScgKyBzdHIoZGVwdGgpKQppZiAoa2luZCA9PSAndXNlcicgYW5kIHRhcmdldF91aWQgaXMgTm9uZSkgb3IgKGtpbmQgPT0gJ2dyb3VwJyBhbmQgdGFyZ2V0X2dpZCBpcyBOb25lKToKICAgIHByaW50KCdwcmluY2lwYWwgbm90IGZvdW5kJywgZmlsZT1zeXMuc3RkZXJyKQogICAgcmFpc2UgU3lzdGVtRXhpdCgyKQpzdGFydF9kZXB0aCA9IHJvb3QucnN0cmlwKCcvJykuY291bnQoJy8nKQptYXRjaGVzID0gW10Kc2Nhbm5lZCA9IDAKaWYgb3MucGF0aC5pc2Rpcihyb290KToKICAgIGZvciBjdXJyZW50LCBkaXJzLCBmaWxlcyBpbiBvcy53YWxrKHJvb3QsIHRvcGRvd249VHJ1ZSwgZm9sbG93bGlua3M9RmFsc2UpOgogICAgICAgIGN1cnJlbnRfZGVwdGggPSBjdXJyZW50LnJzdHJpcCgnLycpLmNvdW50KCcvJykgLSBzdGFydF9kZXB0aAogICAgICAgIGlmIGN1cnJlbnRfZGVwdGggPj0gZGVwdGg6CiAgICAgICAgICAgIGRpcnNbOl0gPSBbXQogICAgICAgIGVudHJpZXMgPSBbY3VycmVudF0gKyBbb3MucGF0aC5qb2luKGN1cnJlbnQsIGVudHJ5KSBmb3IgZW50cnkgaW4gZGlycyArIGZpbGVzXQogICAgICAgIGZvciBwYXRoIGluIGVudHJpZXM6CiAgICAgICAgICAgIGlmIHNjYW5uZWQgPj0gbGltaXQgKiAyMCBvciBsZW4obWF0Y2hlcykgPj0gbGltaXQ6CiAgICAgICAgICAgICAgICBicmVhawogICAgICAgICAgICB0cnk6CiAgICAgICAgICAgICAgICBzdCA9IG9zLmxzdGF0KHBhdGgpCiAgICAgICAgICAgIGV4Y2VwdCBPU0Vycm9yOgogICAgICAgICAgICAgICAgY29udGludWUKICAgICAgICAgICAgc2Nhbm5lZCArPSAxCiAgICAgICAgICAgIGlmIChraW5kID09ICd1c2VyJyBhbmQgc3Quc3RfdWlkID09IHRhcmdldF91aWQpIG9yIChraW5kID09ICdncm91cCcgYW5kIHN0LnN0X2dpZCA9PSB0YXJnZXRfZ2lkKToKICAgICAgICAgICAgICAgIG93bmVyID0gbmV4dCgodS5wd19uYW1lIGZvciB1IGluIHVzZXJzIGlmIHUucHdfdWlkID09IHN0LnN0X3VpZCksIHN0cihzdC5zdF91aWQpKQogICAgICAgICAgICAgICAgZ3JvdXAgPSBuZXh0KChnLmdyX25hbWUgZm9yIGcgaW4gZ3JvdXBzIGlmIGcuZ3JfZ2lkID09IHN0LnN0X2dpZCksIHN0cihzdC5zdF9naWQpKQogICAgICAgICAgICAgICAgbWF0Y2hlcy5hcHBlbmQocGF0aCArICc6b3duZXI9JyArIG93bmVyICsgJzpncm91cD0nICsgZ3JvdXApCiAgICAgICAgaWYgbGVuKG1hdGNoZXMpID49IGxpbWl0OgogICAgICAgICAgICBicmVhawpwcmludCgnZW50cmllc1NjYW5uZWQ9JyArIHN0cihzY2FubmVkKSkKcHJpbnQoJ21hdGNoZXM9JyArIHN0cihsZW4obWF0Y2hlcykpKQpwcmludCgnXG4nLmpvaW4obWF0Y2hlc1s6bGltaXRdKSk='))\" {targetType} {name} {scanRoot} {depth} {limit}",
            TimeSpan.FromSeconds(30),
            [
                new AllowedCommandParameterDefinition("targetType", Pattern: PrincipalTypePattern),
                new AllowedCommandParameterDefinition("name", Pattern: GroupNamePattern),
                new AllowedCommandParameterDefinition("scanRoot", MaxLength: 128, Pattern: OwnershipScanRootPattern),
                new AllowedCommandParameterDefinition("depth", Pattern: DepthPattern),
                new AllowedCommandParameterDefinition("limit", Pattern: BoundedListLimitPattern),
            ]),
        new(
            "user_usage_check",
            "python3 -c \"import base64,sys; exec(base64.b64decode('aW1wb3J0IGdsb2IsIGdycCwgb3MsIHB3ZCwgc3VicHJvY2Vzcywgc3lzCmtpbmQgPSBzeXMuYXJndlsxXQpuYW1lID0gc3lzLmFyZ3ZbMl0KbGltaXQgPSBpbnQoc3lzLmFyZ3ZbM10pCnVzZXJzID0gcHdkLmdldHB3YWxsKCkKZ3JvdXBzID0gZ3JwLmdldGdyYWxsKCkKdXNlciA9IG5leHQoKHUgZm9yIHUgaW4gdXNlcnMgaWYgdS5wd19uYW1lID09IG5hbWUpLCBOb25lKQpncm91cCA9IG5leHQoKGcgZm9yIGcgaW4gZ3JvdXBzIGlmIGcuZ3JfbmFtZSA9PSBuYW1lKSwgTm9uZSkKZXhpc3RzID0gKHVzZXIgaXMgbm90IE5vbmUpIGlmIGtpbmQgPT0gJ3VzZXInIGVsc2UgKGdyb3VwIGlzIG5vdCBOb25lKQpwcmludCgncHJpbmNpcGFsVHlwZT0nICsga2luZCkKcHJpbnQoJ25hbWU9JyArIG5hbWUpCnByaW50KCdleGlzdHM9JyArIHN0cihleGlzdHMpLmxvd2VyKCkpCnNlcnZpY2UgPSBzdWJwcm9jZXNzLnJ1bihbJ3N5c3RlbWN0bCcsICdsaXN0LXVuaXRzJywgJy0tdHlwZT1zZXJ2aWNlJywgJy0tYWxsJywgJy0tbm8tbGVnZW5kJywgJy0tcGxhaW4nXSwgdGV4dD1UcnVlLCBjYXB0dXJlX291dHB1dD1UcnVlKQp1bml0cyA9IFtsaW5lLnNwbGl0KClbMF0gZm9yIGxpbmUgaW4gc2VydmljZS5zdGRvdXQuc3BsaXRsaW5lcygpIGlmIGxpbmUuc3RyaXAoKV1bOmxpbWl0XQpzZXJ2aWNlX21hdGNoZXMgPSBbXQpmaWVsZHMgPSBbJ1VzZXInLCAnR3JvdXAnLCAnU3VwcGxlbWVudGFyeUdyb3VwcyddCmZvciB1bml0IGluIHVuaXRzOgogICAgZm9yIGZpZWxkIGluIGZpZWxkczoKICAgICAgICB2YWx1ZSA9IHN1YnByb2Nlc3MucnVuKFsnc3lzdGVtY3RsJywgJ3Nob3cnLCB1bml0LCAnLXAnLCBmaWVsZCwgJy0tdmFsdWUnXSwgdGV4dD1UcnVlLCBjYXB0dXJlX291dHB1dD1UcnVlKS5zdGRvdXQuc3RyaXAoKQogICAgICAgIHBhcnRzID0gW3BhcnQgZm9yIHBhcnQgaW4gdmFsdWUucmVwbGFjZSgnLCcsICcgJykuc3BsaXQoKSBpZiBwYXJ0XQogICAgICAgIGlmIChraW5kID09ICd1c2VyJyBhbmQgZmllbGQgPT0gJ1VzZXInIGFuZCB2YWx1ZSA9PSBuYW1lKSBvciAoa2luZCA9PSAnZ3JvdXAnIGFuZCBmaWVsZCBpbiAoJ0dyb3VwJywgJ1N1cHBsZW1lbnRhcnlHcm91cHMnKSBhbmQgbmFtZSBpbiBwYXJ0cyk6CiAgICAgICAgICAgIHNlcnZpY2VfbWF0Y2hlcy5hcHBlbmQodW5pdCArICc6JyArIGZpZWxkKQpjcm9uX2ZpbGVzID0gW3AgZm9yIHAgaW4gWycvZXRjL2Nyb250YWInXSArIHNvcnRlZChnbG9iLmdsb2IoJy9ldGMvY3Jvbi5kLyonKSkgaWYgb3MucGF0aC5pc2ZpbGUocCldCmNyb25fbWF0Y2hlcyA9IFtdCmZvciBwYXRoIGluIGNyb25fZmlsZXM6CiAgICBmb3IgbGluZSBpbiBvcGVuKHBhdGgsIGVycm9ycz0ncmVwbGFjZScpOgogICAgICAgIHBhcnRzID0gbGluZS5zcGxpdCgpCiAgICAgICAgaWYga2luZCA9PSAndXNlcicgYW5kIHBhcnRzIGFuZCBub3QgbGluZS5sc3RyaXAoKS5zdGFydHN3aXRoKCcjJykgYW5kIGxlbihwYXJ0cykgPj0gNyBhbmQgcGFydHNbNV0gPT0gbmFtZToKICAgICAgICAgICAgY3Jvbl9tYXRjaGVzLmFwcGVuZChwYXRoKQpyb290cyA9IFsnL3Zhci93d3cnLCAnL3Zhci9sb2cnLCAnL2V0YyddCnRhcmdldF91aWQgPSB1c2VyLnB3X3VpZCBpZiB1c2VyIGlzIG5vdCBOb25lIGVsc2UgTm9uZQp0YXJnZXRfZ2lkID0gZ3JvdXAuZ3JfZ2lkIGlmIGdyb3VwIGlzIG5vdCBOb25lIGVsc2UgTm9uZQpmaWxlX2NvdW50ID0gMApzY2FubmVkID0gMApmb3Igcm9vdCBpbiByb290czoKICAgIGlmIG5vdCBvcy5wYXRoLmlzZGlyKHJvb3QpOgogICAgICAgIGNvbnRpbnVlCiAgICBzdGFydF9kZXB0aCA9IHJvb3QucnN0cmlwKCcvJykuY291bnQoJy8nKQogICAgZm9yIGN1cnJlbnQsIGRpcnMsIGZpbGVzIGluIG9zLndhbGsocm9vdCwgdG9wZG93bj1UcnVlLCBmb2xsb3dsaW5rcz1GYWxzZSk6CiAgICAgICAgaWYgY3VycmVudC5yc3RyaXAoJy8nKS5jb3VudCgnLycpIC0gc3RhcnRfZGVwdGggPj0gMToKICAgICAgICAgICAgZGlyc1s6XSA9IFtdCiAgICAgICAgZm9yIHBhdGggaW4gW2N1cnJlbnRdICsgW29zLnBhdGguam9pbihjdXJyZW50LCBlbnRyeSkgZm9yIGVudHJ5IGluIGRpcnMgKyBmaWxlc106CiAgICAgICAgICAgIGlmIHNjYW5uZWQgPj0gbGltaXQgKiAyMDoKICAgICAgICAgICAgICAgIGJyZWFrCiAgICAgICAgICAgIHRyeToKICAgICAgICAgICAgICAgIHN0ID0gb3MubHN0YXQocGF0aCkKICAgICAgICAgICAgZXhjZXB0IE9TRXJyb3I6CiAgICAgICAgICAgICAgICBjb250aW51ZQogICAgICAgICAgICBzY2FubmVkICs9IDEKICAgICAgICAgICAgaWYgKGtpbmQgPT0gJ3VzZXInIGFuZCB0YXJnZXRfdWlkIGlzIG5vdCBOb25lIGFuZCBzdC5zdF91aWQgPT0gdGFyZ2V0X3VpZCkgb3IgKGtpbmQgPT0gJ2dyb3VwJyBhbmQgdGFyZ2V0X2dpZCBpcyBub3QgTm9uZSBhbmQgc3Quc3RfZ2lkID09IHRhcmdldF9naWQpOgogICAgICAgICAgICAgICAgZmlsZV9jb3VudCArPSAxCiAgICAgICAgaWYgc2Nhbm5lZCA+PSBsaW1pdCAqIDIwOgogICAgICAgICAgICBicmVhawpwcmludCgnc2VydmljZVVuaXRzQ2hlY2tlZD0nICsgc3RyKGxlbih1bml0cykpKQpwcmludCgnc2VydmljZU1hdGNoZXM9JyArIHN0cihsZW4oc2VydmljZV9tYXRjaGVzKSkpCnByaW50KCdjcm9uT3duZXJNYXRjaGVzPScgKyBzdHIobGVuKHNldChjcm9uX21hdGNoZXMpKSkpCnByaW50KCdmaWxlT3duZXJzaGlwTWF0Y2hlcz0nICsgc3RyKGZpbGVfY291bnQpKQpwcmludCgnc2VydmljZU1hdGNoU291cmNlcz0nICsgJywnLmpvaW4oc2VydmljZV9tYXRjaGVzWzpsaW1pdF0pKQpwcmludCgnY3Jvbk1hdGNoU291cmNlcz0nICsgJywnLmpvaW4oc29ydGVkKHNldChjcm9uX21hdGNoZXMpKVs6bGltaXRdKSk='))\" {targetType} {name} {limit}",
            TimeSpan.FromSeconds(30),
            [
                new AllowedCommandParameterDefinition("targetType", Pattern: PrincipalTypePattern),
                new AllowedCommandParameterDefinition("name", Pattern: GroupNamePattern),
                new AllowedCommandParameterDefinition("limit", Pattern: BoundedListLimitPattern),
            ]),
        new(
            "user_check_group_change",
            "python3 -c \"import grp,pwd,sys; user={user}; groups={groups}; mode={mode}; u=next((x for x in pwd.getpwall() if x.pw_name==user), None); group_rows=grp.getgrall(); requested=[g for g in groups.split(',') if g]; existing=set(g.gr_name for g in group_rows); current=sorted(g.gr_name for g in group_rows if user in g.gr_mem); missing=[g for g in requested if g not in existing]; add=sorted(set(requested)-set(current)); remove=sorted(set(current)-set(requested)) if mode=='replace' else []; print('user=' + user); print('exists=' + str(u is not None).lower()); print('mode=' + mode); print('requestedGroups=' + ','.join(requested)); print('missingGroups=' + ','.join(missing)); print('groupsToAdd=' + ','.join(add)); print('groupsToRemove=' + ','.join(remove)); print('requiresConfirmation=true'); print('confirmation=user_apply_group_change:' + user + ':' + mode + ':' + ','.join(requested)); print('rollbackSupported=true'); raise SystemExit(0 if u is not None and not missing else 2)\"",
            TimeSpan.FromSeconds(10),
            [
                new AllowedCommandParameterDefinition("user", Pattern: UserNamePattern),
                new AllowedCommandParameterDefinition("groups", MaxLength: 256, Pattern: GroupListPattern),
                new AllowedCommandParameterDefinition("mode", Pattern: GroupChangeModePattern),
            ]),
        new(
            "user_apply_group_change",
            "sudo -n python3 -c \"import base64,sys; exec(base64.b64decode('aW1wb3J0IGdycCwgb3MsIHB3ZCwgc3VicHJvY2Vzcywgc3lzCnVzZXI9c3lzLmFyZ3ZbMV0KZ3JvdXBzPXN5cy5hcmd2WzJdCm1vZGU9c3lzLmFyZ3ZbM10KdT1uZXh0KCh4IGZvciB4IGluIHB3ZC5nZXRwd2FsbCgpIGlmIHgucHdfbmFtZSA9PSB1c2VyKSwgTm9uZSkKaWYgdSBpcyBOb25lOgogICAgcHJpbnQoJ2V4aXN0cz1mYWxzZScpCiAgICByYWlzZSBTeXN0ZW1FeGl0KDIpCmdyb3VwX3Jvd3M9Z3JwLmdldGdyYWxsKCkKcmVxdWVzdGVkPVtnIGZvciBnIGluIGdyb3Vwcy5zcGxpdCgnLCcpIGlmIGddCmV4aXN0aW5nPXNldChnLmdyX25hbWUgZm9yIGcgaW4gZ3JvdXBfcm93cykKbWlzc2luZz1bZyBmb3IgZyBpbiByZXF1ZXN0ZWQgaWYgZyBub3QgaW4gZXhpc3RpbmddCmlmIG1pc3Npbmc6CiAgICBwcmludCgnZXhpc3RzPXRydWUnKQogICAgcHJpbnQoJ21pc3NpbmdHcm91cHM9JyArICcsJy5qb2luKG1pc3NpbmcpKQogICAgcmFpc2UgU3lzdGVtRXhpdCgyKQpjdXJyZW50PXNvcnRlZChnLmdyX25hbWUgZm9yIGcgaW4gZ3JvdXBfcm93cyBpZiB1c2VyIGluIGcuZ3JfbWVtKQpiYWNrdXBfZGlyPScvdmFyL2JhY2t1cHMva2VscGllL3VzZXJzJwpvcy5tYWtlZGlycyhiYWNrdXBfZGlyLCBleGlzdF9vaz1UcnVlKQpiYWNrdXBfcGF0aD1iYWNrdXBfZGlyICsgJy8nICsgdXNlciArICctZ3JvdXBzLWxhdGVzdC50eHQnCm9wZW4oYmFja3VwX3BhdGgsICd3Jykud3JpdGUoJywnLmpvaW4oY3VycmVudCkgKyAnXG4nKQphcmdzPVsndXNlcm1vZCddCmFyZ3MuYXBwZW5kKCctYUcnIGlmIG1vZGUgPT0gJ2FwcGVuZCcgZWxzZSAnLUcnKQphcmdzLmV4dGVuZChbJywnLmpvaW4ocmVxdWVzdGVkKSwgdXNlcl0pCnJlc3VsdD1zdWJwcm9jZXNzLnJ1bihhcmdzLCB0ZXh0PVRydWUsIGNhcHR1cmVfb3V0cHV0PVRydWUpCnByaW50KCd1c2VyPScgKyB1c2VyKQpwcmludCgnbW9kZT0nICsgbW9kZSkKcHJpbnQoJ2N1cnJlbnRHcm91cENvdW50PScgKyBzdHIobGVuKGN1cnJlbnQpKSkKcHJpbnQoJ3JlcXVlc3RlZEdyb3VwQ291bnQ9JyArIHN0cihsZW4ocmVxdWVzdGVkKSkpCnByaW50KCdtaXNzaW5nR3JvdXBzPScpCnByaW50KCdjaGFuZ2VkPScgKyBzdHIocmVzdWx0LnJldHVybmNvZGUgPT0gMCkubG93ZXIoKSkKcHJpbnQoJ2JhY2t1cFBhdGg9JyArIGJhY2t1cF9wYXRoKQpwcmludCgncm9sbGJhY2tDb25maXJtYXRpb249dXNlcl9yb2xsYmFja19ncm91cF9jaGFuZ2U6JyArIHVzZXIpCnByaW50KCdzdGFuZGFyZEVycm9yU3VtbWFyeT0nICsgKHJlc3VsdC5zdGRlcnIuc3BsaXRsaW5lcygpWzBdWzoxMjBdIGlmIHJlc3VsdC5zdGRlcnIuc3BsaXRsaW5lcygpIGVsc2UgJycpKQpyYWlzZSBTeXN0ZW1FeGl0KHJlc3VsdC5yZXR1cm5jb2RlKQ=='))\" {user} {groups} {mode}",
            TimeSpan.FromSeconds(30),
            [
                new AllowedCommandParameterDefinition("user", Pattern: UserNamePattern),
                new AllowedCommandParameterDefinition("groups", MaxLength: 256, Pattern: GroupListPattern),
                new AllowedCommandParameterDefinition("mode", Pattern: GroupChangeModePattern),
            ],
            SshCommandRiskLevel.ConfirmRequired),
        new(
            "user_rollback_group_change",
            "sudo -n python3 -c \"import base64,sys; exec(base64.b64decode('aW1wb3J0IG9zLCBzdWJwcm9jZXNzLCBzeXMKdXNlcj1zeXMuYXJndlsxXQpiYWNrdXBfcGF0aD0nL3Zhci9iYWNrdXBzL2tlbHBpZS91c2Vycy8nICsgdXNlciArICctZ3JvdXBzLWxhdGVzdC50eHQnCmlmIG5vdCBvcy5wYXRoLmlzZmlsZShiYWNrdXBfcGF0aCk6CiAgICBwcmludCgndXNlcj0nICsgdXNlcikKICAgIHByaW50KCdiYWNrdXBFeGlzdHM9ZmFsc2UnKQogICAgcmFpc2UgU3lzdGVtRXhpdCgyKQpncm91cHM9b3BlbihiYWNrdXBfcGF0aCwgZXJyb3JzPSdyZXBsYWNlJykucmVhZCgpLnN0cmlwKCkKcmVzdWx0PXN1YnByb2Nlc3MucnVuKFsndXNlcm1vZCcsJy1HJyxncm91cHMsdXNlcl0sIHRleHQ9VHJ1ZSwgY2FwdHVyZV9vdXRwdXQ9VHJ1ZSkKcHJpbnQoJ3VzZXI9JyArIHVzZXIpCnByaW50KCdiYWNrdXBFeGlzdHM9dHJ1ZScpCnByaW50KCdyZXN0b3JlZEdyb3VwQ291bnQ9JyArIHN0cihsZW4oW2cgZm9yIGcgaW4gZ3JvdXBzLnNwbGl0KCcsJykgaWYgZ10pKSkKcHJpbnQoJ3Jlc3RvcmVkPScgKyBzdHIocmVzdWx0LnJldHVybmNvZGUgPT0gMCkubG93ZXIoKSkKcHJpbnQoJ3N0YW5kYXJkRXJyb3JTdW1tYXJ5PScgKyAocmVzdWx0LnN0ZGVyci5zcGxpdGxpbmVzKClbMF1bOjEyMF0gaWYgcmVzdWx0LnN0ZGVyci5zcGxpdGxpbmVzKCkgZWxzZSAnJykpCnJhaXNlIFN5c3RlbUV4aXQocmVzdWx0LnJldHVybmNvZGUp'))\" {user}",
            TimeSpan.FromSeconds(30),
            [
                new AllowedCommandParameterDefinition("user", Pattern: UserNamePattern),
            ],
            SshCommandRiskLevel.ConfirmRequired),
        new(
            "user_check_permission_change",
            "python3 -c \"import glob,os,pwd,re,sys; user={user}; shell={shell}; login={login}; sudo={sudo}; u=next((x for x in pwd.getpwall() if x.pw_name==user), None); current_shell=u.pw_shell if u else ''; shell_exists=os.path.exists(shell); files=[p for p in ['/etc/sudoers']+sorted(glob.glob('/etc/sudoers.d/*')) if os.path.isfile(p) and os.access(p, os.R_OK)]; pattern=re.compile(r'(^|[\\s,])' + re.escape(user) + r'([\\s,=]|$)'); rows=[p for p in files for line in open(p, errors='replace') if line.strip() and not line.lstrip().startswith('#') and pattern.search(line)]; print('user=' + user); print('exists=' + str(u is not None).lower()); print('currentShell=' + current_shell); print('requestedShell=' + shell); print('shellExists=' + str(shell_exists).lower()); print('loginTarget=' + login); print('sudoTarget=' + sudo); print('sudoersFilesReadable=' + str(len(files))); print('sudoersMatches=' + str(len(set(rows)))); print('requiresConfirmation=true'); print('confirmation=user_apply_permission_change:' + user + ':' + shell + ':' + login + ':' + sudo); print('rollbackSupported=partial'); raise SystemExit(0 if u is not None and shell_exists else 2)\"",
            TimeSpan.FromSeconds(10),
            [
                new AllowedCommandParameterDefinition("user", Pattern: UserNamePattern),
                new AllowedCommandParameterDefinition("shell", MaxLength: 96, Pattern: LoginShellPattern),
                new AllowedCommandParameterDefinition("login", Pattern: LoginStatePattern),
                new AllowedCommandParameterDefinition("sudo", Pattern: PermissionStatePattern),
            ]),
        new(
            "user_apply_permission_change",
            CreateEncodedPythonCommand(UserApplyPermissionChangeScript, "{user} {shell} {login} {sudo}"),
            TimeSpan.FromSeconds(30),
            [
                new AllowedCommandParameterDefinition("user", Pattern: UserNamePattern),
                new AllowedCommandParameterDefinition("shell", MaxLength: 96, Pattern: LoginShellPattern),
                new AllowedCommandParameterDefinition("login", Pattern: LoginStatePattern),
                new AllowedCommandParameterDefinition("sudo", Pattern: PermissionStatePattern),
            ],
            SshCommandRiskLevel.ConfirmRequired),
        new(
            "user_rollback_permission_change",
            "sudo -n python3 -c \"import base64,sys; exec(base64.b64decode('aW1wb3J0IGJhc2U2NCwgb3MsIHN1YnByb2Nlc3MsIHN5cwp1c2VyID0gc3lzLmFyZ3ZbMV0KbWFuYWdlZF9uYW1lID0gdXNlci5yZXBsYWNlKCckJywgJ19kb2xsYXInKQpzdWRvX3BhdGggPSAnL2V0Yy9zdWRvZXJzLmQva2VscGllLScgKyBtYW5hZ2VkX25hbWUKYmFja3VwX3BhdGggPSAnL3Zhci9iYWNrdXBzL2tlbHBpZS91c2Vycy8nICsgdXNlciArICctcGVybWlzc2lvbnMtbGF0ZXN0Lm1ldGEnCmlmIG5vdCBvcy5wYXRoLmlzZmlsZShiYWNrdXBfcGF0aCk6CiAgICBwcmludCgndXNlcj0nICsgdXNlcikKICAgIHByaW50KCdiYWNrdXBFeGlzdHM9ZmFsc2UnKQogICAgcmFpc2UgU3lzdGVtRXhpdCgyKQptZXRhID0ge30KZm9yIGxpbmUgaW4gb3BlbihiYWNrdXBfcGF0aCwgZXJyb3JzPSdyZXBsYWNlJyk6CiAgICBpZiAnPScgaW4gbGluZToKICAgICAgICBrZXksIHZhbHVlID0gbGluZS5yc3RyaXAoJ1xuJykuc3BsaXQoJz0nLCAxKQogICAgICAgIG1ldGFba2V5XSA9IHZhbHVlCnNoZWxsID0gbWV0YS5nZXQoJ3NoZWxsJywgJycpCmxvY2tlZCA9IG1ldGEuZ2V0KCdsb2NrZWQnLCAndW5rbm93bicpCnN1ZG9fZXhpc3RzID0gbWV0YS5nZXQoJ3N1ZG9FeGlzdHMnLCAnZmFsc2UnKSA9PSAndHJ1ZScKc3Vkb19wYXlsb2FkID0gbWV0YS5nZXQoJ3N1ZG9CYXNlNjQnLCAnJykKcmVzdWx0ID0gc3VicHJvY2Vzcy5Db21wbGV0ZWRQcm9jZXNzKGFyZ3M9W10sIHJldHVybmNvZGU9MCwgc3Rkb3V0PScnLCBzdGRlcnI9JycpCnNoZWxsX3Jlc3RvcmVkID0gRmFsc2UKbG9naW5fcmVzdG9yZWQgPSBGYWxzZQpzdWRvX3Jlc3RvcmVkID0gRmFsc2UKaWYgc2hlbGwgYW5kIG9zLnBhdGguZXhpc3RzKHNoZWxsKToKICAgIHJlc3VsdCA9IHN1YnByb2Nlc3MucnVuKFsndXNlcm1vZCcsICctcycsIHNoZWxsLCB1c2VyXSwgdGV4dD1UcnVlLCBjYXB0dXJlX291dHB1dD1UcnVlKQogICAgc2hlbGxfcmVzdG9yZWQgPSByZXN1bHQucmV0dXJuY29kZSA9PSAwCiAgICBpZiByZXN1bHQucmV0dXJuY29kZSAhPSAwOgogICAgICAgIHByaW50KCd1c2VyPScgKyB1c2VyKQogICAgICAgIHByaW50KCdiYWNrdXBFeGlzdHM9dHJ1ZScpCiAgICAgICAgcHJpbnQoJ3Jlc3RvcmVkPWZhbHNlJykKICAgICAgICBwcmludCgnc3RhbmRhcmRFcnJvclN1bW1hcnk9JyArIChyZXN1bHQuc3RkZXJyLnNwbGl0bGluZXMoKVswXVs6MTIwXSBpZiByZXN1bHQuc3RkZXJyLnNwbGl0bGluZXMoKSBlbHNlICcnKSkKICAgICAgICByYWlzZSBTeXN0ZW1FeGl0KHJlc3VsdC5yZXR1cm5jb2RlKQppZiBsb2NrZWQgPT0gJ3RydWUnOgogICAgcmVzdWx0ID0gc3VicHJvY2Vzcy5ydW4oWyd1c2VybW9kJywgJy1MJywgdXNlcl0sIHRleHQ9VHJ1ZSwgY2FwdHVyZV9vdXRwdXQ9VHJ1ZSkKICAgIGxvZ2luX3Jlc3RvcmVkID0gcmVzdWx0LnJldHVybmNvZGUgPT0gMAplbGlmIGxvY2tlZCA9PSAnZmFsc2UnOgogICAgcmVzdWx0ID0gc3VicHJvY2Vzcy5ydW4oWyd1c2VybW9kJywgJy1VJywgdXNlcl0sIHRleHQ9VHJ1ZSwgY2FwdHVyZV9vdXRwdXQ9VHJ1ZSkKICAgIGxvZ2luX3Jlc3RvcmVkID0gcmVzdWx0LnJldHVybmNvZGUgPT0gMAppZiByZXN1bHQucmV0dXJuY29kZSAhPSAwOgogICAgcHJpbnQoJ3VzZXI9JyArIHVzZXIpCiAgICBwcmludCgnYmFja3VwRXhpc3RzPXRydWUnKQogICAgcHJpbnQoJ3Jlc3RvcmVkPWZhbHNlJykKICAgIHByaW50KCdzdGFuZGFyZEVycm9yU3VtbWFyeT0nICsgKHJlc3VsdC5zdGRlcnIuc3BsaXRsaW5lcygpWzBdWzoxMjBdIGlmIHJlc3VsdC5zdGRlcnIuc3BsaXRsaW5lcygpIGVsc2UgJycpKQogICAgcmFpc2UgU3lzdGVtRXhpdChyZXN1bHQucmV0dXJuY29kZSkKaWYgc3Vkb19leGlzdHM6CiAgICB3aXRoIG9wZW4oc3Vkb19wYXRoLCAnd2InKSBhcyBoYW5kbGU6CiAgICAgICAgaGFuZGxlLndyaXRlKGJhc2U2NC5iNjRkZWNvZGUoc3Vkb19wYXlsb2FkKSkKICAgIG9zLmNobW9kKHN1ZG9fcGF0aCwgMG80NDApCiAgICBzdWRvX3Jlc3RvcmVkID0gVHJ1ZQplbHNlOgogICAgaWYgb3MucGF0aC5leGlzdHMoc3Vkb19wYXRoKToKICAgICAgICBvcy5yZW1vdmUoc3Vkb19wYXRoKQogICAgc3Vkb19yZXN0b3JlZCA9IFRydWUKcHJpbnQoJ3VzZXI9JyArIHVzZXIpCnByaW50KCdiYWNrdXBFeGlzdHM9dHJ1ZScpCnByaW50KCdzaGVsbFJlc3RvcmVkPScgKyBzdHIoc2hlbGxfcmVzdG9yZWQpLmxvd2VyKCkpCnByaW50KCdsb2dpblJlc3RvcmVkPScgKyBzdHIobG9naW5fcmVzdG9yZWQpLmxvd2VyKCkpCnByaW50KCdzdWRvUmVzdG9yZWQ9JyArIHN0cihzdWRvX3Jlc3RvcmVkKS5sb3dlcigpKQpwcmludCgncmVzdG9yZWQ9dHJ1ZScpCnByaW50KCdzdGFuZGFyZEVycm9yU3VtbWFyeT0nICsgKHJlc3VsdC5zdGRlcnIuc3BsaXRsaW5lcygpWzBdWzoxMjBdIGlmIHJlc3VsdC5zdGRlcnIuc3BsaXRsaW5lcygpIGVsc2UgJycpKQ=='))\" {user}",
            TimeSpan.FromSeconds(30),
            [
                new AllowedCommandParameterDefinition("user", Pattern: UserNamePattern),
            ],
            SshCommandRiskLevel.ConfirmRequired),
        new(
            "service_residual_config_check",
            CreateEncodedShellCommand(ServiceResidualConfigCheckScript, "{service} {limit}"),
            TimeSpan.FromSeconds(10),
            [
                ServiceParameter,
                new AllowedCommandParameterDefinition("limit", Pattern: BoundedListLimitPattern),
            ]),
        new(
            "support_report_collect",
            CreateEncodedShellCommand(SupportReportCollectScript, "{limit}"),
            TimeSpan.FromSeconds(30),
            [
                new AllowedCommandParameterDefinition("limit", Pattern: BoundedListLimitPattern),
            ]),
        new(
            "firewall_status",
            CreateEncodedShellCommand(FirewallStatusScript),
            TimeSpan.FromSeconds(15)),
        new(
            "firewall_check_rule",
            "python3 -c \"import shutil,subprocess,sys; action={action}; target={target}; value={value}; zone={zone}; permanent={permanent}; fw=shutil.which('firewall-cmd'); ufw=shutil.which('ufw'); print('action=' + action); print('target=' + target); print('value=' + value); print('zone=' + zone); print('permanent=' + permanent); print('firewalldAvailable=' + str(fw is not None).lower()); print('ufwAvailable=' + str(ufw is not None).lower()); invalid=(target=='port' and '/' not in value) or (target=='service' and '/' in value); print('valid=' + str(not invalid).lower()); sys.exit(2) if invalid else None; state=subprocess.run([fw,'--state'], text=True, capture_output=True) if fw else None; print('firewalldState=' + (state.stdout.strip() if state else 'unavailable')); args=[fw,'--zone',zone,'--query-' + target,value] if fw else []; args.insert(1,'--permanent') if fw and permanent=='true' else None; query=subprocess.run(args, text=True, capture_output=True) if fw else None; print('rulePresent=' + (str(query.returncode == 0).lower() if query else 'unknown')); print('queryExitCode=' + (str(query.returncode) if query else '127')); print('requiresConfirmation=true'); print('confirmation=firewall_apply_rule:' + action + ':' + target + ':' + value + ':' + zone + ':' + permanent)\"",
            TimeSpan.FromSeconds(15),
            [
                new AllowedCommandParameterDefinition("action", Pattern: FirewallActionPattern),
                new AllowedCommandParameterDefinition("target", Pattern: FirewallTargetPattern),
                new AllowedCommandParameterDefinition("value", Pattern: FirewallRuleValuePattern),
                new AllowedCommandParameterDefinition("zone", Pattern: FirewallZonePattern),
                new AllowedCommandParameterDefinition("permanent", Pattern: BooleanPattern),
            ]),
        new(
            "firewall_apply_rule",
            "sudo -n python3 -c \"import shutil,subprocess,sys; action=sys.argv[1]; target=sys.argv[2]; value=sys.argv[3]; zone=sys.argv[4]; permanent=sys.argv[5]; fw=shutil.which('firewall-cmd'); print('action=' + action); print('target=' + target); print('value=' + value); print('zone=' + zone); print('permanent=' + permanent); invalid=(target=='port' and '/' not in value) or (target=='service' and '/' in value); print('valid=' + str(not invalid).lower()); sys.exit(2) if invalid else None; print('firewalldAvailable=' + str(fw is not None).lower()); sys.exit(127) if not fw else None; operation='--add-' + target if action == 'add' else '--remove-' + target; args=[fw,'--zone',zone,operation,value]; args.insert(1,'--permanent') if permanent == 'true' else None; result=subprocess.run(args, text=True, capture_output=True); print('applyExitCode=' + str(result.returncode)); print('changed=' + str(result.returncode == 0).lower()); print('standardErrorSummary=' + (result.stderr.splitlines()[0][:120] if result.stderr.splitlines() else '')); raise SystemExit(result.returncode)\" {action} {target} {value} {zone} {permanent}",
            TimeSpan.FromSeconds(30),
            [
                new AllowedCommandParameterDefinition("action", Pattern: FirewallActionPattern),
                new AllowedCommandParameterDefinition("target", Pattern: FirewallTargetPattern),
                new AllowedCommandParameterDefinition("value", Pattern: FirewallRuleValuePattern),
                new AllowedCommandParameterDefinition("zone", Pattern: FirewallZonePattern),
                new AllowedCommandParameterDefinition("permanent", Pattern: BooleanPattern),
            ],
            SshCommandRiskLevel.ConfirmRequired),
        new(
            "backup_plan_check",
            "python3 -c \"import sys; exec('import os,sys\\nroot=os.path.abspath(sys.argv[1])\\ndepth=int(sys.argv[2])\\nlimit=int(sys.argv[3])\\nexists=os.path.exists(root)\\nscanned=0\\nfiles=0\\ndirs=0\\nsymlinks=0\\nbytes_total=0\\nstart=root.rstrip(\\\"/\\\").count(\\\"/\\\")\\nprint(\\\"scanRoot=\\\" + root)\\nprint(\\\"exists=\\\" + str(exists).lower())\\nprint(\\\"depth=\\\" + str(depth))\\nif exists:\\n    for current, dirnames, filenames in os.walk(root, topdown=True, followlinks=False):\\n        current_depth=current.rstrip(\\\"/\\\").count(\\\"/\\\")-start\\n        if current_depth >= depth:\\n            dirnames[:] = []\\n        for name in list(dirnames) + filenames:\\n            if scanned >= limit:\\n                break\\n            path=os.path.join(current,name)\\n            try:\\n                st=os.lstat(path)\\n            except OSError:\\n                continue\\n            scanned += 1\\n            is_link=os.path.islink(path)\\n            symlinks += 1 if is_link else 0\\n            dirs += 1 if os.path.isdir(path) and not is_link else 0\\n            files += 1 if os.path.isfile(path) and not is_link else 0\\n            bytes_total += st.st_size if os.path.isfile(path) and not is_link else 0\\n        if scanned >= limit:\\n            break\\nprint(\\\"entriesScanned=\\\" + str(scanned))\\nprint(\\\"files=\\\" + str(files))\\nprint(\\\"directories=\\\" + str(dirs))\\nprint(\\\"symlinks=\\\" + str(symlinks))\\nprint(\\\"estimatedBytes=\\\" + str(bytes_total))\\nprint(\\\"requiresConfirmation=true\\\")\\nprint(\\\"confirmation=backup_run:\\\" + root)\\nraise SystemExit(0 if exists else 2)')\" {scanRoot} {depth} {limit}",
            TimeSpan.FromSeconds(30),
            [
                new AllowedCommandParameterDefinition("scanRoot", MaxLength: 128, Pattern: BackupScanRootPattern),
                new AllowedCommandParameterDefinition("depth", Pattern: DepthPattern),
                new AllowedCommandParameterDefinition("limit", Pattern: BoundedListLimitPattern),
            ]),
        new(
            "backup_run",
            "sudo -n python3 -c \"import sys; exec('import os,tarfile,time,sys\\nroot=os.path.abspath(sys.argv[1])\\ndepth=int(sys.argv[2])\\nlimit=int(sys.argv[3])\\nexists=os.path.exists(root)\\nbackup_dir=\\\"/var/backups/kelpie/run\\\"\\nos.makedirs(backup_dir, exist_ok=True)\\nbackup_path=os.path.join(backup_dir, \\\"kelpie-backup-\\\" + time.strftime(\\\"%Y%m%d%H%M%S\\\") + \\\".tar.gz\\\")\\nprint(\\\"scanRoot=\\\" + root)\\nprint(\\\"exists=\\\" + str(exists).lower())\\nprint(\\\"depth=\\\" + str(depth))\\nif not exists:\\n    print(\\\"backupCreated=false\\\")\\n    raise SystemExit(2)\\nstart=root.rstrip(\\\"/\\\").count(\\\"/\\\")\\nentries=0\\nbytes_total=0\\nwith tarfile.open(backup_path, \\\"w:gz\\\") as archive:\\n    for current, dirnames, filenames in os.walk(root, topdown=True, followlinks=False):\\n        current_depth=current.rstrip(\\\"/\\\").count(\\\"/\\\")-start\\n        if current_depth >= depth:\\n            dirnames[:] = []\\n        for name in filenames:\\n            if entries >= limit:\\n                break\\n            path=os.path.join(current, name)\\n            try:\\n                st=os.lstat(path)\\n            except OSError:\\n                continue\\n            if os.path.islink(path) or not os.path.isfile(path):\\n                continue\\n            archive.add(path, arcname=os.path.relpath(path, root), recursive=False)\\n            entries += 1\\n            bytes_total += st.st_size\\n        if entries >= limit:\\n            break\\nprint(\\\"backupCreated=true\\\")\\nprint(\\\"backupPath=\\\" + backup_path)\\nprint(\\\"entriesAdded=\\\" + str(entries))\\nprint(\\\"bytesAdded=\\\" + str(bytes_total))\\nprint(\\\"archiveReadable=\\\" + str(tarfile.is_tarfile(backup_path)).lower())')\" {scanRoot} {depth} {limit}",
            TimeSpan.FromSeconds(120),
            [
                new AllowedCommandParameterDefinition("scanRoot", MaxLength: 128, Pattern: BackupScanRootPattern),
                new AllowedCommandParameterDefinition("depth", Pattern: DepthPattern),
                new AllowedCommandParameterDefinition("limit", Pattern: BoundedListLimitPattern),
            ],
            SshCommandRiskLevel.ConfirmRequired),
        new(
            "backup_verify",
            CreateEncodedShellCommand(BackupVerifyScript, "{backupPath}"),
            TimeSpan.FromSeconds(60),
            [
                new AllowedCommandParameterDefinition("backupPath", MaxLength: 256, Pattern: BackupPathPattern),
            ]),
        new(
            "audit_verify",
            "python3 -c \"import json,os,sys; path={logPath}; limit=int({limit}); exists=os.path.isfile(path); print('auditPath=' + path); print('exists=' + str(exists).lower()); sys.exit(2) if not exists else None; lines=0; json_lines=0; missing=0; breaks=0; previous=None; handle=open(path, errors='replace'); exec('for line in handle:\\n    if lines >= limit:\\n        break\\n    lines += 1\\n    text=line.strip()\\n    if not text:\\n        continue\\n    try:\\n        row=json.loads(text)\\n    except Exception:\\n        continue\\n    json_lines += 1\\n    current=row.get(\\\"hash\\\")\\n    prev=row.get(\\\"prevHash\\\") or row.get(\\\"previousHash\\\")\\n    if current is None or prev is None:\\n        missing += 1\\n    elif previous is not None and prev != previous:\\n        breaks += 1\\n    if current:\\n        previous=current'); handle.close(); print('linesScanned=' + str(lines)); print('jsonLines=' + str(json_lines)); print('missingHashFields=' + str(missing)); print('chainBreaks=' + str(breaks)); raise SystemExit(0 if breaks == 0 else 1)\"",
            TimeSpan.FromSeconds(30),
            [
                new AllowedCommandParameterDefinition("logPath", MaxLength: 180, Pattern: AuditLogPathPattern),
                new AllowedCommandParameterDefinition("limit", Pattern: BoundedListLimitPattern),
            ]),
        new(
            "audit_export",
            "python3 -c \"import json,os,sys; path={logPath}; limit=int({limit}); exists=os.path.isfile(path); print('exportVersion=1'); print('auditPath=' + path); print('exists=' + str(exists).lower()); sys.exit(2) if not exists else None; allowed=['timestamp','eventType','toolName','commandName','exitCode','result','riskLevel']; records=0; handle=open(path, errors='replace'); exec('for line in handle:\\n    if records >= limit:\\n        break\\n    text=line.strip()\\n    if not text:\\n        continue\\n    try:\\n        row=json.loads(text)\\n    except Exception:\\n        records += 1\\n        print(\\\"record=\\\" + str(records) + \\\":format=text\\\")\\n        continue\\n    pairs=[]\\n    for key in allowed:\\n        value=row.get(key)\\n        if value is not None:\\n            pairs.append(key + \\\"=\\\" + str(value).replace(\\\"\\\\n\\\", \\\" \\\")[:80])\\n    records += 1\\n    print(\\\"record=\\\" + str(records) + \\\":\\\" + \\\",\\\".join(pairs))'); handle.close(); print('records=' + str(records))\"",
            TimeSpan.FromSeconds(30),
            [
                new AllowedCommandParameterDefinition("logPath", MaxLength: 180, Pattern: AuditLogPathPattern),
                new AllowedCommandParameterDefinition("limit", Pattern: BoundedListLimitPattern),
            ]),
        new(
            "check_http_local",
            "python3 -c \"import urllib.request; port=int({port}); response=urllib.request.urlopen('http://127.0.0.1:%d/' % port, timeout=5); print('status=' + str(response.status)); print('content_type=' + str(response.headers.get('Content-Type', '')))\"",
            TimeSpan.FromSeconds(10),
            [
                new AllowedCommandParameterDefinition("port", Pattern: AllowedCommandPatterns.TcpPort),
            ]),
        new(
            "check_tcp_connect_local",
            "python3 -c \"import socket; port=int({port}); sock=socket.create_connection(('127.0.0.1', port), timeout=5); print('connected'); sock.close()\"",
            TimeSpan.FromSeconds(10),
            [
                new AllowedCommandParameterDefinition("port", Pattern: AllowedCommandPatterns.TcpPort),
            ]),
        new("get_listening_ports", "ss -lntu", TimeSpan.FromSeconds(15)),
        new("get_failed_services", "systemctl --failed --no-pager", TimeSpan.FromSeconds(20)),
        new(
            "get_journal_recent",
            "journalctl -n {lines} --no-pager",
            TimeSpan.FromSeconds(20),
            [
                new AllowedCommandParameterDefinition("lines", Pattern: LinesPattern),
            ]),
        new(
            "service_status",
            "systemctl status {service} --no-pager",
            TimeSpan.FromSeconds(30),
            [ServiceParameter]),
        new(
            "service_is_active",
            "systemctl is-active {service}",
            TimeSpan.FromSeconds(10),
            [ServiceParameter]),
        new(
            "service_is_enabled",
            "systemctl is-enabled {service}",
            TimeSpan.FromSeconds(10),
            [ServiceParameter]),
        new(
            "list_services",
            "sh -c 'state=\"$1\"; limit=\"$2\"; systemctl list-units --type=service --state=\"$state\" --no-pager --plain --all --no-legend | head -n \"$limit\"' sh {state} {limit}",
            TimeSpan.FromSeconds(30),
            [
                new AllowedCommandParameterDefinition("state", Pattern: ServiceStatePattern),
                new AllowedCommandParameterDefinition("limit", Pattern: LimitPattern),
            ]),
        new(
            "tail_log",
            "journalctl -u {service} -n {lines} --no-pager",
            TimeSpan.FromSeconds(20),
            [
                ServiceParameter,
                new AllowedCommandParameterDefinition("lines", Pattern: LinesPattern),
            ]),
    ];

    /// <inheritdoc />
    public IReadOnlyCollection<string> OsFamilies { get; } = ["*"];

    /// <inheritdoc />
    public bool Supports(SshConnectionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return !string.IsNullOrWhiteSpace(profile.OsFamily);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<AllowedCommandDefinition> GetCommands(SshConnectionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return Commands;
    }

    private static string CreateEncodedPythonCommand(string script, string arguments)
    {
        var encoded = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(script));
        return $"sudo -n python3 -c \"import base64,sys; exec(base64.b64decode('{encoded}'))\" {arguments}";
    }

    private static string CreateEncodedShellCommand(string script)
    {
        var encoded = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(script));
        return $"sh -c \"printf %s '{encoded}' | base64 -d | sh\"";
    }

    private static string CreateEncodedShellCommand(string script, string arguments)
    {
        var encoded = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(script));
        return $"sh -c \"printf %s '{encoded}' | base64 -d | sh -s -- {arguments}\"";
    }
}
