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

        // Link to subtask for CLIP if applicable. Which CLIP table the id refers
        // to is decided by TaskList: "Certificate Of FItness" means
        // CLIP.CertificateOfFitness.Id, "Plant Monitoring" means
        // CLIP.PlantMonitoring.Id. Deliberately not a foreign key - those tables
        // belong to EHS_PORTAL and ESTAFF only ever reads them.
        public int? SubTaskId { get; set; }

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
