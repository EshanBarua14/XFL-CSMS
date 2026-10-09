using System.ComponentModel.DataAnnotations;
using XFLCSMS.Models.Branch;

namespace XFLCSMS.Models.Admin
{
    /// <summary>The form "Create User" of the administrator. The account is active at once: no token by e-mail.</summary>
    public class NewUserView
    {
        [Required(ErrorMessage = "Please enter the full name.")]
        [StringLength(100)]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter the email.")]
        [EmailAddress(ErrorMessage = "This is not an email address.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter the phone number.")]
        [RegularExpression("^[0-9+-]+$", ErrorMessage = "Digits, + and - only.")]
        [Display(Name = "Phone Number")]
        public string PhonNumber { get; set; } = string.Empty;

        [StringLength(100)]
        public string? Designation { get; set; }

        /// <summary>The branch decides the brokerage house too.</summary>
        [Range(1, int.MaxValue, ErrorMessage = "Please select the brokerage house and branch.")]
        [Display(Name = "Brokerage House and Branch")]
        public int Branch { get; set; }

        [Required(ErrorMessage = "Please enter the employee ID.")]
        [StringLength(15, MinimumLength = 1, ErrorMessage = "1 to 15 characters.")]
        [Display(Name = "Employee ID")]
        public string EmployeeId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter the user name.")]
        [StringLength(25, MinimumLength = 5, ErrorMessage = "5 to 25 characters.")]
        [RegularExpression(@"^[A-Za-z0-9._-]+$", ErrorMessage = "Letters, digits, dot, dash and underscore only.")]
        [Display(Name = "User Name")]
        public string UserName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter a password.")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "At least 6 characters.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please repeat the password.")]
        [Compare(nameof(Password), ErrorMessage = "The two passwords are not the same.")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm Password")]
        public string ConfirmPassword { get; set; } = string.Empty;

        /// <summary>Admin, Support Maneger, Support Engineer or Maker (the values the sign-in looks at).</summary>
        [Required]
        public string Role { get; set; } = "Maker";

        /// <summary>For the drop-down: every branch with the name of its house.</summary>
        public List<(int BranchId, string Label)> Branches { get; set; } = new();
    }
}
