namespace ClientAccess.Models
{
    public class AdminListLicensesRequest
    {
        public string? AdminKey { get; set; }
    }

    public class AdminAddDaysRequest
    {
        public string? AccessKey { get; set; }
        public int? AccessId { get; set; }
        public int Days { get; set; } = 30;
        public string? AdminKey { get; set; }
    }

    public class AdminCreateLicenseRequest
    {
        public string ClientName { get; set; } = "";
        public string? AppCode { get; set; } = "att-bot";
        public string? Prefix { get; set; } = "ATT";
        public int DaysActive { get; set; } = 30;
        public int GraceDays { get; set; } = 3;
        public decimal LateFeePerDay { get; set; } = 500;
        public string? AdminKey { get; set; }
    }

    public class LicenseItemDto
    {
        public int AccessId { get; set; }
        public string ClientName { get; set; } = "";
        public string AccessKey { get; set; } = "";
        public string AppCode { get; set; } = "att-bot";
        public string AccessStatus { get; set; } = "ACTIVE";
        public string RealAccessStatus { get; set; } = "ACTIVE";
        public bool IsMaster { get; set; }
        public DateTime ExpiresAt { get; set; }
        public int DaysLeft { get; set; }
        public DateTime? LastPaymentAt { get; set; }
        public int OverdueDays { get; set; }
        public int GraceDays { get; set; }
        public decimal LateFeePerDay { get; set; }
        public decimal LateFeeAmount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class BotVerifyRequest
    {
        public string AccessKey { get; set; } = "";
        public string? AppCode { get; set; } = "att-bot";
        public int WorkerId { get; set; } = 1;
        public string? MachineName { get; set; }
    }

    public class BotVerifyResponse
    {
        public bool Success { get; set; }
        public bool CanRun { get; set; }
        public string AccessStatus { get; set; } = "";
        public string Message { get; set; } = "";
        public string ClientName { get; set; } = "";
        public int DaysLeft { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public DateTime? LastPaymentAt { get; set; }
        public bool IsMaster { get; set; }
    }

    public class AdminActionResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public object? Data { get; set; }
    }
}
