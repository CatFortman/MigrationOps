using MigrationOps.Core.MigrationFramework.Services;

class Program
{
    static void Main(string[] args)
    {
        var migrationsDirectory = "Migrations"; // Adjust the path as needed.

        MigrationService migrationService = new MigrationService();
        migrationService.ApplyMigrations(migrationsDirectory);
    }
}
