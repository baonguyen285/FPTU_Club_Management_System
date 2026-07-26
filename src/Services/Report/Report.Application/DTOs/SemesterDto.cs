using System.ComponentModel.DataAnnotations;

namespace Report.Application.DTOs;

public sealed class CreateSemesterRequest
{
    [Required, StringLength(30, MinimumLength = 2)]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(150, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }
}

public sealed class UpdateSemesterRequest
{
    [Required, StringLength(150, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }
}

public sealed record SemesterDto(
    Guid Id,
    string Code,
    string Name,
    DateTime StartDate,
    DateTime EndDate,
    string Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
