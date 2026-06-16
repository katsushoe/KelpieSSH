using KelpieSSH.Application.Ssh;
using Renci.SshNet;

namespace KelpieSSH.Infrastructure.Ssh;

internal sealed class SshNetAuthenticationFactory
{
    private readonly ISshPasswordProvider _passwordProvider;

    public SshNetAuthenticationFactory(ISshPasswordProvider passwordProvider)
    {
        _passwordProvider = passwordProvider;
    }

    public async ValueTask<AuthenticationMethod> CreateAsync(
        SshConnectionProfile profile,
        CancellationToken cancellationToken)
    {
        if (string.Equals(profile.AuthenticationMethod, "privateKey", StringComparison.OrdinalIgnoreCase))
        {
            var privateKeyPath = profile.PrivateKeyPath
                ?? throw new InvalidOperationException("SSH private key path is required.");
            var privateKeyFile = string.IsNullOrEmpty(profile.PrivateKeyPassphrase)
                ? new PrivateKeyFile(privateKeyPath)
                : new PrivateKeyFile(privateKeyPath, profile.PrivateKeyPassphrase);

            return new PrivateKeyAuthenticationMethod(profile.UserName, privateKeyFile);
        }

        if (string.Equals(profile.AuthenticationMethod, "password", StringComparison.OrdinalIgnoreCase))
        {
            var secretName = profile.PasswordSecretName
                ?? throw new InvalidOperationException("SSH password secret name is required.");
            var password = await _passwordProvider.GetPasswordAsync(secretName, cancellationToken);
            if (string.IsNullOrEmpty(password))
            {
                throw new InvalidOperationException($"SSH password is not available for secret: {secretName}");
            }

            return new PasswordAuthenticationMethod(profile.UserName, password);
        }

        throw new NotSupportedException($"SSH authentication method is not implemented: {profile.AuthenticationMethod}");
    }
}
