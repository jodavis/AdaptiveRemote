
namespace AdaptiveRemote.Services.TiVo;

public class TiVoSettings
{
    /// <summary>
    /// If true, TiVo services will not start, but all TiVo commands will
    /// be enabled as no-ops.
    /// </summary>
    public bool Fake { get; set; } = false;

    // TODO: Remove this when ScanningTiVoLocator isn't using it anymore
    public string? IP { get; set; }
}
