
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Data.Common;
using System.Security.Cryptography.X509Certificates;

namespace Muntonrecht.ApiService.Context;

public partial class MuntonrechtContext : IdentityDbContext<UserModel>
{
    public MuntonrechtContext(DbContextOptions<MuntonrechtContext> contextOptions) : base(contextOptions)
    {
    }
    

    public void RunMigrations(ILogger? logger = null)
    {
        var conn = Database.GetDbConnection();
        conn.Open();

        EnsureAppliedScriptsTable(conn);

        var appliedScripts = GetAppliedScripts(conn);

        var scriptFolder = Path.Combine(AppContext.BaseDirectory, "SqlScripts");

        if (!Directory.Exists(scriptFolder))
        {
            logger?.LogWarning(
                "SQL script folder '{ScriptFolder}' not found — no scripts applied.",
                scriptFolder);
            return;
        }

        // Scripts can depend on each other (e.g. auto_ChatModel.sql has an FK
        // to "Teams", which is created by auto_TeamModel.sql that sorts later).
        // Apply them in passes, deferring scripts that reference tables/columns
        // that don't exist yet, until everything has been applied.
        var pending = Directory.GetFiles(scriptFolder, "*.sql")
            .OrderBy(x => x)
            .Select(path => (Name: Path.GetFileName(path), Sql: File.ReadAllText(path)))
            .Where(s => !appliedScripts.Contains(s.Name))
            .ToList();

        if (pending.Count > 0)
        {
            logger?.LogInformation(
                "SQL migrations: {PendingCount} pending script(s) to apply: {PendingScripts}",
                pending.Count, string.Join(", ", pending.Select(p => p.Name)));
        }
        else
        {
            logger?.LogInformation(
                "SQL migrations: up to date — all {AppliedCount} known scripts already applied.",
                appliedScripts.Count);
        }

        while (pending.Count > 0)
        {
            var deferred = new List<(string Name, string Sql, DbException Error)>();
            var appliedAny = false;

            foreach (var (name, sql) in pending)
            {
                try
                {
                    // Note: no explicit transaction here — some scripts (e.g.
                    // CreateIdentity.sql) manage their own START TRANSACTION /
                    // COMMIT, which would complete an outer NpgsqlTransaction.
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = sql;
                    cmd.ExecuteNonQuery();

                    RecordScript(conn, name);
                    appliedAny = true;
                    logger?.LogInformation("SQL script applied: {ScriptName}", name);
                }
                catch (DbException ex) when (ex.SqlState is "42P01" or "42703")
                {
                    // 42P01: undefined table, 42703: undefined column —
                    // a dependency isn't created yet; retry next pass.
                    deferred.Add((name, sql, ex));
                    logger?.LogWarning(
                        "SQL script deferred (missing dependency, will retry): {ScriptName}",
                        name);
                }
            }

            if (!appliedAny && deferred.Count > 0)
            {
                var last = deferred[^1];
                logger?.LogError(
                    "Unable to apply SQL script '{ScriptName}': unresolvable dependencies.",
                    last.Name);
                throw new InvalidOperationException(
                    $"Unable to apply SQL script '{last.Name}': unresolvable dependencies.",
                    last.Error);
            }

            pending = deferred
                .Select(d => (d.Name, d.Sql))
                .ToList();
        }

        conn.Close();
    }

    private void EnsureAppliedScriptsTable(DbConnection conn)
    {
        using var cmd = conn.CreateCommand();

        cmd.CommandText =
        """
        CREATE TABLE IF NOT EXISTS applied_scripts (
            id SERIAL PRIMARY KEY,
            script_name TEXT NOT NULL UNIQUE,
            applied_at TIMESTAMP NOT NULL DEFAULT NOW()
        );
        """;

        cmd.ExecuteNonQuery();
    }

    private HashSet<string> GetAppliedScripts(DbConnection conn)
    {
        var scripts = new HashSet<string>();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT script_name FROM applied_scripts";

        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            scripts.Add(reader.GetString(0));
        }

        return scripts;
    }

    private void RecordScript(DbConnection conn, string name)
    {
        using var cmd = conn.CreateCommand();

        cmd.CommandText =
        """
        INSERT INTO applied_scripts (script_name)
        VALUES (@name)
        """;

        var param = cmd.CreateParameter();
        param.ParameterName = "name";
        param.Value = name;

        cmd.Parameters.Add(param);

        cmd.ExecuteNonQuery();
    }

}
