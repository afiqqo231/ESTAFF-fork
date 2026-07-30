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

        public string Description { get; set; }

        [Required]
        public TaskStatus Status { get; set; } = TaskStatus.Pending;

        public TaskPriority? Priority { get; set; }

        [Required]
        public DateTime DueDate { get; set; }

        // Which EHS work stream this task belongs to (required by the schema).
        [Required]
        public int TaskClassificationId { get; set; }

        // The specific recurring job within that classification. Optional, but
        // for CLIP tasks it is what tells SubTaskId which table to look in.
        // The column keeps EF's original navigation-derived name.
        [Column("TaskList_TaskListId")]
        public int? TaskListId { get; set; }

        // Id of the linked record in the module that owns this classification.
        // For CLIP that is CLIP.CertificateOfFitness.Id or CLIP.PlantMonitoring.Id,
        // decided by TaskListId. Deliberately not a foreign key: those tables
        // belong to EHS_PORTAL and ESTAFF only ever reads them.
        public int? SubTaskId { get; set; }

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

        [ForeignKey("TaskClassificationId")]
        public virtual TaskClassification TaskClassification { get; set; }

        [ForeignKey("TaskListId")]
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
