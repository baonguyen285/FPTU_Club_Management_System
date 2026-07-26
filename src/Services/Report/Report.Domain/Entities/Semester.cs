using Report.Domain.Enums;
using Shared.Kernel.Domain;

namespace Report.Domain.Entities;

public sealed class Semester : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public SemesterStatus Status { get; set; } = SemesterStatus.Draft;
}
