namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Provides Nginx application configuration commands.
/// </summary>
public sealed class NginxServiceConfigCommandProvider : IAllowedCommandProvider
{
    private const string Base64PathPattern = "^[A-Za-z0-9+/=]{1,4096}$";
    private const string MaxBytesPattern = "^[1-9][0-9]{0,5}$";
    private const string LinesPattern = "^[1-9][0-9]{0,3}$";
    private const string SinceMinutesPattern = "^[0-9]{1,4}$";
    private const string Base64ContentPattern = "^[A-Za-z0-9+/=]+$";
    private const string ReadLogScriptBase64 = "aW1wb3J0IGJhc2U2NCwgZGF0ZXRpbWUsIG9zLCByZSwgc3lzCnAgPSBiYXNlNjQuYjY0ZGVjb2RlKHN5cy5hcmd2WzFdKS5kZWNvZGUoJ3V0Zi04JykKYWxsb3dlZCA9IGJhc2U2NC5iNjRkZWNvZGUoc3lzLmFyZ3ZbMl0pLmRlY29kZSgndXRmLTgnKS5zcGxpdGxpbmVzKCkKbWF4YiA9IGludChzeXMuYXJndlszXSkKbGluZV9saW1pdCA9IGludChzeXMuYXJndls0XSkKc2luY2UgPSBpbnQoc3lzLmFyZ3ZbNV0pCnJwID0gb3MucGF0aC5yZWFscGF0aChwKQphbGxvd2VkX3JlYWwgPSBsaXN0KG1hcChvcy5wYXRoLnJlYWxwYXRoLCBbeCBmb3IgeCBpbiBhbGxvd2VkIGlmIHhdKSkKaWYgcnAgbm90IGluIGFsbG93ZWRfcmVhbDoKICAgIHN5cy5leGl0KCdFUlJPUjogcGF0aCBpcyBub3QgYW4gYWxsb3dlZCBhcHAgbG9nIGZpbGUnKQppZiBub3Qgb3MucGF0aC5pc2ZpbGUocnApOgogICAgc3lzLmV4aXQoJ0VSUk9SOiBsb2cgcGF0aCBpcyBub3QgYSByZWd1bGFyIGZpbGUnKQpzaXplID0gb3MucGF0aC5nZXRzaXplKHJwKQp3aXRoIG9wZW4ocnAsICdyYicpIGFzIGY6CiAgICBmLnNlZWsobWF4KDAsIHNpemUgLSAobWF4YiArIDEpKSkKICAgIGRhdGEgPSBmLnJlYWQobWF4YiArIDEpCmlmIGInXHgwMCcuZGVjb2RlKCd1bmljb2RlX2VzY2FwZScpLmVuY29kZSgnbGF0aW4xJykgaW4gZGF0YToKICAgIHN5cy5leGl0KCdFUlJPUjogYmluYXJ5IGxvZyBmaWxlIGlzIG5vdCBhbGxvd2VkJykKdGV4dCA9IGRhdGFbLW1heGI6XS5kZWNvZGUoJ3V0Zi04JywgJ3JlcGxhY2UnKQppZiBzaXplID4gbWF4YjoKICAgIHN5cy5zdGRlcnIud3JpdGUoJ0tFTFBJRV9UUlVOQ0FURUQ9MVxuJykKcm93cyA9IHRleHQuc3BsaXRsaW5lcygpWy1saW5lX2xpbWl0Ol0KY3V0b2ZmID0gZGF0ZXRpbWUuZGF0ZXRpbWUubm93KGRhdGV0aW1lLnRpbWV6b25lLnV0YykgLSBkYXRldGltZS50aW1lZGVsdGEobWludXRlcz1zaW5jZSkKbW9udGhfbmFtZXMgPSBbJ0phbicsICdGZWInLCAnTWFyJywgJ0FwcicsICdNYXknLCAnSnVuJywgJ0p1bCcsICdBdWcnLCAnU2VwJywgJ09jdCcsICdOb3YnLCAnRGVjJ10KYmFkID0gMApvdXQgPSBbXQpmb3Igcm93IGluIHJvd3M6CiAgICBrZWVwID0gVHJ1ZQogICAgaWYgc2luY2UgPiAwOgogICAgICAgIGtlZXAgPSBGYWxzZQogICAgICAgIG0gPSByZS5zZWFyY2gocidcWyhbMC05XVswLTldKS8oW0EtWmEtel1bQS1aYS16XVtBLVphLXpdKS8oWzAtOV1bMC05XVswLTldWzAtOV0pOihbMC05XVswLTldKTooWzAtOV1bMC05XSk6KFswLTldWzAtOV0pIChbKy1dWzAtOV1bMC05XVswLTldWzAtOV0pXF0nLCByb3cpCiAgICAgICAgaWYgbToKICAgICAgICAgICAgdHogPSBtLmdyb3VwKDcpCiAgICAgICAgICAgIG9mZiA9IGRhdGV0aW1lLnRpbWVkZWx0YShob3Vycz1pbnQodHpbMTozXSksIG1pbnV0ZXM9aW50KHR6WzM6NV0pKQogICAgICAgICAgICBpZiB0elswXSA9PSAnLSc6CiAgICAgICAgICAgICAgICBvZmYgPSAtb2ZmCiAgICAgICAgICAgIG1vbnRoID0gbW9udGhfbmFtZXMuaW5kZXgobS5ncm91cCgyKSkgKyAxIGlmIG0uZ3JvdXAoMikgaW4gbW9udGhfbmFtZXMgZWxzZSAxCiAgICAgICAgICAgIGR0ID0gZGF0ZXRpbWUuZGF0ZXRpbWUoaW50KG0uZ3JvdXAoMykpLCBtb250aCwgaW50KG0uZ3JvdXAoMSkpLCBpbnQobS5ncm91cCg0KSksIGludChtLmdyb3VwKDUpKSwgaW50KG0uZ3JvdXAoNikpLCB0emluZm89ZGF0ZXRpbWUudGltZXpvbmUob2ZmKSkKICAgICAgICAgICAga2VlcCA9IGR0LmFzdGltZXpvbmUoZGF0ZXRpbWUudGltZXpvbmUudXRjKSA+PSBjdXRvZmYKICAgICAgICBlbHNlOgogICAgICAgICAgICBtID0gcmUubWF0Y2gocicoWzAtOV1bMC05XVswLTldWzAtOV0pLyhbMC05XVswLTldKS8oWzAtOV1bMC05XSkgKFswLTldWzAtOV0pOihbMC05XVswLTldKTooWzAtOV1bMC05XSknLCByb3cpCiAgICAgICAgICAgIGlmIG06CiAgICAgICAgICAgICAgICBkdCA9IGRhdGV0aW1lLmRhdGV0aW1lKGludChtLmdyb3VwKDEpKSwgaW50KG0uZ3JvdXAoMikpLCBpbnQobS5ncm91cCgzKSksIGludChtLmdyb3VwKDQpKSwgaW50KG0uZ3JvdXAoNSkpLCBpbnQobS5ncm91cCg2KSkpLmFzdGltZXpvbmUoKQogICAgICAgICAgICAgICAga2VlcCA9IGR0LmFzdGltZXpvbmUoZGF0ZXRpbWUudGltZXpvbmUudXRjKSA+PSBjdXRvZmYKICAgICAgICAgICAgZWxzZToKICAgICAgICAgICAgICAgIGJhZCArPSAxCiAgICBpZiBrZWVwOgogICAgICAgIG91dC5hcHBlbmQocm93KQppZiBzaW5jZSA+IDAgYW5kIGJhZCA+IDA6CiAgICBzeXMuc3RkZXJyLndyaXRlKCdLRUxQSUVfU0lOQ0VfRklMVEVSX1BBUlRJQUw9MVxuJykKc3lzLnN0ZG91dC53cml0ZSgnXG4nLmpvaW4ob3V0KSArICgnXG4nIGlmIG91dCBlbHNlICcnKSk=";
    private const string DisableDefaultSitesScriptBase64 = "aW1wb3J0IGJhc2U2NCxvcyxyZSxzeXMKYmFzZT0nL2V0Yy9uZ2lueC9zaXRlcy1lbmFibGVkJwpiYWNrdXA9Jy9ldGMvbmdpbngvLmtlbHBpZS1kaXNhYmxlZC1zaXRlcycKcG9ydD0nODAnCnJ4PXJlLmNvbXBpbGUocicoP2ltKV5ccypsaXN0ZW5ccytbXjtdKig/PCFcZCknK3JlLmVzY2FwZShwb3J0KStyJyg/IVxkKVteO10qXGJkZWZhdWx0X3NlcnZlclxiW147XSo7JykKZGVmIG1hcmtlcl9wYXRoKG5hbWUpOgogICAgdG9rZW49YmFzZTY0LnVybHNhZmVfYjY0ZW5jb2RlKG5hbWUuZW5jb2RlKCd1dGYtOCcpKS5kZWNvZGUoJ2FzY2lpJykucnN0cmlwKCc9JykKICAgIHJldHVybiBvcy5wYXRoLmpvaW4oYmFja3VwLCB0b2tlbiArICcudGFyZ2V0JykKaWYgbm90IG9zLnBhdGguaXNkaXIoYmFzZSk6CiAgICBzeXMuZXhpdCgwKQpuYW1lcz1zb3J0ZWQob3MubGlzdGRpcihiYXNlKSkKZm9yIG5hbWUgaW4gbmFtZXM6CiAgICBpZiAnLycgaW4gbmFtZSBvciAnXHgwMCcgaW4gbmFtZSBvciBuYW1lIGluICgnLicsJy4uJyk6CiAgICAgICAgc3lzLmV4aXQoJ0VSUk9SOiB1bnNhZmUgc2l0ZXMtZW5hYmxlZCBlbnRyeScpCm1hdGNoZXM9W10KZm9yIG5hbWUgaW4gbmFtZXM6CiAgICBwPW9zLnBhdGguam9pbihiYXNlLG5hbWUpCiAgICBpZiBub3Qgb3MucGF0aC5pc2xpbmsocCk6CiAgICAgICAgY29udGludWUKICAgIG1hcmtlcj1tYXJrZXJfcGF0aChuYW1lKQogICAgaWYgb3MucGF0aC5leGlzdHMobWFya2VyKToKICAgICAgICBzeXMuZXhpdCgnRVJST1I6IGRpc2FibGVkIHNpdGUgbWFya2VyIGFscmVhZHkgZXhpc3RzJykKICAgIHRhcmdldD1vcy5yZWFkbGluayhwKQogICAgaWYgbm90IHRhcmdldCBvciAnXHgwMCcgaW4gdGFyZ2V0OgogICAgICAgIHN5cy5leGl0KCdFUlJPUjogdW5zYWZlIGVuYWJsZWQgc2l0ZSBzeW1saW5rIHRhcmdldCcpCiAgICBycD1vcy5wYXRoLnJlYWxwYXRoKHApCiAgICBpZiBub3Qgb3MucGF0aC5pc2ZpbGUocnApOgogICAgICAgIGNvbnRpbnVlCiAgICB3aXRoIG9wZW4ocnAsJ3JiJykgYXMgZjoKICAgICAgICBkYXRhPWYucmVhZCg2NTUzNikKICAgIGlmIGInXHgwMCcgaW4gZGF0YToKICAgICAgICBzeXMuZXhpdCgnRVJST1I6IGJpbmFyeSBuZ2lueCBzaXRlIGNvbmZpZyBpcyBub3QgYWxsb3dlZCcpCiAgICB0ZXh0PWRhdGEuZGVjb2RlKCd1dGYtOCcsJ3JlcGxhY2UnKQogICAgaWYgcnguc2VhcmNoKHRleHQpOgogICAgICAgIG1hdGNoZXMuYXBwZW5kKChwLHRhcmdldCxtYXJrZXIpKQppZiBtYXRjaGVzOgogICAgaWYgb3MucGF0aC5leGlzdHMoYmFja3VwKSBhbmQgbm90IG9zLnBhdGguaXNkaXIoYmFja3VwKToKICAgICAgICBzeXMuZXhpdCgnRVJST1I6IGRpc2FibGVkIHNpdGUgbWFya2VyIHBhdGggaXMgbm90IGEgZGlyZWN0b3J5JykKICAgIG9zLm1ha2VkaXJzKGJhY2t1cCwgbW9kZT0wbzcwMCwgZXhpc3Rfb2s9VHJ1ZSkKZGlzYWJsZWQ9W10KdHJ5OgogICAgZm9yIHAsdGFyZ2V0LG1hcmtlciBpbiBtYXRjaGVzOgogICAgICAgIHRtcD1tYXJrZXIrJy50bXAnCiAgICAgICAgd2l0aCBvcGVuKHRtcCwndycsZW5jb2Rpbmc9J3V0Zi04JyxuZXdsaW5lPScnKSBhcyBmOgogICAgICAgICAgICBmLndyaXRlKHRhcmdldCkKICAgICAgICAgICAgZi53cml0ZSgnXG4nKQogICAgICAgIG9zLnJlcGxhY2UodG1wLCBtYXJrZXIpCiAgICAgICAgb3MudW5saW5rKHApCiAgICAgICAgZGlzYWJsZWQuYXBwZW5kKChwLG1hcmtlcikpCmV4Y2VwdCBFeGNlcHRpb24gYXMgZXg6CiAgICBmb3IgcCxtYXJrZXIgaW4gcmV2ZXJzZWQoZGlzYWJsZWQpOgogICAgICAgIHRyeToKICAgICAgICAgICAgaWYgbm90IG9zLnBhdGgubGV4aXN0cyhwKSBhbmQgb3MucGF0aC5pc2ZpbGUobWFya2VyKToKICAgICAgICAgICAgICAgIHRhcmdldD1vcGVuKG1hcmtlciwncicsZW5jb2Rpbmc9J3V0Zi04JykucmVhZCgpLnJzdHJpcCgnXG4nKQogICAgICAgICAgICAgICAgb3Muc3ltbGluayh0YXJnZXQscCkKICAgICAgICAgICAgaWYgb3MucGF0aC5pc2ZpbGUobWFya2VyKToKICAgICAgICAgICAgICAgIG9zLnJlbW92ZShtYXJrZXIpCiAgICAgICAgZXhjZXB0IEV4Y2VwdGlvbjoKICAgICAgICAgICAgcGFzcwogICAgc3lzLmV4aXQoJ0VSUk9SOiBmYWlsZWQgdG8gZGlzYWJsZSBuZ2lueCBkZWZhdWx0IHNpdGUgbGluazogJytzdHIoZXgpKQpzeXMuc3Rkb3V0LndyaXRlKCdcbicuam9pbihbcCBmb3IgcCxfLF8gaW4gbWF0Y2hlc10pKQ==";
    private const string RollbackDefaultSitesScriptBase64 = "aW1wb3J0IGJhc2U2NCxvcyxzeXMKYmFzZT0nL2V0Yy9uZ2lueC9zaXRlcy1lbmFibGVkJwpiYWNrdXA9Jy9ldGMvbmdpbngvLmtlbHBpZS1kaXNhYmxlZC1zaXRlcycKZGVmIG1hcmtlcl9wYXRoKG5hbWUpOgogICAgdG9rZW49YmFzZTY0LnVybHNhZmVfYjY0ZW5jb2RlKG5hbWUuZW5jb2RlKCd1dGYtOCcpKS5kZWNvZGUoJ2FzY2lpJykucnN0cmlwKCc9JykKICAgIHJldHVybiBvcy5wYXRoLmpvaW4oYmFja3VwLCB0b2tlbiArICcudGFyZ2V0JykKcGF0aHM9YmFzZTY0LmI2NGRlY29kZShzeXMuYXJndlsxXSkuZGVjb2RlKCd1dGYtOCcpLnNwbGl0bGluZXMoKQpyZXN0b3JlZD1bXQpyZWNvcmRzPVtdCmZvciBwIGluIHBhdGhzOgogICAgaWYgbm90IHAuc3RhcnRzd2l0aChiYXNlICsgJy8nKSBvciAnXHgwMCcgaW4gcCBvciAnLy4uLycgaW4gcDoKICAgICAgICBzeXMuZXhpdCgnRVJST1I6IHVuc2FmZSBkaXNhYmxlZCBzaXRlIHBhdGgnKQogICAgbmFtZT1vcy5wYXRoLmJhc2VuYW1lKHApCiAgICBpZiBuYW1lIGluICgnJywnLicsJy4uJykgb3IgJy8nIGluIG5hbWU6CiAgICAgICAgc3lzLmV4aXQoJ0VSUk9SOiB1bnNhZmUgZGlzYWJsZWQgc2l0ZSBuYW1lJykKICAgIG1hcmtlcj1tYXJrZXJfcGF0aChuYW1lKQogICAgaWYgbm90IG9zLnBhdGguaXNmaWxlKG1hcmtlcik6CiAgICAgICAgc3lzLmV4aXQoJ0VSUk9SOiBkaXNhYmxlZCBzaXRlIG1hcmtlciBpcyBub3QgYXZhaWxhYmxlJykKICAgIHRhcmdldD1vcGVuKG1hcmtlciwncicsZW5jb2Rpbmc9J3V0Zi04JykucmVhZCgpLnJzdHJpcCgnXG4nKQogICAgaWYgbm90IHRhcmdldCBvciAnXHgwMCcgaW4gdGFyZ2V0OgogICAgICAgIHN5cy5leGl0KCdFUlJPUjogdW5zYWZlIGRpc2FibGVkIHNpdGUgdGFyZ2V0JykKICAgIGlmIG9zLnBhdGgubGV4aXN0cyhwKToKICAgICAgICBzeXMuZXhpdCgnRVJST1I6IG9yaWdpbmFsIHNpdGUgbGluayBhbHJlYWR5IGV4aXN0cycpCiAgICByZWNvcmRzLmFwcGVuZCgocCx0YXJnZXQsbWFya2VyKSkKZm9yIHAsdGFyZ2V0LG1hcmtlciBpbiByZWNvcmRzOgogICAgb3Muc3ltbGluayh0YXJnZXQscCkKICAgIG9zLnJlbW92ZShtYXJrZXIpCiAgICByZXN0b3JlZC5hcHBlbmQocCkKc3lzLnN0ZG91dC53cml0ZSgnXG4nLmpvaW4ocmVzdG9yZWQpKQ==";

