using MigrationOps.Core.MigrationFramework.Services;

namespace MigrationOps.Core.Tests
{
    public class ExtractChecksumFromScriptTests
    {
        private readonly MigrationService _service = TestMigrationService.Create();

        [Fact]
        public void ExtractsChecksumValue()
        {
            var script = "-- Tags: Db1\n-- Checksum: abc123\nSELECT 1;";

            Assert.Equal("abc123", _service.ExtractChecksumFromScript(script));
        }

        [Fact]
        public void TrimsWhitespaceAroundChecksum()
        {
            var script = "-- Checksum:   abc123   \nSELECT 1;";

            Assert.Equal("abc123", _service.ExtractChecksumFromScript(script));
        }

        [Fact]
        public void HandlesWindowsLineEndings()
        {
            var script = "-- Tags: Db1\r\n-- Checksum: winval\r\nSELECT 1;";

            Assert.Equal("winval", _service.ExtractChecksumFromScript(script));
        }

        [Fact]
        public void HandlesUnixLineEndings()
        {
            var script = "-- Tags: Db1\n-- Checksum: unixval\nSELECT 1;";

            Assert.Equal("unixval", _service.ExtractChecksumFromScript(script));
        }

        [Fact]
        public void ThrowsWhenNoChecksumLinePresent()
        {
            var script = "-- Tags: Db1\nSELECT 1;";

            Assert.Throws<InvalidOperationException>(() => _service.ExtractChecksumFromScript(script));
        }

        [Fact]
        public void ThrowsForEmptyScript()
        {
            Assert.Throws<InvalidOperationException>(() => _service.ExtractChecksumFromScript(""));
        }
    }
}
