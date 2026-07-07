using System;
using System.Collections.Generic;
using Microsoft.AspNet.Identity.EntityFramework;

namespace ESTAFF.Models.Data
{
    
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; }
        public string Role { get; set; }
        public string EmpNumber { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime LastModifiedDate { get; set; } = DateTime.Now;


        public virtual ICollection<Staff> ManagedStaffs { get; set; } = new List<Staff>();
        public virtual ICollection<TaskItem> AssignedTasks { get; set; } = new List<TaskItem>();
        public virtual ICollection<TaskItem> CreatedTasks {get; set; } = new List<TaskItem>();
        public virtual ICollection<TaskHistory> TaskHistories { get; set; } = new List<TaskHistory>();
        public virtual ICollection<Report> SubmittedReports { get; set; } = new List<Report>();
        public virtual ICollection<Report> ApprovedReports { get; set; } = new List<Report>();
    }
}