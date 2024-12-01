using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ChatterBox.Models
{
    public class Contact
    {
        [Key]
        public int Id { get; set; }  // Changed from ContactId to Id

        [Required]
        public string UserId { get; set; }

        [Required]
        public string ContactUserId { get; set; }

        public DateTime CreatedAt { get; set; }
        public bool IsBlocked { get; set; }

        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; }

        [ForeignKey("ContactUserId")]
        public virtual ApplicationUser ContactUser { get; set; }
    }
}