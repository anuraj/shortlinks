using System.ComponentModel.DataAnnotations;

namespace ShortLinks.Shared.ViewModels
{
    public class Link
    {
        [MaxLength(256), RegularExpression("^[a-zA-Z0-9_-]*$"), MinLength(3), StringLength(256)]
        public string? Alias { get; set; }
        [DataType(DataType.Url)]
        [Required, Url, MaxLength(2048), StringLength(2048)]
        public required string LongUrl { get; set; }
        [DataType(DataType.DateTime)]
        public DateTime? ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsExpired { get; set; }
        public bool IsCustom { get; set; }
    }

    public class LinkClick
    {
        public string? Alias { get; set; }
        public DateTime ClickedAt { get; set; } = DateTime.UtcNow;
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
    }
}