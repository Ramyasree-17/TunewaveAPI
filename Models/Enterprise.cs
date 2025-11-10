namespace TunewaveAPI.Models
{
    public class Enterprise
    {
        public int EnterpriseID { get; set; }
        public string EnterpriseName { get; set; } = string.Empty;
        public string? Domain { get; set; }
        public decimal RevenueShare { get; set; }
        public bool QCRequired { get; set; }
        public int CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
