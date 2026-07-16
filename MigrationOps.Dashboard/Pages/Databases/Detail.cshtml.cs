using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MigrationOps.Dashboard.Data;

namespace MigrationOps.Dashboard.Pages.Databases
{
    public class DetailModel : PageModel
    {
        private readonly MigrationDataService _dataService;

        public DetailModel(MigrationDataService dataService)
        {
            _dataService = dataService;
        }

        public DatabaseOverview Overview { get; private set; } = new();

        public IActionResult OnGet(string name)
        {
            var actualName = _dataService.GetDatabaseNames()
                .FirstOrDefault(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));

            if (actualName == null)
            {
                return NotFound();
            }

            Overview = _dataService.GetDatabaseOverview(actualName);
            return Page();
        }
    }
}
