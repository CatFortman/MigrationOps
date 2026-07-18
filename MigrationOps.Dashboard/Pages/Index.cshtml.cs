using Microsoft.AspNetCore.Mvc.RazorPages;
using MigrationOps.Dashboard.Data;

namespace MigrationOps.Dashboard.Pages
{
    public class IndexModel : PageModel
    {
        private readonly MigrationDataService _dataService;

        public IndexModel(MigrationDataService dataService)
        {
            _dataService = dataService;
        }

        public List<DatabaseOverview> Overviews { get; private set; } = new();

        public int TotalApplied { get; private set; }
        public int RecentFailures { get; private set; }
        public int TotalPending { get; private set; }
        public int TotalDrift { get; private set; }
        public DateTime? LastActivity { get; private set; }

        public void OnGet()
        {
            Overviews = _dataService.GetAllDatabaseOverviews();

            var recentCutoff = DateTime.Now.AddDays(-7);

            TotalApplied = Overviews.Sum(o => o.History.Count(h => h.Success));
            RecentFailures = Overviews.Sum(o => o.History.Count(h => !h.Success && h.AppliedOn >= recentCutoff));
            TotalPending = Overviews.Sum(o => o.FileStatuses.Count(f => !f.IsApplied));
            TotalDrift = Overviews.Sum(o => o.FileStatuses.Count(f => f.HasDrift));
            LastActivity = Overviews.SelectMany(o => o.History).Select(h => (DateTime?)h.AppliedOn).Max();
        }
    }
}
