using MigrationOps.Core.Models;
using MigrationOps.Dashboard.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MigrationOps.Dashboard.Pages
{
    public class HistoryRow
    {
        public string Database { get; set; } = string.Empty;
        public MigrationHistoryRecord Record { get; set; } = new();
    }

    public class HistoryModel : PageModel
    {
        private readonly MigrationDataService _dataService;

        public HistoryModel(MigrationDataService dataService)
        {
            _dataService = dataService;
        }

        [BindProperty(SupportsGet = true)]
        public string? Database { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Status { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? From { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? To { get; set; }

        public List<string> DatabaseNames { get; private set; } = new();
        public List<HistoryRow> Rows { get; private set; } = new();

        public void OnGet()
        {
            DatabaseNames = _dataService.GetDatabaseNames();

            var rows = _dataService.GetAllDatabaseOverviews()
                .SelectMany(o => o.History.Select(h => new HistoryRow { Database = o.Name, Record = h }))
                .AsEnumerable();

            if (!string.IsNullOrEmpty(Database))
            {
                rows = rows.Where(r => string.Equals(r.Database, Database, StringComparison.OrdinalIgnoreCase));
            }

            if (Status == "success")
            {
                rows = rows.Where(r => r.Record.Success);
            }
            else if (Status == "failed")
            {
                rows = rows.Where(r => !r.Record.Success);
            }

            if (From.HasValue)
            {
                rows = rows.Where(r => r.Record.AppliedOn >= From.Value);
            }

            if (To.HasValue)
            {
                rows = rows.Where(r => r.Record.AppliedOn <= To.Value.AddDays(1));
            }

            Rows = rows.OrderByDescending(r => r.Record.AppliedOn).ToList();
        }
    }
}
