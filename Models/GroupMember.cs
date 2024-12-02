using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ChatterBox.Models
{
    public class GroupMember
    {
        public int GroupId { get; set; }
        public string UserId { get; set; }

        [Required]
        [StringLength(50)]
        public string Role { get; set; }  // "Admin", "Member"

        public DateTime JoinedAt { get; set; }

        // New property to track if user was previously admin
        public bool WasAdmin { get; set; }

        // New property to track when user last became admin (if applicable)
        public DateTime? BecameAdminAt { get; set; }

        // New property to track when user last stopped being admin (if applicable)
        public DateTime? StoppedBeingAdminAt { get; set; }

        public virtual Group Group { get; set; }
        public virtual ApplicationUser User { get; set; }
    }
}