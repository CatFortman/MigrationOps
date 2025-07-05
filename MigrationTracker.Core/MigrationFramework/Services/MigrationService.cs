using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace TrackGateSql.Core.MigrationFramework.Services
{
    public class MigrationService
    {
        private readonly string _connectionStringTemplate;
        private readonly IConfiguration _configuration;

        public MigrationService()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("Configurations/dbconfig.json", optional: true, reloadOnChange: true);

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

        public void ApplyMigrations(string directory)
        {
            var files = Directory.GetFiles(directory, "*.sql")
                                 .OrderBy(f => Path.GetFileName(f))
                                 .ToList();

            foreach (var file in files)
            {
                var tags = ParseTagsFromFile(file);

                foreach (var tag in tags)
                {
                    var currentDb = DetermineDatabaseFromTags(new List<string> { tag });
                    string connectionString = GetConnectionString(currentDb);

                    EnsureMigrationHistoryTable(connectionString);

                    string migrationName = Path.GetFileName(file);
                    string script = File.ReadAllText(file);

                    // Extract the checksum from the script.
                    string checksum = ExtractChecksumFromScript(script);

                    if (HasMigrationBeenApplied(connectionString, migrationName, checksum))
                    {
                        Console.WriteLine($"Skipping {migrationName} as it has already been applied to {currentDb}");
                        continue;
                    }

                    using (var connection = new SqlConnection(connectionString))
                    {
                        connection.Open();

                        using (var command = new SqlCommand(script, connection))
                        {
                            try
                            {
                                command.ExecuteNonQuery();
                                Console.WriteLine($"Applied {migrationName} to {currentDb} on the specified server");

                                RecordMigration(connectionString, migrationName, checksum);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error applying {migrationName} to {currentDb}: {ex.Message}");
                            }
                        }
                    }
                }
            }
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


        public void EnsureMigrationHistoryTable(string connectionString)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                var command = new SqlCommand(AppConstants.SqlStatements.CreateMigrationHistoryTable, connection);
                command.ExecuteNonQuery();
            }
        }

        public void RecordMigration(string connectionString, string migrationName, string checksum)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                var command = new SqlCommand(AppConstants.SqlStatements.InsertMigrationRecord, connection);
                command.Parameters.AddWithValue("@MigrationName", migrationName);
                command.Parameters.AddWithValue("@Checksum", checksum);
                command.ExecuteNonQuery();
            }
        }

        public bool HasMigrationBeenApplied(string connectionString, string migrationName, string checksum)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                var command = new SqlCommand(AppConstants.SqlStatements.CheckMigrationApplied, connection);
                command.Parameters.AddWithValue("@MigrationName", migrationName);
                command.Parameters.AddWithValue("@Checksum", checksum);
                return (int)command.ExecuteScalar() > 0;
            }
        }

        public string ExtractChecksumFromScript(string script)
        {
            var lines = script.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

            foreach (var line in lines)
            {
                if (line.StartsWith("-- Checksum:"))
                {
                    return line.Split(':')[1].Trim(); // Extract the checksum value after "-- Checksum: "
                }
            }

            throw new InvalidOperationException("No checksum found in the script.");
        }



    }
}
