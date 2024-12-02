using System.ComponentModel.DataAnnotations;

namespace ChatterBox.Models
{
    public class Group
    {
        public int GroupId { get; set; }

        [Required]
        public string Name { get; set; }

        public string? CreatedById { get; set; }

        public DateTime CreatedAt { get; set; }

        public bool IsPrivate { get; set; }

        // New property to track current admin
        public string? CurrentAdminId { get; set; }

        // New property to track last admin transfer date
        public DateTime? LastAdminTransferDate { get; set; }

        public virtual ApplicationUser? CreatedBy { get; set; }

        // New navigation property for current admin
        public virtual ApplicationUser? CurrentAdmin { get; set; }

        public virtual ICollection<GroupMember> Members { get; set; } = new List<GroupMember>();
    }
}