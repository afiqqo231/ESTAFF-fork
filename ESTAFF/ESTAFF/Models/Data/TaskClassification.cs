using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ESTAFF.Models.Data
{
    [Table("TaskClassifications", Schema = "ESTAFF")]
    public class TaskClassification
    {
        [Key]
        public int TaskClassificationId { get; set; }
        
        [Required]
        [StringLength(100)]
        public string Name { get; set; }
        
        public virtual ICollection<TaskList> TaskLists { get; set; }
        public virtual ICollection<TaskItem> TaskItems { get; set; }
    }
}