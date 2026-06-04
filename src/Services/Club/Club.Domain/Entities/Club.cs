using System;

namespace Club.Domain.Entities
{
    public class Club
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public Guid AdvisorId { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
