using System.Reflection;
using AdaptiveRemote.Backend.CompiledLayoutService.Logging;
using AdaptiveRemote.Contracts;

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
        logger.HealthCheckRequested();

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
