using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using TaskStatus = ESTAFF.Models.Data.TaskStatus;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using ESTAFF.Filters;
using ESTAFF.Models.Data;
using ESTAFF.Models.ViewModels;

namespace ESTAFF.Controllers
{
    [AdminOnly]
    public class AdminController : Controller
    {
        private ApplicationDbContext _db = new ApplicationDbContext();

        // ══════════════════════════════════════════
        // DASHBOARD
        // ══════════════════════════════════════════
        public ActionResult Index()
        {
            ViewBag.PageTitle    = "Dashboard";
            ViewBag.PageSubtitle = "Welcome back! Here's what's happening today.";

            var totalEmployees = _db.Users
                .Count(u => !u.IsAdmin && u.IsActive);

            var activeTasks = _db.Tasks
                .Count(t => t.Status == TaskStatus.Pending
                         || t.Status == TaskStatus.InProgress);

            var overdueTasks = _db.Tasks
                .Count(t => t.Status == TaskStatus.Overdue);

            var pendingReports = _db.Reports
                .Count(r => r.Status == ReportStatus.Submitted);

            var totalTasks = _db.Tasks.Count();

            var completedTasks = _db.Tasks
                .Where(t => t.Status == TaskStatus.Complete)
                .ToList();

            var onTimeCount = completedTasks
                .Count(t => t.CompletedDate.HasValue
                         && t.CompletedDate <= t.DueDate);

            var onTimeRate = completedTasks.Count > 0
                ? Math.Round((decimal)onTimeCount / completedTasks.Count * 100, 1)
                : 0;

            ViewBag.TotalEmployees  = totalEmployees;
            ViewBag.ActiveTasks     = activeTasks;
            ViewBag.OverdueTasks    = overdueTasks;
            ViewBag.PendingReports  = pendingReports;
            ViewBag.TotalTasks      = totalTasks;
            ViewBag.OnTimeRate      = onTimeRate;
            ViewBag.PendingCount    = _db.Tasks.Count(t => t.Status == TaskStatus.Pending);
            ViewBag.InProgressCount = _db.Tasks.Count(t => t.Status == TaskStatus.InProgress);
            ViewBag.CompleteCount   = _db.Tasks.Count(t => t.Status == TaskStatus.Complete);
            ViewBag.OverdueCount    = _db.Tasks.Count(t => t.Status == TaskStatus.Overdue);

            ViewBag.RecentTasks = _db.Tasks
                .OrderByDescending(t => t.CreatedDate)
                .Take(8)
                .ToList();

            return View();
        }

        // ══════════════════════════════════════════
        // EMPLOYEES — LIST
        // ══════════════════════════════════════════
        public ActionResult Employees()
        {
            ViewBag.PageTitle    = "My Employees";
            ViewBag.PageSubtitle = "Manage your team members.";

            var employees = _db.Users
                .Where(u => !u.IsAdmin)
                .OrderByDescending(u => u.CreatedDate)
                .ToList()
                .Select(u => new EmployeeCardViewModel
                {
                    UserId             = u.Id,
                    FullName           = u.FullName,
                    EmpNumber          = u.EmpNumber,
                    Email              = u.Email,
                    ProfilePicturePath = u.ProfilePicturePath,
                    IsActive           = u.IsActive,
                    HireDate           = u.HireDate ?? DateTime.Now,
                    TotalTasks         = _db.Tasks
                        .Count(t => t.AssignedToUserId == u.Id),
                    CompletedTasks     = _db.Tasks
                        .Count(t => t.AssignedToUserId == u.Id
                                 && t.Status == TaskStatus.Complete),
                    PendingTasks       = _db.Tasks
                        .Count(t => t.AssignedToUserId == u.Id
                                 && (t.Status == TaskStatus.Pending
                                 ||  t.Status == TaskStatus.InProgress)),
                    OverdueTasks       = _db.Tasks
                        .Count(t => t.AssignedToUserId == u.Id
                                 && t.Status == TaskStatus.Overdue),
                    OnTimeRate         = CalculateOnTimeRate(u.Id)
                })
                .ToList();

            return View(employees);
        }

        // ══════════════════════════════════════════
        // CREATE STAFF — GET
        // ══════════════════════════════════════════
        public ActionResult CreateStaff()
        {
            ViewBag.PageTitle    = "Add Employee";
            ViewBag.PageSubtitle = "Create a new staff account.";
            return View();
        }

