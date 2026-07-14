namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Provides internal web public file commands.
/// </summary>
public sealed class WebPublicFileCommandProvider : IAllowedCommandProvider
{
    private const string Base64PathPattern = "^[A-Za-z0-9+/=]{1,4096}$";
    private const string Base64ContentPattern = "^[A-Za-z0-9+/=]+$";
    private const string MaxBytesPattern = "^[1-9][0-9]{0,7}$";
    private const string MaxLinesPattern = "^[0-9]{1,4}$";
    private const string MaxDepthPattern = "^[0-5]$";
    private const string LimitPattern = "^[1-9][0-9]{0,2}$";
    private const string SliceModePattern = "^(head|tail)$";
    private const string CreateDirectoriesPattern = "^[01]$";
    private const string RecursivePattern = "^[01]$";
    private const string ModePattern = "^[0-7]{3}$";
    private const string HelperPath = "/usr/local/libexec/kelpie/kelpie-web-permission-helper";
    private const string ListScriptBase64 = "aW1wb3J0IGJhc2U2NCxqc29uLG9zLHB3ZCxncnAsc3RhdCxzeXMsZGF0ZXRpbWUKc2l0ZV9yb290PWJhc2U2NC5iNjRkZWNvZGUoc3lzLmFyZ3ZbMV0pLmRlY29kZSgndXRmLTgnKQpyZWw9YmFzZTY0LmI2NGRlY29kZShzeXMuYXJndlsyXSkuZGVjb2RlKCd1dGYtOCcpCm1heF9kZXB0aD1pbnQoc3lzLmFyZ3ZbM10pCmxpbWl0PWludChzeXMuYXJndls0XSkKcm9vdF9yZWFsPW9zLnBhdGgucmVhbHBhdGgoc2l0ZV9yb290KQpwYXJ0cz1bcCBmb3IgcCBpbiByZWwucmVwbGFjZSgnXFwnLCcvJykuc3BsaXQoJy8nKSBpZiBwXQppZiBhbnkocCA9PSAnLi4nIGZvciBwIGluIHBhcnRzKToKICAgIHN5cy5leGl0KCdFUlJPUjogaW52YWxpZCB3ZWIgcHVibGljIGRpcmVjdG9yeSBwYXRoJykKdGFyZ2V0PW9zLnBhdGguam9pbihyb290X3JlYWwsKnBhcnRzKQpyZXNvbHZlZD1vcy5wYXRoLnJlYWxwYXRoKHRhcmdldCkKaWYgb3MucGF0aC5jb21tb25wYXRoKFtyb290X3JlYWwscmVzb2x2ZWRdKSAhPSByb290X3JlYWw6CiAgICBzeXMuZXhpdCgnRVJST1I6IHJlc29sdmVkIHBhdGggaXMgb3V0c2lkZSB3ZWIgcHVibGljIHJvb3QnKQppZiBub3Qgb3MucGF0aC5leGlzdHMocmVzb2x2ZWQpOgogICAgcHJpbnQoanNvbi5kdW1wcyh7J3Jlc29sdmVkUGF0aCc6cmVzb2x2ZWQsJ2V4aXN0cyc6RmFsc2UsJ2VudHJpZXMnOltdfSxzZXBhcmF0b3JzPSgnLCcsJzonKSkpCiAgICBzeXMuZXhpdCgwKQppZiBub3Qgb3MucGF0aC5pc2RpcihyZXNvbHZlZCk6CiAgICBzeXMuZXhpdCgnRVJST1I6IHdlYiBwdWJsaWMgcGF0aCBpcyBub3QgYSBkaXJlY3RvcnknKQppZiBtYXhfZGVwdGggPCAwIG9yIG1heF9kZXB0aCA+IDU6CiAgICBzeXMuZXhpdCgnRVJST1I6IG1heERlcHRoIGlzIG91dCBvZiByYW5nZScpCmlmIGxpbWl0IDwgMSBvciBsaW1pdCA+IDUwMDoKICAgIHN5cy5leGl0KCdFUlJPUjogbGltaXQgaXMgb3V0IG9mIHJhbmdlJykKb3V0PVtdCnRydW5jYXRlZD1GYWxzZQpkZWYgb3duZXJfbmFtZShzdCk6CiAgICB0cnk6IHJldHVybiBwd2QuZ2V0cHd1aWQoc3Quc3RfdWlkKS5wd19uYW1lCiAgICBleGNlcHQgS2V5RXJyb3I6IHJldHVybiBzdHIoc3Quc3RfdWlkKQpkZWYgZ3JvdXBfbmFtZShzdCk6CiAgICB0cnk6IHJldHVybiBncnAuZ2V0Z3JnaWQoc3Quc3RfZ2lkKS5ncl9uYW1lCiAgICBleGNlcHQgS2V5RXJyb3I6IHJldHVybiBzdHIoc3Quc3RfZ2lkKQpkZWYgYWRkX2VudHJ5KHBhdGgsIGRlcHRoKToKICAgIGdsb2JhbCB0cnVuY2F0ZWQKICAgIGlmIGxlbihvdXQpID49IGxpbWl0OgogICAgICAgIHRydW5jYXRlZD1UcnVlCiAgICAgICAgcmV0dXJuIEZhbHNlCiAgICBzdD1vcy5sc3RhdChwYXRoKQogICAgcnA9b3MucGF0aC5yZWFscGF0aChwYXRoKQogICAgaWYgb3MucGF0aC5jb21tb25wYXRoKFtyb290X3JlYWwscnBdKSAhPSByb290X3JlYWw6CiAgICAgICAgcmV0dXJuIFRydWUKICAgIG5hbWU9b3MucGF0aC5iYXNlbmFtZShwYXRoKQogICAgcmVscGF0aD0nLycgKyBvcy5wYXRoLnJlbHBhdGgocGF0aCwgcm9vdF9yZWFsKS5yZXBsYWNlKG9zLnNlcCwnLycpCiAgICBpZiByZWxwYXRoID09ICcvLic6IHJlbHBhdGg9Jy8nCiAgICBtb2RlPXN0YXQuU19JTU9ERShzdC5zdF9tb2RlKQogICAgaXNfbGluaz1zdGF0LlNfSVNMTksoc3Quc3RfbW9kZSkKICAgIHR5cD0nc3ltbGluaycgaWYgaXNfbGluayBlbHNlICdkaXJlY3RvcnknIGlmIHN0YXQuU19JU0RJUihzdC5zdF9tb2RlKSBlbHNlICdmaWxlJyBpZiBzdGF0LlNfSVNSRUcoc3Quc3RfbW9kZSkgZWxzZSAnb3RoZXInCiAgICBvdXQuYXBwZW5kKHsnbmFtZSc6bmFtZSwncGF0aCc6cmVscGF0aCwncmVzb2x2ZWRQYXRoJzpycCwndHlwZSc6dHlwLCdzaXplJzpzdC5zdF9zaXplLCdtb2RlJzpmb3JtYXQobW9kZSwnMDNvJyksJ293bmVyJzpvd25lcl9uYW1lKHN0KSwnZ3JvdXAnOmdyb3VwX25hbWUoc3QpLCdsYXN0TW9kaWZpZWQnOmRhdGV0aW1lLmRhdGV0aW1lLmZyb210aW1lc3RhbXAoc3Quc3RfbXRpbWUsZGF0ZXRpbWUudGltZXpvbmUudXRjKS5pc29mb3JtYXQoKS5yZXBsYWNlKCcrMDA6MDAnLCdaJyksJ2RlcHRoJzpkZXB0aCwnaXNTeW1saW5rJzppc19saW5rfSkKICAgIHJldHVybiBUcnVlCmlmIG1heF9kZXB0aCA9PSAwOgogICAgZW50cmllcz1bb3MucGF0aC5qb2luKHJlc29sdmVkLG4pIGZvciBuIGluIHNvcnRlZChvcy5saXN0ZGlyKHJlc29sdmVkKSldCiAgICBmb3IgcCBpbiBlbnRyaWVzOgogICAgICAgIGlmIG5vdCBhZGRfZW50cnkocCwwKTogYnJlYWsKZWxzZToKICAgIGZvciBjdXJyZW50LCBkaXJzLCBmaWxlcyBpbiBvcy53YWxrKHJlc29sdmVkLCBmb2xsb3dsaW5rcz1GYWxzZSk6CiAgICAgICAgZGVwdGg9MCBpZiBjdXJyZW50ID09IHJlc29sdmVkIGVsc2UgbGVuKG9zLnBhdGgucmVscGF0aChjdXJyZW50LHJlc29sdmVkKS5zcGxpdChvcy5zZXApKQogICAgICAgIGlmIGRlcHRoID49IG1heF9kZXB0aDoKICAgICAgICAgICAgZGlyc1s6XSA9IFtdCiAgICAgICAgZm9yIG5hbWUgaW4gc29ydGVkKGRpcnMgKyBmaWxlcyk6CiAgICAgICAgICAgIHA9b3MucGF0aC5qb2luKGN1cnJlbnQsbmFtZSkKICAgICAgICAgICAgZW50cnlfZGVwdGg9MCBpZiBjdXJyZW50ID09IHJlc29sdmVkIGVsc2UgZGVwdGgKICAgICAgICAgICAgaWYgbm90IGFkZF9lbnRyeShwLCBlbnRyeV9kZXB0aCk6CiAgICAgICAgICAgICAgICBkaXJzWzpdID0gW10KICAgICAgICAgICAgICAgIGJyZWFrCiAgICAgICAgaWYgdHJ1bmNhdGVkOgogICAgICAgICAgICBicmVhawpwcmludChqc29uLmR1bXBzKHsncmVzb2x2ZWRQYXRoJzpyZXNvbHZlZCwnZXhpc3RzJzpUcnVlLCdlbnRyaWVzJzpvdXQsJ3RydW5jYXRlZCc6dHJ1bmNhdGVkfSxzZXBhcmF0b3JzPSgnLCcsJzonKSkp";
    private const string StatScriptBase64 = "aW1wb3J0IGJhc2U2NCxqc29uLG9zLHB3ZCxncnAsc3RhdCxzeXMsZGF0ZXRpbWUKc2l0ZV9yb290PWJhc2U2NC5iNjRkZWNvZGUoc3lzLmFyZ3ZbMV0pLmRlY29kZSgndXRmLTgnKQpyZWw9YmFzZTY0LmI2NGRlY29kZShzeXMuYXJndlsyXSkuZGVjb2RlKCd1dGYtOCcpCnJvb3RfcmVhbD1vcy5wYXRoLnJlYWxwYXRoKHNpdGVfcm9vdCkKcGFydHM9W3AgZm9yIHAgaW4gcmVsLnJlcGxhY2UoJ1xcJywnLycpLnNwbGl0KCcvJykgaWYgcF0KaWYgYW55KHAgPT0gJy4uJyBmb3IgcCBpbiBwYXJ0cyk6CiAgICBzeXMuZXhpdCgnRVJST1I6IGludmFsaWQgd2ViIHB1YmxpYyBwYXRoJykKdGFyZ2V0PW9zLnBhdGguam9pbihyb290X3JlYWwsKnBhcnRzKQpyZXNvbHZlZD1vcy5wYXRoLnJlYWxwYXRoKHRhcmdldCkKaWYgb3MucGF0aC5jb21tb25wYXRoKFtyb290X3JlYWwscmVzb2x2ZWRdKSAhPSByb290X3JlYWw6CiAgICBzeXMuZXhpdCgnRVJST1I6IHJlc29sdmVkIHBhdGggaXMgb3V0c2lkZSB3ZWIgcHVibGljIHJvb3QnKQppZiBub3Qgb3MucGF0aC5leGlzdHModGFyZ2V0KSBhbmQgbm90IG9zLnBhdGguaXNsaW5rKHRhcmdldCk6CiAgICBwcmludChqc29uLmR1bXBzKHsncmVzb2x2ZWRQYXRoJzpyZXNvbHZlZCwnZXhpc3RzJzpGYWxzZX0sc2VwYXJhdG9ycz0oJywnLCc6JykpKQogICAgc3lzLmV4aXQoMCkKc3Q9b3MubHN0YXQodGFyZ2V0KQpkZWYgb3duZXJfbmFtZShzdCk6CiAgICB0cnk6IHJldHVybiBwd2QuZ2V0cHd1aWQoc3Quc3RfdWlkKS5wd19uYW1lCiAgICBleGNlcHQgS2V5RXJyb3I6IHJldHVybiBzdHIoc3Quc3RfdWlkKQpkZWYgZ3JvdXBfbmFtZShzdCk6CiAgICB0cnk6IHJldHVybiBncnAuZ2V0Z3JnaWQoc3Quc3RfZ2lkKS5ncl9uYW1lCiAgICBleGNlcHQgS2V5RXJyb3I6IHJldHVybiBzdHIoc3Quc3RfZ2lkKQppc19saW5rPXN0YXQuU19JU0xOSyhzdC5zdF9tb2RlKQp0eXA9J3N5bWxpbmsnIGlmIGlzX2xpbmsgZWxzZSAnZGlyZWN0b3J5JyBpZiBzdGF0LlNfSVNESVIoc3Quc3RfbW9kZSkgZWxzZSAnZmlsZScgaWYgc3RhdC5TX0lTUkVHKHN0LnN0X21vZGUpIGVsc2UgJ290aGVyJwpwcmludChqc29uLmR1bXBzKHsncmVzb2x2ZWRQYXRoJzpyZXNvbHZlZCwnZXhpc3RzJzpUcnVlLCd0eXBlJzp0eXAsJ3NpemUnOnN0LnN0X3NpemUsJ21vZGUnOmZvcm1hdChzdGF0LlNfSU1PREUoc3Quc3RfbW9kZSksJzAzbycpLCdvd25lcic6b3duZXJfbmFtZShzdCksJ2dyb3VwJzpncm91cF9uYW1lKHN0KSwnbGFzdE1vZGlmaWVkJzpkYXRldGltZS5kYXRldGltZS5mcm9tdGltZXN0YW1wKHN0LnN0X210aW1lLGRhdGV0aW1lLnRpbWV6b25lLnV0YykuaXNvZm9ybWF0KCkucmVwbGFjZSgnKzAwOjAwJywnWicpLCdpc1N5bWxpbmsnOmlzX2xpbmt9LHNlcGFyYXRvcnM9KCcsJywnOicpKSk=";
    private const string HashScriptBase64 = "aW1wb3J0IGJhc2U2NAppbXBvcnQgZ3JwCmltcG9ydCBoYXNobGliCmltcG9ydCBqc29uCmltcG9ydCBvcwppbXBvcnQgcHdkCmltcG9ydCBzdGF0CmltcG9ydCBzeXMKCnJvb3QgPSBiYXNlNjQuYjY0ZGVjb2RlKHN5cy5hcmd2WzFdKS5kZWNvZGUoInV0Zi04IikKcmVsYXRpdmUgPSBiYXNlNjQuYjY0ZGVjb2RlKHN5cy5hcmd2WzJdKS5kZWNvZGUoInV0Zi04IikKbWF4aW11bSA9IGludChzeXMuYXJndlszXSkKCmRlZiBlbWl0X2Vycm9yKGNvZGUpOgogICAgcHJpbnQoanNvbi5kdW1wcyh7ImVycm9yQ29kZSI6IGNvZGV9LCBzZXBhcmF0b3JzPSgiLCIsICI6IikpKQogICAgc3lzLmV4aXQoMCkKCnRyeToKICAgIHJvb3RfcmVhbCA9IG9zLnBhdGgucmVhbHBhdGgocm9vdCkKICAgIHBhcnRzID0gW3BhcnQgZm9yIHBhcnQgaW4gcmVsYXRpdmUucmVwbGFjZSgiXFwiLCAiLyIpLnNwbGl0KCIvIikgaWYgcGFydF0KICAgIGlmIG5vdCBwYXJ0cyBvciBhbnkocGFydCA9PSAiLi4iIGZvciBwYXJ0IGluIHBhcnRzKToKICAgICAgICBlbWl0X2Vycm9yKCJpbnZhbGlkLXBhdGgiKQogICAgdGFyZ2V0ID0gb3MucGF0aC5qb2luKHJvb3RfcmVhbCwgKnBhcnRzKQogICAgdHJ5OgogICAgICAgIGJlZm9yZSA9IG9zLmxzdGF0KHRhcmdldCkKICAgIGV4Y2VwdCBGaWxlTm90Rm91bmRFcnJvcjoKICAgICAgICBlbWl0X2Vycm9yKCJmaWxlLW5vdC1mb3VuZCIpCiAgICBpZiBzdGF0LlNfSVNMTksoYmVmb3JlLnN0X21vZGUpIG9yIG5vdCBzdGF0LlNfSVNSRUcoYmVmb3JlLnN0X21vZGUpOgogICAgICAgIGVtaXRfZXJyb3IoImZpbGUtdHlwZS1ub3Qtc3VwcG9ydGVkIikKICAgIHJlc29sdmVkID0gb3MucGF0aC5yZWFscGF0aCh0YXJnZXQpCiAgICBpZiBvcy5wYXRoLmNvbW1vbnBhdGgoW3Jvb3RfcmVhbCwgcmVzb2x2ZWRdKSAhPSByb290X3JlYWw6CiAgICAgICAgZW1pdF9lcnJvcigicGF0aC1vdXRzaWRlLXJvb3QiKQogICAgaWYgYmVmb3JlLnN0X3NpemUgPiBtYXhpbXVtOgogICAgICAgIGVtaXRfZXJyb3IoImZpbGUtdG9vLWxhcmdlIikKICAgIGZsYWdzID0gb3MuT19SRE9OTFkgfCBnZXRhdHRyKG9zLCAiT19OT0ZPTExPVyIsIDApCiAgICBkZXNjcmlwdG9yID0gb3Mub3Blbih0YXJnZXQsIGZsYWdzKQogICAgdHJ5OgogICAgICAgIG9wZW5lZCA9IG9zLmZzdGF0KGRlc2NyaXB0b3IpCiAgICAgICAgaWRlbnRpdHlfYmVmb3JlID0gKGJlZm9yZS5zdF9kZXYsIGJlZm9yZS5zdF9pbm8sIGJlZm9yZS5zdF9zaXplLCBiZWZvcmUuc3RfbXRpbWVfbnMpCiAgICAgICAgaWRlbnRpdHlfb3BlbmVkID0gKG9wZW5lZC5zdF9kZXYsIG9wZW5lZC5zdF9pbm8sIG9wZW5lZC5zdF9zaXplLCBvcGVuZWQuc3RfbXRpbWVfbnMpCiAgICAgICAgaWYgaWRlbnRpdHlfYmVmb3JlICE9IGlkZW50aXR5X29wZW5lZDoKICAgICAgICAgICAgZW1pdF9lcnJvcigiZmlsZS1jaGFuZ2VkLWR1cmluZy1yZWFkIikKICAgICAgICBkaWdlc3QgPSBoYXNobGliLnNoYTI1NigpCiAgICAgICAgdG90YWwgPSAwCiAgICAgICAgd2hpbGUgVHJ1ZToKICAgICAgICAgICAgY2h1bmsgPSBvcy5yZWFkKGRlc2NyaXB0b3IsIG1pbig2NTUzNiwgbWF4aW11bSAtIHRvdGFsICsgMSkpCiAgICAgICAgICAgIGlmIG5vdCBjaHVuazoKICAgICAgICAgICAgICAgIGJyZWFrCiAgICAgICAgICAgIHRvdGFsICs9IGxlbihjaHVuaykKICAgICAgICAgICAgaWYgdG90YWwgPiBtYXhpbXVtOgogICAgICAgICAgICAgICAgZW1pdF9lcnJvcigiZmlsZS10b28tbGFyZ2UiKQogICAgICAgICAgICBkaWdlc3QudXBkYXRlKGNodW5rKQogICAgICAgIGFmdGVyID0gb3MuZnN0YXQoZGVzY3JpcHRvcikKICAgIGZpbmFsbHk6CiAgICAgICAgb3MuY2xvc2UoZGVzY3JpcHRvcikKICAgIGlkZW50aXR5X2FmdGVyID0gKGFmdGVyLnN0X2RldiwgYWZ0ZXIuc3RfaW5vLCBhZnRlci5zdF9zaXplLCBhZnRlci5zdF9tdGltZV9ucykKICAgIGlmIGlkZW50aXR5X29wZW5lZCAhPSBpZGVudGl0eV9hZnRlciBvciB0b3RhbCAhPSBhZnRlci5zdF9zaXplOgogICAgICAgIGVtaXRfZXJyb3IoImZpbGUtY2hhbmdlZC1kdXJpbmctcmVhZCIpCiAgICB0cnk6CiAgICAgICAgb3duZXIgPSBwd2QuZ2V0cHd1aWQoYWZ0ZXIuc3RfdWlkKS5wd19uYW1lCiAgICBleGNlcHQgS2V5RXJyb3I6CiAgICAgICAgb3duZXIgPSBzdHIoYWZ0ZXIuc3RfdWlkKQogICAgdHJ5OgogICAgICAgIGdyb3VwID0gZ3JwLmdldGdyZ2lkKGFmdGVyLnN0X2dpZCkuZ3JfbmFtZQogICAgZXhjZXB0IEtleUVycm9yOgogICAgICAgIGdyb3VwID0gc3RyKGFmdGVyLnN0X2dpZCkKICAgIHByaW50KGpzb24uZHVtcHMoewogICAgICAgICJyZXNvbHZlZFBhdGgiOiByZXNvbHZlZCwKICAgICAgICAiYWxnb3JpdGhtIjogInNoYTI1NiIsCiAgICAgICAgImhhc2giOiBkaWdlc3QuaGV4ZGlnZXN0KCksCiAgICAgICAgInNpemUiOiB0b3RhbCwKICAgICAgICAib3duZXIiOiBvd25lciwKICAgICAgICAiZ3JvdXAiOiBncm91cCwKICAgICAgICAibW9kZSI6IGZvcm1hdChzdGF0LlNfSU1PREUoYWZ0ZXIuc3RfbW9kZSksICIwM28iKSwKICAgICAgICAiaXNTeW1saW5rIjogRmFsc2UsCiAgICAgICAgImVycm9yQ29kZSI6IE5vbmUKICAgIH0sIHNlcGFyYXRvcnM9KCIsIiwgIjoiKSkpCmV4Y2VwdCBQZXJtaXNzaW9uRXJyb3I6CiAgICBlbWl0X2Vycm9yKCJyZW1vdGUtcmVhZC1mYWlsZWQiKQpleGNlcHQgT1NFcnJvcjoKICAgIGVtaXRfZXJyb3IoInJlbW90ZS1yZWFkLWZhaWxlZCIpCg==";
    private const string CheckWriteScriptBase64 = "aW1wb3J0IGJhc2U2NCxqc29uLG9zLHN5cwpzaXRlX3Jvb3Q9YmFzZTY0LmI2NGRlY29kZShzeXMuYXJndlsxXSkuZGVjb2RlKCd1dGYtOCcpCnJlbD1iYXNlNjQuYjY0ZGVjb2RlKHN5cy5hcmd2WzJdKS5kZWNvZGUoJ3V0Zi04JykKY3JlYXRlX2RpcnM9c3lzLmFyZ3ZbM10gPT0gJzEnCnJvb3RfcmVhbD1vcy5wYXRoLnJlYWxwYXRoKHNpdGVfcm9vdCkKcGFydHM9W3AgZm9yIHAgaW4gcmVsLnJlcGxhY2UoJ1xcJywnLycpLnNwbGl0KCcvJykgaWYgcF0KaWYgbm90IHBhcnRzIG9yIGFueShwID09ICcuLicgZm9yIHAgaW4gcGFydHMpOgogICAgc3lzLmV4aXQoJ0VSUk9SOiBpbnZhbGlkIHdlYiBwdWJsaWMgZmlsZSBwYXRoJykKdGFyZ2V0PW9zLnBhdGguam9pbihyb290X3JlYWwsKnBhcnRzKQpwYXJlbnQ9b3MucGF0aC5kaXJuYW1lKHRhcmdldCkKcGFyZW50X3JlYWw9b3MucGF0aC5yZWFscGF0aChwYXJlbnQpCmlmIG9zLnBhdGguY29tbW9ucGF0aChbcm9vdF9yZWFsLHBhcmVudF9yZWFsXSkgIT0gcm9vdF9yZWFsOgogICAgc3lzLmV4aXQoJ0VSUk9SOiByZXNvbHZlZCBwYXJlbnQgaXMgb3V0c2lkZSB3ZWIgcHVibGljIHJvb3QnKQpyZXNvbHZlZD1vcy5wYXRoLnJlYWxwYXRoKHRhcmdldCkKaWYgb3MucGF0aC5leGlzdHModGFyZ2V0KSBvciBvcy5wYXRoLmlzbGluayh0YXJnZXQpOgogICAgaWYgb3MucGF0aC5jb21tb25wYXRoKFtyb290X3JlYWwscmVzb2x2ZWRdKSAhPSByb290X3JlYWw6CiAgICAgICAgc3lzLmV4aXQoJ0VSUk9SOiByZXNvbHZlZCBwYXRoIGlzIG91dHNpZGUgd2ViIHB1YmxpYyByb290JykKICAgIGlmIG5vdCBvcy5wYXRoLmlzZmlsZSh0YXJnZXQpOgogICAgICAgIHByaW50KGpzb24uZHVtcHMoeydyZXNvbHZlZFBhdGgnOnJlc29sdmVkLCdleGlzdHMnOlRydWUsJ2NhbldyaXRlJzpGYWxzZSwncmVhc29uJzonVGFyZ2V0IHBhdGggaXMgbm90IGEgcmVndWxhciBmaWxlLid9LHNlcGFyYXRvcnM9KCcsJywnOicpKSkKICAgICAgICBzeXMuZXhpdCgwKQogICAgY2FuPW9zLmFjY2Vzcyh0YXJnZXQsIG9zLldfT0spCiAgICBwcmludChqc29uLmR1bXBzKHsncmVzb2x2ZWRQYXRoJzpyZXNvbHZlZCwnZXhpc3RzJzpUcnVlLCdjYW5Xcml0ZSc6Y2FuLCdyZWFzb24nOk5vbmUgaWYgY2FuIGVsc2UgJ1RhcmdldCBmaWxlIGlzIG5vdCB3cml0YWJsZSBieSB0aGUgU1NIIHVzZXIuJ30sc2VwYXJhdG9ycz0oJywnLCc6JykpKQogICAgc3lzLmV4aXQoMCkKaWYgb3MucGF0aC5pc2RpcihwYXJlbnRfcmVhbCk6CiAgICBjYW49b3MuYWNjZXNzKHBhcmVudF9yZWFsLCBvcy5XX09LKQogICAgcHJpbnQoanNvbi5kdW1wcyh7J3Jlc29sdmVkUGF0aCc6cmVzb2x2ZWQsJ2V4aXN0cyc6RmFsc2UsJ2NhbldyaXRlJzpjYW4sJ3JlYXNvbic6Tm9uZSBpZiBjYW4gZWxzZSAnUGFyZW50IGRpcmVjdG9yeSBpcyBub3Qgd3JpdGFibGUgYnkgdGhlIFNTSCB1c2VyLid9LHNlcGFyYXRvcnM9KCcsJywnOicpKSkKICAgIHN5cy5leGl0KDApCnByaW50KGpzb24uZHVtcHMoeydyZXNvbHZlZFBhdGgnOnJlc29sdmVkLCdleGlzdHMnOkZhbHNlLCdjYW5Xcml0ZSc6Y3JlYXRlX2RpcnMsJ3JlYXNvbic6Tm9uZSBpZiBjcmVhdGVfZGlycyBlbHNlICdQYXJlbnQgZGlyZWN0b3J5IGRvZXMgbm90IGV4aXN0IGFuZCBjcmVhdGVEaXJlY3RvcmllcyBpcyBkaXNhYmxlZC4nfSxzZXBhcmF0b3JzPSgnLCcsJzonKSkp";
    private const string ReadScriptBase64 = "aW1wb3J0IGJhc2U2NCxqc29uLG9zLHN5cwpzaXRlX3Jvb3Q9YmFzZTY0LmI2NGRlY29kZShzeXMuYXJndlsxXSkuZGVjb2RlKCd1dGYtOCcpCnJlbD1iYXNlNjQuYjY0ZGVjb2RlKHN5cy5hcmd2WzJdKS5kZWNvZGUoJ3V0Zi04JykKbWF4Yj1pbnQoc3lzLmFyZ3ZbM10pCnJvb3RfcmVhbD1vcy5wYXRoLnJlYWxwYXRoKHNpdGVfcm9vdCkKcGFydHM9W3AgZm9yIHAgaW4gcmVsLnJlcGxhY2UoJ1xcJywnLycpLnNwbGl0KCcvJykgaWYgcF0KaWYgbm90IHBhcnRzIG9yIGFueShwID09ICcuLicgZm9yIHAgaW4gcGFydHMpOgogICAgc3lzLmV4aXQoJ0VSUk9SOiBpbnZhbGlkIHdlYiBwdWJsaWMgZmlsZSBwYXRoJykKdGFyZ2V0PW9zLnBhdGguam9pbihyb290X3JlYWwsKnBhcnRzKQpyZXNvbHZlZD1vcy5wYXRoLnJlYWxwYXRoKHRhcmdldCkKaWYgb3MucGF0aC5jb21tb25wYXRoKFtyb290X3JlYWwscmVzb2x2ZWRdKSAhPSByb290X3JlYWw6CiAgICBzeXMuZXhpdCgnRVJST1I6IHJlc29sdmVkIHBhdGggaXMgb3V0c2lkZSB3ZWIgcHVibGljIHJvb3QnKQppZiBub3Qgb3MucGF0aC5leGlzdHMocmVzb2x2ZWQpOgogICAgcHJpbnQoanNvbi5kdW1wcyh7J3Jlc29sdmVkUGF0aCc6cmVzb2x2ZWQsJ2V4aXN0cyc6RmFsc2V9LHNlcGFyYXRvcnM9KCcsJywnOicpKSkKICAgIHN5cy5leGl0KDApCmlmIG5vdCBvcy5wYXRoLmlzZmlsZShyZXNvbHZlZCk6CiAgICBzeXMuZXhpdCgnRVJST1I6IHdlYiBwdWJsaWMgcGF0aCBpcyBub3QgYSByZWd1bGFyIGZpbGUnKQpzaXplPW9zLnBhdGguZ2V0c2l6ZShyZXNvbHZlZCkKaWYgc2l6ZSA+IG1heGI6CiAgICBzeXMuZXhpdCgnRVJST1I6IHdlYiBwdWJsaWMgZmlsZSBleGNlZWRzIG1heGltdW0gcmVhZCBzaXplJykKd2l0aCBvcGVuKHJlc29sdmVkLCdyYicpIGFzIGY6CiAgICBkYXRhPWYucmVhZChtYXhiKzEpCnByaW50KGpzb24uZHVtcHMoeydyZXNvbHZlZFBhdGgnOnJlc29sdmVkLCdleGlzdHMnOlRydWUsJ2NvbnRlbnRCYXNlNjQnOmJhc2U2NC5iNjRlbmNvZGUoZGF0YSkuZGVjb2RlKCdhc2NpaScpLCdzaXplJzpsZW4oZGF0YSksJ2xhc3RNb2RpZmllZCc6X19pbXBvcnRfXygnZGF0ZXRpbWUnKS5kYXRldGltZS5mcm9tdGltZXN0YW1wKG9zLnBhdGguZ2V0bXRpbWUocmVzb2x2ZWQpLF9faW1wb3J0X18oJ2RhdGV0aW1lJykudGltZXpvbmUudXRjKS5pc29mb3JtYXQoKS5yZXBsYWNlKCcrMDA6MDAnLCdaJyl9LHNlcGFyYXRvcnM9KCcsJywnOicpKSk=";
    private const string SliceScriptBase64 = "aW1wb3J0IGJhc2U2NCxqc29uLG9zLHN5cyxkYXRldGltZQpzaXRlX3Jvb3Q9YmFzZTY0LmI2NGRlY29kZShzeXMuYXJndlsxXSkuZGVjb2RlKCd1dGYtOCcpCnJlbD1iYXNlNjQuYjY0ZGVjb2RlKHN5cy5hcmd2WzJdKS5kZWNvZGUoJ3V0Zi04JykKbW9kZT1zeXMuYXJndlszXQptYXhiPWludChzeXMuYXJndls0XSkKbWF4X2xpbmVzPWludChzeXMuYXJndls1XSkKcm9vdF9yZWFsPW9zLnBhdGgucmVhbHBhdGgoc2l0ZV9yb290KQpwYXJ0cz1bcCBmb3IgcCBpbiByZWwucmVwbGFjZSgnXFwnLCcvJykuc3BsaXQoJy8nKSBpZiBwXQppZiBub3QgcGFydHMgb3IgYW55KHAgPT0gJy4uJyBmb3IgcCBpbiBwYXJ0cyk6CiAgICBzeXMuZXhpdCgnRVJST1I6IGludmFsaWQgd2ViIHB1YmxpYyBmaWxlIHBhdGgnKQppZiBtb2RlIG5vdCBpbiAoJ2hlYWQnLCd0YWlsJyk6CiAgICBzeXMuZXhpdCgnRVJST1I6IGludmFsaWQgc2xpY2UgbW9kZScpCmlmIG1heGIgPCAxIG9yIG1heGIgPiAxMDQ4NTc2OgogICAgc3lzLmV4aXQoJ0VSUk9SOiBtYXhCeXRlcyBpcyBvdXQgb2YgcmFuZ2UnKQppZiBtYXhfbGluZXMgPCAwIG9yIG1heF9saW5lcyA+IDEwMDA6CiAgICBzeXMuZXhpdCgnRVJST1I6IG1heExpbmVzIGlzIG91dCBvZiByYW5nZScpCnRhcmdldD1vcy5wYXRoLmpvaW4ocm9vdF9yZWFsLCpwYXJ0cykKcmVzb2x2ZWQ9b3MucGF0aC5yZWFscGF0aCh0YXJnZXQpCmlmIG9zLnBhdGguY29tbW9ucGF0aChbcm9vdF9yZWFsLHJlc29sdmVkXSkgIT0gcm9vdF9yZWFsOgogICAgc3lzLmV4aXQoJ0VSUk9SOiByZXNvbHZlZCBwYXRoIGlzIG91dHNpZGUgd2ViIHB1YmxpYyByb290JykKaWYgbm90IG9zLnBhdGguZXhpc3RzKHJlc29sdmVkKToKICAgIHByaW50KGpzb24uZHVtcHMoeydyZXNvbHZlZFBhdGgnOnJlc29sdmVkLCdleGlzdHMnOkZhbHNlfSxzZXBhcmF0b3JzPSgnLCcsJzonKSkpCiAgICBzeXMuZXhpdCgwKQppZiBub3Qgb3MucGF0aC5pc2ZpbGUocmVzb2x2ZWQpOgogICAgc3lzLmV4aXQoJ0VSUk9SOiB3ZWIgcHVibGljIHBhdGggaXMgbm90IGEgcmVndWxhciBmaWxlJykKc291cmNlX3NpemU9b3MucGF0aC5nZXRzaXplKHJlc29sdmVkKQp3aXRoIG9wZW4ocmVzb2x2ZWQsJ3JiJykgYXMgZjoKICAgIGlmIG1vZGUgPT0gJ3RhaWwnOgogICAgICAgIGYuc2VlayhtYXgoMCxzb3VyY2Vfc2l6ZS1tYXhiKSkKICAgICAgICBkYXRhPWYucmVhZChtYXhiKQogICAgZWxzZToKICAgICAgICBkYXRhPWYucmVhZChtYXhiKQppZiBtYXhfbGluZXMgPiAwOgogICAgbGluZXM9ZGF0YS5zcGxpdGxpbmVzKGtlZXBlbmRzPVRydWUpCiAgICBkYXRhPWInJy5qb2luKGxpbmVzWzptYXhfbGluZXNdIGlmIG1vZGUgPT0gJ2hlYWQnIGVsc2UgbGluZXNbLW1heF9saW5lczpdKQpwcmludChqc29uLmR1bXBzKHsncmVzb2x2ZWRQYXRoJzpyZXNvbHZlZCwnZXhpc3RzJzpUcnVlLCdjb250ZW50QmFzZTY0JzpiYXNlNjQuYjY0ZW5jb2RlKGRhdGEpLmRlY29kZSgnYXNjaWknKSwnc2l6ZSc6bGVuKGRhdGEpLCdzb3VyY2VTaXplJzpzb3VyY2Vfc2l6ZSwnbGFzdE1vZGlmaWVkJzpkYXRldGltZS5kYXRldGltZS5mcm9tdGltZXN0YW1wKG9zLnBhdGguZ2V0bXRpbWUocmVzb2x2ZWQpLGRhdGV0aW1lLnRpbWV6b25lLnV0YykuaXNvZm9ybWF0KCkucmVwbGFjZSgnKzAwOjAwJywnWicpfSxzZXBhcmF0b3JzPSgnLCcsJzonKSkp";
    private const string WriteScriptBase64 = "aW1wb3J0IGJhc2U2NCxqc29uLG9zLHN5cwpzaXRlX3Jvb3Q9YmFzZTY0LmI2NGRlY29kZShzeXMuYXJndlsxXSkuZGVjb2RlKCd1dGYtOCcpCnJlbD1iYXNlNjQuYjY0ZGVjb2RlKHN5cy5hcmd2WzJdKS5kZWNvZGUoJ3V0Zi04JykKY29udGVudF9iYXNlNjQ9c3lzLnN0ZGluLnJlYWQoKQpkYXRhPWJhc2U2NC5iNjRkZWNvZGUoY29udGVudF9iYXNlNjQsdmFsaWRhdGU9VHJ1ZSkKbWF4Yj1pbnQoc3lzLmFyZ3ZbM10pCmNyZWF0ZV9kaXJzPXN5cy5hcmd2WzRdID09ICcxJwppZiBsZW4oZGF0YSkgPiBtYXhiOgogICAgc3lzLmV4aXQoJ0VSUk9SOiB3ZWIgcHVibGljIGNvbnRlbnQgZXhjZWVkcyBtYXhpbXVtIHdyaXRlIHNpemUnKQpyb290X3JlYWw9b3MucGF0aC5yZWFscGF0aChzaXRlX3Jvb3QpCnBhcnRzPVtwIGZvciBwIGluIHJlbC5yZXBsYWNlKCdcXCcsJy8nKS5zcGxpdCgnLycpIGlmIHBdCmlmIG5vdCBwYXJ0cyBvciBhbnkocCA9PSAnLi4nIGZvciBwIGluIHBhcnRzKToKICAgIHN5cy5leGl0KCdFUlJPUjogaW52YWxpZCB3ZWIgcHVibGljIGZpbGUgcGF0aCcpCnRhcmdldD1vcy5wYXRoLmpvaW4ocm9vdF9yZWFsLCpwYXJ0cykKcGFyZW50PW9zLnBhdGguZGlybmFtZSh0YXJnZXQpCnBhcmVudF9yZWFsPW9zLnBhdGgucmVhbHBhdGgocGFyZW50KQppZiBvcy5wYXRoLmNvbW1vbnBhdGgoW3Jvb3RfcmVhbCxwYXJlbnRfcmVhbF0pICE9IHJvb3RfcmVhbDoKICAgIHN5cy5leGl0KCdFUlJPUjogcmVzb2x2ZWQgcGFyZW50IGlzIG91dHNpZGUgd2ViIHB1YmxpYyByb290JykKaWYgbm90IG9zLnBhdGguaXNkaXIocGFyZW50X3JlYWwpOgogICAgaWYgY3JlYXRlX2RpcnM6CiAgICAgICAgdHJ5OgogICAgICAgICAgICBvcy5tYWtlZGlycyhwYXJlbnQsZXhpc3Rfb2s9VHJ1ZSkKICAgICAgICBleGNlcHQgT1NFcnJvciBhcyBleDoKICAgICAgICAgICAgc3lzLmV4aXQoJ0VSUk9SOiBmYWlsZWQgdG8gY3JlYXRlIHdlYiBwdWJsaWMgcGFyZW50IGRpcmVjdG9yeTogJyArIHN0cihleCkpCiAgICAgICAgcGFyZW50X3JlYWw9b3MucGF0aC5yZWFscGF0aChwYXJlbnQpCiAgICAgICAgaWYgb3MucGF0aC5jb21tb25wYXRoKFtyb290X3JlYWwscGFyZW50X3JlYWxdKSAhPSByb290X3JlYWw6CiAgICAgICAgICAgIHN5cy5leGl0KCdFUlJPUjogcmVzb2x2ZWQgcGFyZW50IGlzIG91dHNpZGUgd2ViIHB1YmxpYyByb290JykKICAgIGVsc2U6CiAgICAgICAgc3lzLmV4aXQoJ0VSUk9SOiB3ZWIgcHVibGljIHBhcmVudCBkaXJlY3RvcnkgZG9lcyBub3QgZXhpc3QnKQpyZXNvbHZlZD1vcy5wYXRoLnJlYWxwYXRoKHRhcmdldCkKaWYgb3MucGF0aC5jb21tb25wYXRoKFtyb290X3JlYWwscmVzb2x2ZWRdKSAhPSByb290X3JlYWw6CiAgICBzeXMuZXhpdCgnRVJST1I6IHJlc29sdmVkIHBhdGggaXMgb3V0c2lkZSB3ZWIgcHVibGljIHJvb3QnKQppZiBvcy5wYXRoLmV4aXN0cyhyZXNvbHZlZCkgYW5kIG5vdCBvcy5wYXRoLmlzZmlsZShyZXNvbHZlZCk6CiAgICBzeXMuZXhpdCgnRVJST1I6IHdlYiBwdWJsaWMgcGF0aCBpcyBub3QgYSByZWd1bGFyIGZpbGUnKQpleGlzdGVkPW9zLnBhdGguZXhpc3RzKHJlc29sdmVkKQp0cnk6CiAgICB3aXRoIG9wZW4ocmVzb2x2ZWQsJ3diJykgYXMgZjoKICAgICAgICBmLndyaXRlKGRhdGEpCmV4Y2VwdCBPU0Vycm9yIGFzIGV4OgogICAgc3lzLmV4aXQoJ0VSUk9SOiBmYWlsZWQgdG8gd3JpdGUgd2ViIHB1YmxpYyBmaWxlOiAnICsgc3RyKGV4KSkKcHJpbnQoanNvbi5kdW1wcyh7J3Jlc29sdmVkUGF0aCc6cmVzb2x2ZWQsJ3dyaXR0ZW4nOlRydWUsJ2NyZWF0ZWQnOm5vdCBleGlzdGVkLCdvdmVyd3JpdHRlbic6ZXhpc3RlZCwnc2l6ZSc6bGVuKGRhdGEpfSxzZXBhcmF0b3JzPSgnLCcsJzonKSkp";

