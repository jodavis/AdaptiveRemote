using AdaptiveRemote.Backend.CompiledLayoutService.Endpoints;
using AdaptiveRemote.Backend.CompiledLayoutService.Logging;
using AdaptiveRemote.Backend.CompiledLayoutService.Services;
using AdaptiveRemote.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Register services
builder.Services.AddSingleton<ICompiledLayoutRepository, HardcodedLayoutProvider>();

WebApplication app = builder.Build();

ILogger<Program> logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.ServiceStarting();

// Map endpoints
app.MapHealthEndpoints();
app.MapLayoutEndpoints();

string listenAddress = app.Urls.FirstOrDefault() ?? "http://localhost:5000";
logger.ServiceStarted(listenAddress);

app.Run();

// Make Program visible for testing
public partial class Program { }
