using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Report.Domain.Entities;
using Report.Domain.Enums;
using Report.Infrastructure.Persistence;
using Shared.Kernel.Exceptions;
using Shared.Kernel.Responses;
using Shared.Kernel.Security;
using System.ComponentModel.DataAnnotations;
using Report.Application.DTOs;
using Report.Application.Interfaces;

namespace Report.API.Controllers
{
    [ApiController]
    [Route("api/v1/kpi")]
    public class KpiController : ControllerBase
    {
        private readonly ReportDbContext _context;
        private readonly ISemesterService _semesterService;

        public KpiController(ReportDbContext context, ISemesterService semesterService)
        {
            _context = context;
            _semesterService = semesterService;
        }

        [HttpGet("leaderboard")]
        public async Task<IActionResult> GetLeaderboard([FromQuery] Guid semesterId, CancellationToken cancellationToken)
        {
            if (semesterId == Guid.Empty)
                throw new BadRequestException("semesterId is required.");
            if (!await _context.Semesters.AnyAsync(x => x.Id == semesterId && x.IsActive, cancellationToken))
                throw new NotFoundException("Semester not found.");

            var scores = await _context.KpiScoreHistories
                .AsNoTracking()
                .Where(x => x.IsActive && x.SemesterId == semesterId)
                .GroupBy(x => x.ClubId)
                .Select(g => new
                {
                    clubId = g.Key,
                    totalPoints = g.Sum(x => x.Points)
                })
                .OrderByDescending(x => x.totalPoints).ThenBy(x => x.clubId)
                .ToListAsync(cancellationToken);

            var ranked = scores
                .Select((item, index) => new
                {
                    rank = index + 1,
                    item.clubId,
                    clubName = item.clubId.ToString(),
                    semesterId,
                    item.totalPoints,
                })
                .ToList();

            return Ok(new ApiResponse<object>(ranked, "KPI leaderboard fetched successfully."));
        }

        [HttpGet("rules")]
        public async Task<IActionResult> GetRules([FromQuery] Guid? semesterId)
        {
            var rules = await _context.KpiRules
                .AsNoTracking()
                .Where(r => r.IsActive)
                .Where(r => !semesterId.HasValue || r.SemesterId == semesterId)
                .OrderBy(r => r.Name)
                .ToListAsync();

            return Ok(new ApiResponse<object>(rules, "KPI rules fetched successfully."));
        }

        [Authorize(Roles = SystemRoleNames.StudentAffairsAdmin)]
        [HttpPost("rules")]
        public async Task<IActionResult> CreateRule([FromBody] KpiRuleRequest request)
        {
            var semester = await _context.Semesters.SingleOrDefaultAsync(x => x.Id == request.SemesterId && x.IsActive)
                ?? throw new NotFoundException("Semester not found.");
            if (semester.Status == SemesterStatus.Closed)
                throw new ConflictException("Closed semesters cannot be changed.");
            if (await _context.KpiRules.AnyAsync(x => x.SemesterId == request.SemesterId && x.Name == request.Name.Trim() && x.IsActive))
                throw new ConflictException("A KPI rule with this name already exists in the semester.");

            var rule = new KpiRule
            {
                Id = Guid.NewGuid(),
                SemesterId = request.SemesterId,
                Name = request.Name.Trim(),
                Description = request.Description.Trim(),
                MaxPoints = request.MaxPoints,
                Weight = request.Weight
            };

            _context.KpiRules.Add(rule);
            await _context.SaveChangesAsync();

            return StatusCode(201, new ApiResponse<object>(rule, "KPI rule created successfully."));
        }

        [Authorize(Roles = SystemRoleNames.StudentAffairsAdmin)]
        [HttpPut("rules/{id}")]
        public async Task<IActionResult> UpdateRule(Guid id, [FromBody] KpiRuleRequest request)
        {
            var rule = await _context.KpiRules.FirstOrDefaultAsync(r => r.Id == id && r.IsActive);
            if (rule == null)
            {
                throw new NotFoundException("KPI rule not found.");
            }
            var semester = await _context.Semesters.SingleOrDefaultAsync(x => x.Id == rule.SemesterId && x.IsActive)
                ?? throw new NotFoundException("Semester not found.");
            if (semester.Status == SemesterStatus.Closed)
                throw new ConflictException("Closed semesters cannot be changed.");
            if (await _context.KpiRules.AnyAsync(x => x.Id != id && x.SemesterId == rule.SemesterId && x.Name == request.Name.Trim() && x.IsActive))
                throw new ConflictException("A KPI rule with this name already exists in the semester.");

            rule.Name = request.Name.Trim();
            rule.Description = request.Description.Trim();
            rule.MaxPoints = request.MaxPoints;
            rule.Weight = request.Weight;
            rule.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new ApiResponse<object>(rule, "KPI rule updated successfully."));
        }

