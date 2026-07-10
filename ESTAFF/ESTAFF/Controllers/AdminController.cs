using System.Web.Mvc;
using ESTAFF.Filters;

namespace ESTAFF.Controllers
{
    [AdminOnly]
    public class AdminController : Controller
    {
        public ActionResult Index()
        {
            ViewBag.PageTitle = "Dashboard";
            ViewBag.PageSubtitle = "Here's your overview.";
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

        public ActionResult AssignTasks()
        {
            ViewBag.PageTitle = "Assign Tasks";
            ViewBag.PageSubtitle = "Create and assign tasks to members.";
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