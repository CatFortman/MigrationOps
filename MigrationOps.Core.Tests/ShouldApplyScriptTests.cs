using MigrationOps.Core.MigrationFramework.Services;

namespace MigrationOps.Core.Tests
{
    public class ShouldApplyScriptTests
    {
        [Fact]
        public void MatchesExactTag()
        {
            Assert.True(MigrationService.ShouldApplyScript(new List<string> { "Db1", "Db2" }, "Db1"));
        }

        [Fact]
        public void IsCaseInsensitive()
        {
            Assert.True(MigrationService.ShouldApplyScript(new List<string> { "db1" }, "DB1"));
        }

        [Fact]
        public void ReturnsFalseWhenTagNotPresent()
        {
            Assert.False(MigrationService.ShouldApplyScript(new List<string> { "Db2" }, "Db1"));
        }

        [Fact]
        public void ReturnsFalseForEmptyTagList()
        {
            Assert.False(MigrationService.ShouldApplyScript(new List<string>(), "Db1"));
        }
    }
}
