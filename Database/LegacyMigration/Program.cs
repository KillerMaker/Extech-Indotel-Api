using System.Data;
using Azure.Core;
using Azure.Identity;
using Microsoft.Data.SqlClient;

// Standalone operator tool. Never runs during API startup or deployment.
try
{
    if (args.Contains("--self-test"))
    {
        MigrationChecks.SelfTest();
        return 0;
    }
    if (args.Any(a => a != "--apply"))
        throw new InvalidOperationException("Usage: dotnet run --project Database/LegacyMigration -- [--apply|--self-test]");

    string Required(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
        ? value : throw new InvalidOperationException($"Set {name} in the environment.");
    var sourceSettings = new SqlConnectionStringBuilder(Required("MIGRATION_SOURCE_CONNECTION_STRING"));
    var targetSettings = new SqlConnectionStringBuilder(Required("MIGRATION_TARGET_CONNECTION_STRING"));
    if (string.Equals(sourceSettings.DataSource, targetSettings.DataSource, StringComparison.OrdinalIgnoreCase)
        && string.Equals(sourceSettings.InitialCatalog, targetSettings.InitialCatalog, StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("Source and target must be different databases.");
    sourceSettings.ApplicationIntent = ApplicationIntent.ReadOnly;
    sourceSettings.ApplicationName = "Indotel legacy migration source";
    targetSettings.ApplicationName = "Indotel legacy migration target";
    await using var source = new SqlConnection(sourceSettings.ConnectionString);
    if (Environment.GetEnvironmentVariable("MIGRATION_SOURCE_ENTRA_TENANT_ID") is { Length: > 0 } tenant)
    {
        if (sourceSettings.Authentication != SqlAuthenticationMethod.NotSpecified || sourceSettings.IntegratedSecurity
            || sourceSettings.UserID.Length > 0 || sourceSettings.Password.Length > 0)
            throw new InvalidOperationException("For browser Entra login omit Authentication, User ID, Password and Integrated Security from the source connection string.");
        var credential = new InteractiveBrowserCredential(new InteractiveBrowserCredentialOptions { TenantId = tenant });
        source.AccessToken = (await credential.GetTokenAsync(new TokenRequestContext(["https://database.windows.net/.default"]))).Token;
    }
    await source.OpenAsync();
    var tables = new Dictionary<string, DataTable>();
    // Serializable reads retain locks until all four tables are captured consistently.
    using (var read = source.BeginTransaction(IsolationLevel.Serializable))
    {
        var selections = new Dictionary<string, string>
        {
            ["UserRole"] = "RoleId,RoleName,RoleDescription",
            ["AppUser"] = "UserId,Email,Password,Name,Phone,RoleId",
            ["Client"] = "ClientId,PublicId,WisproId,CreatedById,Name,PobertyLevel,ContractNumber,Email,Street,Number,Phone,PhoneMobile,NationalIdentificationNumber,City,State",
            ["EmailTemplate"] = "Name,Template"
        };
        foreach (var (name, columns) in selections)
        {
            using var command = new SqlCommand($"SELECT {columns} FROM dbo.[{name}]", source, read) { CommandTimeout = 300 };
            using var reader = await command.ExecuteReaderAsync();
            var table = new DataTable(name);
            table.Load(reader);
            tables.Add(name, table);
        }
        read.Commit();
    }
    MigrationChecks.Validate(tables);
    await using var target = new SqlConnection(targetSettings.ConnectionString);
    await target.OpenAsync();
    using var transaction = target.BeginTransaction(IsolationLevel.Serializable);
    async Task<object?> Scalar(string sql)
    {
        using var command = new SqlCommand(sql, target, transaction) { CommandTimeout = 300 };
        return await command.ExecuteScalarAsync();
    }
    await Scalar("SET XACT_ABORT ON; SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON; SET ANSI_PADDING ON; SET ANSI_WARNINGS ON; SET ARITHABORT ON; SET CONCAT_NULL_YIELDS_NULL ON; SET NUMERIC_ROUNDABORT OFF;");
    // Lock the destination through validation and import, preventing concurrent API inserts.
    foreach (var name in new[] { "AppUser", "Client" })
        if (Convert.ToInt64(await Scalar($"SELECT COUNT_BIG(*) FROM dbo.[{name}] WITH (TABLOCKX, HOLDLOCK)")) != 0)
            throw new InvalidOperationException($"Target {name} must be empty. This tool never merges or overwrites users/clients.");

    // Existing role IDs must mean the same thing; do not accidentally grant admin privileges.
    foreach (DataRow row in tables["UserRole"].Rows)
    {
        using var command = new SqlCommand("SELECT RoleName FROM dbo.UserRole WITH (TABLOCKX, HOLDLOCK) WHERE RoleId=@id", target, transaction);
        command.Parameters.AddWithValue("@id", row["RoleId"]);
        var existing = await command.ExecuteScalarAsync();
        if (existing != null && !string.Equals(Convert.ToString(existing), Convert.ToString(row["RoleName"]), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"RoleId {row["RoleId"]} conflicts with the target role. Resolve the mapping before importing.");
    }
    // SQL collation and target constraints remain the final authority during the atomic import.
    Console.WriteLine(string.Join(", ", tables.Select(t => $"{t.Key}: {t.Value.Rows.Count}")));
    if (!args.Contains("--apply"))
    {
        transaction.Rollback();
        Console.WriteLine("Preflight passed; no data written. Run with --apply to import. Target collation/constraint checks also run during import.");
        return 0;
    }

    foreach (DataRow row in tables["UserRole"].Rows)
    {
        using var command = new SqlCommand("IF NOT EXISTS (SELECT 1 FROM dbo.UserRole WHERE RoleId=@id) INSERT dbo.UserRole(RoleId,RoleName,RoleDescription) VALUES (@id,@name,@description)", target, transaction);
        command.Parameters.AddWithValue("@id", row["RoleId"]);
        command.Parameters.AddWithValue("@name", row["RoleName"]);
        command.Parameters.AddWithValue("@description", row["RoleDescription"]);
        await command.ExecuteNonQueryAsync();
    }
    foreach (var name in new[] { "AppUser", "Client" })
    {
        var table = tables[name];
        if (name == "Client")
            foreach (DataRow row in table.Rows)
                foreach (var column in new[] { "Street", "Number" })
                    if (row.IsNull(column)) row[column] = "";
        using var bulk = new SqlBulkCopy(target, SqlBulkCopyOptions.KeepIdentity | SqlBulkCopyOptions.KeepNulls | SqlBulkCopyOptions.CheckConstraints, transaction)
        { DestinationTableName = $"dbo.[{name}]", BulkCopyTimeout = 300 };
        foreach (DataColumn column in table.Columns)
            bulk.ColumnMappings.Add(column.ColumnName, column.ColumnName);
        await bulk.WriteToServerAsync(table);
        if (Convert.ToInt64(await Scalar($"SELECT COUNT_BIG(*) FROM dbo.[{name}]")) != table.Rows.Count)
            throw new InvalidOperationException($"Row count verification failed for {name}.");
        // Keep subsequent API inserts beyond the largest preserved legacy identity.
        if (table.Rows.Count > 0)
            await Scalar($"DBCC CHECKIDENT ('dbo.{name}', RESEED) WITH NO_INFOMSGS;");
    }
    foreach (DataRow row in tables["EmailTemplate"].Rows)
    {
        using var command = new SqlCommand("UPDATE dbo.EmailTemplate SET Template=@body WHERE Name=@name; IF @@ROWCOUNT=0 INSERT dbo.EmailTemplate(Name,Template) VALUES (@name,@body);", target, transaction);
        command.Parameters.AddWithValue("@name", row["Name"]);
        command.Parameters.AddWithValue("@body", row["Template"]);
        await command.ExecuteNonQueryAsync();
    }
    transaction.Commit();
    Console.WriteLine("Import committed. IDs, relationships and password hashes preserved. Review client localities and Siuben states before starting scheduled jobs.");
    return 0;
}
catch (Exception error)
{
    // Do not print provider messages: duplicate-key errors can expose customer data.
    Console.Error.WriteLine(error is SqlException sql
        ? $"SQL operation failed (number {sql.Number}). Any open target transaction was rolled back. Inspect constraints, connectivity and permissions without sharing credentials."
        : error is InvalidOperationException ? error.Message : $"Migration failed ({error.GetType().Name}). No partial import was committed.");
    return 1;
}

static class MigrationChecks
{
    public static void Validate(Dictionary<string, DataTable> tables)
    {
        void Required(string table, params string[] columns)
        {
            foreach (DataRow row in tables[table].Rows)
                foreach (var column in columns)
                    if (row.IsNull(column) || row[column] is string s && string.IsNullOrWhiteSpace(s))
                        throw new InvalidOperationException($"Source {table}.{column} has a null/blank value. Correct the source or prepare an explicitly reviewed mapping.");
        }
        void Unique(string table, string column, Func<object, string>? normalize = null)
        {
            normalize ??= value => Convert.ToString(value)!.TrimEnd();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow row in tables[table].Rows)
                if (!seen.Add(normalize(row[column])))
                    throw new InvalidOperationException($"Source {table}.{column} contains duplicate values.");
        }
        Required("UserRole", "RoleId", "RoleName");
        Required("AppUser", "UserId", "Email", "Password", "Name", "RoleId");
        if (tables["AppUser"].Rows.Cast<DataRow>().Any(r => r.IsNull("Phone")))
            throw new InvalidOperationException("Source AppUser.Phone has a null value.");
        Required("Client", "ClientId", "PublicId", "WisproId", "CreatedById", "Name", "PobertyLevel", "NationalIdentificationNumber");
        Required("EmailTemplate", "Name", "Template");
        Unique("UserRole", "RoleId"); Unique("UserRole", "RoleName");
        Unique("AppUser", "UserId"); Unique("AppUser", "Email");
        Unique("Client", "ClientId"); Unique("Client", "WisproId");
        Unique("Client", "NationalIdentificationNumber", v => Convert.ToString(v)!.Replace("-", "").TrimEnd());
        Unique("EmailTemplate", "Name");
        var roles = tables["UserRole"].Rows.Cast<DataRow>().Select(r => (int)r["RoleId"]).ToHashSet();
        var users = tables["AppUser"].Rows.Cast<DataRow>().Select(r => (int)r["UserId"]).ToHashSet();
        if (tables["AppUser"].Rows.Cast<DataRow>().Any(r => !roles.Contains((int)r["RoleId"]))
            || tables["Client"].Rows.Cast<DataRow>().Any(r => !users.Contains((int)r["CreatedById"])))
            throw new InvalidOperationException("Source contains broken role/user relationships.");
    }

    public static void SelfTest()
    {
        Dictionary<string, DataTable> Fixture()
        {
            var result = new Dictionary<string, DataTable>();
            void Add(string name, string[] columns, object[] values)
            {
                var table = new DataTable(name);
                foreach (var column in columns) table.Columns.Add(column, column != "WisproId" && (column.EndsWith("Id") || column == "PobertyLevel") ? typeof(int) : typeof(string));
                table.Rows.Add(values); result.Add(name, table);
            }
            Add("UserRole", ["RoleId", "RoleName"], [1, "Admin"]);
            Add("AppUser", ["UserId", "Email", "Password", "Name", "Phone", "RoleId"], [7, "test@example.com", "unchanged-hash", "Test", "", 1]);
            Add("Client", ["ClientId", "PublicId", "WisproId", "CreatedById", "Name", "PobertyLevel", "NationalIdentificationNumber"], [9, 10, "wispro-42", 7, "Test", 1, "000-0000000-1"]);
            Add("EmailTemplate", ["Name", "Template"], ["Test", "<p>Test</p>"]);
            return result;
        }
        Validate(Fixture());
        void Reject(Action<Dictionary<string, DataTable>> mutate)
        {
            var tables = Fixture(); mutate(tables);
            try { Validate(tables); } catch (InvalidOperationException) { return; }
            throw new Exception("Expected preflight rejection.");
        }
        Reject(t => t["Client"].Rows[0]["PublicId"] = DBNull.Value);
        Reject(t => t["AppUser"].Rows[0]["RoleId"] = DBNull.Value);
        Reject(t => t["Client"].Rows[0]["CreatedById"] = 999);
        Reject(t => t["AppUser"].Rows[0]["RoleId"] = 999);
        Reject(t => t["EmailTemplate"].ImportRow(t["EmailTemplate"].Rows[0]));
        Reject(t => { var row = t["Client"].NewRow(); row.ItemArray = t["Client"].Rows[0].ItemArray; row["ClientId"] = 10; row["WisproId"] = 43; row["NationalIdentificationNumber"] = "00000000001"; t["Client"].Rows.Add(row); });
        Console.WriteLine("7 migration preflight checks passed.");
    }
}
