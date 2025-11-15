namespace TunewaveAPI.Models
{
    public class Enterprise
    {
        public int EnterpriseID { get; set; }
        public string EnterpriseName { get; set; } = string.Empty;
        public string? Domain { get; set; }
        public decimal RevenueShare { get; set; }
        public bool QCRequired { get; set; }
        public string Status { get; set; } = "Active";
        public int CreatedBy { get; set; }
        public int? OwnedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // Owner info
        public int? OwnerID { get; set; }
        public string OwnerName { get; set; } = string.Empty;
        public string OwnerEmail { get; set; } = string.Empty;

        // Optional: only if you plan to create from backend
        public string OwnerPasswordHash { get; set; } = string.Empty;
    }
}
