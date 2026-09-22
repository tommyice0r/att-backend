using System.Data;
using ClientAccess.DataBase;
using Microsoft.Data.SqlClient;

namespace ClientAccess.Services
{
    public class ConfigService
    {
        private readonly Database _database = new Database();

        public async Task<Dictionary<string, string>> GetConfigAsync()
        {
            DataTable dt = await _database.RunStoredProcedure("sp_GetGlobalConfig", new List<SqlParameter>(), true);
            var config = new Dictionary<string, string>();

            foreach (DataRow row in dt.Rows)
            {
                string key = row["Key"].ToString() ?? "";
                string value = row["Value"]?.ToString() ?? "";
                config[key] = value;
            }

            return config;
        }

        public async Task SaveConfigAsync(string key, string value)
        {
            var parameters = new List<SqlParameter>
            {
                new SqlParameter("@Key", key),
                new SqlParameter("@Value", value)
            };

            await _database.RunStoredProcedure("sp_SaveGlobalConfig", parameters, false);
        }

        public async Task SaveConfigBatchAsync(Dictionary<string, string> config)
        {
            foreach (var kvp in config)
            {
                await SaveConfigAsync(kvp.Key, kvp.Value);
            }
        }
    }
}
