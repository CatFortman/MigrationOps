namespace MigrationOps.Core.MigrationFramework.AppConstants
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
                    Checksum NVARCHAR(64) NULL,
                    Success BIT NOT NULL DEFAULT 1,
                    ErrorMessage NVARCHAR(MAX) NULL,
                    DurationMs INT NULL
                );
            END
            ELSE
            BEGIN
                IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '__MigrationHistory' AND COLUMN_NAME = 'Success')
                    ALTER TABLE __MigrationHistory ADD Success BIT NOT NULL DEFAULT 1;

                IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '__MigrationHistory' AND COLUMN_NAME = 'ErrorMessage')
                    ALTER TABLE __MigrationHistory ADD ErrorMessage NVARCHAR(MAX) NULL;

                IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '__MigrationHistory' AND COLUMN_NAME = 'DurationMs')
                    ALTER TABLE __MigrationHistory ADD DurationMs INT NULL;
            END";

        // SQL for inserting a migration record.
        public static readonly string InsertMigrationRecord = @"
            INSERT INTO __MigrationHistory (MigrationName, AppliedOn, Checksum, Success, ErrorMessage, DurationMs)
            VALUES (@MigrationName, GETDATE(), @Checksum, @Success, @ErrorMessage, @DurationMs)";

        // SQL for checking if a migration has been successfully applied (a failed attempt does not block retry).
        public static readonly string CheckMigrationApplied = @"
           SELECT COUNT(1)
           FROM __MigrationHistory
           WHERE MigrationName = @MigrationName AND Checksum = @Checksum AND Success = 1";

        // SQL for reading the full migration history, most recent first.
        public static readonly string SelectMigrationHistory = @"
           SELECT MigrationId, MigrationName, AppliedOn, Checksum, Success, ErrorMessage, DurationMs
           FROM __MigrationHistory
           ORDER BY AppliedOn DESC";

        // SQL for creating the __ScriptHistory table.
        public static readonly string CreateScriptHistoryTable = @"
            IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '__ScriptHistory')
            BEGIN
                CREATE TABLE __ScriptHistory (
                    ScriptId INT PRIMARY KEY IDENTITY(1,1),
                    ScriptName NVARCHAR(255) NOT NULL,
                    AppliedOn DATETIME NOT NULL,
                    Checksum NVARCHAR(64) NULL
                );
            END";

        // SQL for inserting a database object script record.
        public static readonly string InsertScriptRecord = @"
            INSERT INTO __ScriptHistory (ScriptName, AppliedOn, Checksum)
            VALUES (@ScriptName, GETDATE(), @Checksum)";

        // SQL for checking if a database object script has been applied.
        public static readonly string CheckScriptApplied = @"
           SELECT COUNT(1)
           FROM __ScriptHistory
           WHERE ScriptName = @ScriptName AND Checksum = @Checksum";

        // SQL for reading the full database object script history, most recent first.
        public static readonly string SelectScriptHistory = @"
           SELECT ScriptId, ScriptName, AppliedOn, Checksum
           FROM __ScriptHistory
           ORDER BY AppliedOn DESC";

    }
}
