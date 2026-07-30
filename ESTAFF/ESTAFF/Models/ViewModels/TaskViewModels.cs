using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.RegularExpressions;
using ESTAFF.Models.Data;
using Newtonsoft.Json;

namespace ESTAFF.Models.ViewModels
{
    public class AssignTaskViewModel
    {
        [Required(ErrorMessage = "Title is required")]
        [StringLength(256)]
        [Display(Name = "Task Title")]
        public string Title { get; set; }

        [Display(Name = "Description")]
        public string Description { get; set; }

        [Required(ErrorMessage = "Please assign to an employee")]
        [Display(Name = "Assign To")]
        public string AssignedToUserId { get; set; }

        [Required(ErrorMessage = "Due date is required")]
        [DataType(DataType.Date)]
        [Display(Name = "Due Date")]
        public DateTime DueDate { get; set; } = DateTime.Today.AddDays(7);

        [Display(Name = "Priority")]
        public TaskPriority? Priority { get; set; }

        [Required(ErrorMessage = "Please select a task classification")]
        [Display(Name = "Task Classification")]
        public int TaskClassificationId { get; set; }

        [Required(ErrorMessage = "Please select a task")]
        [Display(Name = "Task")]
        public int TaskListId { get; set; }

        // Posted by the CLIP picker on the admin form as "COF:14" / "PM:3".
        [Display(Name = "CLIP Item")]
        public string ClipItemKey { get; set; }

        public TaskFormOptions Options { get; set; } = new TaskFormOptions();

        // Dropdown List of Employees
        public List<EmployeeSelectItem> Employees { get; set; } 
            = new List<EmployeeSelectItem>();
    }

    public class EditTaskViewModel
    {
        public int TaskId { get; set; }

        [Required(ErrorMessage = "Title is required")]
        [StringLength(256)]
        [Display(Name = "Task Title")]
        public string Title { get; set; }

        [Display(Name = "Description")]
        public string Description { get; set; }

        [Required(ErrorMessage = "Please assign to an employee")]
        [Display(Name = "Assign To")]
        public string AssignedToUserId { get; set; }

        [Required(ErrorMessage = "Due date is required")]
        [DataType(DataType.Date)]
        [Display(Name = "Due Date")]
        public DateTime DueDate { get; set; }

        [Display(Name = "Priority")]
        public TaskPriority? Priority { get; set; }

        [Display(Name = "Status")]
        public TaskStatus Status { get; set; }

        [Display(Name = "Task Classification")]
        public int TaskClassificationId { get; set; }

        [Display(Name = "Task")]
        public int? TaskListId { get; set; }

        [Display(Name = "CLIP Item")]
        public string ClipItemKey { get; set; }

        // Only recorded when the status actually changes on save.
        [StringLength(500)]
        [Display(Name = "Status Remark")]
        public string StatusRemark { get; set; }

        public TaskFormOptions Options { get; set; } = new TaskFormOptions();

        // Dropdown List
        public List<EmployeeSelectItem> Employees { get; set; }
            = new List<EmployeeSelectItem>();
    }

    public class TaskListItemViewModel
    {
        public int TaskId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public int? SubTaskId { get; set; }
        public TaskStatus Status { get; set; }
        public TaskPriority? Priority { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public string AssignedToUserId { get; set; }
        public string AssignedToName { get; set; }
        public string AssignedToEmpID { get; set; }
        public string CreatedByName { get; set; }

        public int TaskClassificationId { get; set; }
        public string ClassificationName { get; set; }
        public int? TaskListId { get; set; }
        public string TaskListName { get; set; }

        // The linked CLIP record, when this task is CLIP-classified and linked.
        public ClipItemViewModel ClipItem { get; set; }

        // Newest status transition, shown inline on the task.
        public StatusRemarkViewModel LatestStatusRemark { get; set; }

        public bool IsOverdue => Status != TaskStatus.Complete
            && DueDate.Date < DateTime.Today;

        // CSS modifier suffix for the status badge / dot (badge-pending, ...).
        public string StatusClass => TaskDisplay.StatusClass(Status);

        public string ClassificationSlug =>
            TaskDisplay.ClassificationSlug(ClassificationName);

        public string ClassificationIcon =>
            TaskDisplay.ClassificationIcon(ClassificationName);

        public string ClassificationLabel =>
            string.IsNullOrWhiteSpace(ClassificationName)
                ? "Unclassified"
                : ClassificationName;
    }

