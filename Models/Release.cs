namespace TunewaveAPI.Models
{
    public class Release
    {
        public int ReleaseID { get; set; }
        public int? LabelID { get; set; }
        public int? ArtistID { get; set; }
        public string ReleaseTitle { get; set; } = string.Empty;
        public DateTime ReleaseDate { get; set; }
        public string? UPC { get; set; }
        public string? Status { get; set; }
        public int CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
