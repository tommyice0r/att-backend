using ClientAccess.Models;
using ClientAccess.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClientAccess.Controllers
{
    [Route("Api/[controller]")]
    [ApiController]
    public class ClientAccessController : ControllerBase
    {
        private readonly ClientAccessServices _clientAccessServices = new ClientAccessServices();

        // POST: /Api/ClientAccess/secure/activate
        [HttpPost("secure/activate")]
        public async Task<IActionResult> Activate([FromBody] ActivateAccessRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.AccessKey))
            {
                return BadRequest(new
                {
                    success = false,
                    canRun = false,
                    hasPermission = false,
                    accessStatus = "INVALID",
                    message = "Clave de acceso requerida."
                });
            }

            var result = await _clientAccessServices.ActivateAccessAsync(request);

            if (result.AccessStatus == "INVALID")
            {
                return Unauthorized(result);
            }

            return Ok(result);
        }

        // ========================================================
        // ENDPOINT PARA BOTS: COMPROBACIÓN DE PERMISOS ANTES DE EJECUTAR
        // ========================================================

        // POST: /Api/ClientAccess/bot/verify
        // POST: /Api/ClientAccess/secure/check
        [HttpPost("bot/verify")]
        [HttpPost("secure/check")]
        public async Task<IActionResult> VerifyBot([FromBody] BotVerifyRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.AccessKey))
            {
                return BadRequest(new
                {
                    success = false,
                    canRun = false,
                    accessStatus = "INVALID",
                    message = "Clave de acceso (accessKey) requerida."
                });
            }

            var result = await _clientAccessServices.VerifyBotAccessAsync(request);

            if (!result.CanRun)
            {
                // Retorna 403 Forbidden para que el bot entienda de inmediato que debe detenerse
                return StatusCode(StatusCodes.Status403Forbidden, result);
            }

            return Ok(result);
        }

        // GET: /Api/ClientAccess/bot/verify?accessKey=ATT-XXXX-XXXX&workerId=1
        [HttpGet("bot/verify")]
        public async Task<IActionResult> VerifyBotGet([FromQuery] string? accessKey, [FromQuery] int workerId = 1, [FromQuery] string? machineName = null)
        {
            if (string.IsNullOrWhiteSpace(accessKey))
            {
                return BadRequest(new
                {
                    success = false,
                    canRun = false,
                    accessStatus = "INVALID",
                    message = "Parámetro 'accessKey' requerido en la URL."
                });
            }

            var result = await _clientAccessServices.VerifyBotAccessAsync(new BotVerifyRequest
            {
                AccessKey = accessKey,
                WorkerId = workerId,
                MachineName = machineName
            });

            if (!result.CanRun)
            {
                return StatusCode(StatusCodes.Status403Forbidden, result);
            }

            return Ok(result);
        }

        // ========================================================
        // ENDPOINTS ADMINISTRATIVOS (LICENCIA MAESTRA)
        // ========================================================

        // POST: /Api/ClientAccess/admin/list
        [HttpPost("admin/list")]
        [HttpGet("admin/list")]
        public async Task<IActionResult> ListAllLicenses()
        {
            try
            {
                var licenses = await _clientAccessServices.GetAllLicensesAsync();
                return Ok(new
                {
                    success = true,
                    count = licenses.Count,
                    data = licenses
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = $"Error obteniendo licencias: {ex.Message}"
                });
            }
        }

        // POST: /Api/ClientAccess/admin/add-days
        [HttpPost("admin/add-days")]
        public async Task<IActionResult> AddDays([FromBody] AdminAddDaysRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.AccessKey))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "AccessKey es requerida."
                });
            }

            if (request.Days <= 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "La cantidad de días debe ser mayor a 0."
                });
            }

            try
            {
                var result = await _clientAccessServices.AddDaysAsync(request.AccessKey, request.Days);
                if (!result.success)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = result.message
                    });
                }

                return Ok(new
                {
                    success = true,
                    message = result.message,
                    expiresAt = result.newExp,
                    daysLeft = result.daysLeft,
                    lastPaymentAt = result.lastPayment
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = $"Error al sumar días: {ex.Message}"
                });
            }
        }

        // POST: /Api/ClientAccess/admin/create
        [HttpPost("admin/create")]
        public async Task<IActionResult> CreateLicense([FromBody] AdminCreateLicenseRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.ClientName))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Nombre del cliente es requerido."
                });
            }

            try
            {
                var created = await _clientAccessServices.CreateLicenseAsync(request);
                if (created == null)
                {
                    return StatusCode(500, new
                    {
                        success = false,
                        message = "No se pudo crear la licencia."
                    });
                }

                return Ok(new
                {
                    success = true,
                    message = "Licencia creada con éxito.",
                    data = created
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = $"Error al crear licencia: {ex.Message}"
                });
            }
        }
    }
}