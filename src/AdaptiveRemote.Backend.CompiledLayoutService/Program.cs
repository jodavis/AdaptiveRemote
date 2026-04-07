using AdaptiveRemote.Backend.CompiledLayoutService.Endpoints;
using AdaptiveRemote.Backend.CompiledLayoutService.Logging;
using AdaptiveRemote.Backend.CompiledLayoutService.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Configure JSON serialization to use source-generated context
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AdaptiveRemote.Contracts.LayoutContractsJsonContext.Default);
});

// Register services
builder.Services.AddSingleton<HardcodedLayoutProvider>();

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
