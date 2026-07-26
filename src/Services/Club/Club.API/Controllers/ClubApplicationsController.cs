using Club.Application.DTOs;
using Club.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Exceptions;
using Shared.Kernel.Responses;
using Shared.Kernel.Security;

namespace Club.API.Controllers;

[ApiController]
[Route("api/v1/club-applications")]
[Authorize]
public sealed class ClubApplicationsController : ControllerBase
{
    private readonly IClubApplicationService _service;
    public ClubApplicationsController(IClubApplicationService service) => _service = service;

    [HttpPost]
    [Authorize(Roles = SystemRoleNames.Student)]
    public async Task<IActionResult> Create(CreateClubApplicationRequest request, CancellationToken token) =>
        Ok(new ApiResponse<object>(await _service.CreateAsync(GetActorId(), request, token),
            "Club application submitted successfully."));

    [HttpGet("mine")]
    [Authorize(Roles = SystemRoleNames.Student)]
    public async Task<IActionResult> Mine(CancellationToken token) =>
        Ok(new ApiResponse<object>(await _service.GetMineAsync(GetActorId(), token),
            "Retrieved own club applications."));

    [HttpGet]
    [Authorize(Roles = SystemRoleNames.StudentAffairsAdmin)]
    public async Task<IActionResult> All(CancellationToken token) =>
        Ok(new ApiResponse<object>(await _service.GetAllAsync(token),
            "Retrieved club applications."));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken token) =>
        Ok(new ApiResponse<object>(await _service.GetAsync(id, GetActorId(),
            User.IsInRole(SystemRoleNames.StudentAffairsAdmin), token), "Retrieved club application."));

    [HttpPut("{id:guid}")]
    [Authorize(Roles = SystemRoleNames.Student)]
    public async Task<IActionResult> Update(Guid id, UpdateClubApplicationRequest request, CancellationToken token) =>
        Ok(new ApiResponse<object>(await _service.UpdateAsync(id, GetActorId(), request, token),
            "Club application updated."));

    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = SystemRoleNames.StudentAffairsAdmin)]
    public async Task<IActionResult> Approve(Guid id, CancellationToken token) =>
        Ok(new ApiResponse<object>(await _service.ApproveAsync(id, GetActorId(), token),
            "Club application approved and leader bootstrapped."));

    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = SystemRoleNames.StudentAffairsAdmin)]
    public async Task<IActionResult> Reject(Guid id, RejectClubApplicationRequest request, CancellationToken token) =>
        Ok(new ApiResponse<object>(await _service.RejectAsync(id, GetActorId(), request.Reason, token),
            "Club application rejected."));

    private Guid GetActorId()
    {
        var value = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
        return Guid.TryParse(value, out var id) ? id : throw new UnauthorizedException("Invalid actor identity.");
    }
}
