using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Auth.Domain.Entities;
using Auth.Application.DTOs;
using Auth.Application.Services;
using Auth.Infrastructure.Persistence;
using Shared.Kernel.Responses;
using Shared.Kernel.Exceptions;

namespace Auth.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AuthDbContext _context;
        private readonly IJwtService _jwtService;
        private readonly IEmailSender _emailSender;

        public AuthController(AuthDbContext context, IJwtService jwtService, IEmailSender emailSender)
        {
            _context = context;
            _jwtService = jwtService;
            _emailSender = emailSender;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            var email = NormalizeEmail(request.Email);
            var existingUser = await _context.Users.SingleOrDefaultAsync(u => u.Email == email);
            if (existingUser != null)
            {
                if (!existingUser.IsEmailVerified)
                {
                    await IssueVerificationCodeAsync(existingUser);
                    return Ok(new ApiResponse<object>(new { requiresEmailVerification = true }, "Email is already registered but not verified. A new verification email has been sent."));
                }

                throw new BadRequestException("Email is already registered. Please login or use forgot password.");
            }

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                FullName = request.FullName,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                Role = "Student",
                IsActive = true,
                IsEmailVerified = false
            };

            _context.Users.Add(user);
            await IssueVerificationCodeAsync(user, saveChanges: false);
            await _context.SaveChangesAsync();

            var responseData = new UserResponse
            {
                Id = user.Id,
                Email = user.Email,
                FullName = user.FullName,
                Role = user.Role,
                IsActive = user.IsActive,
                IsEmailVerified = user.IsEmailVerified
            };

            return StatusCode(201, new ApiResponse<UserResponse>(responseData, "Account registered successfully. Please verify your email."));
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var email = NormalizeEmail(request.Email);
            var user = await _context.Users.SingleOrDefaultAsync(u => u.Email == email);
            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                throw new UnauthorizedException("Invalid credentials");
            }

            if (!user.IsActive)
            {
                throw new BadRequestException("Account is deactivated");
            }

            if (!user.IsEmailVerified)
            {
                await IssueVerificationCodeAsync(user);
                throw new BadRequestException("Email is not verified. A new verification email has been sent.");
            }

            var accessToken = _jwtService.GenerateAccessToken(user);
            var refreshTokenString = _jwtService.GenerateRefreshToken();

            var refreshToken = new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Token = refreshTokenString,
                ExpiresAt = DateTime.UtcNow.AddDays(7)
            };

            _context.RefreshTokens.Add(refreshToken);
            await _context.SaveChangesAsync();

            var responseData = new LoginResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshTokenString,
                ExpiresIn = 3600 // 1 hour
            };

            return Ok(new ApiResponse<LoginResponse>(responseData, "Logged in successfully"));
        }

        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            var storedToken = await _context.RefreshTokens
                .SingleOrDefaultAsync(t => t.Token == request.RefreshToken);

            if (storedToken == null || storedToken.IsExpired)
            {
                if (storedToken != null)
                {
                    _context.RefreshTokens.Remove(storedToken);
                    await _context.SaveChangesAsync();
                }
                throw new UnauthorizedException("Invalid or expired refresh token");
            }

            var user = await _context.Users.FindAsync(storedToken.UserId);
            if (user == null || !user.IsActive)
            {
                throw new UnauthorizedException("User no longer exists or is deactivated");
            }

            var newAccessToken = _jwtService.GenerateAccessToken(user);
            var newRefreshTokenString = _jwtService.GenerateRefreshToken();

            _context.RefreshTokens.Remove(storedToken);

            var newRefreshToken = new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Token = newRefreshTokenString,
                ExpiresAt = DateTime.UtcNow.AddDays(7)
            };

            _context.RefreshTokens.Add(newRefreshToken);
            await _context.SaveChangesAsync();

            var responseData = new LoginResponse
            {
                AccessToken = newAccessToken,
                RefreshToken = newRefreshTokenString,
                ExpiresIn = 3600
            };

            return Ok(new ApiResponse<LoginResponse>(responseData, "Token refreshed successfully"));
        }

        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
        {
            var storedToken = await _context.RefreshTokens
                .SingleOrDefaultAsync(t => t.Token == request.RefreshToken);

            if (storedToken != null)
            {
                _context.RefreshTokens.Remove(storedToken);
                await _context.SaveChangesAsync();
            }

            return Ok(new ApiResponse<object>(null, "Logged out successfully"));
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> Me()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                throw new UnauthorizedException("Invalid identity token");
            }

            var user = await _context.Users.FindAsync(Guid.Parse(userIdClaim.Value));
            if (user == null)
            {
                throw new NotFoundException("User not found");
            }

            var responseData = new UserResponse
            {
                Id = user.Id,
                Email = user.Email,
                FullName = user.FullName,
                Role = user.Role,
                IsActive = user.IsActive,
                IsEmailVerified = user.IsEmailVerified
            };

            return Ok(new ApiResponse<UserResponse>(responseData, "Get profile details successfully"));
        }

        [Authorize]
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                throw new UnauthorizedException("Invalid identity token");
            }

            var userId = Guid.Parse(userIdClaim.Value);
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                throw new NotFoundException("User not found");
            }

            if (!BCrypt.Net.BCrypt.Verify(request.OldPassword, user.PasswordHash))
            {
                throw new BadRequestException("Incorrect old password");
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            return Ok(new ApiResponse<object>(null, "Password changed successfully"));
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            var email = NormalizeEmail(request.Email);
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null)
            {
                throw new NotFoundException("User with this email not found");
            }

            var code = GenerateSixDigitCode();
            user.ResetPasswordCode = code;
            user.ResetPasswordCodeExpiresAt = DateTime.UtcNow.AddMinutes(15);
            await _emailSender.SendPasswordResetCodeAsync(user.Email, user.FullName, code);
            await _context.SaveChangesAsync();

            return Ok(new ApiResponse<object>(new { email = user.Email }, "Password reset code has been sent to your email."));
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            var email = NormalizeEmail(request.Email);
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null)
            {
                throw new NotFoundException("User not found");
            }

            if (user.ResetPasswordCode != request.ResetCode ||
                user.ResetPasswordCodeExpiresAt == null ||
                user.ResetPasswordCodeExpiresAt < DateTime.UtcNow)
            {
                throw new BadRequestException("Invalid or expired reset code");
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            user.ResetPasswordCode = null;
            user.ResetPasswordCodeExpiresAt = null;
            _context.Users.Update(user);

            await _context.SaveChangesAsync();

            return Ok(new ApiResponse<object>(null, "Password reset successfully"));
        }

        [HttpPost("verify-email")]
        public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request)
        {
            var email = NormalizeEmail(request.Email);
            var user = await _context.Users.SingleOrDefaultAsync(u => u.Email == email);
            if (user == null)
            {
                throw new NotFoundException("User not found");
            }

            if (user.IsEmailVerified)
            {
                return Ok(new ApiResponse<object>(new { isEmailVerified = true }, "Email is already verified."));
            }

            if (user.EmailVerificationCode != request.Code ||
                user.EmailVerificationCodeExpiresAt == null ||
                user.EmailVerificationCodeExpiresAt < DateTime.UtcNow)
            {
                throw new BadRequestException("Invalid or expired verification code");
            }

            user.IsEmailVerified = true;
            user.EmailVerificationCode = null;
            user.EmailVerificationCodeExpiresAt = null;
            await _context.SaveChangesAsync();

            return Ok(new ApiResponse<object>(new { isEmailVerified = true }, "Email verified successfully."));
        }

        [HttpPost("resend-verification")]
        [HttpPost("resend-verification-email")]
        public async Task<IActionResult> ResendVerification([FromBody] ResendVerificationEmailRequest request)
        {
            var email = NormalizeEmail(request.Email);
            var user = await _context.Users.SingleOrDefaultAsync(u => u.Email == email);
            if (user == null)
            {
                throw new NotFoundException("User not found");
            }

            if (user.IsEmailVerified)
            {
                throw new BadRequestException("Email is already verified. Please login or use forgot password.");
            }

            await IssueVerificationCodeAsync(user);
            return Ok(new ApiResponse<object>(new { requiresEmailVerification = true }, "Verification email has been resent."));
        }

        private async Task IssueVerificationCodeAsync(User user, bool saveChanges = true)
        {
            var code = GenerateSixDigitCode();
            user.EmailVerificationCode = code;
            user.EmailVerificationCodeExpiresAt = DateTime.UtcNow.AddMinutes(15);
            await _emailSender.SendVerificationCodeAsync(user.Email, user.FullName, code);
            if (saveChanges)
            {
                await _context.SaveChangesAsync();
            }
        }

        private static string GenerateSixDigitCode()
        {
            return Random.Shared.Next(100000, 999999).ToString();
        }

        private static string NormalizeEmail(string email)
        {
            return email.Trim().ToLowerInvariant();
        }
    }
}
