using System.Data;
using ClientAccess.DataBase;
using ClientAccess.Models;
using Microsoft.Data.SqlClient;

namespace ClientAccess.Services
{
    public class LeadService
    {
        private readonly Database _database = new Database();

        public async Task<List<LeadModel>> GetPendingLeadsAsync(int accessId, int limit = 100)
        {
            var parameters = new List<SqlParameter>
            {
                new SqlParameter("@AccessId", accessId),
                new SqlParameter("@Limit", limit)
            };

            DataTable dt = await _database.RunStoredProcedure("sp_GetPendingLeads", parameters, true);
            var leads = new List<LeadModel>();

            foreach (DataRow row in dt.Rows)
            {
                leads.Add(new LeadModel
                {
                    Id = Convert.ToInt32(row["Id"]),
                    PhoneNumber = row["PhoneNumber"].ToString() ?? "",
                    FullName = row["FullName"]?.ToString(),
                    Address = row["Address"]?.ToString(),
                    ZipCode = row["ZipCode"]?.ToString(),
                    StateCode = row["StateCode"]?.ToString()
                });
            }

            return leads;
        }

        public async Task<LeadQueueResponse?> GetNextLeadAsync(int accessId)
        {
            var parameters = new List<SqlParameter>
            {
                new SqlParameter("@AccessId", accessId)
            };

            DataTable dt = await _database.RunStoredProcedure("sp_GetNextLead", parameters, true);

            if (dt.Rows.Count == 0) return null;

            DataRow row = dt.Rows[0];
            if (row["LeadId"] == DBNull.Value) return null;

            return new LeadQueueResponse
            {
                LeadId = Convert.ToInt32(row["LeadId"]),
                PhoneNumber = row["PhoneNumber"].ToString(),
                FullName = row["FullName"]?.ToString(),
                Address = row["Address"]?.ToString(),
                ZipCode = row["ZipCode"]?.ToString(),
                StateCode = row["StateCode"]?.ToString()
            };
        }

        public async Task<int> InsertLeadAsync(int accessId, int? sequenceId, string phoneNumber, string? fullName, string? address, string? zipCode, string? stateCode, string source = "generator")
        {
            var parameters = new List<SqlParameter>
            {
                new SqlParameter("@AccessId", accessId),
                new SqlParameter("@SequenceId", (object?)sequenceId ?? DBNull.Value),
                new SqlParameter("@PhoneNumber", phoneNumber),
                new SqlParameter("@FullName", (object?)fullName ?? DBNull.Value),
                new SqlParameter("@Address", (object?)address ?? DBNull.Value),
                new SqlParameter("@ZipCode", (object?)zipCode ?? DBNull.Value),
                new SqlParameter("@StateCode", (object?)stateCode ?? DBNull.Value),
                new SqlParameter("@Source", source)
            };

            DataTable dt = await _database.RunStoredProcedure("sp_InsertLead", parameters, false);
            return 0;
        }

        public async Task<int> BulkInsertLeadsAsync(int accessId, int? sequenceId, List<LeadModel> leads, string source = "upload")
        {
            int count = 0;
            foreach (var lead in leads)
            {
                await InsertLeadAsync(accessId, sequenceId, lead.PhoneNumber, lead.FullName, lead.Address, lead.ZipCode, lead.StateCode, source);
                count++;
            }
            return count;
        }

        public async Task<int> CountLeadsAsync(int accessId, string? status = null)
        {
            var parameters = new List<SqlParameter>
            {
                new SqlParameter("@AccessId", accessId),
                new SqlParameter("@Status", (object?)status ?? DBNull.Value)
            };

            DataTable dt = await _database.RunStoredProcedure("sp_CountLeads", parameters, true);
            if (dt.Rows.Count > 0)
                return Convert.ToInt32(dt.Rows[0]["Count"]);
            return 0;
        }

        public async Task UpdateLeadStatusAsync(int leadId, string status)
        {
            var parameters = new List<SqlParameter>
            {
                new SqlParameter("@LeadId", leadId),
                new SqlParameter("@Status", status)
            };

            await _database.RunStoredProcedure("sp_UpdateLeadStatus", parameters, false);
        }

        public async Task<DashboardStats> GetDashboardStatsAsync(int accessId)
        {
            var parameters = new List<SqlParameter>
            {
                new SqlParameter("@AccessId", accessId)
            };

            DataTable dt = await _database.RunStoredProcedure("sp_GetDashboardStats", parameters, true);
            if (dt.Rows.Count == 0) return new DashboardStats();

            DataRow row = dt.Rows[0];
            return new DashboardStats
            {
                TotalLeads = Convert.ToInt32(row["TotalLeads"]),
                PendingLeads = Convert.ToInt32(row["PendingLeads"]),
                Hits = Convert.ToInt32(row["Hits"]),
                TotalSequences = Convert.ToInt32(row["TotalSequences"])
            };
        }
    }
}
