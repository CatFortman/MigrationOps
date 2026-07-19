using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MigrationOps.Core.Models;
using MigrationOps.Dashboard.Data;

namespace MigrationOps.Dashboard.Pages
{
    public class DryRunModel : PageModel
    {
        private readonly MigrationDataService _dataService;

        public DryRunModel(MigrationDataService dataService)
        {
            _dataService = dataService;
        }

        public List<string> DatabaseNames { get; private set; } = new();
        public string? Database { get; private set; }
        public bool VerifyRan { get; private set; }
        public DryRunPlan Plan { get; private set; } = new();

        // Target databases plus "(unresolved)" when tagless files were found, matching the
        // console report's grouping.
        public List<string> Groups { get; private set; } = new();

        // Same criteria as the console's exit code: any Changed, ValidationError, or
        // VerifyFailed entry fails the dry-run.
        public bool Succeeded { get; private set; }

        public IActionResult OnGet(string? database)
        {
            return Run(database, verify: false);
        }

        public IActionResult OnPostVerify(string? database)
        {
            return Run(database, verify: true);
        }

        private IActionResult Run(string? database, bool verify)
        {
            DatabaseNames = _dataService.GetDatabaseNames();

            if (!string.IsNullOrEmpty(database))
            {
                Database = DatabaseNames
                    .FirstOrDefault(n => string.Equals(n, database, StringComparison.OrdinalIgnoreCase));

                if (Database == null)
                {
                    return NotFound();
                }
            }

            VerifyRan = verify;
            Plan = _dataService.RunDryRun(Database, verify);

            Groups = Plan.TargetDatabases.ToList();
            if (Plan.Entries.Any(e => e.Database == "(unresolved)"))
            {
                Groups.Add("(unresolved)");
            }

            Succeeded = !Plan.Entries.Any(e => e.Status == PlanEntryStatus.ValidationError
                                            || e.Status == PlanEntryStatus.Changed
                                            || e.VerifyStatus == PlanEntryStatus.VerifyFailed);

            return Page();
        }

        public List<PlanEntry> EntriesFor(string group)
        {
            return Plan.Entries
                .Where(e => e.Database.Equals(group, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }
}
