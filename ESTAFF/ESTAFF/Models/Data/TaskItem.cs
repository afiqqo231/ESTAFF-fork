using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ESTAFF.Models.Data
{
    [Table("TaskItems", Schema = "ESTAFF")]
    public class TaskItem
    {
        [Key]
        public int TaskId { get; set; }

        [Required]
        [StringLength(256)]
        public string Title { get; set; }

        // The property and column stay "Description": renaming them would mean
        // a migration against a database shared with EHS_PORTAL. Only what the
        // user reads changes.
        [Display(Name = "Concern/Issue")]
        public string Description { get; set; }

        [Required]
        public TaskStatus Status { get; set; } = TaskStatus.Pending;

        public TaskPriority? Priority { get; set; }

        [Required]
        public DateTime DueDate { get; set; }

        // ── Attached CLIP record (optional) ─────────────────────
        //
        // Any task may cover a certificate of fitness or a plant monitoring
        // record, whatever its classification. The pair below is the whole
        // attachment: ClipItemKind says which CLIP table, SubTaskId says which
        // row in it. Both set means attached; either null means not.
        //
        // Deliberately not a foreign key - those tables belong to EHS_PORTAL
        // and ESTAFF only ever reads them.
        //
        // This used to be implied rather than stored: a task was CLIP work when
        // its classification was named "CLIP", and its TaskList name decided
        // which table SubTaskId meant. That made attaching a record a
        // consequence of how the task was filed, so the same certificate could
        // not be covered by a task classified as anything else. Kind is now
        // recorded outright and the classification is left to mean what it
        // says.
        public ClipItemKind? ClipItemKind { get; set; }

        // The id of the attached CLIP row. Keeps the column name SubTaskId: it
        // is in a database shared with EHS_PORTAL, so renaming it would be a
        // migration against another application's neighbourhood for no gain.
        public int? SubTaskId { get; set; }

        // True when the task carries a complete CLIP attachment. Half a link -
        // a kind with no id, or an id with no kind - is treated as none, which
        // is what rows written before ClipItemKind existed look like until
        // Add_Task_ClipItemKind.sql backfills them.
        [NotMapped]
        public bool HasClipItem => ClipItemKind.HasValue && SubTaskId.HasValue;

        // The specific recurring job within the classification. The column keeps
        // the name EF generated from TaskList.TaskItems; mapping it explicitly
        // here lets a task read its own task list without loading the collection.
        [Column("TaskList_TaskListId")]
        public int? TaskListId { get; set; }

        [Required]
            [ForeignKey("TaskClassification")]
        public int TaskClassificationId { get; set; }

        [Required]
        [ForeignKey("AssignedToUser")]
        public string AssignedToUserId { get; set; }

        [Required]
        [ForeignKey("CreatedByUser")]
        public string CreatedByUserId { get; set; }

        public DateTime AssignedDate { get; set; } = DateTime.Now;

        public DateTime? CompletedDate { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public DateTime LastModifiedDate { get; set; } = DateTime.Now;


        public virtual ApplicationUser AssignedToUser { get; set; }
        public virtual ApplicationUser CreatedByUser { get; set; }
        public virtual TaskClassification TaskClassification { get; set; }
        public virtual TaskList TaskList { get; set; }
        public virtual ICollection<TaskHistory> Histories { get; set; } = new List<TaskHistory>();
    }

    public enum TaskStatus
    {
        Pending = 1,
        InProgress = 2,
        Complete = 3,
        Overdue = 4
    }

    public enum TaskPriority
    {
        Low = 1,
        Medium = 2,
        High = 3
    }

}
