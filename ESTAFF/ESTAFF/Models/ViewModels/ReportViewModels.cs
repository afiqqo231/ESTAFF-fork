using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ESTAFF.Models.Data;

namespace ESTAFF.Models.ViewModels
{
    public class GenerateReportViewModel
    {
        [Required(ErrorMessage = "Report type is required")]
        [Display(Name = "Report Type")]
        public ReportType ReportType { get; set; }

        [Required(ErrorMessage = "Period start is required")]
        [DataType(DataType.Date)]
        [Display(Name = "Period Start")]
        public DateTime PeriodStart { get; set; } = DateTime.Today
            .AddDays(-(int)DateTime.Today.DayOfWeek + 1);

        [Required(ErrorMessage = "Period end is required")]
        [DataType(DataType.Date)]
        [Display(Name = "Period End")]
        public DateTime PeriodEnd { get; set; } = DateTime.Today;

        // Preview tasks
        public List<TaskItem> Tasks { get; set; }
            = new List<TaskItem>();
    }

    public class ReportListItemViewModel
    {
        public int ReportId { get; set; }
        public string EmpName { get; set; }
        public string EmpNumber { get; set; }
        public ReportType ReportType { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public ReportStatus Status { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? SubmittedDate { get; set; }
        public DateTime? ApprovedDate { get; set; }
        public string RejectionReason { get; set; }
    }

    public class ReportDetailViewModel
    {
        public int ReportId { get; set; }
        public string EmpName { get; set; }
        public string EmpNumber { get; set; }
        public string EmpEmail { get; set; }
        public ReportType ReportType { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public ReportStatus Status { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? SubmittedDate { get; set; }
        public DateTime? ApprovedDate { get; set; }
        public string RejectionReason { get; set; }
        public List<TaskItem> Tasks { get; set; }
            = new List<TaskItem>();

        // Stats
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int PendingTasks { get; set; }
        public int OverdueTasks { get; set; }
        public decimal CompletionRate { get; set; }
    }

    public class ApproveReportViewModel
    {
        public int ReportId { get; set; }

        [Display(Name = "Rejection Reason")]
        public string RejectionReason { get; set; }
    }
}