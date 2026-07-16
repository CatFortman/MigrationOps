using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MigrationOps.Dashboard.Data;

namespace MigrationOps.Dashboard.Pages
{
    [AllowAnonymous]
    public class LoginModel : PageModel
    {
        private readonly DashboardStoreService _store;

        public LoginModel(DashboardStoreService store)
        {
            _store = store;
        }

        [BindProperty]
        public string Username { get; set; } = string.Empty;

        [BindProperty]
        public string Password { get; set; } = string.Empty;

        public string? Error { get; set; }

        public IActionResult OnGet()
        {
            if (!_store.HasAnyUsers())
            {
                return Redirect("/Register");
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!_store.ValidateCredentials(Username, Password))
            {
                Error = "Invalid username or password.";
                return Page();
            }

            var claims = new[] { new Claim(ClaimTypes.Name, Username) };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

            return Redirect("/");
        }

        public async Task<IActionResult> OnPostLogoutAsync()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Redirect("/Login");
        }
    }
}
