using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ESTAFF.Models.Data;

namespace ESTAFF.Models.ViewModels
{
    // ══════════════════════════════════════════════════════════════
    // Task taxonomy maintenance.
    //
    // The assign/edit task forms read ESTAFF.TaskClassifications and
    // ESTAFF.TaskLists straight out of the database, so these screens are the
    // supported way to change what those dropdowns offer.
    //
    // "Task type" is what the UI calls a TaskList row - the recurring job
    // within a classification. The entity keeps its original name.
    // ══════════════════════════════════════════════════════════════

    // One row on the classification list.
    public class ClassificationListItemViewModel
    {
        public int TaskClassificationId { get; set; }
        public string Name { get; set; }

        // How much depends on this row. Both have to be zero before it can be
        // removed, and both are worth showing before anyone tries.
        public int TaskTypeCount { get; set; }
        public int TaskCount { get; set; }

        // The row ClipService resolves by name. It drives the CLIP item picker,
        // so the screens treat it as load-bearing rather than ordinary data.
        public bool IsClip { get; set; }

        public bool CanDelete => !IsClip && TaskCount == 0 && TaskTypeCount == 0;

        public string Slug => TaskDisplay.ClassificationSlug(Name);
        public string Icon => TaskDisplay.ClassificationIcon(Name);

        // Why the delete button is disabled, phrased for the person reading it.
        public string DeleteBlockedReason
        {
            get
            {
                if (IsClip)
                    return "The CLIP classification is referenced by name in "
                         + "the CLIP integration and cannot be removed.";

                if (TaskCount > 0)
                    return "In use by " + Count(TaskCount, "task") + ".";

                if (TaskTypeCount > 0)
                    return "Remove its " + Count(TaskTypeCount, "task type")
                         + " first.";

                return null;
            }
        }

        private static string Count(int value, string noun)
        {
            return value + " " + noun + (value == 1 ? "" : "s");
        }
    }

    // Create and edit a classification. One shape for both, because the two
    // forms ask for exactly the same thing.
    public class ClassificationFormViewModel
    {
        public int? TaskClassificationId { get; set; }

        [Required(ErrorMessage = "Name is required")]
        [StringLength(100, ErrorMessage =
            "Name cannot be longer than 100 characters")]
        [Display(Name = "Classification Name")]
        public string Name { get; set; }

        public bool IsClip { get; set; }

        // Populated on edit only - a classification has to exist before task
        // types can hang off it.
        public List<TaskTypeRowViewModel> TaskTypes { get; set; }
            = new List<TaskTypeRowViewModel>();

        public bool IsNew => !TaskClassificationId.HasValue;

        public string Slug => TaskDisplay.ClassificationSlug(Name);
        public string Icon => TaskDisplay.ClassificationIcon(Name);
    }

    // One task type under a classification, as shown on the edit screen.
    public class TaskTypeRowViewModel
    {
        public int TaskListId { get; set; }
        public int TaskClassificationId { get; set; }

        public string Name { get; set; }
        public string Description { get; set; }

        public int TaskCount { get; set; }

        public bool CanDelete => TaskCount == 0;

        // Set for task types under CLIP: which CLIP record the picker links a
        // task to, decided by ClipService.ClassifyTaskListName from this name.
        public ClipItemKind? ClipKind { get; set; }

        public string ClipKindLabel
        {
            get
            {
                if (!ClipKind.HasValue) return null;

                return ClipKind.Value == ClipItemKind.COF
                    ? "Links to Certificate of Fitness records"
                    : "Links to Plant Monitoring records";
            }
        }
    }

    // The add/edit form for a task type. Posted from the classification edit
    // screen, one form per row.
    //
    // No validation attributes here on purpose: these forms redirect back to
    // the edit screen, and ModelState does not survive a redirect. The rules
    // live in ClassificationsController.ValidateTaskType, which reports them
    // through TempData - one place rather than two that can disagree.
    public class TaskTypeFormViewModel
    {
        public int TaskListId { get; set; }
        public int TaskClassificationId { get; set; }

        public string Name { get; set; }
        public string Description { get; set; }

        public bool IsNew => TaskListId == 0;
    }
}
