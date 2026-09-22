using System.Data;
using ClientAccess.DataBase;
using ClientAccess.Models;
using Npgsql;

namespace ClientAccess.Services
{
    public class ClientAccessServices
    {
        private readonly Database _database = new Database();

        public async Task<ActivateAccessResponse> ActivateAccessAsync(ActivateAccessRequest request)
        {
            string accessKey = request.AccessKey.Trim();
            string appCode = string.IsNullOrWhiteSpace(request.AppCode) ? "att-bot" : request.AppCode.Trim();

            try
            {
                var parameters = new List<NpgsqlParameter>
                {
                    new NpgsqlParameter("p_key", accessKey),
                    new NpgsqlParameter("p_app", appCode)
                };

                string sql = @"SELECT * FROM sp_activate_client_access(@p_key, @p_app);";
                DataTable dt = await _database.ExecutePostgresQueryAsync(sql, parameters);

                if (dt.Rows.Count == 0)
                {
                    string fallbackSql = @"
                        SELECT 
                            access_id, client_name, access_key, app_code, access_status,
                            last_payment_at, expires_at, grace_days, late_fee_per_day, is_master,
                            manual_allow_until, manual_allow_reason,
                            CASE 
                                WHEN is_master THEN 99999
                                WHEN CURRENT_DATE <= expires_at::DATE THEN (expires_at::DATE - CURRENT_DATE)::INT
                                ELSE 0
                            END AS days_left,
                            CASE 
                                WHEN is_master THEN 0
                                WHEN CURRENT_DATE <= expires_at::DATE THEN 0
                                ELSE (CURRENT_DATE - expires_at::DATE)::INT
                            END AS overdue_days,
                            CASE 
                                WHEN is_master THEN 'ACTIVE'
                                WHEN access_status = 'DISABLED' THEN 'BLOCKED'
                                WHEN CURRENT_DATE <= expires_at::DATE THEN 'ACTIVE'
                                WHEN manual_allow_until IS NOT NULL AND CURRENT_TIMESTAMP <= manual_allow_until THEN 'EXCEPTION'
                                WHEN (CURRENT_DATE - expires_at::DATE)::INT <= grace_days THEN 'GRACE'
                                ELSE 'BLOCKED'
                            END AS real_access_status,
                            CASE 
                                WHEN is_master THEN 0::NUMERIC
                                WHEN CURRENT_DATE <= expires_at::DATE THEN 0::NUMERIC
                                ELSE ((CURRENT_DATE - expires_at::DATE) * late_fee_per_day)::NUMERIC
                            END AS late_fee_amount,
                            CASE 
                                WHEN NOT is_master AND manual_allow_until IS NOT NULL AND CURRENT_TIMESTAMP <= manual_allow_until AND CURRENT_DATE > expires_at::DATE THEN TRUE
                                ELSE FALSE
                            END AS is_manual_exception,
                            CASE
                                WHEN is_master THEN TRUE
                                WHEN access_status = 'DISABLED' THEN FALSE
                                WHEN CURRENT_DATE <= expires_at::DATE THEN TRUE
                                WHEN manual_allow_until IS NOT NULL AND CURRENT_TIMESTAMP <= manual_allow_until THEN TRUE
                                WHEN (CURRENT_DATE - expires_at::DATE)::INT <= grace_days THEN TRUE
                                ELSE FALSE
                            END AS can_run
                        FROM client_access_records
                        WHERE access_key = @p_key AND app_code = @p_app
                        LIMIT 1;";
                    
                    var fallbackParams = new List<NpgsqlParameter>
                    {
                        new NpgsqlParameter("p_key", accessKey),
                        new NpgsqlParameter("p_app", appCode)
                    };
                    dt = await _database.ExecutePostgresQueryAsync(fallbackSql, fallbackParams);
                }

                if (dt.Rows.Count == 0)
                {
                    return new ActivateAccessResponse
                    {
                        Success = false,
                        CanRun = false,
                        HasPermission = false,
                        AccessStatus = "INVALID",
                        Message = "Clave de acceso inválida o no registrada."
                    };
                }

                DataRow row = dt.Rows[0];

                bool isMaster = row.Table.Columns.Contains("is_master") && row["is_master"] != DBNull.Value && Convert.ToBoolean(row["is_master"]);
                string realStatus = row["real_access_status"]?.ToString() ?? "BLOCKED";
                if (isMaster) realStatus = "ACTIVE";

                bool canRun = isMaster || (row.Table.Columns.Contains("can_run") && row["can_run"] != DBNull.Value && Convert.ToBoolean(row["can_run"]))
                              || realStatus is "ACTIVE" or "GRACE" or "EXCEPTION";

                DateTime expiresAt = Convert.ToDateTime(row["expires_at"]);
                int daysLeft = isMaster ? 99999 : (row.Table.Columns.Contains("days_left") && row["days_left"] != DBNull.Value
                    ? Convert.ToInt32(row["days_left"])
                    : Math.Max(0, (expiresAt.Date - DateTime.UtcNow.Date).Days));

                DateTime? lastPaymentAt = row.Table.Columns.Contains("last_payment_at") && row["last_payment_at"] != DBNull.Value
                    ? Convert.ToDateTime(row["last_payment_at"])
                    : null;

                decimal lateFeePerDay = Convert.ToDecimal(row["late_fee_per_day"]);
                decimal lateFeeAmount = isMaster ? 0 : Convert.ToDecimal(row["late_fee_amount"]);
                int overdueDays = isMaster ? 0 : Convert.ToInt32(row["overdue_days"]);
                string currency = "USD";

                return new ActivateAccessResponse
                {
                    Success = canRun,
                    CanRun = canRun,
                    HasPermission = canRun,
                    Token = canRun ? Guid.NewGuid().ToString("N") : null,
                    AccessId = Convert.ToInt32(row["access_id"]),
                    ClientName = row["client_name"].ToString() ?? "",
                    AccessKey = row["access_key"].ToString() ?? "",
                    AppCode = row["app_code"].ToString() ?? "",
                    AccessStatus = realStatus,
                    IsMaster = isMaster,
                    ExpiresAt = expiresAt,
                    DaysLeft = daysLeft,
                    LastPaymentAt = lastPaymentAt,
                    OverdueDays = overdueDays,
                    LateFeePerDay = lateFeePerDay,
                    LateFeeAmount = lateFeeAmount,
                    Currency = currency,
                    IsManualException = row["is_manual_exception"] != DBNull.Value && Convert.ToBoolean(row["is_manual_exception"]),
                    ManualAllowUntil = row["manual_allow_until"] == DBNull.Value ? null : Convert.ToDateTime(row["manual_allow_until"]),
                    ManualAllowReason = row["manual_allow_reason"]?.ToString() ?? "",

                    Message = isMaster 
                        ? "Licencia Maestra activa sin restricciones." 
                        : realStatus switch
                        {
                            "ACTIVE" => $"Licencia activa. Te quedan {daysLeft} días de servicio.",
                            "GRACE" => $"Pago vencido. Tienes {overdueDays} día(s) de atraso. Período de gracia activo.",
                            "EXCEPTION" => $"Acceso temporal habilitado por excepción manual.",
                            "BLOCKED" => $"Acceso bloqueado por vencimiento ({overdueDays} días de atraso). Recarga días para continuar.",
                            _ => "Estado de licencia no reconocido."
                        }
                };
            }
            catch (Exception ex)
            {
                return new ActivateAccessResponse
                {
                    Success = false,
                    CanRun = false,
                    HasPermission = false,
                    AccessStatus = "ERROR",
                    Message = $"Error conectando al servidor global de licencias: {ex.Message}"
                };
            }
        }

