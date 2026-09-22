using System.Data;
using Microsoft.Data.SqlClient;
using Npgsql;

namespace ClientAccess.DataBase
{
    public class Database
    {
        private IConfigurationRoot GetConfig()
        {
            string environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
            return new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile($"appsettings.{environment}.json", optional: true)
                .AddEnvironmentVariables()
                .Build();
        }

        public string GetPostgresConnectionString()
        {
            var config = GetConfig();
            string? connStr = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
                           ?? config.GetSection("ConnectionStrings")["Postgres"];

            if (string.IsNullOrWhiteSpace(connStr))
            {
                // Fallback default
                connStr = "Host=localhost;Port=5432;Database=CLIENTACCESS;Username=postgres;Password=postgres;";
            }

            return connStr;
        }

        public string GetSqlConnectionString()
        {
            var config = GetConfig();
            string? connStr = Environment.GetEnvironmentVariable("ConnectionStrings__SQL")
                           ?? config.GetSection("ConnectionStrings")["SQL"];

            return connStr ?? "";
        }

        // ========================================================
        // POSTGRESQL METHODS
        // ========================================================

        public async Task<DataTable> ExecutePostgresQueryAsync(string sql, List<NpgsqlParameter>? parameters = null)
        {
            DataTable dt = new DataTable();
            string connStr = GetPostgresConnectionString();

            await using var conn = new NpgsqlConnection(connStr);
            await conn.OpenAsync();

            await using var cmd = new NpgsqlCommand(sql, conn);
            if (parameters != null && parameters.Count > 0)
            {
                cmd.Parameters.AddRange(parameters.ToArray());
            }

            await using var reader = await cmd.ExecuteReaderAsync();
            dt.Load(reader);

            return dt;
        }

        public async Task<int> ExecutePostgresNonQueryAsync(string sql, List<NpgsqlParameter>? parameters = null)
        {
            string connStr = GetPostgresConnectionString();

            await using var conn = new NpgsqlConnection(connStr);
            await conn.OpenAsync();

            await using var cmd = new NpgsqlCommand(sql, conn);
            if (parameters != null && parameters.Count > 0)
            {
                cmd.Parameters.AddRange(parameters.ToArray());
            }

            return await cmd.ExecuteNonQueryAsync();
        }

        // ========================================================
        // SQL SERVER METHODS (LEGACY FALLBACK)
        // ========================================================

        public async Task<DataTable> RunStoredProcedure(string ProcedureName, List<SqlParameter> lstParameters, bool WaitDtReturn)
        {
            SqlConnection tmpCnn = new SqlConnection(GetSqlConnectionString());
            DataTable dtTMP = new DataTable();
            SqlCommand com = new SqlCommand();

            com.Connection = tmpCnn;
            com.CommandType = CommandType.StoredProcedure;
            com.CommandText = ProcedureName;

            if (lstParameters != null)
            {
                com.Parameters.AddRange(lstParameters.ToArray());
            }

            if (WaitDtReturn)
            {
                SqlDataAdapter daData = new SqlDataAdapter(com);
                daData.Fill(dtTMP);
            }
            else
            {
                tmpCnn.Open();
                com.ExecuteNonQuery();
                tmpCnn.Close();
            }
            com.Parameters.Clear();
            tmpCnn.Dispose();
            return await Task.FromResult(dtTMP);
        }
    }
}
