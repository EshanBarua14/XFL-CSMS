using System.ComponentModel.DataAnnotations;

namespace XFLCSMS.Models.Login
{
    public class ResetPasswordRequest
    {
        [Required]
        public string Token { get; set; } = string.Empty;
        [Required, RegularExpression(XFLCSMS.Models.Register.PasswordRules.Pattern, ErrorMessage = XFLCSMS.Models.Register.PasswordRules.Message)]
        public string Password { get; set; } = string.Empty;
        [Required, Compare("Password", ErrorMessage = "Password and Confirm Password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
