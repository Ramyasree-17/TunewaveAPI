namespace TunewaveAPI.Models
{
    public class LabelCreateRequest
    {
        public int EnterpriseID { get; set; }
        public string LabelName { get; set; } = string.Empty;
        public string? PlanType { get; set; }
        public bool QCRequired { get; set; }
        public string? Domain { get; set; }
        public decimal RevenueShare { get; set; }
    }

    public class LabelUpdateRequest
    {
        public string? Domain { get; set; }
        public bool? QCRequired { get; set; }
        public decimal? RevenueShare { get; set; }
        public string? Status { get; set; }
    }

    public class LabelTransferRequest
    {
        public int LabelId { get; set; }
        public int OldEnterpriseId { get; set; }    // used only for direct transfer by SuperAdmin
        public int NewEnterpriseId { get; set; }    // target enterprise
        public string Reason { get; set; } = string.Empty;
    }

    public class LabelStatusRequest
    {
        public int LabelId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Remarks { get; set; }
    }
}
