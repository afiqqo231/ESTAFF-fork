using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin.Security;
using ESTAFF.Models.Data;
using ESTAFF.Models.ViewModels;

namespace ESTAFF.Controllers
{
    public class AccountController : Controller
    {
        private ApplicationUserManager _userManager;
        private ApplicationSignInManager _signInManager;
        private ApplicationRoleManager _roleManager;

        public ApplicationUserManager UserManager
        {
            get => _userManager ?? HttpContext.GetOwinContext()
                .GetUserManager<ApplicationUserManager>();
            private set => _userManager = value;
        }

        public ApplicationSignInManager SignInManager
        {
            get => _signInManager ?? HttpContext.GetOwinContext()
                .Get<ApplicationSignInManager>();
            private set => _signInManager = value;
        }

        public ApplicationRoleManager RoleManager
        {
            get => _roleManager ?? HttpContext.GetOwinContext()
                .Get<ApplicationRoleManager>();
            private set => _roleManager = value;
        }

        private IAuthenticationManager AuthenticationManager =>
            HttpContext.GetOwinContext().Authentication;

        // GET: Account/Login
        [AllowAnonymous]
        public ActionResult Login(string returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Login(LoginViewModel model, string returnUrl)
        {
            if (!ModelState.IsValid) 
                return View(model);

            var result = await SignInManager.PasswordSignInAsync(
                model.Email, model.Password, model.RememberMe, shouldLockout: true
            );

            switch (result)
            {
                case SignInStatus.Success:
                    var user = await UserManager.FindByEmailAsync(model.Email);
                    return RedirectToRoleDashboard(user.Role);

                case SignInStatus.LockedOut:
                    ModelState.AddModelError("", "Your account is locked. Try again in 5 minutes.");
                    return View(model);
                
                case SignInStatus.Failure:
                default:
                    ModelState.AddModelError("", "Invalid Email or Password.");
                    return View(model);
            }
        }

        // GET: Account/Register
        [AllowAnonymous]
        public ActionResult Register()
        {
            return View();
        }

        // POST: Account/Register
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid) 
                return View(model);

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName,
                Role = model.Role
            };

            var result = await UserManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                // Ensure role exists
                await EnsureRoleExists(model.Role);
                await UserManager.AddToRoleAsync(user.Id, model.Role);

                // sign in after registration
                await SignInManager.SignInAsync(user, isPersistent: false, rememberBrowser: false);
                
                return RedirectToRoleDashboard(model.Role);
            }

            AddErrors(result);
            return View(model);
        }

        // POST: Account/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Logout()
        {
            AuthenticationManager.SignOut(DefaultAuthenticationTypes.ApplicationCookie);
            return RedirectToAction("Login", "Account");
        }

        // Helper Methods - Redirect based on role
        private ActionResult RedirectToRoleDashboard(string role)
        {
            if (role == "Manager")
                return RedirectToAction("Index", "Manager");
            else 
                return RedirectToAction("Index", "Staff");
        }

        // Helper Methods - Ensure role exists in DB
        private async Task EnsureRoleExists(string roleName)
        {
           if(!await RoleManager.RoleExistsAsync(roleName))
            await RoleManager.CreateAsync(
                new Microsoft.AspNet.Identity.EntityFramework.IdentityRole (roleName)
            );  
        }

        // Helper Methods - Add errors to ModelState
        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError("", error);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _userManager?.Dispose();
                _signInManager?.Dispose();
                _roleManager?.Dispose();
            }
            base.Dispose(disposing);
        }

    }
}