using System.ComponentModel.DataAnnotations;

namespace WaitWise.Models
{
    public class Department
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(250)]
        public string Description { get; set; } = string.Empty;

        [Required]
        [StringLength(5)]
        public string CodePrefix { get; set; } = "A"; // e.g. A, B, C for ticket prefixes (A-101)

        [Range(1, 240)]
        public int DefaultServiceTimeMinutes { get; set; } = 10;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual ICollection<Counter> Counters { get; set; } = new List<Counter>();
        public virtual ICollection<QueueTicket> Tickets { get; set; } = new List<QueueTicket>();
        public virtual ICollection<ServiceLog> ServiceLogs { get; set; } = new List<ServiceLog>();
    }
}
