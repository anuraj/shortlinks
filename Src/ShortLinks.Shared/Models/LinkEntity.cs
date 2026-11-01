using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ShortLinks.Shared.Models
{
    public class LinkEntity
    {
        [Key]
        public required string Alias { get; set; }
        public required string LongUrl { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsExpired { get; set; }
        public bool IsCustom { get; set; }
    }
}