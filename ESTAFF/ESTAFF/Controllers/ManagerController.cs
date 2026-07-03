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
            
            var managerId = User.Identity.GetUserId();

            // dashboard stats
            var staffs = _db.Staffs
                .Where(e => e.ManagerId == managerId)
                .ToList();

            var staffIds = staffs.Select(e => e.UserId).ToList();

            // Total employees 
            ViewBag.TotalStaff = staffs.Count;

            // Total active task
            ViewBag.TotalActiveReports = _db.Tasks
                .Count(t => staffIds.Contains(t.AssignedToUserId)
                    && t.Status != Models.Data.TaskStatus.Complete
                    && t.Status != Models.Data.TaskStatus.Overdue);

            // Overdue tasks
            ViewBag.OverdueReports = _db.Tasks
                .Count(t => staffIds.Contains(t.AssignedToUserId)
                    && t.Status == Models.Data.TaskStatus.Overdue);

            
            // Recent tasks
            var recentTasks = _db.Tasks
                .Where(t => staffIds.Contains(t.AssignedToUserId))
                .OrderByDescending(t => t.CreatedDate)
                .Take(8)
                .ToList();

            ViewBag.RecentTasks = recentTasks;

            // Recent Staff
            var recentStaff = staffs
                .Where(e => e.ManagerId == managerId)
                .OrderByDescending(e => e.CreatedDate)
                .Take(5)
                .ToList();

            ViewBag.RecentStaff = recentStaff;

            // On-time completion rate
            var onTimeCompletedTasks = _db.Tasks
                .Count(t => staffIds.Contains(t.AssignedToUserId)
                    && t.Status == Models.Data.TaskStatus.Complete
                    && t.CompletedDate <= t.DueDate);

            var completedTasks = _db.Tasks
                .Where(t => staffIds.Contains(t.AssignedToUserId)
                    && t.Status == Models.Data.TaskStatus.Complete
                    && t.CompletedDate <= t.DueDate)
                .Count();

            ViewBag.OnTimeCompletionRate = completedTasks > 0
                ? Math.Round((decimal)onTimeCompletedTasks / completedTasks * 100, 1)
                : 0;
            return View();
        }

        public ActionResult Staff()
        {
            SetLayoutData();
            ViewBag.PageTitle = "My EHS Team";
            ViewBag.PageSubtitle = "Manage your team members.";
            return View();
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

        public ActionResult Tasks()
        {
            SetLayoutData();
            ViewBag.PageTitle = "All Tasks";
            ViewBag.PageSubtitle = "View and manage all EHS members tasks.";
            return View();
        }

        public ActionResult AssignTask()
        {
            SetLayoutData();
            ViewBag.PageTitle = "Assign Task";
            ViewBag.PageSubtitle = "Create a new task and assign to a team member.";
            return View();
        }

        public ActionResult TaskHistory()
        {
            SetLayoutData();
            ViewBag.PageTitle = "Task History";
            ViewBag.PageSubtitle = "View audit trail of all task changes";
            return View();
        }

        public ActionResult PendingReports()
        {
            SetLayoutData();
            ViewBag.PageTitle = "Pending Approvals";
            ViewBag.PageSubtitle = "View all approved EHS reports.";
            return View();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _db.Dispose();
            base.Dispose(disposing);
        }

    }
}