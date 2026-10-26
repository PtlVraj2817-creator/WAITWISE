using Microsoft.AspNetCore.SignalR;

namespace WaitWise.Hubs
{
    public class QueueHub : Hub
    {
        public const string DisplayBoardGroup = "DisplayBoard";

        /// <summary>
        /// Public display board joins this group to receive all ticket calls across all departments
        /// </summary>
        public async Task JoinDisplayBoard()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, DisplayBoardGroup);
        }

        /// <summary>
        /// Customer tracker joins a specific ticket group to receive private status updates
        /// </summary>
        public async Task JoinTicket(string ticketNumber)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"ticket_{ticketNumber.ToUpper()}");
        }

        /// <summary>
        /// Staff or customers watching a specific department queue
        /// </summary>
        public async Task JoinDepartment(int departmentId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"dept_{departmentId}");
        }

        public async Task LeaveDepartment(int departmentId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"dept_{departmentId}");
        }
    }
}
