using McpServer.Opportunity.Tools;
using McpServer.Opportunity.Services;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using System.Collections.Concurrent;
using Microsoft.Identity.Web;
using Microsoft.AspNetCore.Authentication.JwtBearer;

var builder = WebApplication.CreateBuilder(args);

// In-memory session storage for Copilot Studio compatibility
var activeSessions = new ConcurrentDictionary<string, DateTime>();

// Add Authentication with Azure AD JWT Bearer
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

builder.Services.AddAuthorization();

// Add MCP server services with SSE transport (for Copilot Studio compatibility)
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithTools<OpportunityTools>();

// Register DataverseService
builder.Services.AddScoped<IDataverseService, DataverseService>();

// Add CORS for HTTP transport support in browsers
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod()
              .WithExposedHeaders("Content-Type", "Cache-Control", "X-Request-Id");
    });
});

builder.Services.AddHttpContextAccessor();

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddAzureWebAppDiagnostics();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// Middleware to handle Accept header and Session-ID for Copilot Studio compatibility
app.Use(async (context, next) =>
{
    // If Accept header is missing or doesn't include text/event-stream, add it
    if (!context.Request.Headers.Accept.Any(a => a != null && a.Contains("text/event-stream")))
    {
        context.Request.Headers.Accept = "text/event-stream";
    }

    await next();
});

// Enable CORS
app.UseCors();

// Enable Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/ping", (ILoggerFactory loggerFactory) =>
{
    var logger = loggerFactory.CreateLogger("Ping");
    logger.LogWarning("Ping endpoint called");
    return Results.Ok("pong");
});

// Map MCP endpoints
app.MapMcp("/");
app.MapMcp("/mcp/");

// Add a simple home page
app.MapGet("/status", () => "MCP Opportunity Server on Azure App Service - Ready for use with HTTP transport");

app.Run();
