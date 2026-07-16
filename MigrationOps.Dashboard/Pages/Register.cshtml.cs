using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MigrationOps.Dashboard.Data;

namespace MigrationOps.Dashboard.Pages
{
    // Only reachable while __DashboardUsers is empty (true first-run bootstrap).
    // Once an account exists this always redirects to /Login — it is not open signup.
    [AllowAnonymous]
    public class RegisterModel : PageModel
    {
        private readonly DashboardStoreService _store;

        public RegisterModel(DashboardStoreService store)
        {
            _store = store;
        }

        [BindProperty]
        public string Username { get; set; } = string.Empty;

        [BindProperty]
        public string Password { get; set; } = string.Empty;

        [BindProperty]
        public string ConfirmPassword { get; set; } = string.Empty;

        public string? Error { get; set; }

        public IActionResult OnGet()
        {
            if (_store.HasAnyUsers())
            {
                return Redirect("/Login");
            }

            return Page();
        }

        public IActionResult OnPost()
        {
            if (_store.HasAnyUsers())
            {
                return Redirect("/Login");
            }

            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
            {
                Error = "Username and password are required.";
                return Page();
            }

            if (Password.Length < 8)
            {
                Error = "Password must be at least 8 characters.";
                return Page();
            }

            if (Password != ConfirmPassword)
            {
                Error = "Passwords do not match.";
                return Page();
            }

            _store.CreateUser(Username, Password);

            return Redirect("/Login");
        }
    }
}
