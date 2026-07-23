using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ESTAFF.Models.Data
{
    public class PlantMonitoring
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int PlantID { get; set; }

        [Required]
        public int MonitoringID { get; set; }

        [StringLength(100)]
        public string Area { get; set; }

        [DataType(DataType.DateTime)]
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy HH:mm}", ApplyFormatInEditMode = false)]
        public DateTime? ExpDate { get; set; }

        // Quotation Phase
        [DataType(DataType.DateTime)]
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy HH:mm}", ApplyFormatInEditMode = false)]
        public DateTime? QuoteDate { get; set; }

        [DataType(DataType.DateTime)]
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy HH:mm}", ApplyFormatInEditMode = false)]
        public DateTime? QuoteCompleteDate { get; set; }

        [StringLength(100)]
        public string QuoteUserAssign { get; set; }

        [StringLength(500)]
        public string QuoteDoc { get; set; }

        // Preparation Phase
        [DataType(DataType.DateTime)]
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy HH:mm}", ApplyFormatInEditMode = false)]
        public DateTime? EprDate { get; set; }

        [DataType(DataType.DateTime)]
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy HH:mm}", ApplyFormatInEditMode = false)]
        public DateTime? EprCompleteDate { get; set; }

        [StringLength(100)]
        [Display(Name = "Preparation Assigned To")]
        public string EprUserAssign { get; set; }

        [StringLength(500)]
        [Display(Name = "ePR Document")]
        public string EprDoc { get; set; }

        // Work Execution Phase
        [DataType(DataType.DateTime)]
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy HH:mm}", ApplyFormatInEditMode = false)]
        public DateTime? WorkDate { get; set; }

        [DataType(DataType.DateTime)]
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy HH:mm}", ApplyFormatInEditMode = false)]
        public DateTime? WorkSubmitDate { get; set; }

        [DataType(DataType.DateTime)]
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy HH:mm}", ApplyFormatInEditMode = false)]
        public DateTime? WorkCompleteDate { get; set; }

        [StringLength(100)]
        public string WorkUserAssign { get; set; }

        [StringLength(500)]
        public string WorkDoc { get; set; }

        [Display(Name = "Remarks")]
        public string Remarks { get; set; }

        [StringLength(50)]
        [Display(Name = "Process Status")]
        public string ProcStatus { get; set; }

        [StringLength(50)]
        [Display(Name = "Expiration Status")]
        public string ExpStatus { get; set; }

        // Navigation properties
        [ForeignKey("PlantID")]
        public virtual Plant Plant { get; set; }

        [ForeignKey("MonitoringID")]
        public virtual Monitoring Monitoring { get; set; }

        // Helper method to calculate process status
        public void CalculateProcStatus()
        {
            if (WorkCompleteDate.HasValue)
                ProcStatus = "Completed";
            else if (WorkDate.HasValue)
                ProcStatus = "Work In Progress";
            else if (EprDate.HasValue)
                ProcStatus = "ePR Raised";
            else if (QuoteDate.HasValue)
                ProcStatus = "Quotation Requested";
            else
                ProcStatus = "Not Started";
        }

        // Helper method to calculate expiration status
        public void CalculateExpStatus()
        {
            string previousExpStatus = ExpStatus; // Store previous status to detect changes
            
            if (!ExpDate.HasValue)
                ExpStatus = "No Expiry";
            else if (ExpDate < DateTime.Now)
                ExpStatus = "Expired";
            else if (ExpDate < DateTime.Now.AddDays(90))
                ExpStatus = "Expiring Soon";
            else
                ExpStatus = "Active";
                
            // If status changed to "Expiring Soon", automatically set process status to "Not Started"
            if (ExpStatus == "Expiring Soon" && previousExpStatus != "Expiring Soon")
            {
                ProcStatus = "Not Started";
            }
        }

        // Helper method to calculate both statuses
        public void CalculateStatuses()
        {
            CalculateProcStatus();
            CalculateExpStatus();
        }

        [NotMapped]
        public string ProcStatusCssClass
        {
            get
            {
                switch (ProcStatus)
                {
                    case "Completed":
                        return "bg-success";
                    case "Work In Progress":
                        return "bg-warning";
                    case "ePR Raised":
                        return "bg-warning";
                    case "Quotation Requested":
                        return "bg-primary";
                    case "Not Started":
                        return "bg-notstarted";
                    default:
                        return "";
                }
            }
        }

        [NotMapped]
        public string ExpStatusCssClass
        {
            get
            {
                switch (ExpStatus)
                {
                    case "Expired":
                        return "bg-danger";
                    case "Expiring Soon":
                        return "bg-warning";
                    case "Active":
                        return "bg-success";
                    case "No Expiry":
                        return "bg-secondary";
                    default:
                        return "";
                }
            }
        }
    }
} 