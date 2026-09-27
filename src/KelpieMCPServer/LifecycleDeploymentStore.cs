using System.Text.Json;
using Microsoft.Data.Sqlite;
using ModelContextProtocol.Protocol;
using Moyai.ProviderAuthentication;

namespace KelpieMCPServer;

/// <summary>Durable context and write-ahead stage journal. Pending stages are never replayed.</summary>
public sealed class LifecycleDeploymentStore(string databasePath)
{
    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath, Mode = SqliteOpenMode.ReadWriteCreate,
            DefaultTimeout = 5, Pooling = false,
        }.ToString());
        try { await connection.OpenAsync(cancellationToken); return connection; }
        catch { await connection.DisposeAsync(); throw; }
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (!Path.IsPathFullyQualified(databasePath))
            throw new ProviderAuthenticationException("authentication_unavailable");
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS lifecycle_deployments (
                id TEXT PRIMARY KEY, project TEXT NOT NULL, target TEXT NOT NULL, profile TEXT NOT NULL,
                deployment TEXT, transfer TEXT);
            CREATE TABLE IF NOT EXISTS lifecycle_stages (
                deployment_id TEXT NOT NULL, tool TEXT NOT NULL, input_hash TEXT NOT NULL,
                result TEXT, PRIMARY KEY(deployment_id, tool));
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<LifecycleSnapshot?> FindAsync(string id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT project,target,profile,deployment,transfer,
                EXISTS(SELECT 1 FROM lifecycle_stages WHERE deployment_id=$id AND result IS NULL)
            FROM lifecycle_deployments WHERE id=$id;
            """;
        command.Parameters.AddWithValue("$id", id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new(new(Guid.Parse(reader.GetString(0)), reader.GetString(1), reader.GetString(2)),
            reader.IsDBNull(3) ? null : JsonSerializer.Deserialize<ServerDeployment>(reader.GetString(3)),
            reader.IsDBNull(4) ? null : JsonSerializer.Deserialize<WebBulkTransfer>(reader.GetString(4)),
            reader.GetBoolean(5));
    }

    /// <summary>Returns a completed result or atomically reserves a previously unseen stage.</summary>
    public async Task<CallToolResult?> BeginAsync(string id, LifecycleTarget target, string tool,
        string inputHash, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$project", target.ProjectId.ToString("D"));
        command.Parameters.AddWithValue("$target", target.TargetId);
        command.Parameters.AddWithValue("$profile", target.ProfileName);
        command.Parameters.AddWithValue("$tool", tool);
        command.Parameters.AddWithValue("$hash", inputHash);
        if (tool == "deploy_prepare")
        {
            command.CommandText = """
                INSERT OR IGNORE INTO lifecycle_deployments(id,project,target,profile)
                VALUES($id,$project,$target,$profile);
                """;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        command.CommandText = "SELECT COUNT(*) FROM lifecycle_deployments WHERE id=$id AND project=$project AND target=$target AND profile=$profile";
        if (Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) != 1)
            throw new ProviderAuthenticationException("auth_project_mismatch");
        command.CommandText = "SELECT COUNT(*) FROM lifecycle_stages WHERE deployment_id=$id AND result IS NULL";
        if (Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) != 0)
            throw new ProviderAuthenticationException("deployment_result_unknown");
        command.CommandText = "SELECT input_hash,result FROM lifecycle_stages WHERE deployment_id=$id AND tool=$tool";
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
            {
                if (reader.GetString(0) != inputHash)
                    throw new ProviderAuthenticationException("idempotency-conflict");
                return JsonSerializer.Deserialize<CallToolResult>(reader.GetString(1))
                    ?? throw new ProviderAuthenticationException("deployment_result_unknown");
            }
        }
        command.CommandText = "INSERT INTO lifecycle_stages(deployment_id,tool,input_hash) VALUES($id,$tool,$hash)";
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return null;
    }

    internal async Task CancelBeforeDispatchAsync(string id, string tool, string inputHash, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM lifecycle_stages WHERE deployment_id=$id AND tool=$tool AND input_hash=$hash AND result IS NULL";
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$tool", tool);
        command.Parameters.AddWithValue("$hash", inputHash);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task CompleteAsync(string id, string tool, CallToolResult result,
        ServerDeployment? deployment, WebBulkTransfer? transfer, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE lifecycle_deployments SET deployment=$deployment,transfer=$transfer WHERE id=$id;
            UPDATE lifecycle_stages SET result=$result WHERE deployment_id=$id AND tool=$tool AND result IS NULL;
            """;
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$tool", tool);
        command.Parameters.AddWithValue("$result", JsonSerializer.Serialize(result));
        command.Parameters.AddWithValue("$deployment", deployment is null ? DBNull.Value : JsonSerializer.Serialize(deployment));
        command.Parameters.AddWithValue("$transfer", transfer is null ? DBNull.Value : JsonSerializer.Serialize(transfer));
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}

public sealed record LifecycleSnapshot(LifecycleTarget Target, ServerDeployment? Deployment,
    WebBulkTransfer? Transfer, bool ResultUnknown);
