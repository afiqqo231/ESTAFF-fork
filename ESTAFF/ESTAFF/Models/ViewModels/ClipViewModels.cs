using System;
using System.Collections.Generic;

namespace ESTAFF.Models.ViewModels
{
    public enum ClipItemKind
    {
        COF = 1,
        PlantMonitoring = 2
    }

    // How close an item is to its expiry. Drives the colour/urgency treatment in
    // the CLIP picker and on the task cards.
    public enum ClipUrgency
    {
        None        = 0,   // no expiry date recorded
        Active      = 1,
        ExpiringSoon = 2,
        Expired     = 3
    }

    // A single CLIP record (Certificate of Fitness or Plant Monitoring) flattened
    // into one shape so both kinds can share the same dropdown and badges.
    public class ClipItemViewModel
    {
        public ClipItemKind Kind { get; set; }
        public int Id { get; set; }

        public int PlantId { get; set; }
        public string PlantName { get; set; }

        // COF: registration number.  Plant Monitoring: monitoring name.
        public string Title { get; set; }

        // COF: machine name.  Plant Monitoring: area.
        public string Subtitle { get; set; }

        public DateTime? ExpiryDate { get; set; }

        // "Expired" / "Expiring Soon" / "Active" / "No Expiry" — mirrors CLIP.
        public string ExpiryStatus { get; set; }

        // Plant Monitoring only: "Not Started" / "Quotation Requested" / ...
        public string ProcessStatus { get; set; }

        public ClipUrgency Urgency { get; set; }

        // Stable identifier posted by the CLIP picker, e.g. "COF:14" / "PM:3".
        // Carries the kind as well as the id because both CLIP tables number
        // their rows independently — the id alone is ambiguous.
        public string Key
        {
            get
            {
                return (Kind == ClipItemKind.COF ? "COF" : "PM") + ":" + Id;
            }
        }

        public string KindLabel
        {
            get
            {
                return Kind == ClipItemKind.COF
                    ? "Certificate of Fitness"
                    : "Plant Monitoring";
            }
        }

        public string KindShortLabel
        {
            get { return Kind == ClipItemKind.COF ? "COF" : "Monitoring"; }
        }

        public string KindIcon
        {
            get
            {
                return Kind == ClipItemKind.COF
                    ? "fa-certificate"
                    : "fa-gauge-high";
            }
        }

        // Negative once the item is past its expiry date.
        public int? DaysToExpiry
        {
            get
            {
                if (!ExpiryDate.HasValue) return null;
                return (int)Math.Round(
                    (ExpiryDate.Value.Date - DateTime.Today).TotalDays);
            }
        }

        // "expired" / "soon" / "active" / "none" — used as a CSS modifier suffix.
        public string UrgencyClass
        {
            get
            {
                switch (Urgency)
                {
                    case ClipUrgency.Expired:      return "expired";
                    case ClipUrgency.ExpiringSoon: return "soon";
                    case ClipUrgency.Active:       return "active";
                    default:                       return "none";
                }
            }
        }

        // Short human phrasing of the countdown, e.g. "Expired 12 days ago".
        public string ExpiryText
        {
            get
            {
                var days = DaysToExpiry;
                if (!days.HasValue) return "No expiry date";

                if (days.Value < 0)
                {
                    var overdue = -days.Value;
                    return overdue == 1
                        ? "Expired 1 day ago"
                        : "Expired " + overdue + " days ago";
                }

                if (days.Value == 0) return "Expires today";
                if (days.Value == 1) return "Expires tomorrow";
                return "Expires in " + days.Value + " days";
            }
        }

        public string ExpiryDateText
        {
            get
            {
                return ExpiryDate.HasValue
                    ? ExpiryDate.Value.ToString("dd MMM yyyy")
                    : "—";
            }
        }

        // One-line label for the native <select> fallback.
        public string OptionLabel
        {
            get
            {
                var label = KindShortLabel + " · " + Title;
                if (!string.IsNullOrWhiteSpace(Subtitle))
                    label += " (" + Subtitle + ")";
                return label + " — " + ExpiryStatus + " · " + ExpiryText;
            }
        }
    }

    // Everything the _ClipPicker partial needs. The picker renders a real
    // <select> and progressively enhances it, so the form still submits when
    // JavaScript is unavailable.
    public class ClipPickerViewModel
    {
        // Name/id of the posted field, e.g. "ClipItemKey".
        public string FieldName { get; set; } = "ClipItemKey";

        public string SelectedKey { get; set; }

        public List<ClipItemViewModel> Items { get; set; }
            = new List<ClipItemViewModel>();

        // Optional: URL returning the item list as JSON for another employee.
        // Used on the admin Assign/Edit Task pages, where the list depends on
        // which employee the task is assigned to.
        public string ReloadUrl { get; set; }

        // Optional: id of the employee <select> that triggers a reload.
        public string ReloadTriggerId { get; set; }
    }
}
