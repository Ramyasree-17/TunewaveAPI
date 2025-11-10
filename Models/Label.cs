namespace TunewaveAPI.Models
{
    public class Label
    {
        public int LabelID { get; set; }
        public int EnterpriseID { get; set; }
        public string LabelName { get; set; } = string.Empty;
        public string? Domain { get; set; }
        public string? PlanType { get; set; }
        public decimal RevenueShare { get; set; }
        public bool QCRequired { get; set; }
        public int CreatedBy { get; set; }
    }
}
