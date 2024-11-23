using System;
using System.ComponentModel.DataAnnotations;

namespace ChatterBox.Models
{
    public class Message
    {
        public int MessageId { get; set; }

        [Required]
        public string SenderId { get; set; }

        public string? ReceiverId { get; set; }

        public int? GroupId { get; set; }

        [Required]
        public string Content { get; set; }

        public DateTime SentAt { get; set; }

        public bool IsRead { get; set; }

        public bool IsDeleted { get; set; }

        public virtual ApplicationUser Sender { get; set; }
        public virtual ApplicationUser? Receiver { get; set; }
        public virtual Group? Group { get; set; }
    }
}