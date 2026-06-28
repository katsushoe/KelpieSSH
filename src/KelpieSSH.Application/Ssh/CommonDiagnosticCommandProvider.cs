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

    private const string CronListScript = """
limit="$1"
count=0

print_entry() {
    source_path="$1"
    line="$2"

    trimmed=$(printf '%s' "$line" | sed 's/^[[:space:]]*//;s/[[:space:]]*$//')
    case "$trimmed" in
        ''|\#*) return ;;
    esac

    if [ "$count" -lt "$limit" ]; then
        printf '%s:%s\n' "$source_path" "$trimmed"
        count=$((count + 1))
    fi
}

for path in /etc/crontab /etc/cron.d/*; do
    [ -f "$path" ] || continue
    while IFS= read -r line || [ -n "$line" ]; do
        print_entry "$path" "$line"
        [ "$count" -ge "$limit" ] && exit 0
    done < "$path"
done

if [ "$count" -lt "$limit" ] && command -v crontab >/dev/null 2>&1; then
    crontab_output=$(crontab -l 2>&1)
    crontab_code=$?
    if [ "$crontab_code" -eq 0 ]; then
        printf '%s\n' "$crontab_output" | while IFS= read -r line || [ -n "$line" ]; do
            print_entry "user-crontab" "$line"
            [ "$count" -ge "$limit" ] && exit 0
        done
    elif [ "$crontab_code" -ne 1 ]; then
        printf '%s' "$crontab_output" >&2
    fi
fi
""";

    private const string CronValidateScript = """
expr="$1"
run_user="$2"
command_text="$3"
log_path="$4"

cron_ok=true
set -- $expr
if [ "$#" -ne 5 ]; then
    cron_ok=false
else
    for part in "$@"; do
        case "$part" in
            ''|*[!0-9A-Za-z*/?,#LW.-]*) cron_ok=false ;;
        esac
    done
fi

base_user="$run_user"
case "$base_user" in
    *'$') base_user=${base_user%?} ;;
esac

user_ok=false
case "$base_user" in
    [a-z_]*)
        case "$base_user" in
            *[!a-z0-9_-]*) ;;
            *) user_ok=true ;;
        esac
        ;;
esac

trimmed_command=$(printf '%s' "$command_text" | sed 's/[[:space:]]//g')
command_ok=false
[ -n "$trimmed_command" ] && command_ok=true

log_ok=false
case "$log_path" in
    /var/log/*)
        case "$log_path" in
            *..*) ;;
            *) log_ok=true ;;
        esac
        ;;
esac

ok=false
if [ "$cron_ok" = true ] && [ "$user_ok" = true ] && [ "$command_ok" = true ] && [ "$log_ok" = true ]; then
    ok=true
fi

printf 'valid=%s\n' "$ok"
printf 'cronExpression=%s\n' "$expr"
printf 'runUser=%s\n' "$run_user"
printf 'logPath=%s\n' "$log_path"

[ "$ok" = true ] || exit 1
""";

    private const string CronCheckWriteScript = """
target="$1"
run_user="$2"
expr="$3"
command_text="$4"
log_path="$5"

exists=false
if getent passwd "$run_user" >/dev/null 2>&1; then
    exists=true
fi

target_path="/etc/cron.d/kelpie-managed"
if [ "$target" = "user" ]; then
    target_path="user-crontab"
fi

printf 'targetType=%s\n' "$target"
printf 'target=%s\n' "$target_path"
printf 'runUser=%s\n' "$run_user"
printf 'userExists=%s\n' "$exists"
printf 'cronExpression=%s\n' "$expr"
printf 'logPath=%s\n' "$log_path"
printf 'requiresConfirmation=true\n'
printf 'confirmation=cron_write:%s:%s\n' "$target" "$run_user"
printf 'rollbackSupported=true\n'

[ "$exists" = true ] || exit 2
""";

    private const string CronWriteScript = """
target="$1"
run_user="$2"
expr="$3"
command_text="$4"
log_path="$5"

backup_dir="/var/backups/kelpie/cron"
mkdir -p "$backup_dir"
marker="# kelpie-managed:$target:$run_user"

if [ "$target" = "user" ]; then
    list_output=$(crontab -u "$run_user" -l 2>&1)
    list_code=$?
    if [ "$list_code" -eq 0 ]; then
        existed=true
        current="$list_output"
    elif [ "$list_code" -eq 1 ]; then
        existed=false
        current=""
    else
        printf '%s\n' "$list_output" >&2
        exit "$list_code"
    fi
    backup_path="$backup_dir/user-$run_user-latest.cron"
    target_path="user-crontab"
else
    target_path="/etc/cron.d/kelpie-managed"
    if [ -e "$target_path" ]; then
        existed=true
        current=$(cat "$target_path" 2>/dev/null)
    else
        existed=false
        current=""
    fi
    backup_path="$backup_dir/system-$run_user-latest.cron"
fi

printf '%s\n' "$current" > "$backup_path"
printf 'existed=%s\n' "$existed" > "$backup_path.meta"

filtered=$(printf '%s\n' "$current" | awk -v marker="$marker" 'index($0, marker) == 0')
if [ "$target" = "user" ]; then
    new_line="$expr $command_text >> $log_path 2>&1 $marker"
else
    new_line="$expr $run_user $command_text >> $log_path 2>&1 $marker"
fi

payload=$(printf '%s\n%s\n' "$filtered" "$new_line" | sed '/^[[:space:]]*$/d')
if [ "$target" = "user" ]; then
    result_output=$(printf '%s\n' "$payload" | crontab -u "$run_user" - 2>&1)
    result_code=$?
else
    printf '%s\n' "$payload" > "$target_path"
    chmod 0644 "$target_path"
    result_output=""
    result_code=0
fi

printf 'targetType=%s\n' "$target"
printf 'target=%s\n' "$target_path"
printf 'runUser=%s\n' "$run_user"
printf 'changed=%s\n' "$([ "$result_code" -eq 0 ] && printf true || printf false)"
printf 'backupPath=%s\n' "$backup_path"
printf 'rollbackConfirmation=cron_rollback:%s:%s\n' "$target" "$run_user"
printf 'standardErrorSummary=%s\n' "$(printf '%s\n' "$result_output" | sed -n '1{s/[[:cntrl:]]//g;s/^\(.\{0,120\}\).*/\1/;p;}')"
exit "$result_code"
""";

    private const string CronRollbackScript = """
target="$1"
run_user="$2"

backup_dir="/var/backups/kelpie/cron"
if [ "$target" = "user" ]; then
    backup_path="$backup_dir/user-$run_user-latest.cron"
    target_path="user-crontab"
else
    backup_path="$backup_dir/system-$run_user-latest.cron"
    target_path="/etc/cron.d/kelpie-managed"
fi

meta_path="$backup_path.meta"
if [ ! -f "$backup_path" ] || [ ! -f "$meta_path" ]; then
    printf 'backupPath=%s\n' "$backup_path"
    printf 'backupExists=false\n'
    exit 2
fi

if grep -qx 'existed=true' "$meta_path"; then
    existed=true
else
    existed=false
fi

payload=$(cat "$backup_path")
if [ "$target" = "user" ]; then
    if [ "$existed" = true ]; then
        result_output=$(printf '%s\n' "$payload" | crontab -u "$run_user" - 2>&1)
        result_code=$?
    else
        result_output=$(crontab -u "$run_user" -r 2>&1)
        result_code=$?
        [ "$result_code" -eq 1 ] && result_code=0
    fi
else
    if [ "$existed" = true ]; then
        printf '%s\n' "$payload" > "$target_path"
        chmod 0644 "$target_path"
    elif [ -e "$target_path" ]; then
        rm -f "$target_path"
    fi
    result_output=""
    result_code=0
fi

printf 'targetType=%s\n' "$target"
printf 'target=%s\n' "$target_path"
printf 'runUser=%s\n' "$run_user"
printf 'backupExists=true\n'
printf 'restored=%s\n' "$([ "$result_code" -eq 0 ] && printf true || printf false)"
printf 'standardErrorSummary=%s\n' "$(printf '%s\n' "$result_output" | sed -n '1{s/[[:cntrl:]]//g;s/^\(.\{0,120\}\).*/\1/;p;}')"
exit "$result_code"
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

    private const string SudoersCheckScript = """