    public class TaskHistoryItemViewModel
    {
        public int HistoryId { get; set; }
        public int TaskId { get; set; }
        public string TaskTitle { get; set; }
        public string Action { get; set; }
        public string OldValue { get; set; }
        public string NewValue { get; set; }
        public string Remark { get; set; }
        public string ChangedByName { get; set; }
        public DateTime ChangedDate { get; set; }
    }

    public class EmployeeSelectItem
    {
        public string UserId { get; set; }
        public string FullName { get; set; }
        public string EmpID { get; set; }
        public string Display => $"{FullName} ({EmpID})";
    }


    // Employee Side
    public class CreateTaskViewModel
    {
        [Required(ErrorMessage = "Title is required")]
        [StringLength(256)]
        [Display(Name = "Task Title")]
        public string Title { get; set; }

        [Display(Name = "Description")]
        public string Description { get; set; }

        [Required(ErrorMessage = "Due date is required")]
        [DataType(DataType.Date)]
        [Display(Name = "Due Date")]
        public DateTime DueDate { get; set; } = DateTime.Today.AddDays(1);

        [Display(Name = "Certificate Of Fitness (COF)")]
        public int? SubTaskId { get; set; }
        
        [Required(ErrorMessage = "Please select a task classification")]
        [Display(Name = "Task Classification")]
        public int TaskClassificationId { get; set; }

        [Required(ErrorMessage = "Please select a task")]
        [Display(Name = "Task List")]
        public int TaskListId { get; set; }
        
        [Display(Name = "Priority")]
        public TaskPriority? Priority { get; set; }
    }

    public class UpdateTaskStatusViewModel
    {
        public int TaskId { get; set; }
        public TaskStatus Status { get; set; }

        [StringLength(500)]
        public string Remark { get; set; }
    }

    // A single status transition, with whatever note the user attached to it.
    // Populated by TaskService.GetLatestStatusRemark(s).
    public class StatusRemarkViewModel
    {
        public int TaskId { get; set; }
        public TaskStatus? FromStatus { get; set; }
        public TaskStatus? ToStatus { get; set; }
        public string Remark { get; set; }
        public string ChangedByName { get; set; }
        public DateTime ChangedDate { get; set; }

        public bool HasRemark => !string.IsNullOrWhiteSpace(Remark);

        // "Pending -> In Progress", or just the new status on the first entry.
        public string TransitionText
        {
            get
            {
                var to = ToStatus.HasValue
                    ? TaskDisplay.StatusLabel(ToStatus.Value)
                    : "-";

                return FromStatus.HasValue
                    ? TaskDisplay.StatusLabel(FromStatus.Value) + " → " + to
                    : to;
            }
        }

        // "just now" / "3h ago" / "12 Jul 2026"
        public string RelativeTime
        {
            get
            {
                var span = DateTime.Now - ChangedDate;

                if (span.TotalSeconds < 60) return "just now";
                if (span.TotalMinutes < 60)
                    return (int)span.TotalMinutes + "m ago";
                if (span.TotalHours < 24)
                    return (int)span.TotalHours + "h ago";
                if (span.TotalDays < 7)
                    return (int)span.TotalDays + "d ago";

                return ChangedDate.ToString("dd MMM yyyy");
            }
        }
    }

    // ══════════════════════════════════════════════════════════════
    // Admin-side task form support.
    //
    // The employee pages drive classification through the cascading
    // ViewBag SelectLists in EmployeeController (PopulateTaskClassification /
    // PopulateTaskList / GetTaskByClassification). The admin pages use these
    // strongly-typed options instead. Worth unifying on one approach later -
    // until then both are live and neither depends on the other.
    // ══════════════════════════════════════════════════════════════

    public class TaskFormOptions
    {
        public List<ClassificationOption> Classifications { get; set; }
            = new List<ClassificationOption>();

        // All task types across every classification. The form filters them
        // client-side as the classification changes.
        public List<TaskListOption> TaskLists { get; set; }
            = new List<TaskListOption>();

        // CLIP records for the relevant employee's plants, nearest expiry first.
        public List<ClipItemViewModel> ClipItems { get; set; }
            = new List<ClipItemViewModel>();

        // Id of the classification named "CLIP" - the one that reveals the CLIP
        // picker. Null when that row is missing from the lookup table.
        public int? ClipClassificationId { get; set; }
    }

    public class ClassificationOption
    {
        public int TaskClassificationId { get; set; }
        public string Name { get; set; }

