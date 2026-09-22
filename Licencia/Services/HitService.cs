using System.Data;
using ClientAccess.DataBase;
using ClientAccess.Models;
using Microsoft.Data.SqlClient;

namespace ClientAccess.Services
{
    public class HitService
    {
        private readonly Database _database = new Database();

        public async Task<int> SaveHitAsync(HitSaveRequest request, int accessId)
        {
            var parameters = new List<SqlParameter>
            {
                new SqlParameter("@AccessId", accessId),
                new SqlParameter("@LeadId", (object?)request.LeadId ?? DBNull.Value),
                new SqlParameter("@PhoneNumber", request.PhoneNumber),
                new SqlParameter("@FullName", (object?)request.FullName ?? DBNull.Value),
                new SqlParameter("@Address", (object?)request.Address ?? DBNull.Value),
                new SqlParameter("@ZipCode", (object?)request.ZipCode ?? DBNull.Value),
                new SqlParameter("@DeviceMessage", (object?)request.DeviceMessage ?? DBNull.Value),
                new SqlParameter("@DeviceType", (object?)request.DeviceType ?? DBNull.Value),
                new SqlParameter("@HitType", (object?)request.HitType ?? DBNull.Value),
                new SqlParameter("@ProfileRaw", (object?)request.ProfileRaw ?? DBNull.Value)
            };

            DataTable dt = await _database.RunStoredProcedure("sp_InsertHit", parameters, true);
            if (dt.Rows.Count > 0)
                return Convert.ToInt32(dt.Rows[0]["Id"]);
            return 0;
        }

        public async Task<List<HitModel>> GetHitsAsync(int accessId)
        {
            var parameters = new List<SqlParameter>
            {
                new SqlParameter("@AccessId", accessId)
            };

            DataTable dt = await _database.RunStoredProcedure("sp_GetHitsByAccess", parameters, true);
            var hits = new List<HitModel>();

            foreach (DataRow row in dt.Rows)
            {
                hits.Add(new HitModel
                {
                    Id = Convert.ToInt32(row["Id"]),
                    PhoneNumber = row["PhoneNumber"].ToString() ?? "",
                    FullName = row["FullName"]?.ToString(),
                    Address = row["Address"]?.ToString(),
                    ZipCode = row["ZipCode"]?.ToString(),
                    DeviceMessage = row["DeviceMessage"]?.ToString(),
                    DeviceType = row["DeviceType"]?.ToString(),
                    HitType = row["HitType"]?.ToString(),
                    CreatedAt = Convert.ToDateTime(row["CreatedAt"])
                });
            }

            return hits;
        }

        public async Task<int> CountHitsAsync(int accessId)
        {
            var parameters = new List<SqlParameter>
            {
                new SqlParameter("@AccessId", accessId)
            };

            DataTable dt = await _database.RunStoredProcedure("sp_CountHitsByAccess", parameters, true);
            if (dt.Rows.Count > 0)
                return Convert.ToInt32(dt.Rows[0]["Count"]);
            return 0;
        }
    }
}