kind="$1"
name="$2"

exists=false
admin_groups=""
if [ "$kind" = "user" ]; then
    if getent passwd "$name" >/dev/null 2>&1; then
        exists=true
        for group in $(id -nG "$name" 2>/dev/null); do
            case "$group" in
                admin|sudo|wheel)
                    if [ -z "$admin_groups" ]; then
                        admin_groups="$group"
                    else
                        admin_groups="$admin_groups,$group"
                    fi
                    ;;
            esac
        done
    fi
else
    if getent group "$name" >/dev/null 2>&1; then
        exists=true
    fi
    case "$name" in
        admin|sudo|wheel) admin_groups="$name" ;;
    esac
fi

token="$name"
if [ "$kind" = "group" ]; then
    token="%$name"
fi

readable=0
matches=0
match_sources=""
for path in /etc/sudoers /etc/sudoers.d/*; do
    [ -f "$path" ] || continue
    [ -r "$path" ] || continue
    readable=$((readable + 1))
    if awk -v token="$token" '
        /^[[:space:]]*($|#)/ { next }
        {
            line = $0
            pattern = "(^|[[:space:],])" token "([[:space:],=]|$)"
            if (line ~ pattern) {
                found = 1
            }
        }
        END { exit(found ? 0 : 1) }
    ' "$path"; then
        matches=$((matches + 1))
        if [ -z "$match_sources" ]; then
            match_sources="$path"
        else
            match_sources="$match_sources,$path"
        fi
    fi
done

printf 'principalType=%s\n' "$kind"
printf 'name=%s\n' "$name"
printf 'exists=%s\n' "$exists"
printf 'adminGroups=%s\n' "$admin_groups"
printf 'sudoersFilesReadable=%s\n' "$readable"
printf 'sudoersMatches=%s\n' "$matches"
printf 'sudoersMatchSources=%s\n' "$match_sources"
""";

    private const string UserServiceUsageCheckScript = """
kind="$1"
name="$2"
limit="$3"

units=""
list_output=$(systemctl list-units --type=service --all --no-legend --plain 2>&1)
list_code=$?
if [ "$list_code" -eq 0 ] || [ "$list_code" -eq 1 ]; then
    units=$(printf '%s\n' "$list_output" | awk -v limit="$limit" 'NF > 0 && count < limit { print $1; count++ }')
else
    printf '%s' "$list_output" >&2
fi

units_checked=0
matches=0
rows=""
for unit in $units; do
    units_checked=$((units_checked + 1))
    for field in User Group SupplementaryGroups; do
        value=$(systemctl show "$unit" -p "$field" --value 2>/dev/null | tr ',' ' ')
        hit=false
        if [ "$kind" = "user" ] && [ "$field" = "User" ] && [ "$value" = "$name" ]; then
            hit=true
        elif [ "$kind" = "group" ] && { [ "$field" = "Group" ] || [ "$field" = "SupplementaryGroups" ]; }; then
            for part in $value; do
                if [ "$part" = "$name" ]; then
                    hit=true
                    break
                fi
            done
        fi

        if [ "$hit" = true ]; then
            matches=$((matches + 1))
            if [ "$matches" -le "$limit" ]; then
                row="$unit:$field=$value"
                if [ -z "$rows" ]; then
                    rows="$row"
                else
                    rows="$rows
$row"
                fi
            fi
        fi
    done
done

printf 'principalType=%s\n' "$kind"
printf 'name=%s\n' "$name"
printf 'unitsChecked=%s\n' "$units_checked"
printf 'matches=%s\n' "$matches"
[ -n "$rows" ] && printf '%s\n' "$rows"
""";

    private const string UserFileOwnershipCheckScript = """
kind="$1"
name="$2"
root="$3"
depth="$4"
limit="$5"

printf 'scanRoot=%s\n' "$root"
printf 'principalType=%s\n' "$kind"
printf 'name=%s\n' "$name"
printf 'depth=%s\n' "$depth"

target_id=""
if [ "$kind" = "user" ]; then
    target_id=$(getent passwd "$name" | awk -F: '{ print $3 }')
else
    target_id=$(getent group "$name" | awk -F: '{ print $3 }')
fi

if [ -z "$target_id" ]; then
    echo "principal not found" >&2
    exit 2
fi

scanned=0
matches=0
rows=""
scan_limit=$((limit * 20))
find_depth=$((depth + 1))

if [ -d "$root" ]; then
    while IFS= read -r path; do
        [ "$scanned" -ge "$scan_limit" ] && break
        [ "$matches" -ge "$limit" ] && break

        ids=$(stat -c '%u:%g' "$path" 2>/dev/null) || continue
        owner_id=${ids%%:*}
        group_id=${ids#*:}
        scanned=$((scanned + 1))

        hit=false
        if [ "$kind" = "user" ] && [ "$owner_id" = "$target_id" ]; then
            hit=true
        elif [ "$kind" = "group" ] && [ "$group_id" = "$target_id" ]; then
            hit=true
        fi

        if [ "$hit" = true ]; then
            owner=$(stat -c '%U' "$path" 2>/dev/null)
            group=$(stat -c '%G' "$path" 2>/dev/null)
            row="$path:owner=$owner:group=$group"
            matches=$((matches + 1))
            if [ -z "$rows" ]; then
                rows="$row"
            else
                rows="$rows
$row"
            fi
        fi
    done <<EOF
$(find "$root" -maxdepth "$find_depth" -xdev -print 2>/dev/null)
EOF
fi

printf 'entriesScanned=%s\n' "$scanned"
printf 'matches=%s\n' "$matches"
[ -n "$rows" ] && printf '%s\n' "$rows"
""";

    private const string UserUsageCheckScript = """
kind="$1"
name="$2"
limit="$3"

exists=false
target_id=""
if [ "$kind" = "user" ]; then
    target_id=$(getent passwd "$name" | awk -F: '{ print $3 }')
else
    target_id=$(getent group "$name" | awk -F: '{ print $3 }')
fi

if [ -n "$target_id" ]; then
    exists=true
fi

service_units=""
list_output=$(systemctl list-units --type=service --all --no-legend --plain 2>&1)
list_code=$?
if [ "$list_code" -eq 0 ] || [ "$list_code" -eq 1 ]; then
    service_units=$(printf '%s\n' "$list_output" | awk -v limit="$limit" 'NF > 0 && count < limit { print $1; count++ }')
else
    printf '%s' "$list_output" >&2
fi

service_units_checked=0
service_matches=0
service_sources=""
for unit in $service_units; do
    service_units_checked=$((service_units_checked + 1))
    for field in User Group SupplementaryGroups; do
        value=$(systemctl show "$unit" -p "$field" --value 2>/dev/null | tr ',' ' ')
        hit=false
        if [ "$kind" = "user" ] && [ "$field" = "User" ] && [ "$value" = "$name" ]; then
            hit=true
        elif [ "$kind" = "group" ] && { [ "$field" = "Group" ] || [ "$field" = "SupplementaryGroups" ]; }; then
            for part in $value; do
                if [ "$part" = "$name" ]; then
                    hit=true
                    break
                fi
            done
        fi

        if [ "$hit" = true ]; then
            service_matches=$((service_matches + 1))
            if [ "$service_matches" -le "$limit" ]; then
                row="$unit:$field"
                if [ -z "$service_sources" ]; then
                    service_sources="$row"
                else
                    service_sources="$service_sources,$row"
                fi
            fi
        fi
    done
done

cron_sources=""
if [ "$kind" = "user" ]; then
    for path in /etc/crontab /etc/cron.d/*; do
        [ -f "$path" ] || continue
        if awk -v user="$name" '
            /^[[:space:]]*($|#)/ { next }
            NF >= 7 && $6 == user { found = 1 }
            END { exit(found ? 0 : 1) }
        ' "$path"; then
            if [ -z "$cron_sources" ]; then
                cron_sources="$path"
            else
                case ",$cron_sources," in
                    *",$path,"*) ;;
                    *) cron_sources="$cron_sources,$path" ;;
                esac
            fi
        fi
    done
fi

cron_owner_matches=0
if [ -n "$cron_sources" ]; then
    cron_owner_matches=$(printf '%s\n' "$cron_sources" | awk -F, '{ print NF }')
fi

file_matches=0
scanned=0
scan_limit=$((limit * 20))
if [ -n "$target_id" ]; then
    for root in /var/www /var/log /etc; do
        [ -d "$root" ] || continue
        while IFS= read -r path; do
            [ "$scanned" -ge "$scan_limit" ] && break

            ids=$(stat -c '%u:%g' "$path" 2>/dev/null) || continue
            owner_id=${ids%%:*}
            group_id=${ids#*:}
            scanned=$((scanned + 1))

            if [ "$kind" = "user" ] && [ "$owner_id" = "$target_id" ]; then
                file_matches=$((file_matches + 1))
            elif [ "$kind" = "group" ] && [ "$group_id" = "$target_id" ]; then
                file_matches=$((file_matches + 1))
            fi
        done <<EOF
$(find "$root" -maxdepth 2 -xdev -print 2>/dev/null)
EOF
        [ "$scanned" -ge "$scan_limit" ] && break
    done
fi

printf 'principalType=%s\n' "$kind"
printf 'name=%s\n' "$name"
printf 'exists=%s\n' "$exists"
printf 'serviceUnitsChecked=%s\n' "$service_units_checked"
printf 'serviceMatches=%s\n' "$service_matches"
printf 'cronOwnerMatches=%s\n' "$cron_owner_matches"
printf 'fileOwnershipMatches=%s\n' "$file_matches"
printf 'serviceMatchSources=%s\n' "$service_sources"
printf 'cronMatchSources=%s\n' "$cron_sources"
""";

    private const string UserCheckGroupChangeScript = """
user="$1"
groups="$2"
mode="$3"

exists=false
if getent passwd "$user" >/dev/null 2>&1; then
    exists=true
fi

requested=""
missing=""
current=$(getent group | awk -F: -v user="$user" '
{
    count = split($4, members, ",")
    for (i = 1; i <= count; i++) {
        if (members[i] == user) {
            print $1
        }
    }
}' | sort | paste -sd, -)

groups_to_add=""
old_ifs=$IFS
IFS=,
for group in $groups; do
    [ -n "$group" ] || continue
    if [ -z "$requested" ]; then
        requested="$group"
    else
        requested="$requested,$group"
    fi

    if ! getent group "$group" >/dev/null 2>&1; then
        if [ -z "$missing" ]; then
            missing="$group"
        else
            missing="$missing,$group"
        fi
    fi

    found=false
    IFS=,
    for current_group in $current; do
        if [ "$current_group" = "$group" ]; then
            found=true
            break
        fi
    done

    if [ "$found" = false ]; then
        if [ -z "$groups_to_add" ]; then
            groups_to_add="$group"
        else
            groups_to_add="$groups_to_add,$group"
        fi
    fi
done
IFS=$old_ifs

groups_to_remove=""
if [ "$mode" = "replace" ]; then
    old_ifs=$IFS
    IFS=,
    for current_group in $current; do
        [ -n "$current_group" ] || continue
        keep=false
        IFS=,
        for requested_group in $requested; do
            if [ "$requested_group" = "$current_group" ]; then
                keep=true
                break
            fi
        done

        if [ "$keep" = false ]; then
            if [ -z "$groups_to_remove" ]; then
                groups_to_remove="$current_group"
            else
                groups_to_remove="$groups_to_remove,$current_group"
            fi
        fi
    done
    IFS=$old_ifs
fi

printf 'user=%s\n' "$user"
printf 'exists=%s\n' "$exists"
printf 'mode=%s\n' "$mode"
printf 'requestedGroups=%s\n' "$requested"
printf 'missingGroups=%s\n' "$missing"
printf 'groupsToAdd=%s\n' "$groups_to_add"
printf 'groupsToRemove=%s\n' "$groups_to_remove"
printf 'requiresConfirmation=true\n'
printf 'confirmation=user_apply_group_change:%s:%s:%s\n' "$user" "$mode" "$requested"
printf 'rollbackSupported=true\n'

if [ "$exists" = true ] && [ -z "$missing" ]; then
    exit 0
fi

exit 2
""";

    private const string UserCheckPermissionChangeScript = """
user="$1"
shell_path="$2"
login="$3"
sudo="$4"

exists=false
current_shell=""
row=$(getent passwd "$user")
if [ -n "$row" ]; then
    exists=true
    current_shell=$(printf '%s\n' "$row" | awk -F: '{ print $7 }')
fi

shell_exists=false
if [ -e "$shell_path" ]; then
    shell_exists=true
fi

sudoers_readable=0
sudoers_matches=0
sudoers_sources=""
for path in /etc/sudoers /etc/sudoers.d/*; do
    [ -f "$path" ] || continue
    [ -r "$path" ] || continue
    sudoers_readable=$((sudoers_readable + 1))
    if awk -v token="$user" '
        /^[[:space:]]*($|#)/ { next }
        {
            pattern = "(^|[[:space:],])" token "([[:space:],=]|$)"
            if ($0 ~ pattern) {
                found = 1
            }
        }
        END { exit(found ? 0 : 1) }
    ' "$path"; then
        case ",$sudoers_sources," in
            *",$path,"*) ;;
            *)
                sudoers_matches=$((sudoers_matches + 1))
                if [ -z "$sudoers_sources" ]; then
                    sudoers_sources="$path"
                else
                    sudoers_sources="$sudoers_sources,$path"
                fi
                ;;
        esac
    fi
done

printf 'user=%s\n' "$user"
printf 'exists=%s\n' "$exists"
printf 'currentShell=%s\n' "$current_shell"
printf 'requestedShell=%s\n' "$shell_path"
printf 'shellExists=%s\n' "$shell_exists"
printf 'loginTarget=%s\n' "$login"
printf 'sudoTarget=%s\n' "$sudo"
printf 'sudoersFilesReadable=%s\n' "$sudoers_readable"
printf 'sudoersMatches=%s\n' "$sudoers_matches"
printf 'requiresConfirmation=true\n'
printf 'confirmation=user_apply_permission_change:%s:%s:%s:%s\n' "$user" "$shell_path" "$login" "$sudo"
printf 'rollbackSupported=partial\n'

if [ "$exists" = true ] && [ "$shell_exists" = true ]; then
    exit 0
fi

exit 2
""";

    private const string UserApplyGroupChangeScript = """
user="$1"
groups="$2"
mode="$3"

if ! getent passwd "$user" >/dev/null 2>&1; then
    printf 'exists=false\n'
    exit 2
fi

missing=""
old_ifs=$IFS
IFS=,
for group in $groups; do
    [ -n "$group" ] || continue
    if ! getent group "$group" >/dev/null 2>&1; then
        if [ -z "$missing" ]; then
            missing="$group"
        else
            missing="$missing,$group"
        fi
    fi
done
IFS=$old_ifs

if [ -n "$missing" ]; then
    printf 'exists=true\n'
    printf 'missingGroups=%s\n' "$missing"
    exit 2
fi

current=$(awk -F: -v user="$user" '
{
    split($4, members, ",")
    for (i in members) {
        if (members[i] == user) {
            print $1
            break
        }
    }
}
' /etc/group | paste -sd, -)
backup_dir="/var/backups/kelpie/users"
mkdir -p "$backup_dir"
backup_path="$backup_dir/$user-groups-latest.txt"
printf '%s\n' "$current" > "$backup_path"

if [ "$mode" = "append" ]; then
    result_output=$(usermod -aG "$groups" "$user" 2>&1)
else
    result_output=$(usermod -G "$groups" "$user" 2>&1)
fi
result_code=$?

printf 'user=%s\n' "$user"
printf 'mode=%s\n' "$mode"
printf 'currentGroupCount=%s\n' "$(printf '%s' "$current" | awk -F, '{ print ($0 == "" ? 0 : NF) }')"
printf 'requestedGroupCount=%s\n' "$(printf '%s' "$groups" | awk -F, '{ print ($0 == "" ? 0 : NF) }')"
printf 'missingGroups=\n'
printf 'changed=%s\n' "$([ "$result_code" -eq 0 ] && printf true || printf false)"
printf 'backupPath=%s\n' "$backup_path"
printf 'rollbackConfirmation=user_rollback_group_change:%s\n' "$user"
printf 'standardErrorSummary=%s\n' "$(printf '%s\n' "$result_output" | sed -n '1{s/[[:cntrl:]]//g;s/^\(.\{0,120\}\).*/\1/;p;}')"
exit "$result_code"
""";

    private const string UserRollbackGroupChangeScript = """
