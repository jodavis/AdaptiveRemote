
namespace AdaptiveRemote.Services.TiVo;

public class TiVoSettings
{
    public bool Fake { get; set; } = false;

    // TODO: Remove this when ScanningTiVoLocator isn't using it anymore
    public string? IP { get; set; }
}
