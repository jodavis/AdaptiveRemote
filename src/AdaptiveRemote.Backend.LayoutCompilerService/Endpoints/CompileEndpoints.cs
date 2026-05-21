using AdaptiveRemote.Backend.Common.Logging;
using AdaptiveRemote.Backend.LayoutCompilerService.Services;
using AdaptiveRemote.Contracts;

namespace AdaptiveRemote.Backend.LayoutCompilerService.Endpoints;

public static class CompileEndpoints
{
    public static void MapCompileEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/compile", CompileLayout)
            .WithName(nameof(CompileLayout))
            .Produces<CompiledLayout>(StatusCodes.Status200OK)
            .ProducesValidationProblem();

        app.MapPost("/compile/preview", CompilePreview)
            .WithName(nameof(CompilePreview))
            .Produces<PreviewLayout>(StatusCodes.Status200OK)
            .ProducesValidationProblem();
    }

    private static async Task<IResult> CompileLayout(
        HttpRequest request,
        LayoutCompiler compiler,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        using IDisposable scope = logger.StartRequestScope("POST", "/compile");

        RawLayout? raw = await request
            .ReadFromJsonAsync(LayoutContractsJsonContext.Default.RawLayout, ct)
            .ConfigureAwait(false);

        if (raw is null)
        {
            return Results.BadRequest("Request body must be a valid RawLayout.");
        }

        CompiledLayout result = compiler.Compile(raw);

        return Results.Json(result, LayoutContractsJsonContext.Default.CompiledLayout);
    }

    private static async Task<IResult> CompilePreview(
        HttpRequest request,
        LayoutCompiler compiler,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        using IDisposable scope = logger.StartRequestScope("POST", "/compile/preview");

        IReadOnlyList<RawLayoutElementDto>? elements = await request
            .ReadFromJsonAsync(LayoutContractsJsonContext.Default.IReadOnlyListRawLayoutElementDto, ct)
            .ConfigureAwait(false);

        if (elements is null)
        {
            return Results.BadRequest("Request body must be a valid list of RawLayoutElementDto.");
        }

        PreviewLayout result = compiler.CompilePreview(elements);

        return Results.Json(result, LayoutContractsJsonContext.Default.PreviewLayout);
    }
}