user="$1"
backup_path="/var/backups/kelpie/users/$user-groups-latest.txt"

if [ ! -f "$backup_path" ]; then
    printf 'user=%s\n' "$user"
    printf 'backupExists=false\n'
    exit 2
fi

groups=$(cat "$backup_path" | tr -d '\r\n')
result_output=$(usermod -G "$groups" "$user" 2>&1)
result_code=$?

printf 'user=%s\n' "$user"
printf 'backupExists=true\n'
printf 'restoredGroupCount=%s\n' "$(printf '%s' "$groups" | awk -F, '{ print ($0 == "" ? 0 : NF) }')"
printf 'restored=%s\n' "$([ "$result_code" -eq 0 ] && printf true || printf false)"
printf 'standardErrorSummary=%s\n' "$(printf '%s\n' "$result_output" | sed -n '1{s/[[:cntrl:]]//g;s/^\(.\{0,120\}\).*/\1/;p;}')"
exit "$result_code"
""";

    private const string UserApplyPermissionChangeScript = """
user="$1"
shell_path="$2"
login="$3"
sudo_state="$4"

row=$(getent passwd "$user")
if [ -z "$row" ]; then
    printf 'exists=false\n'
    exit 2
fi

current_shell=$(printf '%s\n' "$row" | awk -F: '{ print $7 }')
if [ ! -e "$shell_path" ]; then
    printf 'exists=true\n'
    printf 'shellExists=false\n'
    exit 2
