using Microsoft.AspNetCore.Mvc.Rendering;
using WaitWise.Models.Enums;

namespace WaitWise.ViewModels
{
    public class StaffDeskViewModel
    {
        public int CounterId { get; set; }
        public string CounterName { get; set; } = string.Empty;
        public int DepartmentId { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
        public string DepartmentCodePrefix { get; set; } = string.Empty;
        public string StaffName { get; set; } = string.Empty;
        public string StaffEmail { get; set; } = string.Empty;

        public List<SelectListItem> AvailableCounters { get; set; } = new List<SelectListItem>();

        public StaffCurrentTicketDto? CurrentTicket { get; set; }
        public List<StaffWaitingTicketDto> WaitingTickets { get; set; } = new List<StaffWaitingTicketDto>();

        public int CompletedTodayCount { get; set; }
        public double AverageServiceDurationMinutes { get; set; }
        public string? SuccessMessage { get; set; }
        public string? WarningMessage { get; set; }
    }

    public class StaffCurrentTicketDto
    {
        public int TicketId { get; set; }
        public string TicketNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string? CustomerPhone { get; set; }
        public DateTime CalledAt { get; set; }
        public int ElapsedMinutes => (int)Math.Max(0, (DateTime.UtcNow - CalledAt).TotalMinutes);
    }

    public class StaffWaitingTicketDto
    {
        public int TicketId { get; set; }
        public string TicketNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string? CustomerPhone { get; set; }
        public DateTime CreatedAt { get; set; }
        public int EstimatedWaitMinutes { get; set; }
        public int PositionInLine { get; set; }
    }
}
