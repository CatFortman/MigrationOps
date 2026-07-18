using MigrationOps.Core.MigrationFramework.Services;
using MigrationOps.Core.Models;

namespace MigrationOps.Dashboard.Data
{
    public class DatabaseOverview
    {
        public string Name { get; set; } = string.Empty;
        public List<MigrationHistoryRecord> History { get; set; } = new();
        public List<MigrationFileStatus> FileStatuses { get; set; } = new();
    }

    // Thin read-only wrapper around MigrationOps.Core.MigrationService, reused as-is so the
    // dashboard shares the exact tag/checksum/drift logic the ConsoleApp runner uses.
    public class MigrationDataService
    {
        private readonly MigrationService _migrationService;
        private readonly string _migrationsRoot;

        public MigrationDataService(IConfiguration configuration)
        {
            var dbConfigPath = configuration["DbConfigPath"]
                ?? throw new InvalidOperationException("DbConfigPath is not configured.");
            _migrationsRoot = Path.GetFullPath(configuration["MigrationsRoot"]
                ?? throw new InvalidOperationException("MigrationsRoot is not configured."));

            _migrationService = new MigrationService(dbConfigPath);
        }

        public List<string> GetDatabaseNames() => _migrationService.GetDatabaseNames();

        public DatabaseOverview GetDatabaseOverview(string databaseName)
        {
            var connectionString = _migrationService.GetConnectionString(databaseName);
            var history = _migrationService.GetMigrationHistory(connectionString);
            var fileStatuses = _migrationService.GetMigrationFileStatuses(_migrationsRoot, databaseName, history);

            return new DatabaseOverview
            {
                Name = databaseName,
                History = history,
                FileStatuses = fileStatuses
            };
        }

        public List<DatabaseOverview> GetAllDatabaseOverviews()
        {
            return GetDatabaseNames().Select(GetDatabaseOverview).ToList();
        }
    }
}
