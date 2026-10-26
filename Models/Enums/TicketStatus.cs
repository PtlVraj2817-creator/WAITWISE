namespace WaitWise.Models.Enums
{
    public enum TicketStatus
    {
        Waiting = 0,    // In line waiting to be called
        Serving = 1,    // Currently at counter being served
        Completed = 2,  // Service finished successfully
        Cancelled = 3,  // Cancelled by customer or staff
        NoShow = 4      // Called but customer did not show up
    }
}
