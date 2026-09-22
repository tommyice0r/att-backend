namespace ClientAccess.Models
{
    public class ActivateAccessResponse
    {
        public bool Success { get; set; }
        public string? Token { get; set; }

        public int? AccessId { get; set; }
        public string ClientName { get; set; } = "";
        public string AccessKey { get; set; } = "";
        public string AppCode { get; set; } = "";

        public string AccessStatus { get; set; } = "";
        public DateTime? ExpiresAt { get; set; }
        public int DaysLeft { get; set; }
        public DateTime? LastPaymentAt { get; set; }
        public bool CanRun { get; set; }
        public bool HasPermission { get; set; }

        public int OverdueDays { get; set; }
        public decimal LateFeePerDay { get; set; }
        public decimal LateFeeAmount { get; set; }

        public string Currency { get; set; } = "USD";
        public string Message { get; set; } = "";
        public bool IsManualException { get; set; }
        public bool IsMaster { get; set; }
        public DateTime? ManualAllowUntil { get; set; }
        public string ManualAllowReason { get; set; } = "";
    }
}
