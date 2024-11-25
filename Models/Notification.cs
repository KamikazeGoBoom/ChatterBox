using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace ChatterBox.Models
{
    public class Notification
    {
        [Key]
        public int NotificationId { get; set; }
        [Required]
        [StringLength(450)]
        public string UserId { get; set; } = null!;
        [Required]
        [StringLength(200)]
        public string Title { get; set; } = null!;
        [Required]
        [StringLength(1000)]
        public string Message { get; set; } = null!;
        [Required]
        [StringLength(50)]
        public string Type { get; set; } = null!;  // "Message", "GroupInvite", etc.
        [StringLength(100)]
        public string? RelatedEntityId { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; } = null!;
    }
}