    private static readonly AllowedCommandDefinition[] Commands =
    [
        new(
            "web_public_file_list_internal",
            CreateEncodedPythonStdinCommand(ListScriptBase64, "{siteRootBase64} {pathBase64} {maxDepth} {limit}"),
            TimeSpan.FromSeconds(10),
            [
                new AllowedCommandParameterDefinition("siteRootBase64", MaxLength: 4096, Pattern: Base64PathPattern),
                new AllowedCommandParameterDefinition("pathBase64", MaxLength: 4096, Pattern: Base64PathPattern),
                new AllowedCommandParameterDefinition("maxDepth", MaxLength: 1, Pattern: MaxDepthPattern),
                new AllowedCommandParameterDefinition("limit", MaxLength: 3, Pattern: LimitPattern),
            ]),
        new(
            "web_public_file_stat_internal",
            CreateEncodedPythonStdinCommand(StatScriptBase64, "{siteRootBase64} {pathBase64}"),
            TimeSpan.FromSeconds(10),
            [
                new AllowedCommandParameterDefinition("siteRootBase64", MaxLength: 4096, Pattern: Base64PathPattern),
                new AllowedCommandParameterDefinition("pathBase64", MaxLength: 4096, Pattern: Base64PathPattern),
            ]),
        new(
            "web_public_file_hash_internal",
            CreateEncodedPythonStdinCommand(HashScriptBase64, "{siteRootBase64} {pathBase64} {maxBytes}"),
            TimeSpan.FromSeconds(30),
            [
                new AllowedCommandParameterDefinition("siteRootBase64", MaxLength: 4096, Pattern: Base64PathPattern),
                new AllowedCommandParameterDefinition("pathBase64", MaxLength: 4096, Pattern: Base64PathPattern),
                new AllowedCommandParameterDefinition("maxBytes", MaxLength: 8, Pattern: MaxBytesPattern),
            ]),
        new(
            "web_public_file_check_write_internal",
            CreateEncodedPythonStdinCommand(CheckWriteScriptBase64, "{siteRootBase64} {pathBase64} {createDirectories}"),
            TimeSpan.FromSeconds(10),
            [
                new AllowedCommandParameterDefinition("siteRootBase64", MaxLength: 4096, Pattern: Base64PathPattern),
                new AllowedCommandParameterDefinition("pathBase64", MaxLength: 4096, Pattern: Base64PathPattern),
                new AllowedCommandParameterDefinition("createDirectories", MaxLength: 1, Pattern: CreateDirectoriesPattern),
            ]),
        new(
            "web_public_file_read_internal",
            CreateEncodedPythonStdinCommand(ReadScriptBase64, "{siteRootBase64} {pathBase64} {maxBytes}"),
            TimeSpan.FromSeconds(10),
            [
                new AllowedCommandParameterDefinition("siteRootBase64", MaxLength: 4096, Pattern: Base64PathPattern),
                new AllowedCommandParameterDefinition("pathBase64", MaxLength: 4096, Pattern: Base64PathPattern),
                new AllowedCommandParameterDefinition("maxBytes", MaxLength: 8, Pattern: MaxBytesPattern),
            ]),
        new(
            "web_public_file_slice_internal",
            CreateEncodedPythonStdinCommand(SliceScriptBase64, "{siteRootBase64} {pathBase64} {mode} {maxBytes} {maxLines}"),
            TimeSpan.FromSeconds(10),
            [
                new AllowedCommandParameterDefinition("siteRootBase64", MaxLength: 4096, Pattern: Base64PathPattern),
                new AllowedCommandParameterDefinition("pathBase64", MaxLength: 4096, Pattern: Base64PathPattern),
                new AllowedCommandParameterDefinition("mode", MaxLength: 4, Pattern: SliceModePattern),
                new AllowedCommandParameterDefinition("maxBytes", MaxLength: 8, Pattern: MaxBytesPattern),
                new AllowedCommandParameterDefinition("maxLines", MaxLength: 4, Pattern: MaxLinesPattern),
            ]),
        new(
            "web_public_file_write_internal",
            CreateEncodedPythonCommand(WriteScriptBase64, "{siteRootBase64} {pathBase64} {maxBytes} {createDirectories}"),
            TimeSpan.FromSeconds(30),
            [
                new AllowedCommandParameterDefinition("siteRootBase64", MaxLength: 4096, Pattern: Base64PathPattern),
                new AllowedCommandParameterDefinition("pathBase64", MaxLength: 4096, Pattern: Base64PathPattern),
                new AllowedCommandParameterDefinition("maxBytes", MaxLength: 8, Pattern: MaxBytesPattern),
                new AllowedCommandParameterDefinition("createDirectories", MaxLength: 1, Pattern: CreateDirectoriesPattern),
            ],
            SshCommandRiskLevel.ConfirmRequired),
        new(
            "web_public_file_write_with_permissions_internal",
            "sudo -n " + HelperPath + " write-file {siteRootBase64} {pathBase64} - {maxBytes} {createDirectories} {ownerBase64} {modeBase64}",
            TimeSpan.FromSeconds(30),
            [
                new AllowedCommandParameterDefinition("siteRootBase64", MaxLength: 4096, Pattern: Base64PathPattern),
                new AllowedCommandParameterDefinition("pathBase64", MaxLength: 4096, Pattern: Base64PathPattern),
                new AllowedCommandParameterDefinition("maxBytes", MaxLength: 8, Pattern: MaxBytesPattern),
                new AllowedCommandParameterDefinition("createDirectories", MaxLength: 1, Pattern: CreateDirectoriesPattern),
                new AllowedCommandParameterDefinition("ownerBase64", MaxLength: 128, Pattern: Base64PathPattern),
                new AllowedCommandParameterDefinition("modeBase64", MaxLength: 64, Pattern: Base64PathPattern),
            ],
            SshCommandRiskLevel.ConfirmRequired),
        new(
            "web_change_owner_internal",
            "sudo -n " + HelperPath + " change-owner {siteRootBase64} {pathBase64} {ownerBase64} {groupBase64} {recursive}",
            TimeSpan.FromSeconds(30),
            [
                new AllowedCommandParameterDefinition("siteRootBase64", MaxLength: 4096, Pattern: Base64PathPattern),
                new AllowedCommandParameterDefinition("pathBase64", MaxLength: 4096, Pattern: Base64PathPattern),
                new AllowedCommandParameterDefinition("ownerBase64", MaxLength: 128, Pattern: Base64PathPattern),
                new AllowedCommandParameterDefinition("groupBase64", MaxLength: 128, Pattern: Base64PathPattern),
                new AllowedCommandParameterDefinition("recursive", MaxLength: 1, Pattern: RecursivePattern),
            ],
            SshCommandRiskLevel.ConfirmRequired),
        new(
            "web_change_mode_internal",
            "sudo -n " + HelperPath + " change-mode {siteRootBase64} {pathBase64} {mode} {recursive}",
            TimeSpan.FromSeconds(30),
            [
                new AllowedCommandParameterDefinition("siteRootBase64", MaxLength: 4096, Pattern: Base64PathPattern),
                new AllowedCommandParameterDefinition("pathBase64", MaxLength: 4096, Pattern: Base64PathPattern),
                new AllowedCommandParameterDefinition("mode", MaxLength: 3, Pattern: ModePattern),
                new AllowedCommandParameterDefinition("recursive", MaxLength: 1, Pattern: RecursivePattern),
            ],
            SshCommandRiskLevel.ConfirmRequired),
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

    private static string CreateEncodedPythonStdinCommand(string encodedScript, string arguments)
    {
        return $"sh -c \"printf %s '{encodedScript}' | base64 -d | python3 - {arguments}\"";
    }

    private static string CreateEncodedPythonCommand(string encodedScript, string arguments)
    {
        return $"sh -c \"python3 -c \\\"$(printf %s '{encodedScript}' | base64 -d)\\\" {arguments}\"";
    }
}
