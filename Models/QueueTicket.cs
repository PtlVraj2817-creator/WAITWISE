using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using WaitWise.Models.Enums;

namespace WaitWise.Models
{
    public class QueueTicket
    {
        public int Id { get; set; }

        [Required]
        [StringLength(20)]
        public string TicketNumber { get; set; } = string.Empty; // e.g. A-101

        public int DailySequence { get; set; } // Sequence number for the day (e.g. 101)

        [Required]
        [StringLength(100)]
        public string CustomerName { get; set; } = string.Empty;

        [StringLength(20)]
        public string? CustomerPhone { get; set; }

        [Required]
        public int DepartmentId { get; set; }
        public virtual Department? Department { get; set; }

        public int? CounterId { get; set; }
        [ForeignKey("CounterId")]
        public virtual Counter? Counter { get; set; }

        public TicketStatus Status { get; set; } = TicketStatus.Waiting;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? CalledAt { get; set; }

        public DateTime? CompletedAt { get; set; }

        public int EstimatedWaitMinutes { get; set; } = 0;

        [StringLength(500)]
        public string? Notes { get; set; }
    }
}
