using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web;
using TaskStatus = ESTAFF.Models.Data.TaskStatus;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using ESTAFF.Filters;
using ESTAFF.Models.Data;
using ESTAFF.Models.ViewModels;
using ESTAFF.Services;

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
            return View(new CreateStaffViewModel());
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
                return View("CreateStaff", model);

            var userManager = HttpContext.GetOwinContext()
                .GetUserManager<ApplicationUserManager>();

            // Check duplicate employee number
            if (_db.Users.Any(u => u.EmpNumber == model.EmpNumber))
            {
                ModelState.AddModelError("EmpNumber",
                    "This employee number is already in use.");
                return View("CreateStaff", model);
            }

            // Check duplicate email
            if (await userManager.FindByEmailAsync(model.Email) != null)
            {
                ModelState.AddModelError("Email",
                    "An account with this email already exists.");
                return View("CreateStaff", model);
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

            return View("CreateStaff", model);
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
                return View("EditEmployee", model);

            // Check duplicate employee number (exclude self)
            if (_db.Users.Any(u => u.EmpNumber == model.EmpNumber
                                && u.Id != id))
            {
                ModelState.AddModelError("EmpNumber",
                    "This employee number is already in use.");
                return View("EditEmployee", model);
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

        // ══════════════════════════════════════════
        // TASKS — LIST
        // ══════════════════════════════════════════
        public ActionResult Tasks(string status = "", string employeeId = "")
        {
            ViewBag.PageTitle    = "All Tasks";
            ViewBag.PageSubtitle = "View and manage all employee tasks.";

            // Auto flag overdue tasks
            var taskService = new TaskService(_db);
            taskService.UpdateOverdueTasks();

            var query = _db.Tasks.AsQueryable();

            // Filter by status
            if (!string.IsNullOrEmpty(status) &&
                Enum.TryParse<TaskStatus>(status, out var statusEnum))
                query = query.Where(t => t.Status == statusEnum);

            // Filter by employee
            if (!string.IsNullOrEmpty(employeeId))
                query = query.Where(t => t.AssignedToUserId == employeeId);

            var tasks = query
                .OrderByDescending(t => t.CreatedDate)
                .ToList()
                .Select(t => new TaskListItemViewModel
                 {
                    TaskId = t.TaskId,
                    Title = t.Title,
                    Description = t.Description,
                    Status = t.Status,
                    Priority = t.Priority,
                    DueDate = t.DueDate,
                    CreatedDate = t.CreatedDate,
                    CompletedDate = t.CompletedDate,
                    AssignedToUserId = t.AssignedToUserId,
                    AssignedToName = t.AssignedToUser?.FullName ?? "-",
                    AssignedToEmpNumber = t.AssignedToUser?.EmpNumber ?? "-",
                    CreatedByName = t.CreatedByUser?.FullName ?? "-"
                    
                 })
                 .ToList();

            // Employee dropdown for filtering
            ViewBag.Employees = _db.Users
                .Where(u => !u.IsAdmin && u.IsActive)
                .OrderBy(u => u.FullName)
                .ToList();

            ViewBag.SelectedStatus = status;
            ViewBag.SelectedEmployeeId = employeeId;

            return View(tasks);
        }


        // ══════════════════════════════════════════
        // ASSIGN TASK — GET
        // ══════════════════════════════════════════
        public ActionResult AssignTask()
        {
            ViewBag.PageTitle    = "Assign Task";
            ViewBag.PageSubtitle = "Create and assign a task to an employee.";

            var vm = new AssignTaskViewModel
            {
                Employees = GetEmployeeSelectList()
            };
            
            return View(vm);
        }

        // ══════════════════════════════════════════
        // ASSIGN TASK — POST
        // ══════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AssignTask(AssignTaskViewModel model)
        {
            ViewBag.PageTitle = "Assign Task";
            ViewBag.PageSubtitle = "Create and assign a task to an employee.";

            model.Employees = GetEmployeeSelectList();

            if (!ModelState.IsValid)
                return View(model);

            var adminId = System.Web.HttpContext.Current.User
                .Identity.GetUserId();

            var task = new TaskItem
            {
                Title = model.Title,
                Description = model.Description,
                AssignedToUserId = model.AssignedToUserId,
                CreatedByUserId = adminId,
                DueDate = model.DueDate,
                Priority = model.Priority,
                Status = TaskStatus.Pending,
                CreatedDate = DateTime.Now,
                LastModifiedDate = DateTime.Now
            };

            _db.Tasks.Add(task);
            _db.SaveChanges();

            // Log history
            var taskService = new TaskService(_db);
            taskService.LogHistory(
                task.TaskId,
                "Created",
                null,
                $"Task '{task.Title}' assigned",
                adminId);

            TempData["SuccessMessage"] =
                $"Task '{model.Title}' assigned successfully!";
            return RedirectToAction("Tasks");
        }

        // ══════════════════════════════════════════
        // EDIT TASK — GET
        // ══════════════════════════════════════════
        public ActionResult EditTask(int id)
        {
            ViewBag.PageTitle = "Edit Tasks";
            ViewBag.PageSubtitle = "Update task details.";

            var task = _db.Tasks.Find(id);
            if (task == null) return HttpNotFound();

            var vm = new EditTaskViewModel
            {
                TaskId = task.TaskId,
                Title = task.Title,
                Description = task.Description,
                AssignedToUserId = task.AssignedToUserId,
                DueDate = task.DueDate,
                Priority = task.Priority,
                Status = task.Status,
                Employees = GetEmployeeSelectList()
            };
            return View(vm);
        }

        // ══════════════════════════════════════════
        // EDIT TASK — POST
        // ══════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditTask(int id, EditTaskViewModel model)
        {
            ViewBag.PageTitle = "Edit Task";
            ViewBag.PageSubtitle = "Update task details.";

            model.Employees = GetEmployeeSelectList();

            if (!ModelState.IsValid)
                return View(model);

            var task = _db.Tasks.Find(id);
            if (task == null) return HttpNotFound();

            var adminId = System.Web.HttpContext.Current.User
                .Identity.GetUserId();
            var taskService = new TaskService(_db);
            var changes = new System.Text.StringBuilder();

            // Track changes
            if (task.Title != model.Title)
            {
                changes.Append($"Title: '{task.Title}' → '{model.Title}'. ");
                task.Title = model.Title;
            }

            if (task.AssignedToUserId != model.AssignedToUserId)
            {
                var oldEmp = _db.Users.Find(task.AssignedToUserId);
                var newEmp = _db.Users.Find(model.AssignedToUserId);
                changes.Append($"Assigned: '{oldEmp?.FullName}'" + 
                    $" → '{newEmp?.FullName}'. ");
                task.AssignedToUserId = model.AssignedToUserId;
            }

            if(task.DueDate != model.DueDate)
            {
                changes.Append($"Due Date: '{task.DueDate:MMM dd}'" + 
                    $" → '{model.DueDate:MMM dd}'. ");
                task.DueDate = model.DueDate;
            }

            if (task.Priority != model.Priority)
            {
                changes.Append($"Priority: '{task.Priority}'" +
                    $" → '{model.Priority}'. ");
                task.Priority = model.Priority;
            }

            if (task.Status != model.Status)
            {
                changes.Append($"Status: '{task.Status}'" +
                            $" → '{model.Status}'. ");
                task.Status = model.Status;

                if (model.Status == TaskStatus.Complete)
                    task.CompletedDate = DateTime.Now;
            }

            task.LastModifiedDate = DateTime.Now;
            _db.SaveChanges();

            if (changes.Length > 0)
                taskService.LogHistory(
                    task.TaskId,
                    "Updated",
                    "Previous values",
                    changes.ToString(),
                    adminId
                    );

            TempData["SuccessMessage"] = "Task updated successfully!";
            return RedirectToAction("Tasks");
                
        }

        // ══════════════════════════════════════════
        // DELETE TASK — POST
        // ══════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteTask(int id)
        {
            var task = _db.Tasks.Find(id);
            if (task == null) return HttpNotFound();

            var adminId = System.Web.HttpContext.Current.User
                .Identity.GetUserId();
            var taskService = new TaskService(_db);
            var taskTitle = task.Title;

            taskService.LogHistory(
                task.TaskId,
                "Deleted",
                $"Task '{task.Title}'",
                null,
                adminId
            );

            _db.Tasks.Remove(task);
            _db.SaveChanges();

            TempData["SuccessMessage"] = 
                $"Task '{taskTitle}' deleted successfully!";
            return RedirectToAction("Tasks");
        }

        // ══════════════════════════════════════════
        // TASK HISTORY
        // ══════════════════════════════════════════
        public ActionResult TaskHistory()
        {
            ViewBag.PageTitle    = "Task History";
            ViewBag.PageSubtitle = "Audit trail of all task changes.";

            var history = _db.TaskHistories
                .OrderByDescending(h => h.ChangedDate)
                .ToList()
                .Select(h => new TaskHistoryItemViewModel
                {
                    HistoryId = h.HistoryId,
                    TaskId = h.TaskId,
                    TaskTitle = h.Task?.Title ?? "-",
                    Action = h.Action,
                    OldValue = h.OldValue,
                    NewValue = h.NewValue,
                    ChangedByName = h.ChangedByUser?.FullName ?? "-",
                    ChangedDate = h.ChangedDate
                })
                .ToList();

            return View(history);
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

        private List<EmployeeSelectItem> GetEmployeeSelectList()
        {
            return _db.Users
                .Where(u => !u.IsAdmin && u.IsActive)
                .OrderBy(u => u.FullName)
                .Select(u => new EmployeeSelectItem
                {
                    UserId = u.Id,
                    FullName = u.FullName,
                    EmployeeNumber = u.EmpNumber
                })
                .ToList();
        }
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