        // ══════════════════════════════════════════
        // CREATE STAFF — POST
        // ══════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> CreateStaff(CreateStaffViewModel model)
        {
            ViewBag.PageTitle    = "Add Employee";
            ViewBag.PageSubtitle = "Create a new staff account.";

            if (!ModelState.IsValid)
                return View(model);

            var userManager = HttpContext.GetOwinContext()
                .GetUserManager<ApplicationUserManager>();

            // Check duplicate employee number
            if (_db.Users.Any(u => u.EmpNumber == model.EmpNumber))
            {
                ModelState.AddModelError("EmpNumber",
                    "This employee number is already in use.");
                return View(model);
            }

            // Check duplicate email
            if (await userManager.FindByEmailAsync(model.Email) != null)
            {
                ModelState.AddModelError("Email",
                    "An account with this email already exists.");
                return View(model);
            }

            var newUser = new ApplicationUser
            {
                UserName         = model.Email,
                Email            = model.Email,
                FullName         = model.FullName,
                EmpNumber        = model.EmpNumber,
                IsAdmin          = false,
                IsActive         = true,
                HireDate         = model.HireDate,
                CreatedDate      = DateTime.Now,
                LastModifiedDate = DateTime.Now
            };

            var result = await userManager.CreateAsync(newUser, model.Password);

            if (result.Succeeded)
            {
                TempData["SuccessMessage"] =
                    $"Account for {model.FullName} ({model.EmpNumber}) created successfully!";
                return RedirectToAction("Employees");
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError("", error);

            return View(model);
        }

        // ══════════════════════════════════════════
        // EDIT EMPLOYEE — GET
        // ══════════════════════════════════════════
        public ActionResult EditEmployee(string id)
        {
            ViewBag.PageTitle    = "Edit Employee";
            ViewBag.PageSubtitle = "Update employee information.";

            var user = _db.Users.Find(id);
            if (user == null || user.IsAdmin)
                return HttpNotFound();

            var completedTasks = _db.Tasks
                .Where(t => t.AssignedToUserId == id
                         && t.Status == TaskStatus.Complete)
                .ToList();

            var onTime = completedTasks
                .Count(t => t.CompletedDate.HasValue
                         && t.CompletedDate <= t.DueDate);

            var vm = new EditEmployeeViewModel
            {
                UserId             = user.Id,
                FullName           = user.FullName,
                EmpNumber          = user.EmpNumber,
                Email              = user.Email,
                HireDate           = user.HireDate ?? DateTime.Now,
                IsActive           = user.IsActive,
                ProfilePicturePath = user.ProfilePicturePath,
                TotalTasks         = _db.Tasks.Count(t => t.AssignedToUserId == id),
                CompletedTasks     = completedTasks.Count,
                PendingTasks       = _db.Tasks.Count(t => t.AssignedToUserId == id
                                         && (t.Status == TaskStatus.Pending
                                         ||  t.Status == TaskStatus.InProgress)),
                OverdueTasks       = _db.Tasks.Count(t => t.AssignedToUserId == id
                                         && t.Status == TaskStatus.Overdue),
                OnTimeRate         = completedTasks.Count > 0
                                         ? Math.Round((decimal)onTime / completedTasks.Count * 100, 1)
                                         : 0
            };

            return View(vm);
        }

        // ══════════════════════════════════════════
        // EDIT EMPLOYEE — POST
        // ══════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> EditEmployee(
            string id, EditEmployeeViewModel model)
        {
            ViewBag.PageTitle    = "Edit Employee";
            ViewBag.PageSubtitle = "Update employee information.";

            var user = _db.Users.Find(id);
            if (user == null || user.IsAdmin)
                return HttpNotFound();

            if (!ModelState.IsValid)
                return View(model);

            // Check duplicate employee number (exclude self)
            if (_db.Users.Any(u => u.EmpNumber == model.EmpNumber
                                && u.Id != id))
            {
                ModelState.AddModelError("EmpNumber",
                    "This employee number is already in use.");
                return View(model);
            }

            user.FullName           = model.FullName;
            user.EmpNumber          = model.EmpNumber;
            user.Email              = model.Email;
            user.UserName           = model.Email;
            user.HireDate           = model.HireDate;
            user.IsActive           = model.IsActive;
            user.LastModifiedDate   = DateTime.Now;

            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] =
                $"{model.FullName}'s information updated successfully!";
            return RedirectToAction("Employees");
        }

        // ══════════════════════════════════════════
        // TOGGLE ACTIVE STATUS
        // ══════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ToggleActive(string id)
        {
            var user = _db.Users.Find(id);
            if (user == null || user.IsAdmin)
                return HttpNotFound();

            user.IsActive          = !user.IsActive;
            user.LastModifiedDate  = DateTime.Now;
            await _db.SaveChangesAsync();

            var status = user.IsActive ? "activated" : "deactivated";
            TempData["SuccessMessage"] =
                $"{user.FullName}'s account has been {status}.";

            return RedirectToAction("Employees");
        }

        // ══════════════════════════════════════════
        // PLACEHOLDER ACTIONS
        // ══════════════════════════════════════════
        public ActionResult Tasks()
        {
            ViewBag.PageTitle    = "All Tasks";
            ViewBag.PageSubtitle = "View and manage all employee tasks.";
            return View();
        }

        public ActionResult AssignTask()
        {
            ViewBag.PageTitle    = "Assign Task";
            ViewBag.PageSubtitle = "Create and assign a task to an employee.";
            return View();
        }

        public ActionResult TaskHistory()
        {
            ViewBag.PageTitle    = "Task History";
            ViewBag.PageSubtitle = "Audit trail of all task changes.";
            return View();
        }

        public ActionResult PendingReports()
        {
            ViewBag.PageTitle    = "Pending Approvals";
            ViewBag.PageSubtitle = "Review and approve submitted reports.";
            return View();
        }

        public ActionResult ApprovedReports()
        {
            ViewBag.PageTitle    = "Approved Reports";
            ViewBag.PageSubtitle = "View all approved employee reports.";
            return View();
        }

        // ══════════════════════════════════════════
        // HELPER
        // ══════════════════════════════════════════
        private decimal CalculateOnTimeRate(string userId)
        {
            var completed = _db.Tasks
                .Where(t => t.AssignedToUserId == userId
                         && t.Status == TaskStatus.Complete)
                .ToList();

            if (completed.Count == 0) return 0;

            var onTime = completed
                .Count(t => t.CompletedDate.HasValue
                         && t.CompletedDate <= t.DueDate);

            return Math.Round((decimal)onTime / completed.Count * 100, 1);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _db.Dispose();
            base.Dispose(disposing);
        }
    }
}