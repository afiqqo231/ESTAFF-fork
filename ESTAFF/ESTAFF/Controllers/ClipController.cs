using System.Net;
using System.Web.Mvc;
using ESTAFF.Models.Data;
using ESTAFF.Models.ViewModels;
using ESTAFF.Services;
using Microsoft.AspNet.Identity;

namespace ESTAFF.Controllers
{
    [Authorize]
    public class ClipController : Controller
    {
        private ApplicationDbContext _db = new ApplicationDbContext();
        
        // GET /CLIP/Progress?key=PM:3
        [HttpGet]
        public ActionResult Progress(string key)
        {
            ClipItemKind kind;
            int id;
            if (!ClipService.TryParseKey(key, out kind, out id))
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            using (var clip = new ClipService(_db))
            {
                var item = kind == ClipItemKind.COF
                    ? clip.GetCofItem(id)
                    : clip.GetMonitoringItem(id);

                if (item == null) return HttpNotFound();
                
                // Same rule ApplyKeyToTask enforces on write: a hand-crafted
                // link must not reach a plant the user isnt assigned to.
                var user = _db.Users.Find(User.Identity.GetUserId());
                if (user == null) return new HttpStatusCodeResult(HttpStatusCode.Forbidden);

                if (!user.IsAdmin)
                {
                    var allowed = clip.GetPlantIdsForUser(user.Id);
                    if (!allowed.Contains(item.PlantId))
                        return new HttpStatusCodeResult(HttpStatusCode.Forbidden);
                }

                var url = ClipService.BuildProgressUrl(kind, id);
                if (url == null) return new HttpStatusCodeResult(HttpStatusCode.ServiceUnavailable);

                return Redirect(url);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _db == null)
            {
                _db.Dispose();
                _db = null;
            }
            base.Dispose(disposing);
        }
    }
}