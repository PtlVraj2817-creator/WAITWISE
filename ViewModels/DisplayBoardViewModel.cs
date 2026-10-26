namespace WaitWise.ViewModels
{
    public class DisplayBoardViewModel
    {
        public List<ServingCounterDto> ServingCounters { get; set; } = new List<ServingCounterDto>();
        public List<DepartmentQueueSummaryDto> DepartmentQueues { get; set; } = new List<DepartmentQueueSummaryDto>();
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }

    public class ServingCounterDto
    {
        public int CounterId { get; set; }
        public string CounterName { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public string? TicketNumber { get; set; }
        public string? StaffName { get; set; }
        public bool IsServing => !string.IsNullOrEmpty(TicketNumber);
        public DateTime? CalledAt { get; set; }
    }

    public class DepartmentQueueSummaryDto
    {
        public int DepartmentId { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
        public string CodePrefix { get; set; } = string.Empty;
        public int WaitingCount { get; set; }
        public int EstimatedWaitMinutes { get; set; }
        public List<string> UpNextTickets { get; set; } = new List<string>();
    }

    public class HomeDashboardViewModel
    {
        public List<DepartmentCardItem> Departments { get; set; } = new List<DepartmentCardItem>();
        public int TotalWaitingToday { get; set; }
        public int TotalServedToday { get; set; }
        public int ActiveCountersCount { get; set; }
        public double AverageWaitTimeMinutes { get; set; }
    }
}
