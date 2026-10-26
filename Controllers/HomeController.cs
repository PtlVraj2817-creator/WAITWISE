using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WaitWise.Data;
using WaitWise.Models;
using WaitWise.Models.Enums;
using WaitWise.Services;
using WaitWise.ViewModels;

namespace WaitWise.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IQueuePredictorService _predictorService;
        private readonly ILogger<HomeController> _logger;

        public HomeController(
            ApplicationDbContext context,
            IQueuePredictorService predictorService,
            ILogger<HomeController> logger)
        {
            _context = context;
            _predictorService = predictorService;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var today = DateTime.UtcNow.Date;

            var activeDepts = await _context.Departments
                .Where(d => d.IsActive)
                .OrderBy(d => d.Name)
                .ToListAsync();

            var departmentCards = new List<DepartmentCardItem>();

            foreach (var dept in activeDepts)
            {
                var waitingCount = await _context.QueueTickets
                    .CountAsync(t => t.DepartmentId == dept.Id && t.Status == TicketStatus.Waiting);

                var activeCounters = await _predictorService.GetActiveCounterCountAsync(dept.Id);
                var estimatedWait = await _predictorService.PredictWaitTimeMinutesAsync(dept.Id, waitingCount);

                departmentCards.Add(new DepartmentCardItem
                {
                    Id = dept.Id,
                    Name = dept.Name,
                    Description = dept.Description,
                    CodePrefix = dept.CodePrefix,
                    WaitingCount = waitingCount,
                    ActiveCounters = activeCounters,
                    EstimatedWaitMinutes = estimatedWait
                });
            }

            var totalWaiting = await _context.QueueTickets
                .CountAsync(t => t.Status == TicketStatus.Waiting);

            var totalServedToday = await _context.QueueTickets
                .CountAsync(t => t.Status == TicketStatus.Completed && t.CreatedAt >= today);

            var activeCountersTotal = await _context.Counters
                .CountAsync(c => c.IsActive);

            var avgWait = departmentCards.Any()
                ? Math.Round(departmentCards.Average(d => d.EstimatedWaitMinutes), 1)
                : 0;

            var model = new HomeDashboardViewModel
            {
                Departments = departmentCards,
                TotalWaitingToday = totalWaiting,
                TotalServedToday = totalServedToday,
                ActiveCountersCount = activeCountersTotal,
                AverageWaitTimeMinutes = avgWait
            };

            return View(model);
        }

        public IActionResult DisplayBoard()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetDisplayBoardData()
        {
            var counters = await _context.Counters
                .Include(c => c.Department)
                .Include(c => c.CurrentTicket)
                .Include(c => c.AssignedStaff)
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .Select(c => new ServingCounterDto
                {
                    CounterId = c.Id,
                    CounterName = c.Name,
                    DepartmentName = c.Department != null ? c.Department.Name : string.Empty,
                    TicketNumber = c.CurrentTicket != null ? c.CurrentTicket.TicketNumber : null,
                    StaffName = c.AssignedStaff != null ? c.AssignedStaff.FullName : null,
                    CalledAt = c.CurrentTicket != null ? c.CurrentTicket.CalledAt : null
                })
                .ToListAsync();

            var depts = await _context.Departments
                .Where(d => d.IsActive)
                .OrderBy(d => d.Name)
                .ToListAsync();

            var departmentSummaries = new List<DepartmentQueueSummaryDto>();

            foreach (var d in depts)
            {
                var waitingTickets = await _context.QueueTickets
                    .Where(t => t.DepartmentId == d.Id && t.Status == TicketStatus.Waiting)
                    .OrderBy(t => t.DailySequence)
                    .ToListAsync();

                var activeCounters = await _predictorService.GetActiveCounterCountAsync(d.Id);
                var estimatedWait = await _predictorService.PredictWaitTimeMinutesAsync(d.Id, waitingTickets.Count);

                departmentSummaries.Add(new DepartmentQueueSummaryDto
                {
                    DepartmentId = d.Id,
                    DepartmentName = d.Name,
                    CodePrefix = d.CodePrefix,
                    WaitingCount = waitingTickets.Count,
                    EstimatedWaitMinutes = estimatedWait,
                    UpNextTickets = waitingTickets.Take(5).Select(t => t.TicketNumber).ToList()
                });
            }

            return Json(new DisplayBoardViewModel
            {
                ServingCounters = counters,
                DepartmentQueues = departmentSummaries,
                LastUpdated = DateTime.UtcNow
            });
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
