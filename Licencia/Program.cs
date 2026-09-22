var builder = WebApplication.CreateBuilder(args);

// ========================================================
// SOPORTE PARA MIGRACIÓN AUTOMÁTICA O POR CLI
// ========================================================
if (args.Contains("--migrate") || args.Contains("migrate"))
{
    Console.WriteLine("[MIGRACIÓN] Iniciando migración a PostgreSQL...");
    string connStr = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
                  ?? builder.Configuration.GetConnectionString("Postgres")
                  ?? "Host=69.197.154.138;Port=5490;Database=ATT;Username=postgres;Password=RmgENZmGOSKm9XcsLPYg;";

    string[] searchPaths = [
        Path.Combine(AppContext.BaseDirectory, "schema_postgres.sql"),
        Path.Combine(Directory.GetCurrentDirectory(), "schema_postgres.sql"),
        Path.Combine(Directory.GetCurrentDirectory(), "..", "schema_postgres.sql"),
        @"C:\Users\BOX\Documents\ATT\Licencia\schema_postgres.sql"
    ];

    string? sqlPath = searchPaths.FirstOrDefault(File.Exists);

    if (sqlPath != null)
    {
        try
        {
            Console.WriteLine($"[MIGRACIÓN] Leyendo archivo SQL: {sqlPath}");
            string sql = await File.ReadAllTextAsync(sqlPath);

            Console.WriteLine($"[MIGRACIÓN] Conectando a PostgreSQL...");
            await using var conn = new Npgsql.NpgsqlConnection(connStr);
            await conn.OpenAsync();

            Console.WriteLine($"[MIGRACIÓN] Ejecutando schema completo...");
            await using var cmd = new Npgsql.NpgsqlCommand(sql, conn);
            cmd.CommandTimeout = 120;
            await cmd.ExecuteNonQueryAsync();

            Console.WriteLine("[MIGRACIÓN EXITOSA] ¡Base de datos PostgreSQL configurada con éxito con todas sus tablas, funciones y semillas!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MIGRACIÓN ERROR] Falló la ejecución: {ex.Message}");
        }
    }
    else
    {
        Console.WriteLine("[MIGRACIÓN ERROR] No se encontró el archivo schema_postgres.sql.");
    }
    return;
}

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", corsBuilder =>
    {
        corsBuilder
            .WithOrigins("*")
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("AllowFrontend");
app.UseAuthorization();
app.MapControllers();

app.Run();