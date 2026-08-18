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

        private ClipService Clip => new ClipService(_db, _clip);

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
        public ActionResult MyTasks(string status = "", string q = null,
            string sort = null)
        {
            SetLayoutData();
            ViewBag.PageTitle = "My Tasks";
            ViewBag.PageSubtitle = "Manage all your tasks.";

            var userId = User.Identity.GetUserId();

            // Auto-flag overdue
            new TaskService(_db).UpdateOverdueTasks();

            // Tab counts are taken from the unfiltered set, so they keep
            // reading as totals for the whole workload while a search narrows
            // only the cards below them.
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

            // The cards show the classification, task type, who raised the
            // task and its CLIP record, so the lookups are joined rather than
            // lazily loaded one row at a time.
            var query = _db.TaskItems
                .Include(t => t.TaskClassification)
                .Include(t => t.TaskList)
                .Include(t => t.CreatedByUser)
                .Where(t => t.AssignedToUserId == userId);

            if (!string.IsNullOrEmpty(status) &&
                Enum.TryParse<TaskStatus>(status, out var statusEnum))
                query = query.Where(t => t.Status == statusEnum);

            var term = (q ?? "").Trim();
            if (term.Length > 0)
            {
                query = query.Where(t => t.Title.Contains(term)
                    || (t.Description != null && t.Description.Contains(term))
                    || (t.TaskClassification != null
                        && t.TaskClassification.Name.Contains(term))
                    || (t.TaskList != null && t.TaskList.Name.Contains(term)));
            }

            var tasks = Sort(query.ToList(), sort);

            ViewBag.SelectedStatus = status;
            ViewBag.SearchTerm = term;
            ViewBag.SelectedSort = NormaliseSort(sort);
            ViewBag.TotalCount = all.Count;
            ViewBag.CurrentUserId = userId;

            return View(BuildMyTaskList(tasks, userId));
        }

        private static string NormaliseSort(string sort)
        {
            switch (sort)
            {
                case "created":
                case "priority":
                case "title":
                    return sort;
                default:
                    return "due";
            }
        }

        // Default order answers "what needs me next": open work by due date,
        // soonest first, with finished tasks pushed to the end rather than
        // interleaved. Sorting by creation date — the previous behaviour —
        // buried an overdue task from last month at the bottom of the page.
        private static List<TaskItem> Sort(List<TaskItem> tasks, string sort)
        {
            switch (NormaliseSort(sort))
            {
                case "created":
                    return tasks
                        .OrderByDescending(t => t.CreatedDate)
                        .ToList();

                case "priority":
                    return tasks
                        .OrderBy(t => t.Status == TaskStatus.Complete ? 1 : 0)
                        .ThenByDescending(t => t.Priority.HasValue
                            ? (int)t.Priority.Value
                            : 0)
                        .ThenBy(t => t.DueDate)
                        .ToList();

                case "title":
                    return tasks
                        .OrderBy(t => t.Title)
                        .ToList();

                default:
                    return tasks
                        .OrderBy(t => t.Status == TaskStatus.Complete ? 1 : 0)
                        .ThenBy(t => t.DueDate)
                        .ThenByDescending(t => t.Priority.HasValue
                            ? (int)t.Priority.Value
                            : 0)
                        .ToList();
            }
        }

        // Mirrors AdminController.BuildTaskList: the CLIP record and the status
        // action flow are resolved in batched queries rather than per card, so
        // the employee sees the same detail about their own task that an admin
        // sees when reviewing it.
        private List<TaskListItemViewModel> BuildMyTaskList(
            List<TaskItem> tasks, string userId)
        {
            var clipItems = Clip.GetItemsForTasks(tasks);
            var flows = new TaskService(_db)
                .GetStatusActionFlows(tasks.Select(t => t.TaskId));

            return tasks.Select(t =>
            {
                var flow = flows.ContainsKey(t.TaskId)
                    ? flows[t.TaskId]
                    : new List<StatusRemarkViewModel>();

                return new TaskListItemViewModel
                {
                    TaskId                 = t.TaskId,
                    Title                  = t.Title,
                    Description            = t.Description,
                    SubTaskId              = t.SubTaskId,
                    TaskClassificationId   = t.TaskClassificationId,
                    ClassificationName     = t.TaskClassification?.Name,
                    TaskListId             = t.TaskListId,
                    TaskListName           = t.TaskList?.Name,
                    Status                 = t.Status,
                    Priority               = t.Priority,
                    DueDate                = t.DueDate,
                    CreatedDate            = t.CreatedDate,
                    CompletedDate          = t.CompletedDate,
                    AssignedToUserId       = t.AssignedToUserId,
                    CreatedByUserId        = t.CreatedByUserId,
                    CreatedByName          = t.CreatedByUserId == userId
                                                 ? "You"
                                                 : t.CreatedByUser?.UserName ?? "-",
                    ClipItem               = clipItems.ContainsKey(t.TaskId)
                                                 ? clipItems[t.TaskId]
                                                 : null,
                    StatusActions          = flow,
                    LatestStatusRemark     = flow.LastOrDefault()
                };
            }).ToList();
        }


        // Classifications, task types, and the CLIP records for the signed-in
        // employee's own plants. The admin equivalent takes an employee id
        // because an admin picks the assignee; here it is always the caller.
        private TaskFormOptions GetFormOptions()
        {
            var clip = Clip;

            return new TaskFormOptions
            {
                Classifications = TaskDisplay.ToOptions(
                    _db.TaskClassifications
                        .OrderBy(c => c.TaskClassificationId)
                        .ToList()),
                TaskLists = TaskDisplay.ToOptions(_db.TaskLists
                    .OrderBy(l => l.Name)
                    .ToList()),
                // Every CLIP record, filtered by plant in the picker. Not just
                // the employee's own plants: that mapping is EHS_PORTAL's and
                // is incomplete, so it left the picker empty for anyone
                // missing a CLIP.UserPlants row. See ClipService.GetAllItems.
                ClipItems = clip.GetAllItems()
            };
        }

        // The one check the data annotations cannot express: a task type has to
        // belong to the chosen classification, so it is required here rather
        // than on the model. Attaching a CLIP record stays optional.
        private void ValidateClassification(CreateTaskViewModel model)
        {
            if (!model.TaskListId.HasValue)
            {
                ModelState.AddModelError("TaskListId",
                    "Select the task type this task covers.");
            }
        }

        // The period rules live in TaskPeriod because both forms answer to
        // them; the controller only reports what they return. Whether a period
        // is required depends on ScheduleType, which no annotation can see.
        private void ValidatePeriod(ITaskPeriodFields fields)
        {
            foreach (var error in TaskPeriod.Validate(fields))
                ModelState.AddModelError(error.Key, error.Value);
        }

        // Mirrors AdminController.ApplyClassificationLink: the rule itself lives
        // in ClipService, the controller only reports the rejection.
        private bool ApplyClassificationLink(TaskItem task,
            int? classificationId, int? taskListId, string clipItemKey)
        {
            var result = Clip.TryApplyClassificationLink(task,
                classificationId, taskListId, clipItemKey);

            if (result != ClipService.ClipAttachResult.Unavailable)
                return true;

            ModelState.AddModelError("ClipItemKey",
                "That CLIP item no longer exists in CLIP. Pick another.");
            return false;
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
                // Yourself by default - assigning to a plant colleague is the
                // exception, not the usual case.
                AssignedToUserId = User.Identity.GetUserId(),

                // Long term with no period is the ordinary task, so the
                // form opens on it and asks for nothing extra. Choosing
                // "Daily" is what brings the period into play.
                ScheduleType = TaskScheduleType.LongTerm,

                Options = GetFormOptions(),
                Employees = GetEmployeeSelectList()
            });
        }

        // ===========
        // Populate TaskList based on selected TaskClassification
        //============
        [HttpGet]
        public JsonResult GetTaskByClassification(int classificationId)
        {
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

            model.Options = GetFormOptions();
            model.Employees = GetEmployeeSelectList();

            var userId = User.Identity.GetUserId();

            // Blank means "mine". Anything else has to be somebody the picker
            // actually offered: sharing a plant is the authorisation here, so
            // it is re-checked against the database rather than trusted from
            // the form, which a caller can post anything into.
            var assigneeId = string.IsNullOrWhiteSpace(model.AssignedToUserId)
                ? userId
                : model.AssignedToUserId;

            if (assigneeId != userId &&
                model.Employees.All(e => e.UserId != assigneeId))
            {
                ModelState.AddModelError("AssignedToUserId",
                    "You can only assign tasks to employees at your own plant.");
            }

            ValidateClassification(model);
            ValidatePeriod(model);

            if (!ModelState.IsValid)
                return View(model);

            var task = new TaskItem
            {
                Title = model.Title,
                Description = model.Description,
                AssignedToUserId = assigneeId,
                CreatedByUserId = userId,
                DueDate = model.DueDate,
                Priority = model.Priority,
                Status = TaskStatus.Pending,
                CreatedDate = DateTime.Now,
                LastModifiedDate = DateTime.Now,
                TaskClassificationId = model.TaskClassificationId
            };

            // Schedule type and the period, cleared together if the task is
            // long term and none was given.
            TaskPeriod.ApplyTo(task, model);

            // Sets the task type and any attached CLIP record. Any task may
            // carry one, whoever it is assigned to.
            if (!ApplyClassificationLink(task, model.TaskClassificationId,
                    model.TaskListId, model.ClipItemKey))
                return View(model);

            _db.TaskItems.Add(task);
            _db.SaveChanges();

            var assigneeName = assigneeId == userId
                ? null
                : model.Employees.First(e => e.UserId == assigneeId).FullName;

            new TaskService(_db).LogHistory(
                task.TaskId,
                "Created",
                null,
                assigneeName == null
                    ? $"Task '{task.Title}' created by employee."
                    : $"Task '{task.Title}' created and assigned to {assigneeName}.",
                userId);

            TempData["SuccessMessage"] = assigneeName == null
                ? $"Task '{model.Title}' created successfully."
                : $"Task '{model.Title}' assigned to {assigneeName}.";
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

                // Shown exactly as stored. A task with no period keeps none:
                // the form only insists on one if it is switched to Daily, and
                // filling the hours in here would put a period on a long-term
                // task nobody asked to change.
                ScheduleType = task.ScheduleType,
                PeriodDate = task.PeriodDate,
                PeriodStart = task.PeriodStart,
                PeriodEnd = task.PeriodEnd,

                Priority = task.Priority,
                TaskClassificationId = task.TaskClassificationId,
                TaskListId = task.TaskListId,
                ClipItemKey = ClipService.BuildKeyForTask(task),
                Options = GetFormOptions()
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

            ViewBag.Status = task.Status;

            model.Options = GetFormOptions();

            ValidateClassification(model);
            ValidatePeriod(model);

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
                changes.Append("Concern/Issue updated. ");
                task.Description = model.Description;
            }

            if (task.DueDate != model.DueDate)
            {
                changes.Append($"Due: '{task.DueDate:MMM dd}'" +
                    $" -> '{model.DueDate:MMM dd}'. ");
                task.DueDate = model.DueDate;
            }

            // Schedule type and period read as one thing in the history:
            // "Daily, 25 Aug 08:00 - 17:00", so a change to any part of it is
            // one legible line rather than three.
            var scheduleBefore = TaskPeriod.Describe(task);
            TaskPeriod.ApplyTo(task, model);
            var scheduleAfter = TaskPeriod.Describe(task);

            if (scheduleBefore != scheduleAfter)
                changes.Append(
                    $"Schedule: '{scheduleBefore}' -> '{scheduleAfter}'. ");

            if (task.Priority != model.Priority)
            {
                changes.Append($"Priority: '{task.Priority}'" +
                    $" -> '{model.Priority}'. ");
                task.Priority = model.Priority;
            }

            var before = Clip.DescribeClassification(task);

            task.TaskClassificationId = model.TaskClassificationId;

            if (!ApplyClassificationLink(task, model.TaskClassificationId,
                    model.TaskListId, model.ClipItemKey))
                return View(model);

            var after = Clip.DescribeClassification(task);
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

        // ===========
        // Update Task Status - POST
        // ===========
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateStatus(
            int taskId, TaskStatus status,
            string actionTaken, string returnUrl = null)
        {
            var userId = User.Identity.GetUserId();
            var task = _db.TaskItems.Find(taskId);

            // only allow to update own tasks
            if (task == null || task.AssignedToUserId != userId)
                return HttpNotFound();

            // In Progress / Complete must say what was actually done
            if (RequiresActionTaken(status)
                && string.IsNullOrWhiteSpace(actionTaken))
            {
                TempData["ErrorMessage"] =
                    $"Please describe the action taken before moving " +
                    $"'{task.Title}' to {StatusLabel(status)}.";
                return RedirectBack(returnUrl);
            }

            if (task.Status == status)
                return RedirectBack(returnUrl);

            var oldStatus = task.Status;

            task.Status = status;
            task.CompletedDate = status == TaskStatus.Complete
                ? DateTime.Now
                : (DateTime?)null;
            task.LastModifiedDate = DateTime.Now;
            _db.SaveChanges();

            // The action-taken text belongs in Remark, not folded into the new
            // value: Remark is the field GetLatestStatusRemark reads back onto
            // the task. Old/new values stay raw enum names so the transition
            // parses back into a TaskStatus.
            new TaskService(_db).LogStatusChange(
                task.TaskId, oldStatus, status, userId, actionTaken);

            TempData["SuccessMessage"] =
                $"'{task.Title}' marked as {StatusLabel(status)}.";
            return RedirectBack(returnUrl);
        }

        private static bool RequiresActionTaken(TaskStatus status)
        {
            return status == TaskStatus.InProgress
                || status == TaskStatus.Complete;
        }

        private static string StatusLabel(TaskStatus status)
        {
            return status == TaskStatus.InProgress
                ? "In Progress"
                : status.ToString();
        }

        // Returns to the page the status was changed from
        // (keeps the tab filter / calendar period intact)
        private ActionResult RedirectBack(string returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

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
            var taskService = new TaskService(_db);

            // The same period query the submitted report is read back through,
            // so the preview cannot show a different set of tasks than the one
            // that gets filed.
            var tasks = taskService.GetTasksForReportPeriod(
                userId, model.PeriodStart, model.PeriodEnd);

            model.Tasks = tasks;

            // The preview shows the same breakdown as the submitted report, so
            // the employee can check the actions they recorded before sending.
            model.TaskDetails = taskService
                .BuildReportTaskDetails(tasks, Clip.GetItemsForTasks(tasks));

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

            var taskService = new TaskService(_db);

            var tasks = taskService.GetTasksForReportPeriod(
                userId, report.PeriodStart, report.PeriodEnd);

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

            // Same resolved detail the PDF is built from, so the page and the
            // downloaded copy describe each task identically.
            vm.TaskDetails = taskService
                .BuildReportTaskDetails(tasks, Clip.GetItemsForTasks(tasks));

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

            var taskService = new TaskService(_db);

            var tasks = taskService.GetTasksForReportPeriod(
                userId, report.PeriodStart, report.PeriodEnd);

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

            vm.TaskDetails = taskService
                .BuildReportTaskDetails(tasks, Clip.GetItemsForTasks(tasks));

            var pdfService = new ReportPdfService();
            var bytes = pdfService.GeneratePdf(vm);
            // Named after the statutory return it is, so a downloaded copy is
            // filed under the same name as the one the SHO keeps.
            var fileName =
                $"ESH_{vm.ReportTypeLabel}_Report_" +
                $"{vm.EmpNumber}_" +
                $"{vm.PeriodStart:yyyyMMdd}_" +
                $"{vm.PeriodEnd:yyyyMMdd}.pdf";

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
            if (disposing)
            {
                _db.Dispose();
                _clip.Dispose();
            }
            base.Dispose(disposing);
        }
        
        // Colleagues the signed-in employee may assign work to: every active,
        // non-admin user who shares at least one plant with them.
        //
        // There is no PlantId on ApplicationUser to filter by — AspNetUsers is
        // EHS_PORTAL's table and ESTAFF does not add columns to it. Who works
        // where is CLIP.UserPlants, a user-to-plant many-to-many, so "same
        // plant" means "shares a row in that table", not an equality test.
        //
        // The caller is always included, so creating a task for yourself still
        // works. Be aware CLIP.UserPlants is EHS_PORTAL's own record and is
        // incomplete — an employee with no rows there sees only themselves.
        private List<EmployeeSelectItem> GetEmployeeSelectList()
        {
            var userId = User.Identity.GetUserId();

            // Materialised rather than left as a subquery: both lists are tiny
            // and it keeps the final query a plain IN (...).
            var myPlantIds = _db.UserPlants
                .Where(up => up.UserId == userId)
                .Select(up => up.PlantId)
                .Distinct()
                .ToList();

            var plantMateIds = _db.UserPlants
                .Where(up => myPlantIds.Contains(up.PlantId))
                .Select(up => up.UserId)
                .Distinct()
                .ToList();

            if (!plantMateIds.Contains(userId))
                plantMateIds.Add(userId);

            return _db.Users
                .Where(u => !u.IsAdmin
                            && u.IsActive
                            && plantMateIds.Contains(u.Id))
                .OrderBy(u => u.UserName)
                .Select(u => new EmployeeSelectItem
                {
                    UserId = u.Id,
                    FullName = u.UserName,
                    EmpID = u.EmpID
                })
                .ToList();
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
