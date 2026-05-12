using System.Text.Json;
using AdaptiveRemote.Backend.Common.Logging;
using AdaptiveRemote.Contracts;

namespace AdaptiveRemote.Backend.LayoutCompilerService.Endpoints;

public static class CompileEndpoints
{
    public static void MapCompileEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/compile", CompileLayout)
            .WithName(nameof(CompileLayout))
            .Produces<CompiledLayout>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

        app.MapPost("/compile/preview", CompilePreview)
            .WithName(nameof(CompilePreview))
            .Produces<PreviewLayout>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);
    }

    private static async Task<IResult> CompileLayout(
        HttpContext httpContext,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        using IDisposable _ = logger.StartRequestScope("POST", "/compile");

        RawLayout? raw;
        try
        {
            raw = await JsonSerializer
                .DeserializeAsync(httpContext.Request.Body, LayoutContractsJsonContext.Default.RawLayout, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.CompilationFailed(Guid.Empty, $"Failed to deserialize request body: {ex.Message}", ex);
            return Results.BadRequest("Invalid request body: could not deserialize RawLayout.");
        }

        if (raw is null)
        {
            logger.CompilationFailed(Guid.Empty, "Request body deserialized to null", exception: null);
            return Results.BadRequest("Invalid request body: RawLayout was null.");
        }

        logger.CompilationStarted(raw.Id, raw.Elements.Count);

        CompiledLayout compiled;
        try
        {
            compiled = LayoutCompilationEngine.Compile(raw);
        }
        catch (Exception ex)
        {
            logger.CompilationFailed(raw.Id, ex.Message, ex);
            return Results.BadRequest("Compilation failed.");
        }

        logger.CompilationSucceeded(raw.Id, compiled.Elements.Count);

        string responseJson = JsonSerializer.Serialize(compiled, LayoutContractsJsonContext.Default.CompiledLayout);
        return Results.Content(responseJson, "application/json");
    }

    private static async Task<IResult> CompilePreview(
        HttpContext httpContext,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        using IDisposable _ = logger.StartRequestScope("POST", "/compile/preview");

        IReadOnlyList<RawLayoutElementDto>? elements;
        try
        {
            elements = await JsonSerializer
                .DeserializeAsync(httpContext.Request.Body, LayoutContractsJsonContext.Default.IReadOnlyListRawLayoutElementDto, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.CompilationFailed(Guid.Empty, $"Failed to deserialize preview request body: {ex.Message}", ex);
            return Results.BadRequest("Invalid request body: could not deserialize element list.");
        }

        if (elements is null)
        {
            logger.CompilationFailed(Guid.Empty, "Preview request body deserialized to null", exception: null);
            return Results.BadRequest("Invalid request body: element list was null.");
        }

        logger.PreviewCompilationStarted(elements.Count);

        PreviewLayout preview;
        try
        {
            preview = LayoutCompilationEngine.CompilePreview(elements);
        }
        catch (Exception ex)
        {
            logger.CompilationFailed(Guid.Empty, $"Preview compilation failed: {ex.Message}", ex);
            return Results.BadRequest("Preview compilation failed.");
        }

        logger.PreviewCompilationSucceeded(elements.Count);

        string responseJson = JsonSerializer.Serialize(preview, LayoutContractsJsonContext.Default.PreviewLayout);
        return Results.Content(responseJson, "application/json");
    }
}