        // ========================================================
        // VERIFICACIÓN DIRECTA PARA BOTS (1 SOLA LLAMADA ULTRA RÁPIDA)
        // ========================================================
        public async Task<BotVerifyResponse> VerifyBotAccessAsync(BotVerifyRequest req)
        {
            string accessKey = req.AccessKey.Trim();
            string appCode = string.IsNullOrWhiteSpace(req.AppCode) ? "att-bot" : req.AppCode.Trim();

            try
            {
                var parameters = new List<NpgsqlParameter>
                {
                    new NpgsqlParameter("p_key", accessKey),
                    new NpgsqlParameter("p_app", appCode),
                    new NpgsqlParameter("p_worker", req.WorkerId),
                    new NpgsqlParameter("p_machine", (object?)req.MachineName ?? DBNull.Value)
                };

                string sql = "SELECT * FROM sp_verify_bot_access(@p_key, @p_app, @p_worker, @p_machine);";
                DataTable dt = await _database.ExecutePostgresQueryAsync(sql, parameters);

                if (dt.Rows.Count > 0)
                {
                    DataRow r = dt.Rows[0];
                    return new BotVerifyResponse
                    {
                        Success = true,
                        CanRun = Convert.ToBoolean(r["can_run"]),
                        AccessStatus = r["access_status"]?.ToString() ?? "BLOCKED",
                        Message = r["message"]?.ToString() ?? "",
                        ClientName = r["client_name"]?.ToString() ?? "",
                        DaysLeft = Convert.ToInt32(r["days_left"]),
                        ExpiresAt = r["expires_at"] == DBNull.Value ? null : Convert.ToDateTime(r["expires_at"]),
                        LastPaymentAt = r["last_payment_at"] == DBNull.Value ? null : Convert.ToDateTime(r["last_payment_at"]),
                        IsMaster = Convert.ToBoolean(r["is_master"])
                    };
                }
            }
            catch
            {
                // Fallback a ActivateAccessAsync si sp_verify_bot_access aún no existe
            }

            var act = await ActivateAccessAsync(new ActivateAccessRequest
            {
                AccessKey = accessKey,
                AppCode = appCode
            });

            return new BotVerifyResponse
            {
                Success = act.Success,
                CanRun = act.CanRun,
                AccessStatus = act.AccessStatus,
                Message = act.Message,
                ClientName = act.ClientName,
                DaysLeft = act.DaysLeft,
                ExpiresAt = act.ExpiresAt,
                LastPaymentAt = act.LastPaymentAt,
                IsMaster = act.IsMaster
            };
        }

