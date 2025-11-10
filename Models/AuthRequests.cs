namespace TunewaveAPI.Models
{
    public class RegisterRequest
    {
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? Role { get; set; }
        public string? MobileNumber { get; set; }
        public string? CountryCode { get; set; }
        public decimal ThresholdValue { get; set; }
        public decimal RevenueShare { get; set; }
        public DateTime? AgreementStartDate { get; set; }
        public DateTime? AgreementEndDate { get; set; }
        public string? PublicProfileName { get; set; }
        public string? LabelName { get; set; }
    }

    public class LoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
