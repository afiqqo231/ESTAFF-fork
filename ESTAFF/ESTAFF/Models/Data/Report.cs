using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ESTAFF.Models.Data
{
    public class Report
    {
        [Key]
        public int ReportId { get; set; }

        [Required]
        [ForeignKey("User")]
        public string UserId { get; set; }

        [Required]
        public ReportType ReportType { get; set; }

        [Required]
        public DateTime PeriodStart { get; set; }

        [Required]
        public DateTime PeriodEnd { get; set; }

        public string Content { get; set; }

        [Required]
        public ReportStatus Status { get; set; } = ReportStatus.Draft;

        public DateTime? SubmittedDate { get; set; }

        public DateTime? ApprovedDate { get; set; }

        public string RejectionReason { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public DateTime LastModifiedDate { get; set; } = DateTime.Now;

        // Navigation
        public virtual ApplicationUser User { get; set; }
    }

    public enum ReportType
    {
        weekly = 1,
        monthly = 2,   
    }

    public enum ReportStatus
    {
        Draft = 1, 
        Submitted = 2,
        Approved = 3,
        Rejected = 4
    }
}