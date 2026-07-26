using Shared.Kernel.Domain;

namespace Report.Domain.Entities
{
    public class KpiRule : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public Guid SemesterId { get; set; }
        public string Description { get; set; } = string.Empty;
        public int MaxPoints { get; set; }
        public decimal Weight { get; set; } = 1;
    }
}
