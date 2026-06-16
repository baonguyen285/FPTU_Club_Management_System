using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Collections.Concurrent;
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
        private static readonly ConcurrentDictionary<string, string> _resetCodes = new();

        public AuthController(AuthDbContext context, IJwtService jwtService)
        {
            _context = context;
            _jwtService = jwtService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (await _context.Users.AnyAsync(u => u.Email == request.Email))
            {
                throw new BadRequestException("Email already exists");
            }
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = request.Email,
                FullName = request.FullName,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                Role = "Student",
                IsActive = true
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var responseData = new UserResponse
            {
                Id = user.Id,
                Email = user.Email,
                FullName = user.FullName,
                Role = user.Role,
                IsActive = user.IsActive
            };

            return StatusCode(201, new ApiResponse<UserResponse>(responseData, "Account registered successfully", 201));
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var user = await _context.Users.SingleOrDefaultAsync(u => u.Email == request.Email);
            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                throw new UnauthorizedException("Invalid credentials");
            }

            if (!user.IsActive)
            {
                throw new BadRequestException("Account is deactivated");
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
                IsActive = user.IsActive
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
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (user == null)
            {
                throw new NotFoundException("User with this email not found");
            }

            var random = new Random();
            var code = random.Next(100000, 999999).ToString();
            
            _resetCodes[request.Email] = code;

            var responseMessage = $"Reset code generated successfully. For testing purposes, your code is: {code}";
            return Ok(new ApiResponse<string>(code, responseMessage));
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            if (!_resetCodes.TryGetValue(request.Email, out var storedCode) || storedCode != request.ResetCode)
            {
                throw new BadRequestException("Invalid or expired reset code");
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (user == null)
            {
                throw new NotFoundException("User not found");
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            _context.Users.Update(user);
            
            _resetCodes.TryRemove(request.Email, out _);

            await _context.SaveChangesAsync();

            return Ok(new ApiResponse<object>(null, "Password reset successfully"));
        }
    }
}
