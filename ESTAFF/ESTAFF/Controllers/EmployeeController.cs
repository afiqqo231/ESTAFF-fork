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
                    SubTaskId = t.SubTaskId,
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


        private void PopulateTaskClassification(int? selectedId = null)
        {
            var classification = new TaskService(_db).GetTaskClassification();
            ViewBag.classificationList = new SelectList(
                classification.Select(c => new
                {
                    Value = c.TaskClassificationId,
                    Text = c.Name
                }), "Value", "Text", selectedId);

        }

        private void PopulateTaskList(int classificationId, int? selectedId)
        {
            var task = new TaskService(_db).GetTaskList(classificationId);
            ViewBag.taskList = new SelectList(
                task.Select(t => new
                {
                    Value = t.TaskListId,
                    Text = t.Name
                }), "Value", "Text", selectedId);

            if (selectedId == 24 || selectedId == 25)
            {
                var userId = User.Identity.GetUserId();
                var plantId = _db.UserPlants
                    .Where(up => up.UserId == userId)
                    .Select(up => up.PlantId)
                    .FirstOrDefault();
                if (selectedId == 24)
                {
                    var cofs = new TaskService(_db).GetCOFList(plantId);
                    ViewBag.SubTaskList = new SelectList(
                        cofs.Select(c => new
                        {
                            Value = c.Id,
                            Text = c.MachineName + " (" + c.RegistrationNo + ")"
                        }),
                        "Value", "Text", selectedId);
                }
                else if (selectedId == 25)
                {
                    var monitoring = new TaskService(_db).GetPlantMonitoringList(plantId);
                    ViewBag.SubTaskList = new SelectList(
                        monitoring.Select(m => new
                        {
                            Value = m.Id,
                            Text = m.Area + " (Status: " + (m.ProcStatus ?? "N/A") + ")"
                        }),
                        "Value", "Text", selectedId);
                }
                else
                {
                    ViewBag.SubTaskList = new SelectList(
                        Enumerable.Empty<SelectListItem>());
                }
            }

        }

        // ===========
        // Create Task - Get
        // ===========
        public ActionResult CreateTask()
        {
            SetLayoutData();
            ViewBag.PageTitle = "Create Task";
            ViewBag.PageSubtitle = "Add a new task to your list.";
            PopulateTaskClassification();
            ViewBag.COFList = new SelectList(Enumerable.Empty<SelectListItem>());
            return View(new CreateTaskViewModel());
        }
        
        // ===========
        // Populate TaskList based on selected TaskClassification
        //============
        [HttpGet]
        public JsonResult GetTaskByClassification(int classificationId)
        {
            if (classificationId == 4)
            {
                
            }
            var tasks = new TaskService(_db).GetTaskList(classificationId);
            var result = tasks.Select(t => new
            {
                value = t.TaskListId,
                text = t.Name
            });
            return Json(result, JsonRequestBehavior.AllowGet);
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
                PopulateTaskClassification(model.TaskClassificationId);
                PopulateTaskList(model.TaskClassificationId, model.TaskListId);
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
                SubTaskId = model.SubTaskId,
                // Without the task list, SubTaskId is ambiguous - nothing can
                // tell whether it points at a COF or a plant monitoring record.
                TaskListId = model.TaskListId > 0
                    ? model.TaskListId
                    : (int?)null,
                Status = TaskStatus.Pending,
                CreatedDate = DateTime.Now,
                LastModifiedDate = DateTime.Now,
                TaskClassificationId = model.TaskClassificationId
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
        
       // ============
       // Calendar - Unified View
       // ============
        public ActionResult Calendar(
            string view = "weekly",
            DateTime? date = null)
        {
            SetLayoutData();
            ViewBag.PageTitle = "Calendar";
            ViewBag.PageSubtitle = "Manage your tasks in a calendar view.";

            var userId = User.Identity.GetUserId();
            var targetDate = date?.Date ?? DateTime.Today;

            new TaskService(_db).UpdateOverdueTasks();

            // Calculate period based on view
            DateTime periodStart;
            DateTime periodEnd;

            switch (view.ToLower())
            {
                case "daily":
                    periodStart = targetDate;
                    periodEnd = targetDate;
                    break;

                case "monthly":
                    periodStart = new DateTime(
                        targetDate.Year, targetDate.Month, 1);
                    periodEnd = periodStart
                        .AddMonths(1).AddDays(-1);
                    break;

                default: // weekly
                    int diff = (int)targetDate.DayOfWeek 
                        - (int)DayOfWeek.Monday;
                    if (diff < 0) diff += 7;
                    periodStart = targetDate.AddDays(-diff);
                    periodEnd = periodStart.AddDays(6);
                    break;
            }

            var endOfDay = periodEnd.AddDays(1).AddTicks(-1);

            var tasks = _db.TaskItems
                .Where(t => t.AssignedToUserId == userId
                         && t.DueDate >= periodStart
                         && t.DueDate <= endOfDay)
                .OrderBy(t => t.DueDate)
                .ToList();

            // Build day groups
            var days = new List<DayTaskGroup>();
            for (var d = periodStart; d <= periodEnd;
                d = d.AddDays(1))
            {
                days.Add(new DayTaskGroup
                {
                    Date = d,
                    Tasks = tasks.Where(t => 
                        t.DueDate.Date == d.Date).ToList()
                });
            }

            // Navigaiton dates
            switch (view.ToLower())
            {
                case "daily":
                    ViewBag.PrevDate = targetDate.AddDays(-1);
                    ViewBag.NextDate = targetDate.AddDays(1);
                    break;
                case "monthly":
                    ViewBag.PrevDate = targetDate.AddMonths(-1);
                    ViewBag.NextDate = targetDate.AddMonths(1);
                    break;
                default: // weekly
                    ViewBag.PrevDate = targetDate.AddDays(-7);
                    ViewBag.NextDate = targetDate.AddDays(7);
                    break;
            }

            ViewBag.CurrentView = view.ToLower();
            ViewBag.TargetDate = targetDate;
            ViewBag.PeriodStart = periodStart;
            ViewBag.PeriodEnd = periodEnd;
            ViewBag.IsToday = targetDate == DateTime.Today ||
                (periodStart <= DateTime.Today && 
                DateTime.Today <= periodEnd);

            // All tasks for this employee (drag drop)
            ViewBag.TotalTaskCount = _db.TaskItems
                .Count(t => t.AssignedToUserId == userId);

            return View(days);
        }

        // ===========
        // Reschedule Task - POST (Drag & Drop)
        // ===========
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RescheduleTask(
            int taskId, string newDate)
        {
            var userId = User.Identity.GetUserId();
            var task = _db.TaskItems.Find(taskId);

            if (task == null || task.AssignedToUserId != userId)
                return Json(new
                {
                    success = false,
                    message = "Task not found."
                });

            if (!DateTime.TryParse(newDate, out var parsedDate))
                return Json(new
                {
                    success = false,
                    message = "Invalid date."
                });

            var oldDate = task.DueDate;
            task.DueDate = parsedDate;
            task.LastModifiedDate = DateTime.Now;

            // overdue and new date is future, reset to pending
            if (task.Status == TaskStatus.Overdue
                && parsedDate >= DateTime.Today)
            {
                task.Status = TaskStatus.Pending;
            }

            _db.SaveChanges();

            new TaskService(_db).LogHistory(
                task.TaskId, 
                "Updated",
                $"Due: {oldDate:MMM dd, yyyy}",
                $"Due: {parsedDate:MMM dd, yyyy} (rescheduled)",
                userId);
            
            return Json(new
            {
                success = true,
                message = $"Task resecheduled to " +
                    $"{parsedDate:MMM dd, yyyy}."
            });
            
        }

        // ===========
        // Update Status - Post
        // ===========
        // Posted by the status control on MyTasks, Calendar and DailyView.
        // Remark is optional and stored with the transition, so the task can
        // show why it last moved.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateStatus(int taskId, TaskStatus status,
            string remark = null, string returnUrl = null)
        {
            var userId = User.Identity.GetUserId();
            var task = _db.TaskItems.Find(taskId);

            // Employees may only move their own tasks.
            if (task == null || task.AssignedToUserId != userId)
                return HttpNotFound();

            var oldStatus = task.Status;

            if (oldStatus == status)
            {
                TempData["ErrorMessage"] =
                    $"'{task.Title}' is already {TaskDisplay.StatusLabel(status)}.";
                return RedirectToLocalOr(returnUrl, "MyTasks");
            }

            task.Status = status;
            task.CompletedDate = status == TaskStatus.Complete
                ? DateTime.Now
                : (DateTime?)null;
            task.LastModifiedDate = DateTime.Now;
            _db.SaveChanges();

            new TaskService(_db).LogStatusChange(
                task.TaskId, oldStatus, status, userId, remark);

            TempData["SuccessMessage"] =
                $"'{task.Title}' moved to {TaskDisplay.StatusLabel(status)}.";
            return RedirectToLocalOr(returnUrl, "MyTasks");
        }

        // ===========
        // Delete Task - Post
        // ===========
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteTask(int id)
        {
            var userId = User.Identity.GetUserId();
            var task = _db.TaskItems.Find(id);

            // Employees may only delete tasks they raised themselves - tasks
            // assigned by an admin stay on the board.
            if (task == null
                || task.AssignedToUserId != userId
                || task.CreatedByUserId != userId)
                return HttpNotFound();

            var title = task.Title;

            _db.TaskItems.Remove(task);
            _db.SaveChanges();

            TempData["SuccessMessage"] = $"Task '{title}' deleted.";
            return RedirectToAction("MyTasks");
        }

        // Keeps the user on the page they acted from, ignoring off-site URLs.
        private ActionResult RedirectToLocalOr(string returnUrl, string fallbackAction)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction(fallbackAction);
        }

        // ===========
        // Profile
        // ===========
        // The sidebar links here and Views/Employee/Profile.cshtml exists, so
        // the action has to as well. "new" because Controller.Profile is a
        // protected property on the base class.
        public new ActionResult Profile()
        {
            SetLayoutData();
            ViewBag.PageTitle = "My Profile";
            ViewBag.PageSubtitle = "View and update your profile.";

            var user = CurrentUser;
            if (user == null) return HttpNotFound();

            var userId = User.Identity.GetUserId();

            ViewBag.TotalTasks = _db.TaskItems
                .Count(t => t.AssignedToUserId == userId);
            ViewBag.CompletedTasks = _db.TaskItems
                .Count(t => t.AssignedToUserId == userId
                         && t.Status == TaskStatus.Complete);
            ViewBag.PendingTasks = _db.TaskItems
                .Count(t => t.AssignedToUserId == userId
                         && (t.Status == TaskStatus.Pending
                         || t.Status == TaskStatus.InProgress));
            ViewBag.OverdueTasks = _db.TaskItems
                .Count(t => t.AssignedToUserId == userId
                         && t.Status == TaskStatus.Overdue);

            var completed = _db.TaskItems
                .Where(t => t.AssignedToUserId == userId
                         && t.Status == TaskStatus.Complete)
                .ToList();

            var onTime = completed
                .Count(t => t.CompletedDate.HasValue
                    && t.CompletedDate <= t.DueDate);
            ViewBag.OnTimeRate = completed.Count > 0
                ? Math.Round((decimal)onTime / completed.Count * 100, 1)
                : 0;

            return View(user);
        }

        public ActionResult DailyView(DateTime? date = null)
        {
            return RedirectToAction("Calendar",
                new { view = "daily",
                    date = (date ?? DateTime.Today)
                        .ToString("yyyy-MM-dd") });
        }

        public ActionResult WeeklyView(DateTime? weekStart = null)
        {
            return RedirectToAction("Calendar",
                new { view = "weekly",
                    date = (weekStart ?? DateTime.Today)
                        .ToString("yyyy-MM-dd") });
        }
        

        // ===========
        // My Reports - LIST
        // ===========
        public ActionResult MyReports()
        {
            SetLayoutData();
            ViewBag.PageTitle = "My Reports";
            ViewBag.PageSubtitle = "View all your submitted reports.";

            var userId = User.Identity.GetUserId();

            var reports = _db.Reports
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.CreatedDate)
                .ToList()
                .Select(r => new ReportListItemViewModel
                {
                    ReportId = r.ReportId,
                    EmpName = r.User?.UserName ?? "-",
                    EmpNumber = r.User?.EmpID ?? "-",
                    ReportType = r.ReportType,    
                    PeriodStart = r.PeriodStart,
                    PeriodEnd = r.PeriodEnd,
                    Status = r.Status,
                    CreatedDate = r.CreatedDate,
                    SubmittedDate = r.SubmittedDate,
                    ApprovedDate = r.ApprovedDate,
                    RejectionReason = r.RejectionReason
                })
                .ToList();

            return View(reports);
        }

        // ===========
        // Generate Report - GET
        // ===========
        public ActionResult GenerateReport()
        {
            SetLayoutData();
            ViewBag.PageTitle = "Generate Report";
            ViewBag.PageSubtitle = "Create a weekly or monthly report.";

            // Default to current week
            var today = DateTime.Today;
            var weekStart = today.AddDays(
                -(int)today.DayOfWeek + (int)DayOfWeek.Monday);
            if (today.DayOfWeek == DayOfWeek.Sunday)
                weekStart = today.AddDays(-6);
            
            var vm = new GenerateReportViewModel
            {
                PeriodStart = weekStart,
                PeriodEnd = today
            };

            return View(vm);
        }

        // ===========
        // Generate Report - POST
        // ===========
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult PreviewReport(GenerateReportViewModel model)
        {
            SetLayoutData();
            ViewBag.PageTitle = "Preview Report";
            ViewBag.PageSubtitle = "Review before submitting.";

            if (!ModelState.IsValid)
                return View("GenerateReport", model);

            var userId = User.Identity.GetUserId();
            var endOfDay = model.PeriodEnd.AddDays(1).AddTicks(-1);

            var tasks = _db.TaskItems
                .Where(t => t.AssignedToUserId == userId
                         && t.DueDate >= model.PeriodStart
                         && t.DueDate <= endOfDay)
                .OrderBy(t => t.DueDate)
                .ToList();

            model.Tasks = tasks;
            return View("PreviewReport", model);
        }

        // ===========
        // Submit Report - POST
        // ===========
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SubmitReport(GenerateReportViewModel model)
        {
            SetLayoutData();
            var userId = User.Identity.GetUserId();

            // Check if a report already exists for this period
            var existingReport = _db.Reports.FirstOrDefault(r =>
                r.UserId == userId
                && r.PeriodStart == model.PeriodStart
                && r.PeriodEnd == model.PeriodEnd
                && r.Status != ReportStatus.Rejected);

            if (existingReport != null)
            {
                TempData["ErrorMessage"] = 
                    "A report for this period has already been submitted.";
                return RedirectToAction("MyReports");
            }

            var report = new Report
            {
                UserId = userId,
                ReportType = model.ReportType,
                PeriodStart = model.PeriodStart,
                PeriodEnd = model.PeriodEnd,
                Status = ReportStatus.Submitted,
                SubmittedDate = DateTime.Now,
                CreatedDate = DateTime.Now,
                LastModifiedDate = DateTime.Now
            };

            _db.Reports.Add(report);
            _db.SaveChanges();

            TempData["SuccessMessage"] = 
                "Report submitted successfully! " + 
                "Awaiting manager approval.";
            return RedirectToAction("MyReports");
        }

        // ===========
        // View Report - GET
        // ===========
        public ActionResult ViewReport(int id)
        {
            SetLayoutData();
            ViewBag.PageTitle = "Report Details";
            ViewBag.PageSubtitle = "View your report details.";

            var userId = User.Identity.GetUserId();
            var report = _db.Reports.Find(id);

            if (report == null || report.UserId != userId)
                return HttpNotFound();

            var endOfDay = report.PeriodEnd.AddDays(1).AddTicks(-1);

            var tasks = _db.TaskItems
                .Where(t => t.AssignedToUserId == userId
                         && t.DueDate >= report.PeriodStart
                         && t.DueDate <= endOfDay)
                .OrderBy(t => t.DueDate)
                .ToList();

            var completed = tasks.Count(t =>
                t.Status == TaskStatus.Complete);

            var vm = new ReportDetailViewModel
            {
                ReportId = report.ReportId,
                EmpName = report.User?.UserName ?? "-",
                EmpNumber = report.User?.EmpID ?? "-",
                EmpEmail = report.User?.Email ?? "-",
                ReportType = report.ReportType,
                PeriodStart = report.PeriodStart,
                PeriodEnd = report.PeriodEnd,
                Status = report.Status,
                CreatedDate = report.CreatedDate,
                SubmittedDate = report.SubmittedDate,
                ApprovedDate = report.ApprovedDate,
                RejectionReason = report.RejectionReason,
                Tasks = tasks,
                TotalTasks = tasks.Count,
                CompletedTasks = completed,
                PendingTasks = tasks.Count(t =>
                    t.Status == TaskStatus.Pending ||
                    t.Status == TaskStatus.InProgress),
                OverdueTasks = tasks.Count(t =>
                    t.Status == TaskStatus.Overdue),
                CompletionRate = tasks.Count > 0 
                    ? Math.Round(
                        (decimal)completed / tasks.Count * 100, 1)
                    : 0
            };

            return View(vm);
        }

        // ===========
        // Download Report Pdf
        // ===========
        public ActionResult DownloadReportPdf(int id)
        {
            var userId = User.Identity.GetUserId();
            var report = _db.Reports.Find(id);

            if (report == null || report.UserId != userId)
                return HttpNotFound();

            var endOfDay = report.PeriodEnd.AddDays(1).AddTicks(-1);
            var tasks = _db.TaskItems
                .Where(t => t.AssignedToUserId == userId
                         && t.DueDate >= report.PeriodStart
                         && t.DueDate <= endOfDay)
                .OrderBy(t => t.DueDate)
                .ToList();

            var completed = tasks.Count(t =>
                t.Status == TaskStatus.Complete);

            var vm = new ReportDetailViewModel
            {
                ReportId = report.ReportId,
                EmpName = report.User?.UserName ?? "-",
                EmpNumber = report.User?.EmpID ?? "-",
                EmpEmail = report.User?.Email ?? "-",
                ReportType = report.ReportType,
                PeriodStart = report.PeriodStart,
                PeriodEnd = report.PeriodEnd,
                Status = report.Status,
                CreatedDate = report.CreatedDate,
                SubmittedDate = report.SubmittedDate,
                ApprovedDate = report.ApprovedDate,
                RejectionReason = report.RejectionReason,
                Tasks = tasks,
                TotalTasks = tasks.Count,
                CompletedTasks = completed,
                PendingTasks = tasks.Count(t =>
                    t.Status == TaskStatus.Pending ||
                    t.Status == TaskStatus.InProgress),
                OverdueTasks = tasks.Count(t =>
                    t.Status == TaskStatus.Overdue),
                CompletionRate = tasks.Count > 0
                    ? Math.Round(
                        (decimal)completed / tasks.Count * 100, 1)
                    : 0
            };

            var pdfService = new ReportPdfService();
            var bytes = pdfService.GeneratePdf(vm);
            var fileName = 
                $"Report_{vm.EmpNumber}_" +
                $"{vm.PeriodStart:yyyMMdd}_" +
                $"{vm.PeriodEnd:yyyMMdd}.pdf";

            return File(bytes, "application/pdf", fileName);
        }

        // ===========
        // Resubmit Rejected Report - Post
        // ===========
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ResubmitReport(int id)
        {
            var userId = User.Identity.GetUserId();
            var report = _db.Reports.Find(id);

            if (report == null 
                || report.UserId != userId
                || report.Status != ReportStatus.Rejected)
                return HttpNotFound();

            report.Status = ReportStatus.Submitted;
            report.SubmittedDate = DateTime.Now;
            report.RejectionReason = null;
            report.LastModifiedDate = DateTime.Now;
            _db.SaveChanges();

            TempData["SuccessMessage"] = 
                "Report resubmitted successfully!";
            return RedirectToAction("MyReports");
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
