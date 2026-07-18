namespace MigrationOps.Core.Models
{
    public class ScriptHistoryRecord
    {
        public int ScriptId { get; set; }
        public string ScriptName { get; set; } = string.Empty;
        public DateTime AppliedOn { get; set; }
        public string? Checksum { get; set; }
    }
}
