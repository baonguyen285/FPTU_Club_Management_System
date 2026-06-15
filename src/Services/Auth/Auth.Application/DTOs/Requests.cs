using System.ComponentModel.DataAnnotations;

namespace Auth.Application.DTOs
{
    public class RegisterRequest
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        [RegularExpression(@"^[a-zA-Z0-9._%+-]+@fpt\.edu\.vn$", ErrorMessage = "Email must be an FPT University email ending with @fpt.edu.vn")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters long")]
        public string Password { get; set; }

        [Required(ErrorMessage = "Confirm password is required")]
        [Compare("Password", ErrorMessage = "Password and Confirm Password do not match")]
        public string ConfirmPassword { get; set; }

        [Required(ErrorMessage = "Full name is required")]
        public string FullName { get; set; }

        public string Role { get; set; } = "Student";
    }

    public class LoginRequest
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }

    public class RefreshTokenRequest
    {
        public string RefreshToken { get; set; }
    }

    public class ChangePasswordRequest
    {
        [Required(ErrorMessage = "Old password is required")]
        public string OldPassword { get; set; }

        [Required(ErrorMessage = "New password is required")]
        [MinLength(6, ErrorMessage = "New password must be at least 6 characters long")]
        public string NewPassword { get; set; }

        [Required(ErrorMessage = "Confirm new password is required")]
        [Compare("NewPassword", ErrorMessage = "New password and Confirm new password do not match")]
        public string ConfirmNewPassword { get; set; }
    }

    public class ForgotPasswordRequest
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        [RegularExpression(@"^[a-zA-Z0-9._%+-]+@fpt\.edu\.vn$", ErrorMessage = "Email must be an FPT University email ending with @fpt.edu.vn")]
        public string Email { get; set; }
    }

    public class ResetPasswordRequest
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        [RegularExpression(@"^[a-zA-Z0-9._%+-]+@fpt\.edu\.vn$", ErrorMessage = "Email must be an FPT University email ending with @fpt.edu.vn")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Reset code is required")]
        public string ResetCode { get; set; }

        [Required(ErrorMessage = "New password is required")]
        [MinLength(6, ErrorMessage = "New password must be at least 6 characters long")]
        public string NewPassword { get; set; }

        [Required(ErrorMessage = "Confirm new password is required")]
        [Compare("NewPassword", ErrorMessage = "New password and Confirm new password do not match")]
        public string ConfirmNewPassword { get; set; }
    }
}