    private static readonly AllowedCommandDefinition[] Commands =
    [
        new("service_config_nginx_version", "nginx -V", TimeSpan.FromSeconds(10)),
        new(
            "service_config_nginx_test_config",
            "sudo -n nginx -t",
            TimeSpan.FromSeconds(30),
            RiskLevel: SshCommandRiskLevel.ConfirmRequired),
        new(
            "service_config_nginx_disable_default_sites",
            CreateEncodedPythonStdinCommand(DisableDefaultSitesScriptBase64, string.Empty, sudo: true),
            TimeSpan.FromSeconds(30),
            RiskLevel: SshCommandRiskLevel.ConfirmRequired),
        new(
            "service_config_nginx_rollback_default_sites",
            CreateEncodedPythonStdinCommand(RollbackDefaultSitesScriptBase64, "{disabledPathsBase64}", sudo: true),
            TimeSpan.FromSeconds(30),
            [
                new AllowedCommandParameterDefinition("disabledPathsBase64", MaxLength: 4096, Pattern: Base64PathPattern),
            ],
            SshCommandRiskLevel.ConfirmRequired),
        new(
            "service_config_nginx_read_config",
            CreateEncodedPythonScriptCommand("import base64,os,sys; p=base64.b64decode(sys.argv[1]).decode('utf-8'); allowed=base64.b64decode(sys.argv[2]).decode('utf-8').splitlines(); dirs=base64.b64decode(sys.argv[3]).decode('utf-8').splitlines(); rp=os.path.realpath(p); parent=os.path.realpath(os.path.dirname(p)); allowed_real=list(map(os.path.realpath,[x for x in allowed if x])); dir_real=list(map(os.path.realpath,[x for x in dirs if x])); ok=(rp in allowed_real) or (parent in dir_real and os.path.dirname(rp)==parent); sys.exit('ERROR: path is not an allowed service config file') if not ok else None; sys.exit('ERROR: config path is not a regular file') if not os.path.isfile(rp) else None; maxb=int(sys.argv[4]); data=open(rp,'rb').read(maxb+1); sys.exit('ERROR: binary config file is not allowed') if b'\\x00' in data else None; text=data[:maxb].decode('utf-8'); sys.stderr.write('KELPIE_TRUNCATED=1\\n') if len(data) > maxb else None; sys.stdout.write(text)", "{pathBase64} {allowedPathsBase64} {allowedDirsBase64} {maxBytes}", sudo: false),
            TimeSpan.FromSeconds(10),
            [
                new AllowedCommandParameterDefinition("pathBase64", MaxLength: 4096, Pattern: Base64PathPattern),
                new AllowedCommandParameterDefinition("allowedPathsBase64", MaxLength: 4096, Pattern: Base64PathPattern),
                new AllowedCommandParameterDefinition("allowedDirsBase64", MaxLength: 4096, Pattern: Base64PathPattern),
                new AllowedCommandParameterDefinition("maxBytes", MaxLength: 6, Pattern: MaxBytesPattern),
            ]),
        new(
            "service_config_nginx_write_config",
            CreateEncodedPythonCommand("import base64,os,shutil,sys; marker='KELPIE_CREATED_CONFIG_FILE_BACKUP_V1\\n'; p=base64.b64decode(sys.argv[1]).decode('utf-8'); allowed=base64.b64decode(sys.argv[2]).decode('utf-8').splitlines(); dirs=base64.b64decode(sys.argv[3]).decode('utf-8').splitlines(); rp=os.path.realpath(p); parent=os.path.realpath(os.path.dirname(p)); allowed_real=list(map(os.path.realpath,[x for x in allowed if x])); dir_real=list(map(os.path.realpath,[x for x in dirs if x])); ok=(rp in allowed_real) or (parent in dir_real and os.path.dirname(rp)==parent); sys.exit('ERROR: path is not an allowed service config file') if not ok else None; exists=os.path.exists(rp); sys.exit('ERROR: config path is not a regular file') if exists and not os.path.isfile(rp) else None; sys.exit('ERROR: config parent directory is not available') if not os.path.isdir(parent) else None; bak=p+'.kelpiebakup'; bak_parent=os.path.realpath(os.path.dirname(bak)); sys.exit('ERROR: backup path is not in config parent directory') if bak_parent != parent else None; data=base64.b64decode(sys.stdin.read(), validate=True); text=data.decode('utf-8'); sys.exit('ERROR: binary config file is not allowed') if '\\x00' in text else None; (shutil.copy2(rp,bak) if exists else open(bak,'w',encoding='utf-8',newline='').write(marker)) if not os.path.exists(bak) else None; open(p,'w',encoding='utf-8',newline='').write(text); sys.stdout.write(str(len(data)))", "{pathBase64} {allowedPathsBase64} {allowedDirsBase64}", sudo: true),
            TimeSpan.FromSeconds(30),
            [
                new AllowedCommandParameterDefinition("pathBase64", MaxLength: 4096, Pattern: Base64PathPattern),
                new AllowedCommandParameterDefinition("allowedPathsBase64", MaxLength: 4096, Pattern: Base64PathPattern),
                new AllowedCommandParameterDefinition("allowedDirsBase64", MaxLength: 4096, Pattern: Base64PathPattern),
            ],
            SshCommandRiskLevel.ConfirmRequired),
        new(
            "service_config_nginx_check_write_config",
            CreateEncodedPythonScriptCommand("import base64,os,sys; p=base64.b64decode(sys.argv[1]).decode('utf-8'); allowed=base64.b64decode(sys.argv[2]).decode('utf-8').splitlines(); dirs=base64.b64decode(sys.argv[3]).decode('utf-8').splitlines(); rp=os.path.realpath(p); parent=os.path.realpath(os.path.dirname(p)); allowed_real=list(map(os.path.realpath,[x for x in allowed if x])); dir_real=list(map(os.path.realpath,[x for x in dirs if x])); ok=(rp in allowed_real) or (parent in dir_real and os.path.dirname(rp)==parent); sys.exit('ERROR: path is not an allowed service config file') if not ok else None; exists=os.path.exists(rp); sys.exit('ERROR: config path is not a regular file') if exists and not os.path.isfile(rp) else None; sys.exit('ERROR: config parent directory is not available') if not os.path.isdir(parent) else None; bak=p+'.kelpiebakup'; bak_parent=os.path.realpath(os.path.dirname(bak)); sys.exit('ERROR: backup path is not in config parent directory') if bak_parent != parent else None; sys.exit('ERROR: config backup path exists but is not a regular file') if os.path.exists(bak) and not os.path.isfile(bak) else None; (open(rp,'r+b').close() if exists else None); sys.exit('ERROR: config parent directory is not writable') if not os.path.exists(bak) and not os.access(parent, os.W_OK) else None; sys.stdout.write('1')", "{pathBase64} {allowedPathsBase64} {allowedDirsBase64}", sudo: true),
            TimeSpan.FromSeconds(10),
            [
                new AllowedCommandParameterDefinition("pathBase64", MaxLength: 4096, Pattern: Base64PathPattern),
                new AllowedCommandParameterDefinition("allowedPathsBase64", MaxLength: 4096, Pattern: Base64PathPattern),
                new AllowedCommandParameterDefinition("allowedDirsBase64", MaxLength: 4096, Pattern: Base64PathPattern),
            ]),
        new(
            "service_config_nginx_rollback_config",
            CreateEncodedPythonScriptCommand("import base64,os,sys; marker=b'KELPIE_CREATED_CONFIG_FILE_BACKUP_V1\\n'; p=base64.b64decode(sys.argv[1]).decode('utf-8'); allowed=base64.b64decode(sys.argv[2]).decode('utf-8').splitlines(); dirs=base64.b64decode(sys.argv[3]).decode('utf-8').splitlines(); rp=os.path.realpath(p); parent=os.path.realpath(os.path.dirname(p)); allowed_real=list(map(os.path.realpath,[x for x in allowed if x])); dir_real=list(map(os.path.realpath,[x for x in dirs if x])); ok=(rp in allowed_real) or (parent in dir_real and os.path.dirname(rp)==parent); sys.exit('ERROR: path is not an allowed service config file') if not ok else None; bak=p+'.kelpiebakup'; bak_parent=os.path.realpath(os.path.dirname(bak)); sys.exit('ERROR: backup path is not in config parent directory') if bak_parent != parent else None; sys.exit('ERROR: config backup file is not available') if not os.path.isfile(bak) else None; data=open(bak,'rb').read(); sys.exit('ERROR: binary config backup file is not allowed') if b'\\x00' in data else None; data.decode('utf-8'); (os.remove(p) if os.path.exists(p) else None) if data == marker else os.replace(bak,p); os.remove(bak) if data == marker and os.path.exists(bak) else None; sys.stdout.write(str(len(data)))", "{pathBase64} {allowedPathsBase64} {allowedDirsBase64}", sudo: true),
            TimeSpan.FromSeconds(30),
            [
                new AllowedCommandParameterDefinition("pathBase64", MaxLength: 4096, Pattern: Base64PathPattern),
                new AllowedCommandParameterDefinition("allowedPathsBase64", MaxLength: 4096, Pattern: Base64PathPattern),
                new AllowedCommandParameterDefinition("allowedDirsBase64", MaxLength: 4096, Pattern: Base64PathPattern),
            ],
            SshCommandRiskLevel.ConfirmRequired),
        new(
            "service_config_nginx_commit_config",
            CreateEncodedPythonScriptCommand("import base64,os,sys; p=base64.b64decode(sys.argv[1]).decode('utf-8'); allowed=base64.b64decode(sys.argv[2]).decode('utf-8').splitlines(); dirs=base64.b64decode(sys.argv[3]).decode('utf-8').splitlines(); rp=os.path.realpath(p); parent=os.path.realpath(os.path.dirname(p)); allowed_real=list(map(os.path.realpath,[x for x in allowed if x])); dir_real=list(map(os.path.realpath,[x for x in dirs if x])); ok=(rp in allowed_real) or (parent in dir_real and os.path.dirname(rp)==parent); sys.exit('ERROR: path is not an allowed service config file') if not ok else None; bak=p+'.kelpiebakup'; bak_parent=os.path.realpath(os.path.dirname(bak)); sys.exit('ERROR: backup path is not in config parent directory') if bak_parent != parent else None; sys.exit('ERROR: config backup file is not available') if not os.path.isfile(bak) else None; os.remove(bak); sys.stdout.write('1')", "{pathBase64} {allowedPathsBase64} {allowedDirsBase64}", sudo: true),
            TimeSpan.FromSeconds(30),
            [
                new AllowedCommandParameterDefinition("pathBase64", MaxLength: 4096, Pattern: Base64PathPattern),
                new AllowedCommandParameterDefinition("allowedPathsBase64", MaxLength: 4096, Pattern: Base64PathPattern),
                new AllowedCommandParameterDefinition("allowedDirsBase64", MaxLength: 4096, Pattern: Base64PathPattern),
            ],
            SshCommandRiskLevel.ConfirmRequired),
        new(
            "service_logfile_nginx_read",
            CreateEncodedPythonStdinCommand(ReadLogScriptBase64, "{pathBase64} {allowedPathsBase64} {maxBytes} {lines} {sinceMinutes}", sudo: false),
            TimeSpan.FromSeconds(10),
            [
                new AllowedCommandParameterDefinition("pathBase64", MaxLength: 4096, Pattern: Base64PathPattern),
                new AllowedCommandParameterDefinition("allowedPathsBase64", MaxLength: 4096, Pattern: Base64PathPattern),
                new AllowedCommandParameterDefinition("maxBytes", MaxLength: 6, Pattern: MaxBytesPattern),
                new AllowedCommandParameterDefinition("lines", MaxLength: 4, Pattern: LinesPattern),
                new AllowedCommandParameterDefinition("sinceMinutes", MaxLength: 4, Pattern: SinceMinutesPattern),
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

    private static string CreateEncodedPythonScriptCommand(string script, string arguments, bool sudo)
    {
        var encoded = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(script));
        return CreateEncodedPythonStdinCommand(encoded, arguments, sudo);
    }

    private static string CreateEncodedPythonStdinCommand(string encodedScript, string arguments, bool sudo)
    {
        var argumentSuffix = string.IsNullOrWhiteSpace(arguments) ? string.Empty : " " + arguments;
        var command = $"sh -c \"printf %s '{encodedScript}' | base64 -d | python3 - --{argumentSuffix}\"";
        return sudo ? "sudo -n " + command : command;
    }

    private static string CreateEncodedPythonCommand(string script, string arguments, bool sudo)
    {
        var encoded = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(script));
        var argumentSuffix = string.IsNullOrWhiteSpace(arguments) ? string.Empty : " " + arguments;
        var command = $"sh -c \"python3 -c \\\"$(printf %s '{encoded}' | base64 -d)\\\" --{argumentSuffix}\"";
        return sudo ? "sudo -n " + command : command;
    }
}
