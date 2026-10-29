using NOVA.AI.Client.Infrastructure;
using Y10.AI.Client;
using NOVA.AI.Client.Mcp.Tools;
using System.Security.Cryptography;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// The MCP host reuses the same Application and Infrastructure services as REST.
// It does not call the REST API over HTTP and does not duplicate use-case logic.
builder.Services.AddNovaAiClientInfrastructure(builder.Configuration);
builder.Services.AddY10AiClient(builder.Configuration);

builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithTools<NovaLoggingTools>()
    .WithTools<NovaAiTools>();

// Default to loopback. Override Mcp:Url only when deploying behind an appropriately
// secured HTTPS endpoint/reverse proxy.
var mcpUrl = builder.Configuration["Mcp:Url"] ?? "http://127.0.0.1:5280";
builder.WebHost.UseUrls(mcpUrl);



var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    var configuredApiKey = app.Configuration["Mcp:ApiKey"];
    if (string.IsNullOrWhiteSpace(configuredApiKey))
    {
        throw new InvalidOperationException(
            "Configure Mcp:ApiKey using a secret/environment variable before running outside Development.");
    }

    app.Use(async (context, next) =>
    {
        if (context.Request.Path.Equals("/health", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        // Check for the presence of the X-API-KEY header and validate.
        if (!context.Request.Headers.TryGetValue("X-API-KEY", out var suppliedApiKey) ||
            !FixedTimeEquals(suppliedApiKey.ToString(), configuredApiKey))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Unauthorized.");
            return;
        }

        await next(context);
    });
}

// Health check endpoint for monitoring. Returns 200 OK .
app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    service = "NOVA.AI.Client.Mcp"
}));

//path of the MCP server endpoint, e.g., http://
app.MapMcp("/mcp");

app.Run();

static bool FixedTimeEquals(string supplied, string expected)
{
    var suppliedBytes = Encoding.UTF8.GetBytes(supplied);
    var expectedBytes = Encoding.UTF8.GetBytes(expected);

    return suppliedBytes.Length == expectedBytes.Length &&
           CryptographicOperations.FixedTimeEquals(suppliedBytes, expectedBytes);
}

public partial class Program { }
