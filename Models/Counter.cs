using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WaitWise.Models
{
    public class Counter
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Name { get; set; } = string.Empty; // e.g. "Counter 1", "Desk A"

        [Required]
        public int DepartmentId { get; set; }
        public virtual Department? Department { get; set; }

        [StringLength(450)]
        public string? AssignedStaffId { get; set; }
        public virtual ApplicationUser? AssignedStaff { get; set; }

        public bool IsActive { get; set; } = true;

        public int? CurrentTicketId { get; set; }
        [ForeignKey("CurrentTicketId")]
        public virtual QueueTicket? CurrentTicket { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual ICollection<QueueTicket> TicketsServed { get; set; } = new List<QueueTicket>();
        public virtual ICollection<ServiceLog> ServiceLogs { get; set; } = new List<ServiceLog>();
    }
}
