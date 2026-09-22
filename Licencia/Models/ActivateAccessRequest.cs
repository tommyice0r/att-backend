namespace ClientAccess.Models
{
    public class ActivateAccessRequest
    {
        public string AccessKey { get; set; } = "";
        public string? MachineId { get; set; }
        public string? AppCode { get; set; }
    }
}
