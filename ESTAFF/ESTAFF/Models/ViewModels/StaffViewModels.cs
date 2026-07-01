using System;
using System.ComponentModel.DataAnnotations;

namespace ESTAFF.Models.ViewModels
{
    public class CreateStaffViewModel
    {
        [Required(ErrorMessage = "Full name is required.")]
        [StringLength(256)]
        [Display(Name = "Full Name")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email address.")]
        [Display(Name = "Email Address")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Password is required.")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = " Password must be at least 6 characters")]
        [DataType(DataType.Password)]
        [Display(Name = "Temporary Password")]
        public string Password { get; set; }

        [Required(ErrorMessage = "Role is required.")]
        [Display(Name = "Role")]
        public string Role { get; set; }

        [StringLength(100)]
        [Display(Name = "Department")]
        public string Department { get; set; }

        [Required(ErrorMessage = "Hire date is required.")]
        [DataType(DataType.Date)]
        [Display(Name = "Hire Date")]
        public DateTime HireDate { get; set; } = DateTime.Today;
    }
}