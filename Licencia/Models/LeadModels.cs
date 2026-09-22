namespace ClientAccess.Models
{
    public class LeadModel
    {
        public int Id { get; set; }
        public int AccessId { get; set; }
        public int? SequenceId { get; set; }
        public string PhoneNumber { get; set; } = "";
        public string? FullName { get; set; }
        public string? Address { get; set; }
        public string? ZipCode { get; set; }
        public string? StateCode { get; set; }
        public string Source { get; set; } = "generator";
        public string Status { get; set; } = "pending";
        public DateTime CreatedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
    }

    public class LeadUploadRequest
    {
        public string AccessKey { get; set; } = "";
        public string LeadsText { get; set; } = ""; // formato: telefono:nombre:direccion:zip
        public string? Source { get; set; }
    }

    public class LeadQueueResponse
    {
        public int? LeadId { get; set; }
        public string? PhoneNumber { get; set; }
        public string? FullName { get; set; }
        public string? Address { get; set; }
        public string? ZipCode { get; set; }
        public string? StateCode { get; set; }
    }

    public class CountResponse
    {
        public int Count { get; set; }
        public string Status { get; set; } = "success";
    }

    public class SequenceModel
    {
        public int Id { get; set; }
        public int AccessId { get; set; }
        public string Sequence { get; set; } = "";
        public string? StateCode { get; set; }
        public string? StateName { get; set; }
        public int TotalLeads { get; set; }
        public string Status { get; set; } = "pending";
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    public class SequenceCreateRequest
    {
        public string AccessKey { get; set; } = "";
        public string Sequence { get; set; } = "";
    }

    public class HitModel
    {
        public int Id { get; set; }
        public int AccessId { get; set; }
        public int? LeadId { get; set; }
        public string PhoneNumber { get; set; } = "";
        public string? FullName { get; set; }
        public string? Address { get; set; }
        public string? ZipCode { get; set; }
        public string? DeviceMessage { get; set; }
        public string? DeviceType { get; set; }
        public string? HitType { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class HitSaveRequest
    {
        public string AccessKey { get; set; } = "";
        public int? LeadId { get; set; }
        public string PhoneNumber { get; set; } = "";
        public string? FullName { get; set; }
        public string? Address { get; set; }
        public string? ZipCode { get; set; }
        public string? DeviceMessage { get; set; }
        public string? DeviceType { get; set; }
        public string? HitType { get; set; }
        public string? ProfileRaw { get; set; }
    }

    public class DashboardStats
    {
        public int TotalLeads { get; set; }
        public int PendingLeads { get; set; }
        public int Hits { get; set; }
        public int TotalSequences { get; set; }
    }
}
