
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;
using System.Security.Cryptography.X509Certificates;

namespace GeenGrens.ApiService.Context;

public partial class GeenGrensContext : IdentityDbContext<UserModel>
{
    public GeenGrensContext(DbContextOptions<GeenGrensContext> contextOptions) : base(contextOptions)
    {
    }
    

    public void RunMigrations()
    {
        var conn = Database.GetDbConnection();
        conn.Open();

        EnsureAppliedScriptsTable(conn);

        var appliedScripts = GetAppliedScripts(conn);

        var scriptFolder = Path.Combine(AppContext.BaseDirectory, "SqlScripts");

        if (!Directory.Exists(scriptFolder))
            return;

        var scripts = Directory.GetFiles(scriptFolder, "*.sql")
            .OrderBy(x => x);

        foreach (var scriptPath in scripts)
        {
            var name = Path.GetFileName(scriptPath);

            if (appliedScripts.Contains(name))
                continue;

            var sql = File.ReadAllText(scriptPath);

            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();

            RecordScript(conn, name);
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
