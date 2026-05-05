using System.Security.Claims;
using AdaptiveRemote.Backend.CompiledLayoutService.Logging;
using AdaptiveRemote.Contracts;

namespace AdaptiveRemote.Backend.CompiledLayoutService.Endpoints;

public static class LayoutEndpoints
{
    public static void MapLayoutEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/layouts/compiled/active", GetActiveLayout)
            .WithName(nameof(GetActiveLayout))
            .Produces<CompiledLayout>(StatusCodes.Status200OK)
            .RequireAuthorization();
    }

    private static async Task<IResult> GetActiveLayout(
        ClaimsPrincipal user,
        ILogger<Program> logger,
        ICompiledLayoutRepository repository,
        CancellationToken cancellationToken)
    {
        string? userId = user.FindFirst("sub")?.Value;
        if (userId is null)
        {
            // Should not happen when RequireAuthorization() is in effect and the token
            // is a valid Cognito JWT, but guard defensively.
            return Results.Unauthorized();
        }

        logger.GetActiveLayoutRequested(userId);

        CompiledLayout? layout = await repository.GetActiveForUserAsync(userId, cancellationToken);

        if (layout == null)
        {
            return Results.NotFound();
        }

        logger.ReturningActiveLayout(layout.Id);

        return Results.Json(
            layout,
            LayoutContractsJsonContext.Default.CompiledLayout);
    }
}