        // ========================================================
        // MÉTODOS DE ADMINISTRACIÓN (SUPER ADMIN / LICENCIA MAESTRA)
        // ========================================================

        public async Task<bool> ValidateAdminKeyAsync(string? key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return false;

            string cleanKey = key.Trim();

            // 1. Validar contra clave maestra de configuración o entorno
            string masterEnv = Environment.GetEnvironmentVariable("AdminSettings__MasterKey") ?? "ATT-MASTER-ADMIN-2026";
            if (string.Equals(cleanKey, masterEnv, StringComparison.OrdinalIgnoreCase))
                return true;

            // 2. Validar contra la base de datos PostgreSQL: registro con is_master = true y activo
            try
            {
                var parameters = new List<NpgsqlParameter>
                {
                    new NpgsqlParameter("p_key", cleanKey)
                };

                string sql = "SELECT is_master, access_status FROM client_access_records WHERE access_key = @p_key LIMIT 1;";
                DataTable dt = await _database.ExecutePostgresQueryAsync(sql, parameters);

                if (dt.Rows.Count > 0)
                {
                    DataRow row = dt.Rows[0];
                    bool isMaster = row["is_master"] != DBNull.Value && Convert.ToBoolean(row["is_master"]);
                    string status = row["access_status"]?.ToString() ?? "";
                    if (isMaster && !string.Equals(status, "DISABLED", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // En caso de fallo de conexión de BD, no otorgar acceso no autenticado
            }

            return false;
        }

        public async Task<List<LicenseItemDto>> GetAllLicensesAsync()
        {
            string sql = @"
                SELECT 
                    access_id, client_name, access_key, app_code, access_status,
                    last_payment_at, expires_at, grace_days, late_fee_per_day, is_master, created_at,
                    CASE 
                        WHEN is_master THEN 99999
                        WHEN CURRENT_DATE <= expires_at::DATE THEN (expires_at::DATE - CURRENT_DATE)::INT
                        ELSE 0
                    END AS days_left,
                    CASE 
                        WHEN is_master THEN 0
                        WHEN CURRENT_DATE <= expires_at::DATE THEN 0
                        ELSE (CURRENT_DATE - expires_at::DATE)::INT
                    END AS overdue_days,
                    CASE 
                        WHEN is_master THEN 'ACTIVE'
                        WHEN access_status = 'DISABLED' THEN 'BLOCKED'
                        WHEN CURRENT_DATE <= expires_at::DATE THEN 'ACTIVE'
                        WHEN manual_allow_until IS NOT NULL AND CURRENT_TIMESTAMP <= manual_allow_until THEN 'EXCEPTION'
                        WHEN (CURRENT_DATE - expires_at::DATE)::INT <= grace_days THEN 'GRACE'
                        ELSE 'BLOCKED'
                    END AS real_access_status,
                    CASE 
                        WHEN is_master THEN 0::NUMERIC
                        WHEN CURRENT_DATE <= expires_at::DATE THEN 0::NUMERIC
                        ELSE ((CURRENT_DATE - expires_at::DATE) * late_fee_per_day)::NUMERIC
                    END AS late_fee_amount
                FROM client_access_records
                ORDER BY is_master DESC, access_id DESC;";

            DataTable dt = await _database.ExecutePostgresQueryAsync(sql);
            var list = new List<LicenseItemDto>();

            foreach (DataRow row in dt.Rows)
            {
                list.Add(new LicenseItemDto
                {
                    AccessId = Convert.ToInt32(row["access_id"]),
                    ClientName = row["client_name"].ToString() ?? "",
                    AccessKey = row["access_key"].ToString() ?? "",
                    AppCode = row["app_code"].ToString() ?? "att-bot",
                    AccessStatus = row["access_status"].ToString() ?? "ACTIVE",
                    RealAccessStatus = row["real_access_status"].ToString() ?? "ACTIVE",
                    IsMaster = row["is_master"] != DBNull.Value && Convert.ToBoolean(row["is_master"]),
                    ExpiresAt = Convert.ToDateTime(row["expires_at"]),
                    DaysLeft = Convert.ToInt32(row["days_left"]),
                    LastPaymentAt = row["last_payment_at"] != DBNull.Value ? Convert.ToDateTime(row["last_payment_at"]) : null,
                    OverdueDays = Convert.ToInt32(row["overdue_days"]),
                    GraceDays = Convert.ToInt32(row["grace_days"]),
                    LateFeePerDay = Convert.ToDecimal(row["late_fee_per_day"]),
                    LateFeeAmount = Convert.ToDecimal(row["late_fee_amount"]),
                    CreatedAt = row["created_at"] != DBNull.Value ? Convert.ToDateTime(row["created_at"]) : DateTime.UtcNow
                });
            }

            return list;
        }

        public async Task<(bool success, string message, DateTime? newExp, int daysLeft, DateTime? lastPayment)> AddDaysAsync(string accessKey, int days)
        {
            if (string.IsNullOrWhiteSpace(accessKey))
                return (false, "AccessKey requerida.", null, 0, null);

            var parameters = new List<NpgsqlParameter>
            {
                new NpgsqlParameter("p_key", accessKey.Trim()),
                new NpgsqlParameter("p_days", days)
            };

            string sql = "SELECT * FROM sp_add_license_days(@p_key, @p_days);";
            try
            {
                DataTable dt = await _database.ExecutePostgresQueryAsync(sql, parameters);
                if (dt.Rows.Count > 0)
                {
                    bool success = Convert.ToBoolean(dt.Rows[0]["success"]);
                    string message = dt.Rows[0]["message"].ToString() ?? "";
                    DateTime? exp = dt.Rows[0]["new_expires_at"] == DBNull.Value ? null : Convert.ToDateTime(dt.Rows[0]["new_expires_at"]);
                    int daysLeft = dt.Rows[0]["days_left"] == DBNull.Value ? 0 : Convert.ToInt32(dt.Rows[0]["days_left"]);
                    DateTime? lastPay = dt.Rows[0]["last_payment_at"] == DBNull.Value ? null : Convert.ToDateTime(dt.Rows[0]["last_payment_at"]);

                    return (success, message, exp, daysLeft, lastPay);
                }
            }
            catch
            {
                // Fallback directo si la función no estuviera creada aún
                string fallbackSql = @"
                    UPDATE client_access_records
                    SET expires_at = CASE 
                            WHEN expires_at < CURRENT_TIMESTAMP THEN CURRENT_TIMESTAMP + (@p_days || ' days')::INTERVAL
                            ELSE expires_at + (@p_days || ' days')::INTERVAL
                        END,
                        access_status = 'ACTIVE',
                        last_payment_at = CURRENT_TIMESTAMP,
                        updated_at = CURRENT_TIMESTAMP
                    WHERE access_key = @p_key
                    RETURNING expires_at, last_payment_at;";

                var fallbackParams = new List<NpgsqlParameter>
                {
                    new NpgsqlParameter("p_key", accessKey.Trim()),
                    new NpgsqlParameter("p_days", days)
                };

                DataTable dt = await _database.ExecutePostgresQueryAsync(fallbackSql, fallbackParams);
                if (dt.Rows.Count > 0)
                {
                    DateTime exp = Convert.ToDateTime(dt.Rows[0]["expires_at"]);
                    DateTime lastPay = Convert.ToDateTime(dt.Rows[0]["last_payment_at"]);
                    int daysLeft = Math.Max(0, (exp.Date - DateTime.UtcNow.Date).Days);
                    return (true, $"Se sumaron {days} días con éxito.", exp, daysLeft, lastPay);
                }
            }

            return (false, "Licencia no encontrada.", null, 0, null);
        }

        public async Task<LicenseItemDto?> CreateLicenseAsync(AdminCreateLicenseRequest req)
        {
            string prefix = string.IsNullOrWhiteSpace(req.Prefix) ? "ATT" : req.Prefix.Trim().ToUpper();
            string appCode = string.IsNullOrWhiteSpace(req.AppCode) ? "att-bot" : req.AppCode.Trim();

            string newKey = $"{prefix}-{Guid.NewGuid().ToString("N").Substring(0, 4).ToUpper()}-{Guid.NewGuid().ToString("N").Substring(0, 4).ToUpper()}-{Guid.NewGuid().ToString("N").Substring(0, 4).ToUpper()}";
            DateTime exp = DateTime.UtcNow.AddDays(req.DaysActive > 0 ? req.DaysActive : 30);
            DateTime now = DateTime.UtcNow;

            string sql = @"
                INSERT INTO client_access_records (
                    client_name, access_key, app_code, access_status, is_master,
                    last_payment_at, expires_at, grace_days, late_fee_per_day,
                    created_at, updated_at
                ) VALUES (
                    @client_name, @access_key, @app_code, 'ACTIVE', FALSE,
                    @now, @expires_at, @grace_days, @late_fee_per_day,
                    @now, @now
                )
                RETURNING access_id, client_name, access_key, app_code, access_status, last_payment_at, expires_at, created_at;";

            var parameters = new List<NpgsqlParameter>
            {
                new NpgsqlParameter("client_name", req.ClientName.Trim()),
                new NpgsqlParameter("access_key", newKey),
                new NpgsqlParameter("app_code", appCode),
                new NpgsqlParameter("now", now),
                new NpgsqlParameter("expires_at", exp),
                new NpgsqlParameter("grace_days", req.GraceDays),
                new NpgsqlParameter("late_fee_per_day", req.LateFeePerDay)
            };

            DataTable dt = await _database.ExecutePostgresQueryAsync(sql, parameters);
            if (dt.Rows.Count > 0)
            {
                DataRow row = dt.Rows[0];
                return new LicenseItemDto
                {
                    AccessId = Convert.ToInt32(row["access_id"]),
                    ClientName = row["client_name"].ToString() ?? "",
                    AccessKey = row["access_key"].ToString() ?? "",
                    AppCode = row["app_code"].ToString() ?? "",
                    AccessStatus = "ACTIVE",
                    RealAccessStatus = "ACTIVE",
                    IsMaster = false,
                    ExpiresAt = Convert.ToDateTime(row["expires_at"]),
                    DaysLeft = Math.Max(0, (exp.Date - DateTime.UtcNow.Date).Days),
                    LastPaymentAt = now,
                    OverdueDays = 0,
                    GraceDays = req.GraceDays,
                    LateFeePerDay = req.LateFeePerDay,
                    LateFeeAmount = 0,
                    CreatedAt = Convert.ToDateTime(row["created_at"])
                };
            }

            return null;
        }

        // ========================================================
        // AUDITORÍA Y LOGS DE ACCESOS EN TIEMPO REAL
        // ========================================================

        public void LogAccessAsync(string accessKey, string? clientName, string? machineName, string? ipAddress, string endpoint, string actionStatus, bool canRun, int workerId, string? message)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    string sql = @"
                        INSERT INTO access_logs (
                            access_key, client_name, machine_name, ip_address, 
                            endpoint, action_status, can_run, worker_id, message, created_at
                        ) VALUES (
                            @access_key, @client_name, @machine_name, @ip_address,
                            @endpoint, @action_status, @can_run, @worker_id, @message, CURRENT_TIMESTAMP
                        );";

                    var parameters = new List<NpgsqlParameter>
                    {
                        new NpgsqlParameter("access_key", (object?)accessKey ?? ""),
                        new NpgsqlParameter("client_name", (object?)clientName ?? DBNull.Value),
                        new NpgsqlParameter("machine_name", (object?)machineName ?? DBNull.Value),
                        new NpgsqlParameter("ip_address", (object?)ipAddress ?? DBNull.Value),
                        new NpgsqlParameter("endpoint", (object?)endpoint ?? ""),
                        new NpgsqlParameter("action_status", (object?)actionStatus ?? "UNKNOWN"),
                        new NpgsqlParameter("can_run", canRun),
                        new NpgsqlParameter("worker_id", workerId),
                        new NpgsqlParameter("message", (object?)message ?? DBNull.Value)
                    };

                    await _database.ExecutePostgresNonQueryAsync(sql, parameters);
                }
                catch
                {
                    // Registro silencioso en segundo plano sin interrumpir operaciones
                }
            });
        }

