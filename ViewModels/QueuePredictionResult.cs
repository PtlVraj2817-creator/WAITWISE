namespace WaitWise.ViewModels
{
    public class QueuePredictionResult
    {
        public int TicketId { get; set; }
        public string TicketNumber { get; set; } = string.Empty;
        public int DepartmentId { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
        public int PeopleAhead { get; set; }
        public int ActiveCounters { get; set; }
        public double AverageServiceDurationMinutes { get; set; }
        public int EstimatedWaitMinutes { get; set; }
        public DateTime EstimatedCallTime { get; set; }
        public string CongestionLevel { get; set; } = "Normal"; // "Normal", "Moderate", "Peak Rush"
        public string CongestionBadgeClass { get; set; } = "bg-success"; // Bootstrap CSS class
        public string StatusMessage { get; set; } = string.Empty;
    }
}
