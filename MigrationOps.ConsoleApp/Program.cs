using MigrationOps.Core.MigrationFramework.Services;

class Program
{
    static int Main(string[] args)
    {
        MigrationService migrationService = new MigrationService();

        try
        {
            // Database objects (functions, views, stored procedures, triggers) are applied before
            // migrations so that migration scripts can rely on the latest object definitions.
            migrationService.ApplyDatabaseObjectScripts(migrationService.GetScriptDirectory());
            migrationService.ApplyMigrations(migrationService.GetMigrationDirectory());
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"MigrationOps run halted: {ex.Message}");
            return 1;
        }

        return 0;
    }
}
