using ClientAccess.Models;
using ClientAccess.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClientAccess.Controllers
{
    [Route("Api/[controller]/secure")]
    [ApiController]
    public class LeadsController : ControllerBase
    {
        private readonly LeadService _leadService = new LeadService();
        private readonly ClientAccessServices _clientAccessServices = new ClientAccessServices();

        private async Task<int?> ValidateAndGetAccessId(string accessKey)
        {
            var validation = await _clientAccessServices.ActivateAccessAsync(new ActivateAccessRequest
            {
                AccessKey = accessKey,
                AppCode = "att-bot"
            });

            if (validation.AccessStatus is "BLOCKED" or "INVALID")
                return null;

            return validation.AccessId;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadLeads([FromBody] LeadUploadRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.AccessKey))
                return BadRequest(new { success = false, message = "AccessKey requerida." });

            var accessId = await ValidateAndGetAccessId(request.AccessKey);
            if (accessId == null)
                return Unauthorized(new { success = false, message = "Acceso bloqueado o inválido." });

            if (string.IsNullOrWhiteSpace(request.LeadsText))
                return BadRequest(new { success = false, message = "No hay leads para procesar." });

            var lines = request.LeadsText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var leads = new List<LeadModel>();
            int imported = 0;

            foreach (var line in lines)
            {
                var parts = line.Trim().Split(':');
                if (parts.Length < 1 || string.IsNullOrWhiteSpace(parts[0])) continue;

                var lead = new LeadModel
                {
                    PhoneNumber = parts[0].Trim(),
                    FullName = parts.Length > 1 ? parts[1].Trim() : null,
                    Address = parts.Length > 2 ? parts[2].Trim() : null,
                    ZipCode = parts.Length > 3 ? parts[3].Trim() : null
                };
                leads.Add(lead);
            }

            imported = await _leadService.BulkInsertLeadsAsync(accessId.Value, null, leads, request.Source ?? "upload");

            return Ok(new { success = true, imported, message = $"Se importaron {imported} leads." });
        }

        [HttpPost("count")]
        public async Task<IActionResult> CountLeads([FromBody] AccessKeyRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.AccessKey))
                return BadRequest(new { success = false, message = "AccessKey requerida." });

            var accessId = await ValidateAndGetAccessId(request.AccessKey);
            if (accessId == null)
                return Unauthorized(new { success = false, message = "Acceso bloqueado o inválido." });

            var total = await _leadService.CountLeadsAsync(accessId.Value);
            var pending = await _leadService.CountLeadsAsync(accessId.Value, "pending");

            return Ok(new { success = true, total, pending });
        }

        [HttpPost("next")]
        public async Task<IActionResult> GetNextLead([FromBody] AccessKeyRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.AccessKey))
                return BadRequest(new { success = false, message = "AccessKey requerida." });

            var accessId = await ValidateAndGetAccessId(request.AccessKey);
            if (accessId == null)
                return Unauthorized(new { success = false, message = "Acceso bloqueado o inválido." });

            var lead = await _leadService.GetNextLeadAsync(accessId.Value);
            if (lead == null)
                return Ok(new { success = true, data = (object?)null, message = "No hay leads pendientes." });

            return Ok(new { success = true, data = lead });
        }

        [HttpPost("update-status")]
        public async Task<IActionResult> UpdateLeadStatus([FromBody] UpdateLeadStatusRequest request)
        {
            if (request.LeadId <= 0)
                return BadRequest(new { success = false, message = "LeadId inválido." });

            await _leadService.UpdateLeadStatusAsync(request.LeadId, request.Status);
            return Ok(new { success = true });
        }
    }

    public class UpdateLeadStatusRequest
    {
        public int LeadId { get; set; }
        public string Status { get; set; } = "";
    }
}
