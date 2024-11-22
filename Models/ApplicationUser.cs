using Microsoft.AspNetCore.Identity;

namespace ChatterBox.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string? Status { get; set; }
        public DateTime Birthday { get; set; }
        public bool DarkModeEnabled { get; set; }
        public DateTime LastSeen { get; set; }
        public bool IsDeactivated { get; set; }
    }
}