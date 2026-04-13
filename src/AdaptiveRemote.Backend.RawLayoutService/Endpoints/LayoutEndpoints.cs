using System.Security.Claims;
using AdaptiveRemote.Backend.RawLayoutService.Logging;
using AdaptiveRemote.Contracts;

namespace AdaptiveRemote.Backend.RawLayoutService.Endpoints;

public static class LayoutEndpoints
{
    public static void MapLayoutEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/layouts/raw", ListRawLayouts)
            .WithName(nameof(ListRawLayouts))
            .Produces<IReadOnlyList<RawLayout>>(StatusCodes.Status200OK)
            .RequireAuthorization();

        app.MapGet("/layouts/raw/{id:guid}", GetRawLayout)
            .WithName(nameof(GetRawLayout))
            .Produces<RawLayout>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .RequireAuthorization();

        app.MapPost("/layouts/raw", CreateRawLayout)
            .WithName(nameof(CreateRawLayout))
            .Produces<RawLayout>(StatusCodes.Status201Created)
            .RequireAuthorization();

        app.MapPut("/layouts/raw/{id:guid}", UpdateRawLayout)
            .WithName(nameof(UpdateRawLayout))
            .Produces<RawLayout>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .RequireAuthorization();

        app.MapDelete("/layouts/raw/{id:guid}", DeleteRawLayout)
            .WithName(nameof(DeleteRawLayout))
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }

    private static async Task<IResult> ListRawLayouts(
        ClaimsPrincipal user,
        ILogger<Program> logger,
        IRawLayoutRepository repository,
        CancellationToken cancellationToken)
    {
        string? userId = user.FindFirst("sub")?.Value;
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        logger.ListRawLayoutsRequested(userId);

        try
        {
            IReadOnlyList<RawLayout> layouts = await repository.ListByUserAsync(userId, cancellationToken);

            return Results.Json(
                layouts,
                LayoutContractsJsonContext.Default.IReadOnlyListRawLayout);
        }
        catch (Exception ex)
        {
            logger.ErrorRetrievingRawLayouts(userId, ex);
            return Results.Problem("Error retrieving raw layouts");
        }
    }

    private static async Task<IResult> GetRawLayout(
        Guid id,
        ClaimsPrincipal user,
        ILogger<Program> logger,
        IRawLayoutRepository repository,
        CancellationToken cancellationToken)
    {
        string? userId = user.FindFirst("sub")?.Value;
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        logger.GetRawLayoutRequested(userId, id);

        try
        {
            RawLayout? layout = await repository.GetAsync(id, cancellationToken);

            if (layout == null || layout.UserId != userId)
            {
                return Results.NotFound();
            }

            return Results.Json(
                layout,
                LayoutContractsJsonContext.Default.RawLayout);
        }
        catch (Exception ex)
        {
            logger.ErrorRetrievingRawLayout(id, userId, ex);
            return Results.Problem("Error retrieving raw layout");
        }
    }

    private static async Task<IResult> CreateRawLayout(
        RawLayout layout,
        ClaimsPrincipal user,
        ILogger<Program> logger,
        IRawLayoutRepository repository,
        ILayoutProcessingTrigger processingTrigger,
        INotificationPublisher notificationPublisher,
        CancellationToken cancellationToken)
    {
        string? userId = user.FindFirst("sub")?.Value;
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        logger.CreateRawLayoutRequested(userId);

        // Validate required fields
        if (string.IsNullOrWhiteSpace(layout.Name))
        {
            return Results.BadRequest(new { error = "Name is required" });
        }

        if (layout.Elements is null)
        {
            return Results.BadRequest(new { error = "Elements is required" });
        }

        try
        {
            // Generate new ID and timestamps if not provided
            RawLayout newLayout = layout with
            {
                Id = layout.Id == Guid.Empty ? Guid.NewGuid() : layout.Id,
                UserId = userId, // Always override with authenticated user
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                Version = 1,
                ValidationResult = null // Clear any validation result from the request
            };

            RawLayout savedLayout = await repository.SaveAsync(newLayout, cancellationToken);

            logger.RawLayoutCreated(savedLayout.Id);

            // Publish notification (stub for now)
            await notificationPublisher.PublishLayoutSavedAsync(userId, savedLayout.Id, cancellationToken);

            // Trigger processing (stub for now)
            await processingTrigger.TriggerAsync(savedLayout.Id, cancellationToken);

            return Results.Created($"/layouts/raw/{savedLayout.Id}", savedLayout);
        }
        catch (Exception ex)
        {
            logger.ErrorCreatingRawLayout(userId, ex);
            return Results.Problem("Error creating raw layout");
        }
    }

    private static async Task<IResult> UpdateRawLayout(
        Guid id,
        RawLayout layout,
        ClaimsPrincipal user,
        ILogger<Program> logger,
        IRawLayoutRepository repository,
        ILayoutProcessingTrigger processingTrigger,
        INotificationPublisher notificationPublisher,
        CancellationToken cancellationToken)
    {
        string? userId = user.FindFirst("sub")?.Value;
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        logger.UpdateRawLayoutRequested(userId, id);

        try
        {
            // Verify the layout exists and belongs to the user
            RawLayout? existingLayout = await repository.GetAsync(id, cancellationToken);
            if (existingLayout == null || existingLayout.UserId != userId)
            {
                return Results.NotFound();
            }

            // Update with new data, preserving system fields
            RawLayout updatedLayout = layout with
            {
                Id = id,
                UserId = userId, // Always override with authenticated user
                CreatedAt = existingLayout.CreatedAt,
                UpdatedAt = DateTimeOffset.UtcNow,
                Version = existingLayout.Version + 1,
                ValidationResult = null // Clear validation result on update
            };

            RawLayout savedLayout = await repository.SaveAsync(updatedLayout, cancellationToken);

            logger.RawLayoutUpdated(savedLayout.Id);

            // Publish notification (stub for now)
            await notificationPublisher.PublishLayoutSavedAsync(userId, savedLayout.Id, cancellationToken);

            // Trigger processing (stub for now)
            await processingTrigger.TriggerAsync(savedLayout.Id, cancellationToken);

            return Results.Json(
                savedLayout,
                LayoutContractsJsonContext.Default.RawLayout);
        }
        catch (Exception ex)
        {
            logger.ErrorUpdatingRawLayout(id, userId, ex);
            return Results.Problem("Error updating raw layout");
        }
    }

    private static async Task<IResult> DeleteRawLayout(
        Guid id,
        ClaimsPrincipal user,
        ILogger<Program> logger,
        IRawLayoutRepository repository,
        CancellationToken cancellationToken)
    {
        string? userId = user.FindFirst("sub")?.Value;
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        logger.DeleteRawLayoutRequested(userId, id);

        try
        {
            // Verify the layout exists and belongs to the user
            RawLayout? existingLayout = await repository.GetAsync(id, cancellationToken);
            if (existingLayout == null || existingLayout.UserId != userId)
            {
                return Results.NotFound();
            }

            await repository.DeleteAsync(id, cancellationToken);

            logger.RawLayoutDeleted(id);

            return Results.NoContent();
        }
        catch (Exception ex)
        {
            logger.ErrorDeletingRawLayout(id, userId, ex);
            return Results.Problem("Error deleting raw layout");
        }
    }
}
