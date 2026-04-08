using AdaptiveRemote.Backend.CompiledLayoutService.Logging;
using AdaptiveRemote.Contracts;

namespace AdaptiveRemote.Backend.CompiledLayoutService.Endpoints;

public static class LayoutEndpoints
{
    public static void MapLayoutEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/layouts/compiled/active", GetActiveLayout)
            .WithName(nameof(GetActiveLayout))
            .Produces<CompiledLayout>(StatusCodes.Status200OK);
    }

    private static async Task<IResult> GetActiveLayout(
        ILogger<Program> logger,
        ICompiledLayoutRepository repository,
        CancellationToken cancellationToken)
    {
        logger.GetActiveLayoutRequested();

        // For MVP, we use a hardcoded userId. Auth will provide real userId in ADR-168.
        string userId = "mvp-user";
        CompiledLayout? layout = await repository.GetActiveForUserAsync(userId, cancellationToken);

        if (layout == null)
        {
            return Results.NotFound();
        }

        logger.ReturningActiveLayout(layout.Id);

        // Use the LayoutContractsJsonContext for serialization
        return Results.Json(
            layout,
            LayoutContractsJsonContext.Default.CompiledLayout);
    }
}
