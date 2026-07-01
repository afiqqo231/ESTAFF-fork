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
    public class AuthController : Controller
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

        // GET: Auth/Login
        [AllowAnonymous]
        public ActionResult Login(string returnUrl)
        {
            if (User.Identity.IsAuthenticated)
            {
                var userId = User.Identity.GetUserId();
                var user = UserManager.FindById(userId);
                return RedirectToRoleDashboard(user?.Role);
            }

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

        // POST: Auth/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Logout()
        {
            AuthenticationManager.SignOut(DefaultAuthenticationTypes.ApplicationCookie);
            return RedirectToAction("Login", "Auth");
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