        public async Task<List<AccessLogDto>> GetAccessLogsAsync(int limit = 100, string? search = null)
        {
            string sql;
            var parameters = new List<NpgsqlParameter>
            {
                new NpgsqlParameter("limit", limit > 0 ? limit : 100)
            };

            if (!string.IsNullOrWhiteSpace(search))
            {
                sql = @"
                    SELECT log_id, access_key, client_name, machine_name, ip_address, 
                           endpoint, action_status, can_run, worker_id, message, created_at
                    FROM access_logs
                    WHERE access_key ILIKE @search 
                       OR client_name ILIKE @search 
                       OR machine_name ILIKE @search 
                       OR ip_address ILIKE @search
                       OR action_status ILIKE @search
                    ORDER BY created_at DESC
                    LIMIT @limit;";
                parameters.Add(new NpgsqlParameter("search", $"%{search.Trim()}%"));
            }
            else
            {
                sql = @"
                    SELECT log_id, access_key, client_name, machine_name, ip_address, 
                           endpoint, action_status, can_run, worker_id, message, created_at
                    FROM access_logs
                    ORDER BY created_at DESC
                    LIMIT @limit;";
            }

            DataTable dt = await _database.ExecutePostgresQueryAsync(sql, parameters);
            var logs = new List<AccessLogDto>();

            foreach (DataRow row in dt.Rows)
            {
                logs.Add(new AccessLogDto
                {
                    LogId = Convert.ToInt64(row["log_id"]),
                    AccessKey = row["access_key"]?.ToString() ?? "",
                    ClientName = row["client_name"]?.ToString() ?? "",
                    MachineName = row["machine_name"]?.ToString() ?? "",
                    IpAddress = row["ip_address"]?.ToString() ?? "",
                    Endpoint = row["endpoint"]?.ToString() ?? "",
                    ActionStatus = row["action_status"]?.ToString() ?? "",
                    CanRun = row["can_run"] != DBNull.Value && Convert.ToBoolean(row["can_run"]),
                    WorkerId = row["worker_id"] != DBNull.Value ? Convert.ToInt32(row["worker_id"]) : 1,
                    Message = row["message"]?.ToString() ?? "",
                    CreatedAt = Convert.ToDateTime(row["created_at"])
                });
            }

            return logs;
        }
    }
}
