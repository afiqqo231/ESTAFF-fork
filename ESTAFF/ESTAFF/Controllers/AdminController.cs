using System.Web.Mvc;
using ESTAFF.Filters;

namespace ESTAFF.Controllers
{
    [AdminOnly]
    public class AdminController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }
    }
}