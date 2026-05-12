using AdaptiveRemote.TestUtilities;

namespace AdaptiveRemote.TestUtilities;

public static class HttpClientExtensions
{
    public static string ReadContentAsString(this HttpResponseMessage response)
        => WaitHelpers.WaitForAsyncTask(response.Content.ReadAsStringAsync);
    
}
