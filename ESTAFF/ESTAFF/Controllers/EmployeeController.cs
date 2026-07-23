using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using ESTAFF.Filters;
using ESTAFF.Models.Data;
using ESTAFF.Models.ViewModels;
using ESTAFF.Services;

namespace ESTAFF.Controllers
{
    [EmployeeOnly]
    public class EmployeeController : Controller
    {
        private ApplicationDbContext _db = new ApplicationDbContext();

        // Helper method to get current user empnumber
        private ApplicationUser CurrentUser => 
            _db.Users.Find(User.Identity.GetUserId());

        // Helper method to set layout variables
        private void SetLayoutData()
        {
            var user = CurrentUser;
            ViewBag.FullName = user?.UserName ?? "";
        }

        // ===========
        // Dashboard
        // ===========
        public ActionResult Index()
        {
            SetLayoutData();
            ViewBag.PageTitle = "Dashboard";
            ViewBag.PageSubtitle = "Here's your task overview for today.";

            var userId = User.Identity.GetUserId();

            // Auto-flag overdue
            new TaskService(_db).UpdateOverdueTasks();

            var allTasks = _db.TaskItems
                .Where(t => t.AssignedToUserId == userId)
                .ToList();

            ViewBag.TotalTasks = allTasks.Count;
            ViewBag.PendingTasks = allTasks.Count(t =>
                t.Status == TaskStatus.Pending);
            ViewBag.InProgressTasks = allTasks.Count(t =>
                t.Status == TaskStatus.InProgress);
            ViewBag.CompletedTasks = allTasks.Count(t =>
                t.Status == TaskStatus.Complete);
            ViewBag.OverdueTasks = allTasks.Count(t =>
                t.Status == TaskStatus.Overdue);

            // On-time rate
            var completed = allTasks
                .Where(t => t.Status == TaskStatus.Complete)
                .ToList();
            var onTime = completed
                .Count(t => t.CompletedDate.HasValue
                    && t.CompletedDate <= t.DueDate);
            ViewBag.OnTimeRate = completed.Count > 0
                ? Math.Round((decimal)onTime / completed.Count * 100, 1)
                : 0;

            // Due Today
            var today = DateTime.Today;
            ViewBag.DueToday = allTasks
                .Where(t => t.DueDate.Date == today
                    && t.Status != TaskStatus.Complete)
                .OrderBy(t => t.DueDate)
                .Take(5)
                .ToList();

            // Recent Tasks
            ViewBag.RecentTasks = allTasks
                .OrderByDescending(t => t.CreatedDate)
                .Take(6)
                .ToList();

            return View();
        }

        // ===========
        // Task Management
        // ===========
        public ActionResult MyTasks(string status = "")
        {
            SetLayoutData();
            ViewBag.PageTitle = "My Tasks";
            ViewBag.PageSubtitle = "Manage all your tasks.";

            var userId = User.Identity.GetUserId();

            // Auto-flag overdue
            new TaskService(_db).UpdateOverdueTasks();

            var query = _db.TaskItems
                .Where(t => t.AssignedToUserId == userId)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status) &&
                Enum.TryParse<TaskStatus>(status, out var statusEnum))
                query = query.Where(t => t.Status == statusEnum);

            var tasks = query
                .OrderByDescending(t => t.CreatedDate)
                .ToList()
                .Select(t => new TaskListItemViewModel
                {
                    TaskId = t.TaskId,
                    Title = t.Title,
                    Description = t.Description,
                    COFId = t.COFId,
                    Status = t.Status,
                    Priority = t.Priority,
                    DueDate = t.DueDate,
                    CreatedDate = t.CreatedDate,
                    CompletedDate = t.CompletedDate,
                    CreatedByName = t.CreatedByUser?.UserName ?? "-"

                })
                .ToList();

            ViewBag.SelectedStatus = status;

            // Count for tabs
            var all = _db.TaskItems 
                .Where(t => t.AssignedToUserId == userId)
                .ToList();
            ViewBag.AllCount = all.Count;
            ViewBag.PendingCount = all.Count(t =>
                t.Status == TaskStatus.Pending);
            ViewBag.InProgCount = all.Count(t => 
                t.Status == TaskStatus.InProgress);
            ViewBag.CompleteCount = all.Count(t =>
                t.Status == TaskStatus.Complete);
            ViewBag.OverdueCount = all.Count(t =>
                t.Status == TaskStatus.Overdue);

