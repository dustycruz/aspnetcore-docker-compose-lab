using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MyApi.Data;
using MyApi.Endpoints;
using MyApi.HealthChecks;
using MyApi.Services;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

var sqlConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' is not configured. " +
        "docker-compose.yml sets it for the container. To run the API from Visual Studio or 'dotnet run', store it once with " +
        "'dotnet user-secrets set \"ConnectionStrings:DefaultConnection\" \"<connection string>\" --project MyApi' " +
        "(see docs/INSTALLATION.md, section 5.4).");

var redisConnectionString = builder.Configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException(
        "Connection string 'Redis' is not configured. Set the ConnectionStrings__Redis environment variable.");

// SQL Server through Entity Framework Core. Transient failures (for example while the
// SQL Server container is still starting) are retried automatically.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(sqlConnectionString, sql => sql.EnableRetryOnFailure(maxRetryCount: 10)));

// One shared Redis connection for the whole application, as recommended by StackExchange.Redis.
// AbortOnConnectFail = false lets the API start, and keep reconnecting, while Redis is unavailable.
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    var options = ConfigurationOptions.Parse(redisConnectionString);
    options.AbortOnConnectFail = false;
    return ConnectionMultiplexer.Connect(options);
});

builder.Services.AddScoped<IProductCache, RedisProductCache>();
builder.Services.AddScoped<ProductService>();

// AddHealthChecks is built into ASP.NET Core; AddDbContextCheck comes from
// Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("sqlserver", tags: ["ready"])
    .AddCheck<RedisHealthCheck>("redis", tags: ["ready"]);

builder.Services.AddProblemDetails();

var app = builder.Build();

if (app.Configuration.GetValue("Database:ApplyMigrationsOnStartup", false))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    app.Logger.LogInformation("Applying EF Core migrations to SQL Server...");
    await db.Database.MigrateAsync();
}

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapGet("/", (IHostEnvironment env) => Results.Ok(new
{
    service = "MyApi",
    environment = env.EnvironmentName,
    host = Environment.MachineName,
    endpoints = new[] { "/api/products", "/health/live", "/health/ready" },
}));

app.MapProductEndpoints();

// Liveness: the process is up. Runs no dependency checks.
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = WriteHealthResponse,
});

// Readiness: SQL Server and Redis are reachable.
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = WriteHealthResponse,
});

app.Run();

static Task WriteHealthResponse(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";
    var payload = new
    {
        status = report.Status.ToString(),
        totalDurationMs = report.TotalDuration.TotalMilliseconds,
        checks = report.Entries.Select(entry => new
        {
            name = entry.Key,
            status = entry.Value.Status.ToString(),
            description = entry.Value.Description,
            durationMs = entry.Value.Duration.TotalMilliseconds,
        }),
    };
    return context.Response.WriteAsync(JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
}
