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

        public virtual ApplicationUser? CreatedBy { get; set; }
        public virtual ICollection<GroupMember> Members { get; set; } = new List<GroupMember>();
    }
}