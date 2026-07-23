using MigrationOps.Core.MigrationFramework.Services;

namespace MigrationOps.Core.Tests
{
    public class DetermineDatabaseFromTagsTests
    {
        // DetermineDatabaseFromTags reads the "Databases" section of configuration, so each
        // test points a MigrationService at a throwaway dbconfig.json with just the database
        // names it needs (connection strings are never used by this method).
        private static MigrationService CreateServiceWithDatabases(params string[] databaseNames)
        {
            var entries = string.Join(",", databaseNames.Select(n => $"\"{n}\": {{ \"ConnectionString\": \"unused\" }}"));
            var json = $"{{ \"Databases\": {{ {entries} }} }}";

            using var configFile = new TempFile(json, ".json");
            return new MigrationService(configFile.Path);
        }

        [Fact]
        public void ReturnsMatchingDatabaseName()
        {
            var service = CreateServiceWithDatabases("Db1", "Db2");

            Assert.Equal("Db1", service.DetermineDatabaseFromTags(new List<string> { "Db1" }));
        }

        [Fact]
        public void MatchesConfiguredDatabaseCaseInsensitively()
        {
            var service = CreateServiceWithDatabases("Db1");

            // Note: the match is case-insensitive, but the method returns the tag as written
            // in the file, not the configured key's casing. That's safe downstream only
            // because IConfiguration's own indexer (used by GetConnectionString) is itself
            // case-insensitive - this test documents current behavior rather than asserting
            // it is the ideal API.
            Assert.Equal("db1", service.DetermineDatabaseFromTags(new List<string> { "db1" }));
        }

        [Fact]
        public void ReturnsFirstTagThatMatchesAConfiguredDatabase()
        {
            var service = CreateServiceWithDatabases("Db1", "Db2");

            Assert.Equal("Db2", service.DetermineDatabaseFromTags(new List<string> { "UnknownTag", "Db2" }));
        }

        [Fact]
        public void ThrowsWhenNoTagMatchesAConfiguredDatabase()
        {
            var service = CreateServiceWithDatabases("Db1", "Db2");

            Assert.Throws<InvalidOperationException>(() => service.DetermineDatabaseFromTags(new List<string> { "Db3" }));
        }
    }
}
