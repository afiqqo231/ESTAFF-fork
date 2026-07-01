using System;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using ESTAFF.Models.Data;
using ESTAFF.Models;
using ESTAFF.Models.ViewModels;
using System.Threading.Tasks;

namespace ESTAFF.Controllers
{
    [Authorize(Roles = "Manager")]
    public class ManagerController : Controller
    {
        private ApplicationDbContext _db = new ApplicationDbContext();

        private void SetLayoutData()
        {
            var userId = User.Identity.GetUserId();
            var user = _db.Users.Find(userId);
            ViewBag.FullName = user?.FullName ?? User.Identity.Name;
            ViewBag.PendingReportsCount = _db.Reports
                .Count(r => r.Status == ReportStatus.Submitted);
        }

        public ActionResult Index()
        {
            SetLayoutData();
            ViewBag.PageTitle = "Manager Dashboard";
            ViewBag.PageSubtitle = "Welcome to the Manager Dashboard. Here you can manage staff and view reports.";
            return View();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _db.Dispose();
            base.Dispose(disposing);
        }

        public ActionResult CreateStaff()
        {
            SetLayoutData();
            ViewBag.PageTitle = "Create Staff Account";
            ViewBag.PageSubtitle = "Add a new EHS team member to the system.";
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> CreateStaff(CreateStaffViewModel model)
        {
            SetLayoutData();
            ViewBag.PageTitle = "Create Staff Account";
            ViewBag.PageSubtitle = "Add a new EHS team member to the system.";

            if (!ModelState.IsValid)
                return View(model);

            var userManager = HttpContext.GetOwinContext()
                .GetUserManager<ApplicationUserManager>();
            var roleManager = HttpContext.GetOwinContext()
                .Get<ApplicationRoleManager>();

            // Check if email already exists
            var existingUser = await userManager.FindByEmailAsync(model.Email);
            if (existingUser != null)
            {
                ModelState.AddModelError("Email", "An account with this email already exists.");
                return View(model);
            }

            // Create the use acc
            var newUser = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName,
                Role = model.Role,
                IsActive = true,
                CreatedDate = DateTime.Now
            };

            var result = await userManager.CreateAsync(newUser, model.Password);

            if (result.Succeeded)
            {
                // ensure the role exists
                if (!await roleManager.RoleExistsAsync(model.Role))
                    await roleManager.CreateAsync(
                        new Microsoft.AspNet.Identity.EntityFramework.IdentityRole(model.Role));
                
                await userManager.AddToRoleAsync(newUser.Id, model.Role);

                // create record for employee role
                if (model.Role == "Staff")
                {
                    var managerId = User.Identity.GetUserId();
                    var staff = new Staff
                    {
                        UserId = newUser.Id,
                        ManagerId = managerId,
                        Department = model.Department,
                        HireDate = model.HireDate,
                        CreatedDate = DateTime.Now
                    };
                    _db.Staffs.Add(staff);
                    await _db.SaveChangesAsync();
                }

                TempData["SuccessMessage"] = $"Account for {model.FullName} created successfully.";
                return RedirectToAction("Staff");
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError("", error);

            return View(model);
        }
    }
}