fi

visudo_path=$(command -v visudo 2>/dev/null)
[ -n "$visudo_path" ] || visudo_path="/usr/sbin/visudo"
if [ "$sudo_state" != "unchanged" ] && [ ! -e "$visudo_path" ]; then
    printf 'exists=true\n'
    printf 'visudoExists=false\n'
    exit 2
fi

managed_name=$(printf '%s' "$user" | sed 's/\$/_dollar/g')
sudo_path="/etc/sudoers.d/kelpie-$managed_name"
backup_dir="/var/backups/kelpie/users"
mkdir -p "$backup_dir"
backup_path="$backup_dir/$user-permissions-latest.meta"

locked="unknown"
passwd_path=$(command -v passwd 2>/dev/null)
[ -n "$passwd_path" ] || passwd_path="/usr/bin/passwd"
status_output=$("$passwd_path" -S "$user" 2>/dev/null)
status_code=$?
if [ "$status_code" -eq 0 ]; then
    state=$(printf '%s\n' "$status_output" | awk '{ print $2 }')
    case "$state" in
        L|LK|NP) locked=true ;;
        *) locked=false ;;
    esac
elif [ -r /etc/shadow ]; then
    secret=$(awk -F: -v user="$user" '$1 == user { print $2; exit }' /etc/shadow)
    case "$secret" in
        '!'*|'*'*) locked=true ;;
        '') ;;
        *) locked=false ;;
    esac
