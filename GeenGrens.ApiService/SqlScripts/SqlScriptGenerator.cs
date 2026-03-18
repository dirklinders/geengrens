using GeenGrens.ApiService;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

public static class SqlScriptGenerator
{
    public static void Generate()
    {
        var types = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => t.GetCustomAttribute<GenerateCrudAttribute>() != null)
            .ToList();
        var scriptFolder = Path.Combine(AppContext.BaseDirectory, "SqlScripts");
        Directory.CreateDirectory(scriptFolder);

        foreach (var type in types)
        {
            var sql = GenerateCreateTableSql(type, types);

            var path = Path.Combine(scriptFolder, $"auto_{type.Name}.sql");

            if (!Directory.EnumerateFiles(scriptFolder, $"*auto_{type.Name}.sql").Any())
                File.WriteAllText(path, sql);
        }

        // Generate many-to-many join tables
        GenerateManyToManyTables(types, scriptFolder);
    }

    private static string GenerateCreateTableSql(Type type, List<Type> allTypes)
    {
        var sb = new StringBuilder();

        var plural = type.Name.Replace("Model", "s");

        sb.AppendLine($"CREATE TABLE IF NOT EXISTS \"{plural}\" (");

        var props = type.GetProperties()
            .Where(p => IsSimpleType(p.PropertyType)) // Ignore navigation properties
            .ToArray();

        for (int i = 0; i < props.Length; i++)
        {
            var prop = props[i];

            var columnType = MapType(prop.PropertyType);

            var columnDef = $"\"{prop.Name}\" {columnType}";

            // Auto PK for Id
            if (prop.Name == "Id")
                columnDef += " GENERATED ALWAYS AS IDENTITY PRIMARY KEY";

            sb.AppendLine(i < props.Length - 1 ? $"    {columnDef}," : $"    {columnDef}");
        }

        // Add foreign keys for one-to-many (properties ending with "Id" and type exists)
        foreach (var prop in type.GetProperties().Where(p => p.Name.EndsWith("Id") && p.Name != "Id"))
        {
            var fkName = prop.Name.Substring(0, prop.Name.Length - 2); // remove "Id"
            
            var fkType = allTypes.FirstOrDefault(t => t.Name == fkName || t.Name == fkName + "Model");
            
            if (fkType != null)
            {
                var fkTypePlural = fkType.Name.Replace("Model", "s");
                sb.AppendLine($",    FOREIGN KEY (\"{prop.Name}\") REFERENCES \"{fkTypePlural}\"(\"Id\")");
            }
        }

        sb.AppendLine(");");

        return sb.ToString();
    }

    private static void GenerateManyToManyTables(List<Type> allTypes, string scriptFolder)
    {
        var pairs = new List<(Type, Type)>();

        // Find List<> navigation properties for many-to-many
        foreach (var type in allTypes)
        {
            var props = type.GetProperties()
                .Where(p => p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(List<>));

            foreach (var prop in props)
            {
                var targetType = prop.PropertyType.GetGenericArguments()[0];

                // Check if targetType also has a List<type>
                var reverseProps = targetType.GetProperties()
                    .Where(p => p.PropertyType.IsGenericType &&
                                p.PropertyType.GetGenericTypeDefinition() == typeof(List<>) &&
                                p.PropertyType.GetGenericArguments()[0] == type);

                if (!reverseProps.Any())
                {
                    // Not a bidirectional List, so skip (probably one-to-many)
                    continue;
                }

                // Alphabetical order for table name
                var names = new[] { type.Name, targetType.Name }.OrderBy(n => n).ToArray();

                // Ensure no duplicate pairs
                if (!pairs.Any(p => (p.Item1.Name == names[0] && p.Item2.Name == names[1]) ||
                                    (p.Item1.Name == names[1] && p.Item2.Name == names[0])))
                {
                    pairs.Add((allTypes.First(t => t.Name == names[0]), allTypes.First(t => t.Name == names[1])));
                }
            }
        }

        foreach (var (t1, t2) in pairs)
        {
            var tableName = StripModel(t1.Name) + StripModel(t2.Name);

            var t1Plural = StripModel(t1.Name) + "s";
            var t2Plural = StripModel(t2.Name) + "s";

            var sql = $"""
            CREATE TABLE IF NOT EXISTS "{tableName}" (
                "{StripModel(t1.Name)}Id" INTEGER NOT NULL,
                "{StripModel(t2.Name)}Id" INTEGER NOT NULL,
                PRIMARY KEY ("{StripModel(t1.Name)}Id", "{StripModel(t2.Name)}Id"),
                FOREIGN KEY ("{StripModel(t1.Name)}Id") REFERENCES "{t1Plural}"("Id"),
                FOREIGN KEY ("{StripModel(t2.Name)}Id") REFERENCES "{t2Plural}"("Id")
            );
            """;

            var path = Path.Combine(scriptFolder, $"auto_{tableName}.sql");

            if (!Directory.EnumerateFiles(scriptFolder, $"*auto_{tableName}.sql").Any())
                File.WriteAllText(path, sql);
        }
    }

    private static bool IsSimpleType(Type type)
    {
        return
            type.IsPrimitive ||
            type == typeof(string) ||
            type == typeof(decimal) ||
            type == typeof(DateTime) ||
            type == typeof(Guid) ||
            type.IsEnum ||
            (Nullable.GetUnderlyingType(type) != null && IsSimpleType(Nullable.GetUnderlyingType(type)));
    }

    private static string MapType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;

        return type switch
        {
            _ when type == typeof(int) => "INTEGER",
            _ when type == typeof(long) => "BIGINT",
            _ when type == typeof(string) => "TEXT",
            _ when type == typeof(bool) => "BOOLEAN",
            _ when type == typeof(DateTime) => "TIMESTAMP",
            _ when type == typeof(decimal) => "NUMERIC",
            _ when type == typeof(Guid) => "UUID",
            _ => "TEXT"
        };
    }

    private static string StripModel(string name)
    {
        return Regex.Replace(name, "Model$", "");
    }
}