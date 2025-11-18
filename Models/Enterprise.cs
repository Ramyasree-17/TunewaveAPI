namespace TunewaveAPI.Models
{
    public class Enterprise
    {
        // ============================
        // ✔ Primary Key (AlphaNumeric)
        // Example: B01, A9C, T2X
        // ============================
        public string EnterpriseId { get; set; } = string.Empty;

        // ============================
        // ✔ Basic Details
        // ============================
        public string EnterpriseName { get; set; } = string.Empty;
        public string? Domain { get; set; }
        public decimal RevenueShare { get; set; }
        public bool QCRequired { get; set; }
        public string Status { get; set; } = "Active";

        // ============================
        // ✔ Owner Information
        // ============================
        public int? OwnerUserId { get; set; }
        public string OwnerName { get; set; } = string.Empty;
        public string OwnerEmail { get; set; } = string.Empty;

        // ============================
        // ✔ Audit Fields
        // ============================
        public int CreatedBy { get; set; }
        public int? UpdatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? UpdatedDate { get; set; }
    }
}
