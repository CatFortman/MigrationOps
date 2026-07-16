namespace MigrationOps.Core.Models
{
    public class MigrationHistoryRecord
    {
        public int MigrationId { get; set; }
        public string MigrationName { get; set; } = string.Empty;
        public DateTime AppliedOn { get; set; }
        public string? Checksum { get; set; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public int? DurationMs { get; set; }
    }
}
