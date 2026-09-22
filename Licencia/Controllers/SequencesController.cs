using ClientAccess.Models;
using ClientAccess.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClientAccess.Controllers
{
    [Route("Api/[controller]/secure")]
    [ApiController]
    public class SequencesController : ControllerBase
    {
        private readonly SequenceService _sequenceService = new SequenceService();
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

        [HttpPost("create")]
        public async Task<IActionResult> CreateSequence([FromBody] SequenceCreateRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.AccessKey))
                return BadRequest(new { success = false, message = "AccessKey requerida." });

            var accessId = await ValidateAndGetAccessId(request.AccessKey);
            if (accessId == null)
                return Unauthorized(new { success = false, message = "Acceso bloqueado o inválido." });

            if (string.IsNullOrWhiteSpace(request.Sequence))
                return BadRequest(new { success = false, message = "Secuencia requerida." });

            var seqId = await _sequenceService.CreateSequenceAsync(accessId.Value, request.Sequence.Trim());
            return Ok(new { success = true, sequenceId = seqId });
        }

        [HttpPost("list")]
        public async Task<IActionResult> GetSequences([FromBody] AccessKeyRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.AccessKey))
                return BadRequest(new { success = false, message = "AccessKey requerida." });

            var accessId = await ValidateAndGetAccessId(request.AccessKey);
            if (accessId == null)
                return Unauthorized(new { success = false, message = "Acceso bloqueado o inválido." });

            var sequences = await _sequenceService.GetSequencesAsync(accessId.Value);
            return Ok(new { success = true, data = sequences });
        }

        [HttpPost("claim")]
        public async Task<IActionResult> ClaimSequence([FromBody] AccessKeyRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.AccessKey))
                return BadRequest(new { success = false, message = "AccessKey requerida." });

            var accessId = await ValidateAndGetAccessId(request.AccessKey);
            if (accessId == null)
                return Unauthorized(new { success = false, message = "Acceso bloqueado o inválido." });

            var seq = await _sequenceService.ClaimSequenceAsync(accessId.Value);
            if (seq == null)
                return Ok(new { success = true, data = (object?)null, message = "No hay secuencias pendientes." });

            return Ok(new { success = true, data = seq });
        }

        [HttpPost("complete")]
        public async Task<IActionResult> CompleteSequence([FromBody] CompleteSequenceRequest request)
        {
            if (request.SequenceId <= 0)
                return BadRequest(new { success = false, message = "SequenceId inválido." });

            await _sequenceService.CompleteSequenceAsync(request.SequenceId, request.TotalLeads, request.Status);
            return Ok(new { success = true });
        }
    }

    public class CompleteSequenceRequest
    {
        public int SequenceId { get; set; }
        public int TotalLeads { get; set; }
        public string Status { get; set; } = "completed";
    }
}
