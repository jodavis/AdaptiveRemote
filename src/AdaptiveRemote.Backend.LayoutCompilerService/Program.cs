using AdaptiveRemote.Backend.Common.Logging;
using AdaptiveRemote.Backend.LayoutCompilerService.Endpoints;

string? logFilePath = null;
for (int i = 0; i < args.Length - 1; i++)
{
    if (args[i] == "--logFile")
    {
        logFilePath = args[i + 1];
        break;
    }
}

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

if (!string.IsNullOrEmpty(logFilePath))
{
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole();
    builder.Logging.AddFile(logFilePath);
}

WebApplication app = builder.Build();

ILogger<Program> logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.ServiceStarting("LayoutCompilerService");

// Map endpoints
app.MapCompileEndpoints();

// Log the configured listen address; fall back to Kestrel's default.
string listenAddress = app.Configuration["ASPNETCORE_URLS"]
    ?? app.Configuration["urls"]
    ?? "http://localhost:5000";
logger.ServiceStarted("LayoutCompilerService", listenAddress);

app.Run();

// Make Program visible for testing
public partial class Program { }
