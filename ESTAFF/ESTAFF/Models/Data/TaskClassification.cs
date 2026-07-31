using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ESTAFF.Models.Data
{
    // The EHS work stream a task belongs to. A lookup table rather than an enum
    // because the taxonomy is maintained as data (see ESTAFF.TaskClassifications):
    //   1 Chemical & Legal   2 DOSH / BOMBA / DOE
    //   3 Environmental      4 CLIP
    // Only CLIP has records in another module to link to - see ClipService.
    [Table("TaskClassifications", Schema = "ESTAFF")]
    public class TaskClassification
    {
        // Name of the classification that owns the CLIP integration. Matched
        // case-insensitively so renaming the row's casing does not break it.
        public const string ClipName = "CLIP";

        [Key]
        public int TaskClassificationId { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        public virtual ICollection<TaskList> TaskLists { get; set; }
        public virtual ICollection<TaskItem> TaskItems { get; set; }
    }
}