            return View(tasks);
        }

        // Helper method to populate the optional COF dropdown
        // with certificates for the current user's assigned plants
        private void PopulateCOFList(int? selectedId = null)
        {
            var userId = User.Identity.GetUserId();

            var plantIds = _db.UserPlants
                .Where(up => up.UserId == userId)
                .Select(up => up.PlantId)
                .ToList();

            var cofs = new TaskService(_db).GetCOFsForPlants(plantIds);

            ViewBag.COFList = new SelectList(
                cofs.Select(c => new
                {
                    Value = c.Id,
                    Text = $"{c.RegistrationNo} — {c.MachineName} ({c.Status})"
                }),
                "Value", "Text", selectedId);
        }

        // ===========
        // Create Task - Get
        // ===========
        public ActionResult CreateTask()
        {
            SetLayoutData();
            ViewBag.PageTitle = "Create Task";
            ViewBag.PageSubtitle = "Add a new task to your list.";
            PopulateCOFList();
            return View(new CreateTaskViewModel());
        }
        // ===========
        // Create Task - Post
        // ===========
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateTask(CreateTaskViewModel model)
        {
            SetLayoutData();
            ViewBag.PageTitle = "Create Task";
            ViewBag.PageSubtitle = "Add a new task to your list.";

            if (!ModelState.IsValid)
            {
                PopulateCOFList(model.COFId);
                return View(model);
            }

            var userId = User.Identity.GetUserId();

            var task = new TaskItem
            {
                Title = model.Title,
                Description = model.Description,
                AssignedToUserId = userId,
                CreatedByUserId = userId,
                DueDate = model.DueDate,
                Priority = model.Priority,
                COFId = model.COFId,
                Status = TaskStatus.Pending,
                CreatedDate = DateTime.Now,
                LastModifiedDate = DateTime.UtcNow
            };

            _db.TaskItems.Add(task);
            _db.SaveChanges();

            new TaskService(_db).LogHistory(
                task.TaskId,
                "Created",
                null,
                $"Task '{task.Title}' created by employee.",
                userId);

            TempData["SuccessMessage"] = 
                $"Task '{model.Title}' created successfully.";
            return RedirectToAction("MyTasks");
            
        }

        // ===========
        // Edit Task - Get
        // ===========
        public ActionResult EditTask(int id)
        {
            SetLayoutData();
            ViewBag.PageTitle = "Edit Task";
            ViewBag.PageSubtitle = "Update your task details.";

            var userId = User.Identity.GetUserId();
            var task = _db.TaskItems.Find(id);

            // only allow to edit own tasks
            if (task == null || task.AssignedToUserId != userId)
               return HttpNotFound();

            var vm = new CreateTaskViewModel
            {
                Title = task.Title,
                Description = task.Description,
                DueDate = task.DueDate,
                Priority = task.Priority
            };

            ViewBag.TaskId = id;
            ViewBag.Status = task.Status;
            return View(vm);
        }

        // ===========
        // Edit Task - Post
        // ===========
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditTask(int id, CreateTaskViewModel model)
        {
            SetLayoutData();
            ViewBag.PageTitle = "Edit Task";
            ViewBag.PageSubtitle = "Update your task details.";
            ViewBag.TaskId = id;

            var userId = User.Identity.GetUserId();
            var task = _db.TaskItems.Find(id);

            if (task == null || task.AssignedToUserId != userId)
                return HttpNotFound();

            if (!ModelState.IsValid)
            {
                ViewBag.Status = task.Status;
                return View(model);
            }

            var changes = new System.Text.StringBuilder();

            if (task.Title != model.Title)
            {
                changes.Append($"Title: '{task.Title}' -> '{model.Title}'. ");
                task.Title = model.Title;
            }

            if (task.Description != model.Description)
            {
                changes.Append("description updated. ");
                task.Description = model.Description;
            }

            if (task.DueDate != model.DueDate)
            {
                changes.Append($"Due: '{task.DueDate:MMM dd}'" +
                    $" -> '{model.DueDate:MMM dd}'. ");
                task.DueDate = model.DueDate;
            }

            if (task.Priority != model.Priority)
            {
                changes.Append($"Priority: '{task.Priority}'" + 
                    $" -> '{model.Priority}'. ");
                task.Priority = model.Priority;
            }

            task.LastModifiedDate = DateTime.Now;
            _db.SaveChanges();

            if (changes.Length > 0)
            
                new TaskService(_db).LogHistory(
                    task.TaskId,
                    "Updated",
                    "Previous values",
                    changes.ToString(),
                    userId);
            
            TempData["SuccessMessage"] = "Task updated successfully!";
            return RedirectToAction("MyTasks");
            
        }
        
        // ===========
        // Daily Views
        // ===========
        public ActionResult DailyView(DateTime? date = null)
        {
            SetLayoutData();
            ViewBag.PageTitle    = "Daily View";
            ViewBag.PageSubtitle = "Your tasks for today.";

            var userId      = User.Identity.GetUserId();
            var targetDate  = date?.Date ?? DateTime.Today;

            new TaskService(_db).UpdateOverdueTasks();

            var tasks = _db.TaskItems
                .Where(t => t.AssignedToUserId == userId
                         && t.DueDate.Year  == targetDate.Year
                         && t.DueDate.Month == targetDate.Month
                         && t.DueDate.Day   == targetDate.Day)
                .OrderBy(t => t.Priority)
                .ToList();

            ViewBag.TargetDate = targetDate;
            ViewBag.PrevDate   = targetDate.AddDays(-1);
            ViewBag.NextDate   = targetDate.AddDays(1);
            ViewBag.IsToday    = targetDate == DateTime.Today;

            return View(tasks);
        }

        // ===========
        // Weekly Views
        // ===========

        public ActionResult WeeklyView(DateTime? weekStart = null)
        {
            SetLayoutData();
            ViewBag.PageTitle    = "Weekly View";
            ViewBag.PageSubtitle = "Your tasks for this week.";

            var userId = User.Identity.GetUserId();

            // Get Monday of the target week
            var today  = DateTime.Today;
            var start  = weekStart?.Date ?? today.AddDays(
                -(int)today.DayOfWeek + (int)DayOfWeek.Monday);

            if (start.DayOfWeek == DayOfWeek.Sunday)
                start = start.AddDays(-6);

            var end = start.AddDays(6);

            new TaskService(_db).UpdateOverdueTasks();

            var tasks = _db.TaskItems
                .Where(t => t.AssignedToUserId == userId
                         && t.DueDate >= start
                         && t.DueDate <= end)
                .OrderBy(t => t.DueDate)
                .ThenBy(t => t.Priority)
                .ToList();

            // Group by day
            var days = new List<DayTaskGroup>();
            for (int i = 0; i < 7; i++)
            {
                var day = start.AddDays(i);
                days.Add(new DayTaskGroup
                {
                    Date  = day,
                    Tasks = tasks.Where(t =>
                        t.DueDate.Date == day.Date).ToList()
                });

            }

            ViewBag.WeekStart = start;
            ViewBag.WeekEnd   = end;
            ViewBag.PrevWeek  = start.AddDays(-7);
            ViewBag.NextWeek  = start.AddDays(7);
            ViewBag.IsThisWeek = start <= today && today <= end;

            return View(days);
        }
        
        public ActionResult Profile()
        {
            SetLayoutData();
            ViewBag.PageTitle = "My Profile";
            ViewBag.PageSubtitle = "View and update your profile.";

            var user = CurrentUser;
            return View(user);
        }

        // ===========
        // PlaceHolder for future features
        // ===========
        public ActionResult MyReports()
        {
            SetLayoutData();
            ViewBag.PageTitle = "My Reports";
            ViewBag.PageSubtitle = "View all your submitted reports.";
            return View();
        }

        public ActionResult GenerateReport()
        {
            SetLayoutData();
            ViewBag.PageTitle = "Generate Report";
            ViewBag.PageSubtitle = "Create a weekly or monthly report.";
            return View();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _db.Dispose();
            base.Dispose(disposing);
        }
    }

    // Helper class 
    public class DayTaskGroup
    {
        public DateTime Date { get; set; }
        public List<TaskItem> Tasks { get; set; }
            = new List<TaskItem>();
    }
}
