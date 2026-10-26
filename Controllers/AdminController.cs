using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WaitWise.Data;
using WaitWise.Hubs;
using WaitWise.Models;
using WaitWise.Models.Enums;
using WaitWise.Services;
using WaitWise.ViewModels;

namespace WaitWise.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IQueuePredictorService _predictorService;
        private readonly IHubContext<QueueHub> _hubContext;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            ApplicationDbContext context,
            IQueuePredictorService predictorService,
            IHubContext<QueueHub> hubContext,
            ILogger<AdminController> logger)
        {
            _context = context;
            _predictorService = predictorService;
            _hubContext = hubContext;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var today = DateTime.UtcNow.Date;

            var depts = await _context.Departments
                .OrderBy(d => d.Name)
                .ToListAsync();

            var counters = await _context.Counters
                .ToListAsync();

            var todayTickets = await _context.QueueTickets
                .Where(t => t.CreatedAt >= today)
                .ToListAsync();

            var deptOverviews = new List<AdminDepartmentOverviewDto>();

            foreach (var d in depts)
            {
                var waiting = await _context.QueueTickets
                    .CountAsync(t => t.DepartmentId == d.Id && t.Status == TicketStatus.Waiting);

                var activeCounters = counters.Count(c => c.DepartmentId == d.Id && c.IsActive);
                var dynamicAvg = await _predictorService.GetDynamicAverageServiceTimeMinutesAsync(d.Id);
                var waitMinutes = await _predictorService.PredictWaitTimeMinutesAsync(d.Id, waiting);

                deptOverviews.Add(new AdminDepartmentOverviewDto
                {
                    Id = d.Id,
                    Name = d.Name,
                    CodePrefix = d.CodePrefix,
                    DefaultTimeMinutes = d.DefaultServiceTimeMinutes,
                    DynamicAvgDurationMinutes = dynamicAvg,
                    WaitingCount = waiting,
                    ActiveCountersCount = activeCounters,
                    EstimatedWaitMinutes = waitMinutes,
                    IsActive = d.IsActive
                });
            }

            var recentLogs = await _context.ServiceLogs
                .Include(s => s.Department)
                .Include(s => s.Counter)
                .Include(s => s.Ticket)
                .OrderByDescending(s => s.CompletedAt)
                .Take(8)
                .Select(s => new AdminRecentLogDto
                {
                    Id = s.Id,
                    TicketNumber = s.Ticket != null ? s.Ticket.TicketNumber : "N/A",
                    DepartmentName = s.Department != null ? s.Department.Name : "General",
                    CounterName = s.Counter != null ? s.Counter.Name : "Desk",
                    DurationMinutes = s.DurationMinutes,
                    CompletedAt = s.CompletedAt
                })
                .ToListAsync();

            var model = new AdminDashboardViewModel
            {
                TotalDepartments = depts.Count,
                TotalCounters = counters.Count,
                ActiveCounters = counters.Count(c => c.IsActive),
                TotalWaitingNow = await _context.QueueTickets.CountAsync(t => t.Status == TicketStatus.Waiting),
                TotalServingNow = counters.Count(c => c.CurrentTicketId.HasValue),
                TotalCompletedToday = todayTickets.Count(t => t.Status == TicketStatus.Completed),
                TotalNoShowToday = todayTickets.Count(t => t.Status == TicketStatus.NoShow),
                OverallAvgWaitMinutes = deptOverviews.Any() ? Math.Round(deptOverviews.Average(d => d.EstimatedWaitMinutes), 1) : 0,
                DepartmentOverviews = deptOverviews,
                RecentServiceLogs = recentLogs
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Departments(string? success = null)
        {
            var depts = await _context.Departments
                .Include(d => d.Counters)
                .OrderBy(d => d.Name)
                .ToListAsync();

            var model = new DepartmentManageViewModel
            {
                ExistingDepartments = depts,
                SuccessMessage = success
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveDepartment(DepartmentManageViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.ExistingDepartments = await _context.Departments.ToListAsync();
                return View("Departments", model);
            }

            if (model.Id > 0)
            {
                var existing = await _context.Departments.FindAsync(model.Id);
                if (existing != null)
                {
                    existing.Name = model.Name.Trim();
                    existing.Description = model.Description?.Trim() ?? string.Empty;
                    existing.CodePrefix = model.CodePrefix.Trim().ToUpper();
                    existing.DefaultServiceTimeMinutes = model.DefaultServiceTimeMinutes;
                    existing.IsActive = model.IsActive;
                }
            }
            else
            {
                var newDept = new Department
                {
                    Name = model.Name.Trim(),
                    Description = model.Description?.Trim() ?? string.Empty,
                    CodePrefix = model.CodePrefix.Trim().ToUpper(),
                    DefaultServiceTimeMinutes = model.DefaultServiceTimeMinutes,
                    IsActive = model.IsActive
                };
                _context.Departments.Add(newDept);
            }

            await _context.SaveChangesAsync();
            await _hubContext.Clients.Group(QueueHub.DisplayBoardGroup).SendAsync("QueueUpdated", 0);

            return RedirectToAction(nameof(Departments), new { success = "Department saved successfully!" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleDepartment(int id)
        {
            var dept = await _context.Departments.FindAsync(id);
            if (dept != null)
            {
                dept.IsActive = !dept.IsActive;
                await _context.SaveChangesAsync();
                await _hubContext.Clients.Group(QueueHub.DisplayBoardGroup).SendAsync("QueueUpdated", 0);
            }
            return RedirectToAction(nameof(Departments));
        }

        [HttpGet]
        public async Task<IActionResult> Counters(string? success = null)
        {
            var depts = await _context.Departments
                .Where(d => d.IsActive)
                .OrderBy(d => d.Name)
                .ToListAsync();

            var counters = await _context.Counters
                .Include(c => c.Department)
                .Include(c => c.AssignedStaff)
                .Include(c => c.CurrentTicket)
                .OrderBy(c => c.Name)
                .Select(c => new CounterItemDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    DepartmentId = c.DepartmentId,
                    DepartmentName = c.Department != null ? c.Department.Name : "N/A",
                    StaffName = c.AssignedStaff != null ? c.AssignedStaff.FullName : "Unassigned",
                    IsActive = c.IsActive,
                    CurrentTicketNumber = c.CurrentTicket != null ? c.CurrentTicket.TicketNumber : null
                })
                .ToListAsync();

            var model = new CounterManageViewModel
            {
                DepartmentOptions = depts.Select(d => new SelectListItem
                {
                    Value = d.Id.ToString(),
                    Text = $"{d.Name} ({d.CodePrefix})"
                }).ToList(),
                ExistingCounters = counters,
                SuccessMessage = success
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveCounter(CounterManageViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return await Counters();
            }

            if (model.Id > 0)
            {
                var existing = await _context.Counters.FindAsync(model.Id);
                if (existing != null)
                {
                    existing.Name = model.Name.Trim();
                    existing.DepartmentId = model.DepartmentId;
                    existing.IsActive = model.IsActive;
                }
            }
            else
            {
                var newCounter = new Counter
                {
                    Name = model.Name.Trim(),
                    DepartmentId = model.DepartmentId,
                    IsActive = model.IsActive
                };
                _context.Counters.Add(newCounter);
            }

            await _context.SaveChangesAsync();
            await _hubContext.Clients.Group(QueueHub.DisplayBoardGroup).SendAsync("QueueUpdated", model.DepartmentId);

            return RedirectToAction(nameof(Counters), new { success = "Counter saved successfully!" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleCounter(int id)
        {
            var counter = await _context.Counters.FindAsync(id);
            if (counter != null)
            {
                counter.IsActive = !counter.IsActive;
                await _context.SaveChangesAsync();
                await _hubContext.Clients.Group(QueueHub.DisplayBoardGroup).SendAsync("QueueUpdated", counter.DepartmentId);
            }
            return RedirectToAction(nameof(Counters));
        }

        [HttpGet]
        public async Task<IActionResult> Analytics()
        {
            var logs = await _context.ServiceLogs
                .Include(s => s.Department)
                .Include(s => s.Counter)
                .Include(s => s.Ticket)
                .OrderByDescending(s => s.CompletedAt)
                .ToListAsync();

            var depts = await _context.Departments.ToListAsync();

            var deptStats = new List<DepartmentDurationStatsDto>();

            foreach (var d in depts)
            {
                var dLogs = logs.Where(l => l.DepartmentId == d.Id).ToList();
                if (dLogs.Any())
                {
                    deptStats.Add(new DepartmentDurationStatsDto
                    {
                        DepartmentName = d.Name,
                        CodePrefix = d.CodePrefix,
                        TotalCompleted = dLogs.Count,
                        AverageMinutes = Math.Round(dLogs.Average(l => l.DurationMinutes), 1),
                        MinMinutes = Math.Round(dLogs.Min(l => l.DurationMinutes), 1),
                        MaxMinutes = Math.Round(dLogs.Max(l => l.DurationMinutes), 1)
                    });
                }
            }

            var hourlyGroups = logs
                .GroupBy(l => l.HourOfDay)
                .OrderBy(g => g.Key)
                .Select(g => new HourlyLoadStatsDto
                {
                    HourOfDay = g.Key,
                    ConsultationCount = g.Count()
                })
                .ToList();

            var recentDto = logs.Take(25).Select(s => new AdminRecentLogDto
            {
                Id = s.Id,
                TicketNumber = s.Ticket?.TicketNumber ?? "N/A",
                DepartmentName = s.Department?.Name ?? "General",
                CounterName = s.Counter?.Name ?? "Desk",
                DurationMinutes = s.DurationMinutes,
                CompletedAt = s.CompletedAt
            }).ToList();

            var model = new AnalyticsViewModel
            {
                TotalServiceLogsRecorded = logs.Count,
                OverallAverageDuration = logs.Any() ? Math.Round(logs.Average(l => l.DurationMinutes), 1) : 0,
                DepartmentStats = deptStats,
                HourlyStats = hourlyGroups,
                DetailedLogs = recentDto
            };

            return View(model);
        }
    }
}
