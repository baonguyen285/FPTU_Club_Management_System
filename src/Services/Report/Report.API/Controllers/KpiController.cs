using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Report.Domain.Entities;
using Report.Domain.Enums;
using Report.Infrastructure.Persistence;
using Shared.Kernel.Exceptions;
using Shared.Kernel.Responses;
using System.ComponentModel.DataAnnotations;

namespace Report.API.Controllers
{
    [ApiController]
    [Route("api/v1/kpi")]
    public class KpiController : ControllerBase
    {
        private readonly ReportDbContext _context;

        public KpiController(ReportDbContext context)
        {
            _context = context;
        }

        [HttpGet("leaderboard")]
        public async Task<IActionResult> GetLeaderboard([FromQuery] string? semester)
        {
            var approvedReports = await _context.Reports
                .AsNoTracking()
                .Where(r => r.IsActive && r.Status == ReportStatus.Approved)
                .GroupBy(r => r.ClubId)
                .Select(g => new
                {
                    clubId = g.Key,
                    totalPoints = g.Count() * 10,
                    approvedReports = g.Count(),
                    semester
                })
                .OrderByDescending(x => x.totalPoints)
                .ToListAsync();

            var ranked = approvedReports
                .Select((item, index) => new
                {
                    rank = index + 1,
                    item.clubId,
                    item.totalPoints,
                    item.approvedReports,
                    item.semester
                })
                .ToList();

            return Ok(new ApiResponse<object>(ranked, "KPI leaderboard fetched successfully."));
        }

        [HttpGet("rules")]
        public async Task<IActionResult> GetRules()
        {
            var rules = await _context.KpiRules
                .AsNoTracking()
                .Where(r => r.IsActive)
                .OrderBy(r => r.Name)
                .ToListAsync();

            return Ok(new ApiResponse<object>(rules, "KPI rules fetched successfully."));
        }

        [Authorize(Roles = "Admin,Advisor")]
        [HttpPost("rules")]
        public async Task<IActionResult> CreateRule([FromBody] KpiRuleRequest request)
        {
            var rule = new KpiRule
            {
                Id = Guid.NewGuid(),
                Name = request.Name.Trim(),
                Description = request.Description.Trim(),
                MaxPoints = request.MaxPoints,
                Weight = request.Weight
            };

            _context.KpiRules.Add(rule);
            await _context.SaveChangesAsync();

            return StatusCode(201, new ApiResponse<object>(rule, "KPI rule created successfully."));
        }

        [Authorize(Roles = "Admin,Advisor")]
        [HttpPut("rules/{id}")]
        public async Task<IActionResult> UpdateRule(Guid id, [FromBody] KpiRuleRequest request)
        {
            var rule = await _context.KpiRules.FirstOrDefaultAsync(r => r.Id == id && r.IsActive);
            if (rule == null)
            {
                throw new NotFoundException("KPI rule not found.");
            }

            rule.Name = request.Name.Trim();
            rule.Description = request.Description.Trim();
            rule.MaxPoints = request.MaxPoints;
            rule.Weight = request.Weight;
            rule.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new ApiResponse<object>(rule, "KPI rule updated successfully."));
        }

        [Authorize(Roles = "Admin,Advisor")]
        [HttpDelete("rules/{id}")]
        public async Task<IActionResult> DeleteRule(Guid id)
        {
            var rule = await _context.KpiRules.FirstOrDefaultAsync(r => r.Id == id && r.IsActive);
            if (rule == null)
            {
                throw new NotFoundException("KPI rule not found.");
            }

            rule.IsActive = false;
            rule.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new ApiResponse<object>(null, "KPI rule deleted successfully."));
        }
    }

    public class KpiRuleRequest
    {
        [Required]
        [StringLength(150, MinimumLength = 2)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        [Range(1, 1000)]
        public int MaxPoints { get; set; }

        [Range(0.1, 10)]
        public decimal Weight { get; set; } = 1;
    }
}
