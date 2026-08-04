using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using ESTAFF.Filters;
using ESTAFF.Models.Data;
using ESTAFF.Models.ViewModels;
using ESTAFF.Services;

namespace ESTAFF.Controllers
{
    // Maintains the task taxonomy: the classifications offered on the assign
    // and edit task forms, and the task types (TaskList rows) under each one.
    //
    // Both are lookup tables the task forms read directly, so a row saved here
    // shows up in the dropdowns on the next page load.
    //
    // Two rules run through every action:
    //   - a row that something still points at is never deleted, and the
    //     screen says what is pointing at it;
    //   - the CLIP classification is resolved by name in ClipService, so it is
    //     protected from deletion and flagged before it is renamed.
    [AdminOnly]
    public class ClassificationsController : Controller
    {
        private readonly ApplicationDbContext _db = new ApplicationDbContext();

        // ══════════════════════════════════════════
        // LIST
        // ══════════════════════════════════════════

        public ActionResult Index()
        {
            ViewBag.PageTitle    = "Task Classifications";
            ViewBag.PageSubtitle =
                "The classifications and task types offered when assigning a task.";

            var classifications = _db.TaskClassifications
                .OrderBy(c => c.TaskClassificationId)
                .ToList();

            // Counted in two queries rather than one per row.
            var typeCounts = _db.TaskLists
                .GroupBy(l => l.TaskClassificationId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionary(x => x.Key, x => x.Count);

            var taskCounts = _db.TaskItems
                .GroupBy(t => t.TaskClassificationId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionary(x => x.Key, x => x.Count);

            var model = classifications
                .Select(c => new ClassificationListItemViewModel
                {
                    TaskClassificationId = c.TaskClassificationId,
                    Name          = c.Name,
                    TaskTypeCount = Lookup(typeCounts, c.TaskClassificationId),
                    TaskCount     = Lookup(taskCounts, c.TaskClassificationId),
                    IsClip        = IsClipName(c.Name)
                })
                .ToList();

            return View(model);
        }

        // ══════════════════════════════════════════
        // CREATE CLASSIFICATION
        // ══════════════════════════════════════════

        public ActionResult Create()
        {
            ViewBag.PageTitle    = "New Classification";
            ViewBag.PageSubtitle =
                "Add a work stream to the assign task form.";

            return View(new ClassificationFormViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(ClassificationFormViewModel model)
        {
            model.Name = (model.Name ?? "").Trim();

            if (NameIsTaken(model.Name))
                ModelState.AddModelError("Name",
                    "A classification with this name already exists.");

            if (!ModelState.IsValid)
            {
                ViewBag.PageTitle = "New Classification";
                return View(model);
            }

            var classification = new TaskClassification { Name = model.Name };
            _db.TaskClassifications.Add(classification);
            _db.SaveChanges();

            TempData["SuccessMessage"] =
                "Classification '" + classification.Name + "' created. "
                + "Add its task types below.";

            // Straight to edit: a classification with no task types cannot be
            // used yet, because the task form requires one.
            return RedirectToAction("Edit",
                new { id = classification.TaskClassificationId });
        }

        // ══════════════════════════════════════════
        // EDIT CLASSIFICATION
        // ══════════════════════════════════════════

        public ActionResult Edit(int id)
        {
            var model = BuildForm(id);
            if (model == null) return HttpNotFound();

            ViewBag.PageTitle    = "Edit Classification";
            ViewBag.PageSubtitle = model.Name;

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(ClassificationFormViewModel model)
        {
            var classification = _db.TaskClassifications
                .Find(model.TaskClassificationId);

            if (classification == null) return HttpNotFound();

            model.Name = (model.Name ?? "").Trim();

            if (NameIsTaken(model.Name, classification.TaskClassificationId))
                ModelState.AddModelError("Name",
                    "A classification with this name already exists.");

            if (!ModelState.IsValid)
            {
                // The posted form carries only the name; the task types have to
                // be read back before the view can be rendered again.
                var reloaded = BuildForm(classification.TaskClassificationId);
                model.TaskTypes = reloaded.TaskTypes;
                model.IsClip    = reloaded.IsClip;

                ViewBag.PageTitle = "Edit Classification";
                return View(model);
            }

            var wasClip = IsClipName(classification.Name);
            classification.Name = model.Name;
            _db.SaveChanges();

            TempData["SuccessMessage"] =
                "Classification updated to '" + classification.Name + "'.";

            // Renaming this row away from "CLIP" leaves ClipService unable to
            // find it, which silently disables the CLIP picker. Saying so is
            // more use than refusing the edit.
            if (wasClip && !IsClipName(classification.Name))
                TempData["ErrorMessage"] =
                    "This was the CLIP classification. The CLIP item picker "
                    + "looks the row up by the name 'CLIP' and will no longer "
                    + "find it, so CLIP tasks can no longer be linked to a "
                    + "certificate or monitoring record.";

            return RedirectToAction("Edit",
                new { id = classification.TaskClassificationId });
        }

        // ══════════════════════════════════════════
        // DELETE CLASSIFICATION
        // ══════════════════════════════════════════

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            var classification = _db.TaskClassifications.Find(id);
            if (classification == null) return HttpNotFound();

            var blocked = DeleteBlockedReason(classification);
            if (blocked != null)
            {
                TempData["ErrorMessage"] =
                    "'" + classification.Name + "' was not deleted. " + blocked;
                return RedirectToAction("Index");
            }

            _db.TaskClassifications.Remove(classification);
            _db.SaveChanges();

            TempData["SuccessMessage"] =
                "Classification '" + classification.Name + "' deleted.";

            return RedirectToAction("Index");
        }

        // ══════════════════════════════════════════
        // TASK TYPES
        // ══════════════════════════════════════════

        // Adds a task type when TaskListId is 0, updates it otherwise. One
        // action because the row form is the same either way.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SaveTaskType(TaskTypeFormViewModel model)
        {
            var classification = _db.TaskClassifications
                .Find(model.TaskClassificationId);

            if (classification == null) return HttpNotFound();

            model.Name        = (model.Name ?? "").Trim();
            model.Description = (model.Description ?? "").Trim();

            var error = ValidateTaskType(model);
            if (error != null)
            {
                TempData["ErrorMessage"] = error;
                return RedirectToAction("Edit",
                    new { id = model.TaskClassificationId });
            }

            if (model.IsNew)
            {
                _db.TaskLists.Add(new TaskList
                {
                    Name                 = model.Name,
                    Description          = model.Description,
                    TaskClassificationId = model.TaskClassificationId
                });

                TempData["SuccessMessage"] =
                    "Task type '" + model.Name + "' added.";
            }
            else
            {
                var taskType = _db.TaskLists.Find(model.TaskListId);
                if (taskType == null) return HttpNotFound();

                taskType.Name        = model.Name;
                taskType.Description = model.Description;

                TempData["SuccessMessage"] =
                    "Task type '" + model.Name + "' updated.";
            }

            _db.SaveChanges();

            return RedirectToAction("Edit",
                new { id = model.TaskClassificationId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteTaskType(int id)
        {
            var taskType = _db.TaskLists.Find(id);
            if (taskType == null) return HttpNotFound();

            var classificationId = taskType.TaskClassificationId;
            var inUse = _db.TaskItems.Count(t => t.TaskListId == id);

            if (inUse > 0)
            {
                TempData["ErrorMessage"] =
                    "'" + taskType.Name + "' was not deleted. It is in use by "
                    + inUse + (inUse == 1 ? " task." : " tasks.");
            }
            else
            {
                _db.TaskLists.Remove(taskType);
                _db.SaveChanges();

                TempData["SuccessMessage"] =
                    "Task type '" + taskType.Name + "' deleted.";
            }

            return RedirectToAction("Edit", new { id = classificationId });
        }

        // ══════════════════════════════════════════
        // HELPERS
        // ══════════════════════════════════════════

        private ClassificationFormViewModel BuildForm(int id)
        {
            var classification = _db.TaskClassifications.Find(id);
            if (classification == null) return null;

            var taskTypes = _db.TaskLists
                .Where(l => l.TaskClassificationId == id)
                .OrderBy(l => l.Name)
                .ToList();

            // Grouped on the nullable column and unwrapped afterwards: EF
            // translates the key as it is declared, not as .Value.
            var taskCounts = _db.TaskItems
                .Where(t => t.TaskClassificationId == id
                         && t.TaskListId != null)
                .GroupBy(t => t.TaskListId)
                .Select(g => new { TaskListId = g.Key, Count = g.Count() })
                .ToList()
                .ToDictionary(x => x.TaskListId.Value, x => x.Count);

            var isClip = IsClipName(classification.Name);

            return new ClassificationFormViewModel
            {
                TaskClassificationId = classification.TaskClassificationId,
                Name   = classification.Name,
                IsClip = isClip,
                TaskTypes = taskTypes
                    .Select(l => new TaskTypeRowViewModel
                    {
                        TaskListId           = l.TaskListId,
                        TaskClassificationId = l.TaskClassificationId,
                        Name                 = l.Name,
                        Description          = l.Description,
                        TaskCount            = Lookup(taskCounts, l.TaskListId),

                        // Only meaningful under CLIP, where the name decides
                        // which CLIP table a task links to.
                        ClipKind = isClip
                            ? ClipService.ClassifyTaskListName(l.Name)
                            : null
                    })
                    .ToList()
            };
        }

        private string ValidateTaskType(TaskTypeFormViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Name))
                return "Task type name is required.";

            if (model.Name.Length > 100)
                return "Task type name cannot be longer than 100 characters.";

            if (model.Description != null && model.Description.Length > 100)
                return "Description cannot be longer than 100 characters.";

            // Names have to stay unique within their classification: the task
            // form shows nothing but the name, so two identical rows are
            // indistinguishable to whoever is assigning the task.
            var duplicate = _db.TaskLists.Any(l =>
                l.TaskClassificationId == model.TaskClassificationId
                && l.TaskListId != model.TaskListId
                && l.Name == model.Name);

            if (duplicate)
                return "'" + model.Name + "' already exists in this "
                     + "classification.";

            return null;
        }

        private string DeleteBlockedReason(TaskClassification classification)
        {
            if (IsClipName(classification.Name))
                return "The CLIP integration looks this classification up by "
                     + "name, so it cannot be removed.";

            var taskCount = _db.TaskItems.Count(t =>
                t.TaskClassificationId == classification.TaskClassificationId);

            if (taskCount > 0)
                return "It is in use by " + taskCount
                     + (taskCount == 1 ? " task." : " tasks.");

            var typeCount = _db.TaskLists.Count(l =>
                l.TaskClassificationId == classification.TaskClassificationId);

            if (typeCount > 0)
                return "Delete its " + typeCount
                     + (typeCount == 1 ? " task type" : " task types")
                     + " first.";

            return null;
        }

        private bool NameIsTaken(string name, int? exceptId = null)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;

            // SQL Server compares case-insensitively under the default
            // collation, so "clip" and "CLIP" collide here as they should.
            return _db.TaskClassifications.Any(c =>
                c.Name == name
                && (!exceptId.HasValue
                    || c.TaskClassificationId != exceptId.Value));
        }

        private static bool IsClipName(string name)
        {
            return string.Equals(name, TaskClassification.ClipName,
                StringComparison.OrdinalIgnoreCase);
        }

        private static int Lookup(Dictionary<int, int> counts, int key)
        {
            return counts.ContainsKey(key) ? counts[key] : 0;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _db.Dispose();
            base.Dispose(disposing);
        }
    }
}
