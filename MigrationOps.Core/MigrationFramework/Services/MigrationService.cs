using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using MigrationOps.Core.MigrationFramework.AppConstants;

namespace MigrationOps.Core.MigrationFramework.Services
{
    public class MigrationService
    {
        private static readonly string[] DatabaseObjectFolders = { "Functions", "Views", "StoredProcedures", "Triggers" };

        private readonly string _connectionStringTemplate;
        private readonly IConfiguration _configuration;

        public MigrationService()
        {
            // Layering, lowest to highest precedence:
            //   1. dbconfig.json          - committed template, no real secrets
            //   2. dbconfig.local.json    - gitignored, per-developer local overrides
            //   3. environment variables  - e.g. Databases__Db1__ConnectionString, used in CI/CD
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("Configurations/dbconfig.json", optional: true, reloadOnChange: true)
                .AddJsonFile("Configurations/dbconfig.local.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();

            _configuration = builder.Build();

            _connectionStringTemplate = _configuration["DatabaseSettings:ConnectionStringTemplate"];
        }

        private string GetConnectionString(string databaseName)
        {
            return _configuration[$"Databases:{databaseName}:ConnectionString"];
        }

        public string GetMigrationDirectory()
        {
            return _configuration["MigrationSettings:MigrationDirectory"];
        }

        public string GetScriptDirectory()
        {
            return _configuration["MigrationSettings:ScriptDirectory"];
        }

        public static List<string> ParseTagsFromFile(string filePath)
        {
            var tags = new List<string>();
            var lines = File.ReadLines(filePath);

            foreach (var line in lines)
            {
                if (line.StartsWith("-- Tags:"))
                {
                    var tagsLine = line.Split(':')[1].Trim();
                    tags = tagsLine.Split(',').Select(tag => tag.Trim()).ToList();
                    break;
                }
            }

            if (tags.Count == 0)
            {
                throw new InvalidOperationException($"The script {Path.GetFileName(filePath)} does not contain a 'Tags' comment.");
            }

            return tags;
        }

        public static bool ShouldApplyScript(List<string> tags, string currentDb)
        {
            return tags.Contains(currentDb, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Applies database object scripts (functions, views, stored procedures, triggers) from the
        /// configured script directory. Runs before migrations so that migrations can rely on the
        /// latest object definitions.
        ///
        /// A script whose SQL fails here (e.g. a view referencing a table a pending migration
        /// creates) is deferred rather than fatal — the caller retries the returned files with
        /// <see cref="RetryDeferredScripts"/> after migrations run. Validation failures (missing
        /// checksum/tags, no CREATE OR ALTER) still throw immediately, since a retry cannot fix them.
        /// </summary>
        /// <returns>The scripts that failed to apply and should be retried after migrations.</returns>
        public List<string> ApplyDatabaseObjectScripts(string scriptsRootDirectory)
        {
            var files = DatabaseObjectFolders
                .Select(folder => Path.Combine(scriptsRootDirectory, folder))
                .Where(Directory.Exists)
                .SelectMany(folder => Directory.GetFiles(folder, "*.sql").OrderBy(f => Path.GetFileName(f)))
                .ToList();

            var deferred = new List<string>();

            foreach (var file in files)
            {
                if (!ApplyScriptFile(file, ScriptKind.DatabaseObject, deferSqlFailures: true))
                {
                    deferred.Add(file);
                }
            }

            return deferred;
        }

        /// <summary>
        /// Retries database object scripts deferred by <see cref="ApplyDatabaseObjectScripts"/>.
        /// By this point migrations have run, so any remaining failure is a real error and throws.
        /// </summary>
        public void RetryDeferredScripts(List<string> deferredFiles)
        {
            foreach (var file in deferredFiles)
            {
                ApplyScriptFile(file, ScriptKind.DatabaseObject);
            }
        }

        public void ApplyMigrations(string directory)
        {
            var files = Directory.GetFiles(directory, "*.sql")
                                 .OrderBy(f => Path.GetFileName(f))
                                 .ToList();

            foreach (var file in files)
            {
                ApplyScriptFile(file, ScriptKind.Migration);
            }
        }

        private enum ScriptKind
        {
            Migration,
            DatabaseObject
        }

        private bool ApplyScriptFile(string file, ScriptKind kind, bool deferSqlFailures = false)
        {
            string scriptName = Path.GetFileName(file);
            string kindLabel = kind == ScriptKind.Migration ? "migration" : "database object script";

            List<string> tags;
            string checksum;
            string script = File.ReadAllText(file);

            try
            {
                tags = ParseTagsFromFile(file);
                checksum = ExtractChecksumFromScript(script);

                if (kind == ScriptKind.DatabaseObject)
                {
                    EnsureCreateOrAlterStatement(script, scriptName);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to process {kindLabel} '{scriptName}': {ex.Message}", ex);
            }

            foreach (var tag in tags)
            {
                string currentDb;
                string connectionString;

                try
                {
                    currentDb = DetermineDatabaseFromTags(new List<string> { tag });
                    connectionString = GetConnectionString(currentDb);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Failed to resolve target database for {kindLabel} '{scriptName}' (tag '{tag}'): {ex.Message}", ex);
                }

                EnsureHistoryTable(connectionString, kind);

                if (HasBeenApplied(connectionString, scriptName, checksum, kind))
                {
                    Console.WriteLine($"Skipping {scriptName} as it has already been applied to {currentDb}");
                    continue;
                }

                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            using (var command = new SqlCommand(script, connection, transaction))
                            {
                                command.ExecuteNonQuery();
                            }

                            RecordApplied(connection, transaction, scriptName, checksum, kind);

                            transaction.Commit();
                            Console.WriteLine($"Applied {scriptName} to {currentDb} on the specified server");
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();

                            if (deferSqlFailures)
                            {
                                Console.WriteLine(
                                    $"Deferring {scriptName} on {currentDb} (will retry after migrations): {ex.Message}");
                                return false;
                            }

                            throw new InvalidOperationException(
                                $"Failed to apply {kindLabel} '{scriptName}' to database '{currentDb}' (rolled back): {ex.Message}", ex);
                        }
                    }
                }
            }

            return true;
        }

        public string DetermineDatabaseFromTags(List<string> tags)
        {
            // Get list of known databases.
            var knownDatabases = _configuration.GetSection("Databases").GetChildren().Select(db => db.Key).ToList();

            // Check each tag to see if it matches a known database.
            foreach (var tag in tags)
            {
                if (knownDatabases.Contains(tag, StringComparer.OrdinalIgnoreCase))
                {
                    return tag;
                }
            }

            throw new InvalidOperationException("No valid database found in tags.");
        }

        private void EnsureHistoryTable(string connectionString, ScriptKind kind)
        {
            var sql = kind == ScriptKind.Migration
                ? SqlStatements.CreateMigrationHistoryTable
                : SqlStatements.CreateScriptHistoryTable;

            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                var command = new SqlCommand(sql, connection);
                command.ExecuteNonQuery();
            }
        }

