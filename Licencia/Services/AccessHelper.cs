using System.Data;
using ClientAccess.DataBase;
using Npgsql;

namespace ClientAccess.Services
{
    public class AccessHelper
    {
        private readonly Database _database = new Database();

        public async Task<int?> ResolveAccessIdAsync(string accessKey)
        {
            if (string.IsNullOrWhiteSpace(accessKey))
                return null;

            try
            {
                string sql = "SELECT access_id FROM client_access_records WHERE access_key = @key LIMIT 1;";
                var parameters = new List<NpgsqlParameter>
                {
                    new NpgsqlParameter("key", accessKey.Trim())
                };

                DataTable dt = await _database.ExecutePostgresQueryAsync(sql, parameters);
                if (dt.Rows.Count > 0)
                    return Convert.ToInt32(dt.Rows[0]["access_id"]);
            }
            catch
            {
                // Fallback a SQL Server si la instancia aún no migró
                try
                {
                    var sqlParams = new List<Microsoft.Data.SqlClient.SqlParameter>
                    {
                        new Microsoft.Data.SqlClient.SqlParameter("@AccessKey", accessKey.Trim())
                    };
                    DataTable dtSql = await _database.RunStoredProcedure("sp_GetAccessIdByKey", sqlParams, true);
                    if (dtSql.Rows.Count > 0)
                        return Convert.ToInt32(dtSql.Rows[0]["Id"]);
                }
                catch { }
            }

            return null;
        }
    }
}
