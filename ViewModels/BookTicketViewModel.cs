using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace WaitWise.ViewModels
{
    public class BookTicketViewModel
    {
        [Required(ErrorMessage = "Please select a department / service")]
        [Display(Name = "Department / Service")]
        public int DepartmentId { get; set; }

        [Required(ErrorMessage = "Please enter your name")]
        [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
        [Display(Name = "Your Full Name")]
        public string CustomerName { get; set; } = string.Empty;

        [Phone(ErrorMessage = "Please enter a valid phone number")]
        [StringLength(20)]
        [Display(Name = "Mobile Phone (Optional for SMS alerts)")]
        public string? CustomerPhone { get; set; }

        public List<SelectListItem> DepartmentOptions { get; set; } = new List<SelectListItem>();
        public List<DepartmentCardItem> DepartmentCards { get; set; } = new List<DepartmentCardItem>();
    }

    public class DepartmentCardItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string CodePrefix { get; set; } = string.Empty;
        public int WaitingCount { get; set; }
        public int ActiveCounters { get; set; }
        public int EstimatedWaitMinutes { get; set; }
    }
}
