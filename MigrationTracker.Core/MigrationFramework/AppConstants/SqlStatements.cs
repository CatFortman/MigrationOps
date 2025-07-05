namespace TrackGateSql.Core.MigrationFramework.AppConstants
{
    public static class SqlStatements
    {
        // SQL for creating the __MigrationHistory table.
        public static readonly string CreateMigrationHistoryTable = @"
            IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '__MigrationHistory')
            BEGIN
                CREATE TABLE __MigrationHistory (
                    MigrationId INT PRIMARY KEY IDENTITY(1,1),
                    MigrationName NVARCHAR(255) NOT NULL,
                    AppliedOn DATETIME NOT NULL,
                    Checksum NVARCHAR(64) NULL
                );
            END";

        // SQL for inserting a migration record.
        public static readonly string InsertMigrationRecord = @"
            INSERT INTO __MigrationHistory (MigrationName, AppliedOn, Checksum)
            VALUES (@MigrationName, GETDATE(), @Checksum)";

        // SQL for checking if a migration has been applied.
        public static readonly string CheckMigrationApplied = @"
           SELECT COUNT(1)
           FROM __MigrationHistory
           WHERE MigrationName = @MigrationName AND Checksum = @Checksum";

    }
}
