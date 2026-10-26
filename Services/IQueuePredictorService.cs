using WaitWise.ViewModels;

namespace WaitWise.Services
{
    public interface IQueuePredictorService
    {
        /// <summary>
        /// Calculates the dynamic moving average service duration (in minutes) based on recent completed tickets.
        /// </summary>
        Task<double> GetDynamicAverageServiceTimeMinutesAsync(int departmentId);

        /// <summary>
        /// Returns the number of currently active counters assigned to the department.
        /// </summary>
        Task<int> GetActiveCounterCountAsync(int departmentId);

        /// <summary>
        /// Calculates how many tickets are ahead of this specific ticket in the department queue.
        /// </summary>
        Task<int> GetPeopleAheadCountAsync(int ticketId);

        /// <summary>
        /// Calculates estimated wait time for a given department and queue position.
        /// </summary>
        Task<int> PredictWaitTimeMinutesAsync(int departmentId, int peopleAhead);

        /// <summary>
        /// Compiles a full prediction breakdown (people ahead, active desks, ETA, congestion index).
        /// </summary>
        Task<QueuePredictionResult?> GetPredictionDetailsAsync(int ticketId);

        /// <summary>
        /// Recalculates and persists estimated wait times for all currently waiting tickets in a department.
        /// Returns a map of TicketId -> NewEstimatedWaitMinutes.
        /// </summary>
        Task<Dictionary<int, int>> RecalculateDepartmentQueueAsync(int departmentId);
    }
}
