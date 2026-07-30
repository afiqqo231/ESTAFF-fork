using System;
using System.Collections.Generic;
using System.Data.Entity;
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
        private ClipDbContext _clip = new ClipDbContext();

        // Helper method to get current user empnumber
        private ApplicationUser CurrentUser =>
            _db.Users.Find(User.Identity.GetUserId());

        private ClipService Clip => new ClipService(_db, _clip);

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

            var allTasks = TaskQuery()
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
            ViewBag.DueToday = BuildTaskList(allTasks
                .Where(t => t.DueDate.Date == today
                    && t.Status != TaskStatus.Complete)
                .OrderBy(t => t.DueDate)
                .Take(5)
                .ToList());

            // Recent Tasks
            ViewBag.RecentTasks = BuildTaskList(allTasks
                .OrderByDescending(t => t.CreatedDate)
                .Take(6)
                .ToList());

            return View();
        }

        // ===========
        // Task Management
        // ===========
        public ActionResult MyTasks(string status = "", string classification = "")
        {
            SetLayoutData();
            ViewBag.PageTitle = "My Tasks";
            ViewBag.PageSubtitle = "Manage all your tasks.";

            var userId = User.Identity.GetUserId();

            // Auto-flag overdue
            new TaskService(_db).UpdateOverdueTasks();

            var query = TaskQuery()
                .Where(t => t.AssignedToUserId == userId);

            if (!string.IsNullOrEmpty(status) &&
                Enum.TryParse<TaskStatus>(status, out var statusEnum))
                query = query.Where(t => t.Status == statusEnum);

            if (!string.IsNullOrEmpty(classification) &&
                int.TryParse(classification, out var classificationId))
                query = query.Where(t =>
                    t.TaskClassificationId == classificationId);

            var tasks = BuildTaskList(query
                .OrderByDescending(t => t.CreatedDate)
                .ToList());

            ViewBag.SelectedStatus = status;
            ViewBag.SelectedClassification = classification;
            ViewBag.Classifications = GetClassificationOptions();

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

        // Tasks with the lookups the list view model needs already joined.
        private IQueryable<TaskItem> TaskQuery()
        {
            return _db.TaskItems
                .Include(t => t.TaskClassification)
                .Include(t => t.TaskList)
                .Include(t => t.CreatedByUser)
                .Include(t => t.AssignedToUser);
        }

        // Projects tasks into the list view model, resolving each task's linked
        // CLIP record and its newest status remark in batched queries.
        private List<TaskListItemViewModel> BuildTaskList(List<TaskItem> tasks)
        {
            var clipItems = Clip.GetItemsForTasks(tasks);
            var remarks = new TaskService(_db)
                .GetLatestStatusRemarks(tasks.Select(t => t.TaskId));

            return tasks.Select(t => new TaskListItemViewModel
            {
                TaskId               = t.TaskId,
                Title                = t.Title,
                Description          = t.Description,
                TaskClassificationId = t.TaskClassificationId,
                ClassificationName   = t.TaskClassification?.Name,
                TaskListId           = t.TaskListId,
                TaskListName         = t.TaskList?.Name,
                SubTaskId            = t.SubTaskId,
                Status               = t.Status,
                Priority             = t.Priority,
                DueDate              = t.DueDate,
                CreatedDate          = t.CreatedDate,
                CompletedDate        = t.CompletedDate,
                AssignedToUserId     = t.AssignedToUserId,
                AssignedToName       = t.AssignedToUser?.UserName ?? "-",
                AssignedToEmpID      = t.AssignedToUser?.EmpID ?? "-",
                CreatedByName        = t.CreatedByUser?.UserName ?? "-",
                ClipItem             = clipItems.ContainsKey(t.TaskId)
                                           ? clipItems[t.TaskId]
                                           : null,
                LatestStatusRemark   = remarks.ContainsKey(t.TaskId)
                                           ? remarks[t.TaskId]
                                           : null
            }).ToList();
        }

        private List<ClassificationOption> GetClassificationOptions()
        {
            return TaskDisplay.ToOptions(_db.TaskClassifications
                .OrderBy(c => c.TaskClassificationId)
                .ToList());
        }

        // Classifications, task types, and the CLIP records for the current
        // user's plants — everything the task form's dropdowns need.
        private TaskFormOptions GetFormOptions()
        {
            var clip = Clip;

            return new TaskFormOptions
            {
                Classifications = GetClassificationOptions(),
                TaskLists = TaskDisplay.ToOptions(_db.TaskLists
                    .OrderBy(l => l.Name)
                    .ToList()),
                ClipItems = clip.GetItemsForUser(User.Identity.GetUserId()),
                ClipClassificationId =
                    clip.GetClipClassification()?.TaskClassificationId
            };
        }

        // ===========
        // Create Task - Get
        // ===========
        public ActionResult CreateTask()
        {
            SetLayoutData();
            ViewBag.PageTitle = "Create Task";
            ViewBag.PageSubtitle = "Add a new task to your list.";

            return View(new CreateTaskViewModel
            {
                Options = GetFormOptions()
            });
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

            var userId = User.Identity.GetUserId();
            model.Options = GetFormOptions();

            var isClip = model.TaskClassificationId.HasValue
                && model.TaskClassificationId == model.Options.ClipClassificationId;

            if (isClip && string.IsNullOrWhiteSpace(model.ClipItemKey))
            {
                ModelState.AddModelError("ClipItemKey",
                    "Select the COF or plant monitoring record this task covers.");
            }

            if (!ModelState.IsValid)
                return View(model);

            var task = new TaskItem
            {
                Title = model.Title,
                Description = model.Description,
                AssignedToUserId = userId,
                CreatedByUserId = userId,
                DueDate = model.DueDate,
                Priority = model.Priority,
                TaskClassificationId = model.TaskClassificationId.Value,
                Status = TaskStatus.Pending,
                CreatedDate = DateTime.Now,
                LastModifiedDate = DateTime.Now
            };

            if (!ApplyClassificationLink(task, model.TaskClassificationId,
                    model.TaskListId, model.ClipItemKey, userId, isClip))
                return View(model);

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

        // Sets TaskListId/SubTaskId from the form. For CLIP the picker decides
        // both; for every other classification the task type is chosen directly
        // and there is no linked record. Returns false (with a model error) when
        // the CLIP item is not one the owner may use.
        private bool ApplyClassificationLink(TaskItem task,
            int? classificationId, int? taskListId, string clipItemKey,
            string ownerUserId, bool isClip)
        {
            if (isClip)
            {
                if (!Clip.ApplyKeyToTask(task, clipItemKey, ownerUserId))
                {
                    ModelState.AddModelError("ClipItemKey",
                        "That CLIP item is not available for the assigned plants.");
                    return false;
                }
                return true;
            }

            task.SubTaskId = null;
            task.TaskListId = null;

            // Only accept a task type that actually belongs to the chosen
            // classification, so a stale or hand-edited post cannot cross them.
            if (taskListId.HasValue && classificationId.HasValue)
            {
                var belongs = _db.TaskLists.Any(l =>
                    l.TaskListId == taskListId.Value
                    && l.TaskClassificationId == classificationId.Value);

                if (belongs) task.TaskListId = taskListId;
            }

            return true;
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
                Priority = task.Priority,
                TaskClassificationId = task.TaskClassificationId,
                TaskListId = task.TaskListId,
                ClipItemKey = Clip.BuildKeyForTask(task),
                Options = GetFormOptions()
            };

            ViewBag.TaskId = id;
            ViewBag.Status = task.Status;
            ViewBag.LatestStatusRemark =
                new TaskService(_db).GetLatestStatusRemark(task.TaskId);

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

            ViewBag.Status = task.Status;
            ViewBag.LatestStatusRemark =
                new TaskService(_db).GetLatestStatusRemark(task.TaskId);
            model.Options = GetFormOptions();

            var isClip = model.TaskClassificationId.HasValue
                && model.TaskClassificationId == model.Options.ClipClassificationId;

            if (isClip && string.IsNullOrWhiteSpace(model.ClipItemKey))
            {
                ModelState.AddModelError("ClipItemKey",
                    "Select the COF or plant monitoring record this task covers.");
            }

            if (!ModelState.IsValid)
                return View(model);

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

            var before = DescribeClassification(task);

            task.TaskClassificationId = model.TaskClassificationId.Value;

            if (!ApplyClassificationLink(task, model.TaskClassificationId,
                    model.TaskListId, model.ClipItemKey, userId, isClip))
                return View(model);

            var after = DescribeClassification(task);
            if (before != after)
                changes.Append($"Classification: '{before}' -> '{after}'. ");

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

        // "CLIP / Plant Monitoring #12" — a stable string for the audit trail.
        private string DescribeClassification(TaskItem task)
        {
            var classification = _db.TaskClassifications
                .FirstOrDefault(c =>
                    c.TaskClassificationId == task.TaskClassificationId);

            var parts = new List<string>
            {
                classification?.Name ?? task.TaskClassificationId.ToString()
            };

            if (task.TaskListId.HasValue)
            {
                var list = _db.TaskLists
                    .FirstOrDefault(l => l.TaskListId == task.TaskListId.Value);
                parts.Add(list?.Name ?? ("#" + task.TaskListId.Value));
            }

            if (task.SubTaskId.HasValue) parts.Add("#" + task.SubTaskId.Value);

            return string.Join(" / ", parts);
        }

        // ===========
        // Update Status - Post
        // ===========
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateStatus(int taskId, TaskStatus status,
            string remark, string returnUrl = null)
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

            // Employees may only delete tasks they raised themselves — tasks
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

            var tasks = BuildTaskList(TaskQuery()
                .Where(t => t.AssignedToUserId == userId
                         && t.DueDate.Year  == targetDate.Year
                         && t.DueDate.Month == targetDate.Month
                         && t.DueDate.Day   == targetDate.Day)
                .OrderBy(t => t.Priority)
                .ToList());

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

            var tasks = BuildTaskList(TaskQuery()
                .Where(t => t.AssignedToUserId == userId
                         && t.DueDate >= start
                         && t.DueDate <= end)
                .OrderBy(t => t.DueDate)
                .ThenBy(t => t.Priority)
                .ToList());

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
                ? Math.Round(
                    (decimal)onTime / completed.Count * 100, 1)
                : 0;

            return View(user);
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

            var tasks = TaskQuery()
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

            return View(BuildReportDetail(report, userId));
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

            var vm = BuildReportDetail(report, userId);

            var pdfService = new ReportPdfService();
            var bytes = pdfService.GeneratePdf(vm);
            var fileName =
                $"Report_{vm.EmpNumber}_" +
                $"{vm.PeriodStart:yyyyMMdd}_" +
                $"{vm.PeriodEnd:yyyyMMdd}.pdf";

            return File(bytes, "application/pdf", fileName);
        }

        private ReportDetailViewModel BuildReportDetail(Report report, string userId)
        {
            var endOfDay = report.PeriodEnd.AddDays(1).AddTicks(-1);

            var tasks = TaskQuery()
                .Where(t => t.AssignedToUserId == userId
                         && t.DueDate >= report.PeriodStart
                         && t.DueDate <= endOfDay)
                .OrderBy(t => t.DueDate)
                .ToList();

            var completed = tasks.Count(t => t.Status == TaskStatus.Complete);

            return new ReportDetailViewModel
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
            if (disposing)
            {
                _db.Dispose();
                _clip.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    // Helper class
    public class DayTaskGroup
    {
        public DateTime Date { get; set; }
        public List<TaskListItemViewModel> Tasks { get; set; }
            = new List<TaskListItemViewModel>();
    }
}