        private void RecordApplied(SqlConnection connection, SqlTransaction transaction, string scriptName, string checksum, ScriptKind kind)
        {
            var sql = kind == ScriptKind.Migration ? SqlStatements.InsertMigrationRecord : SqlStatements.InsertScriptRecord;
            var paramName = kind == ScriptKind.Migration ? "@MigrationName" : "@ScriptName";

            using (var command = new SqlCommand(sql, connection, transaction))
            {
                command.Parameters.AddWithValue(paramName, scriptName);
                command.Parameters.AddWithValue("@Checksum", checksum);
                command.ExecuteNonQuery();
            }
        }

        private bool HasBeenApplied(string connectionString, string scriptName, string checksum, ScriptKind kind)
        {
            var sql = kind == ScriptKind.Migration ? SqlStatements.CheckMigrationApplied : SqlStatements.CheckScriptApplied;
            var paramName = kind == ScriptKind.Migration ? "@MigrationName" : "@ScriptName";

            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                var command = new SqlCommand(sql, connection);
                command.Parameters.AddWithValue(paramName, scriptName);
                command.Parameters.AddWithValue("@Checksum", checksum);
                return (int)command.ExecuteScalar() > 0;
            }
        }

        public string ExtractChecksumFromScript(string script)
        {
            var lines = script.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            foreach (var line in lines)
            {
                if (line.StartsWith("-- Checksum:"))
                {
                    return line.Split(':')[1].Trim(); // Extract the checksum value after "-- Checksum: "
                }
            }

            throw new InvalidOperationException("No checksum found in the script.");
        }

        /// <summary>
        /// Database object scripts must be idempotent, since they are re-run on every deploy.
        /// This enforces that the first executable statement (after the checksum/tags header
        /// comments) is a CREATE OR ALTER, rather than a plain CREATE that fails on redeploy.
        /// </summary>
        private static void EnsureCreateOrAlterStatement(string script, string scriptName)
        {
            var lines = script.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                if (trimmed.Length == 0 || trimmed.StartsWith("--"))
                {
                    continue;
                }

                if (!trimmed.StartsWith("CREATE OR ALTER", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Database object script '{scriptName}' must begin with a 'CREATE OR ALTER' statement.");
                }

                return;
            }

            throw new InvalidOperationException($"Database object script '{scriptName}' is empty or contains no executable statement.");
        }
    }
}
