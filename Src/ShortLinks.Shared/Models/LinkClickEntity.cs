using System.ComponentModel.DataAnnotations;

namespace ShortLinks.Shared.Models
{
    public class LinkClickEntity
    {
        [Key]
        public int Id { get; set; }
        public required string Alias { get; set; }
        public DateTime ClickedAt { get; set; } = DateTime.UtcNow;
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
    }
}