using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ESTAFF.Models.Data
{
    public class TaskHistory
    {
        [Key]
        public int HistoryId { get; set; }

        [Required]
        [ForeignKey("Task")]
        public int TaskId { get; set; }

        [Required]
        [StringLength(50)]
        public string Action { get; set; }

        public string OldValue { get; set; }

        public string NewValue { get; set; }

        [Required]
        [ForeignKey("ChangedByUser")]
        public string ChangedByUserId { get; set; }

        public DateTime ChangedDate { get; set; } = DateTime.Now;

        public virtual TaskItem Task { get; set; }
        public virtual ApplicationUser ChangedByUser { get; set; }
    }
}