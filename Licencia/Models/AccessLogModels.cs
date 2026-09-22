namespace ClientAccess.Models
{
    public class AccessLogDto
    {
        public long LogId { get; set; }
        public string AccessKey { get; set; } = "";
        public string ClientName { get; set; } = "";
        public string MachineName { get; set; } = "";
        public string IpAddress { get; set; } = "";
        public string Endpoint { get; set; } = "";
        public string ActionStatus { get; set; } = "";
        public bool CanRun { get; set; }
        public int WorkerId { get; set; } = 1;
        public string Message { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }

    public class AdminGetLogsRequest
    {
        public string? AdminKey { get; set; }
        public int Limit { get; set; } = 100;
        public string? Search { get; set; }
    }
}
