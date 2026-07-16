using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using MigrationOps.Core.MigrationFramework.AppConstants;
using MigrationOps.Core.Models;

namespace MigrationOps.Core.MigrationFramework.Services
{
    public class MigrationService
    {
        private readonly string _connectionStringTemplate;
        private readonly IConfiguration _configuration;
        private readonly IMigrationAlertNotifier _alertNotifier;

        public MigrationService()
            : this(Path.Combine(Directory.GetCurrentDirectory(), "Configurations", "dbconfig.json"))
        {
        }

        // Lets callers whose working directory isn't the ConsoleApp's (e.g. the Dashboard) point
        // directly at the shared dbconfig.json instead of resolving it via the current directory.
        public MigrationService(string dbConfigFilePath)
        {
            var fullPath = Path.GetFullPath(dbConfigFilePath);
            var basePath = Path.GetDirectoryName(fullPath) ?? Directory.GetCurrentDirectory();
            var fileName = Path.GetFileName(fullPath);

            var builder = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile(fileName, optional: true, reloadOnChange: true);

            _configuration = builder.Build();

            _connectionStringTemplate = _configuration["DatabaseSettings:ConnectionStringTemplate"];

            var alertsEnabled = bool.TryParse(_configuration["AlertSettings:Enabled"], out var enabled) && enabled;
            _alertNotifier = new WebhookAlertNotifier(_configuration["AlertSettings:WebhookUrl"], alertsEnabled);
        }

        public string GetConnectionString(string databaseName)
        {
            return _configuration[$"Databases:{databaseName}:ConnectionString"];
        }

        public string GetMigrationDirectory()
        {
            return _configuration["MigrationSettings:MigrationDirectory"];
        }

        public List<string> GetDatabaseNames()
        {
            return _configuration.GetSection("Databases").GetChildren().Select(db => db.Key).ToList();
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
                            var stopwatch = Stopwatch.StartNew();

                            try
                            {
                                command.ExecuteNonQuery();
                                stopwatch.Stop();
                                Console.WriteLine($"Applied {migrationName} to {currentDb} on the specified server");

                                RecordMigration(connectionString, migrationName, checksum, success: true, errorMessage: null, durationMs: (int)stopwatch.ElapsedMilliseconds);
                            }
                            catch (Exception ex)
                            {
                                stopwatch.Stop();
                                Console.WriteLine($"Error applying {migrationName} to {currentDb}: {ex.Message}");

                                RecordMigration(connectionString, migrationName, checksum, success: false, errorMessage: ex.Message, durationMs: (int)stopwatch.ElapsedMilliseconds);

                                _alertNotifier.NotifyFailureAsync(migrationName, currentDb, ex.Message).GetAwaiter().GetResult();
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
                var command = new SqlCommand(SqlStatements.CreateMigrationHistoryTable, connection);
                command.ExecuteNonQuery();
            }
        }

        public void RecordMigration(string connectionString, string migrationName, string checksum, bool success, string? errorMessage, int durationMs)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                var command = new SqlCommand(AppConstants.SqlStatements.InsertMigrationRecord, connection);
                command.Parameters.AddWithValue("@MigrationName", migrationName);
                command.Parameters.AddWithValue("@Checksum", checksum);
                command.Parameters.AddWithValue("@Success", success);
                command.Parameters.AddWithValue("@ErrorMessage", (object?)errorMessage ?? DBNull.Value);
                command.Parameters.AddWithValue("@DurationMs", durationMs);
                command.ExecuteNonQuery();
            }
        }

        public List<MigrationHistoryRecord> GetMigrationHistory(string connectionString)
        {
            EnsureMigrationHistoryTable(connectionString);

            var records = new List<MigrationHistoryRecord>();

            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (var command = new SqlCommand(AppConstants.SqlStatements.SelectMigrationHistory, connection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        records.Add(new MigrationHistoryRecord
                        {
                            MigrationId = reader.GetInt32(reader.GetOrdinal("MigrationId")),
                            MigrationName = reader.GetString(reader.GetOrdinal("MigrationName")),
                            AppliedOn = reader.GetDateTime(reader.GetOrdinal("AppliedOn")),
                            Checksum = reader.IsDBNull(reader.GetOrdinal("Checksum")) ? null : reader.GetString(reader.GetOrdinal("Checksum")),
                            Success = reader.GetBoolean(reader.GetOrdinal("Success")),
                            ErrorMessage = reader.IsDBNull(reader.GetOrdinal("ErrorMessage")) ? null : reader.GetString(reader.GetOrdinal("ErrorMessage")),
                            DurationMs = reader.IsDBNull(reader.GetOrdinal("DurationMs")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("DurationMs"))
                        });
                    }
                }
            }

            return records;
        }

        // Diffs the migration files targeting `database` against its history to report what's
        // pending and whether an already-applied file's contents have drifted from what was recorded.
        public List<MigrationFileStatus> GetMigrationFileStatuses(string migrationsDirectory, string database, List<MigrationHistoryRecord> history)
        {
            var statuses = new List<MigrationFileStatus>();

            var latestSuccessChecksum = history
                .Where(h => h.Success)
                .GroupBy(h => h.MigrationName)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(h => h.AppliedOn).First().Checksum);

            var files = Directory.GetFiles(migrationsDirectory, "*.sql").OrderBy(f => Path.GetFileName(f));

            foreach (var file in files)
            {
                var tags = ParseTagsFromFile(file);

                if (!ShouldApplyScript(tags, database))
                {
                    continue;
                }

                var fileName = Path.GetFileName(file);

                string currentChecksum;
                try
                {
                    currentChecksum = ExtractChecksumFromScript(File.ReadAllText(file));
                }
                catch (InvalidOperationException)
                {
                    // No "-- Checksum:" line yet — a migration still being drafted, not yet
                    // committed through the pre-commit hook. Report it distinctly rather than
                    // failing the whole status listing for one in-progress file.
                    statuses.Add(new MigrationFileStatus
                    {
                        FileName = fileName,
                        Tags = tags,
                        ChecksumMissing = true
                    });
                    continue;
                }

                var isApplied = history.Any(h => h.Success && h.MigrationName == fileName && h.Checksum == currentChecksum);
                var hasRecordedChecksum = latestSuccessChecksum.TryGetValue(fileName, out var recordedChecksum);

                statuses.Add(new MigrationFileStatus
                {
                    FileName = fileName,
                    Tags = tags,
                    IsApplied = isApplied,
                    HasDrift = hasRecordedChecksum && recordedChecksum != currentChecksum,
                    RecordedChecksum = hasRecordedChecksum ? recordedChecksum : null,
                    CurrentChecksum = currentChecksum
                });
            }

            return statuses;
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
