using Microsoft.EntityFrameworkCore;
using WaitWise.Data;
using WaitWise.Models;
using WaitWise.Models.Enums;
using WaitWise.ViewModels;

namespace WaitWise.Services
{
    public class QueuePredictorService : IQueuePredictorService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<QueuePredictorService> _logger;

        public QueuePredictorService(ApplicationDbContext context, ILogger<QueuePredictorService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<double> GetDynamicAverageServiceTimeMinutesAsync(int departmentId)
        {
            // Look up to the last 20 completed service records for this department
            var recentLogs = await _context.ServiceLogs
                .Where(s => s.DepartmentId == departmentId && s.DurationMinutes > 0)
                .OrderByDescending(s => s.CompletedAt)
                .Take(20)
                .Select(s => s.DurationMinutes)
                .ToListAsync();

            if (recentLogs.Count >= 3)
            {
                // Weighted Moving Average (WMA): Most recent records carry higher significance
                double weightedSum = 0;
                double totalWeights = 0;
                int count = recentLogs.Count;

                for (int i = 0; i < count; i++)
                {
                    double weight = count - i; // Latest record has weight = count, second has count - 1, etc.
                    weightedSum += recentLogs[i] * weight;
                    totalWeights += weight;
                }

                double dynamicAvg = Math.Round(weightedSum / totalWeights, 1);
                return Math.Max(1.0, dynamicAvg); // Minimum 1 minute
            }

            // Fallback to department configured default
            var department = await _context.Departments.FindAsync(departmentId);
            return department?.DefaultServiceTimeMinutes ?? 10.0;
        }

        public async Task<int> GetActiveCounterCountAsync(int departmentId)
        {
            var activeCount = await _context.Counters
                .CountAsync(c => c.DepartmentId == departmentId && c.IsActive);

            return activeCount;
        }

        public async Task<int> GetPeopleAheadCountAsync(int ticketId)
        {
            var targetTicket = await _context.QueueTickets.FindAsync(ticketId);
            if (targetTicket == null || targetTicket.Status != TicketStatus.Waiting)
            {
                return 0;
            }

            // Count waiting tickets created before this ticket in the same department
            var aheadCount = await _context.QueueTickets
                .CountAsync(t => t.DepartmentId == targetTicket.DepartmentId 
                              && t.Status == TicketStatus.Waiting 
                              && t.DailySequence < targetTicket.DailySequence);

            return aheadCount;
        }

        public async Task<int> PredictWaitTimeMinutesAsync(int departmentId, int peopleAhead)
        {
            if (peopleAhead <= 0)
            {
                return 0; // First in queue
            }

            var avgDuration = await GetDynamicAverageServiceTimeMinutesAsync(departmentId);
            var activeCounters = await GetActiveCounterCountAsync(departmentId);

            // If counters are temporarily 0, use baseline of 1 to calculate realistic turnaround
            int effectiveCounters = Math.Max(1, activeCounters);

            // Wait = Ceiling((People Ahead * Average Service Duration) / Active Counters)
            double calculated = (peopleAhead * avgDuration) / effectiveCounters;
            return (int)Math.Ceiling(calculated);
        }

        public async Task<QueuePredictionResult?> GetPredictionDetailsAsync(int ticketId)
        {
            var ticket = await _context.QueueTickets
                .Include(t => t.Department)
                .FirstOrDefaultAsync(t => t.Id == ticketId);

            if (ticket == null)
            {
                return null;
            }

            if (ticket.Status != TicketStatus.Waiting)
            {
                return new QueuePredictionResult
                {
                    TicketId = ticket.Id,
                    TicketNumber = ticket.TicketNumber,
                    DepartmentId = ticket.DepartmentId,
                    DepartmentName = ticket.Department?.Name ?? "Unknown",
                    PeopleAhead = 0,
                    ActiveCounters = await GetActiveCounterCountAsync(ticket.DepartmentId),
                    AverageServiceDurationMinutes = 0,
                    EstimatedWaitMinutes = 0,
                    EstimatedCallTime = DateTime.UtcNow,
                    CongestionLevel = ticket.Status.ToString(),
                    CongestionBadgeClass = ticket.Status == TicketStatus.Serving ? "bg-primary" : "bg-secondary",
                    StatusMessage = ticket.Status == TicketStatus.Serving 
                        ? "Currently being served at counter" 
                        : $"Ticket is {ticket.Status}"
                };
            }

            var peopleAhead = await GetPeopleAheadCountAsync(ticketId);
            var avgDuration = await GetDynamicAverageServiceTimeMinutesAsync(ticket.DepartmentId);
            var activeCounters = await GetActiveCounterCountAsync(ticket.DepartmentId);
            var waitMinutes = await PredictWaitTimeMinutesAsync(ticket.DepartmentId, peopleAhead);

            // Determine Congestion Level
            int effectiveCounters = Math.Max(1, activeCounters);
            double queueLoad = (double)peopleAhead / effectiveCounters;

            string congestionLevel;
            string badgeClass;

            if (activeCounters == 0)
            {
                congestionLevel = "Desks Paused";
                badgeClass = "bg-warning text-dark";
            }
            else if (queueLoad <= 2.0)
            {
                congestionLevel = "Smooth Flow";
                badgeClass = "bg-success";
            }
            else if (queueLoad <= 5.0)
            {
                congestionLevel = "Moderate Queue";
                badgeClass = "bg-info text-dark";
            }
            else
            {
                congestionLevel = "Peak Rush";
                badgeClass = "bg-danger";
            }

            string statusMessage = peopleAhead == 0
                ? "You are next in line! Please stay near the service desk."
                : $"{peopleAhead} person(s) ahead of you.";

            return new QueuePredictionResult
            {
                TicketId = ticket.Id,
                TicketNumber = ticket.TicketNumber,
                DepartmentId = ticket.DepartmentId,
                DepartmentName = ticket.Department?.Name ?? "Unknown",
                PeopleAhead = peopleAhead,
                ActiveCounters = activeCounters,
                AverageServiceDurationMinutes = avgDuration,
                EstimatedWaitMinutes = waitMinutes,
                EstimatedCallTime = DateTime.UtcNow.AddMinutes(waitMinutes),
                CongestionLevel = congestionLevel,
                CongestionBadgeClass = badgeClass,
                StatusMessage = statusMessage
            };
        }

        public async Task<Dictionary<int, int>> RecalculateDepartmentQueueAsync(int departmentId)
        {
            var waitingTickets = await _context.QueueTickets
                .Where(t => t.DepartmentId == departmentId && t.Status == TicketStatus.Waiting)
                .OrderBy(t => t.DailySequence)
                .ToListAsync();

            var avgDuration = await GetDynamicAverageServiceTimeMinutesAsync(departmentId);
            var activeCounters = await GetActiveCounterCountAsync(departmentId);
            int effectiveCounters = Math.Max(1, activeCounters);

            var resultMap = new Dictionary<int, int>();

            for (int i = 0; i < waitingTickets.Count; i++)
            {
                int peopleAhead = i;
                int waitMinutes = peopleAhead == 0 
                    ? 0 
                    : (int)Math.Ceiling((peopleAhead * avgDuration) / effectiveCounters);

                waitingTickets[i].EstimatedWaitMinutes = waitMinutes;
                resultMap[waitingTickets[i].Id] = waitMinutes;
            }

            await _context.SaveChangesAsync();
            return resultMap;
        }
    }
}
