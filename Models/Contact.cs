using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ChatterBox.Models
{
    public enum ContactStatus
    {
        Pending = 0,
        Accepted = 1,
        Declined = 2
    }

    public class Contact
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public string ContactUserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsBlocked { get; set; }
        public ContactStatus Status { get; set; } = ContactStatus.Accepted;

        public virtual ApplicationUser User { get; set; }
        public virtual ApplicationUser ContactUser { get; set; }
    }
}