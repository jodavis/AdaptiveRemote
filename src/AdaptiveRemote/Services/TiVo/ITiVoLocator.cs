using System.Net;

namespace AdaptiveRemote.Services.TiVo;

internal interface ITiVoLocator
{
    /// <summary>
    /// Find an EndPoint that can be used to connect to the TiVo.
    /// </summary>
    /// <param name="cancellationToken">
    /// Indicates whether initialization has been cancelled and should
    /// be aborted.
    /// </param>
    /// <returns>
    /// A Task that indicates the status of the search for a TiVo.
    ///   Incomplete - still searching
    ///   Complete - TiVo found
    ///   Faulted - Something went wrong or no TiVo was found
    ///   Cancelled - The cancellationToken was checked and the search
    ///     was cleanly aborted
    /// </returns>
    Task<EndPoint> FindTiVoAsync(CancellationToken cancellationToken);
}
