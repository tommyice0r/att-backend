using System.Data;
using ClientAccess.DataBase;
using ClientAccess.Models;
using Microsoft.Data.SqlClient;

namespace ClientAccess.Services
{
    public class SequenceService
    {
        private readonly Database _database = new Database();

        public async Task<int> CreateSequenceAsync(int accessId, string sequence, string? stateCode = null, string? stateName = null)
        {
            var parameters = new List<SqlParameter>
            {
                new SqlParameter("@AccessId", accessId),
                new SqlParameter("@Sequence", sequence),
                new SqlParameter("@StateCode", (object?)stateCode ?? DBNull.Value),
                new SqlParameter("@StateName", (object?)stateName ?? DBNull.Value)
            };

            DataTable dt = await _database.RunStoredProcedure("sp_CreateSequence", parameters, true);
            if (dt.Rows.Count > 0)
                return Convert.ToInt32(dt.Rows[0]["Id"]);
            return 0;
        }

        public async Task<List<SequenceModel>> GetSequencesAsync(int accessId)
        {
            var parameters = new List<SqlParameter>
            {
                new SqlParameter("@AccessId", accessId)
            };

            DataTable dt = await _database.RunStoredProcedure("sp_GetSequencesByAccess", parameters, true);
            var sequences = new List<SequenceModel>();

            foreach (DataRow row in dt.Rows)
            {
                sequences.Add(new SequenceModel
                {
                    Id = Convert.ToInt32(row["Id"]),
                    AccessId = accessId,
                    Sequence = row["Sequence"].ToString() ?? "",
                    StateCode = row["StateCode"]?.ToString(),
                    StateName = row["StateName"]?.ToString(),
                    TotalLeads = Convert.ToInt32(row["TotalLeads"]),
                    Status = row["Status"].ToString() ?? "pending",
                    CreatedAt = Convert.ToDateTime(row["CreatedAt"]),
                    CompletedAt = row["CompletedAt"] == DBNull.Value ? null : Convert.ToDateTime(row["CompletedAt"])
                });
            }

            return sequences;
        }

        public async Task UpdateSequenceStatsAsync(int sequenceId, int? totalLeads = null, string? status = null, DateTime? completedAt = null)
        {
            var parameters = new List<SqlParameter>
            {
                new SqlParameter("@SequenceId", sequenceId),
                new SqlParameter("@TotalLeads", (object?)totalLeads ?? DBNull.Value),
                new SqlParameter("@Status", (object?)status ?? DBNull.Value),
                new SqlParameter("@CompletedAt", (object?)completedAt ?? DBNull.Value)
            };

            await _database.RunStoredProcedure("sp_UpdateSequenceStats", parameters, false);
        }

        public async Task<SequenceModel?> ClaimSequenceAsync(int accessId)
        {
            var parameters = new List<SqlParameter>
            {
                new SqlParameter("@AccessId", accessId)
            };

            DataTable dt = await _database.RunStoredProcedure("sp_ClaimSequence", parameters, true);
            if (dt.Rows.Count == 0) return null;

            DataRow row = dt.Rows[0];
            if (row["Id"] == DBNull.Value) return null;

            return new SequenceModel
            {
                Id = Convert.ToInt32(row["Id"]),
                Sequence = row["Sequence"].ToString() ?? "",
                StateCode = row["StateCode"]?.ToString(),
                StateName = row["StateName"]?.ToString()
            };
        }

        public async Task CompleteSequenceAsync(int sequenceId, int totalLeads, string status = "completed")
        {
            var parameters = new List<SqlParameter>
            {
                new SqlParameter("@SequenceId", sequenceId),
                new SqlParameter("@TotalLeads", totalLeads),
                new SqlParameter("@Status", status)
            };

            await _database.RunStoredProcedure("sp_CompleteSequence", parameters, false);
        }
    }
}