        public string Slug => TaskDisplay.ClassificationSlug(Name);
        public string Icon => TaskDisplay.ClassificationIcon(Name);
    }

    public class TaskListOption
    {
        public int TaskListId { get; set; }
        public int TaskClassificationId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
    }

    // What the _ClassificationField partial needs, so the admin task forms can
    // render the same block from their own view models.
    public class TaskFormFieldsViewModel
    {
        public int? TaskClassificationId { get; set; }
        public int? TaskListId { get; set; }
        public string ClipItemKey { get; set; }

        public TaskFormOptions Options { get; set; } = new TaskFormOptions();

        // Set on the admin forms, where the CLIP list depends on the assignee.
        public string ClipReloadUrl { get; set; }
        public string ClipReloadTriggerId { get; set; }

        public string ClipHint { get; set; } =
            "Certificates of fitness and plant monitoring records for the " +
            "assigned plants. Expired items are listed first.";

        public static TaskFormFieldsViewModel From(AssignTaskViewModel m)
        {
            return new TaskFormFieldsViewModel
            {
                TaskClassificationId = m.TaskClassificationId,
                TaskListId = m.TaskListId,
                ClipItemKey = m.ClipItemKey,
                Options = m.Options
            };
        }

        public static TaskFormFieldsViewModel From(EditTaskViewModel m)
        {
            return new TaskFormFieldsViewModel
            {
                TaskClassificationId = m.TaskClassificationId,
                TaskListId = m.TaskListId,
                ClipItemKey = m.ClipItemKey,
                Options = m.Options
            };
        }
    }

    // What _ClassificationBadge needs. A small carrier type so the partial can
    // be fed from a TaskItem or a TaskListItemViewModel alike.
    public class ClassificationBadgeModel
    {
        public string Name { get; set; }
        public string TaskListName { get; set; }

        // Dense layouts (table cells, week chips) hide the sub-type.
        public bool ShowTaskList { get; set; } = true;

        public static ClassificationBadgeModel From(
            TaskListItemViewModel task, bool showTaskList = true)
        {
            return new ClassificationBadgeModel
            {
                Name = task.ClassificationName,
                TaskListName = task.TaskListName,
                ShowTaskList = showTaskList
            };
        }

        public static ClassificationBadgeModel From(
            TaskItem task, bool showTaskList = true)
        {
            return new ClassificationBadgeModel
            {
                Name = task.TaskClassification?.Name,
                TaskListName = task.TaskList?.Name,
                ShowTaskList = showTaskList
            };
        }
    }

    // Presentation rules shared by every view that renders a task, so badge
    // classes and labels cannot drift between the admin and employee pages.
    public static class TaskDisplay
    {
        public static string StatusClass(TaskStatus status)
        {
            return status == TaskStatus.InProgress
                ? "inprogress"
                : status.ToString().ToLower();
        }

        public static string StatusLabel(TaskStatus status)
        {
            return status == TaskStatus.InProgress
                ? "In Progress"
                : status.ToString();
        }

        // Classifications are rows, not an enum, so the badge colour is keyed on
        // a slug derived from the name: "DOSH / BOMBA / DOE" -> "dosh-bomba-doe".
        // A classification added later simply falls back to the neutral style.
        public static string ClassificationSlug(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "unknown";

            var slug = Regex.Replace(name.ToLowerInvariant(), "[^a-z0-9]+", "-");
            return slug.Trim('-');
        }

        public static string ClassificationIcon(string name)
        {
            switch (ClassificationSlug(name))
            {
                case "clip":            return "fa-shield-halved";
                case "chemical-legal":  return "fa-flask";
                case "dosh-bomba-doe":  return "fa-helmet-safety";
                case "environmental":   return "fa-leaf";
                default:                return "fa-tag";
            }
        }

        public static List<ClassificationOption> ToOptions(
            IEnumerable<TaskClassification> classifications)
        {
            return classifications
                .Select(c => new ClassificationOption
                {
                    TaskClassificationId = c.TaskClassificationId,
                    Name = c.Name
                })
                .ToList();
        }

        public static List<TaskListOption> ToOptions(IEnumerable<TaskList> lists)
        {
            return lists
                .Select(l => new TaskListOption
                {
                    TaskListId = l.TaskListId,
                    TaskClassificationId = l.TaskClassificationId,
                    Name = l.Name,
                    Description = l.Description
                })
                .ToList();
        }
    }
}