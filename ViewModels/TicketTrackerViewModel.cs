using WaitWise.Models.Enums;

namespace WaitWise.ViewModels
{
    public class TicketTrackerViewModel
    {
        public bool HasTicket => TicketId > 0;
        public int TicketId { get; set; }
        public string TicketNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public int DepartmentId { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
        public TicketStatus Status { get; set; }
        public string? CounterName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CalledAt { get; set; }
        public DateTime? CompletedAt { get; set; }

        public QueuePredictionResult? Prediction { get; set; }
        public string SearchQuery { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
    }
}
