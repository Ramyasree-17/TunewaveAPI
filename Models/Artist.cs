namespace TunewaveAPI.Models
{
    public class Artist
    {
        public int ArtistID { get; set; }
        public int LabelID { get; set; }
        public string ArtistName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Country { get; set; }
        public string? Genre { get; set; }
        public decimal RevenueShare { get; set; }
        public int CreatedBy { get; set; }
    }
}
