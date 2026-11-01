namespace ShortLinks.Shared.ViewModels
{
    public class LinkClick
    {
        public string? Alias { get; set; }
        public DateTime ClickedAt { get; set; } = DateTime.UtcNow;
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
    }
}