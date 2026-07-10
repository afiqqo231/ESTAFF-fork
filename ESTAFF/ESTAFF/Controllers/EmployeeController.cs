using System.Web.Mvc;
using ESTAFF.Filters;

namespace ESTAFF.Controllers
{
    [EmployeeOnly]
    public class EmployeeController : Controller
    {
        public ActionResult Index()
        {
            ViewBag.PageTitle = "Dashboard";
            ViewBag.PageSubtitle = "Here's your task overview for today.";
            return View();
        }

        public ActionResult MyTasks()
        {
            ViewBag.PageTitle = "My Tasks";
            ViewBag.PageSubtitle = "Manage all your tasks.";
            return View();
        }

        public ActionResult CreateTask()
        {
            ViewBag.PageTitle = "Create Task";
            ViewBag.PageSubtitle = "Add a new task to your list.";
            return View();
        }

        public ActionResult DailyView()
        {
            ViewBag.PageTitle = "Daily View";
            ViewBag.PageSubtitle = "Your tasks for today.";
            return View();
        }

        public ActionResult WeeklyView()
        {
            ViewBag.PageTitle = "Weekly View";
            ViewBag.PageSubtitle = "Your tasks for this week.";
            return View();
        }

        public ActionResult MyReports()
        {
            ViewBag.PageTitle = "My Reports";
            ViewBag.PageSubtitle = "View all your submitted reports.";
            return View();
        }

        public ActionResult GenerateReport()
        {
            ViewBag.PageTitle = "Generate Report";
            ViewBag.PageSubtitle = "Create a new weekly or monthly report.";
            return View();
        }
        
        public ActionResult Profile()
        {
            ViewBag.PageTitle = "My Profile";
            ViewBag.PageSubtitle = "View and update your profile.";
            return View();
        }
    }
}
