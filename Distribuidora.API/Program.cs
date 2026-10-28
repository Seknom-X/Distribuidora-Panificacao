using Microsoft.AspNetCore.RateLimiting;
using Distribuidora.API.Security;
using Distribuidora.API.Data;
using Microsoft.EntityFrameworkCore;
var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration
    .GetConnectionString("DistribuidoraDb")
    ?? throw new InvalidOperationException(
        "Conexao DistribuidoraDb nao configurada."
    );

builder.Services.AddDbContext<DistribuidoraDbContext>(
    options => options.UseMySql(
        connectionString,
        new MySqlServerVersion(new Version(8, 4, 0))
    )
);


// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddRateLimiter(o => {
 o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
 o.AddPolicy("login", context => System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
  context.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions {
   PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0
  }));
});
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddSegurancaDistribuidora(builder.Configuration);

var app = builder.Build();
if (args.Contains("--criar-admin", StringComparer.OrdinalIgnoreCase))
{
    await app.CriarAdminLocalAsync();
    return;
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
if (!app.Environment.IsDevelopment()) app.UseHttpsRedirection();
app.UseRouting();
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<Distribuidora.API.Middleware.AuditoriaOperacoesMiddleware>();

app.MapControllers();

app.Run();

