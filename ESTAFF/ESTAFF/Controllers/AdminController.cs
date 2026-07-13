using System;
using System.Linq;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using ESTAFF.Models.Data;
using ESTAFF.Filters;

namespace ESTAFF.Controllers
{
    [AdminOnly]
    public class AdminController : Controller
    {
        private ApplicationDbContext _db = new ApplicationDbContext();
        public ActionResult Index()
        {
            ViewBag.PageTitle = "Dashboard";
            ViewBag.PageSubtitle = "Here's your overview.";

            // --Stats
            var totalEmployees = _db.Users
                .Count(u => !u.IsAdmin && u.IsActive);

            var totalTasks = _db.Tasks.Count();

            var activeTasks = _db.Tasks
                .Count(t => t.Status == TaskStatus.Pending
                        || t.Status == TaskStatus.InProgress);

            var overdueTasks = _db.Tasks
                .Count(t => t.Status == TaskStatus.Overdue);

            var pendingReports = _db.Reports
                .Count(r => r.Status == ReportStatus.Submitted);

            // --On-time completion rate
            var completedTasks = _db.Tasks
                .Where(t => t.Status == TaskStatus.Complete)
                .ToList();

            var onTimeCount = completedTasks
                .Count(t => t.CompletedDate.HasValue
                    && t.CompletedDate <= t.DueDate);

            var onTimeRate = completedTasks.Count > 0
                ? Math.Round((decimal)onTimeCount / completedTasks.Count * 100, 1)
                : 0;
            
            ViewBag.TotalEmployees = totalEmployees;
            ViewBag.TotalTasks = totalTasks;
            ViewBag.ActiveTasks = activeTasks;
            ViewBag.OverdueTasks = overdueTasks;
            ViewBag.PendingReports = pendingReports;
            ViewBag.OnTimeRate = onTimeRate;

            // --Recent Tasks
            var recentTasks = _db.Tasks
                .OrderByDescending(t => t.CreatedDate)
                .Take(5)
                .ToList();

            ViewBag.RecentTasks = recentTasks;

            // --Tasks breakdown
            ViewBag.PendingCount = _db.Tasks.Count(t => t.Status == TaskStatus.Pending);
            ViewBag.InProgressCount = _db.Tasks.Count(t => t.Status == TaskStatus.InProgress);
            ViewBag.CompleteCount = _db.Tasks.Count(t => t.Status == TaskStatus.Complete);
            ViewBag.OverdueCount = _db.Tasks.Count(t => t.Status == TaskStatus.Overdue);

            return View();
        }

        public ActionResult Employees()
        {
            ViewBag.PageTitle = "My Employees";
            ViewBag.PageSubtitle = "Manage your team members.";
            return View();
        }

        public ActionResult CreateStaff()
        {
            ViewBag.PageTitle = "Create Staff";
            ViewBag.PageSubtitle = "Create a new member account.";
            return View();
        }

        public ActionResult Tasks()
        {
            ViewBag.PageTitle = "All Tasks";
            ViewBag.PageSubtitle = "View and manage all member tasks.";
            return View();
        }

        public ActionResult AssignTask()
        {
            ViewBag.PageTitle = "Assign Task";
            ViewBag.PageSubtitle = "Create and assign a task to a member.";
            return View();
        }

        public ActionResult TaskHistory()
        {
            ViewBag.PageTitle = "Task History";
            ViewBag.PageSubtitle = "Audit trail of all task changes.";
            return View();
        }

        public ActionResult PendingReports()
        {
            ViewBag.PageTitle = "Pending Approvals";
            ViewBag.PageSubtitle = "Review and approve submitted reports.";
            return View();
        }

        public ActionResult ApprovedReports()
        {
            ViewBag.PageTitle = "Approved Reports";
            ViewBag.PageSubtitle = "View all approved reports.";
            return View();
        }
    }
}