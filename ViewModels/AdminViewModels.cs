using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using WaitWise.Models;

namespace WaitWise.ViewModels
{
    public class AdminDashboardViewModel
    {
        public int TotalDepartments { get; set; }
        public int TotalCounters { get; set; }
        public int ActiveCounters { get; set; }
        public int TotalWaitingNow { get; set; }
        public int TotalServingNow { get; set; }
        public int TotalCompletedToday { get; set; }
        public int TotalNoShowToday { get; set; }
        public double OverallAvgWaitMinutes { get; set; }

        public List<AdminDepartmentOverviewDto> DepartmentOverviews { get; set; } = new List<AdminDepartmentOverviewDto>();
        public List<AdminRecentLogDto> RecentServiceLogs { get; set; } = new List<AdminRecentLogDto>();
    }

    public class AdminDepartmentOverviewDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string CodePrefix { get; set; } = string.Empty;
        public int DefaultTimeMinutes { get; set; }
        public double DynamicAvgDurationMinutes { get; set; }
        public int WaitingCount { get; set; }
        public int ActiveCountersCount { get; set; }
        public int EstimatedWaitMinutes { get; set; }
        public bool IsActive { get; set; }
    }

    public class AdminRecentLogDto
    {
        public int Id { get; set; }
        public string TicketNumber { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public string CounterName { get; set; } = string.Empty;
        public string StaffName { get; set; } = string.Empty;
        public double DurationMinutes { get; set; }
        public DateTime CompletedAt { get; set; }
    }

    public class DepartmentManageViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Department name is required")]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(250)]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Prefix code is required (e.g. A, B)")]
        [StringLength(5)]
        [Display(Name = "Ticket Prefix (e.g. A, B)")]
        public string CodePrefix { get; set; } = "A";

        [Required]
        [Range(1, 240, ErrorMessage = "Default time must be between 1 and 240 minutes")]
        [Display(Name = "Default Service Time (Minutes)")]
        public int DefaultServiceTimeMinutes { get; set; } = 10;

        public bool IsActive { get; set; } = true;

        public List<Department> ExistingDepartments { get; set; } = new List<Department>();
        public string? SuccessMessage { get; set; }
    }

    public class CounterManageViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Counter name is required (e.g. Counter 4)")]
        [StringLength(50)]
        [Display(Name = "Counter / Desk Name")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select a department")]
        [Display(Name = "Assigned Department")]
        public int DepartmentId { get; set; }

        public bool IsActive { get; set; } = true;

        public List<SelectListItem> DepartmentOptions { get; set; } = new List<SelectListItem>();
        public List<CounterItemDto> ExistingCounters { get; set; } = new List<CounterItemDto>();
        public string? SuccessMessage { get; set; }
    }

    public class CounterItemDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int DepartmentId { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
        public string? StaffName { get; set; }
        public bool IsActive { get; set; }
        public string? CurrentTicketNumber { get; set; }
    }

    public class AnalyticsViewModel
    {
        public int TotalServiceLogsRecorded { get; set; }
        public double OverallAverageDuration { get; set; }
        public List<DepartmentDurationStatsDto> DepartmentStats { get; set; } = new List<DepartmentDurationStatsDto>();
        public List<HourlyLoadStatsDto> HourlyStats { get; set; } = new List<HourlyLoadStatsDto>();
        public List<AdminRecentLogDto> DetailedLogs { get; set; } = new List<AdminRecentLogDto>();
    }

    public class DepartmentDurationStatsDto
    {
        public string DepartmentName { get; set; } = string.Empty;
        public string CodePrefix { get; set; } = string.Empty;
        public int TotalCompleted { get; set; }
        public double AverageMinutes { get; set; }
        public double MinMinutes { get; set; }
        public double MaxMinutes { get; set; }
    }

    public class HourlyLoadStatsDto
    {
        public int HourOfDay { get; set; }
        public string HourFormatted => $"{HourOfDay % 12 switch { 0 => 12, var h => h }}:00 {(HourOfDay >= 12 ? "PM" : "AM")}";
        public int ConsultationCount { get; set; }
    }
}
