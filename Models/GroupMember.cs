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
        public string Role { get; set; }

        public DateTime JoinedAt { get; set; }

        public virtual Group Group { get; set; }
        public virtual ApplicationUser User { get; set; }
    }
}