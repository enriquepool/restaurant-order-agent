using System.Text.Json.Serialization;
using OrderAgent.Agent;
using OrderAgent.Api.Endpoints;
using OrderAgent.Api.Llm;
using OrderAgent.Infrastructure;
using OrderAgent.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// --- Servicios ---
builder.Services.AddLlm(builder.Configuration);
builder.Services.AddOrderAgent(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// CORS para futuros clientes web. Las apps móviles nativas no lo necesitan.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// --- Base de datos ---
if (app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
    await app.Services.ApplyMigrationsAsync();

// --- Pipeline HTTP ---
app.UseCors();

if (app.Environment.IsDevelopment())
    app.MapOpenApi(); // /openapi/v1.json

app.MapGet("/", () => Results.Redirect("/health")).ExcludeFromDescription();
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).WithTags("Health");
app.MapGet("/health/ready", async (OrderAgentDbContext db, CancellationToken ct) =>
        await db.Database.CanConnectAsync(ct)
            ? Results.Ok(new { status = "ready" })
            : Results.Problem("No hay conexión con la base de datos.", statusCode: StatusCodes.Status503ServiceUnavailable))
    .WithTags("Health");

app.MapApiV1(app.Environment);
app.MapWhatsAppWebhook();
app.MapAgentCard();

app.Run();

// Permite usar WebApplicationFactory<Program> en pruebas de integración.
public partial class Program;
