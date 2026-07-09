using System.Web.Mvc;
using ESTAFF.Filters;

namespace ESTAFF.Controllers
{
    [EmployeeOnly]
    public class EmployeeController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }
    }
}