fi

if [ -f "$sudo_path" ]; then
    sudo_exists=true
    sudo_payload=$(base64 "$sudo_path" | tr -d '\n')
else
    sudo_exists=false
    sudo_payload=""
fi

{
    printf 'shell=%s\n' "$current_shell"
    printf 'locked=%s\n' "$locked"
    printf 'sudoExists=%s\n' "$sudo_exists"
    printf 'sudoBase64=%s\n' "$sudo_payload"
} > "$backup_path"

shell_changed=false
login_changed=false
sudo_changed=false
result_output=""
result_code=0

if [ "$current_shell" != "$shell_path" ]; then
    result_output=$(usermod -s "$shell_path" "$user" 2>&1)
    result_code=$?
    [ "$result_code" -eq 0 ] && shell_changed=true
fi

if [ "$result_code" -eq 0 ]; then
    if [ "$login" = "disabled" ]; then
        result_output=$(usermod -L "$user" 2>&1)
        result_code=$?
        [ "$result_code" -eq 0 ] && login_changed=true
    elif [ "$login" = "enabled" ]; then
        result_output=$(usermod -U "$user" 2>&1)
        result_code=$?
        [ "$result_code" -eq 0 ] && login_changed=true
    fi
fi

if [ "$result_code" -eq 0 ]; then
    if [ "$sudo_state" = "present" ]; then
        tmp="$sudo_path.tmp"
        printf '%s ALL=(ALL) NOPASSWD:ALL\n' "$user" > "$tmp"
        chmod 0440 "$tmp"
        result_output=$("$visudo_path" -cf "$tmp" 2>&1)
        result_code=$?
        if [ "$result_code" -eq 0 ]; then
            mv "$tmp" "$sudo_path"
            sudo_changed=true
        else
            rm -f "$tmp"
        fi
    elif [ "$sudo_state" = "absent" ] && [ -e "$sudo_path" ]; then
        result_output=$(rm -f "$sudo_path" 2>&1)
        result_code=$?
        [ "$result_code" -eq 0 ] && sudo_changed=true
    fi
fi

printf 'user=%s\n' "$user"
printf 'shellChanged=%s\n' "$shell_changed"
printf 'loginChanged=%s\n' "$login_changed"
printf 'sudoChanged=%s\n' "$sudo_changed"
printf 'backupPath=%s\n' "$backup_path"
printf 'rollbackConfirmation=user_rollback_permission_change:%s\n' "$user"
printf 'standardErrorSummary=%s\n' "$(printf '%s\n' "$result_output" | sed -n '1{s/[[:cntrl:]]//g;s/^\(.\{0,120\}\).*/\1/;p;}')"
exit "$result_code"
""";

    private const string UserRollbackPermissionChangeScript = """
user="$1"
managed_name=$(printf '%s' "$user" | sed 's/\$/_dollar/g')
sudo_path="/etc/sudoers.d/kelpie-$managed_name"
backup_path="/var/backups/kelpie/users/$user-permissions-latest.meta"

if [ ! -f "$backup_path" ]; then
    printf 'user=%s\n' "$user"
    printf 'backupExists=false\n'
    exit 2
fi

get_meta() {
    key="$1"
    awk -F= -v key="$key" '$1 == key { sub("^[^=]*=", ""); print; exit }' "$backup_path"
}

shell_path=$(get_meta shell)
locked=$(get_meta locked)
sudo_exists=$(get_meta sudoExists)
sudo_payload=$(get_meta sudoBase64)

result_output=""
result_code=0
shell_restored=false
login_restored=false
sudo_restored=false

if [ -n "$shell_path" ] && [ -e "$shell_path" ]; then
    result_output=$(usermod -s "$shell_path" "$user" 2>&1)
    result_code=$?
    [ "$result_code" -eq 0 ] && shell_restored=true
fi

if [ "$result_code" -eq 0 ]; then
    if [ "$locked" = "true" ]; then
        result_output=$(usermod -L "$user" 2>&1)
        result_code=$?
        [ "$result_code" -eq 0 ] && login_restored=true
    elif [ "$locked" = "false" ]; then
        result_output=$(usermod -U "$user" 2>&1)
        result_code=$?
        [ "$result_code" -eq 0 ] && login_restored=true
    fi
fi

if [ "$result_code" -eq 0 ]; then
    if [ "$sudo_exists" = "true" ]; then
        printf '%s' "$sudo_payload" | base64 -d > "$sudo_path"
        chmod 0440 "$sudo_path"
        sudo_restored=true
    else
        rm -f "$sudo_path"
        sudo_restored=true
    fi
fi

