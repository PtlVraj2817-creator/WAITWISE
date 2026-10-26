using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WaitWise.Models
{
    public class ServiceLog
    {
        public int Id { get; set; }

        public int? TicketId { get; set; }
        public virtual QueueTicket? Ticket { get; set; }

        [Required]
        public int DepartmentId { get; set; }
        public virtual Department? Department { get; set; }

        public int? CounterId { get; set; }
        public virtual Counter? Counter { get; set; }

        [StringLength(450)]
        public string? StaffUserId { get; set; }

        public double DurationMinutes { get; set; } // Time taken to complete service

        public DateTime CompletedAt { get; set; } = DateTime.UtcNow;

        public DayOfWeek DayOfWeek { get; set; }

        public int HourOfDay { get; set; }
    }
}
