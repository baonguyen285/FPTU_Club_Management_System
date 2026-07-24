using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Auth.Application.DTOs;
using Auth.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Exceptions;
using Shared.Kernel.Responses;

namespace Auth.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    [Authorize(Roles = "Admin")]
    public class UsersController : ControllerBase
    {
        private static readonly string[] AllowedRoles = { "Admin", "Advisor", "ClubManager", "Student" };
        private readonly AuthDbContext _context;

        public UsersController(AuthDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? role,
            [FromQuery] bool? isActive,
            [FromQuery] string? search,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 100);
            var query = _context.Users.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(role))
            {
                query = query.Where(u => u.Role == role);
            }

            if (isActive.HasValue)
            {
                query = query.Where(u => u.IsActive == isActive.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var keyword = search.Trim();
                query = query.Where(u => u.FullName.Contains(keyword) || u.Email.Contains(keyword));
            }

            var total = await query.CountAsync();
            var users = await query
                .OrderBy(u => u.FullName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new UserResponse
                {
                    Id = u.Id,
                    Email = u.Email,
                    FullName = u.FullName,
                    Role = u.Role,
                    IsActive = u.IsActive,
                    IsEmailVerified = u.IsEmailVerified
                })
                .ToListAsync();

            return Ok(new ApiResponse<object>(
                users,
                "Users retrieved successfully",
                meta: new
                {
                    total,
                    page,
                    pageSize,
                    totalPages = (int)Math.Ceiling(total / (double)pageSize)
                }));
        }

        [HttpPut("{id:guid}/role")]
        public async Task<IActionResult> UpdateRole(Guid id, [FromBody] UpdateUserRoleRequest request)
        {
            if (!AllowedRoles.Contains(request.Role))
            {
                throw new BadRequestException("Invalid role");
            }

            if (IsCurrentUser(id))
            {
                throw new BadRequestException("You cannot change your own role.");
            }

            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                throw new NotFoundException("User not found");
            }

            user.Role = request.Role;
            await _context.SaveChangesAsync();

            return Ok(new ApiResponse<object>(new { user.Id, user.Role }, "User role updated successfully"));
        }

        [HttpPut("{id:guid}/status")]
        public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateUserStatusRequest request)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                throw new NotFoundException("User not found");
            }

            if (IsCurrentUser(id) && !request.IsActive)
            {
                throw new BadRequestException("You cannot deactivate your own account.");
            }

            user.IsActive = request.IsActive;
            await _context.SaveChangesAsync();

            return Ok(new ApiResponse<object>(new { user.Id, user.IsActive }, "User status updated successfully"));
        }

        private bool IsCurrentUser(Guid id)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(userIdClaim, out var currentUserId) && currentUserId == id;
        }
    }

    public class UpdateUserRoleRequest
    {
        public string Role { get; set; } = string.Empty;
    }

    public class UpdateUserStatusRequest
    {
        public bool IsActive { get; set; }
    }
}