printf 'user=%s\n' "$user"
printf 'backupExists=true\n'
printf 'shellRestored=%s\n' "$shell_restored"
printf 'loginRestored=%s\n' "$login_restored"
printf 'sudoRestored=%s\n' "$sudo_restored"
printf 'restored=%s\n' "$([ "$result_code" -eq 0 ] && printf true || printf false)"
printf 'standardErrorSummary=%s\n' "$(printf '%s\n' "$result_output" | sed -n '1{s/[[:cntrl:]]//g;s/^\(.\{0,120\}\).*/\1/;p;}')"
exit "$result_code"
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

    private const string FirewallCheckRuleScript = """
action="$1"
target="$2"
value="$3"
zone="$4"
permanent="$5"

firewall_cmd=$(command -v firewall-cmd 2>/dev/null)
ufw_cmd=$(command -v ufw 2>/dev/null)

printf 'action=%s\n' "$action"
printf 'target=%s\n' "$target"
printf 'value=%s\n' "$value"
printf 'zone=%s\n' "$zone"
printf 'permanent=%s\n' "$permanent"

if [ -n "$firewall_cmd" ]; then
    firewalld_available=true
else
    firewalld_available=false
fi

if [ -n "$ufw_cmd" ]; then
    ufw_available=true
else
    ufw_available=false
fi

printf 'firewalldAvailable=%s\n' "$firewalld_available"
printf 'ufwAvailable=%s\n' "$ufw_available"

valid=true
case "$target:$value" in
    port:*/*) ;;
    port:*) valid=false ;;
    service:*/*) valid=false ;;
esac

printf 'valid=%s\n' "$valid"
[ "$valid" = true ] || exit 2

if [ -n "$firewall_cmd" ]; then
    firewalld_state=$("$firewall_cmd" --state 2>/dev/null)
    if [ "$permanent" = true ]; then
        "$firewall_cmd" --permanent --zone "$zone" "--query-$target" "$value" >/dev/null 2>&1
    else
        "$firewall_cmd" --zone "$zone" "--query-$target" "$value" >/dev/null 2>&1
    fi
    query_code=$?
    if [ "$query_code" -eq 0 ]; then
        rule_present=true
    else
        rule_present=false
    fi
else
    firewalld_state="unavailable"
    rule_present="unknown"
    query_code=127
fi

printf 'firewalldState=%s\n' "$firewalld_state"
printf 'rulePresent=%s\n' "$rule_present"
printf 'queryExitCode=%s\n' "$query_code"
printf 'requiresConfirmation=true\n'
printf 'confirmation=firewall_apply_rule:%s:%s:%s:%s:%s\n' "$action" "$target" "$value" "$zone" "$permanent"
""";

    private const string FirewallApplyRuleScript = """
action="$1"
target="$2"
value="$3"
zone="$4"
permanent="$5"

firewall_cmd=$(command -v firewall-cmd 2>/dev/null)

printf 'action=%s\n' "$action"
printf 'target=%s\n' "$target"
printf 'value=%s\n' "$value"
printf 'zone=%s\n' "$zone"
printf 'permanent=%s\n' "$permanent"

valid=true
case "$target:$value" in
    port:*/*) ;;
    port:*) valid=false ;;
    service:*/*) valid=false ;;
esac

printf 'valid=%s\n' "$valid"
[ "$valid" = true ] || exit 2

if [ -z "$firewall_cmd" ]; then
    printf 'firewalldAvailable=false\n'
    exit 127
fi

printf 'firewalldAvailable=true\n'
operation="--add-$target"
[ "$action" = "remove" ] && operation="--remove-$target"

if [ "$permanent" = true ]; then
    result_output=$("$firewall_cmd" --permanent --zone "$zone" "$operation" "$value" 2>&1)
else
    result_output=$("$firewall_cmd" --zone "$zone" "$operation" "$value" 2>&1)
fi
result_code=$?

printf 'applyExitCode=%s\n' "$result_code"
printf 'changed=%s\n' "$([ "$result_code" -eq 0 ] && printf true || printf false)"
printf 'standardErrorSummary=%s\n' "$(printf '%s\n' "$result_output" | sed -n '1{s/[[:cntrl:]]//g;s/^\(.\{0,120\}\).*/\1/;p;}')"
exit "$result_code"
""";

    private const string BackupPlanCheckScript = """
root="$1"
depth="$2"
limit="$3"

scanned=0
files=0
directories=0
symlinks=0
estimated_bytes=0
exists=false

if [ -e "$root" ]; then
    exists=true
fi

printf 'scanRoot=%s\n' "$root"
printf 'exists=%s\n' "$exists"
printf 'depth=%s\n' "$depth"

if [ "$exists" = true ]; then
    find_depth=$((depth + 1))
    while IFS= read -r path; do
        [ "$scanned" -ge "$limit" ] && break
        [ -n "$path" ] || continue

        scanned=$((scanned + 1))
        if [ -L "$path" ]; then
            symlinks=$((symlinks + 1))
        elif [ -d "$path" ]; then
            directories=$((directories + 1))
        elif [ -f "$path" ]; then
            files=$((files + 1))
            size=$(stat -c '%s' "$path" 2>/dev/null || printf '0')
            estimated_bytes=$((estimated_bytes + size))
        fi
    done <<EOF
$(find "$root" -mindepth 1 -maxdepth "$find_depth" -xdev -print 2>/dev/null)
EOF
fi

printf 'entriesScanned=%s\n' "$scanned"
printf 'files=%s\n' "$files"
printf 'directories=%s\n' "$directories"
printf 'symlinks=%s\n' "$symlinks"
printf 'estimatedBytes=%s\n' "$estimated_bytes"
printf 'requiresConfirmation=true\n'
printf 'confirmation=backup_run:%s\n' "$root"

[ "$exists" = true ] || exit 2
""";

    private const string BackupRunScript = """
root="$1"
depth="$2"
limit="$3"

backup_dir="/var/backups/kelpie/run"
mkdir -p "$backup_dir"
backup_path="$backup_dir/kelpie-backup-$(date +%Y%m%d%H%M%S).tar.gz"
list_path="$backup_path.list"

printf 'scanRoot=%s\n' "$root"
printf 'exists=%s\n' "$([ -e "$root" ] && printf true || printf false)"
printf 'depth=%s\n' "$depth"

if [ ! -e "$root" ]; then
    printf 'backupCreated=false\n'
    exit 2
fi

find_depth=$((depth + 1))
entries=0
bytes_total=0
: > "$list_path"

while IFS= read -r path; do
    [ "$entries" -ge "$limit" ] && break
    [ -f "$path" ] || continue
    [ -L "$path" ] && continue

    size=$(stat -c '%s' "$path" 2>/dev/null || printf '0')
    bytes_total=$((bytes_total + size))
    rel=${path#"$root"/}
    printf '%s\n' "$rel" >> "$list_path"
    entries=$((entries + 1))
done <<EOF
$(find "$root" -mindepth 1 -maxdepth "$find_depth" -xdev -type f -print 2>/dev/null)
EOF

tar_output=$(tar -C "$root" -czf "$backup_path" -T "$list_path" 2>&1)
tar_code=$?
rm -f "$list_path"

if [ "$tar_code" -eq 0 ]; then
    tar -tf "$backup_path" >/dev/null 2>&1
    readable_code=$?
else
    readable_code=$tar_code
fi

printf 'backupCreated=%s\n' "$([ "$tar_code" -eq 0 ] && printf true || printf false)"
printf 'backupPath=%s\n' "$backup_path"
printf 'entriesAdded=%s\n' "$entries"
printf 'bytesAdded=%s\n' "$bytes_total"
printf 'archiveReadable=%s\n' "$([ "$readable_code" -eq 0 ] && printf true || printf false)"
printf 'standardErrorSummary=%s\n' "$(printf '%s\n' "$tar_output" | sed -n '1{s/[[:cntrl:]]//g;s/^\(.\{0,120\}\).*/\1/;p;}')"
exit "$tar_code"
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

    private const string AuditVerifyScript = """
