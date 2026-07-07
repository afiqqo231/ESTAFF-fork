using System.ComponentModel.DataAnnotations;

namespace ESTAFF.Models.ViewModels
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Employee Number is required.")]
        [Display(Name = "Employee Number")]
        public string EmpNumber { get; set; }

        [Required(ErrorMessage = "Password is required.")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; }

        [Display(Name = "Remember me?")]
        public bool RememberMe { get; set; }
    }
}