        [Authorize(Roles = SystemRoleNames.StudentAffairsAdmin)]
        [HttpDelete("rules/{id}")]
        public async Task<IActionResult> DeleteRule(Guid id)
        {
            var rule = await _context.KpiRules.FirstOrDefaultAsync(r => r.Id == id && r.IsActive);
            if (rule == null)
            {
                throw new NotFoundException("KPI rule not found.");
            }
            var semester = await _context.Semesters.SingleOrDefaultAsync(x => x.Id == rule.SemesterId && x.IsActive)
                ?? throw new NotFoundException("Semester not found.");
            if (semester.Status == SemesterStatus.Closed)
                throw new ConflictException("Closed semesters cannot be changed.");

            rule.IsActive = false;
            rule.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new ApiResponse<object>(null, "KPI rule deleted successfully."));
        }

        [Authorize(Roles = SystemRoleNames.StudentAffairsAdmin)]
        [HttpPost("adjustments")]
        public async Task<IActionResult> CreateAdjustment(
            [FromBody] KpiAdjustmentRequest request,
            CancellationToken cancellationToken)
        {
            var semester = await _context.Semesters.SingleOrDefaultAsync(
                x => x.Id == request.SemesterId && x.IsActive, cancellationToken)
                ?? throw new NotFoundException("Semester not found.");
            if (semester.Status == SemesterStatus.Closed)
                throw new ConflictException("Closed semesters cannot be changed.");
            if (request.Points == 0)
                throw new BadRequestException("Points must not be zero.");
            if (request.RuleId.HasValue && !await _context.KpiRules.AnyAsync(
                x => x.Id == request.RuleId && x.SemesterId == request.SemesterId && x.IsActive, cancellationToken))
                throw new NotFoundException("KPI rule not found in the semester.");

            var history = new KpiScoreHistory
            {
                Id = Guid.NewGuid(),
                ClubId = request.ClubId,
                SemesterId = request.SemesterId,
                RuleId = request.RuleId,
                Points = request.Points,
                Reason = request.Reason.Trim(),
                SourceType = "ManualAdjustment",
                AdjustedBy = GetActorId()
            };
            _context.KpiScoreHistories.Add(history);
            await _context.SaveChangesAsync(cancellationToken);
            return StatusCode(201, new ApiResponse<object>(history, "KPI adjustment recorded."));
        }

        [HttpGet("clubs/{clubId:guid}")]
        public async Task<IActionResult> GetClubScore(Guid clubId, [FromQuery] Guid semesterId, CancellationToken cancellationToken)
        {
            var total = await _context.KpiScoreHistories.AsNoTracking()
                .Where(x => x.IsActive && x.ClubId == clubId && x.SemesterId == semesterId)
                .SumAsync(x => x.Points, cancellationToken);
            return Ok(new ApiResponse<object>(new { clubId, semesterId, totalPoints = total }, "Club KPI score fetched."));
        }

        [HttpGet("clubs/{clubId:guid}/history")]
        public async Task<IActionResult> GetClubHistory(Guid clubId, [FromQuery] Guid semesterId, CancellationToken cancellationToken)
        {
            var history = await _context.KpiScoreHistories.AsNoTracking()
                .Where(x => x.IsActive && x.ClubId == clubId && x.SemesterId == semesterId)
                .OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
                .ToListAsync(cancellationToken);
            return Ok(new ApiResponse<object>(history, "Club KPI history fetched."));
        }

        [HttpGet("semesters")]
        public async Task<IActionResult> GetSemesters(CancellationToken cancellationToken)
        {
            var result = await _semesterService.GetAllAsync(cancellationToken);
            return Ok(new ApiResponse<object>(result, "Semesters fetched successfully."));
        }

        [HttpGet("semesters/{id:guid}")]
        public async Task<IActionResult> GetSemester(Guid id, CancellationToken cancellationToken)
        {
            var result = await _semesterService.GetByIdAsync(id, cancellationToken);
            return Ok(new ApiResponse<object>(result, "Semester fetched successfully."));
        }

        [Authorize(Roles = SystemRoleNames.StudentAffairsAdmin)]
        [HttpPost("semesters")]
        public async Task<IActionResult> CreateSemester(
            [FromBody] CreateSemesterRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _semesterService.CreateAsync(request, GetActorId(), cancellationToken);
            return StatusCode(201, new ApiResponse<object>(result, "Semester created successfully."));
        }

        [Authorize(Roles = SystemRoleNames.StudentAffairsAdmin)]
        [HttpPut("semesters/{id:guid}")]
        public async Task<IActionResult> UpdateSemester(
            Guid id,
            [FromBody] UpdateSemesterRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _semesterService.UpdateAsync(id, request, GetActorId(), cancellationToken);
            return Ok(new ApiResponse<object>(result, "Semester updated successfully."));
        }

        [Authorize(Roles = SystemRoleNames.StudentAffairsAdmin)]
        [HttpPost("semesters/{id:guid}/activate")]
        public async Task<IActionResult> ActivateSemester(Guid id, CancellationToken cancellationToken)
        {
            var result = await _semesterService.ActivateAsync(id, GetActorId(), cancellationToken);
            return Ok(new ApiResponse<object>(result, "Semester activated successfully."));
        }

        [Authorize(Roles = SystemRoleNames.StudentAffairsAdmin)]
        [HttpPost("semesters/{id:guid}/close")]
        public async Task<IActionResult> CloseSemester(Guid id, CancellationToken cancellationToken)
        {
            var result = await _semesterService.CloseAsync(id, GetActorId(), cancellationToken);
            return Ok(new ApiResponse<object>(result, "Semester closed successfully."));
        }

        private Guid GetActorId()
        {
            var value = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
            return Guid.TryParse(value, out var actorId)
                ? actorId
                : throw new UnauthorizedException("Invalid actor identity.");
        }
    }

    public class KpiRuleRequest
    {
        [Required]
        public Guid SemesterId { get; set; }
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

    public sealed class KpiAdjustmentRequest
    {
        [Required]
        public Guid ClubId { get; set; }
        [Required]
        public Guid SemesterId { get; set; }
        public Guid? RuleId { get; set; }
        [Range(typeof(decimal), "-1000", "1000")]
        public decimal Points { get; set; }
        [Required, StringLength(500, MinimumLength = 3)]
        public string Reason { get; set; } = string.Empty;
    }
}
