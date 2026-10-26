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
    public class QueueController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IQueuePredictorService _predictorService;
        private readonly IHubContext<QueueHub> _hubContext;
        private readonly ILogger<QueueController> _logger;

        public QueueController(
            ApplicationDbContext context,
            IQueuePredictorService predictorService,
            IHubContext<QueueHub> hubContext,
            ILogger<QueueController> logger)
        {
            _context = context;
            _predictorService = predictorService;
            _hubContext = hubContext;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Book(int? departmentId = null)
        {
            var activeDepts = await _context.Departments
                .Where(d => d.IsActive)
                .OrderBy(d => d.Name)
                .ToListAsync();

            var cards = new List<DepartmentCardItem>();

            foreach (var dept in activeDepts)
            {
                var waitingCount = await _context.QueueTickets
                    .CountAsync(t => t.DepartmentId == dept.Id && t.Status == TicketStatus.Waiting);

                var activeCounters = await _predictorService.GetActiveCounterCountAsync(dept.Id);
                var estimatedWait = await _predictorService.PredictWaitTimeMinutesAsync(dept.Id, waitingCount);

                cards.Add(new DepartmentCardItem
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

            var model = new BookTicketViewModel
            {
                DepartmentId = departmentId ?? (activeDepts.FirstOrDefault()?.Id ?? 0),
                DepartmentCards = cards,
                DepartmentOptions = activeDepts.Select(d => new SelectListItem
                {
                    Value = d.Id.ToString(),
                    Text = $"{d.Name} (Prefix: {d.CodePrefix})"
                }).ToList()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Book(BookTicketViewModel model)
        {
            var dept = await _context.Departments.FindAsync(model.DepartmentId);
            if (dept == null || !dept.IsActive)
            {
                ModelState.AddModelError(nameof(model.DepartmentId), "The selected department is invalid or inactive.");
            }

            if (!ModelState.IsValid)
            {
                // Reload departments for view redisplay
                var activeDepts = await _context.Departments.Where(d => d.IsActive).ToListAsync();
                model.DepartmentOptions = activeDepts.Select(d => new SelectListItem
                {
                    Value = d.Id.ToString(),
                    Text = $"{d.Name} (Prefix: {d.CodePrefix})"
                }).ToList();
                return View(model);
            }

            var today = DateTime.UtcNow.Date;

            // Generate daily sequence number
            var maxSeq = await _context.QueueTickets
                .Where(t => t.DepartmentId == dept!.Id && t.CreatedAt >= today)
                .MaxAsync(t => (int?)t.DailySequence);

            int nextSeq = (maxSeq.HasValue && maxSeq.Value >= 100) ? maxSeq.Value + 1 : 101;
            string ticketNumber = $"{dept!.CodePrefix}-{nextSeq}";

            // Calculate people ahead & smart estimated wait time
            int peopleAhead = await _context.QueueTickets
                .CountAsync(t => t.DepartmentId == dept.Id && t.Status == TicketStatus.Waiting);

            int estimatedWait = await _predictorService.PredictWaitTimeMinutesAsync(dept.Id, peopleAhead);

            var ticket = new QueueTicket
            {
                TicketNumber = ticketNumber,
                DailySequence = nextSeq,
                CustomerName = model.CustomerName.Trim(),
                CustomerPhone = model.CustomerPhone?.Trim(),
                DepartmentId = dept.Id,
                Status = TicketStatus.Waiting,
                CreatedAt = DateTime.UtcNow,
                EstimatedWaitMinutes = estimatedWait
            };

            _context.QueueTickets.Add(ticket);
            await _context.SaveChangesAsync();

            _logger.LogInformation("New ticket created: {TicketNumber} for {CustomerName}", ticketNumber, ticket.CustomerName);

            // Notify staff desks and public display board via SignalR
            await _hubContext.Clients.Group($"dept_{dept.Id}").SendAsync("QueueUpdated", dept.Id);
            await _hubContext.Clients.Group(QueueHub.DisplayBoardGroup).SendAsync("QueueUpdated", dept.Id);

            return RedirectToAction(nameof(Track), new { ticketNumber = ticket.TicketNumber });
        }

        [HttpGet]
        public async Task<IActionResult> Track(string? ticketNumber = null)
        {
            var model = new TicketTrackerViewModel
            {
                SearchQuery = ticketNumber ?? string.Empty
            };

            if (string.IsNullOrWhiteSpace(ticketNumber))
            {
                return View(model);
            }

            var cleanTicketNumber = ticketNumber.Trim().ToUpper();

            var ticket = await _context.QueueTickets
                .Include(t => t.Department)
                .Include(t => t.Counter)
                .FirstOrDefaultAsync(t => t.TicketNumber == cleanTicketNumber);

            if (ticket == null)
            {
                model.ErrorMessage = $"No ticket found matching '{cleanTicketNumber}'. Please check your ticket number and try again.";
                return View(model);
            }

            model.TicketId = ticket.Id;
            model.TicketNumber = ticket.TicketNumber;
            model.CustomerName = ticket.CustomerName;
            model.DepartmentId = ticket.DepartmentId;
            model.DepartmentName = ticket.Department?.Name ?? "General";
            model.Status = ticket.Status;
            model.CounterName = ticket.Counter?.Name;
            model.CreatedAt = ticket.CreatedAt;
            model.CalledAt = ticket.CalledAt;
            model.CompletedAt = ticket.CompletedAt;
            model.Prediction = await _predictorService.GetPredictionDetailsAsync(ticket.Id);

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> GetTicketStatusJson(string ticketNumber)
        {
            if (string.IsNullOrWhiteSpace(ticketNumber))
            {
                return BadRequest(new { error = "Ticket number is required." });
            }

            var clean = ticketNumber.Trim().ToUpper();
            var ticket = await _context.QueueTickets
                .Include(t => t.Department)
                .Include(t => t.Counter)
                .FirstOrDefaultAsync(t => t.TicketNumber == clean);

            if (ticket == null)
            {
                return NotFound(new { error = "Ticket not found." });
            }

            var prediction = await _predictorService.GetPredictionDetailsAsync(ticket.Id);

            return Json(new
            {
                ticketId = ticket.Id,
                ticketNumber = ticket.TicketNumber,
                customerName = ticket.CustomerName,
                departmentName = ticket.Department?.Name ?? "General",
                status = ticket.Status.ToString(),
                statusInt = (int)ticket.Status,
                counterName = ticket.Counter?.Name,
                calledAt = ticket.CalledAt?.ToString("hh:mm tt"),
                peopleAhead = prediction?.PeopleAhead ?? 0,
                estimatedWaitMinutes = prediction?.EstimatedWaitMinutes ?? 0,
                congestionLevel = prediction?.CongestionLevel ?? "Normal",
                congestionBadgeClass = prediction?.CongestionBadgeClass ?? "bg-success",
                statusMessage = prediction?.StatusMessage ?? string.Empty
            });
        }
    }
}
