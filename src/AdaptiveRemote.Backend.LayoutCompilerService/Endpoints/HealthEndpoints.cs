using System.Reflection;
using AdaptiveRemote.Backend.Common.Logging;
using AdaptiveRemote.Contracts;

namespace AdaptiveRemote.Backend.LayoutCompilerService.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", GetHealth)
            .WithName(nameof(GetHealth))
            .Produces<HealthResponse>(StatusCodes.Status200OK);
    }

    private static IResult GetHealth(ILogger<Program> logger)
    {
        using IDisposable scope = logger.StartRequestScope("GET", "/health");

        string? version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "unknown";

        HealthResponse response = new(
            ServiceName: "LayoutCompilerService",
            Version: version,
            Status: "healthy"
        );

        logger.HealthCheckSuccessful();

        return Results.Json(response, LayoutContractsJsonContext.Default.HealthResponse);
    }
}