path="$1"
limit="$2"

printf 'auditPath=%s\n' "$path"
if [ ! -f "$path" ]; then
    printf 'exists=false\n'
    exit 2
fi

printf 'exists=true\n'

lines=0
json_lines=0
missing=0
breaks=0
previous=""

while IFS= read -r line || [ -n "$line" ]; do
    [ "$lines" -ge "$limit" ] && break
    lines=$((lines + 1))
    [ -n "$line" ] || continue

    case "$line" in
        \{*\})
            json_lines=$((json_lines + 1))
            current=$(printf '%s\n' "$line" | sed -n 's/.*"hash"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p')
            previous_hash=$(printf '%s\n' "$line" | sed -n 's/.*"prevHash"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p')
            if [ -z "$previous_hash" ]; then
                previous_hash=$(printf '%s\n' "$line" | sed -n 's/.*"previousHash"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p')
            fi

            if [ -z "$current" ] || [ -z "$previous_hash" ]; then
                missing=$((missing + 1))
            elif [ -n "$previous" ] && [ "$previous_hash" != "$previous" ]; then
                breaks=$((breaks + 1))
            fi

            if [ -n "$current" ]; then
                previous="$current"
            fi
            ;;
    esac
done < "$path"

printf 'linesScanned=%s\n' "$lines"
printf 'jsonLines=%s\n' "$json_lines"
printf 'missingHashFields=%s\n' "$missing"
printf 'chainBreaks=%s\n' "$breaks"

[ "$breaks" -eq 0 ] || exit 1
""";

    private const string AuditExportScript = """
path="$1"
limit="$2"

extract_json_value() {
    key="$1"
    line="$2"
    value=$(printf '%s\n' "$line" | sed -n 's/.*"'"$key"'"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p')
    if [ -z "$value" ]; then
        value=$(printf '%s\n' "$line" | sed -n 's/.*"'"$key"'"[[:space:]]*:[[:space:]]*\([^,}"]*\).*/\1/p' | sed 's/^[[:space:]]*//;s/[[:space:]]*$//')
    fi
    printf '%s' "$value" | tr '\r\n' '  ' | cut -c 1-80
}

append_pair() {
    key="$1"
    value="$2"
    [ -n "$value" ] || return
    if [ -z "$pairs" ]; then
        pairs="$key=$value"
    else
        pairs="$pairs,$key=$value"
    fi
}

printf 'exportVersion=1\n'
printf 'auditPath=%s\n' "$path"
if [ ! -f "$path" ]; then
    printf 'exists=false\n'
    exit 2
fi

printf 'exists=true\n'

records=0
while IFS= read -r line || [ -n "$line" ]; do
    [ "$records" -ge "$limit" ] && break
    [ -n "$line" ] || continue

    records=$((records + 1))
    case "$line" in
        \{*\})
            pairs=""
            for key in timestamp eventType toolName commandName exitCode result riskLevel; do
                value=$(extract_json_value "$key" "$line")
                append_pair "$key" "$value"
            done
            printf 'record=%s:%s\n' "$records" "$pairs"
            ;;
        *)
            printf 'record=%s:format=text\n' "$records"
            ;;
    esac
done < "$path"

printf 'records=%s\n' "$records"
""";

    private const string CheckHttpLocalScript = """
port="$1"

if command -v curl >/dev/null 2>&1; then
    curl --max-time 5 --silent --show-error --output /dev/null --write-out 'status=%{http_code}\ncontent_type=%{content_type}\n' "http://127.0.0.1:$port/"
    exit $?
fi

if command -v wget >/dev/null 2>&1; then
    output=$(wget -S -O /dev/null -T 5 "http://127.0.0.1:$port/" 2>&1)
    code=$?
    status=$(printf '%s\n' "$output" | sed -n 's/.*HTTP\/[0-9.]* \([0-9][0-9][0-9]\).*/\1/p' | tail -n 1)
    content_type=$(printf '%s\n' "$output" | sed -n 's/^[[:space:]]*Content-Type:[[:space:]]*//Ip' | tail -n 1)
    printf 'status=%s\n' "$status"
    printf 'content_type=%s\n' "$content_type"
    exit "$code"
fi

echo "curl or wget command was not found" >&2
exit 127
""";

    private const string CheckTcpConnectLocalScript = """
port="$1"

if command -v nc >/dev/null 2>&1; then
    nc -z -w 5 127.0.0.1 "$port"
    code=$?
elif command -v bash >/dev/null 2>&1; then
    timeout 5 bash -c ":</dev/tcp/127.0.0.1/$port" >/dev/null 2>&1
    code=$?
else
    echo "nc or bash command was not found" >&2
    exit 127
fi

if [ "$code" -eq 0 ]; then
    printf 'connected\n'
fi

