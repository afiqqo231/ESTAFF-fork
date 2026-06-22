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
        [ForeignKey("Staff")]
        public string StaffId { get; set; }

        [Required]
        public ReportType ReportType { get; set; }

        [Required]
        public DateTime ReportPeriodStart { get; set; }

        [Required]
        public DateTime ReportPeriodEnd { get; set; }

        public string Content { get; set; }

        [Required]
        public ReportStatus Status { get; set; } = ReportStatus.Draft;

        public DateTime? SubmittedDate { get; set; }

        [ForeignKey("SubmittedByUser")]
        public string SubmittedByUserId { get; set; }
        
        [ForeignKey("ApprovedByUser")]
        public string ApprovedByUserId { get; set; }
        public DateTime? ApprovedDate { get; set; }

        public string RejectionReason { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public DateTime LastModifiedDate { get; set; } = DateTime.Now;


        public virtual Staff Staff { get; set; } 
        public virtual ApplicationUser SubmittedByUser { get; set; }
        public virtual ApplicationUser ApprovedByUser { get; set; }
        public virtual ICollection<ReportApproval> Approvals { get; set;} = new List<ReportApproval>();
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