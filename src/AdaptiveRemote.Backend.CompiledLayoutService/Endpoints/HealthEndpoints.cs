using System.Reflection;
using AdaptiveRemote.Backend.Common.Logging;
using AdaptiveRemote.Contracts;
using Microsoft.OpenApi;

namespace AdaptiveRemote.Backend.CompiledLayoutService.Endpoints;

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

        HealthResponse response = new HealthResponse(
            ServiceName: "CompiledLayoutService",
            Version: version,
            Status: "healthy"
        );

        logger.HealthCheckSuccessful();

        return Results.Ok(response);
    }
}
