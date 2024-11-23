using System;

namespace ChatterBox.Models
{
    public class Contact
    {
        public int ContactId { get; set; }
        public string UserId { get; set; }
        public string ContactUserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsBlocked { get; set; }

        public virtual ApplicationUser User { get; set; }
        public virtual ApplicationUser ContactUser { get; set; }
    }
}