exit "$code"
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
            CreateEncodedShellCommand(CronListScript, "{limit}"),
            TimeSpan.FromSeconds(20),
            [
                new AllowedCommandParameterDefinition("limit", Pattern: BoundedListLimitPattern),
            ]),
        new(
            "cron_validate",
            CreateEncodedShellCommand(CronValidateScript, "{cronExpression} {runUser} {command} {logPath}"),
            TimeSpan.FromSeconds(10),
            [
                new AllowedCommandParameterDefinition("cronExpression", MaxLength: 128, Pattern: CronExpressionPattern),
                new AllowedCommandParameterDefinition("runUser", Pattern: UserNamePattern),
                new AllowedCommandParameterDefinition("command", MaxLength: 128, Pattern: ShellCommandTextPattern),
                new AllowedCommandParameterDefinition("logPath", MaxLength: 180, Pattern: CronLogPathPattern),
            ]),
        new(
            "cron_check_write",
            CreateEncodedShellCommand(CronCheckWriteScript, "{targetType} {runUser} {cronExpression} {command} {logPath}"),
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
            CreateEncodedSudoShellCommand(CronWriteScript, "{targetType} {runUser} {cronExpression} {command} {logPath}"),
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
            CreateEncodedSudoShellCommand(CronRollbackScript, "{targetType} {runUser}"),
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
            CreateEncodedShellCommand(SudoersCheckScript, "{targetType} {name}"),
            TimeSpan.FromSeconds(10),
            [
                new AllowedCommandParameterDefinition("targetType", Pattern: PrincipalTypePattern),
                new AllowedCommandParameterDefinition("name", Pattern: GroupNamePattern),
            ]),
        new(
            "user_service_usage_check",
            CreateEncodedShellCommand(UserServiceUsageCheckScript, "{targetType} {name} {limit}"),
            TimeSpan.FromSeconds(30),
            [
                new AllowedCommandParameterDefinition("targetType", Pattern: PrincipalTypePattern),
                new AllowedCommandParameterDefinition("name", Pattern: GroupNamePattern),
                new AllowedCommandParameterDefinition("limit", Pattern: BoundedListLimitPattern),
            ]),
        new(
            "user_file_ownership_check",
            CreateEncodedShellCommand(UserFileOwnershipCheckScript, "{targetType} {name} {scanRoot} {depth} {limit}"),
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
            CreateEncodedShellCommand(UserUsageCheckScript, "{targetType} {name} {limit}"),
            TimeSpan.FromSeconds(30),
            [
                new AllowedCommandParameterDefinition("targetType", Pattern: PrincipalTypePattern),
                new AllowedCommandParameterDefinition("name", Pattern: GroupNamePattern),
                new AllowedCommandParameterDefinition("limit", Pattern: BoundedListLimitPattern),
            ]),
        new(
            "user_check_group_change",
            CreateEncodedShellCommand(UserCheckGroupChangeScript, "{user} {groups} {mode}"),
            TimeSpan.FromSeconds(10),
            [
                new AllowedCommandParameterDefinition("user", Pattern: UserNamePattern),
                new AllowedCommandParameterDefinition("groups", MaxLength: 256, Pattern: GroupListPattern),
                new AllowedCommandParameterDefinition("mode", Pattern: GroupChangeModePattern),
            ]),
        new(
            "user_apply_group_change",
            CreateEncodedSudoShellCommand(UserApplyGroupChangeScript, "{user} {groups} {mode}"),
            TimeSpan.FromSeconds(30),
            [
                new AllowedCommandParameterDefinition("user", Pattern: UserNamePattern),
                new AllowedCommandParameterDefinition("groups", MaxLength: 256, Pattern: GroupListPattern),
                new AllowedCommandParameterDefinition("mode", Pattern: GroupChangeModePattern),
            ],
            SshCommandRiskLevel.ConfirmRequired),
        new(
            "user_rollback_group_change",
            CreateEncodedSudoShellCommand(UserRollbackGroupChangeScript, "{user}"),
            TimeSpan.FromSeconds(30),
            [
                new AllowedCommandParameterDefinition("user", Pattern: UserNamePattern),
            ],
            SshCommandRiskLevel.ConfirmRequired),
        new(
            "user_check_permission_change",
            CreateEncodedShellCommand(UserCheckPermissionChangeScript, "{user} {shell} {login} {sudo}"),
            TimeSpan.FromSeconds(10),
            [
                new AllowedCommandParameterDefinition("user", Pattern: UserNamePattern),
                new AllowedCommandParameterDefinition("shell", MaxLength: 96, Pattern: LoginShellPattern),
                new AllowedCommandParameterDefinition("login", Pattern: LoginStatePattern),
                new AllowedCommandParameterDefinition("sudo", Pattern: PermissionStatePattern),
            ]),
        new(
            "user_apply_permission_change",
            CreateEncodedSudoShellCommand(UserApplyPermissionChangeScript, "{user} {shell} {login} {sudo}"),
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
            CreateEncodedSudoShellCommand(UserRollbackPermissionChangeScript, "{user}"),
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
            CreateEncodedShellCommand(FirewallCheckRuleScript, "{action} {target} {value} {zone} {permanent}"),
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
            CreateEncodedSudoShellCommand(FirewallApplyRuleScript, "{action} {target} {value} {zone} {permanent}"),
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
            CreateEncodedShellCommand(BackupPlanCheckScript, "{scanRoot} {depth} {limit}"),
            TimeSpan.FromSeconds(30),
            [
                new AllowedCommandParameterDefinition("scanRoot", MaxLength: 128, Pattern: BackupScanRootPattern),
                new AllowedCommandParameterDefinition("depth", Pattern: DepthPattern),
                new AllowedCommandParameterDefinition("limit", Pattern: BoundedListLimitPattern),
            ]),
        new(
            "backup_run",
            CreateEncodedSudoShellCommand(BackupRunScript, "{scanRoot} {depth} {limit}"),
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
            CreateEncodedShellCommand(AuditVerifyScript, "{logPath} {limit}"),
            TimeSpan.FromSeconds(30),
            [
                new AllowedCommandParameterDefinition("logPath", MaxLength: 180, Pattern: AuditLogPathPattern),
                new AllowedCommandParameterDefinition("limit", Pattern: BoundedListLimitPattern),
            ]),
        new(
            "audit_export",
            CreateEncodedShellCommand(AuditExportScript, "{logPath} {limit}"),
            TimeSpan.FromSeconds(30),
            [
                new AllowedCommandParameterDefinition("logPath", MaxLength: 180, Pattern: AuditLogPathPattern),
                new AllowedCommandParameterDefinition("limit", Pattern: BoundedListLimitPattern),
            ]),
        new(
            "check_http_local",
            CreateEncodedShellCommand(CheckHttpLocalScript, "{port}"),
            TimeSpan.FromSeconds(10),
            [
                new AllowedCommandParameterDefinition("port", Pattern: AllowedCommandPatterns.TcpPort),
            ]),
        new(
            "check_tcp_connect_local",
            CreateEncodedShellCommand(CheckTcpConnectLocalScript, "{port}"),
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

    private static string CreateEncodedSudoShellCommand(string script, string arguments)
    {
        var encoded = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(script));
        return $"sudo -n sh -c \"printf %s '{encoded}' | base64 -d | sh -s -- {arguments}\"";
    }
}
