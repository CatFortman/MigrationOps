namespace MigrationOps.Core.MigrationFramework.Services
{
    public interface IMigrationAlertNotifier
    {
        Task NotifyFailureAsync(string migrationName, string database, string errorMessage);
    }
}
