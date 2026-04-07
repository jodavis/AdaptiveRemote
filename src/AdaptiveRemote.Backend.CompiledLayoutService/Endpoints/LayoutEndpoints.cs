using System.Text.Json;
using AdaptiveRemote.Backend.CompiledLayoutService.Logging;
using AdaptiveRemote.Backend.CompiledLayoutService.Services;
using AdaptiveRemote.Contracts;

namespace AdaptiveRemote.Backend.CompiledLayoutService.Endpoints;

public static class LayoutEndpoints
{
    public static void MapLayoutEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/layouts/compiled/active", GetActiveLayout)
            .WithName("GetActiveLayout")
            .Produces<CompiledLayout>(StatusCodes.Status200OK);
    }

    private static IResult GetActiveLayout(
        ILogger<Program> logger,
        HardcodedLayoutProvider layoutProvider)
    {
        logger.GetActiveLayoutRequested();

        CompiledLayout layout = layoutProvider.GetActiveLayout();

        logger.ReturningActiveLayout(layout.Id);

        // Use the LayoutContractsJsonContext for serialization
        return Results.Json(
            layout,
            LayoutContractsJsonContext.Default.CompiledLayout);
    }
}
