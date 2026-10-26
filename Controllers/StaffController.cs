using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
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
    [Authorize(Roles = "Staff,Admin")]
    public class StaffController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IQueuePredictorService _predictorService;
        private readonly IHubContext<QueueHub> _hubContext;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<StaffController> _logger;

        public StaffController(
            ApplicationDbContext context,
            IQueuePredictorService predictorService,
            IHubContext<QueueHub> hubContext,
            UserManager<ApplicationUser> userManager,
            ILogger<StaffController> logger)
        {
            _context = context;
            _predictorService = predictorService;
            _hubContext = hubContext;
            _userManager = userManager;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Desk(int? counterId = null, string? success = null, string? warning = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var allCounters = await _context.Counters
                .Include(c => c.Department)
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .ToListAsync();

            if (!allCounters.Any())
            {
                return View("NoCountersAvailable");
            }

            // Determine active counter:
            // 1) explicitly passed counterId
            // 2) user's AssignedCounterId
            // 3) first available counter
            Counter? selectedCounter = null;
            if (counterId.HasValue)
            {
                selectedCounter = allCounters.FirstOrDefault(c => c.Id == counterId.Value);
            }
            if (selectedCounter == null && user.AssignedCounterId.HasValue)
            {
                selectedCounter = allCounters.FirstOrDefault(c => c.Id == user.AssignedCounterId.Value);
            }
            if (selectedCounter == null)
            {
                selectedCounter = allCounters.First();
            }

            // Update user's assigned counter in database if changed
            if (user.AssignedCounterId != selectedCounter.Id)
            {
                user.AssignedCounterId = selectedCounter.Id;
                await _userManager.UpdateAsync(user);
            }

            // Load currently serving ticket
            StaffCurrentTicketDto? currentTicketDto = null;
            if (selectedCounter.CurrentTicketId.HasValue)
            {
                var curTicket = await _context.QueueTickets
                    .FirstOrDefaultAsync(t => t.Id == selectedCounter.CurrentTicketId.Value);

                if (curTicket != null && curTicket.Status == TicketStatus.Serving)
                {
                    currentTicketDto = new StaffCurrentTicketDto
                    {
                        TicketId = curTicket.Id,
                        TicketNumber = curTicket.TicketNumber,
                        CustomerName = curTicket.CustomerName,
                        CustomerPhone = curTicket.CustomerPhone,
                        CalledAt = curTicket.CalledAt ?? DateTime.UtcNow
                    };
                }
            }

            // Load waiting tickets for this counter's department
            var waitingTickets = await _context.QueueTickets
                .Where(t => t.DepartmentId == selectedCounter.DepartmentId && t.Status == TicketStatus.Waiting)
                .OrderBy(t => t.DailySequence)
                .ToListAsync();

            var waitingListDto = waitingTickets.Select((t, index) => new StaffWaitingTicketDto
            {
                TicketId = t.Id,
                TicketNumber = t.TicketNumber,
                CustomerName = t.CustomerName,
                CustomerPhone = t.CustomerPhone,
                CreatedAt = t.CreatedAt,
                EstimatedWaitMinutes = t.EstimatedWaitMinutes,
                PositionInLine = index + 1
            }).ToList();

            // Load today's staff stats
            var today = DateTime.UtcNow.Date;
            var completedCount = await _context.ServiceLogs
                .CountAsync(s => s.StaffUserId == user.Id && s.CompletedAt >= today);

            var avgDuration = await _predictorService.GetDynamicAverageServiceTimeMinutesAsync(selectedCounter.DepartmentId);

            var model = new StaffDeskViewModel
            {
                CounterId = selectedCounter.Id,
                CounterName = selectedCounter.Name,
                DepartmentId = selectedCounter.DepartmentId,
                DepartmentName = selectedCounter.Department?.Name ?? "General",
                DepartmentCodePrefix = selectedCounter.Department?.CodePrefix ?? "A",
                StaffName = user.FullName,
                StaffEmail = user.Email ?? string.Empty,
                AvailableCounters = allCounters.Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = $"{c.Name} ({c.Department?.Name ?? "General"})",
                    Selected = c.Id == selectedCounter.Id
                }).ToList(),
                CurrentTicket = currentTicketDto,
                WaitingTickets = waitingListDto,
                CompletedTodayCount = completedCount,
                AverageServiceDurationMinutes = avgDuration,
                SuccessMessage = success,
                WarningMessage = warning
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CallNext(int counterId)
        {
            var counter = await _context.Counters
                .Include(c => c.Department)
                .FirstOrDefaultAsync(c => c.Id == counterId);

            if (counter == null)
            {
                return NotFound();
            }

            // If counter is already serving someone, auto-complete or prompt
            if (counter.CurrentTicketId.HasValue)
            {
                return RedirectToAction(nameof(Desk), new { 
                    counterId = counter.Id, 
                    warning = "Please complete or mark the current ticket as No-Show before calling the next one." 
                });
            }

            // Find next waiting ticket in line for this department
            var nextTicket = await _context.QueueTickets
                .Where(t => t.DepartmentId == counter.DepartmentId && t.Status == TicketStatus.Waiting)
                .OrderBy(t => t.DailySequence)
                .FirstOrDefaultAsync();

            if (nextTicket == null)
            {
                return RedirectToAction(nameof(Desk), new { 
                    counterId = counter.Id, 
                    warning = $"No tickets waiting in line for {counter.Department?.Name ?? "this department"}." 
                });
            }

            // Transition ticket state to Serving
            nextTicket.Status = TicketStatus.Serving;
            nextTicket.CounterId = counter.Id;
            nextTicket.CalledAt = DateTime.UtcNow;

            counter.CurrentTicketId = nextTicket.Id;

            await _context.SaveChangesAsync();

            // Recalculate remaining tickets in this department
            await _predictorService.RecalculateDepartmentQueueAsync(counter.DepartmentId);

            // Broadcast real-time notifications via SignalR
            var callPayload = new
            {
                ticketNumber = nextTicket.TicketNumber,
                counterName = counter.Name,
                departmentName = counter.Department?.Name ?? "General"
            };

            await _hubContext.Clients.Group(QueueHub.DisplayBoardGroup).SendAsync("TicketCalled", callPayload);
            await _hubContext.Clients.Group($"ticket_{nextTicket.TicketNumber}").SendAsync("TicketCalled", callPayload);
            await _hubContext.Clients.Group($"dept_{counter.DepartmentId}").SendAsync("QueueUpdated", counter.DepartmentId);

            _logger.LogInformation("Called ticket {TicketNumber} to {CounterName}", nextTicket.TicketNumber, counter.Name);

            return RedirectToAction(nameof(Desk), new { 
                counterId = counter.Id, 
                success = $"Called ticket {nextTicket.TicketNumber} ({nextTicket.CustomerName}) to {counter.Name}!" 
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CallSpecific(int counterId, int ticketId)
        {
            var counter = await _context.Counters
                .Include(c => c.Department)
                .FirstOrDefaultAsync(c => c.Id == counterId);

            if (counter == null) return NotFound();

            if (counter.CurrentTicketId.HasValue)
            {
                return RedirectToAction(nameof(Desk), new { 
                    counterId = counter.Id, 
                    warning = "Please complete the current ticket before calling another." 
                });
            }

            var ticket = await _context.QueueTickets.FindAsync(ticketId);
            if (ticket == null || ticket.Status != TicketStatus.Waiting)
            {
                return RedirectToAction(nameof(Desk), new { 
                    counterId = counter.Id, 
                    warning = "The selected ticket is no longer waiting." 
                });
            }

            ticket.Status = TicketStatus.Serving;
            ticket.CounterId = counter.Id;
            ticket.CalledAt = DateTime.UtcNow;

            counter.CurrentTicketId = ticket.Id;
            await _context.SaveChangesAsync();

            await _predictorService.RecalculateDepartmentQueueAsync(counter.DepartmentId);

            var callPayload = new
            {
                ticketNumber = ticket.TicketNumber,
                counterName = counter.Name,
                departmentName = counter.Department?.Name ?? "General"
            };

            await _hubContext.Clients.Group(QueueHub.DisplayBoardGroup).SendAsync("TicketCalled", callPayload);
            await _hubContext.Clients.Group($"ticket_{ticket.TicketNumber}").SendAsync("TicketCalled", callPayload);
            await _hubContext.Clients.Group($"dept_{counter.DepartmentId}").SendAsync("QueueUpdated", counter.DepartmentId);

            return RedirectToAction(nameof(Desk), new { 
                counterId = counter.Id, 
                success = $"Called ticket {ticket.TicketNumber} to {counter.Name}!" 
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Recall(int counterId)
        {
            var counter = await _context.Counters
                .Include(c => c.Department)
                .Include(c => c.CurrentTicket)
                .FirstOrDefaultAsync(c => c.Id == counterId);

            if (counter?.CurrentTicket == null)
            {
                return RedirectToAction(nameof(Desk), new { counterId = counter?.Id, warning = "No ticket is currently being served." });
            }

            var callPayload = new
            {
                ticketNumber = counter.CurrentTicket.TicketNumber,
                counterName = counter.Name,
                departmentName = counter.Department?.Name ?? "General"
            };

            // Resend audio chime and announcement
            await _hubContext.Clients.Group(QueueHub.DisplayBoardGroup).SendAsync("TicketCalled", callPayload);
            await _hubContext.Clients.Group($"ticket_{counter.CurrentTicket.TicketNumber}").SendAsync("TicketCalled", callPayload);

            return RedirectToAction(nameof(Desk), new { 
                counterId = counter.Id, 
                success = $"Recalled ticket {counter.CurrentTicket.TicketNumber}! Chime and voice announcement triggered." 
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteCurrent(int counterId)
        {
            var counter = await _context.Counters.FindAsync(counterId);
            if (counter == null || !counter.CurrentTicketId.HasValue)
            {
                return RedirectToAction(nameof(Desk), new { counterId, warning = "No active ticket to complete." });
            }

            var ticket = await _context.QueueTickets.FindAsync(counter.CurrentTicketId.Value);
            var user = await _userManager.GetUserAsync(User);
            var now = DateTime.UtcNow;

            if (ticket != null)
            {
                ticket.Status = TicketStatus.Completed;
                ticket.CompletedAt = now;

                // Calculate actual elapsed duration for predictor learning
                double durationMinutes = 5.0; // default safe baseline
                if (ticket.CalledAt.HasValue)
                {
                    var elapsed = (now - ticket.CalledAt.Value).TotalMinutes;
                    durationMinutes = Math.Round(Math.Max(0.5, elapsed), 1);
                }

                // Add real empirical ServiceLog to SQL Server!
                var log = new ServiceLog
                {
                    TicketId = ticket.Id,
                    DepartmentId = ticket.DepartmentId,
                    CounterId = counter.Id,
                    StaffUserId = user?.Id,
                    DurationMinutes = durationMinutes,
                    CompletedAt = now,
                    DayOfWeek = now.DayOfWeek,
                    HourOfDay = now.Hour
                };

                _context.ServiceLogs.Add(log);
            }

            counter.CurrentTicketId = null;
            await _context.SaveChangesAsync();

            // Recalculate remaining tickets in department
            await _predictorService.RecalculateDepartmentQueueAsync(counter.DepartmentId);

            // Notify all listeners
            await _hubContext.Clients.Group(QueueHub.DisplayBoardGroup).SendAsync("QueueUpdated", counter.DepartmentId);
            await _hubContext.Clients.Group($"dept_{counter.DepartmentId}").SendAsync("QueueUpdated", counter.DepartmentId);

            return RedirectToAction(nameof(Desk), new { 
                counterId = counter.Id, 
                success = $"Completed consultation for {ticket?.TicketNumber}! Service log successfully recorded for wait predictor." 
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkNoShow(int counterId)
        {
            var counter = await _context.Counters.FindAsync(counterId);
            if (counter == null || !counter.CurrentTicketId.HasValue)
            {
                return RedirectToAction(nameof(Desk), new { counterId, warning = "No active ticket to mark as No-Show." });
            }

            var ticket = await _context.QueueTickets.FindAsync(counter.CurrentTicketId.Value);
            if (ticket != null)
            {
                ticket.Status = TicketStatus.NoShow;
                ticket.CompletedAt = DateTime.UtcNow;
            }

            counter.CurrentTicketId = null;
            await _context.SaveChangesAsync();

            await _predictorService.RecalculateDepartmentQueueAsync(counter.DepartmentId);

            await _hubContext.Clients.Group(QueueHub.DisplayBoardGroup).SendAsync("QueueUpdated", counter.DepartmentId);
            await _hubContext.Clients.Group($"dept_{counter.DepartmentId}").SendAsync("QueueUpdated", counter.DepartmentId);

            return RedirectToAction(nameof(Desk), new { 
                counterId = counter.Id, 
                warning = $"Ticket {ticket?.TicketNumber} marked as No-Show." 
            });
        }
    }
}
